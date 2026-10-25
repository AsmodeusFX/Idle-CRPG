# P2-4 首批执行（WP0–WP3）工程实施与自检归档

- 模块：P2-4 首批（工程安全护栏 / 实例与 Account-Run 存档 / 公共XP与AR-DR / KP与怪物区间）
- 入口：用户直接需求＋设计Owner交接《Steam_D2_P2-4_首批执行任务包_WP0-WP3_V1.0_待授权》；用户于 2026-10-25 指示按该包继续执行
- 日期：2026-10-25（本文件 15:4x 收口；实施过程 14:35–15:40）
- 权限：IMPLEMENT_APPROVED_SCOPE（用户指令范围内；未做 Git 提交/推送）
- 状态：工程实现与自检 **PASS_WITH_OPEN_ITEMS**；设计AI复核 **待反馈**；用户验收 **未验收**
- 本文件为该模块唯一 Review，后续 WP 升级、复跑与补救一律在本文件内追加小节

---

## 1. 任务与授权

| 项 | 内容 |
|---|---|
| 目标 | 把旧"个人XP自动升级／Armor减伤／多套怪级权威"迁到新版骨架：`1–4英雄→公共XP/KP→玩家指定升级→自然AR/DR→KP买Lmax→主动攻城` |
| 本批范围 | WP0、WP1、WP2、WP3（D22/D25/D27；D01–D07、D24、D26；D10–D12、D17城堡准入、D21右栏） |
| 非目标 | 正式数值平衡（P2-5A）、完整成就商店与第5席、装备/卷轴/药剂真实功能、`Lmin` 面向玩家开放、Git 发布 |
| 有效基线 | 纲要 V1.2；M3 结项 V0.8；M4 审计 V2＋独立Review V1.0；UI 验收合同 V0.5；本包硬性不变量 INV-01~INV-12 |
| 前置复核 | 用户指定的"检查完成情况"：接手时已存在前一轮实施痕迹（Core/UI/tests 大量改动），本轮做了三处修订并补完验收（见 §4.1） |

---

## 2. 工程事实与实施

### 2.1 WP0｜安全护栏与观测入口（D22/D25/D27）

| 项 | 现状 |
|---|---|
| 构建/检查入口 | 两工程均可 `dotnet build`；`tests/IdleCrpg.Checks.csproj` 为唯一核心检查入口；Godot 4.7.stable.mono 实机可跑 |
| `--param-audit` 输出 | 由历史归档目录改为 `artifacts/qa/param-audit-<UTC时间戳>`，并支持 `--param-out=<路径>`；`ExecutionSafetyChecks` 断言**拒绝**写入历史 `docs/data` 归档、源配置目录与已存在输出目录 |
| 星图编辑器写盘 | 默认落 QA 副本；生产根写盘需显式指令，`StarSmoke` 断言"QA 三表保存但不发布到生产/会话" |
| 证据入口 | 每次运行落 `artifacts/qa/*-<时间戳>/`；本批验收日志固定在 `artifacts/qa/p2-4-first-batch/final-*.log` |
| 基线留存 | `artifacts/qa/p2-4-first-batch/baseline-core.log`、`baseline-smoke.log`、`baseline-combat.png`（14:35，早于 WP1 代码改动），用于解释旧行为差异 |
| 隔离 | QA 夹具在 `artifacts/`；玩家正式档在 `user://`；源配置 `Idle-CRPG/config/` 三者互不覆盖（默认检查与冒烟均走 QA） |

### 2.2 WP1｜英雄实例ID、固定4席与 Account/Run 单文件存档（D01/D02/D03/D24/D26）

- 实例身份：`Actor.HeroInstanceId` 为稳定字符串（本轮 `run-<seed>:<ordinal>:hero:<i>`，旧档 `legacy-<seed>-<dungeonSeed>:hero:<i>`）；`SpecId` 只描述职业。召唤新增 `SummonOwnerInstanceId`，恢复时与属主实例交叉校验；`FindHeroInstance` 仅在职业唯一时兼容旧调用，重复职业不会误选第一人。
- 编队：固定 4 个普通席（`PartyCapacity==4`），可选 1–4 人、允许同职业重复、0 人拒绝；`TeamLocked=true`，`RequestFormation`/`DeployHero` 在本轮直接拒绝并给出"放弃本轮后重新选队"，`ReserveHeroes` 与 `PendingFormation` 必须为空。
- 存档：`SaveDocument.Version=5`，JSON 根只有 `Account` 与 `Run`（`Session` 降级为仅工程内别名）；`Account`＝成就点/轮次序号/成就事实/永久解锁的**结构**（本批不发放任何 AP 或商品）；`Run`＝原 `SessionSnapshot` 全部本轮状态＋新增 KP 与 `[Lmin,Lmax]`。
- 迁移：V1–V4 读入时把 `Session` 原样搬进 `Run`、重建空 `Account`、写 `MigratedFromVersion`；`RunVersion 0→1` 补 `RunId`/实例ID/锁队。旧档含 5 人或候补时抛 `MigrationRequiredException`（保留原件与备份，不删人不清档）。个人经验折算见 §2.3。
- 四种操作区分：普通保存读档｜放弃本轮（`AbandonRun` 保留 Account、清本轮、不发奖励）｜正式重生（`TryRebirth` 恒 false，提示 WP7 未开放）｜清除所有进度（`SaveStore.Reset` 清主档与备份）。UI 分别为"放弃本轮并重新选队"与"清除所有进度"两个不同确认框。

### 2.3 WP2｜公共XP/指定升级、自然AR/DR 与唯一伤害口径（D04/D05/D06/D07）

- XP：`GameSession.PublicExperience`（+百分位余数）为唯一权威；击杀按怪物 `RewardXp` 原值入池，`MonsterExperiencePercent` 恒 100（等级差衰减退役，`experience_level_rate.csv` 已无消费者）；`Experience` 属性改为池别名，删除 `Heroes[0]` 伪投影与 `while` 自动连升。
- 升级：`UpgradeHero(heroInstanceId)` 每次恰好 1 级，先校验费用再扣池、再改 `Attack/MaxHp`（按 `herolevel.csv` 累计差值），不足拒绝且不改状态；读档不补升。
- 旧经验折算（防重复领取）：`PublicExperienceVersion 0→1`；v0 旧共享余额仅按 `s.Experience` 折算一次；v1 个人档只折算各英雄"当前级未花余额"，**不补给已消费的历史成本**；折算后个人字段清零并校验。
- AR/DR：`AttackRating/DefenseRating = 职业基准 + (Level-1)×每级增量 + 修饰器`（[H] 原型 10/10）；怪物按等级×每级值。伤害唯一入口 `DamagePipeline.Resolve` 改为 `rating = Clamp((AR/DR)^1.5, 0.4, 1.5)`，物理分量乘以 rating、元素分量乘 rating×抗性；**Armor 因子退出真实伤害**（`DamageInput.Armor` 仅保留输入契约与校验）。
- 参数隔离：`config/combat/prototype_progression.csv` 全部行 `status=H`（加载器强制校验 H，行数固定 8），替换曲线不改执行逻辑。

### 2.4 WP3｜本轮KP、唯一怪物区间与城堡准入（D10/D11/D12/D17/D21）

- KP：`Kp / KpEarned / KpSpent` 三账；每击杀 +`kill_kp`(1)，以 `RunId:进场地序:难度:章节:种子:层:房:怪序` 收据去重（重开房间不重复领取）；存档校验 `KpEarned-KpSpent==Kp`。
- 升档：`BuyKpUpgrade(MonsterLevelMaximum)` 逐笔 `Lmax+1`，费用 `lmax_cost`(10)，必须先满足"本轮**全部**上阵英雄逐个 AR 与 DR ≥ (Lmax+1)×每级标准"，失败给出具体英雄与项，**不扣款不改状态**；`Lmin` 在 Core 公开命令层恒拒绝，存档校验写死 `MonsterLevelMin==1`，UI/GM 无任何入口。
- 怪级权威：`Reveal` 只取 `Run.MonsterLevelMin/Max`；POI 挑战等级、`floor_plan.MonsterLevel`、`spawn_rule.level` 等不再参与刷怪等级（章节继续决定模板/地形/故事）；`WorldCampaign.SyncMonsterLevelRange` 供城堡准入判断。已生成房间在升档后保持快照，不跨档重刷。
- 城堡：`CanChallengeCastle` = `Lmax ≥ 已配置要求怪级` ＋ 既有章节/统治区前置；**不要求**同级实杀证明；仍必须玩家主动点击，不自动发起。

### 2.5 UI 接线（合同 V0.5）

- 顶栏 A：四页签＋标题＋GM/设置/暂停，未删功能。
- 右上 D：金币与 KP **分开**两格（`currencies[0]`/`currencies[1]`），KP 提示含本轮余额与背包杂物数。
- 右侧 E：`PublicExperienceBalance`（公共XP＋怪级区间）置顶 → 每名英雄独立升级卡（Lv→Lv+1、费用、"可升级/XP不足"、AR/DR）→ KP 购买卡（费用与不达标原因）→ 反馈行 → 既有城堡卡/编队卡。
- 左下 F：小队区新增"公共XP n · 逐人点击升级"，每人显示 `Lv/HP/AR/DR`，技能图标与 CD 区保留，空席显式标"本轮空席（锁定）/未来永久第五席"。
- C 卷轴横栏、G 药剂 4 槽：结构在位，标 `后续开放`（PLACEHOLDER，WP6 交付）。

---

## 3. 自主判断与偏差台账

| 原要求／旧规则 | 工程事实及证据 | 采用方案与理由 | 替代方案 | 玩法／兼容影响 | 授权依据／需裁决项 |
|---|---|---|---|---|---|
| INV-10 旧档不能静默清空或降级 | 3 份旧档含 5 人队伍或候补（`artifacts/qa/217a…/4284e…/b8e5b…/progress_v1.json`） | 抛 `MigrationRequiredException`，保留原件与备份，交用户裁决 | 自动裁到 4 人并冻结多余角色；或允许旧档以 5 人继续本轮 | 该 3 份 QA 档暂不可加载；真实玩家遇到需人工确认 | **需用户裁决**（§8-A） |
| 公共XP折算不得重复发钱 | 旧 v1 个人档的 `Experience` 是当前级未花余额，历史成本已消费 | 只折算未花余额；v0 共享余额只折算一次 | 按累计获得总量折算（会重复发放） | 迁移后总额偏保守，不虚增 | 自主判断，符合 INV-10 |
| INV-05 Armor 不与 AR/DR 双活 | `DamagePipeline` 已无 Armor 因子；`ArmorK`/`armor_curve.csv` 仍被加载用于输入校验 | 本批只保证"不参与伤害"，保留旧曲线供兼容与后续清理 | 立即删除曲线与 `Armor` 字段 | 无玩法影响；存在死代码 | 自主判断；清理列入后续 |
| [P] 升档后已生成怪物如何响应 | 实现为"已生成房间保持快照" | 沿用现有快照规则，避免静默跨档刷怪 | 升档立即重刷未进房内容 | 玩家可感知边界 | **需用户确认**（§8-B） |
| QA-1C 旧档样本易被其它套件改写 | 全量检查/冒烟会重写 `artifacts/qa` 夹具，导致指纹假红（2 份漂移） | 新增显式 `--freeze-legacy-samples` 冻结副本，迁移回归只读冻结副本 | 每次按当前夹具重建清单（无法证明未被改写） | 避免假红；漂移样本单独计数 | 自主判断（工程侧） |
| QA-1C 覆盖口径 | M4 统计 914 份、汇总 783 份 | 清单扩为 925 份并**互斥分类**（v1/v2/v3/v4 × session/no_session/invalid），逐份迁移 | 只测可用档 | 488 份"拒绝但保留"有明确原因（287 故意损坏、106 旧地图版本、95 旧结构/字段），非新缺陷 | 自主判断 |
| 全灭返村既有回归 | 该场景房间可能已被清空，`ContentCleared=true` 导致不再交战 | 测试显式撤销清除标记，恢复"战斗中死亡→同逻辑步返村"断言意图 | 换用全新种子场景 | 仅测试场景确定性，不改产品行为 | 自主判断（测试侧） |
| Godot 冒烟受旧 DLL 影响 | 只构建 tests 时 Godot 侧仍用旧程序集，GM 冒烟假红 | 验收流程固定"先构建两工程再跑冒烟" | 让冒烟自检程序集版本 | 无产品影响 | 自主判断（流程） |
| 合同 F 要求小队区可见公共XP | 原实现只在右栏与悬停提示显示 | 小队标题行新增"公共XP n · 逐人点击升级" | 仅保留右栏显示 | 信息更直白，不改语义 | 自主判断（UI 合同内） |
| 模拟策略无"手动升级"动作 | `SimulationRunner` 策略只有城堡/星图/编队/天赋 | 本批不改策略，等级恒为 1；账本与XP守恒仍校验 | 立即加贪心升级动作 | 长周期成长曲线**不可用**，需后续补 | 遗留（§4.3） |

无其它未登记的偏差；未改动玩家可感知的重大玩法规则，未开放 `Lmin`，未发放成就点。

---

## 4. 验证与遗留

### 4.1 本轮接手时的三处红项及处置

| 红项 | 原因 | 处置 |
|---|---|---|
| 全量检查在"战斗击杀触发全灭"断言中断 | 该场景房间已清空，`ContentCleared` 使战斗分支不启动（测试前提失效，非产品回归） | 测试显式 `ContentCleared=false` 后通过 |
| `--legacy-run-migration` 报"样本指纹变化" | 样本为会被其它套件重写的 QA 夹具 | 改为冻结副本＋显式冻结命令，重跑通过（漂移 2 份单独计数） |
| `--first-batch-checks` 抛 `KeyNotFound '36'` | 出生地块是营地，营地不参与统治归属 | 城堡改从"默认开放统治区"取，重跑通过 |
| 本批专项只在单独开关里 | 默认全量检查不覆盖 WP0–WP3 | `ExecutionSafetyChecks` 与 `FirstBatchChecks` 接入默认尾部 |
| 两处未使用字段告警 | 公共XP改造后 `level_up_ticks` 无来源 | 清理字段，输出恒 0 并注释原因；两工程恢复 0 警告 |

### 4.2 验收证据（全部退出码 0，日志在 `artifacts/qa/p2-4-first-batch/`）

| 需求／行为 | 代码或配置证据 | 环境、命令、退出码 | 结果与局限 |
|---|---|---|---|
| 构建 | `Idle-CRPG/Idle-CRPG.csproj`、`tests/IdleCrpg.Checks.csproj` | `dotnet build`（两工程均 0 警告 0 错误） | `final-build-tests.log` / `final-build-godot.log` |
| 全量核心检查（含本批专项） | 默认尾部已接入 `ExecutionSafetyChecks` 与 `FirstBatchChecks` | `dotnet run --project tests/IdleCrpg.Checks.csproj`，exit 0，约 55s | `final-core.log`；69 条 PASS，含 WP0–WP3 自检 |
| WP0 写盘保护 | `ExecutionSafetyChecks` | `--execution-safety-checks` / 默认套件，exit 0 | 历史归档、源配置、已存在输出均被拒 |
| WP1/2/3 功能 | `FirstBatchChecks` | `--first-batch-checks`，exit 0 | `WP1_PASS / WP2_PASS / WP3_PASS / WP3_RECEIPT_CASTLE_PASS` |
| 旧档迁移 | `LegacyRunMigrationChecks`（冻结样本） | `--legacy-run-migration`，exit 0 | 925 份：迁移 251 / 拒绝保留 488 / 待用户裁决 3 / 空档 183；冻结漂移 2 |
| 旧规则回归 | `RegressionMatrix`（41 个 `*Checks`） | `--regression-matrix`，exit 0 | `REGRESSION_MATRIX passed=41 failed=0` |
| Godot 真实控件 | `RunFirstBatchSmoke`（1/3/4 人、四同职、锁队、AR拒绝、指定升级、KP购买、七分区、存读、GM/设置/暂停） | `godot --path Idle-CRPG -- --first-batch-smoke`，exit 0 | `FIRST_BATCH_UI_PASS window=(1440,900) renderer=Windows`；截图见 §4.4 |
| 原有 UI 冒烟回归 | 12 套旧冒烟 | `godot --headless --path Idle-CRPG -- --smoke-test`，exit 0 | `SMOKE_OK: 交战中` |
| 整屏界面取证 | HUD/GrowthUi | `godot --path Idle-CRPG -- --capture --capture-combat`，`CAPTURE_OK` | `artifacts/preview-combat.png`（145 KB，15:32） |

### 4.3 未覆盖与遗留（不得当作已验收）

1. **模拟策略缺"手动升级"**：`T_tool_probe` 等级恒为 1、`retries=4`；XP/KP 账本与守恒仍通过（`SIM_LEDGER diff=0`、`SIM_XP unexplained=0`），但**长周期成长曲线本轮不可用**；`E_min_party` 仍请求旧星图扩编，需后续清理。
2. **Armor 残留**：`ArmorK`/`armor_curve.csv` 与 `DamageInput.Armor` 仍在（不参与伤害），建议后续单独清理并同步字段字典。
3. **设计文档未同步**：`docs/design/combat_formula.md`、`attributes_levels.md`、`docs/technical/combat_config.md` 仍描述旧 Armor/个人经验口径，需设计侧修订后再改文档（本批未擅自改正式设计规范）。
4. **长跑与后台**：未做最小化/长时间挂机观察；离线结算属后续包。
5. **跨平台**：仅 Windows 单机验证；未验证 Linux/macOS 与 Steam 打包形态。
6. **美术/终版 UI**：卷轴横栏与药剂 4 槽为 PLACEHOLDER；技能图标区密度、空席表现均为原型。
7. **成就点/第5席/正式重生**：仅保留状态结构与禁用入口，未实现玩法。
8. **3 份 5 人旧档**：需用户裁决迁移策略（§8-A）。
9. **KP/评级数值全部为 [H]**：不得作为正式平衡；`Lmin` 永久购买仍关闭。

### 4.4 截图与日志

- 整屏：`artifacts/preview-combat.png`（顶栏/战斗/右栏/左下队伍技能/右下药剂/卷轴横栏齐备，金币与 KP 分列，右栏升级卡与 KP 卡可见，小队区显示公共XP）。
- 流程截图目录：`artifacts/qa/first-batch-ui-<ts>/`：`setup-1/3/4.png`、`locked-team.png`、`kp-rating-rejected.png`、`specified-hero-upgraded.png`、`lmax-purchased.png`、`settings-abandon-and-clear.png`。
- 日志：`artifacts/qa/p2-4-first-batch/final-{build-tests,build-godot,core,first-batch,legacy-migration,regression-matrix,smoke,first-batch-ui}.log`；基线：`baseline-*.log`、`baseline-combat.png`。

---

## 5. 分主体结论

- 工程实现与自检：**PASS_WITH_OPEN_ITEMS**（范围 WP0–WP3；依据 §4.2 全部 exit 0，遗留见 §4.3）
- 设计AI复核：**待反馈**（本文件与其摘要供设计Owner独立核对）
- 用户验收：**未验收**（含 §8 两项待裁决）
- 返工与再验证条件：`Lmin` 安全验收通过前不得开放购买；3 份 5 人旧档裁决后需补迁移分支与用例；模拟策略加入升级动作后需重跑账本与停滞分级。

---

## 6. 回传设计AI摘要

- 需求与裁决：按 V1.0 包完成 WP0–WP3；未开放 Lmin、未发成就点、未改正式数值、未提交 Git。
- 已完成改动及证据：实例ID/4席锁队/Account-Run v5 存档（含 V1–V4 迁移与四种操作区分）；公共XP＋逐人点击升级（去衰减、去自动升级、去 `Heroes[0]` 投影）；AR/DR 倍率取代 Armor（[H] 参数表，40%–150%）；KP 账本、逐笔 Lmax、Lmin 三层封禁、唯一怪级区间、城堡只看 Lmax；右栏成长队列与小队区公共XP显示；`--param-audit` 时间戳输出与写盘拒绝；星图编辑器默认 QA 写盘。证据：`final-*.log`、`preview-combat.png`、`first-batch-ui-*`。
- 自主适配：旧档 5 人→MigrationRequired；XP 折算仅未花余额；Armor 仅退役不删码；升档后保留已生成房间快照；冻结旧档样本；测试场景与构建顺序两处修正。
- 应更新的设计认知：`(AR/DR)^1.5` 与 10/10/10、KP=1、Lmax 费 10 均为 [H] 原型；公共XP下"击杀即永久战力"不再成立（需玩家点击）；`experience_level_rate.csv` 已退役。
- 待裁决／复核／后置：5 人旧档迁移策略；升档对已生成怪物的玩家可感知规则确认；模拟升级动作与 `E_min_party` 清理；Armor 死代码清理；设计文档同步。
- 反馈状态：材料已生成（待用户转交设计AI）。

---

## 7. 证据索引（WP → DesignID → 代码/配置 → 测试 → 截图）

| WP | DesignID | 代码/配置 | 测试 | 截图/日志 |
|---|---|---|---|---|
| WP0 | D22 D25 D27 D23 | `Scripts/Core/QaOutput.cs`；`tests/Program.cs` 参数解析；`Scripts/Core/StarEditorStore.cs`、`Scripts/UI/StarUi.cs` | `ExecutionSafetyChecks`、`RegressionMatrix` | `final-build-*.log`、`baseline-combat.png`、`preview-combat.png` |
| WP1 | D01 D02 D03 D24 D26 | `Scripts/Core/RunState.cs`、`SaveStore.cs`、`SessionSnapshot.cs`、`FormationPlans.cs`、`HeroCreation.cs`、`HeroRoster.cs`、`SummonRuntime.cs`；`Scripts/UI/ClientSettings.cs`、`FormationUi.cs`、`HeroPage.cs` | `FirstBatchChecks`（WP1 段）、`LegacyRunMigrationChecks`、`SaveChecks`/`ProgressChecks`/`FormationChecks` | `locked-team.png`、`settings-abandon-and-clear.png`、`final-legacy-migration.log` |
| WP2 | D04 D05 D06 D07 D21 | `Scripts/Core/HeroExperience.cs`、`DamagePipeline.cs`、`PrototypeProgression.cs`、`config/combat/prototype_progression.csv`；`Scripts/UI/GrowthUi.cs`、`HudLayout.cs` | `FirstBatchChecks`（WP2 段含 1/2/4 人入池）、`AttributeLevelChecks`、`DamageFormulaChecks`、`CombatSkillChecks` | `specified-hero-upgraded.png` |
| WP3 | D10 D11 D12 D17 D21 | `Scripts/Core/KpProgression.cs`、`GameSession.cs`（Reveal/SyncMonsterLevelRange）、`WorldCampaign.cs`、`BranchCampaign.cs`；`GrowthUi.cs` | `FirstBatchChecks`（WP3 段、收据与城堡段） | `kp-rating-rejected.png`、`lmax-purchased.png` |

---

## 8. 待用户裁决

**A. 旧档含 5 人队伍或候补（3 份 QA 样本）怎么迁移？**

- 现状：抛 `MigrationRequiredException`，保留原件与 `.bak`，不加载、不删人、不清档；真实玩家会在启动时看到可读提示。
- 选项：①（当前）拒绝加载并要求人工处理；② 自动裁到 4 人，多余角色转入"冻结候补"（不删数据、本轮不可上阵）；③ 一次性允许该档以 5 人打完本轮，放弃/重生后按 4 席重建。
- 建议：①短期保持，②长期（旧星图扩编买满的玩家更平滑）。需用户定。

**B. KP 升 Lmax 后，已生成房间的怪物怎么处理？**（包内明确要求首次进 WP3 前确认）

- 现状：已生成房间保持原等级快照，只有新生成内容使用新区间。
- 选项：①（当前）保持快照；② 升档后未进入的房间按新区间重刷（会改变已探索内容强度）。

**C. 旧 Armor 死代码与旧曲线文件是否本阶段删除？** 建议并入 WP4/后续清理提交，避免与 AR/DR 调参混在一起。
