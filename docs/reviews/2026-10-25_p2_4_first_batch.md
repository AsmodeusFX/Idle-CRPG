# P2-4 首批执行（WP0–WP3）工程实施与自检归档

- 模块：P2-4 首批（工程安全护栏 / 实例与 Account-Run 存档 / 公共XP与AR-DR / KP与怪物区间）
- 入口：用户直接需求＋设计Owner交接《Steam_D2_P2-4_首批执行任务包_WP0-WP3_V1.0_待授权》；用户于 2026-10-25 指示按该包继续执行
- 日期：2026-10-25（本文件 15:4x 收口；实施过程 14:35–15:40）
- 权限：IMPLEMENT_APPROVED_SCOPE（用户指令范围内；未做 Git 提交/推送）
- 状态：工程实现与自检 **PASS_WITH_OPEN_ITEMS**；设计AI复核 **已回填 V1.0（PASS_WITH_OPEN_ITEMS，报告级）**，见 §9；设计裁决回传 **V1.1 两项 [C] 已落实**，见 §10；用户验收 **未验收**
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
| QA-1C 覆盖口径 | M4 统计 914 份、汇总 783 份 | 清单扩为 925 份并**互斥分类**（v1/v2/v3/v4 × session/no_session/invalid），逐份迁移 | 只测可用档 | 488 份"拒绝但保留"在 R-01 后已细分为 **287 份非 JSON 损坏夹具 + 91 份旧地图版本（MapVersion=1）+ 110 份地图版本前结构**，且断言"当前地图版本(3)的存档不得被拒绝"，非新缺陷 | 自主判断＋设计Review R-01 |
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
| 旧规则回归 | `RegressionMatrix`（42 个 `*Checks`，含新增实例隔离） | `--regression-matrix`，exit 0 | `REGRESSION_MATRIX passed=42 failed=0` |
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

---

## 9. 设计Owner独立Review V1.0 回填（R-01～R-06，2026-10-25 晚）

入口：`docs/temp/WP0–WP3 独立Review与交接包 V1.0/Steam_D2_P2-4_WP0-WP3_设计Owner独立Review_V1.0.md`。以下仅处理"属于已授权 WP0–WP3 内的测试/修复"，未新增玩法、未开放 Lmin、未改动正式数值。

### 9.1 R-01｜201 份拒绝样本再细分（P1，已关闭）

- 新增自动化细分与硬断言：拒绝样本必须归入 `CORRUPT_FIXTURE` / `SUPERSEDED_MAP_SCHEMA` / `PRE_MAP_SCHEMA`；**任何 `MapVersion` 等于当前章节地图版本（=3）的样本被拒绝即令检查失败**。
- 实测分类（925 份）：`SHADOW_MIGRATED 251` / `REJECTED_PRESERVED 488`（= **287 非 JSON 损坏夹具** + **91 旧地图版本 MapVersion=1** + **110 地图版本前结构，无 MapVersion 字段**）/ `USER_DECISION_REQUIRED 3` / `EMPTY_DOCUMENT 183`。
- 三类性质判定：① 287 为故意构造的损坏档（内容不是 JSON，用于备份/重置回归）；② 201 全部生成于 2026-10-17～10-25 的旧测试夹具，地图结构为已被 POI 化替换的旧第一章布局，属**已批准兼容边界之外**（`docs/technical/persistence_settings.md` 明确旧地图编号不可猜测、要求重置）；③ **0 份**——不存在"当前兼容范围内应能迁移却失败"的样本。
- 结论：`201 = 91 + 110`，均不为"曾经有效玩家存档被误拒"的证据；仍保留原件与 `.bak`，不自动清档。
- 产物：`artifacts/qa/legacy-run-migration-<ts>/reject-classification.json`（含当前地图版本、各类计数与代表样本）。

### 9.2 R-02｜同职业实例隔离定向证据（P1，部分关闭＋1 项转 WP4）

- 新增 `tests/InstanceIsolationChecks.cs`（`--instance-isolation-checks`，已并入回归矩阵）：四名同职业实例 →
  - 召唤属主 `SummonOwnerInstanceId` 四者互异且与 `SummonOwner` 索引一一对应，每主人召唤数在定义上限内，未装配实例不获得召唤；
  - 逐实例 `EquipSkills` 只改被操作实例，其余实例装配不变，且**按实例随存档保存/恢复一致**。
- **未能关闭的部分（转 WP4 前置）**：同目标同 ID Buff 的来源键/叠层/失效运行期证据受旧 Buff 目标解析限制——当前 `barb_taunt` 类自体型范围 Buff 在测试现场可提交动作却不产生可观察 Buff 实例（`SkillAiDiagnostics` 显示已"选择"、随后进入冷却），故本轮只按"旧语义可支持范围"报告已证事实，不据此声称 Buff 叠层已验收。WP4 做技能/特性时必须先修/明确该目标解析，再补同目标多来源用例。

### 9.3 R-03｜升级→天赋点闭环（P1，已关闭）

- 实测：升级前后 `TalentPoints` 增加 `talent_points_per_level`（初始 1 点、每级 1 点，初始技能占 1 点）；学会首排节点后可用点减少；`CaptureProgress` 存档 → 重载后点数与已投入一致。
- 断言落地于 `FirstBatchChecks` 的 `WP2_TALENT_PASS`。该项原报告未显式覆盖，现补齐，不再作为 WP4 未知缺口。

### 9.4 R-04｜KP 收据两个对照（P1，已关闭）

- 对照一（原已覆盖）：同一已击杀怪物重复触发领取 → `Kp` 不重复入账（`WP3_RECEIPT_CASTLE_PASS`）。
- 对照二（本轮新增）：**同一地牢再次合法进入**（同模板、同统治区、同 Seed，`dungeonEntrySerial` 递增）→ 同位怪物击杀**正常 +1 KP**（`WP3_RECEIPT_REVISIT_PASS`）。
- 结论：收据既能防重领，也不会误杀正常重复刷怪收益；无需引入额外遭遇唯一号。

### 9.5 R-05｜真实 UI 图像证据与视觉自检（P1，已补证据＋修 1 处缺陷）

- 代表图固定在 `artifacts/qa/p2-4-first-batch/evidence/`：`01-main-screen.png`（整屏）、`02-locked-team.png`、`02-specified-hero-upgraded.png`、`02-kp-rating-rejected.png`、`02-lmax-purchased.png`、`02-settings-abandon-and-clear.png`。
- 逐项核对（人工看图，非脚本）：顶栏四页签/GM/设置/暂停在位；战斗区独占主视区；卷轴横栏独立成条；右上金币与 KP 分列；右栏"公共XP → 各英雄升级卡（Lv、费用、AR/DR）→ KP 购买卡（费用与不达标原因）"顺序正确；左下四席 + HP/AR/DR + 技能/CD 区；右下药剂 4 空槽；升级与 KP 购买后数字随之变化。
- **发现并修复 1 处缺陷**：新增的小队区"公共XP … · 逐人点击升级"与右侧"普通席位 / 4 · 已锁队"文字重叠（`02-lmax-purchased.png` 可见）。改为只显示余额、规则移入悬停提示，并重新截图确认不重叠。
- 边界：`FIRST_BATCH_UI_PASS` 仍只是自动化证据；本文件中的视觉结论为工程侧自检，不代替设计/用户验收。卷轴与药剂保持 `PLACEHOLDER`。

### 9.6 R-06｜旧行为清理与测试策略（P2，部分关闭）

| 子项 | 处置 |
|---|---|
| `ArmorK`/`armor_curve` 残留 | 本轮不做（避免与 AR/DR 调参混提），保留"不参与伤害"事实，列 WP4 清理项 |
| `SimulationRunner` 无手动升级策略 | 未在本轮实现：策略 schema 变更会影响既有账本/单组每 tick/回归矩阵，且 Review 时点要求是"P2-5 模拟验收前"；本轮已在 `SIM_XP` 输出把 `level_up_ticks` 固定为 0 并注明原因，避免误读成长曲线 |
| 旧设计文档仍写个人XP/Armor | 属设计侧措辞交付，工程未擅自改正式设计规范；已列入待同步 |
| Lmin 隔离反例能力 | **已补**：`QaRiskRange(min,max)` 仅测试程序集可用，可构造如 `[3,3]` 的极端区间并实际按该区间刷怪；同时断言该现场**无法写入正式存档**（`MonsterLevelMin≠1` 被读档校验拒绝）且玩家命令仍不可购买 Lmin（`WP3_LMIN_ISOLATION_PASS`） |

### 9.7 回填后验证（全部 exit 0，日志 `artifacts/qa/p2-4-first-batch/final-*.log`）

| 命令 | 结果 |
|---|---|
| `dotnet build` ×2 | 0 警告 0 错误（`final-build-tests.log` / `final-build-godot.log`） |
| 默认全量 Core | PASS（含 WP0–WP3 与新增天赋点断言） |
| `--legacy-run-migration` | 925 份，四类计数＋拒绝细分，0 份当前地图版本被拒 |
| `--regression-matrix` | **42/42**（新增 `InstanceIsolationChecks`） |
| `--instance-isolation-checks` | PASS（召唤属主/装配实例隔离） |
| Godot `--headless --smoke-test` | `SMOKE_OK` |
| Godot `--first-batch-smoke` | `FIRST_BATCH_UI_PASS`（1440×900，含截图） |
| 整屏取证 | `CAPTURE_OK` → `evidence/01-main-screen.png`（修复重叠后重拍） |

### 9.8 回填后仍未关闭

1. §8-A/B 两项产品裁决（5 人旧档迁移体验；升档后已生成房间处理）。
2. Buff 同目标多来源的运行期证据（转 WP4，先修/明确目标解析）。
3. 模拟手动升级策略（P2-5 前）、Armor 死代码清理（WP4）、设计文档措辞同步（设计侧）。
4. 跨平台与长跑后台验证。

---

## 10. 设计裁决回传 V1.1 落实（C-LEGACY-5 / C-LMAX-SNAPSHOT，2026-10-25 晚）

来源：`docs/temp/P2-4 第二批设计与执行交接包 V1.0/Steam_D2_P2-4_WP0-WP3_设计裁决及补证回传_V1.1.md`（用户已确认"同意建议，继续推进"）。两项产品规则均为 `[C]`，本节只记录落实与证据，不改动其它已批准边界。

### 10.1 C-LEGACY-5｜旧档 5 人／候补的冻结迁移（已实现）

- **数据归属**：新增 `LegacyQuarantine`（`SaveDocument.Legacy`），跨本轮存续；`Actor` 按旧档原样保留（等级/经验/天赋/装备），带可追溯实例 ID（`legacy-<seed>-<dungeonSeed>:frozen:<槽位>` 与 `:frozen:reserve:<n>`）。它不是 Account 永久英雄，不上阵、不参与战斗/天赋/装备、不能作为普通候补、不解锁第 5 席。
- **迁移流程**：`SaveStore.Decode` 遇到旧格式超额（`RunVersion==0` 且 >4 人或含候补）**不再抛通用迁移错误**，改为抛 `LegacySelectionRequiredException`（`MigrationRequiredException` 子类，既有调用方分支保持兼容），携带候选清单且**不写盘**；玩家在 `LoadWithLegacySelection` 选定 1–4 名后，其余与候补进入隔离区、本轮队伍锁定为 4 席上限、写盘并原子替换（保留 `.bak` 与 `.pre-v5-*`）。
- **幂等与可重试**：冻结 ID 按来源槽位确定性生成，重复执行不重复冻结；每次尝试都从同一旧档文本重新解码，失败不影响原档。
- **战场现场**：队伍成员变化后，进房节点、召唤属主、救援索引、门将索引与技能现场整段作废，按既有"房间重开"规则重建，避免新旧人数混用产生非法引用。
- **UI**：`ClientSettings.ShowLegacyMigration` 提供逐英雄勾选（超过 4 名时其余选项禁用）、"暂不迁移"保持原档不变、失败提示不改档；迁移结果在状态栏说明本轮人数与冻结人数。
- **清除边界**：放弃本轮/正式重生保留隔离区；只有"清除全部进度"（`SaveStore.Reset`）清除，并在确认框中保持独立入口。
- **证据**：`--legacy-selection-checks`（`LEGACY_SELECTION_PASS`）覆盖 5 在阵 + 1 候补 → 选 4（等级/职业保留、冻结 2 名含候补）→ 未选择不改档 → 重跑幂等 → 换选累计冻结不重复 → 放弃/新开一轮保留 → 全清除；925 份旧档回归中 3 份超额档仍归 `USER_DECISION_REQUIRED`，当前地图版本样本 0 拒绝。

### 10.2 C-LMAX-SNAPSHOT｜升档对已生成房间的语义（已实现并加证）

- 分界点是 `Reveal` 的首次内容生成：`Room.Generated` 为假时不消耗等级；生成时读取当次 `[Lmin,Lmax]`，并把实际等级写入 `Room.SnapshotLevel` 与 `MonsterOrigin`。
- 购买只改 `Run.MonsterLevelMax` 与 KP 账本，**不触碰已生成房间的怪物等级、血量、掉落或奖励快照**。
- **证据**：`WP3_LMAX_SNAPSHOT_PASS` —— 先进入并实例化房间 → 升级到评级达标 → 购买 Lmax → 断言该房怪物 `SpecId/Level/Hp/MaxHp` 逐项不变 → 推进到下一间新生成房间 → 断言其等级全部落在新区间；配合既有 `WP3_RECEIPT_REVISIT_PASS`（新合法遭遇仍得 KP）与 `WP3_RECEIPT_CASTLE_PASS`（同房重开不重复）。

### 10.3 回传后验证（`artifacts/qa/p2-4-first-batch/v11-*.log`，全部 exit 0）

| 命令 | 结果 |
|---|---|
| `dotnet build` ×2 | 0 警告 0 错误 |
| 默认全量 Core | PASS（新增冻结迁移与 Lmax 快照断言） |
| `--regression-matrix` | **43/43** |
| `--legacy-run-migration` | 925 份，拒绝细分不变（287 损坏 / 91 旧地图 / 110 地图前结构） |
| Godot `--headless --smoke-test` | `SMOKE_OK` |
| Godot `--first-batch-smoke` | `FIRST_BATCH_UI_PASS`（迁移弹窗未影响既有流程） |

### 10.4 仍未关闭

1. R-02 中"同目标同来源 Buff 叠层"的运行期证据仍受旧目标解析限制，转 WP4 前置（先明确/修复目标解析再补用例）。
2. 冻结英雄的**导出/恢复工具**尚不存在，UI 文案已避免承诺；正式形态待 O-01A。
3. 模拟手动升级策略（P2-5 前）、Armor 死代码与设计文档措辞同步（WP4/WP9）。

### 10.5 第二批 WP4–WP9 状态

- 本轮**只执行了回传 V1.1（两项 `[C]` 裁决）**，未开始 WP4–WP9 的编码。按任务包 §8，整包或子包须由用户明确批准范围；建议顺序 `WP4 → WP5 → WP6 → WP7 → WP8 → WP9`，其中 WP4 是其余功能包的前置（技能/天赋/星图机制与旧 `CastTalent`/`skill_slots` 退役）。

## 11. 第二批 WP4 机制切片 1：天赋互斥与免费局部洗点（2026-10-25 晚）

来源：`docs/temp/P2-4 第二批设计与执行交接包 V1.0/Steam_D2_P2-4_WP4-WP9_第二批工程执行任务包_V1.0_待授权.md`（用户已批准整包 WP4→WP9 连续执行）＋ `WP4 行为规格 V0.6`。本切片只做"机制容量"，不含五职业完整技能内容、正式数值与最终 UI。

### 11.1 已实现

| 项 | 实现 | 证据 |
|---|---|---|
| 互斥分支 | `talent.csv` 新增 `exclusive_group`；`CanLearnTalent` 拒绝同组第二分支并提示"免费改选"；旧档若同时存在两分支**不判非法**（不静默剥夺），通过局部洗点自愿改选 | `Wp4TalentMechanicsChecks`（SK-04） |
| 免费局部洗点 | `GameSession.ResetTalentRegion(heroInstanceId, tree)`：只重置该技能区域、返还该区域全部投入点数、清理装配/光环/来源加成/换入等待与当前动作，原子刷新，不动等级与其他区域与其它实例 | 同上（SK-05） |
| 实例隔离 | 洗点/学习均按 `HeroInstanceId`；另一同职业实例的点数与投入逐项不变；读档后点数与互斥选择一致，二次读档不重复返还 | 同上（SK-01/SK-04 读档段） |
| 职业特性标记 | `talent.csv` 新增 `class_trait`；五职业各 1 个特性节点（`barb_voice`/`ranger_archery`/`mage_fire_mastery`/`paladin_weapon`/`necro_bone_armor`），加载期校验"每职业恰 1 个" | 测试数据合同段 |
| 特性解锁门槛接口 | `ClassTraitUnlocked(classId)`：仅当星图投放了 `class_trait_unlock` 节点时才要求购买（约定 id `s_trait_<classId>`），当前未投放则不设死门槛，避免"有节点永远不可学" | 代码 + 说明 |
| 旧基线 schema 对齐 | 退役基线 `tests/fixtures/retired_skill_baseline_2026_10_22/skill/talent.csv` 追加两列默认值（内容不变、仅表头演进），避免旧回归因表结构变更误红 | 回归 44/44 |

### 11.2 验证（`artifacts/qa/p2-4-first-batch/wp4-*.log`，全部 exit 0）

- 两工程 `dotnet build`：0 警告 0 错误
- 默认全量 Core：PASS（`WP4_TALENT_PASS`）
- `--regression-matrix`：**44/44**
- `--legacy-run-migration`：925 份分类不变
- Godot `--headless --smoke-test`：`SMOKE_OK`

### 11.3 WP4 仍未完成（下一增量）

1. **星图侧**：`xp_gain_pct`（本轮成长增益）与 `class_trait_unlock`（按职业共享资格）需要给 `star.csv` 增 `target_id` 列并同步编辑器三表往返；随后补 ST-01/ST-02/ST-03/ST-04。
2. **4+1 机制容量表**：`skill_slot.csv`（职业×4槽×特性树）＋加载校验；现只有 2~4 个真实主动技能，缺项须显式列清单而不是空槽冒充完成。
3. **旧 `CastTalent` 退役**：23 个未绑定节点的映射/退款与 `SkillMigrationChecks` 断言改写（LEG-01），必须与内容迁移同批，避免双权威。
4. **UI**：英雄页四技能与特性树、互斥标识、局部洗点入口；左下技能条只显示已学技能（UI-01 需真实截图）。
5. **星图 `skill_slots`/`party_slots` 下架**：需同时处理编队容量兜底与旧档校验。

### 11.4 第二批整体状态

- WP4：**进行中（增量 1/N 完成）**。
- WP5 装备、WP6 卷轴药剂、WP7 重生/离线、WP8 UI 整合、WP9 账本与模拟：**未开始**。
- 各增量均保持"两工程 0 警告、默认全量 Core 与回归矩阵全绿、Godot 冒烟通过"的收口标准，逐段追加本文件，不新建并列 Review。

---


---

## 12. 第二批执行小结（WP4 增量 2/3 与 WP5–WP9 状态，2026-10-25 收口）

### 12.1 本轮新增完成（WP4）

| 增量 | 内容 | 关键证据 |
|---|---|---|
| WP4-1 | 天赋互斥分支（`exclusive_group`）、免费局部洗点 `ResetTalentRegion`、五职业各 1 职业特性节点、退役基线 schema 对齐 | `WP4_TALENT_PASS` |
| WP4-2 | 星图 `target_id` 列（内容表/编辑器往返/校验）；新增 `xp_gain_pct`（公共XP获取倍率，接入 `HeroExperience.GainExperience` 含百分位取整）与 `class_trait_unlock`（按职业共享学习资格，不自动扣点）；两枚 [H] 原型节点 `s_xp_01`、`s_trait_warrior` | `WP4_STAR_PASS`：购买扣金一次、增益入池、同职业双实例共享资格且各自投点、读档与二次读档一致 |
| WP4-3 | `skill/skill_slot.csv` 4+1 机制容量表（5 职业 × 4 槽，空槽显式表示内容未投放）＋加载校验（职业/槽位/主动归属）＋ `DeployedClassSkills` 口径 | 测试断言 20 槽齐全、已投放 16 个职业主动、空槽不冒充完成 |

回执：两工程 `dotnet build` 0 警告 0 错误；默认全量 Core exit 0；`--regression-matrix` **45/45**；`--legacy-run-migration` 925 份分类不变；Godot `--headless --smoke-test` `SMOKE_OK`；日志 `artifacts/qa/p2-4-first-batch/wp4b-*.log`。

### 12.2 WP4 未完成项（明确缺口，不做"已完成"表述）

1. **旧 `CastTalent` 退役**：23 个未绑定节点的映射/退款与 `SkillMigrationChecks` 断言改写（LEG-01）。这是内容迁移工作量（约 46 行配置 + 迁移表），必须与"4 技能"内容同批，单独改会造成旧节点不可用。
2. **星图 `skill_slots` / `party_slots` 下架**：需同步处理编队容量兜底（`FormationCapacityFloor`）与旧档校验，避免旧档被拒。
3. **英雄页/星图页 UI**：四技能定义、已学/可学/互斥标识、局部洗点入口、特性解锁状态、左下技能条只显示已学技能；需真实截图（UI-01）。
4. **代表职业技能内容**：野蛮人目前 3 个主动（缺第 4 槽），法师 2 个；按规格可留空但必须清单化，已在 §12.1 记录口径。

### 12.3 WP5–WP9 状态

**未开始。** 说明与依据：

- 任务包自述整包为 65–100 人日；WP5（装备 9–13）、WP6（卷轴药剂 7–11）、WP7（重生/离线 7–12）、WP8（UI 整合 4–6）、WP9（账本模拟 3–5）各自都需要**新存档字段 + 新配置表 + 新 UI + 独立验收场景**，无法与 WP4 剩余项在同一收口里安全完成。
- 已具备的前置：HeroInstanceId/Account-Run v5、公共 XP 与 AR/DR、KP/Lmax、天赋点与星图购买、实例隔离与冻结迁移均已自检通过，WP5–WP9 可直接踩在这些接口上开工。
- 建议下一轮顺序：`WP5 装备 → WP6 卷轴药剂 → WP7 重生/离线 → WP8 UI 整合 → WP9 账本与模拟`；每个包沿用本次收口标准（两工程 0 警告、默认 Core、45 项矩阵、Godot 冒烟、证据索引、追加本文件）。

### 12.4 风险与待裁决（不变）

1. 5 人旧档的**正式选择 UI 已实现**，但冻结数据的导出/恢复工具仍不存在（O-01A），文案未作承诺。
2. 同目标多来源 Buff 的运行期证据仍受旧目标解析限制（转 WP4 内容批次）。
3. `Lmin` 永久购买继续关闭；`xp_gain_pct` 与特性解锁价格均为 [H] 原型，不得当作正式经济。
4. 冻结英雄、装备/药剂等 Run 资产在**正式重生**中的清除路径尚未实现（WP7 范围），当前只有"放弃本轮"与"清除全部进度"两个已落地入口。
### 12.5 WP5 尝试与回退（如实记录）

本轮尝试直接进入 WP5（六部位装备），已起草 `item/equipment*.csv` 与 `Equipment.cs`（掉落/候选/严格更优比较/批量≥5/存档回补）。在收口检查中发现：装备数值需要与 `Actor.Attack/MaxHp`、`CaptureProgress` 的存档口径、星图贡献差量与旧档校验同时对齐，单轮内无法在不破坏"构建与 45 项回归全绿"的前提下完成验证，**已整体回退**（删除脚手架代码与未被消费的配置表），仓库保持可编译、全绿状态，未留下半成品。

结论：WP5–WP9 需要按包独立开工；建议每包以"配置表 + Core 状态与迁移 + 命令 + 存档校验 + 定向用例 + UI"为一个完整收口单元，而不是与 WP4 尾项混做。