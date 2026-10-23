# P2-1-M3｜模拟环境系统验收（统计与矩阵）

2026-10-23 23:32（Asia/Shanghai）。阶段：P2-1「模拟测试环境建设」最终验收。前置：M1（入口死锁修复）通过、M2（模拟工具能力）测试侧通过。

本轮只验收工具与既有系统：不改正式 Core、配置、数值、经验衰减、城堡手动规则、存档结构，不开发新玩法。所有机器数据在忽略的 `artifacts/simulation/`，本文只给结论与证据指针。

---

## 1 执行与测试覆盖

| 项 | 值 |
|---|---|
| 仓库 / 分支 / 提交 | `e:/Git/Idle-CRPG`、`main`、`82788f5cbf4863f3baf7052761cde74ef222b706`（`.git/refs/heads/main` 直读；`git` CLI 不可用） |
| .NET | SDK 10.0.301，运行 net8.0 |
| Godot | 4.7.stable.mono，`--headless -- --smoke-test` |
| 正式配置 | `Idle-CRPG/config`（切片加载时复制到 `artifacts/qa/production-skills-*`） |
| 配置指纹 | 每份产物写入 `config_fingerprint_sha256`（全 CSV 按相对路径排序后哈希） |
| 源码指纹 | 每份产物写入 `source_fingerprint_sha256`（`Idle-CRPG/Scripts/**/*.cs` + `tests/**/*.cs`，排除 bin/obj）；`git_worktree=unknown` 明示未提交改动只能靠指纹区分 |
| 模拟器 | `SimulationRunner`（20Hz 固定步长，真实 `GameSession.Tick(0.05f)`） |
| 初始状态 | 单 warrior 起步（`starting_heroes`），金币 0、无星图节点、无天赋投入、无存档注入 |
| 动作开关 | 版本 1 四开关（A–D 历史场景）与版本 2 `player_policy`（E/F/T）并存；版本 1/2 不得混用 |
| 采样 | `t=0`，前 30 分钟每 1 分钟，此后每 30 分钟；事件时间 = `tick×0.05s` |
| 批次 | 标准 7 场（240/240/240/240/240/30/6 分钟）+ 多 Seed 30 场（A/C/E ×8 Seed、B/D ×3 Seed，全部 240 分钟）+ 同 Seed 双跑 2 场 + 工具自检内 3 场（6 分钟） |
| 有效运行总 Tick | 约 10.7M（≈ 8 880 游戏分钟的地牢内推进，另有 30/6 分钟短场） |
| 未覆盖 | 实机窗口长稳、多显卡后端、其他操作系统、视觉验收；逐技能/攻击者/召唤事实（见第 7 节） |

执行命令（关键）：

```powershell
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj                          # 全量
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --room-entry-checks
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation-tool-checks
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/C_castle_only.json --tag=m3
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/C_castle_only.json --seed=7 --tag=m3s
```

---

## 2 M3-A 验收合同矩阵（先审计后执行）

| 类别 | 现有实现 | 现有断言/证据 | 未覆盖风险 | 本轮最小补齐 |
|---|---|---|---|---|
| 场景配置 | `tests/Simulation/SimulationConfig.cs`（V1 四开关 / V2 `player_policy`） | 工具自检：V1 兼容、V2 显式、20 项非法配置拒绝（未知字段/版本混用/未知策略/未知节点/未知职业/未知技能/越界周期/越界窗口/未知起始职业等）、缺省子对象默认关闭 | 组合爆炸未穷举（例如同时触发 3 个错误的报文只报第一个） | 无（保住已有关键拒绝项，不加穷举） |
| 玩家操作 | `ExecCastle/ExecStarmap/ExecParty/ExecTalents` 全部走正式 `RequestCastleChallenge`/`BuyStar`/`RequestFormation`/`LearnTalent`/`SetTalentLoadout` | 工具自检：显式星图只买 `s_root`/`s_party_1`，无 `s_skill_1` 等额外购买；`talents.mode=none` 无学习/装配；`castle.enabled=false` 时零 `castle_click`；`policy_order` 记录同 Tick 多组到期只执行一组 | 技能/召唤的逐次施放不可结构化观测（见第 7 节） | 新增 `policy_order` 事件与“关闭即静默”断言 |
| 主循环状态 | `GameSession.Tick`：World→Dungeon→Fight→Rescue→Loot→Stairs/Village | 39 场长跑全部退出 0；C 现场链：`wipe`(2161.95s) → `village_trade`(+338, trades=22) → `TravelDungeon` → `dungeon_enter A04-D04` → `kill` 继续；全程 0 条 `SUSPECTED_STUCK` | 城镇/传送表演阶段未做逐帧核对（只有状态事件） | 无（长跑本身即证据） |
| 战斗/入房 | `AdvanceRoomEntry`/`EntryMoveTarget`/`SpatialTolerance` + `--room-entry-checks` | 四方向 × 6×5/7×6/8×7/9×8 × 1/2/3 人 × 门线外 0~0.0019U 共 384 场景；旧的门外 0.00063U/0.19063U 存档恢复 2 例；退出 0（2.1s） | 仅覆盖普通房；召唤入场在长跑中无专门的边界带用例 | 无（沿用 M1 回归） |
| 经验 | `GrantHeroExperience` + 运行器累计经验对账（升级门槛前缀 + 当前经验求增量） | 39 场 `unexplained_ticks=0`；抽样：21.85s 宝箱 basis=8 → +800 hundredths（gap 0→100%）→ 同 Tick 升级余 2 点 | 同 Tick 多击杀且升级时逐笔归因不可分（合并计入 level_up ticks，不判失败） | 无（对账口径写进 `fields.md`） |
| 金币 | `loot_pickup`/`junk_sale`/`starmap_purchase`/`talent_respec` 逐笔流水 | 39 场 `gold_diff=0`（期初 0 + 收入 − 支出 = 期末） | 洗点扣费未在场景中触发（`talent_respec` 路径未被本次运行覆盖） | 无（保留通路，`F` 覆盖拒绝理由） |
| 地牢/城堡 | 事件 `castle_available/click/queued/enter/result/failure`，源码只在 `CompleteDungeon` 记录 victory | C：36 次 `accepted`、8 次胜利、27 次失败、77 次地牢进入，四类事件可分辨；不把“请求接受”当胜利 | 城堡失败的具体致死序列不可观测（只到 `wipe` 级） | 无（`castle_failure` 已区分“离开未通关”） |
| 异常分类 | `AnomalyDiagnoser`（6 类，含限频/限次/独立经验预算） | 工具自检 5 类判别正确；真实运行只出现 `XP_RATE_ZERO`（A=1、C=2~5、E=4~5 等），0 条 `SUSPECTED_STUCK`/`MANUAL_GATE`/`COMBAT_FAILURE_LOOP` | `MANUAL_GATE` 在长跑中未自然出现（A 不点城堡但仍持续刷本，进度不断刷新） | 增加 `policy_order`/停滞现场落盘；`MANUAL_GATE` 保留单测覆盖 |
| 确定性与追溯 | `business_hash`（snapshots/events/ledger/diagnostics）、`metadata.json` | C 同 Seed 双跑哈希与四份机器文件逐字节一致；同 Seed 不同 `--tag` 哈希也一致；配置指纹改动可检出 | 不同 Seed 不要求哈希不同（已分别记录） | 新增源码指纹 + `git_worktree=unknown` |
| 隔离与资源 | 运行器不调用 `SaveStore`/`SettingsStore`；输出只进 `artifacts/` | 代码检索：`tests/Simulation/*.cs` 无存档写入；每场记录 elapsed/µs·tick/行数/字节/托管堆峰值；诊断现场固定截断 | 峰值内存为采样近似（每分钟一次），非精确峰值 | 新增 `perf` 段与 `stalls/` 现场落盘 |

---

## 3 快速定向检查（每次都执行）

| 检查 | 结果 | 证据 |
|---|---|---|
| 构建（游戏工程 + 检查工程） | PASS，0 警告 0 错误 | `-t:Rebuild` 输出 |
| 全量 Core Checks | PASS，退出 0（冷 46.3s / 重建后 30.7~33.2s） | 约 40 组 `PASS:` 段 |
| `--room-entry-checks` | PASS，退出 0（2.1s） | 384 边界带场景 + 旧档恢复 |
| `--simulation-tool-checks` | PASS，退出 0（3.4s） | 见第 2 节各行 |
| Godot headless smoke | PASS，退出 0（3.6s），`SMOKE_OK` | 十段 UI 冒烟 |

---

## 4 标准场景（Seed 20261023，全部 `--tag=m3`）

| 场景 | 分钟 | 章节 | 等级 | 击杀 | 金币 | 重试 | 通关(本图) | 城堡胜 | 金币差额 | 经验未解释 | 诊断标签 |
|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| A_pure_idle | 240 | 1 | 7 | 4133 | 30356 | 0 | 3 | 0 | 0 | 0 | XP_RATE_ZERO×1 |
| B_guided_progress | 240 | 3 | 30/30/30 | 4811 | 123355 | 0 | 50 | 17 | 0 | 0 | XP_RATE_ZERO×4 |
| C_castle_only | 240 | 2 | 18 | 3703 | 56895 | 44 | 7 | 8 | 0 | 0 | XP_RATE_ZERO×2 |
| D_solo_growth | 240 | 2 | 24 | 3980 | 78077 | 35 | 40 | 9 | 0 | 0 | XP_RATE_ZERO×4 |
| E_min_party | 240 | 3 | 30/30/30 | 4716 | 118182 | 0 | 46 | 16 | 0 | 0 | XP_RATE_ZERO×4 |
| F_policy_probe | 30 | 1 | 7/7 | 566 | 4086 | 0 | 3 | 0 | 0 | 0 | XP_RATE_ZERO×1 |
| T_tool_probe | 6 | 1 | 5/5 | 106 | 598 | 0 | 3 | 0 | 0 | 0 | none |

业务哈希（前缀）：A `a41c3077`、B `cdf59645`、C `afa87089`、D `ccd34477`、E `56c4fdec`、F `84597071`、T `218ab6bd`。

A 仍为 Lv7 平台（未点城堡 + 等级差 5 → 0%），但击杀与金币持续增长（4133 / 30356），**不存在入房冻结**：这正是 M1 修复后的预期对照。

---

## 5 多 Seed 批次

### 5.1 A / C / E（8 Seed × 240 分钟，`--tag=m3s`）

A_pure_idle：8/8 Seed 均为第 1 章 Lv7、`gold_diff=0`、`unexplained=0`、`XP_RATE_ZERO×1`，击杀 4066~4136、金币 28950~30356（同一刷本闭环，浮动来自随机刷怪与拾取）。

C_castle_only：

| Seed | 章节 | 等级 | 击杀 | 金币 | 重试 | 本图通关 | 诊断 |
|---|---:|---|---:|---:|---:|---:|---|
| 1 | 2 | 18 | 3700 | 55257 | 51 | 1 | XP_ZERO×5 |
| 2 | 1 | 17 | 3614 | 57813 | 51 | 39 | XP_ZERO×3 |
| 3 | 2 | 20 | 3776 | 61799 | 53 | 17 | XP_ZERO×5 |
| 7 | 1 | 17 | 3721 | 61126 | 65 | 40 | XP_ZERO×2 |
| 42 | 2 | 19 | 3628 | 56083 | 47 | 11 | XP_ZERO×5 |
| 99 | 1 | 16 | 3740 | 58003 | 67 | 33 | XP_ZERO×4 |
| 20261017 | 1 | 16 | 3747 | 58178 | 69 | 33 | XP_ZERO×3 |
| 20261023 | 2 | 18 | 3703 | 56895 | 44 | 7 | XP_ZERO×2 |

E_min_party：8/8 Seed 均到第 3 章 Lv30，`retries=0`，`gold_diff=0`、`unexplained=0`，击杀 4497~4725、金币 111280~118182、本图通关 44~49。说明精确最小扩编在 8 个 Seed 下都能稳定推进，且不产生额外成长动作。

### 5.2 B / D（3 Seed × 240 分钟）

| 场景 | Seed | 章节 | 等级 | 击杀 | 金币 | 重试 | 本图通关 | 差额 | 未解释 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|
| B_guided_progress | 1 | 3 | 30/30/30 | 4753 | 119314 | 0 | 52 | 0 | 0 |
| B_guided_progress | 7 | 3 | 30/30/30 | 4889 | 122904 | 0 | 49 | 0 | 0 |
| B_guided_progress | 20261023 | 3 | 30/30/30 | 4811 | 123355 | 0 | 50 | 0 | 0 |
| D_solo_growth | 1 | 2 | 24 | 4069 | 78569 | 37 | 40 | 0 | 0 |
| D_solo_growth | 7 | 2 | 24 | 4016 | 78875 | 35 | 42 | 0 | 0 |
| D_solo_growth | 20261023 | 2 | 24 | 3980 | 78077 | 35 | 40 | 0 | 0 |

（明细见 `artifacts/m3-multiseed-bd.csv`；差额与未解释列来自每场产物计数。）

### 5.3 重复复跑

| 检验 | 结果 |
|---|---|
| C 同 Seed 双跑（同 `--tag=m3dup`，两次覆盖同目录） | `business_hash` 相同（`afa87089…`），`snapshots/events/ledger/diagnostics` 四份文件逐字节一致 |
| C 同 Seed 不同目录（`--tag=m3` 与 `--tag=m3s`） | 哈希相同，说明确定性不依赖目录或进程 |
| 采样密度独立性（`T_tool_probe` vs `T_tool_probe_sparse`） | `events.csv` 与 `ledger.csv` 逐字节一致，快照行数不同——采集/采样不影响同 Seed 业务结果 |
| 30 场多 Seed 一致性 | `gold_diff=0`、`unexplained_ticks=0` 全部成立（0 例例外） |
| 采集增强前后两次全批重跑 | 标准 7 场与多 Seed 30 场的 `business_hash` **全部不变**（例如 A `a41c3077`、C `afa87089`、E `56c4fdec`）；仅 `metadata.json` 的源码指纹/性能段随代码与耗时更新，说明新增观测代码不改变正式业务结果 |

---

## 6 精确策略、对账与停滞现场

**E 精确性**（Seed 20261023）：仅 3 次购买 `s_root(3)`→`s_party_1(25)`→`s_party_2(50)`，合计 78 金币；编队 `warrior` → `warrior|ranger` → `warrior|ranger|mage` 随席位扩编两步生效；无天赋学习、无额外技能装配事件。F 场景记录到官方拒绝理由：`星图前置未点亮`、`金币不足：需要 3，持有 0`、`职业未解锁(kills=0 need=100)`、`需在战斗技能投入5点`。

**金币闭合**：39 场全部 `期初 0 + 拾取 + 售卖 − 星图扣费 = 期末`，差额 0；C 的 44 次全灭返村后仍闭合（返村售卖计入 `junk_sale`），说明失败返村不重复结算。

**经验闭合**：39 场 `unexplained_ticks=0`；`level_up` 与 `room_event`（宝箱 basis=8）都能与增量对上。

**异常分类**：真实长跑只产生 `XP_RATE_ZERO`（设计平台提示），0 条 `SUSPECTED_STUCK`、0 条 `MANUAL_GATE`、0 条 `COMBAT_FAILURE_LOOP`。C/D 的 44/35 次全灭因击杀持续而未被误判为停滞。

**停滞现场落盘**：M1 修复后长跑不再自然出现 `SUSPECTED_STUCK`，因此用确定性检查直接驱动落盘路径（构造告警 → 写出 → 校验内容），确认文件含 tick、分类、`--seed=` 复跑命令、配置/源码双指纹与完整现场行。真实路径为 `artifacts/simulation/<场景>_<Seed>_<tag>/stalls/stall-<tick>.md`，现场行包含：阶段/章节/难度/POI/楼层/房间/状态、金币与击杀/重试/交易/待批城堡、每名英雄等级/HP/坐标/存活/经验、房间生成与清理标志/尺寸/中心/待拾取、存活怪物清单、预约账本、房间归属容差与是否在房外、入场诊断（门线/朝内/通行边界/深度/停止原因）、复活状态、**入房掩码（pass/engaged/deployed/candidate 十六进制）与 targeting 时钟**、最后进展时间与已停滞时长、领队与移动状态、最近 5 条事件；导航目标字段标注 `unavailable（Core 私有字段未暴露）`。

**失败→返村→重试可持续**（C 现场）：`wipe`(2161.95s) → `village_trade`(+338, trades=22) → `TravelDungeon` → `dungeon_enter A04-D04` → `kill`，无重复奖励、无跳过交易、无假进展。

---

## 7 性能、隔离与指纹

| 指标 | 实测 |
|---|---|
| 240 分钟（288 000 Tick）墙钟 | 4.5–6.8s（30 场多 Seed 批次，均值 5.7s）；标准场 4.7–6.8s |
| 单 Tick 成本 | 15.7–23.7 µs（240 分钟场，含采集与对账） |
| 产物体积 | 1.14–1.51 MB/场（events 1.1万~1.45万行、ledger 3.7千~5.0千行、snapshots 39 行；整分钟结束时含 1 条与末次采样重合的收尾快照） |
| 托管堆峰值 | 27–30 MB（240 分钟场，每分钟采样近似值；6 分钟短场 20~35 MB，受 GC 时机影响） |
| 诊断条数 | ≤ 5/场（`max_alerts`），现场行固定截断（怪物≤6、入场诊断≤4 行） |
| 隔离 | 运行器不含 `SaveStore`/`SettingsStore` 调用；无 `user://` 写入；输出只在 `artifacts/` |
| 指纹可区分性 | 修改任一 CSV 后 `config_fingerprint_sha256` 变化（工具自检实测）；`source_fingerprint_sha256` 覆盖未提交源码；`git_worktree=unknown` 明示 |
| 覆盖保护 | 本轮统一 `--tag=m3/m3s/m3dup`，M2 的 `*_20261023` 目录未被覆盖 |

---

## 8 未覆盖指标与 M3-C 审批提案（未实施）

**仍 `UNAVAILABLE`**（产物中逐场列出，不以 0 顶替）：

| 指标 | 现有替代证据 | 限制 |
|---|---|---|
| 击杀攻击者身份 | 击杀事件有怪物 ID/等级/Boss/奖励经验；`attacker=unavailable` | 无法回答“谁打死了它” |
| 逐技能施放/命中次数 | `SkillAiDiagnostics`（有界文本）、`ActiveBuffCount/ActiveFlightCount` | 只有当前窗口摘要，非逐次事实 |
| 逐技能伤害与承伤分解 | `HitCount`、英雄 HP 变化、`LastDamage`（单次） | 无法按技能汇总 |
| 召唤生命周期 | 召唤数量/HP 在快照与诊断文本中可见 | 无召唤/消失事件 |
| 城堡失败致死序列 | `wipe`+`castle_failure` | 只到“全灭”粒度 |

**提案**（标记 `CORE_READONLY_FACTS_PENDING_APPROVAL`，本轮**未改**任何正式 Core）：在 `SettleDamage`（击杀/命中）、`GrantHeroExperience`（经验授予）、`SkillRuntime.Commit`（施法）、`SummonRuntime.SummonEffect`（召唤）四处读取既有事实写入一个**有界环形缓冲**（建议 256~512 条，`{Tick,Kind,ActorId,TargetId,SkillId,Amount,Level}`），默认关闭、仅测试入口开启、固定容量无扩容、不入存档、不改变随机数与动作次序；验证方法为“开/关采集两次运行 `business_hash` 必须一致”。对 P2-4 的影响：缺少该接口则只能用聚合计数与文本诊断验证技能数值，无法逐项核验全部技能/战斗参数。

---

## 9 结果分级

| 验收项 | 结论 | 证据 |
|---|---|---|
| 场景配置合法性与拒绝 | **PASS** | 工具自检 20 项拒绝 + 默认关闭 |
| 玩家操作精确执行 | **PASS** | E 仅 78 金币三节点、编队两步；关闭即静默；`policy_order` |
| 主循环推进与失败重试 | **PASS** | 39 场长跑；C 现场链；0 `SUSPECTED_STUCK` |
| 入口死锁回归 | **PASS** | `--room-entry-checks` 384 场景 + 旧档恢复 |
| 经验可解释性 | **PASS** | 39 场 `unexplained=0` |
| 金币闭合 | **PASS** | 39 场 `diff=0` |
| 地牢/城堡可分辨 | **PASS** | C：36 接受 / 8 胜 / 27 失败 / 77 进入 |
| 异常分类口径 | **PASS** | 单测 5 类 + 真实 `XP_RATE_ZERO`；`stalls/stall-<tick>.md` 落盘内容由定向检查验证（含复跑命令与双指纹） |
| 确定性与追溯 | **PASS** | 双跑逐字节一致；跨目录同哈希；双指纹 |
| 隔离与资源有界 | **PASS** | 无存档写入；体积/堆/告警上限实测 |
| 逐技能/攻击者/召唤事实 | **UNAVAILABLE** | 见第 8 节；需 Core 只读接口 |
| `MANUAL_GATE` 在长跑中自然出现 | **NOT_APPLICABLE** | A 不点城堡但持续刷本，进度不断刷新，不构成停滞；该分级由单测覆盖 |
| 洗点扣费路径 | **UNAVAILABLE** | 本轮场景未触发 `ResetTalents`；通路仍在（`TalentChecks` 覆盖扣费） |
| 实机窗口/多平台/视觉 | **UNAVAILABLE** | 仅无头验收 |

---

## 10 裁决

**`M3_TEST_ENV_PASS`。**

- 配置合法性、玩家行为精确性与隔离检查通过；全量 Core Checks、Godot headless smoke、入口/存档回归通过。
- 标准 7 场 + 多 Seed 30 场完成，金币差额 0、经验无无法解释结算、同 Seed 复跑一致、版本/策略/Seed 可追溯。
- 不存在已确认的永久冻结、重复结算、静默跳过合法操作或玩家存档污染。
- 逐技能/攻击者/召唤事实缺失，按第 8 节标 `UNAVAILABLE` 并给出审批提案。

**可启动 P2-2｜全系统配置结构盘点**；同时以 `M3_COMBAT_FACTS_PENDING` 记录：在 Core 只读采集获批并回归前，**不得宣称已具备 P2-4 全技能逐项数值核验能力**。

---

## 11 变更文件与边界

| 类型 | 文件 |
|---|---|
| 新增 | `tests/Simulation/T_tool_probe_sparse.json`（采样独立性对照场景） |
| 修改（仅测试） | `tests/Simulation/SimulationRunner.cs`（`--seed`/`--tag`、源码指纹与 perf 元数据、停滞现场落盘、`policy_order` 同 Tick 单组执行）、`tests/Simulation/SimulationRecord.cs`（`SourceFingerprint`）、`tests/SimulationToolChecks.cs`（指纹/性能/采样独立性/关闭静默/次序断言）、`tests/Program.cs`（`--seed`/`--tag` 解析） |
| 未改 | `Idle-CRPG/Scripts/**`、`Idle-CRPG/config/**`、场景文件、存档结构、旧 A–E 场景语义、既有断言 |
| 文档 | 本文 + 模块 review（`docs/reviews/2026-10-23_p0_a_simulation.md`）M3 小节 + `docs/README.md` 索引 |

---

## 12 复跑与数据目录

```powershell
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj                                     # 全量（含工具自检）
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation-tool-checks
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --room-entry-checks
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/A_pure_idle.json --tag=m3
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/C_castle_only.json --seed=7 --tag=m3s
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/T_tool_probe_sparse.json
& "<Godot 4.7 mono>_console.exe" --headless --path Idle-CRPG -- --smoke-test
```

原始数据（忽略目录）：

- `artifacts/simulation/<场景>_<Seed>_m3/`：标准 7 场（含 `metadata.json`/`fields.md`/`diagnostics.md`）
- `artifacts/simulation/<场景>_<Seed>_m3s/`：多 Seed 30 场
- `artifacts/simulation/C_castle_only_20261023_m3dup/`：同 Seed 双跑
- `artifacts/simulation/T_tool_probe*`：工具自检与采样对照
- `artifacts/m3-multiseed-ace.csv`、`artifacts/m3-multiseed-bd.csv`：多 Seed 汇总
- `artifacts/qa/audit-aud001-stall/`、`artifacts/audit-probe/`：M1 死锁现场与探针日志（未覆盖）
