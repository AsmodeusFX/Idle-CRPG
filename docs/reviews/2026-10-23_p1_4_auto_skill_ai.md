# P1-4 角色自动技能 AI 执行与审计记录

2026-10-23。来源：用户外部引入的 P1-4 V1.0 执行方案（`docs/temp/P1_4_AutoSkill_AI_V1_0/`）。**本文件是本次开发的唯一工程归档**；`docs/temp/` 是外部方案输入区，源文件不复制进工程、由用户自行处置。现行规则见[自动技能 AI](../design/auto_skill_ai.md)。

## 一、G0 只读审计（真实代码事实）

工程里**没有**名为 `PlayerPolicy` / `ActionRequest` 的类。交接包要求的语义由现有 `GameSession.Act` → `RunActiveSkill` → `Commit` 承担，审计按真实路径映射：

| 审计对象 | 真实位置 | 结论 |
|---|---|---|
| 战斗主循环 / 行动决策 | `GameSession.TickDungeon` → `Fight` → `Act` | 固定 20Hz，先 `AdvanceSkills` 再逐单位 `Act`，同 Tick 内顺序稳定 |
| “PlayerPolicy” | `SkillRuntime.RunActiveSkill` | **部分支持**：槽序扫描 + CD 跳过 + 目标筛选；缺召唤名额资格 |
| ActionRequest | `SkillActionState`（`Active/Target/Windup/Recovery/Cooldowns`） | 支持：AI 只写 `Active`，不写 CD |
| Windup→Commit→Recovery | `AdvanceSkills` / `Commit` | 支持：CD 只在 `Commit` 写一次；目标失效取消且不扣 CD |
| Skill/Effect/Buff | `SkillConfiguration` + `DrainEffects` | 支持，AI 不直接产生效果 |
| 技能目标 | `ChooseSkillTarget` / `ChooseCorpseTarget` / `Choose` | 支持：enemy / self / corpse / ally_lowest_hp / missing_buff / nearby_melee |
| 主锁定与接敌容量 | `CombatTargeting` | 支持，AI 只读 |
| 光环 | `PruneAuraActivations` / `SynchronizeAuras` | 支持多光环共存、卸槽与来源撤销 |
| 诅咒 | `missing_buff` + Buff 分组 | 支持不同诅咒共存、同种不重复 |
| 召唤 | `SummonRuntime`（`SummonEffect` 名额判定、跨房 HP 保留、脱战补员） | 支持；AI 侧原先缺“已满不施法” |
| 尸体 | `RecordCorpse` / `ChooseCorpseTarget` / `CorpseExplosionEffect` | 支持一次性原子消费 |
| 受击退让 | `RunRetreat` + `RetreatThreat` | 已实现，未重复开发 |
| 入场通行与敌 AI 延迟 | `AdvanceRoomEntry` / `CombatEntryRemaining` | 已实现，AI 未覆盖 |

## 二、C01–C12 逐项处理

| ID | 现状与处理 | 状态 |
|---|---|---|
| C01 射程外技能 | 现状=先找可用技能→普攻→按主锁定移动，与推荐一致；本轮未改 | CONFIRMED（代码已符合） |
| C02 Commit 前目标失效 | 现状=取消、无 CD、无投射物；治疗/AOE 中心变化未定义 | 继承既有合同；争议分支仍 TBD |
| C03 AOE 收益门槛 | 现状=有一个合法目标即施放；未加阈值 | **TBD，未改**（需产品批准阈值） |
| C04 并列优先级 | 现状=已装备槽序，稳定；未新增 Priority 字段 | CONFIRMED（沿用现有槽序） |
| C05 饥饿 | 无隐藏公平调度 | CONFIRMED（不做） |
| C06 光环生命周期 | 现状=激活后跳过，卸槽/死亡/来源失效撤销；多光环各自占槽 | CONFIRMED（复用现状） |
| C07 召唤补员 | 名额已满现在不再发起施法（本轮修复）；补员与输出的先后仍按槽序 | 部分 CONFIRMED；补员插队仍 TBD |
| C08 诅咒刷新窗口 | 仅缺失时施放，无提前刷新 | CONFIRMED（不做提前刷新） |
| C09 治疗阈值/并列 | 只有真实失血才进入候选，按生命比例升序；无阈值 | 部分 CONFIRMED；阈值 TBD |
| C10 退让与施法冲突 | 退让在 `Act` 中先于技能检查，二者互斥 | CONFIRMED（复用现状） |
| C11 无法释放时的兜底 | 逐技能跳过→普攻→移动；不静默吞动作 | CONFIRMED |
| C12 换人/换技能与 Buff | 沿用现有回城换人与 Buff 清理流程 | CONFIRMED（未新增流程） |

## 三、本轮实现（最小改动）

| 文件 | 改动 |
|---|---|
| `Scripts/Core/SkillRuntime.cs` | `RunActiveSkill` 增加召唤名额资格与逐项跳过原因；新增 `NoteSkillDecision` 有界诊断（≤24 条、仅结论变化追加）；`Commit` 取消时记录 Runtime 结论；`ResetSkillRoom` 清空本房诊断 |
| `Scripts/Core/SummonRuntime.cs` | 新增 `SoleSummonOf`（不限永续/限时）、`SummonCountOf`；`PermanentSummonOf` 改为复用 `SoleSummonOf`，脱战语义不变 |
| `Scripts/Core/CombatTargeting.cs` | GM 诊断追加“技能AI决策”段 |
| `tests/AutoSkillAiChecks.cs` | 新增 AI 级隔离检查（AI01–AI30 可验证子集） |
| `tests/Program.cs` | 注册 `--auto-skill-checks` 并并入默认回归 |
| 文档 | 新增 `docs/design/auto_skill_ai.md`；本记录；`docs/README.md` 索引 |

**没有**新增 `AutoCastManager`/`SkillAIService` 等并行实体；没有第二套条件解释器；没有改怪物 AI、伤害公式、装备或星图。

## 四、验证

| 命令 | 结果 |
|---|---|
| `dotnet build Idle-CRPG/Idle-CRPG.csproj` | 0 警告 0 错误，退出码 0 |
| `dotnet run --project tests/IdleCrpg.Checks.csproj -- --auto-skill-checks` | PASS，退出码 0 |
| `dotnet run --project tests/IdleCrpg.Checks.csproj` | 全部 PASS，退出码 0 |
| `<Godot 4.7.2 .NET> --headless --path Idle-CRPG -- --smoke-test` | 各 UI 冒烟 + `SMOKE_OK`，退出码 0 |

新增检查覆盖：

- 槽序选择、冷却跳过且不改写既有冷却、冷却只在 Commit 启动一次。
- 全部技能不可用→普攻兜底；技能与普攻都射程外→按既有移动追近且不造成伤害。
- Recovery 期间不抢断、不普攻；目标在 Commit 前死亡→取消且不扣冷。
- `nearby_melee` 无威胁不空放、有威胁才进入候选。
- 光环已维持不重施；两种不同诅咒并存、同种已覆盖不重施。
- **召唤满员不施法且不消耗冷却**（本轮修复点）；缺员补召不消耗尸体。
- 无尸体时尸爆不入候选并回到普攻。
- 多英雄动作/冷却独立；同输入重放快照一致；GM 诊断包含技能AI段。

未覆盖/未验证：AI 级用例未逐条对应到 AI01–AI30 编号（部分能力由既有 `SkillBd*`、`CombatSkillChecks`、`TalentChecks` 回归覆盖）；未做 Godot 实机长时间观感验收；未验证 Linux/macOS。

## 五、归档与物料边界

- **工程内只留这份汇总记录**（加[自动技能 AI](../design/auto_skill_ai.md)规则页），不复制、不迁移外部方案原文；`docs/temp/` 属于用户的外部方案输入区，工程不写入、不整理。
- 外部包里的 `data/*_TEST_ONLY.*` 阈值只用于理解语义，未写入任何正式配置；正式表未新增列，`skill.csv` 仍是唯一技能配置入口。
- 本轮只改了 `Scripts/Core` 三个文件与新增一个 Core 检查文件，没有新增并行管理器、第二个条件解释器或第二套目标锁定。

## 六、给后续执行的边界提醒

- 外部方案里的“审批门禁/C01–C12”是方案自带的流程要求，不构成工程规则；工程侧以 `AGENTS.md` 与 `docs/` 现行文档为准。
- 再次执行同类外部方案时：只读审计 → 按切片最小改动 → 跑 Core 检查与 Godot 冒烟 → 在本目录补一份汇总 md，外部源文件留在 `docs/temp/` 由用户处理。
