# P2-3-M4 只读工程差异审计｜当前工程实际状态（V2 Review 回填版）

- 判定：工程自检 `READY_WITH_OPEN_ITEMS`；设计 Owner 独立 Review `PASS_WITH_OPEN_ITEMS`（允许进入 M5 规划，**不等于 P2-4 开发授权**）
- 版本：**V2**（2026-10-25 13:54 回填 `docs/temp/P2-3-M4 设计Owner独立 Review V1.0.md`）；V1 于 13:11 交付
- 权限：`READ_ONLY_RECON`。本轮未修改任何业务源码、正式配置、存档与关卡数据；未执行 commit/push
- 上游依据：`docs/temp/P2-3｜M3结项与M4工程审计交接包/`（M2 纲要 V1.2、M3 结项 V0.8、M4 任务包 V0.1、预填差异清单 V0.1）＋ `docs/temp/P2-3-M4 设计Owner独立 Review V1.0.md`
- 同目录交付：`M4_DIFF_MATRIX.csv`、`M4_DEPENDENCY_AND_MIGRATION.md`、`M4_OPEN_ISSUES.md`、`M4_EVIDENCE_INDEX.md`

标签口径：`[C]` 已确认（V1.2 或用户 2026-10-25 裁决明文）、`[P]` 策划提案/待批曲线与示例、`[O]` 开放待决（尚未定案）。

---

## 0. 设计 Owner Review 回填对照（V1 → V2）

| Review 项 | 处理 | V2 实际变化 |
|---|---|---|
| R-01 六个 B 项不等于全部编码阻断 | 采纳 | 新增**门禁分层 G-01~G-08**（见 §7 与 `M4_OPEN_ISSUES.md`）；`M4_DIFF_MATRIX.csv` 增列 `门禁分层`；Lmin 仅在"永久购买启用前"阻断（B-01） |
| R-02 工作包人日算术不一致 | 采纳并修正 | WP1–WP12 逐项相加实为 **71–108 人日**，V1 的"65–100"为笔误；两版均为粗估，不作工期承诺（`M4_DEPENDENCY_AND_MIGRATION.md` §5 已改） |
| R-03 "3 项/6 项阻断"表述不一致 | 采纳 | 统一为"**6 个待决主题、4 个编码门禁**"；状态报告结论不再用"3 项裁决"概括（见 §8） |
| R-04 存档迁移不可照搬提案 | 采纳 | 迁移章节新增硬约束：旧等级不得因 XP 折算下降、`Σ个人未花XP` 需真实旧档验证、禁止静默清档、`Reset`/放弃本轮/正式重生三者不同名不同确认；并存样本统计见 §5.3 |
| R-05 引擎/UI 验收环境未就绪 | **已解除** | 本机存在 Godot 4.7.stable.mono（`D:\Program Files (x86)\Godot_v4.7-stable_mono_win64\`）；本轮实跑 **headless 冒烟 PASS、跨进程存档读写 OK、有窗口 display-smoke OK、截图 CAPTURE_OK**（§2.2） |
| R-06 双权威必须替代不能叠加 | 采纳 | 每条退役项在 `M4_DEPENDENCY_AND_MIGRATION.md` §2.1 增"替代性验收条件"，禁止中间态双活 |
| §5 补证：`--param-audit` 输出路径 | 记录为 M5 回归条件 | 本轮**未改测试代码**；作为 G-08 需求登记（历史归档保护），见 `M4_OPEN_ISSUES.md` T-04 |
| §5 补证：旧档迁移样本 | 已采集 | `artifacts/**` 共存 **914 份 `progress*.json`**：Version 1=32、2=109、3=39、4=321，另有 282 份 `"invalid"` 损坏档与 4/1/1、4/2/1/1 内部版本样本（§5.3） |
| §5 补证：git 状态 | 已复核 | PATH 与常见安装位置（`C:\Program Files\Git`、`%LOCALAPPDATA%\Programs`、`E:\Git`、`D:\Program Files*`）**均无 `git.exe`**；分支/提交/未提交变更仍无法判定，替代取证=mtime+逐文件 SHA256 |
| §5 补证：逐技能事实 | 保持待批 | 属 Core 只读事实，需技术负责人批准 P2-2 D-13，本轮不变 |
| 矩阵分类计数变化 | 主动说明 | V1：`NEW 11 / MODIFY 10 / MIGRATE 2 / REMOVE 1 / REUSE 1 / UNVERIFIED 2`；V2：**`NEW 11 / MODIFY 10 / MIGRATE 2 / REMOVE 1 / REUSE 2 / UNVERIFIED 1`** —— 仅 D27 由 `UNVERIFIED` 改判 `REUSE`（引擎已实测可跑），其余 26 行分类未动，差异等级口径无放宽 |

**边界声明**：本文件与其余四份均为**执行 AI 的工程取证**。设计 Owner 的独立 Review 已声明其不直接访问工程目录，故 `STATIC_VERIFIED` 仍指"执行侧读码并提供路径/指纹"，不构成第三方实测。V2 新增的引擎侧结果（§2.2）是本轮真实运行输出，可复跑。

---

## 1. 一句话结论

工程是**成熟可运行的单层存档 + 自动成长增量 RPG**：单 `SaveDocument` → 单 `GameSession`，个人经验自动升级、Armor 减伤仍在生效、怪物等级存在 5 套来源、无 KP、无装备、无卷轴药剂、无成就与重生、无离线结算。
新版 V1.2 的改造不是"补内容"，而是**状态权威分层（Account/Run）+ 经验语义反转（自动→手动）+ 对抗从 Armor 改为 AR/DR + 全新 KP 货币**。骨架可复用；四条中心语义必须重建，其中只有 **Lmin 永久购买**和 **（可选）城堡实杀证明**构成真正的编码门禁，其余按 WP 灰度推进。

---

## 2. 环境与取证方式

| 项 | 实测值 |
|---|---|
| OS / Shell | Windows 10 (win32) / PowerShell |
| .NET SDK | 10.0.301（`dotnet --version`）；工程 target `net8.0` |
| Godot | **`D:\Program Files (x86)\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64(.console).exe`**，版本 `4.7.stable.mono.official.5b4e0cb0f`；渲染 `OpenGL API 3.3.0 NVIDIA 560.94 / RTX 4060 Laptop GPU` |
| 版本控制取证 | `git` 不在 PATH，且 `C:\Program Files\Git`、`%LOCALAPPDATA%\Programs`、`E:\Git`、`D:\Program Files*` 均无 `git.exe` → 分支/提交/未提交变更**无法判定**（UNVERIFIED）；替代取证=mtime + 逐文件 SHA256 前 16 位 |
| 源码基线 | `Idle-CRPG/Scripts/**/*.cs` 最新 mtime 2026-10-23 22:25:47；本轮未改业务代码 |
| 配置基线 | `Idle-CRPG/config/**/*.csv` 195 个，最新 mtime 2026-10-23 20:45:01，与 P2-2 基线一致 |
| 测试侧基线 | `tests/*.cs` 业务文件最新 2026-10-24 01:42 |

### 2.1 .NET 构建与核心检查（本轮实跑）

```powershell
dotnet build tests\IdleCrpg.Checks.csproj        # 0 警告 0 错误（7.8s）
dotnet build Idle-CRPG\Idle-CRPG.csproj         # 0 警告 0 错误（6.2s，Godot .NET 工程可编）
dotnet run --no-build --project tests\IdleCrpg.Checks.csproj   # 全量核心检查 PASS
```

```text
SIM_HASH 218ab6bd1617c274d2b540ca4b433eaf796981ccb1a0f0f6ced3616dad4a38fc
SIM_LEDGER in=626 out=28 expected=598 actual=598 diff=0 unattributed=0
SIM_XP verified_ticks=196 level_up_ticks=10 event_ticks=9 unexplained_ticks=0 verified_hundredths=44480 unexplained_hundredths=0
SIM_DIAG none
SIM_PERF ticks=7200 elapsed_ms=353 us_per_tick=49.0 events=335 ledger=113 snapshots=8 diagnostics=0
```

### 2.2 Godot 引擎侧验证（V2 新增，R-05 门禁已解除）

```powershell
$godot = 'D:\Program Files (x86)\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe'
& $godot --version                                     # 4.7.stable.mono.official.5b4e0cb0f
& $godot --headless --path Idle-CRPG -- --smoke-test          # SMOKE_OK: 交战中
& $godot --headless --path Idle-CRPG -- --persistence-write   # PERSISTENCE_WRITE_OK
& $godot --headless --path Idle-CRPG -- --persistence-read    # PERSISTENCE_READ_OK: automatic startup restore matches saved snapshot
& $godot --path Idle-CRPG -- --smoke-test --display-smoke     # DISPLAY_SMOKE_OK: preset size, fullscreen, restored window size
& $godot --path Idle-CRPG -- --capture --capture-settings     # CAPTURE_OK → artifacts/preview-settings.png（178 KB）
```

无头冒烟逐项 PASS 明细（原样摘录）：`CLASS_TALENT_UI_SMOKE_OK`（五职业树与初始技能）、`INTERACTION_UI_SMOKE_OK`、`TALENT_UI_SMOKE_OK`、`HERO_UI_SMOKE_OK`、`CLIENT_SMOKE_OK`（设置/音频/存读/重置/新远征）、`SCENE_SMOKE_OK`（房间锁定与完整入镜）、`HUD_SMOKE_OK`（城堡卡与队列、地图联动、仅游戏内倍速）、`CHAPTER_UI_SMOKE_OK`、`ACT1_UI_SMOKE_OK`、`GM_SMOKE_OK`、`STAR_UI_SMOKE_OK`（画布购买/草稿保护/三表保存/会话刷新）、`SKILL_PREVIEW_UI_OK`。

**取证边界**：以上是**现存功能的实机自检**，只证明"单窗口骨架、真实控件、存读档与截图能力可用"；**不证明新版 UI/UE 需求已达成**（D21 右侧成长队列、批量换装、离线提示等仍不存在）。

### 2.3 本轮写盘边界

- 标准构建产物：`tests/bin`、`tests/obj`、`Idle-CRPG/.godot/mono/temp/bin`、SDK 生成的 `tests/IdleCrpg.Checks.AssemblyInfo.cs`。
- 检查产物：`artifacts/simulation/**`、`artifacts/preview-settings.png`、Godot 冒烟使用的 `artifacts/qa` 隔离存档（未触碰玩家 `user://` 正式进度）。
- **未**执行 `--param-audit`（其输出目录硬编码指向历史归档，会覆盖旧证据）。

---

## 3. 现有工程骨架（可复用面）

| 层 | 关键类型（`Idle-CRPG/Scripts/Core/*.cs`） | 现状 |
|---|---|---|
| 存档 | `SaveStore`（`SaveDocument.Version=4`）、`SessionSnapshot`、`AppSettings`/`SettingsStore` | 单 `Session` 槽 + 独立 `settings.json`；原子替换/`.bak`/`.unreadable` 保留/自动重置齐备 |
| 会话 | `GameSession`（UI 唯一状态命令入口）、`FormationPlans`、`GmCommands` | 队伍/金币/背包/经验/星图/世界全在一处 |
| 战斗 | `DamagePipeline.Resolve`、`FightAttributes`、`CombatConfiguration`、`SkillRuntime`、`AbilityRuntime`、`ChainRuntime`、`CorpseRuntime`、`PulseRuntime`、`SummonRuntime` | 唯一伤害实现；4 加算池 + 暴击因子已实现；Dmg1/Dmg2 仅明细字段 |
| 成长 | `HeroExperience`、`LevelConfiguration`、`Talents`、`StartingSkills` | 个人经验 + `while` 自动连升；天赋 61 节点/3 系 |
| 星图 | `Stars`、`StarConfiguration`、`StarCsv`、`StarEditorDocument/Store` | 17 节点 6 类效果白名单，仅 `gold`；无重置接口 |
| 世界/章节 | `WorldCampaign`、`ChapterCampaign`、`AreaParts`、`PoiConfiguration`、`FloorPlanning`、`BranchCampaign`、`FailureRecovery` | 5 章 / 3 难度 / 统治区门禁 / 手动支线 / 失败回刷 |
| 地牢 | `Dungeon`、`MonsterSpawning`、`MonsterPools`、`MonsterContent`、`CombatTargeting` | 结构进门前生成、内容首跨门快照、回访不重刷 |
| 表现 | `Scripts/UI/*`：`Main`、`HudLayout`、`SceneCanvas`、`WorldView`、`FollowScene`、`TalentUi`、`StarUi`、`FormationUi`、`HeroPage`、`GmPanel` | 单窗口 1440×900、四页签；右侧为"远征交互"面板 |

---

## 4. 十个审计对象的实测事实

### 4.1 存档/状态权威（D01/D03/D26）

- 只有一层：`SaveStore.cs:9-15` `SaveDocument{Version=4, Session, Speed, ViewMode, SelectedRegion, Paused}`；无 `Account`/`RunState` 类型（`Scripts/**` 0 命中）。
- 队伍、个人经验、金币、背包、星图、章节、城堡进度、`Kills` 全在 `SessionSnapshot`；`Reset()` 一并清零且零回馈（`SaveStore.cs:77-82`）。
- 迁移靠 12+ 内部版本字段 + 6 个迁移器；外层 `Version` 1~4 共用同一 `Decode`，无逐版本分支。
- **无重生、无成就点、无永久商品、无跨轮保留、无离线结算**（`SessionSnapshot.cs:142` 注释"不结算离线时间"）。

### 4.2 战斗与属性（D06/D07）

- 唯一入口 `SkillRuntime.SettleDamage`(103-153) → `DamagePipeline.Resolve`(65-93)；`Dmg1`=basis、`Dmg2`=`CombatOutcome(Dodged,Blocked,Critical)`，**无 Dmg3、无三阶段函数**。
- Armor 仍生效：物理 `K/(armor+K)`（`DamagePipeline.cs:85`），`K=ArmorK(攻击者等级)` 来自 `combat/armor_curve.csv`（1→5、10000→10004）；元素分量不吃护甲；测试已锁死数值。
- **AR/DR 完全不存在**（`fightattr.csv` 无该 Key，`Scripts/**` 搜 `rating` 0 命中），无"评级惩罚"概念。
- 4 加算池 + 暴击独立因子已实现；判定顺序 闪避→格挡→暴击，RNG 消耗顺序被测试锁定。
- 英雄升级只加 `attack_bonus/hp_bonus`，不改 defense、无自然 AR/DR；怪物升级只乘 ATK/HP 倍率。

### 4.3 英雄/队伍/XP（D02/D03/D04/D05）

- 开局 **1 席**；容量 `min(5, max(floor, 1+Σ party_slots))`；同职业重复被 `Distinct` 拒绝；候补池以 `SpecId` 为键；`Actor` 无实例 UID。
- 经验每人独立 + `while` 自动连升；`GameSession.Experience` 仅是 `Heroes[0]` 投影。
- **等级差衰减仍在生效**：`experience_level_rate.csv`（-5=300%、0=100%、+5=**0%**）。
- 无 XP 池上限；换人在城镇 `Formation` 阶段执行，无锁队。

### 4.4 怪物等级与 KP（D10/D11/D12）

- 等级来源 ≥5：`dungeon.level_min/max` → `poi_challenge.challenge` → `floor_plan.MonsterLevel`（**最终等值覆盖**）→ `spawn_rule.level` 偏移；`FloorMonsterLevel`、`difficulty_bands` 有列无消费者；城堡无固定等级。
- **KP 不存在**（`KP|KillPoint` 0 命中）；仅有会话内 `Kills`（职业解锁用）。
- `SpawnModifiers`（LevelBonus/MinBonus/MaxBonus/DropMultiplier）存在但无 UI/科技接线。
- 掉落/金币有配置入口，但"玩家升级改变掉落"机制不存在。

### 4.5 技能与天赋（D08/D09）

- `talent.csv` 61 节点（39 主动/22 被动，每职 3 系）；`skill.csv` 21 行，其中 16 个职业主动（野蛮人 3、游侠 3、法师 2、圣骑士 4、死灵 4）。
- 每职"4 固定技能 + 1 特性"**不成立**；存在技能槽上限（初始 1、上限 5）与装配编辑 UI。
- 旧 `CastTalent` 仍在（`Talents.cs:165`）；23 个主动节点未绑 `skill_id`（39-16），`SkillMigrationChecks.cs` 断言"旧节点全部保留"。
- 前置为单值 `Prerequisite`，无互斥组；洗点仅整角色付费（需星图 `talent_reset`，测试期望 36 金）。

### 4.6 星图/成就/重生（D15/D16/D17）

- 星图 17 节点：`attack 4 / hp 3 / party_slots 4 / skill_slots 4 / talent_reset 1 / talent_reset_discount 1`；白名单 6 类、仅 gold、仅 flat。
- `party_slots`（1+Σ，硬顶 5）与 `skill_slots` 为硬编码功能节点；无"本轮重置"入口。
- **无成就/成就点/永久商店**；职业解锁按**轮内** `Kills`（0/0/0/100/250）。
- 5 章顺序解锁 + 交易后换图；无"首轮四章"切分、无轮次结算。

### 4.7 装备/掉落/回城（D13/D14）

- **无装备系统**：`item.csv` 仅 4 行（gold/fang/cloth/relic），`itemtype` 仅 currency/junk；无部位/品质/属性列；掉落不绑定英雄实例。
- 回城 `SellJunk` 一次卖出全部 auto_sell，无候选保留、无替换事件、无批量换装；`HeroPage` 装备页签为"后续开放"文案。

### 4.8 卷轴/药剂/离线（D18/D19/D20）

- **卷轴、药剂均不存在**（config 无表；NPC 文案"待开放"；HUD 药剂四空槽为占位）。
- 后台：失焦仅触发保存，无补算、无离线结算（`SaveDocument` 无时间戳）；实机 `--display-smoke` 只验证窗口尺寸/全屏，**最小化后的推进行为仍未观测**。
- 与"不自动增强战力"直接冲突的是**当前自动升级**（D04）。

### 4.9 UI 与观测（D21/D23）

- 单窗口成立且**引擎侧已验证**：`Main.tscn` 仅 `Node2D Main`，1440×900，四页签；`--smoke-test` 通过全部 UI 冒烟；无 Mini/Management 残留。
- 右侧面板"远征交互"仅城堡挑战卡与编队计划卡；无升级/换装/天赋入口 → **UI/UE 需求未达成**（仅骨架可用）。
- 观测：会话计数、`RoomCombatStats`（本房聚合）、`FloorRunReport`；金币账本仅在测试侧（`diff=0`）。

### 4.10 配置与测试可复用性（D22）

- `config` 195 CSV；历史清单状态：`ACTIVE 142 / LEGACY 9 / TEST_ONLY 41 / PREVIEW_ONLY 2 / NOT_REFERENCED 1 / NOT_FOUND 1 / GODOT_IMPORT 195`；配置 mtime 未变，**142 ACTIVE 仍成立**。
- 复核与新版直接相关的三项：属性 Key 36（无 AR/DR）、Effect 10 类 + 5 个零投放分支、旧 `CastTalent` 23 个（39-16）—— 与 P2-2 一致；其余 23 公式/31 接口/16 工作包未重跑（`--param-audit` 会覆盖历史归档）。
- 模拟能力：`SIM_HASH/SIM_LEDGER/SIM_XP/SIM_DIAG/SIM_PERF` 齐备、同 Seed 逐字节一致；**缺逐技能施放、召唤生命周期、城堡失败序列**（P2-2 D-13 `PENDING_APPROVAL`）。

---

## 5. 旧↔新冲突与迁移素材

### 5.1 冲突汇总

| 旧工程事实 | V1.2 要求 | 冲突性质 |
|---|---|---|
| 单 `SessionSnapshot` 存档根 | Account/Run 分层，重生清本轮 | 结构重建（MIGRATE） |
| 个人经验 + `while` 自动升级 | 公共 XP 池 + 玩家手动升级 | 语义反转（MODIFY） |
| 等级差经验倍率（+5→0%） | 无等级差衰减、无池上限 | 规则删除（REMOVE） |
| Armor 减伤 K(攻击者等级) | 取消 Armor，用 AR/DR 唯一倍率 | 公式替换（MODIFY） |
| 5 套怪物等级来源 | 单轮唯一 `[Lmin,Lmax]` | 双权威清理（MODIFY/REMOVE） |
| 无 KP | KP 为本轮可消费击杀货币 | 全新（NEW） |
| 开局 1 席 + 星图扩编至 5 | 开局 4 槽、1~4 出战、第 5 席为账号永久商品 | 结构反转（MODIFY + NEW） |
| 同职业重复被禁止 | 允许重复职业、实例独立 | 数据结构改造（MODIFY） |
| 技能槽 + 装配编辑 UI | 4 固定技能 + 1 特性，学会即生效 | 机制删除（REMOVE/MODIFY） |
| 星图 `party_slots`/`skill_slots` | 两者废止 | 效果下架（REMOVE） |
| 整角色付费洗点 | 单点互斥 + 免费局部洗点 | 新模型（NEW） |
| 无装备/卷轴/药剂/成就/重生/离线 | 均为新版核心内容 | 全新（NEW） |

### 5.2 引擎侧结论（新增）

- 单窗口骨架、真实控件、存读档、截图取证能力**可用**，可作为 P2-4 UI 工作包的验收基础。
- 视觉/UE 需求（右侧成长队列、换装确认、离线提示等）**尚无实现**，不得因冒烟通过而记为已达成。

### 5.3 旧档迁移素材（R-04 支撑，本轮实测统计）

`artifacts/**` 下共存 **914 份 `progress*.json`**：

| 类型 | 数量 | 说明 |
|---|---|---|
| `Version=1` | 32 | 最旧结构样本 |
| `Version=2` | 109 | 含 `CombatVersion=1` 的 22 份 |
| `Version=3` | 39 | 含 `CombatVersion=1` 的 27 份 |
| `Version=4` | 321 | 其中 `CombatVersion=2/HeroExperienceVersion=1` 79 份、`+SkillTreeVersion=1` 109 份 |
| 内容为 `invalid` | 282 | 故意的损坏档，用于备份/重置恢复验证 |

结论：**跨版本回归素材充足**，`Version 1→4` 与内部版本（Combat/HeroExp/SkillTree）都有真实样本，可用于验证"旧角色等级不因 XP 折算下降"；但这些是测试生成档，**不等于真实玩家档**，正式迁移仍需用户提供或保留线上旧档副本。

---

## 6. UNAVAILABLE / UNVERIFIED（更新后）

| 项 | 原因 | 影响 |
|---|---|---|
| 逐技能施放/命中/伤害事实 | Core 只有聚合计数（P2-2 D-13 待批） | D22 逐技能核验不足 |
| 召唤物生命周期、城堡失败序列 | 无事件导出 | D24 召唤强度只能按聚合反推 |
| `SaveDocument.Version` 1→4 逐版本迁移分支 | 代码只见白名单，无分支 | D26 迁移语义不完整确认 |
| 最小化/挂起后的真实推进 | 需实机长时间观察（本轮只跑冒烟） | D20 后半段 |
| 真实玩家存活的旧档 | 仅有测试生成档 | R-04 的玩家档兼容仍需真实样本 |
| git 分支/提交/未提交变更 | 系统无 `git.exe` | 变更取证依赖 mtime + 哈希 |
| 数值表逐行合理性（`herolevel.csv` 1 万行、`monsterlevel.csv` 1 万行、act3~5 `floor_plan` 取值） | 本轮为结构审计 | 数值标定属 M5/数值包 |

> 已从 V1 的 UNAVAILABLE 中移除：Godot 引擎/截图能力（已实测可跑）、单窗口结构（已验证）。

---

## 7. 门禁分层（R-01 落地）

| 门禁 | 内容 | 阻断范围 | 状态 |
|---|---|---|---|
| G-01 | Lmin 永久购买的安全规则与断供验收 | **仅** KP 的 Lmin 购买对玩家开放前 | 未通过（待 B-01 裁决） |
| G-02 | 城堡准入是否要求实杀同档证明 | 仅城堡准入规格定稿前 | 未通过（待 B-02；Review 倾向 `Lmax≥N`） |
| G-03 | AR/DR 正式曲线与倍率定标 | 仅"正式数值"（P2-5A）；P2-4 可用 40%–150% 临时带 + 可配置倍率 | 部分放行 |
| G-04 | 公共 XP 正式成本曲线 | 仅"正式数值"（P2-5A）；P2-4 用临时表 + 手动升级事务 | 部分放行 |
| G-05 | 右侧成长队列 UE 任务链/线框 | 仅 D21 UI 接线包开工前 | 未通过（M5 设计交付） |
| G-06 | 旧档迁移策略与玩家档样本 | 仅 WP2 切主档前 | 未通过（需用户取舍 + 真实档） |
| G-07 | 逐技能只读事实（P2-2 D-13） | 仅逐技能验收/大规模数值标定前 | 未通过（技术审批） |
| G-08 | `--param-audit` 输出路径可配置 | 仅重跑参数审计前（防覆盖历史） | 未实施（登记为需求） |

---

## 8. 结论

`READY_WITH_OPEN_ITEMS`（工程自检）/ `PASS_WITH_OPEN_ITEMS`（设计 Owner Review），二者一致：

1. 骨架可复用：唯一伤害入口、存档事务与备份、快照/种子、章节与统治区门禁、模拟账本、单窗口运行时（**引擎侧已实测可跑**）。
2. 四条中心语义需重建：Account/Run 分层、公共 XP 手动升级、AR/DR 替代 Armor、KP 货币；顺序见 `M4_DEPENDENCY_AND_MIGRATION.md` §4。
3. **6 个待决主题、4 个真正的编码门禁**（G-01/G-02/G-05/G-06）；G-03/G-04 只挡正式数值不挡骨架，G-07/G-08 只挡验收与重跑。**不得表述为"一项未定全部停工"**。
4. 工作量粗估修正为 **71–108 人日**（V1 的 65–100 为算术笔误），不作工期承诺。
5. 本审计与 Review 均**不等于 P2-4 编码授权**；下一主任务是设计 Owner 的 M5 设计规划。

---

## 9. 变更记录

| 版本 | 时间 | 变更 |
|---|---|---|
| V1 | 2026-10-25 13:11 | 首版交付：27 项差异矩阵、状态、迁移、开放问题、证据索引 |
| V2 | 2026-10-25 13:54 | 回填设计 Owner Review V1.0：新增门禁分层 G-01~G-08（R-01/R-03）；修正 WP 工时 71–108（R-02）；补存档迁移硬约束与 914 份旧档样本统计（R-04）；**补跑 Godot 四类引擎验证并解除 R-05**；增"替代不叠加"验收条件（R-06）；登记 `--param-audit` 路径需求（§5 补证） |
