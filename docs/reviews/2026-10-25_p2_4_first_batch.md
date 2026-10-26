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
---

## 13. 第二批 WP5：装备与回村换装（2026-10-25）

### 13.1 已实现

| 项 | 实现 | 证据 |
|---|---|---|
| 六部位与品质 | `item/equipment.csv`（武器/头盔/胸甲/手套/长靴/指环，各一件主属性 ATK 或 HP）＋`item/equipment_quality.csv`（普通/优秀/稀有/传奇，倍率 1/1.25/1.6/2.1，权重和 100）；`Config.LoadEquipment` 强校验 | `WP5_EQUIP_PASS` 数据合同段 |
| 实例绑定掉落 | 击杀时按 `lootRandom` 掷 [H] 概率（`equipment_drop_pct` 已加入原型表，默认 0.25）→ 随机出阵英雄实例×随机部位×怪物等级×品质；数值唯一公式 `(基础+每级×(等级-1))×品质倍率` | 同上 |
| 候选保留与手动穿戴 | 候选只在"数值更高"时替换；`EquipCandidate` 必须玩家点击，按"新总-旧总"增量调整 `Attack/MaxHp` 并同步 `Actor.EquipmentAttack/Hp` | 同 |
| 批量换装门槛 | `BatchEquipReadyCount`/`BatchEquipCandidates`：≥5 个不同"英雄×部位"可明确提升才一次执行；4 件及以下返回 0 且不改状态 | 4件拒绝 / 6件执行两段断言 |
| 回城卖出 | `SellInferiorEquipment` 挂入 `SellJunk`：只卖"同属性且不优于已穿"的候选（按 0.35 折金），跨属性与更优候选保留 | 卖出与保留断言 |
| 存档一致 | `SessionSnapshot.Equipment` 随档保存；读档校验部位/等级/品质/数值公式/重复项，并断言 `Actor.EquipmentAttack/Hp` 与穿戴总和一致（防止重复叠加） | 读档与二次读档 |
| UI | 右栏逐部位候选卡（显示候选等级/品质/属性/当前对比）＋"一键换装（N件）"按钮（<5 禁用并说明）；真实点击与截图 | `03-equipment-batch.png`、`03-equipment-equipped.png`、`FIRST_BATCH_UI_PASS` |

### 13.2 顺带修正

- 职业特性节点从"借用既有 tree0 被动"改为**独立特性节点**（`warrior_trait` 等 5 个，tree2 row3、`required_points=0`、需星图解禁），原因是原做法会把整系前置锁死，导致 `INTERACTION_UI_SMOKE` 的既有学习链失败。修正后 5 职业仍各 1 个特性节点，且不影响既有天赋区域。
- 天赋总节点 61→66，`SkillMigrationChecks`/`SkillPreviewChecks` 的数量断言同步（结构断言未放宽）。

### 13.3 验证（`artifacts/qa/p2-4-first-batch/wp5-*.log`，全部 exit 0）

两工程 0 警告 0 错误；默认全量 Core PASS（51 条 PASS）；`--regression-matrix` **46/46**；`--legacy-run-migration` 925 份分类不变；Godot `--headless --smoke-test` `SMOKE_OK`；`--first-batch-smoke` `FIRST_BATCH_UI_PASS`（含装备卡与批量换装点击）。

### 13.4 WP5 未完成

1. 装备**不参与 Armor**（符合 INV-05），但 HP 装备通过直接调整 `MaxHp` 生效，尚未接入"受伤/治疗/复活"的完整比例规则复核。
2. 装备卖价系数 0.35 为 [H] 原型；正式经济归 P2-5A。
3. 装备词缀、二级洗练、套装、英雄页独立装备页签均未做（规格已列为不做项/后续）。
4. 旧档不含装备 → 读档为空，无迁移负担；尚无"装备跨正式重生"路径（WP7）。

### 13.5 下一包

WP6 卷轴与药剂：至少 1 种单体攻击卷轴（库存恰好-1、失败不扣、真实伤害）＋ 1 种主动 Buff 药剂（4 槽、生效占槽、同种仅一瓶生效、满槽不覆盖、待用可丢弃、运行时钟冻结）。随后 WP7 重生/离线、WP8 UI 整合、WP9 账本与模拟。
---

## 14. 第二批 WP6：卷轴与药剂真实使用链（2026-10-25）

### 14.1 已实现

| 项 | 实现 | 证据 |
|---|---|---|
| 单体攻击卷轴 | `item/scroll.csv`（`scroll_bolt` 1.8 倍、[H] 掉率 0.12）；击杀掉落进本轮库存；`UseScroll(itemId, monsterIndex)` 走**统一伤害管线**结算真实伤害，成功才恰好 -1，非法目标/无库存拒绝且不扣 | `WP6_CONSUMABLE_PASS`（真实伤害、库存 -1、耗尽后拒绝） |
| 药剂四槽 | `item/potion.csv`（`potion_might` 30 秒、攻击 +25%、[H] 掉率 0.12）；槽位固定 4（UI 预留 8 槽布局），掉落进第一个空槽，**满槽直接放弃、不覆盖** | 满槽不覆盖、4 槽占满断言 |
| 生效规则 | 生效中仍占槽；同种药剂**仅一瓶可生效**（其余待用瓶使用被拒）；`DiscardPotion` 仅待用可丢，生效中拒绝并说明"产品尚未裁决" | 并发拒绝、丢弃规则断言 |
| 计时与冻结 | `AdvanceConsumables` 挂在 `GameSession.Tick`（20Hz 逻辑步），到期清空原槽并撤销加成；暂停/关闭不 Tick 即自动冻结 | 20 tick 递减、到期撤销减益断言 |
| 存档 | `SessionSnapshot.Scrolls/Potions`；读档校验卷轴 ID、槽数、剩余时间上界、同种单瓶，旧档无消耗品字段按全空槽兼容 | 读档保留生效与剩余时间、二次读档一致 |
| UI | 战斗区下缘横向卷轴条（库存 + "对最近敌人使用"）；右下 4 药剂槽（待用/生效中/空槽三态，"丢弃"按钮仅待用可用） | `04-consumables-slots.png`、`04-potions-active.png`、`FIRST_BATCH_UI_PASS` |

### 14.2 验证（`artifacts/qa/p2-4-first-batch/wp6-*.log`，全部 exit 0）

两工程 0 警告 0 错误；默认全量 Core PASS；`--regression-matrix` **47/47**；`--legacy-run-migration` 925 份分类不变；Godot `--headless --smoke-test` `SMOKE_OK`；`--first-batch-smoke` `FIRST_BATCH_UI_PASS`（券轴条/药剂点击/生效占槽截图）。

### 14.3 WP6 未完成

1. 仅投 1 种卷轴与 1 种药剂；卷轴全集、自动施放、药剂 8 槽扩展开启均未做（规格允许）。
2. 药剂加成通过 `EffectiveValue(atk)` 临时倍率生效，未占用 `Actor.Attack`，因此不会随存档叠加；但"负面药剂/多类型叠加"未涉及（[O] O-06B 未裁决）。
3. 卷轴目标选择目前为"最近敌人"自动取第一个存活怪；无点选/取消流程，属 UE 待完善项。
4. 离线结算（WP7）尚未接入，故"离线不消耗药剂"仅由"不 Tick 即不推进"保证，未做专项离线用例。

### 14.4 下一包

WP7 重生、账号永久结构与有限离线：第四章通关后主动重生、Run 资产清零而 Account 权益保留、放弃本轮/正式重生/清除全部进度三命令区分、有限时长离线结算与幂等防重领、关闭期间药剂冻结随读档恢复。
---

## 15. 第二批 WP7：重生、账号结构与有限离线（2026-10-25）

### 15.1 已实现

| 项 | 实现 | 证据 |
|---|---|---|
| 重生门槛 | `CanRebirth`：必须完成第 4 章主线（`ChapterMaps[3].MainClearPoi` 已在 Cleared），否则给出可读原因 | `WP7_REBIRTH_PASS`（未通关拒绝） |
| 重生事务 | `Rebirth(out AccountState, out reason)`：只写入已批准事实（`ch4-cleared`、`rebirth:<n>`、`RunOrdinal+1`），**不发放成就点**；旧轮标记 `Retired` 防止把已清零资产写回 | 账号事实、`Retired` 断言 |
| Run 清零 | 新轮经 `StartRun(seed, heroes, account)` 重建：XP/KP/金币/星图/装备/卷轴/药剂/等级全部为初始值，账号事实与轮次序号保留 | 新轮资产与账号对照断言 |
| 三命令区分 | 放弃本轮（`AbandonRun`）不产生任何重生事实；正式重生单独确认框；清除全部进度仍是 `SaveStore.Reset` 且额外强确认 | 放弃无事实断言 + 设置页三入口 |
| 有限离线 | `SettleOffline`：以存档时间戳为锚点、按 `offline_cap_seconds` 封顶（默认 7200s），只结算 XP/KP/金币（60/1/30 每小时，[H]）；结算后写回时间戳，**同一时间段不重复领取** | 半小时 30XP/15金、重复结算 0、10 小时封顶 7200s |
| 不做自动动作 | 离线不升级/换装/天赋/星图/KP/城堡/卷轴药剂；药剂计时只在 `Tick` 推进，离线不 Tick 即冻结 | 等级不变、药剂剩余时间不变断言 |
| UI | 设置页新增"正式重生（需完成第4章主线）"独立确认框；打开存档时弹出一次性离线收益摘要 | `05-settings-rebirth.png`、`wp7-ui.log` |

### 15.2 验证（`artifacts/qa/p2-4-first-batch/wp7-*.log`，全部 exit 0）

两工程 0 警告 0 错误；默认全量 Core PASS；`--regression-matrix` **48/48**；`--legacy-run-migration` 925 份分类不变；Godot `--headless --smoke-test` `SMOKE_OK`；`--first-batch-smoke` `FIRST_BATCH_UI_PASS`。

### 15.3 WP7 未完成

1. 正式重生的"永久权益"目前只有 QA 事实与轮次序号，**正式 AP 经济与第五席/商店**按 O-07C 仍未开放。
2. 离线算法为 [H] 固定每小时配额，未按"离线前合法收益能力"做动态估算（规格允许先可审计限额，正式数值留 P2-5A）；离线实物掉落未启用（O-07B）。
3. 后台运行（窗口最小化）仍走正常自动战斗，未单独观测长跑；跨设备时间回拨未做防作弊处理（仅以存档时间戳单调判断）。
4. 重生 UI 无动画/汇总页，只有确认框 + 状态栏结果。

### 15.4 下一包

WP8 UI/UE 整合与多状态回归（七分区、旧入口迁移审计、多尺寸/长文案/满槽/连续点击/读档重载实机回归，截图证据）；随后 WP9 账本、模拟玩家升级动作与 P2-5 交接。
---

## 16. 第二批 WP8：UI/UE 整合与多状态回归（2026-10-25）

### 16.1 现状复核（七分区 A–G）

| 分区 | 实现要点 | 证据 |
|---|---|---|
| A 顶栏 | 游戏/英雄/星图/地图 ＋ 标题 ＋ GM/设置/暂停，旧入口全部保留 | `01-main-screen.png`、`05-settings-rebirth.png` |
| B 战斗主视区 | 中左大面积战斗/门/房间/速度，右侧交互不侵入 | 同上 |
| C 卷轴横栏 | 战斗区下缘独立横条：库存文本＋"对最近敌人使用" | `04-consumables-slots.png` |
| D 右上货币 | 金币与 KP 分列（`currencies[0]`/`[1]`） | `01-main-screen.png` |
| E 右侧交互 | 公共XP → 逐人升级卡 → KP 购买卡 → 装备候选卡/一键换装 → 城堡卡，滚动区独立 | `03-equipment-batch.png` |
| F 左下队伍 | 公共XP、逐人 Lv/HP/AR/DR、技能图标/CD、空席标注 | `02-lmax-purchased.png` |
| G 右下药剂 | 4 槽三态（空/待用/生效中）＋丢弃按钮，预留 8 槽布局 | `04-potions-active.png` |

### 16.2 已覆盖的多状态（真实 Godot 点击）

1/3/4 人创队与 4 同职业、0 人拒绝、锁队；KP 评级不足拒绝与达标购买；指定英雄升级；装备 6 件候选与一键换装；药剂待用/生效中/满槽/丢弃；卷轴库存与使用；设置页放弃/重生/重置三入口并存；存档重载后上述状态自洽。

### 16.3 WP8 未覆盖（明确缺口）

1. **多尺寸缩放**（不同窗口/全屏/DPI）未逐一回归；当前仅 1440×900 单窗口实机。
2. **长文案/超长本地化**、无货币/满槽极端组合的自动截图矩阵未建立。
3. **连续快速点击**未做压力测试（仅单次点击序列）。
4. 旧入口"迁移/退役"审计表尚未与设计Owner逐条核对（当前为保留为主，未退役入口）。

---

## 17. 第二批 WP9：账本、模拟准备与 P2-5 交接（2026-10-25）

### 17.1 本轮新增：模拟器"玩家手动升级"动作

- `SimulationConfig` 新增 `player_policy.upgrade { mode: none|greedy, interval_seconds }`（默认 `none`，既有场景与基线哈希不受影响）。
- `SimulationRunner.ExecUpgrade`：每次到点只升**一名**英雄**一级**，优先当前最便宜的可升级英雄，事件记 `upgrade_applied`（含 lv 前后与余额），不自动分配天赋点、不换装、不买星图。
- 新增场景 `tests/Simulation/U_upgrade_greedy.json`（30 分钟，greedy/30s）。实跑对照：

| 场景 | 等级 | 击杀 | 重试 |
|---|---|---|---|
| 无升级策略（此前基线） | 1 | 455 | 23 |
| `U_upgrade_greedy` | **12** | 549 | **1** |

说明：模拟器已能表达"玩家主动升级"的成长曲线，不再被迫用永远 Lv.1 的脚本评估新版成长；该动作仍代表显式玩家决策，不代表线上自动升级。

### 17.2 账本与可复现性

- `SIM_LEDGER ... diff=0 unattributed=0`（金币收入-支出-余额闭合）、`SIM_XP ... unexplained_hundredths=0`（XP 守恒，含新的入池倍率路径）。
- 同种子两次运行字节一致由 `SimulationToolChecks` 覆盖（默认全量 Core 中执行）。
- 本轮各资源新增来源的去向：装备只在"英雄×部位"候选/穿戴间移动，卖出按 0.35 折金并入 `SellJunk` 同一次入账；卷轴/药剂只改库存与槽位，不动金币/XP/KP；离线结算只动 XP/KP/金币三项并立即回写时间戳。

### 17.3 WP9 未完成

1. `INT-01~INT-08` 的**端到端功能剧本**尚未固化为一条自动脚本（目前分散在 WP1–WP7 的定向用例与 UI 冒烟中）。
2. 旧 `E_min_party` 等场景仍请求旧星图扩编/旧 Armor 残留断言，未逐条清理（`G-G` 门禁未关闭）。
3. 装备/卷轴/药剂的**逐笔流水账本**（行级 CSV）未建立，只有总额守恒。
4. 10 小时曲线、金币深度、TTK 等属 P2-5A，本轮不做结论。

### 17.4 第二批总体状态（截至本轮）

| 包 | 状态 |
|---|---|
| WP4 技能/天赋/星图 | 机制层完成（增量 1–3）；剩：旧 `CastTalent` 退役、`skill_slots/party_slots` 下架、英雄页/星图页 UI |
| WP5 装备 | 完成机制＋UI＋证据；剩：词缀/洗练/套装、HP 装备的比例复核 |
| WP6 卷轴药剂 | 1 卷轴＋1 药剂完成；剩：全集、自动施放、8 槽扩展、[O] O-06B |
| WP7 重生/离线 | 完成门槛/事务/离线/UI；剩：正式 AP 经济（O-07C）、离线实物（O-07B）、时间回拨防作弊 |
| WP8 UI 整合 | 七分区与多状态已回归；剩：多尺寸/长文案/连续点击矩阵、旧入口审计表 |
| WP9 账本/模拟 | 手动升级动作已交付、账本守恒与固定种子通过；剩：端到端脚本、旧场景清理、行级流水 |
## 18. V1.6 遗留返修首轮：A-04/A-05与独立边界补证（2026-10-26）

### 18.1 来源、授权与当前范围

用户直接要求执行 `docs/temp/P2-4 已授权遗留闭环返修｜V1.6/Steam_D2_P2-4_原范围补齐执行包_V1.6`。工程执行AI按其中00～04及A-01～10矩阵调查；temp原件保持只读。未实施S06～S14新增内容、Lmin、AP或正式调参。S15仍仅有只读权限，本轮尚未完成S15交付。

**整包未完成。** 工程自检仅对以下已改部分PASS；A-01～03旧权威切换及A-07最终联合验收保持OPEN，不用既有全量回归通过替代新版验收。

### 18.2 本轮实际变更与自主适配

- `GrowthUi.cs`：Lmax门槛直接使用与Core相同的 `(Lmax+1)*MonsterRatingPerLevel`，显示各上阵实例须分别满足AR/DR；无等级换算或向上取整。评级标签明确[H]。删除重复“伤害/攻击”和无新版效果的Armor展示，展示首种怪物ATK/HP示例与怪物AR/DR；这不是Boss胜率预测。Lmin文字为退休当前最低等级及退休后下限，按钮和Core仍禁用。
- `Consumables.cs`：新增只读 `NearestScrollTarget` 与命令 `UseScrollOnNearest`，沿用原 `UseScroll` 已确定的作用方（首名存活英雄），按实际战场距离平方选择存活且在当前房间的怪物，等距按原怪物索引稳定决胜。原先取列表第一个活怪已被覆盖；唯一伤害与扣减路径不变。
- `HudLayout.cs`：五槽走上述最近目标命令；无合法目标时禁用。未加入手动瞄准或卷轴自动施放。
- 装备大按钮预览按队伍/配置部位顺序展示五项明确升级，执行仍处理全部可替换项，不再把ATK和HP绝对值混排成统一战力。更换会话清除旧成长操作反馈，避免跨Run残留“已穿戴”提示。
- `Wp5EquipmentChecks.cs`补满血/受伤/0血、升降HP装备、超上限截断、同职隔离、两次正式编码重读；**保持既有绝对HP不变、仅下降时截断，不增加等比治疗规则**。尚无卸装命令及实际救援复活前后完整组合证据，A-09不标完全关闭。
- `Wp6ConsumablesChecks.cs`补列表首怪较远、第二怪较近、等距、目标死亡后重查、库存用尽重复调用、无目标不扣。`FirstBatchSmoke.cs`补对应真实Godot槽控件触发及截图、三种实际窗口尺寸。槽位通过 `Pressed` 信号程序触发，不等同物理鼠标命中/按下松开链路。

替代方案：不采用距离至队伍中心，因为现有卷轴已有明确施法英雄；不采用跨属性数值战力排序；不修改HP治疗价值规则。影响限于已授权语义修正。

### 18.3 验证与证据

Windows；dotnet SDK 10.0.401，项目net8.0；Godot 4.7.2.stable.mono.official.ed1daf0bf；NVIDIA RTX3050/OpenGL Compatibility。Godot从现有本地安装压缩包解压到忽略的 `artifacts/tools/godot/`，未修改依赖或玩家存档。`ClientSettings`对 `--first-batch-smoke` / `--smoke-test`设置automation，存档位于 `artifacts/qa/`。

| 命令 | 退出码/结果 | 证据 |
|---|---|---|
| `dotnet run --project tests/IdleCrpg.Checks.csproj` | 0，默认全量回归通过（旧WP4通过不代表固定4+T完成） | `artifacts/qa/p2-4-v16/core.log` |
| `dotnet run --project tests/IdleCrpg.Checks.csproj -- --wp5-equipment-checks` | 0，WP5/6/7 PASS，含新增边界 | `artifacts/qa/p2-4-v16/targeted.log` |
| `dotnet build Idle-CRPG/Idle-CRPG.csproj` | 0，0警告0错误 | `artifacts/qa/p2-4-v16/build.log` |
| `<Godot .NET> --headless --path Idle-CRPG -- --smoke-test` | 0，SMOKE_OK | `artifacts/qa/p2-4-v16/headless.log` |
| `<Godot .NET> --path Idle-CRPG -- --first-batch-smoke` | 0，FIRST_BATCH_UI_PASS，Windows实际渲染 | `artifacts/qa/p2-4-v16/ui.log` |

最终截图：`artifacts/qa/first-batch-ui-20261026-032007-2840736-49cb9834f43c473683e843ff31097838/`。包括 `lmax-purchased.png`、`equipment-batch.png`、`hero-equipment-bag.png`、`hero-equipment-worn.png`、`layout-1280x720.png`、`layout-1920x1080.png`、`scroll-nearest-before.png`、`scroll-nearest-after.png`。已查看1280×720、装备合批和卷轴前后关键渲染；不声明所有截图逐像素验收。夹具Seed：装备7701～7711；卷轴8803；真实截图中的战斗为明确QA夹具，非正式随机现场。截图、日志、工具不提交。

### 18.4 逐项状态与迁移待裁决

| ID | 工程自检状态 | 尚未完成 |
|---|---|---|
| A-01 | OPEN | 固定S1～S4+T英雄页、旧编辑入口退役 |
| A-02 | OPEN/待权益裁决 | 23旧主动投入及已购槽位隔离/返还方案、生产权威退休、旧档完整差异清单 |
| A-03 | OPEN | 四同职同目标多来源Buff/召唤与独立CD新版证据 |
| A-04 | PASS（本轮语义修正） | 设计Owner/用户复核未发生 |
| A-05 | PASS（Core+Godot槽控件信号触发） | 物理鼠标输入链路尚无证据 |
| A-06 | OPEN（部分补证） | 4+T十二状态、长数值压力、全部截图检查及物理输入 |
| A-07 | OPEN | 依R-A1切换后的INT-01～08联合脚本 |
| A-08 | OPEN | 物品逐笔流水与旧模拟场景退役 |
| A-09 | 部分PASS | 实际复活、卸装完整组合尚未覆盖 |
| A-10 | 部分PASS | 0/1/4/5件及跨属性完整UI矩阵；本轮已有6件与单英雄入口证据 |

已按00文件“旧存档映射如有不可逆损失、金币退款因货币模型不清…请产品裁决”的边界向用户提出：①原数据隔离停用，允许旧技能点免费退回、旧星图金币不自动补偿；②全部旧投入隔离冻结，稍后统一裁决。**等待答复，尚未执行这两种方案中的任何一种；未删除或重映射旧投入，未写玩家进度。** 用户答复决定该迁移价值政策，已有工程实施授权继续有效，不需重批整包。

### 18.5 可回传摘要与三方状态

本轮完成Lmax评级量纲修复、Lmin禁用语义修复、卷轴真实最近合法敌人、合批预览排序修复，并补HP边界及三尺寸Windows渲染/槽控件触发。默认Core/build/headless/真实渲染均通过，但旧技能权威仍在，V1.6不结项；旧价值迁移政策待用户裁决。后续仍需完成R-A1、A-08及最终INT联合证据，S15仅只读。

- E：已改范围PASS；V1.6整包OPEN。
- V-Design：未复核，未向外部设计AI发送材料。
- V-User：未验收；旧档价值政策待答复。

## 19. V1.6权益裁决后闭环（2026-10-26，覆盖第18节待决状态）

### 19.1 来源与规则覆盖

用户明确裁决：**“保留隔离，技能点可免费退回；金币暂不补偿”**。沿用最初执行V1.6的授权，完成R-A1～R-A4；无需再次申请普通工程适配。temp输入保持只读，无commit/push/merge。

覆盖旧生产规则：旧装配/槽数资格、23个未绑定CastTalent主动、技能槽/扩席购买以及已无作用的付费全树洗点/折扣退出生产；固定S1～S4+独立T、免费区域洗点为当前权威。正式Lmin仍关闭，P0地图不改，S14/AP/第五席/新内容/10小时调参未实施。

### 19.2 实际变更与旧→新清单

| 旧能力/字段 | 现在的权威与价值处理 | 证据 |
|---|---|---|
| 23个无skill_id旧主动、CastTalent | 节点定义保留用于读取/历史QA；不能生产学习、装配、AI选择或提交；旧等级转RetiredTalents，尚未返还仍占原实例预算，玩家免费返还一次，原记录不删 | `FixedSkills.cs`；`migration/legacy-disposition.csv`逐项23行；FIXED_SKILLS_PASS |
| SkillIds、EquipSkills、SetTalentLoadout | SkillIds只是从固定定义/实例学习生成的兼容缓存；两个旧命令在生产拒绝；提交层再次检查已学资格 | Slot/Runtime/FixedSkills；旧API负向与S2真实Effect测试 |
| active_skill_max_slots/initial_slots | CSV仍作为历史兼容列校验，生产固定4定义不读它决定资格或容量 | `ActiveSkillSlots`、`TalentSlotCapacity`分支 |
| S1～S4学习与T | S1开局保留原初始一点投入，局部重置不移除基础rank1；S2～S4学习即自动施放；缺定义明确未投放；T按TargetId=ClassId买资格，同职共享资格、实例独立点数 | 新英雄技能页、固定4+T/星图/真实GUI证据 |
| 旧装配/技能原字段 | LegacySkillIds及LegacySkillData保存原投入、装配、来源加成、冷却、光环JSON，永不进入生产战斗 | SHA256旧档输入、新版重读与Legacy归档 |
| skill_slots/party_slots | 原CSV及Run.Stars购买等级保留，商店可见性/购买均拒绝；不退金币；普通席位仍4 | disposition.csv另8行；星图截图、CanBuy负向 |
| talent_reset/discount | 因免费区域重置已替代收费全树重置，继续销售会形成无效权益，故一并下架，保留已购记录且不退款；生产全树重置命令拒绝、费用查询0 | disposition.csv另2行；历史付费测试显式归档 |
| 旧超四席英雄 | 原Legacy.Heroes冻结事务保持，不因本次退休而删人 | LEGACY_SELECTION_PASS、旧五人/候补保护 |
| 保存与跨Run | FixedSkillsVersion=0一次迁移，1只校验；Legacy.Skills/Stars跨Run保留；自动后台写盘、放弃、重生均调用PreserveLegacy，修复旧空文档写盘可能漏带隔离区的问题 | SaveStore/ClientSettings、往返/跨Run/全清回归 |

未知节点/已删技能ID不静默删除或清档，主备份保留并提示人工处理。旧T若无本职业资格，隔离其投入而不偷偷解锁。旧档缺S1时用StarterPointWaived补回基础攻击、不挤占原投入；新英雄点数规则不变。

### 19.3 自主适配与影响

1. 当前内容仍是原型：五职业合计16个非空固定定义，**前四职业只有12个**，不是S06新16行为。为使既有定义可用，主动按S顺位归区，非T被动沿旧tree+1归区，T单独为5；主动根不再以已退休旧主动为前置，被动只继承同区有效前置/原投入门槛。保留旧链或把旧主动硬映射为新技能会恢复双权威/改变语义，因此未采用。未导入98节点或31星图。
2. 区域重置只清当前实例该区动作、Pending、弹道、Buff、召唤与CD；不ResetSkillRoom整房清场，避免误删其他实例/区域状态。来源额外等级保留休眠，未学节点不生效。
3. 最近卷轴沿既有首名存活英雄为作用方，按合法目标实际距离及稳定索引选择。卷轴致死会触发同种卷轴掉落，旧实现用结算前库存写回会吞收入；改为伤害结算后从当前库存扣一张，新增100%掉落致死回归。
4. 流水使用Core现有事务，不新增伤害或收益实现。AssetAudit保存Run事件序号/逻辑Tick，内存缓存有界8192；模拟器逐事件订阅输出`asset_ledger.csv`。宝箱XP单列room_event，不混入击杀来源。装备记录英雄×部位与候选/已穿状态、药剂记录槽位状态，不把事件数当物品数。离线流水由联合脚本按正式结算前后观察输出。
5. 旧付费洗点/装配UI测试原文、E_min_party旧配置、InstanceIsolation旧装配断言保留在`tests/fixtures/retired_loadout_ui_2026_10_26/`；新版用学习/只读固定技能/禁装配取代。旧战斗专属夹具显式启用历史QA调度，不冒充生产验收。新模拟器改用真正Config.Load；E_min_party开局3人，不再买扩席。未删失败用例换绿。
6. HP装备维持原绝对HP不变、上限降低截断语义；补0血/满血/受伤、增减、QA卸装、实际救援、双重读档、同职与跨属性保护，不增加等比治疗。

### 19.4 A-01～A-10与联合闭环

| ID | 工程结果 | 主要证据/边界 |
|---|---|---|
| A-01 | PASS（机制/现有原型） | 固定4卡+T区、S2学习自动生效、无编辑入口；未投放清楚显示，不声称完整职业内容 |
| A-02 | PASS_WITH_OPEN_ITEMS | 23旧主动及10旧星图节点分类/隔离/禁止消费，免费返还幂等、原字段与输入SHA保留；历史925份冻结语料缺失，不能声称已复跑 |
| A-03 | PASS | 四同职业同目标4个Strongest来源贡献、单源撤销/完整快照/正式检查点重开；重复职业召唤归属与CD独立；CSV含SourceHeroInstanceId映射，另有真实窗口图 |
| A-04 | PASS | AR/DR量纲与Core同源；Lmin禁用；Armor不作DR或新版伤害展示；原型分段/首种怪属性示例标H |
| A-05 | PASS | 两怪异距/平距/死亡重查、无目标/零库存不扣、GUI槽位鼠标事件命中、致死再掉同种库存守恒 |
| A-06 | PASS（本机覆盖） | 37张真实渲染图；3尺寸、4+T状态、长数值/中文、控件命中/读档；未覆盖其他OS/DPI，未冒充人工体验 |
| A-07 | PASS（明确QA前置） | 一条`--p2-4-closure-checks`命令，INT-01～08真实命令链与原子账本；第四章终点为QA前置，不是四章实战或10小时验证 |
| A-08 | PASS | Core逐笔资产事件/模拟器asset_ledger.csv；三币逐行及按Run守恒；E_min_party退役扩席购买，宝箱来源区分 |
| A-09 | PASS（技术边界） | HP上下界、QA卸装/实际救援、两次正式重读、同职隔离与跨ATK/HP保护 |
| A-10 | PASS（本机覆盖） | 英雄单人入口与全队合批分开；0/1/4/5项UI矩阵、6件实际全部执行、跨属性不自动替换/卖出；五条预览按队伍/部位稳定顺序 |

INT原始状态、执行覆盖、成长时间轴、三币/物品流水、异常与复跑说明统一交付[本轮单份统计报告](../data/2026-10-26_12-14_data_p2_4_v16_closure.md)，不拆A/B/C或复制原始CSV到docs/data。

### 19.5 UI证据与真实输入说明

最终37图目录：`artifacts/qa/first-batch-ui-20261026-042440-9996104-4d024ded00d5415e8a5a7aa14f0cd32d/`。

- `fixed-skills-s2-unlearned-t-locked`、`fixed-skills-s2-learned`、`fixed-skills-local-reset`、`trait-shared-hero-1/2`、`trait-invested-only-first-instance`：四技能/T的学习与共享资格，实例投资不串。
- `fixed-s2-effect-and-hud-cooldown`、`fixed-s2-effect-removed-s1-retained`、`four-same-class-buff-sources-and-cooldowns`：实际Effect与CD，不只UI“已学”。四圣骑士为明确QA解锁/密集坐标夹具，不代表首轮投放或正式空间布阵验收。
- `star-retired-slots-absent`、`star-class-trait-purchased`：旧槽位/旧收费洗点不售，资格效果可观察。
- `hero-equipment-bag/worn`、`equipment-count-0/1/4/5`、`equipment-batch/equipped`：双入口、逐卡/合批、N件执行。
- `scroll-nearest-before/after`、`potion-four-pending`、`potion-four-slots-one-active`：五格与药剂占槽状态。
- `layout-1280x720`、`layout-1920x1080`、`long-values-1440x900`、`long-values-1280x720`：三币等宽/长数字；数值紧凑显示完整值在tooltip，长英雄名缩略而XP费用保留可见。

学习/重置/两换装入口/卷轴使用注入鼠标按下松开事件，经Viewport GUI命中测试，不再只EmitSignal。状态前置仍可使用明确QA/Core命令。不是人工鼠标体验验收。已查看关键页面与前后状态，不宣称每图逐像素人工验收。

### 19.6 验证索引与局限

本机Windows、dotnet SDK10.0.401/net8.0、Godot4.7.2.NET、OpenGL Compatibility/NVIDIA RTX3050。Godot本地可执行文件在忽略的`artifacts/tools/godot/`，未写入本机路径到正式配置。

| 入口 | 日志/结果 |
|---|---|
| 默认Core | `artifacts/qa/p2-4-v16/fixed-core.log`，退出0 |
| `--p2-4-closure-checks` | `closure.log`，退出0，FIXED_SKILLS_PASS/INT_01_08_PASS |
| `--regression-matrix` | `regression.log`，50 PASS/0 FAIL，退出0 |
| Godot工程build | `fixed-build.log`，退出0，0警告0错误 |
| Godot headless `--smoke-test` | `fixed-headless.log`，退出0，SMOKE_OK |
| Godot有窗口`--first-batch-smoke` | `fixed-ui.log`，退出0，FIRST_BATCH_UI_PASS |
| S15只读脚本 | `s15/read_only_audit_evidence.md`、`source_hashes.csv`；执行前后输入哈希一致 |

原历史925份冻结语料清单`artifacts/qa/p2-4-first-batch/legacy-saves-manifest.json`当前不存在，专用入口报告缺样本；未擅自冻结新生成数据冒充旧语料。当前新合成旧档、未知ID保留、备份、旧五人冻结、跨Run归档与不重复补偿均通过。没有读取/覆盖玩家存档。无Linux/macOS、其它DPI和最小化后台长稳结论。

### 19.7 S15只读调查与范围外项

`tools/Audit-P2V16ReadOnly.ps1`只允许输出artifacts/qa，生成schema_diff.md、id_crosswalk.csv、effect_coverage.csv、source_precedence.md、implementation_packages_proposal.md、read_only_audit_evidence.md及输入哈希清单。**PARTIAL_INPUT**：temp/suggest只有S15引用与汇总，没有S04～S13逐项候选原表，无法逐16技能/98天赋/31星图/28装备/地图逐ID做设计映射；缺失效果标RED/INPUT_MISSING，不按显示名隐式等价。

现有消费者/字段、品质命名冲突、原型H参数与Lmin边界已报告；候选新Effect、完整内容、31档数值/经济、AP/第五席、10小时平衡均未实施。获得原始候选表后才可完成逐项只读对账，新增开发仍须后续明确授权。

### 19.8 可回传设计Owner摘要与三方状态

已完成固定4+T唯一生产权威、旧主动/槽位及失效付费洗点退役；按用户裁决保留隔离、免费主动返还旧点、不退旧金币。修复Lmax量纲/旧Armor文案、真实最近卷轴及致死掉落守恒、合批预览排序、长文本/货币格，并补HP边界与逐笔流水。已有一条INT联合命令和37张本机渲染证据。当前16历史技能跨五职业，首四职业只部署12个，不能报S06完整16行为已实现。

- E：**PASS_WITH_OPEN_ITEMS**；已授权返修工程自检通过，遗留是历史925份语料缺失、S15原表缺失与跨平台/人工验收边界，不据此擅自扩展玩法。
- V-Design：未独立复核；未向外部设计AI发送资料，不声称已同步。
- V-User：未验收；本次旧权益政策已明确裁决，不等于用户已验收全部界面与体验。

补充证据说明：R-A1权威切换前的旧技能HUD/编辑入口截图保留于`artifacts/qa/first-batch-ui-20261026-031823-2795130-3b9d747565eb495fafced34da0e16d54/`（当时A-04/A-05已先修，不能当作整个V1.6改动前快照）。Strongest择强且不重复叠加、强来源离开后回退弱来源的真实数值断言同时由`SkillBdSupportChecks`保留；新FixedSkillClosure补四实例同目标及撤销/存档路径，两者共用同一Buff聚合实现。

分辨率证据补充：最终截图目录含`screenshots.csv`逐图记录窗口、逻辑视口与PNG尺寸。窗口1440×900/1280×720/1920×1080均实际运行；逻辑视口始终1440×900；保持16:10内容比例后PNG分别1440×900/1152×720/1728×1080，不能把PNG尺寸与原生窗口尺寸混用。最终GUI日志及CSV均来自当前编译结果。

工作区并行编辑说明：收尾时检测到另一份从本机安装目录运行的Godot4.7.2编辑器（进程启动于12:20），同时工作区存在`Idle-CRPG.csproj`的SDK4.7.0→4.7.2差异及`Idle-CRPG.csproj.old.2`备份。未改写、回滚或移动这两项，不把它们计入本轮主动实施；最终Godot构建/运行以工作区实际SDK4.7.2和引擎4.7.2通过的日志为准。正式config与temp没有本轮差异。新C#文件的.uid为编辑器生成的源资源元数据，保留。

---

## 20. C-LEGACY-5 迁移入口返修：弹窗溢出视口与创角按钮死锁（2026-10-26）

### 20.1 需求与授权来源

用户直接需求（2026-10-26，附实机截图两张）：更新后无法创建新游戏，开始游戏按钮不可用；要求检查账号迁移与创角逻辑。截图现场为“旧档迁移 · 选择本轮英雄（最多4名）”弹窗内含 5 名 Lv.49 英雄、看不到任何操作按钮，关闭弹窗后创角页“开始游戏”呈灰态。属用户直接下达的缺陷修正，不需要设计AI预批；不涉及新玩法取值，不改变 C-LEGACY-5 的冻结与清除边界。

### 20.2 规则覆盖与事实证据

- **规则覆盖**：`AGENTS.md`“旧档超额不得自动裁剪/静默清档”“设置→放弃本轮重新选队”“清除全部进度为唯一销毁入口”；`docs/technical/persistence_settings.md` V1.6 节；本节替代 §10.1 中“UI：`ShowLegacyMigration` 提供逐英雄勾选”一句对可用性的乐观描述，其余数据与事务结论不变。
- **实机复现**（本机 Windows、Godot 4.7.2 .NET、OpenGL Compatibility、RTX3050）：隔离 QA 旧档（5 名在阵、无实例ID）启动后测得
  `LEGACY_POPUP immediate size=(720, 1815) pos=(360, 36) wrap=True contents=(536, 1815)`；
  `GetContentsMinimumSize()` 在 720 高度弹窗内报 1815，窗口被撑成 1815 高、位置被夹到 y=36，确认/取消按钮落在 y≈1784，**完全在 1440×900 视口之外**——玩家无法确认迁移。
- **第二个独立缺陷**：`progressReadBlocked=true` 同时被写成 `start.Disabled=true`，而“稍后处理/取消”与“迁移失败”都不复位该标志，只有“清除全部进度”能解除。两个缺陷叠加即形成无出口死锁：既不能迁移旧档，也不能开始任何新游戏。
- **第三个缺陷**：`ConfirmationDialog` 默认 `dialog_hide_on_ok=true`，迁移事务抛错时弹窗自动消失，错误只写进底部状态栏，玩家看不到原因也没有重试入口。

### 20.3 实际变更（工程自检范围）

- `Scripts/UI/ClientSettings.cs`
  - 迁移框固定尺寸并 `WrapControls=false`、`DialogHideOnOk=false`；说明文字改为显式换行（自动换行标签在首次布局前会报告极大最小高度，是本次撑高的根因），窗口稳定为 720×440 居中。
  - 默认勾选前 4 名候选，确认按钮开箱可用；勾满 5 名时其余选项禁用且确认禁用；新增候补数量与“默认保留前 4 名”说明。
  - 取消仅隐藏弹窗、保留 `pendingLegacy`，状态栏与摘要提示“点开始游戏可重新打开”；迁移失败保留弹窗并就地显示原因（`LEGACY_MIGRATION_FAILED`）。
  - 新增 `BlockProgress`/`ShowMigrationBlockedNotice`：非迁移类读档阻断同样保留旧档、可重复打开提示。
  - `UpdatePartyControls` 不再写 `start.Disabled=progressReadBlocked`；`ResetProgress` 清理待迁移引用与残留弹窗。
- `Scripts/UI/Main.cs`：`Start()` 在阻断状态改为重新打开迁移框/提示框并说明原因，不再静默 return。
- `Scripts/UI/LegacyMigrationSmoke.cs`（新增 QA 入口，`--legacy-migration-smoke`）：在 `artifacts/qa` 播种同形旧档夹具，驱动真实控件完成迁移界面回归与截图；非 automation 会话调用即抛错，不能污染 `user://`。

### 20.4 自主判断与理由

- 采用“按钮始终可点 + 点击进入迁移决策”而不是“按钮禁用 + 另设入口”：玩家对“开始游戏不可用”的直观反馈就是死路，而迁移本身是启动阻塞的唯一合法出口；这样既不静默丢弃旧档，也不新增玩法或放宽 C-LEGACY-5。
- 默认勾选 4 名，玩家仍然可以改成 1–4 名；默认值不改变“必须由玩家确认”的语义，只避免灰态确认按钮被误读为功能失效。
- 未新增“迁移后直接放弃本轮、重开新队”按钮：那会把迁移与放弃本轮两个已批准命令合并成新语义，属产品取舍；当前路径是先迁移（保留旧档英雄）→设置→放弃本轮并重新选队（账号保留），已在状态栏与摘要提示。
- 失败时保留弹窗而不是回到旧行为，是为了满足“原档一字节不改 + 玩家有重试出口”两条不变式。

### 20.5 验证与局限

| 入口 | 命令 | 结果 |
|---|---|---|
| Godot 工程构建 | `dotnet build Idle-CRPG/Idle-CRPG.csproj` | 0 警告 0 错误 |
| Core 全量 | `dotnet run --project tests/IdleCrpg.Checks.csproj` | 退出 0，含 `LEGACY_SELECTION_PASS`（5+1 旧档选择/冻结/幂等/全清） |
| Godot 无头 UI | `<Godot4.7.2 .NET> --headless --path Idle-CRPG -- --smoke-test` | 退出 0，`SMOKE_OK: 交战中`，12 段 UI 冒烟全 OK |
| 真实控件 UI | `<Godot .NET> --path Idle-CRPG -- --first-batch-smoke` | `FIRST_BATCH_UI_PASS`（1/3/4 人创队、重置、存读未回归） |
| 本缺陷专项 | `<Godot .NET> --path Idle-CRPG -- --legacy-migration-smoke` | `LEGACY_MIGRATION_UI_PASS party=4 frozen=1 dialog=(360, 230), (720, 440)` |

专项断言覆盖：弹窗整体在 1440×900 视口内且确认按钮在窗口内；默认勾选 4 名、确认可用；勾满 5 名时确认禁用、回到 4 名恢复；取消后 `start.Visible && !start.Disabled`；点“开始游戏”重新弹出选择框；人为制造迁移事务异常时弹窗保持可见且状态栏给出原因，恢复后重试成功；确认迁移后本轮恢复 4 名、隔离区冻结 1 名、迁移结果落盘、创角入口隐藏。

证据截图（忽略目录）：`artifacts/qa/legacy-migration-20261026-045201-4507727-4269c3b2bde7446ca0bc8eac62d65b03/legacy-migration-dialog.png`（5 名候选、前 4 名预勾选、确认/稍后处理按钮可见）、同目录`legacy-migration-confirmed.png`（迁移后 Lv.49 四人队、第五席仍“尚未解锁”、状态栏“已完成迁移”）。

**局限**：仅 Windows 本机验证；未读取或覆盖玩家 `user://` 真实旧档，夹具为同形合成档（5 在阵、无实例ID、`RunVersion=0`）；未覆盖含候补的旧档在真实窗口下的界面（提示文本已含候补数量分支，逻辑由 `LEGACY_SELECTION_PASS` 覆盖）；未做其他分辨率/DPI 与 Linux/macOS 实机；未做人工全流程试玩验收。

### 20.6 未决项

- 玩家若只想开新队而不保留旧档英雄，当前仍需“迁移 → 放弃本轮重新选队”两步；是否在设置里提供“放弃旧档本轮并直接重开”的合并入口，需产品裁决。
- 迁移候选职业被新版击杀条件锁定时仅作标注（截图#4/#5），迁移后它们在阵可用；是否在编队/候补入口同步说明，属后续体验项。
- 历史 925 份旧档语料不在工作区，不能声称已用真实样本集复跑本次界面。

### 20.7 可回传设计Owner摘要

更新后“开始游戏不可用”是两处界面缺陷叠加，不是数值或存档事务错误：①`ConfirmationDialog` 里的自动换行标签把窗口撑到 1815 高，确认/取消按钮落到 1440×900 视口之外，玩家无法完成 5→4 的旧档迁移；②迁移未完成时开始按钮被永久禁用且取消/失败都不复位，只剩“清除全部进度”。现已固定弹窗尺寸并显式换行、默认预勾选前 4 名、取消/失败均保留原档与弹窗、开始按钮始终可点并重新打开迁移决策。数据侧未改：仍不自动裁剪、不静默清档、冻结英雄只读保留、只有“清除全部进度”销毁隔离区。证据：专项`LEGACY_MIGRATION_UI_PASS`+两张实机截图，Core 全量与 Godot 冒烟、真实控件 UI 冒烟均通过。

- E：**PASS（工程自检）**；Windows 本机真实控件与渲染验证通过，未覆盖跨平台与人工验收。
- V-Design：未独立复核；未向外部设计AI发送资料，不声称已同步。
- V-User：待用户实机验收（需重启客户端后重新打开迁移框确认）。

---

## 21. S15-C0 工程只读完整对账（READ_ONLY_RECON，2026-10-26）

### 21.1 任务与授权边界

来源：`docs/temp/P2-4 S15_C0完整输入与独立Review_V0.3/docs/02_S15_C0_工程只读完整对账执行任务_V0.3.md`（用户直接下达"执行该包"）。授权性质为 `READ_ONLY_RECON`：不改 Core/UI/生产配置/玩家存档，不改 P0 拓扑，不导入 H 数值，不重开 V1.6 返修。本轮只新增只读审计工具与 `artifacts/qa` 报告，并在本 review 追加结论。

### 21.2 环境与输入完整性

- Windows；dotnet SDK 10.0.401；PowerShell 5.1；未运行游戏（纯静态只读），Godot 4.7.2 .NET 版本仅用于确认工程事实。
- 设计源 21 份 CSV：**记录数与 SHA256 全部匹配** `matrices/source_manifest_verified.csv`（1003 条）；审计前后哈希一致（`source_hashes_before_after.csv`，21/21 无差异）。
- P0 压缩包未解压、未改写；P0 键改用工程正式章节 CSV 对账。
- 复跑：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/S15-Audit-P2C0.ps1`，产物在 `artifacts/qa/s15-c0-<时间戳>/`（忽略目录，按项目规范以 review 为报告入口、机器数据留 artifacts）。

### 21.3 覆盖

| 门禁 | 实际 |
|---|---|
| 675 / 154 全量 | `id_crosswalk_engine_complete.csv` 675 行；`effect_coverage_engine_complete.csv` 154 行 |
| 字段对账 | `schema_diff_complete.csv` 258 行（每条给工程列或显式 UNKNOWN/后缀归类） |
| 16 槽差异 | `skill_slot_engine_diff.csv` 16 行 |
| 31 星图 | `star_consumer_engine.csv`（生产 19 节点）+ 设计 31 节点判定 |
| 冲突/差异 | `floor_count_mismatch.csv`（本次为空） |

### 21.4 关键事实（只读核查）

1. **P0 键 393/393 命中**：136 普通地牢 + 31 城堡 + 226 普通层在工程章节配置中逐键存在，层数与 `poi_profile.floors` 完全一致；`match_strength=NONE` 的 P0 键为 0。
2. **楼层节拍无运行时消费者（RED）**：`Config.FloorPlans` 只用于 `CurrentFloorDisplayName`、`FloorRunReport` 与 `Reveal()` 的 `plan.BossActors`；刷怪数量/精英/事件分别来自 `spawn_rule`、`elite.csv`、`EventChance`。226 层的 `CombatCellCount/EliteCellCount/PlannedKill*` 不改变战斗内容，且该表自带 `ConfigStatus=PROPOSED_NOT_BALANCED`。
3. **技能**：生产 16 实投 + 4 空槽；四职业 16 设计槽判定为 REAL_REUSE 4 / MODIFY 12 / 空槽 4 / 行为冲突 9；槽位重绑不破坏存档（`SkillIds` 存 id 不存槽）。
4. **天赋**：生产 66 节点（active 39/passive 27），23 个 active 因不在 `skill_slot.csv` 被判退休；设计 98 节点在生产无同名行，逐条判定 REAL_REUSE 15 / MODIFY 29 / NEW_EFFECT 29 / DESIGN_DECISION 25。
5. **星图**：生产 19 节点中 10 个退休不可买，**可买 9 节点、活跃购买上限 35 阶**；设计路线要求 55–65 次，缺口 ≥20 次，退休节点不可用于充数（`StarConfiguration.CanBuy` 拒绝）。
6. **效果缺口集中在四类**：`trigger.csv`/`proc_limit.csv`/`proc_icd.csv` 全 0 行（无条件触发、无同次/同目标限额与 ICD）；无目标评分（精英/威胁/覆盖）；无精英伤害乘区与精英 XP 收据；被动白名单只认 atk/armor/crit_rate/engagement_capacity。37 个 NEW_EFFECT 均由这四类造成。
7. **装备**：工程 `common/uncommon/rare/legendary`（w=60/25/12/3，×1/1.25/1.6/2.1），设计"普通/稀有/罕见/传奇"无 1:1 对应；28 模板中 18 可落行，10 缺行（6 个 epic + 4 个指环 HP）。
8. **消耗品**：卷轴 1 条（单目标真实伤害）、药剂 1 条（队伍 ATK%）；其余 7 条需扩表结构与消费点。
9. **怪级**：`monsterlevel.csv` 1..10000 只有 hp/attack 倍率；H-B1 的 31 档 XP/KP/AR/DR 无对应表（XP 走 `spawn_rule.xp_base`，KP 走 `KpProgression`）。
10. **存档兼容**：槽位重绑/新天赋/新星图节点/怪物池替换均不破坏旧档；**药剂槽 4→8 会拒旧档**（`RestoreConsumables`/`ValidateConsumables` 要求槽数严格等于 4），装备品质改 id 需映射表（`EquipmentSave.Quality` 为字符串）。

### 21.5 需用户/设计Owner裁决（未实施）

装备四档身份；226 层节拍是否进入运行时；星图购买次数目标（35 vs 55–65）；G03 额外天赋点是否投放；S02 卷轴容量数值。以上均只列 A/B 案与影响（`compat_options.md`、`implementation_packages_proposal_v2.md`），未改任何配置。

### 21.6 局限

未运行游戏、未做战斗/购买回放，结论为静态配置+代码消费者核查；未逐字节核对 P0 压缩包内部 CSV；258 字段行中 129 行为"多表/仅P0"建议、52 行为 H 数值无生产列、28 行同义不同名待语义确认；星图/装备/怪级数值未评估（属 C3）；未覆盖跨平台与人工验收。

### 21.7 可回传设计Owner摘要

S15-C0 只读对账已完成：675/154/258 全量覆盖，21 份设计源哈希与记录数全部匹配，P0 键 393/393 命中且层数一致、拓扑未改。工程侧真实结论是"键对得上，但运行时能力不足"：①226 层战斗节拍在生成时未被消费（仅显示/报告）；②四职业 16 槽为 4 复用/12 需改/4 空，空槽都能用现有 effect 种类落地；③98 天赋中 29 个需要触发器/限额/目标评分等新系统，另有 25 个 T 分支待设计定稿；④星图现网可买仅 9 节点/35 阶，31 设计中 8 个有真实消费者、11 个需扩星图属性白名单、8 个无消费者、2 个待裁决、1 个缺参数，**55–65 次购买无法用现有节点凑出，且禁止用退休节点充数**；⑤装备品质命名与设计四档不一一对应，需产品裁决；⑥药剂槽 4→8 是唯一会拒旧档的改动。分包建议见 `implementation_packages_proposal_v2.md`（C1-A/C1-B/C2-A/C2-B/C3，含依赖与测试预算）。本轮未修改任何配置、代码或玩家存档。

- E：**PASS（只读对账）**；675/154 全覆盖、输入哈希前后一致、无生产写入。非实机行为验收。
- V-Design：未复核本报告；未向外部设计AI发送资料，不声称已同步。
- V-User：待裁决 21.5 的五项产品取舍后才可分别请求 C1/C2/C3 授权。

---

## 26. S15-C1-B / B3 实施：真实购买事务、57/55 路线、逐节点交互矩阵（2026-10-26）

### 26.1 授权与前置

用户指令："继续推进 B3"。前置：B1（17 节点/46 阶）与 B2（O05/D04/G02，7 阶）均已完成工程自检；价格仍为 `[H]` 受控测试值，未做正式经济。

### 26.2 两条设计路线用真实购买事务跑通

入口：`dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1b3-checks`，逐行读取设计包 `C1B_两阵容替代购买合法路线_条件性.csv`（112 行）并在真实会话中执行 `BuyStar`。

| 场景 | 队伍 | 设计笔数 | 实际笔数 | 实际花费 | 结果 |
|---|---|---:|---:|---:|---|
| 甲_四职均衡 | 战/游/法/圣 | 57 | **57**（+1 入口根） | 20,283 | 全通 |
| 乙_双游侠双圣骑_续航 | 游/游/圣/圣 | 55 | **55**（+1 入口根） | 19,793 | 全通 |

每笔断言：`GoldBefore - Cost == GoldAfter`、等级只 +1、**实际等级 == 设计 RankAfter**、`StarDerived(attr)` 增加（`class_trait_unlock` 以解锁状态判定）。账本 `star_purchase_ledger.csv`（114 行 = 2 入口根 + 112 笔），零缺口、未用禁售节点补数。

**入口根**：生产星图要求唯一根 `s_root`，设计层多个 `ROOT` 节点（O01/D01/G01/A01/F01/R01/R02）在布局里挂在其下，因此路线开始前先买 `s_root`，**不计入 57/55 笔**，单独记 `seq=0` 一行。这是 C1-B 已记录的结构差异，不是凑数。

### 26.3 逐节点交互矩阵（29 个可售节点）

`node_coverage.csv`：每个非退休、有效果的节点都跑完以下断言且全部通过。

| 检查 | 结果 |
|---|---|
| 合法首阶购买（等级=1） | 29/29 |
| 升到 Rank 上限（逐级合法） | 29/29 |
| 满级后再买：拒绝且不扣款 | 29/29 |
| 差 1 金币：`gold = 前置总价 + 本阶价 - 1` 时拒绝且余额不变 | 29/29 |
| 重复命令（满级重复）不产生第二笔扣费 | 29/29 |
| 存档读两次：等级/金币/派生值完全一致 | 29/29 |
| 跨 Run 清零（新会话该节点 0 级） | 29/29 |

附加：
- **OR 前置每条合法路径 + 反例**：`C-PAL(s_hp / s_atk)`、`D04(D02 / D03)`、`S01(s_atk / s_xp_01)` 逐条单独点亮即可购买，且未污染另一条；全未点亮时购买被拒。
- **禁售注入**：`S02/S03/P03/C-NEC/A02/A03/G03` 均不存在或不可购买（配置未投放，`CanBuy` 无入口）。
- **同职不重复收费**：双圣骑士队购买 `C-PAL` 一次后两个实例都能学 T，金币只扣一次。
- **账号级资格**：无圣骑士角色的队伍购买 `C-PAL` 仍成功（资格是账号/星图级，不绑定在场职业）。

### 26.4 三方同种子对照（17 个属性）

`star_effect_proof.csv`：17 个星图属性逐一给出"未购买 / 购买后 / 读档后"三值，要求 `购买后 > 未购买` 且 `读档 == 购买后`（星图无洗点入口，因此第三态用读档代替；这是与设计"洗点后"口径的差异，已记录）。17/17 通过。

### 26.5 UI 证据（真实窗口 + 真实控件路径）

`--first-batch-smoke`（1440×900/Windows）新增并通过：
- O02→O05、D02→D04、G02 全部通过**真实星图界面**购买成功，且 `StarDerived` 三项 >0；
- **满级重复点击**被拒且金币不变（O05 买满 2 阶后再次点击）；
- **前置未满足**点击被拒（未买 P01 时点 P02，等级仍为 0）；
- 职业资格购买后英雄页 T 状态变化与同职共享（沿用 §25 已有的 `star-class-trait-purchased.png` / `trait-shared-hero-*`）。

截图：`artifacts/qa/first-batch-ui-20261026-112631-…/star-b2-nodes-bought.png`、`star-b3-rejections.png`、`star-class-trait-purchased.png`。

### 26.6 差异台账（累计 C3，不阻断）

1. 入口根 `s_root` 为生产唯一根，设计多 `ROOT` 挂在它下面，路线多 1 笔 setup（不计入 57/55）。
2. `O01/D01/G01/C-BAR` 映射到既有 `s_atk/s_hp/s_xp_01/s_trait_warrior`，**沿用生产价**而非设计 `[H]` 价；因此两阵容实际花费（20,283 / 19,793）与设计表累计价（20,750 等）有差异，账本记实际值。
3. 星图无洗点入口，三方对照第三态用"读档"代替"洗点"。
4. `O01/D01` 生产容量（各 5 阶）大于设计（3 阶），路线只买 3 阶、不上限，不影响计数。
5. 前置只支持"点亮"，设计的等级门槛（`@1/@2`）未实现（沿用 §24 记录）。

### 26.7 容量与 C1-B 结论

- 可售：四职业 **63** / 双职业 **61** / 单职业 **60**；纯在线（扣 F03 三阶）60/58/57，均在 55–65 目标内。
- **C1-B 三个分包（B1/B2/B3）工程自检全部通过**；禁售集合维持 `S02/S03/P03/C-NEC/A02/A03/G03`，价格仍 `[H]`，未触碰 C2/C3、Lmin、第五席。
- 未完成/不做：`O05/D04/G02` 的"设计验收"仍需 V-Design 复核；精英身份在正式检查点不保留（§25.6）；226 层节拍、装备四档、正式经济属 C2/C3。

### 26.8 三方状态

- **E：B3 PASS（工程自检）**；57/55 真实购买、29 节点矩阵、OR 路径、17 属性三方对照、真机拒绝路径均有账本/截图证据。
- **V-Design：未复核**；待确认 26.6 的五项差异（尤其入口根与价格口径）。
- **V-User：未验收**；可实机在星图页购买任意可售节点、验证满级/前置/金币不足的拒绝提示。

---

## 25. S15-C1-B / B2 实施：精英伤害、受治疗增强、精英击杀收据（2026-10-26）

### 25.1 授权

用户指令："继续推进 B2"（承接 C1-B 整包授权第 3 条：O05/D04/G02 作为独立子切片实施，未验收前禁售，不允许用通用 ATK 等效果替代）。未触碰 C2/C3、Lmin、第五席。

### 25.2 三个子切片与真实消费者

| 设计节点 | 生产节点 | 真实机制 | 消费者 |
|---|---|---|---|
| O05 精英猎手 | `O05`（2阶，`elite_damage_pct` 3%/阶） | 新增 `DamageModifier.EliteOnly` + `Actor.Elite` 权威标记；`DamageInput.Elite` 只在目标为精英时放行 | `DamagePipeline.cs:84` 过滤；`SkillRuntime.EffectiveAttributes` 注入 |
| D04 恢复强化 | `D04`（2阶，`healing_received_pct` 5%/阶） | 新增战斗属性 `healing_received_pct`（`fightattr.csv` + `fightattr_rules.csv`），在 `HealTarget` 乘算"实际进入目标身上的治疗量" | `SkillSupportRuntime.cs:40`；`EffectiveValue` 的 star 分支 |
| G02 悟性增进 | `G02`（3阶，`elite_xp_pct` 10%/阶） | 击杀时按精英实例令牌一次性结算额外 XP；令牌随 Run 保存，重复结算/读档不二次领奖，新 Run 可再领 | `HeroExperience.GainEliteKillXp` + `SessionSnapshot.EliteKillReceipts` |

精英身份权威：`TryApplyElite`（`MonsterContent.cs:105`）在合法难度掷骰命中后写 `Actor.Elite=true` 与确定性令牌 `E{floor}-{room}-{combatId}-{affix}`，不是 UI 标签；`Actor` 字段随房间怪物一道序列化，旧档缺字段等价 `false`/空令牌。

### 25.3 代码/配置变更

- `GameSession.cs`：`Actor.Elite`、`Actor.EliteToken`。
- `MonsterContent.cs`：精英掷骰成功后写身份与令牌。
- `DamagePipeline.cs`：`DamageModifier.EliteOnly`、`DamageInput.Elite`、过滤条件（与 `BossOnly` 并列）。
- `SkillRuntime.cs`：`EffectiveAttributes` 注入精英伤害乘区；`ApplyStarAttribute` 增加 `healing_received_pct`；`SettleDamage` 传 `target.Elite`；击杀点调用 `GainEliteKillXp`。
- `SkillSupportRuntime.cs`：`HealTarget` 按目标受治疗增强放大，仍受 `CombatMaximumHp` 上限约束。
- `HeroExperience.cs`：`eliteKillReceipts` 集合、`GainEliteKillXp`（最低 +1，避免小额被 floor 抹平）、`EliteReceiptClaimed` 只读查询。
- `SessionSnapshot.cs`：`EliteKillReceipts` 字段、Capture/Restore 同步、校验（≤4096、无重复、非空、≤128字符）。
- 配置：`fightattr.csv`/`fightattr_rules.csv` 增加 `healing_received_pct`；`star.csv`/`star_layout.csv`/`star_plan.csv` 增加 O05/D04/G02（价格沿用 [H]，`PriceApproved=false` 口径）；`StarConfiguration.KnownAttributes` 增加三项。
- 工具：`tools/Apply-S15C1B-B2.ps1`（幂等）。

### 25.4 验证

| 入口 | 结果 |
|---|---|
| 两工程构建 | 0 警告 0 错误 |
| `--c1b-checks` | `C1B_STAR_PASS`：B1 17 节点 + **B2 三项** |
| 全量 Core / `--c1a` / `--c1a2` | 均通过（无回归） |
| Godot 无头 | 退出 0，`SMOKE_OK` |
| 真实窗口 UI | `FIRST_BATCH_UI_PASS`；新增断言：O02/O03/O04/O05、D02/D03/D04、G02 通过真实星图界面购买成功，且 `elite_damage_pct/healing_received_pct/elite_xp_pct` 派生值 >0 |

B2 专项断言（`elite_heal_receipt_proof.csv`）：
1. **O05 不对非精英生效**：同种子下非精英目标购买前后伤害差 <1e-6（证明不是通用 ATK），精英目标伤害显著提高；
2. **D04 只放大真实治疗**：同一祈祷、同一缺口，购买后实际回血更高，且不超过上限；
3. **G02 一次性收据**：首杀发放额外 XP、同令牌第二次击杀只给基础 XP、读档后 `EliteReceiptClaimed=true`、新 Run 同令牌可再领。

### 25.5 容量重算（B2 后）

| 口径 | 四职业 | 双职业 | 单职业 |
|---|---:|---:|---:|
| 设计可用（扣延期 8 + QA 2） | 68 | 68 | 68 |
| **本轮可售（扣 A02/A03 5 阶）** | **63** | **61** | **60** |
| 其中纯在线（再扣 F03 三阶） | 60 | 58 | 57 |

B0 阶段的 56/54/53 是"排除 7 阶缺失消费者"的条件值；B2 落地后 O05/D04/G02 共 7 阶转为真实可售，**四职业 63 已进入 55–65 区间**。B3 仍需按真实购买事务跑通设计层 57/55 两条路线，若不能达到须报实际值与原因，不得凑数。

### 25.6 差异与未完成

1. 精英身份在"正式检查点"（`CaptureProgress`）里以 `MonsterOrigin` 重建房间，未持久化 `Elite`：清房后重进为新掷骰实例，可能不再精英；令牌按 `floor-room-combatId` 确定性生成，因此同一槽位不会重复领奖。若要"回档后仍是精英"，需把 Elite/Token 写入 `MonsterOrigin`（属 C2 敌人语义，本轮未做）。
2. G02 额外经验最少 +1：避免小额击杀被 `floor` 抹平，属可观测性最小保证。
3. O05 只对 `Actor.Elite` 生效；Boss 不自动视为精英（设计未要求）。
4. D04 只影响治疗结算，不影响护盾、吸血或再生；`healing_received_pct` 目前仅星图产出。

**未完成**：B3（31 节点交互矩阵、57/55 真实购买事务、`node_coverage.csv`、全量 UI 截图、账本对账）。O05/D04/G02 现为可售（B2 已自检通过），但**不等于**设计验收通过。

### 25.7 三方状态

- **E：B2 PASS（工程自检）**；三项均有同种子 A/B、无效上下文对照、收据幂等与真机购买证据；B3 未实施。
- **V-Design：未复核**。需确认 25.6 的四项差异（尤其"检查点不保留精英身份"）。
- **V-User：未验收**。可在星图购买 O05/D04/G02 体验：精英怪伤害提升、祈祷回血更高、精英击杀额外经验（同实例只发一次）。

---

## 24. S15-C1-B / B1 实施：星图 17 节点真实消费与四职业资格收口（2026-10-26）

### 24.1 授权与范围

用户明确批准（原文摘要）：按原 C1-B 执行包继续，不再整包申请；接受 B0 的 31 节点/78 阶唯一分类；B1 先做 WORKS+NEEDS_EXTENSION 共 56 阶；四职业 T 资格优先收口；O05/D04/G02 归 B2 子切片、未验收前禁售且不得用通用 ATK 替代；A02/A03 BLOCKED、G03 QA_ONLY、S02/S03/P03/C-NEC 禁售；价格继续 [H]；56/54/53 只是阶段条件容量，不降低 55–65 目标；B2 后再重算并按 57/55 路线验证。本轮未触碰 C2/C3、Lmin、第五席。

### 24.2 ID 映射决定（旧档零损失）

- **保留**现有 9 个可买节点与 10 个退休节点，不做重命名、不做等级压缩，因此旧档星图等级与金币价值不变。
- **设计→生产映射**：`O01→s_atk`（攻击族）、`D01→s_hp`（生命族）、`G01→s_xp_01`、`C-BAR→s_trait_warrior`；其余 27 个设计节点使用设计 ID 新建（B1 先建 17 个可售节点）。
- **唯一根约束**：设计中 `ROOT` 前置的节点（R01/R02/A01/F01）在布局里挂到既有根 `s_root`；语义仍是"起点直达"，引擎不变。
- **等级门槛**：设计 `O01@1/@2` 这类"前置到某等级"在引擎只有"点亮即可"。本轮按点亮处理并在 24.6 记为差异，不为门槛新增运行时。

### 24.3 代码变更

| 文件 | 变更 |
|---|---|
| `StarConfiguration.cs` | 属性白名单从 8 项扩到 22 项（`KnownAttributes`/`IsKnownAttribute`），每项都必须有真实读取点，否则加载即拒绝 |
| `Stars.cs` | `StarDerived()` 只读查询；`ApplyStarTotals` 同时写 `AttackRatingBonus/DefenseRatingBonus`；新增 7 个经济/消耗品乘率 getter（击杀金币、出售、卷轴、药剂效果、药剂时效、行进、离线） |
| `SkillRuntime.cs` | `ApplyStarAttribute`：攻速/暴击/格挡/四抗在 `EffectiveValue` 统一出口叠加；`EffectiveAttributes` 注入元素伤害乘区与四抗；`MoveMultiplier` 仅在**非战斗**时应用行进效率 |
| `GameSession.cs` | 击杀金币入账点按 `kill_gold_pct` 放大，账本记入账前后差额 |
| `Equipment.cs` | 玩家确认出售按 `sell_gold_pct` 放大 |
| `Consumables.cs` | 卷轴伤害乘 `scroll_damage_pct`；药剂强度乘 `potion_effect_pct`；使用瞬间按 `potion_duration_pct` 延长剩余 Tick |
| `Rebirth.cs` | 离线结算按 `offline_pct` 放大 XP/KP/金币（仅合法离线渠道） |
| `Tools` | `tools/Apply-S15C1B-B1.ps1`（幂等追加 17 节点/布局/意向）、`tools/S15-C1B-B0-Prep.ps1`（B0 投影） |

### 24.4 17 个节点（46 阶）与消费点

| 节点 | 属性 | 读取点 | 端点验证 |
|---|---|---|---|
| O02 连斩节奏 | attack_speed_pct | `EffectiveValue`→`Actor.Timer` 推进 | 同种子10秒伤害提升 |
| O03 暴击潜力 | crit_rate | `EffectiveAttributes`→伤害掷骰 | 派生值+掷骰路径 |
| O04 元素驾驭 | element_damage_pct | 四元素 `DamageBucket.General` | 火球伤害提升 |
| D02 格挡练习 | block_rate | `EffectiveAttributes`→格挡判定 | 派生值+判定路径 |
| D03 异常抵抗 | element_resistance | 四抗 `EffectiveAttributes` | 派生值+抗性字段 |
| R01/R02 评级 | attack_rating/defense_rating | `Actor.*RatingBonus`→KpProgression/伤害公式 | 购买后 Bonus>0 |
| C-RAN/C-SOR/C-PAL | class_trait_unlock | `ClassTraitUnlocked`→`CanLearnFixedTalent` | 未购锁定/购买解锁/不重复收费 |
| S01 卷轴威力 | scroll_damage_pct | `UseScroll`→`SettleDamage` | 同一目标伤害提升 |
| P01 药剂时效 | potion_duration_pct | `UsePotion` 剩余 Tick | 剩余时间变长 |
| P02 药剂效力 | potion_effect_pct | `PotionAttackPct` | 百分比变高 |
| A01 行进熟练 | travel_pct | `MoveMultiplier`（非战斗） | 派生值+仅战斗外生效 |
| F01 财富积累 | kill_gold_pct | 击杀金币入账点 | 派生值+入账乘率 |
| F02 售卖收益 | sell_gold_pct | 回城出售 | 派生值+售价乘率 |
| F03 离线修行 | offline_pct | `SettleOffline` | 离线 XP/金币提升 |

### 24.5 验证

| 入口 | 结果 |
|---|---|
| 两工程构建 | 0 警告 0 错误 |
| `--c1b-checks` | `C1B_STAR_PASS`：17 节点逐个购买（金币守恒/等级+1/派生值+满级拒绝）、四职业资格门禁（未购锁定→购买解锁→同职共享不重复收费→读档保持）、10 退休节点拒绝、7 类端到端 A/B、账本与存档二次读取幂等 |
| `--c1a-checks` / `--c1a2-checks` | 通过（T 学习前改为先购资格，互斥/洗点/98 节点未回归） |
| 全量 Core | 退出 0 |
| Godot 无头 | 退出 0，`SMOKE_OK`（星图编辑器冒烟因 (4,2) 被 D02 占用已挪到 (8,2)） |
| 真实窗口 UI | `FIRST_BATCH_UI_PASS`；星图页真实渲染新节点（攻击评级研习/防御评级研习/财富积累/行进熟练）与旧节点并存 |

证据（`artifacts/qa/`）：
- `s15-c1b1-*/star_purchase_ledger.csv`：逐笔 `gold_before,cost,gold_after,level,derived_before,derived_after`；
- `s15-c1b1-*/star_effect_proof.csv`：同种子 A/B 对照（攻速/元素/评级/药剂×2/卷轴/金币/离线）；
- `first-batch-ui-20261026-102436-…/star-class-trait-purchased.png`：新星图全屏、资格节点 1/1、四条新分支可见。

### 24.6 差异与未完成

**差异（累计到 C3 统一 Review）**
1. 前置只支持"点亮"，设计的 `@1/@2` 等级门槛未实现（引擎限制，未新增运行时）。
2. `O01/D01` 沿用旧攻击/生命族节点，容量为 5 阶而非设计 3 阶；购买次数统计按设计 78 阶口径，不按实际节点阶数。
3. `A01` 只在非战斗移动生效（不影响战斗 Tick）；`F01` 在 loot 入账点放大（非逐个击杀结算）。
4. `D02/D03` 本轮验证到"派生值+有效属性注入"，未做同种子长时格挡/抗性 A/B（爆炸半径大，留 B3 矩阵）。
5. 价格沿用 `[H]` 受控测试值，`PriceApproved=false` 概念不落配置字段；未做经济定价。

**未完成（按用户顺序）**
- **B2**：O05 精英伤害（需精英身份权威字段+新乘区）、D04 受治疗增强（需 `healing_received_pct`+Heal 改造）、G02 精英击杀 XP 一次性收据。三者节点尚未建、不可售；验收前不得用 ATK 替代。
- **B3**：31 节点交互矩阵（差1金币/重复命令/OR 前置反例/无角色/禁售注入）、两阵容 57/55 真实购买事务、`node_coverage.csv`、UI 全量截图；须在 B2 完成后按真实可售节点重算容量再跑。

### 24.7 三方状态

- **E：B1 PASS（工程自检）**；17 节点真实消费与四职业门禁有账本/对照/真机证据；B2/B3 未实施，不计入通过。
- **V-Design：未复核**。需确认 24.6 的 5 项差异（尤其前置等级门槛与容量口径）。
- **V-User：未验收**。可在游戏内星图页购买攻击评级/防御评级/财富积累/行进熟练，并在职业页确认 T 需先买资格。

---

## 23. S15-C1-A-2 实施：98 节点天赋（2026-10-26）

### 23.1 授权与输入

用户指令：“继续执行 S15-C1-A-2：98天赋完整实施……先补齐技能级Modifier与已学天赋Trigger注册／注销能力，复用现有React、Proc限制和ICD机制；随后按野蛮人25、游侠25、法师24、圣骑士24的顺序实施”。授权沿用 C1-A 整包（`IMPLEMENT_APPROVED_SCOPE`），本轮不申请新授权；C1-B/C2/C3、正式数值、Lmin、第二轮内容均未触碰。

设计输入：`docs/temp/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/C1A核心输入/` 的 `02_C1A_98天赋逐节点补规表.csv`（98行，Region/MaxRank/前置/互斥/行为）、`S07_四职业天赋节点行为候选_V0.8.csv`、`S07_8套24点构筑合法清单_V0.8.csv`。注：用户提到的《C1-A-1独立Review与C1-A-2已授权续行包 V1.1》在工作区未以独立文件存在（`refs/` 仅有 V0.3/V0.4 zip），本轮以 C1-A 包内 02/03/04 号文件与 §22 记录为准。

### 23.2 引擎能力（本轮新增）

1. **技能级修饰器**：`kind=modifier|trait` + `effect` 白名单（coefficient/range/extent/duration/control/cooldown/targets/summon_hp/summon_attack），由 `SkillRuntime.ModifiersFor(actor,skillId)` 聚合，消费点为 `Commit`（系数/冷却）、`EffectiveSkillRange`（索敌与接敌）、`EffectiveExtent`（Damage/ApplyBuff/AreaPulse）、`ApplyBuff`（持续）、`ChainRuntime`（跳跃数）、`SummonRuntime`（继承比例）。负值只允许出现在修饰器（如“缩小范围换集中”）。
2. **已学天赋触发源**：`talent.csv` 新增 `trigger_id` 列；`trigger.csv.owner_buff` 允许为空表示“由天赋注册的常驻触发源”；`React()` 在Buff触发器之后遍历该角色已学节点，复用既有 `origin_filter` 过滤、`ProcAllowed`（每施放/每目标限额）与 `ProcIcdSeconds`；洗点/退休即注销，无独立可变注册表可泄漏。
3. **前置表达式**：`prerequisite` 支持 `A@1&B@2`（跨区域终点节点，如 `BAR-T06` 需 `T04@1&S204@1`）；裸ID保留“同区域上溯”的旧语义。`TalentRegion` 增加 `trait` 与“前置链可达职业特性根”判定，使T路线节点归入区域5。
4. **校验**：`kind` 白名单扩展；modifier/trait 必须绑定本职业已投放槽位技能；trigger 必须引用 `trigger.csv` 且带限额/ICD；`LoadSkillSlots` 复核槽位归属，避免“绑了不存在槽位”的假效果。

### 23.3 节点落地（98/98）

| 类别 | 数量 | 机制 |
|---|---:|---|
| 既有承载（C1-A-1） | 16 | S201/S301/S401 固定技能节点 + T01 职业特性节点 |
| 新增 modifier | 49 | 技能系数/范围/持续/冷却/目标数/召唤继承 |
| 新增 trigger | 23 | OnHit/OnKill/OnSkillCommit → 落Buff，逐条 `proc_limit`(1/1) 与ICD(3–6s) |
| 新增 trait | 10 | T区域修饰器（避免与S区域混淆） |
| **合计** | **98** | 82 新行 + 16 既有 |

`talent.csv` 现 155 行（active 47 / passive 26 / modifier 49 / trigger 23 / trait 10）。逐节点“机制—作用技能—限额/ICD”台账：`artifacts/qa/s15-c1a2-node-ledger.csv`（82行，由 `tools/Apply-S15C1A2.ps1` 生成，可重复执行且按ID幂等）。

### 23.4 差异台账（**不得**视为最终设计验收通过）

**A-1 结转**：陨星术=前摇+命中点范围爆发（无固定落点）；祝福之锤=绕体径向脉冲；旋风斩=原地周期脉冲（无施放中位移）；T职业路线每职业仅1条资格链（其余职业按过渡规则可学）。

**A-2 本轮新增近似**（均为“有真实消费者但语义收窄”，逐条列入台账，需设计Owner确认）：
1. “优先/评分/转火”类（如 BAR-S303、RAN-S303、SOR-S303、PAL-S203/S303/S304）落地为**射程或范围修饰**，未新增目标评分器（C0 已列为 NEW_EFFECT，本轮未越权实现）。
2. “下一次/限一次/独立收据”类（如 BAR-B03/S206、PAL-S405、T06）落地为**命中后短时增益Buff + 每施放1次限额**，是“命中后”而非严格的“下一次攻击”预置。
3. “精英特化”类（RAN-B03、SOR-S404、PAL-B03/S205、RAN-S304）落地为系数/集中范围，**未引入精英伤害乘区**（C0 列为缺口）。
4. “护阵/守护/牵制”类（RAN-S403、PAL-T03/T05、BAR-T05）落地为队伍护甲或召唤继承比例，未新增护卫AI。
5. `BAR-B02` 硬直与 `PAL-S204` 硬直通过 `stun` Buff + ICD 表达，Boss免疫未单独建模。
6. T01 资格：本轮未新增游侠/法师/圣骑士星图资格节点（属 C1-B），维持“未投放即可学”的过渡规则。

以上为**非阻断性累计项**，不改变“每个节点都有真实Effect消费者”的验收口径，但**不能**标记为最终设计验收通过。

### 23.5 验证（工程自检）

| 入口 | 命令 | 结果 |
|---|---|---|
| 两工程构建 | `dotnet build Idle-CRPG/Idle-CRPG.csproj` / tests | 0 警告 0 错误 |
| A-2 专项 | `dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1a2-checks` | 退出0，`C1A2_TALENT_PASS`：98节点、8套构筑×24点、前置/互斥负向断言、存档往返、免费局部洗点、四同职与四职业队列、修饰器与触发器真实行为 |
| A-1 专项 | `--c1a-checks` | `C1A_SKILL_PASS`（16/16槽位与行为未回归） |
| 全量 Core | 默认运行 | 退出0（`FIXED_SKILLS_PASS`/`LEGACY_SELECTION_PASS`/`WP4…`/`INT_01_08_PASS`） |
| Godot 无头 | `--headless --path Idle-CRPG -- --smoke-test` | 退出0，12段UI冒烟 + `SMOKE_OK` |
| 真实窗口 UI | `--path Idle-CRPG -- --first-batch-smoke` | 退出0，`FIRST_BATCH_UI_PASS`（新增断言：S2区域渲染技能修饰器卡 ≥7；T区域渲染 7 个特性节点） |

**真机证据**（`artifacts/qa/first-batch-ui-20261026-093239-9667460-014b9481c3884be2b51257e6050d17dd/`）：
- `talent-region5-traits.png`：T区域真实渲染“野蛮特性 + 战场本能/狂战连携/狂怒阵线/护队战线/旋风压境/钢铁救援”，并显示真实前置提示（“需学习 野蛮特性 至1级”“需学习 战场本能 至1级”）与资格门禁文案。
- `talent-region2-modifiers.png`：S2区域技能卡与修饰器卡共存（含 BAR-S202/S206）。
- A-2 行为证据：`artifacts/qa/s15-c1a2-*/modifier-damage.csv`（投入BAR-B01×3后同样10秒内实际扣血更高）、`trigger-behavior.csv`（BAR-B02 命中产生 `talent_stun_short`，洗点后不再产生）。

### 23.6 未决项与建议

1. 23.4 的六类近似是否接受为“首轮实现口径”，或需在 C1-A 内追加目标评分器/精英乘区/护卫AI。建议：先接受累计台账，待 C2-A（敌人精英语义）与 C3（数值）确定后再决定是否升级。
2. T01 资格节点：游侠/法师/圣骑士仍为“未投放即可学”；建议随 C1-B 补齐 4 个 `class_trait_unlock` 节点后关闭过渡规则（代码中规则已支持，仅缺配置）。
3. 节点数值（系数/范围/持续）为工程首轮值，非最终平衡值，须 C3 授权后重算。

### 23.7 三方状态

- **E：PASS（工程自检）**；98/98 节点有真实消费者，八套构筑与四同职/四职业队列通过，真机UI与实机行为均有证据；未做10小时体验与数值验收。
- **V-Design：未复核**。需确认 23.4 的六类近似与 T01 过渡规则。
- **V-User：未验收**。可实机查看四职业五个区域的天赋页（S1–S4 + T）；C1-B 仍按已排定计划另行启动。

---

## 22. S15-C1-A-1 实施：四职业16固定技能＋4职业特性T（2026-10-26）

### 22.1 授权与边界

用户指令（原文）：“**全部同意，批准C1-A实施**”；授权标记 `IMPLEMENT_APPROVED_SCOPE`，来源 `docs/temp/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/`（含 00 批准范围、01 五项裁决、02 转发指令、03 分阶段验收、04 权责、C1A核心输入/01–03）。授权覆盖 C1-A-1→C1-A-2 的阶段转换，不覆盖 C1-B/C2/C3、Git 发布、真实玩家存档、P0 拓扑与 Lmin 开放；D-G3 只允许隔离观察、S02/P03 继续暂缓。

### 22.2 设计ID → 生产ID 显式映射（16/16）

| 设计 | 槽 | 生产技能ID | 行为承载 | 判定 |
|---|---|---|---|---|
| BAR-S1 重击 | warrior S1 | `barb_bash` | 单体近战 Damage | 复用 |
| BAR-S2 旋风斩 | warrior S2 | `barb_whirlwind_window` | AreaPulse 周期（原S3移入） | 复用+重绑 |
| BAR-S3 战吼 | warrior S3 | `barb_taunt` | 范围嘲讽 `forced`（原S2移入） | 复用+重绑 |
| BAR-S4 先祖援军 | warrior S4 | `barb_ancestor`（新） | Summon 上限1/限时8s | 新增配置 |
| RAN-S1 弓箭射击 | ranger S1 | `ran_shot`（新） | 单发 SpawnProjectile | 新增配置 |
| RAN-S2 多重箭 | ranger S2 | `ran_multishot` | 3弹道扇面（原S1移入） | 复用+重绑 |
| RAN-S3 导引箭 | ranger S3 | `ran_guided_arrow` | AbilityCast arc_homing（原S2移入） | 复用+重绑 |
| RAN-S4 女武神 | ranger S4 | `ran_valkyrie` | Summon（原S3移入） | 复用+重绑 |
| SOR-S1 火球 | mage S1 | `mage_fireball` | 投射物+火焰伤害 | 复用 |
| SOR-S2 冰霜新星 | mage S2 | `sor_frost_nova`（新） | AreaPulse Cold + `chill` | 新增配置 |
| SOR-S3 连锁闪电 | mage S3 | `sor_chain_lightning` | Chain 4目标（原S2移入） | 复用+重绑 |
| SOR-S4 陨星术 | mage S4 | `sor_meteor`（新） | 2s前摇 + AreaPulse Fire | 新增配置（近似） |
| PAL-S1 圣击 | priest S1 | `paladin_smite` | 单体近战 | 复用 |
| PAL-S2 盾击 | priest S2 | `pal_shield_bash`（新） | Damage + `stun` | 新增配置 |
| PAL-S3 祈祷 | priest S3 | `pal_prayer`（改） | 主动 Heal 脉冲（`ally_lowest_hp`） | 语义修正 |
| PAL-S4 祝福之锤 | priest S4 | `pal_hammer`（新） | 绕体 AreaPulse Physical | 新增配置（近似） |

职业特性T：`warrior_trait`（攻击被动，保留原ID）、`ranger_trait`/`mage_trait`（暴击率被动，原型值）、`priest_trait`（改为 **守护光环** `pal_guard_aura`：MaintainAura 周期治疗）、`pal_trait_zeal`（新，**进攻光环** `pal_zeal_aura`：攻速+20%），两者 `exclusive_group=pal_trait_branch` 互斥且同一时刻只维持一条；T 光环不进入 `SkillIds`、不占 S1–S4、不参与自动施法队列。

### 22.3 实际改动

**配置（`Idle-CRPG/config/skill/`）**
- `skill_slot.csv` 重写为四职业固定顺位；`hero_starting_skill.csv` 游侠改 `ran_shot`。
- `skill.csv`：新增 6 条主动 + 2 条T光环；`pal_prayer` 改为 `pal_prayer_heal`（`ally_lowest_hp`）。
- `effect.csv`：新增 9 条（单发弹道/范围冰爆/减速Buff/陨星/盾击/眩晕/飞锤/守护与狂热光环）。
- `buff.csv`：新增 `sor_frost_nova_buff`(chill 35%/60t)、`pal_shield_bash_stun_buff`(stun/30t)、`pal_guard_buff`(周期治疗)、`pal_zeal_buff`(攻速+20%)。
- `summon.csv`：新增 `barb_ancestor`（上限1、HP/ATK 0.6/0.5、限时8s）。
- `talent.csv`：新增 7 节点（6 个固定技能节点 + `pal_trait_zeal`），`priest_trait` 改为光环主动，圣骑士T拆分为守护/进攻。

**引擎（`Scripts/Core/`）**
- `Talents.cs`：`Tree` 允许 0–3（第4棵树承载 C1-A 新增节点）；职业特性T允许绑定**不同名**的光环技能（仅 `ClassTrait`，仍校验技能存在）；`ClassTraitUnlocked` 增加过渡规则——该职业已投放资格节点时必须购买，尚未投放时保持可学（避免“有T节点永远不可学”的死内容）。
- `FixedSkills.cs`：新增 `TalentAuraSkills`/`IsTalentAura`；`SyncFixedSkills` 维持T光环；`MigrateFixedSkills` 升到 **FixedSkillsVersion=2**，对 v1 旧档按新槽位重排队列并把不再属于 S1–S4 的旧主动转入退休隔离（免费返还点数，不销毁记录），迁移不清除T光环。
- `SessionSnapshot.cs`：写入 `FixedSkillsVersion=2`；激活光环校验允许T光环（不要求在 `SkillIds` 内）；T投资资格校验与过渡规则对齐。
- `SkillSupportRuntime.cs`/`SkillRuntime.cs`/`GameSession.cs`：光环维持与索敌遍历对T光环、非主动引用做防御性跳过。
- `SkillPreview.cs`：试场按节点绑定的真实技能ID装配（T节点 id≠光环技能 id）。
- `SkillSnapshot.cs`：光环校验异常补充 buff/owner/aura/三个子条件，便于定位。

**工具与测试**
- 新增 `tools/Apply-S15C1A1.ps1`（可重复执行的配置落地脚本，已存在行不重复追加）。
- 新增 `tests/C1ASkillChecks.cs` + `--c1a-checks`：16槽映射、逐技能400tick真实行为、未学不出手、同职实例隔离、局部洗点保底、T互斥/光环撤销、v2读档、v1槽位重排迁移。
- 旧断言按规则变化更新（均保留其它保护）：`TalentSmoke`（S2→旋风、T资格过渡）、`SkillPreviewChecks`（节点73/主动47、T绑定技能）、`FixedSkillClosureChecks`（退休25、多来源光环改用守护T、召唤区域4）、`SkillMigrationChecks`（初始技能 ran_shot、T绑定豁免、祈祷改脉冲）、`ClassFollowingChecks`（tree≤3）、`Wp4TalentMechanicsChecks`（四职业20槽、T节点≥1）、`Wp4StarChecks`（资格过渡）;旧技能基线夹具补 `skill_slot.csv`，使 2026-10-22 旧组合自洽。

### 22.4 自主技术方案与被覆盖旧内容

1. **槽位重绑优先于新代码**：S2/S3/S4 轮换用 `skill_slot.csv` 完成，存档按 `skill_id` 而非槽号，故重绑不迁移存档；旧档仅需 `FixedSkillsVersion` 1→2 重排。
2. **祈祷语义修正**：设计明确“主动治疗脉冲、非持续Aura本体”，故 `pal_prayer` 由光环改为 Heal（目标=最缺血友军）；持续恢复移交T守护光环，避免两套常驻光环。
3. **近似项（已在验收中标注）**：陨星术用“2秒前摇+命中点范围爆发”近似“固定落点”（无地面预警系统）；祝福之锤用“自身周围径向脉冲”近似“绕体飞锤”；旋风斩沿用周期脉冲，不实现“施放中位移”。三者均不新增未授权系统。
4. **T资格过渡规则**：未新增星图资格节点（C1-B范围），改为“未投放即可学、已投放须购买”。野蛮人仍被 `s_trait_warrior` 严格门禁。该取舍需设计Owner确认是否接受为 C1-A 期间临时口径。
5. **未复活旧机制**：`CastTalent`、旧装配、技能槽付费、退休节点仍不可用；旧光环 `pal_fanaticism/pal_conviction` 退出固定槽并进入退休隔离。

### 22.5 验证（工程自检）

| 入口 | 命令 | 结果 |
|---|---|---|
| 两工程构建 | `dotnet build Idle-CRPG/Idle-CRPG.csproj` + tests 工程 | 0 警告 0 错误 |
| C1-A专项 | `dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1a-checks` | 退出0，`C1A_SKILL_PASS`（16/16槽位与真实行为、未学不出手、T互斥/实例独立/洗点撤销、v2读档与v1槽位重排迁移） |
| 全量Core | `dotnet run --project tests/IdleCrpg.Checks.csproj` | 退出0，`FIXED_SKILLS_PASS`(25退休)/`LEGACY_SELECTION_PASS`/`WP4_HISTORICAL_TALENT_PASS`/`WP4_STAR_PASS`/`WP5/6/7`/`INT_01_08_PASS` |
| Godot无头 | `--headless --path Idle-CRPG -- --smoke-test` | 退出0，12段UI冒烟 + `SMOKE_OK` |
| 真实窗口UI | `--path Idle-CRPG -- --first-batch-smoke` | 退出0，`FIRST_BATCH_UI_PASS`（1440×900/Windows） |

**真机证据**（`artifacts/qa/first-batch-ui-20261026-080743-0327463-53f517bbb43842bf9bb7c533e6a17d20/`）：`fixed-skills-s3-unlearned-t-locked.png`（S1猛击/S2持续旋风/S3战吼/S4先祖援军 四卡 + T“星图锁定”）、`fixed-skills-s3-learned.png`（真实点击学会S3战吼、自动施放、Rank 1/10）、`fixed-skills-local-reset.png`（免费重置S3、S1保底）、`four-same-class-buff-sources-and-cooldowns.png`（四同职守护光环四来源）。

### 22.6 C1-A-2 状态：**OPEN（未实施）**

98 节点本轮**未落地**，不宣称完成。已确认的两项工程前置缺口（来自 §21 只读对账并在本轮再次核实）：

1. **技能级修饰器缺失**：设计 S2xx/S3xx/S4xx 的“提高某技能伤害/范围/时长/目标数”不能复用现有 `passive`（白名单仅 atk/armor/crit_rate/engagement_capacity，且 `passive` 禁止绑定 `skill_id`）。需要新增 `kind=modifier`（绑定目标技能、在 `Commit` 应用系数/范围/命中数）或等价方案。
2. **触发器可由T光环承载，但缺少“按已学天赋注册触发器”的通用入口**：`React()`/`proc_limit`/`proc_icd` 运行时已完整存在（本轮核实，见 `SkillRuntime.cs:243-283`），`trigger.csv`/`proc_*` 仅缺配置；29 个 NEW_EFFECT 需要“天赋→常驻触发源”的注册路径与目标评分（精英/威胁）扩展。

**逐节点状态与所需Effect**：沿用 `artifacts/qa/s15-c0-20261026-131223/effect_coverage_engine_complete.csv`（154行，含每行所需能力、消费者入口、缺口、代表测试、状态）；本轮未改写其判定，也未把总分类强行指派给具体节点（遵守03号门禁第2条）。

**建议的下一步（需用户确认是否继续同一授权内推进）**：先做 `modifier` + 触发源注册两项运行时扩展（各配 1 组单测），再按 BAR→RAN→SOR→PAL 分批落 98 节点，每批跑"前置/互斥/点数守恒/读档/局部洗点"四类断言，最后跑 S07 八套24点构筑与四同职/四职业各一/跨Run隔离。

### 22.7 三方状态

- **E（工程自检）：C1-A-1 = PASS；C1-A-2 = OPEN**。16/16 技能与 4/4 T 均有固定Seed真实行为与真机UI证据；98 节点未实施，不计入通过。
- **V-Design：未复核**。需重点裁决：①T资格过渡规则（未投放即学）是否接受；②陨星/祝福之锤/旋风三项近似是否满足身份要求；③C1-A-2 是否继续按上述两步扩展推进。
- **V-User：未验收**。C1-A-1 可实机体验（四职业技能卡与T），不等于首轮10小时体验通过。
