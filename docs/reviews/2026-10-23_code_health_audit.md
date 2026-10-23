# 工程代码健康审计（只读）执行归档

2026-10-23。范围：按《Steam D2_工程代码健康审计执行方案 V1.0》对当前仓库做一轮**只读**审计：环境快照、核心调用链、按故障后果审计、现有检查与经济模拟实跑、风险分级与最小修复建议。**本轮不改正式代码、配置、数值、存档格式与既有断言**，只新增本报告、`docs/README.md` 索引行与忽略的 `artifacts/` 日志。

结论先行：**发现 1 个 P0 —— 单入口坐标带死锁**，可让单人队伍在房间门口永久停滞（无战斗、无移动、无收益，且存读档无法恢复）。它同时解释了历史 P0-B 数据报告中 A/C/D 三个场景的“疑似入口资格问题”。**未发现第二处会导致崩溃、重复结算或存档损坏的 P0**；金币对账、同 Seed 复现、全量 Core Checks 与 Godot 冒烟均通过。建议先修该 P0 再推进 P0-A/P0-B/P0-C 基线。

---

## 一、结论总览

| 问题 | 结论 |
|---|---|
| 是否建议继续实施 P0-A | **建议在 AUD-001 修复并加回归之后再固化 P0-A/P0-B/P0-C 基线**；P0-A 的配置结构与四开关实现本身可继续，不需要推倒 |
| 是否存在 P0 阻断 | **是，1 项**：AUD-001 入口坐标带死锁（已复现、已定位根因、已证明存读档无法恢复） |
| 是否存在 P1 | 2 项：AUD-002（阈值语义分散，AUD-001 的结构性根因，建议同批修）、AUD-003（纯挂机基线的 Lv.7 平台期，属设计但会扭曲模拟口径） |
| P2/P3 | 9 项：存档恢复过宽捕获、缺表静默降级、Core 状态可变字段外露、读档重复重建、旧模拟时间分辨率、统计归因偏差、配置残留等 |
| 审计置信度 | 运行类结论为**实测**（命令、退出码、日志均在本文）；根因链为**代码确定 + 实测复现**；触发概率为**推算**（已标注） |
| 未覆盖 | 实机窗口长稳（仅无头/无渲染）、多显卡后端、其他操作系统、视觉验收、天赋/星盘编辑器外部冲突的人工操作验收 |

---

## 二、环境/版本与执行记录

### 2.1 快照

| 项 | 值 |
|---|---|
| 仓库根 | `e:/Git/Idle-CRPG` |
| 分支 / 提交 | `main` / `82788f5cbf4863f3baf7052761cde74ef222b706`（`.git/refs/heads/main` 直读） |
| Git CLI | **不可用**（`git` 不在 PATH），因此 `git status` 与未提交改动清单**无法给出**；`.git/index` 最后写入 2026-10-23 20:45:01 |
| .NET SDK | 10.0.301（`C:\Program Files\dotnet\sdk`），运行时 net8.0 |
| 游戏工程 | `Idle-CRPG/Idle-CRPG.csproj`，`Godot.NET.Sdk/4.7.2`，`net8.0`，`Nullable=enable` |
| 检查工程 | `tests/IdleCrpg.Checks.csproj`，`net8.0`，`RollForward=Major`，链接 `Scripts/Core/*.cs` 与 `MotionPresentation.cs` |
| Godot | `D:\Program Files (x86)\Godot_v4.7-stable_mono_win64\`（`config/features = 4.7, C#, GL Compatibility`） |
| 正式配置根 | `Idle-CRPG/config`（`chapters/act1..act5` 为正式地图；`legacy/` 仅旧回归） |
| 存档/输出 | 玩家 `user://`；自动化与检查写 `artifacts/qa/<guid>`（`--smoke-test`/`--capture`/`--persistence-*`）、`artifacts/economy`、`artifacts/simulation/<scenario>_<seed>`。`.gitignore` 已忽略 `artifacts/`、`**/bin/`、`**/obj/` |

### 2.2 实际执行的命令

| # | 命令 | 结果 |
|---|---|---|
| 1 | `dotnet build tests/IdleCrpg.Checks.csproj` | 退出 0，**0 警告 0 错误** |
| 2 | `dotnet run --no-build --project tests/IdleCrpg.Checks.csproj` | 退出 0，**45s**，全部 `PASS:` 段落（约 40 组），含存档/星图/GM/入场/索敌/召唤/章节/城镇/天赋/技能 BD |
| 3 | `--economy-sim --economy-minutes=240` | 退出 0，**8.1s**，A/B 两策略 30/120/240 分钟数据（下文 AUD-003） |
| 4 | `--simulation tests/Simulation/A_pure_idle.json` | 退出 0，6.3s |
| 5 | `--simulation tests/Simulation/C_castle_only.json` | 退出 0，8.6s；**复跑两次 `snapshots.csv` 完全一致** |
| 6 | `--simulation tests/Simulation/B_guided_progress.json` | 退出 0，6.1s |
| 7 | Godot `--headless --path Idle-CRPG -- --smoke-test` | 退出 0，4.3s，输出 `CLASS_TALENT/HERO/CLIENT/SCENE/HUD/CHAPTER/ACT1/GM/STAR/SKILL_PREVIEW` 十段 `*_OK` 与 `SMOKE_OK` |

工作区变化：仅新增本文件、`docs/README.md` 一行索引，以及忽略目录 `artifacts/`（模拟快照、检查草稿、审计探针与日志，见第九节）。未修改任何 `Scripts/**`、`config/**`、`tests/**`、场景或项目设置。

---

## 三、核心架构与状态流

### 3.1 实际调用链

```text
Main._Ready → Config.Load(config) → ChapterMaps[0..4]
            → SaveStore.Load → SaveDocument.Decode → GameSession.Restore(校验/迁移/重建房间怪物)
Main._Process → AdvanceSimulation(delta) → while(acc>=0.05) session.Tick(0.05)
Tick:
  AdvanceSwapLocks → World.ObserveApprovalWait
  ├─ World.Stage != Dungeon → WorldCampaign.Tick（传送表演→城镇/村庄→室外路线→BeginDungeon）
  └─ World.Stage == Dungeon
       TickDungeon
         ├─ 全灭 → RespawnParty（World.RecordFailure→RespawnInVillage，保留成长与资源）
         ├─ 未清房 → AdvanceRoomEntry（入场通行）→ Fight
         │     Fight = AdvanceSkills → DrainEffects → PrepareMonsterTargets → 友方Act → 敌方Act → DrainEffects
         ├─ 清房 → Rescue（逐人读条复活）→ CollectLoot（走近拾取）→ 房间事件 → 楼梯集合 → EnterFloor/Won
         └─ Won → World.CompleteDungeon（Cleared+返村路线+回城门）
Main.AutoSave → session.CaptureProgress()（主线程复制）→ Task.Run(Encode+原子替换)
```

### 3.2 唯一权威状态源（核查结果）

| 状态 | 唯一写入点 | 结论 |
|---|---|---|
| 金币 | `CollectLoot`/`SellJunk`/`BuyStar`/`ResetTalents`/`GrantGmResources`（均为 `checked`） | 单一实现，UI 无直接写入 |
| 英雄经验/等级 | `GrantHeroExperience`（百分之一经验累加 → 逐级扣减） | 唯一；倒地者同样结算 |
| 房间内容 | `Reveal`（按 `dungeonSeed^FloorIndex^RoomId` 独立随机源） | 首次跨门快照生成，回访不重刷 |
| 击杀/掉落 | `SettleDamage` 单点结算（`!target.Alive` 早退，死亡后不再结算） | 单一入口 |
| 世界阶段 | `WorldCampaign.Plan/Tick/CompleteDungeon/RespawnInVillage/ReturnForFormation` | 单一状态机 |
| 接敌锁 | `PrepareMonsterTargets`（唯一账本 `Targeting.Locks`）+ `ReleaseCombatTargets` | `monsterLockIndex` 只是索引，不存第二份目标 |
| 存档 | `CaptureProgress` → `SaveStore.WriteEncoded`（串行后台 + `File.Replace` 原子替换 + `.bak`） | 主线程只复制分离副本，编码/落盘在后台 |
| UI | 只读 `session` 字段、经命令（`RequestCastleChallenge`/`BuyStar`/`LearnTalent`/`SetTalentLoadout`/`RequestFormation`）改状态 | 生产路径干净；但靠约定，见 AUD-006 |

重复实现/并行路径核查：
- 旧 `CastTalent` 与新 `skill_id` 路径**共用**同一套冷却/恢复预扣与效果队列（`SkillRuntime.cs:213` 在分支前统一记账），差异仅 `CastTalent` 的 `ApplyBuff` 未写 `originCast`，属历史兼容，未发现 CD/数值偏离。
- 城堡入口唯一：`HudLayout` 交互卡 → `GameSession.RequestCastleChallenge` → `WorldCampaign.RequestCastle`（解锁、已占领、重复排队全部校验）；未发现绕过路径。
- `Move` 一层通用位移，但“到点/在房内”的判定阈值有四处且各不相同（AUD-002）。

---

## 四、风险发现

### AUD-001（P0｜已复现）入口坐标带死锁：单人队伍在门口永久停滞，且存读档不可恢复

| 字段 | 内容 |
|---|---|
| 证据强度 | **已复现**（两次复跑逐字节一致的快照）+ 代码确定 |
| 证据定位 | `Scripts/Core/GameSession.cs:431-435`（进房触发阈值 0.32）→ `Scripts/Core/CombatTargeting.cs:181`（`Move(hero, PartyDoorWaypoint(...))`）→ `Scripts/Core/SummonRuntime.cs:216-235`（`PartyDoorWaypoint`/`CorridorWaypointTowards` 返回门线上的点）→ `Scripts/Core/GameSession.cs:562-566`（`Move` 在剩余距离 ≤0.001 时直接不动）→ `Scripts/Core/CombatTargeting.cs:111-120`（`EntryPassageBlocked` 封锁行动）→ `Scripts/Core/CombatTargeting.cs:138-142`（`InCombatRoom` 容差仅 0.0001） |
| 复现条件 | `dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/C_castle_only.json`（Seed 20261023、单 warrior、开城堡点击、240 分钟）。`artifacts/simulation/C_castle_only_20261023/snapshots.csv` 在 `sim_time=1307.35s` 之后所有采样冻结：金币 2980、击杀 341、阶段 `Dungeon`、地图 `A03-D03`，直到 240 分钟。两次运行 `snapshots.csv` 完全一致 |
| 现场（探针实测） | 英雄 `(0.00000, 9.49937)`；房间 #2 边界 `Y[9.500, 14.500]`；`InCombatRoom=false`；`EntryDepth=-0.00063`；`CombatEntryRemaining=0`；预约账本 `locks=` 为空；`EntryDiagnostics`：`通行中 · 目标无 · 停止原因=穿门或走廊`；房内 4 只 Lv6 敌人满血不动。再推进 60 秒英雄位移 **0.00000** |
| 因果链 | ①`Travel` 在队首距门点 ≤0.32 时就切房（此时可能仍在门外最多 0.32U）；②入场推进把门外单位指向**门线本身**；③单步位移 0.19U（Speed 3.8 × 0.05）在门线前留下 0.00063U 余量；④`Move` 把 ≤0.001U 当作已到达，不再前进；⑤`InCombatRoom`/`WithinRoom` 只放行 ≤0.0001U/≤0.00001U，于是单位永远“不在房内”；⑥`EntryPassageBlocked` 使英雄不攻击，敌方候选掩码只含房内友方→怪物也不锁敌；⑦房间不清、无人死亡→不触发 `RespawnParty`，无任何超时兜底 |
| 影响 | 单角色队伍（新开档的唯一合法配置）在 20–90 分钟内高度可能命中；命中后**玩法完全冻结**：无击杀、无经验、无金币、无失败返村；**存档只记录位置，读档后同一死锁重现**（实测：读档后 120 秒击杀 341→341；仅把坐标内推 0.01U 后 30 秒击杀 341→356）。违反 `世界自动循环不得停滞` 与“无无法恢复状态”的验收底线 |
| 最小修复方案 | 二选一，建议 A 为主 + C 兜底：<br>**A（1 行级）**：`AdvanceRoomEntry` 中当房外路径点与当前位置距离 ≤`Move` 阈值时，改用房内推进点 `EntryAdvancePoint(...)`（或直接 `goal`），避免“以自身位置为目标”的空转；<br>**B（对齐阈值）**：`Move` 在 `distance<=0.001f` 时吸附到目标（最大位移 1mm，对既有 `dt=1,speed=min(budget,distance)` 调用等价），使“到点”与“在房内”判定口径一致；<br>**C（看门狗）**：跑图/入场阶段记录连续无位移 Tick，超过约 2 秒则把该单位推进到入场推进点（保留连续移动语义，只做异常恢复） |
| 定向回归 | `tests/RoomEntryChecks.cs` 增加：四方向 × 四房间尺寸 × 队伍 1/2/3 人 × 步长网格（含 `door − step*⌈d/step⌉ ∈ (0, 0.001]` 的构造用例），断言“进门后 1 秒内 `InCombatRoom==true`”；再加一条“停滞 60 秒必然已进入战斗或已返村”的不变量 |
| 当前处置 | **建议立即修（P0-A 之前）** |

### AUD-002（P1｜代码确定）到点/在房内的判定阈值分散且互不一致（AUD-001 的结构性根因）

| 字段 | 内容 |
|---|---|
| 证据强度 | 代码确定；与 AUD-001 的实测现场一致 |
| 证据定位 | `Move` 到达阈值 `0.001`（`GameSession.cs:562-566`）；`InCombatRoom` 容差 `+0.0001`（`CombatTargeting.cs:138-142`）；`WithinRoom` 容差 `+0.00001`（`SummonRuntime.cs:238`）；进房触发 `0.32`（`GameSession.cs:431`）；楼梯集合 `0.35`（`GameSession.cs:223`）；拾取 `0.35`（`GameSession.cs:600`）；召唤跟随 `0.12`/`0.001`/`0.00001`（`SummonRuntime.cs:354,366,381`） |
| 影响 | 任意一对阈值之间的空档（如 `(0.0001, 0.001]`）都会形成“既不满足在房内、又不足以再走一步”的死区。AUD-001 只是其中最严重的一种表现；同类死区也可出现在召唤物入场部署与跟随 |
| 修复建议 | 抽出带中文注释的统一定义（如 `ArrivalEpsilon=0.001`、`RoomBoundaryEpsilon`、`EntryTriggerDistance`），要求 `RoomBoundaryEpsilon < ArrivalEpsilon` 的**顺序**被断言，而非散落魔数 |
| 当前处置 | 与 AUD-001 同批修 |

### AUD-003（P1｜实测）纯挂机基线恒为 Lv.7 平台期：属已批准设计，但会直接扭曲 P0-B/P0-C 的“纯挂机”结论

| 字段 | 内容 |
|---|---|
| 证据强度 | 实测（`--economy-sim` 与 A/C 模拟）+ 配置确定 |
| 证据定位 | `config/hero/experience_level_rate.csv`（等级差 5 → 0%）；`config/chapters/act1/world/region_prerequisite.csv`（`ap2_0: region 11 ← A01-C01`）；`Scripts/Core/HeroExperience.cs:22-26`；`WorldCampaign` 手动城堡（`HudLayout.cs:162` → `RequestCastleChallenge`） |
| 实测 | `--economy-sim --economy-minutes=240`：A（纯挂机）30/120/240 分钟均为 `warrior:7:0`，金币 3737→15150→19211；B（含成长操作但同样未点城堡）三人均 `7:0`。对照 C（开城堡点击）同一 4 小时达 Lv.10–30、跨到第 2–3 章 |
| 归因 | 英雄 Lv.7 面对的怪物上限为 Lv.2（A01-D01/D02/D03），等级差 5 → 经验倍率 0%；A02 及以后需要清掉 `A01-C01`，而城堡必须玩家手动点击。**不是经验公式错误**；`Experience` 只打印整数部分（`ExperienceFraction` 未列出），因此“7:0”不等于经验被清零 |
| 影响 | 不点城堡的 4 小时纯挂机必然停在 Lv.7/Lv.2 区间；任何以 A 场景为分母的成长、金币、效率曲线都会低估后期内容。这不是 Bug，但**必须作为模拟口径约束写明**，否则 P0-B/P0-C 会把“未点击”误判为“成长断点” |
| 修复建议 | 不改代码与数值。P0-B/C 报告强制包含“含手动城堡点击”的主基线场景，并单列“纯挂机平台期”为设计预期；等级差衰减（`gap≥5 → 0%`）按 AGENTS 待在线/离线目标一并评估 |
| 当前处置 | `P0-B/C 前修`（修的是口径与文档，不是代码） |

### AUD-004（P2｜代码确定）存档恢复把程序异常当作“坏档”，并自动重置进度

| 字段 | 内容 |
|---|---|
| 证据定位 | `Scripts/Core/SaveStore.cs:50-62`（捕获 `InvalidDataException/IOException/UnauthorizedAccessException/JsonException/ArgumentException/InvalidOperationException/NullReferenceException/OverflowException`，两次都失败后 `File.Copy` 留档并 `Reset()` 写入空档，`RecoveryReset=true`），`Scripts/UI/ClientSettings.cs:62-65`（只在之后弹提示） |
| 影响 | 恢复/迁移代码里的真实缺陷（NRE、索引/参数错误、溢出）会被当成“存档损坏”，玩家看到的是“进度已被重置”，只有 `.unreadable-<时间戳>` 副本可人工找回。属可恢复但高惊吓度的数据风险 |
| 修复建议 | 只把 `InvalidDataException/JsonException/IOException` 视为坏档；其余异常记录并**停止自动重置**，交给玩家在设置里手动确认重置（现有二次确认流程已具备） |
| 当前处置 | 可后置（有备份），建议与 AUD-001 批次一起顺手收窄 |

### AUD-005（P2｜代码确定）正式章节缺表时静默退回 legacy 行为

| 字段 | 内容 |
|---|---|
| 证据定位 | `FloorPlanning.cs:18`（缺 `dungeon/floor_plan.csv` 直接 `return`）、`MonsterPools.cs:24/41`（缺 `monster/act_monsters.csv`、`region_pool.csv` 直接 `return`）、`PoiConfiguration.cs:40`（缺 `world/map_policy.csv` 直接 `return`） |
| 影响 | 打包遗漏或改名时，正式章节会静默降级为全局怪物表/无楼层计划，而不是启动报错；表现为“数值突然变了”而非“配置缺失”，排查成本高（但运行不崩） |
| 修复建议 | 保留 legacy 夹具的宽松行为，但正式 `Load` 路径把这些表列为必需（`Required`）并在缺失时抛出带文件名的 `InvalidDataException` |
| 当前处置 | 可后置 |

### AUD-006（P2｜代码确定）Core 状态以公开可变字段外露，UI 只靠约定不写

| 字段 | 内容 |
|---|---|
| 证据定位 | `GameSession.cs:63-66`（`List<Actor> Heroes/Summons` 公开）、`Actor` 的 `Hp/Position/Attack/...` 均为公开字段；`Scripts/UI/*` 的 smoke/capture 分支已有直接赋值（如 `Main.cs:96,117`、`TargetingSmoke.cs:31,33,41`） |
| 影响 | 目前生产路径干净（`Refresh` 只读），但“UI 只能经命令改状态”没有编译期保护，新增 UI 代码极易越权改战斗现场；同时 `CaptureProgress` 的正确性依赖“主线程只在自己的步内复制”这一约定 |
| 修复建议 | 中期把 `Actor` 的战斗现场字段收窄为 `internal set`，对 UI 暴露只读投影；短期至少在 `docs/technical/architecture.md` 明确“UI 禁写”的清单 |
| 当前处置 | 可后置 |

### AUD-007（P3｜代码确定）配置目录残留 translation 文件

`Idle-CRPG/config/hero/hero_unlocks.kills.translation`（598B）仍在配置目录。AGENTS 规定 CSV importer 统一 `keep`，误生成的翻译残留须移到忽略的 `artifacts/legacy-csv-translations/`。该文件已被 `.gitignore` 的 `**/config/**/*.translation` 忽略，运行时读取走 `File.ReadAllText`，**不影响行为**；但违反目录约定，且会随工程拷贝扩散。建议移出并保持忽略规则。处置：可后置。

### AUD-008（P3｜代码确定）`checkpoint_seconds` 没有运行时消费者

`config/system/client.csv` 有 `checkpoint_seconds=5`，`Config.ClientConfig.cs` 校验并暴露，但生产代码只有 `AutosaveMinSeconds` 参与调度（`ClientSettings.cs:88`）；`checkpoint_seconds` 仅在 `ClientSmoke` 里被用来断言“无状态变化不周期写盘”。属“配置旋钮存在但无效”，易被误当成存档频率。处置：可后置（补文档说明或删除字段，需同步校验与字段字典）。

### AUD-009（P3｜代码确定）读档路径重复执行一次完整 `Restore`

`SaveStore.Load → Decode`（`SaveStore.cs:34`）内部已 `GameSession.Restore`，`Main.ResumeProgress`（`ClientSettings.cs:57`）又调一次。每次读档做两遍校验/迁移/怪物重建，并双份触发 `Note`/`GrantStarterSkill` 的 `MarkChanged`。无正确性问题，是纯浪费。建议 `Decode` 只反序列化与校验参数，恢复统一交给调用方。

### AUD-010（P3｜代码确定）旧 `--economy-sim` 的事件时间戳只有整数分钟

`tests/EconomySimChecks.cs:47,51,87` 用 `minute=tick/1200` 写事件；新入口 `SimulationRunner.cs:38` 用 `tick/20d`（0.05s）精确秒。历史“星图购买事件时间戳不可靠”即由此粒度造成，**不是买卖或扣费异常**（本轮金币对账已证明扣费精确）。旧入口保留兼容但未迁移，建议在报告中标注精度，或把它切到 `tick/20`。

### AUD-011（P3｜代码确定）楼层统计归因偏差

`GameSession.cs:166-170` 在 Tick 开始时取 `tickFloor`，换层那一步的 `BattleTicks/WalkTicks` 记到旧楼层。仅影响 `FloorRunReport` 的分布，不影响玩法与存档。

### AUD-012（P3｜代码确定）`ReturnForFormation` 后未 `return`

`WorldCampaign.cs:161` 在 `ReturnForFormation`（会改成 `TravelVillage`）后继续执行本帧室外路线推进，同一 `dt` 被用两次。影响极小（一次多走 ≤`travel_speed*dt`），但属状态机“一帧两步”的口径瑕疵。

---

## 五、关键场景测试结果

| 检查 | 是否执行 | 结果 / 证据 |
|---|---|---|
| 编译 + 全量 Core Checks | 已执行 | 退出 0、0 警告、45s；约 40 段 `PASS`（见 2.2） |
| Godot 无头冒烟（UI/Core 集成） | 已执行 | 退出 0、4.3s、`SMOKE_OK`；存档落在 `artifacts/qa/<guid>`，未触碰 `user://` |
| 新档 `旅行→入房→战斗→掉落→升级→回城` | 已执行 | C 场景 21.7 分钟进入第 13 个地牢后命中 AUD-001；A 场景在 150–180 分钟之间命中 AUD-001（金币 18729→19211 后冻结） |
| 同 Seed/同配置重复运行 | 已执行 | C 场景两次 `snapshots.csv` **完全一致**（确定性成立）；差异仅执行墙钟（8.6s vs 8.6s） |
| 金币对账（期初+入账−扣款） | 已执行 | A(120min)：1482 + 1498 − 0 = 2980 = 实际，**差额 0**；含星图购买：1487 + 1494 − 253 = 2728 = 实际，**差额 0** |
| 重复命令/边界 | 已执行（回归覆盖） | `StarChecks` 满级/金币不足拒绝、`TalentChecks` 洗点扣费、`HudLayout` 冒烟“重复点击不重复排队”、`SaveChecks`/`ChapterChecks` 备份恢复 |
| 死亡/失败/重试/切图 | 已执行（回归+模拟） | 全灭返村、救援优先级、重试计数均由 Core Checks 覆盖；P0-B 报告 D 场景实测 4 次重试 |
| 存→载→继续 | 已执行（本次定向） | **AUD-001 现场读档后 120 秒击杀数不变**（341→341），证明死锁被写入存档 |
| 20Hz 长程（4 小时） | 已执行（Core，无渲染） | 240 分钟 = 288k tick/场景，单场景 6–9s 墙钟；抽样：71 只怪的战斗态 19.1µs/tick、20k tick 后堆 19.8MB、gen2 GC 2 次 |
| 存档序列化成本 | 已执行（抽样） | 冷启 `CaptureProgress+Encode` 7.4+87.7ms；热态 0.03–0.17ms + 0.11ms，JSON 16KB（3 英雄、3 层、24 房、71 怪）。0.5s 自动保存节流下成本可忽略 |
| 实机窗口 4 小时 / 多后端 / 其他平台 | **未执行** | 无头不等于视觉与长稳验收；本机未做窗口化长跑与多显卡切换 |
| 截图/视觉验收 | **未执行** | 本轮不产出截图；`--capture` 需要人工判读且会占用 `artifacts/` |
| 玩家真实存档 | **未触碰** | 全部自动化走 `artifacts/qa`；未重置、未覆盖任何 `user://` 存档 |

---

## 六、历史报告交叉核验

| 历史结论 | 状态 | 本轮证据 |
|---|---|---|
| “疑似入口资格问题：EntryGateCleared/Act 可能共同造成无移动无攻击，尚不能确认”（`docs/data/2026-10-23_22-12_data_p0_b_growth_stall.md` 第 6–7 节） | **已被新证据确认，并定位到阈值死区** | AUD-001/AUD-002：精确坐标 9.49937 vs 边界 9.500、`depth=-0.00063`、`locks` 为空、读档不恢复、内推 0.01U 立即恢复战斗 |
| 该报告中 A（150–180min 停滞）、C（≤30min）、D（60–90min）三个单场景停滞 | **被同一根因统一解释** | 三者均为单角色队伍；本轮扫描单角色 6/8 Seed 在 17–92 分钟停滞，2/3 角色 0/8 |
| “4 小时卡在 Lv.7，不能直接认定经验公式错误” | **仍然成立（设计使然）** | AUD-003：等级差 5→0%、A02 需手动城堡解锁；含城堡点击的 C/B 同 4 小时到 Lv.10–30 |
| “星图购买事件时间戳不可靠” | **被覆盖：是记录粒度问题，不是扣费异常** | AUD-010（整数分钟）＋金币对账差额 0 |
| “召唤物接敌容量曾为 0” | **已被修复** | `config/skill/summon.csv` 中 `nec_skeleton`/`ran_valkyrie` 均为 1，`--summon-capacity-checks` 通过 |
| “`CastTalent` 与 `skill_id` 并行可能偏离” | **未发现偏离** | 冷却/恢复在分支前统一记账（`SkillRuntime.cs:213-216`），效果队列共用；仅 `ApplyBuff` 未写 `originCast` |
| “五章 `FloorPlanning` 预算不得当作实际” | **仍然成立（无代码错误）** | `FloorRunReport` 分列 `PlannedCells/PlannedKills` 与 `ActualSpawned/ActualKillFacts`；`ActualSpawned` 仅在 `Reveal` 累加 |
| “`travel_speed` 已改为 0.31，防止旧值 0.65 被别的加载路径使用” | **确认一致** | `chapters/act1..5/world/world.csv` = 0.31；根 `world/world.csv` 与 `legacy/act*/` = 0.65，仅供 `LoadLegacy`；正式 `Config.Load` 的章节地图只读 `chapters/act*` |
| “正式城堡挑战必须手动” | **仍然成立** | `RequestCastle` 全部经玩家命令；模拟器仅在 `auto_castle_challenge=true` 时点击，A 场景不点即停在 Lv.7 |
| P0-A 模拟配置结构“部分实施” | **仍然成立** | `SimulationRunner` 四开关、20Hz、按完成 Tick 计时与采样均在位；缺“配置与四开关隔离定向测试”“首次经验归零可靠诊断”等（见 AUD-003/AUD-010） |

---

## 七、建议整改顺序（本轮不实施）

### 第一批：立即堵漏（P0，建议同日回归）

1. **AUD-001 + AUD-002**：修入场推进的“以自身为目标”空转，并统一到点/在房内阈值语义。
   - 影响范围：`Scripts/Core/CombatTargeting.cs`、`Scripts/Core/SummonRuntime.cs`、`Scripts/Core/GameSession.cs`（均为单点小改，无配置与存档格式变化）。
   - 回归成本：`tests/RoomEntryChecks.cs` 扩展参数化用例 + 新增停滞不变量；再跑全量 Core Checks（45s）与 Godot 冒烟（4s），并复跑 C/D/A 三个单角色场景确认不再冻结。
   - 兼容性：修完后单角色队伍的读档不会再卡在同一位置；已卡住的旧存档在修复后应能自行走出（因阈值口径变化），需用本轮停滞快照做一次“读旧档恢复”验收。

### 第二批：保障模拟数据可信（P1，建议在 P0-A/B 基线前）

2. **AUD-003**：把“含手动城堡点击”定为主基线，纯挂机只作平台期参考；报告必须注明等级差 0% 与手动攻城门槛。
3. **AUD-010**：旧 `--economy-sim` 事件改 `tick/20` 或明确标注分钟粒度；把“同 Seed 复跑一致”写进每次统计报告。
4. 复用本轮对账方法（金币四分账）作为每次模拟报告的固定不变量，差额必须为 0 或被解释。

### 第三批：中期整理（P2/P3，按投入排期）

5. AUD-004（收窄存档异常捕获，移除自动重置的“静默”属性）。
6. AUD-005（正式表缺失必须报错）。
7. AUD-006（UI 禁写的编译期/文档期约束）。
8. AUD-007/AUD-008/AUD-009/AUD-011/AUD-012（配置残留、无效旋钮、读档重建两次、统计归因、一帧两步）。

---

## 八、对后续 P0-A/P0-B/P0-C 的影响

| 问题 | 会扭曲什么 | 建议依赖 |
|---|---|---|
| AUD-001 | 任何“长时间运行”的结论：卡住后金币/击杀/经验曲线全部停止，会被误读成“成长断点”“经济饱和”“内容不足”。A/C/D 三个历史场景的停滞即由此产生 | **必须先修**，否则长程数据不可用 |
| AUD-002 | 修复 AUD-001 时的正确性；不统一阈值会以另一种形态复发 | 与 AUD-001 同批 |
| AUD-003 | 成长/经验/金币/关卡各条曲线的基线水平；纯挂机会把 4 小时压成 Lv.7 平台 | P0-B 归因口径前修 |
| AUD-010 | 关键事件时点（首次零经验、首次死亡、城堡点击—开战—胜利时序）的可追溯性 | P0-B/P0-C 报告前修 |
| AUD-004 | 极少数玩家进度被自动重置，影响“存档不损坏”的对外承诺 | 可与 P0-A 并行，不阻断 |
| AUD-005/AUD-006 | 后续内容投放期的“配置改了没生效”“UI 误改战斗现场”类问题 | 可排到 P0-C 之后 |

投放优先级建议：**修 AUD-001/002 → 复跑 A/B/C/D/E 与 240 分钟基线 → 再评估等级差经验衰减与前 2 小时内容/数值**。在 P0 修好之前，不建议用现有长程模拟结果支持任何数值投放决策。

---

## 九、自检与复跑

自检清单：

- [x] 已区分设计选择（AUD-003、共享阈值造成的“设计上允许”表现）、旧报告（第六节逐条状态）、实测缺陷（AUD-001）与推测（触发概率为推算，已标注）
- [x] 每个 P0/P1 均有文件/调用链/行号证据与复现或明确未复现状态
- [x] 明确写出哪些测试执行了、哪些没执行、工作区只新增报告与忽略目录
- [x] 未改正式代码/配置/数值/存档/既有测试规则
- [x] 已给出最小修复批次建议，等待批准后再动手

复跑方法：

```powershell
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --economy-sim --economy-minutes=240
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/C_castle_only.json
# Godot（版本按本机实际路径）
& "<Godot 4.7 mono>_console.exe" --headless --path Idle-CRPG -- --smoke-test
```

AUD-001 判据：C 场景 `artifacts/simulation/C_castle_only_20261023/snapshots.csv` 在 `sim_time≈1307s` 之后 `kills/gold` 是否继续增长；停滞时英雄坐标与房间边界差应落在 `(0.0001, 0.001]`。

机器原始数据与审计探针（均为忽略目录，可整目录删除）：

- `artifacts/simulation/{A_pure_idle,C_castle_only,B_guided_progress}_20261023/`：每场景 `events.csv`/`snapshots.csv`/`summary.md`
- `artifacts/economy/{summary,timeseries,events}.csv`：`--economy-sim` 输出
- `artifacts/audit-probe/`：本轮只读探针（复现停滞、逐 Tick 追踪、存读档恢复验证、金币对账、序列化与 Tick 开销抽样）与 `probe-out.txt`/`sweep*.txt` 日志；不属于工程代码，不参与构建
- `artifacts/qa/<guid>/`、`artifacts/qa/production-skills-*`：检查与冒烟的隔离存档与配置副本

## 十、变更文件

- 新增：`docs/reviews/2026-10-23_code_health_audit.md`（本文件）
- 文档索引：`docs/README.md` 增加一行指向本文件
- 未改：`Idle-CRPG/Scripts/**`、`Idle-CRPG/config/**`、`Idle-CRPG/Scenes/**`、`project.godot`、`tests/**`、存档格式与既有断言
- 未提交、未推送（按仓库规则由用户自行决定）
