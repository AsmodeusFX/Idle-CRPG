# 角色自动技能 AI

2026-10-23 按 P1-4 V1.0 交接包实施。这里只记录“AI 何时提出 CastSkill”，效果、伤害、冷却与动作时序仍归既有 Runtime。

## 单一真值

| 职责 | 唯一负责方 | AI 的权限 |
|---|---|---|
| 已学/已装备技能槽 | 玩家（星图、天赋、编队命令） | 只读 `Actor.SkillIds`，不自动加点、不改槽 |
| 动作许可、CD、打断 | `SkillRuntime.Commit` / `AdvanceSkills` | 只提出请求，不写动作状态、不扣冷却 |
| 技能临时目标 | `ChooseSkillTarget`（本文件） | 只做合法性筛选，不改怪物主锁定 |
| 主锁定与接敌容量 | `CombatTargeting` | 只读 |
| 伤害、治疗、Buff、光环、诅咒、召唤、尸体 | `Effect`/`Buff`/`SummonRuntime`/`CorpseRuntime` | 只提出意图，不预产生效果 |
| 入场通行与敌人警觉 | `CombatEntryRemaining` / `AdvanceRoomEntry` | 只读取是否可行动 |

## 决策顺序（每个合法行动窗口）

1. 角色死亡、不在本房、处于入场通行未完成阶段、被眩晕时：不产生新请求（由 `Act` 统一拦截）。
2. 已有活动动作或 Recovery：等待结算，不抢断、不新建请求。
3. 按**玩家已装备槽序**扫描主动技能（槽序即优先级，不新增隐藏权重）。
4. 逐项检查：光环已维持 → 冷却中 → **召唤名额已满** → 无合法目标；任一不通过就记录原因并继续下一个技能。
5. 命中即发出一个 `CastSkill` 请求并进入既有 Windup；`Commit` 再复核一次（目标失效即取消、不扣冷却）。
6. 全部技能不可用：普通攻击兜底；普攻也不合法则按既有射程与主锁定移动/等待。
7. 结论变化才写入有界诊断（最多 24 条），不作为战斗状态。

**射程外处理**：技能只在自身射程内找目标；高优先级技能射程外不会让 AI 放弃可立即使用的低优先级技能或普攻，只有在全部动作都不可行时才按既有移动逻辑追近。不瞬移、不为单个技能长途跑位。

## 资格条件（复用既有配置字段）

`skill.csv` 的 `cast_condition` 与 `target_policy` 是唯一配置入口，没有新增并行 AI 配置表。

| 条件/目标 | 语义 | 代表技能 |
|---|---|---|
| `target_policy=enemy` | 射程内合法敌人，按最近/最高生命等既有 Ability 策略 | 猛击、火球术、至圣斩、连锁闪电、多重箭、导引箭 |
| `cast_condition=nearby_melee` | 射程内存在非远程敌人，否则不空放 | 贴身防御类技能（原型预留，正式表暂未投放） |
| `target_policy=ally_lowest_hp` | 真实失血的合法友军，按当前生命比例升序 | 治疗类（原型预留） |
| `cast_condition=missing_buff` | 目标缺少该技能的同类诅咒时才进入候选 | 伤害加深、降低抵抗 |
| `target_policy=corpse` | 本房未被消费的尸体且在施放距离内 | 尸体爆炸 |
| `target_policy=self` | 自身；光环靠已维持标记跳过，召唤靠名额资格跳过 | 三种光环、召唤骷髅、女武神、嘲讽 |

**召唤名额资格（本轮新增）**：技能全部效果指向同一定义且为召唤时，存活数量达到 `summon.csv` 的 `max_count` 就不再发起施法。原因：`Commit` 会先消费冷却与恢复，效果层再静默丢弃，等于白耗一次动作。

**光环维持**：`ActiveAuraSkills` 已含该技能时不重复施放；跨房保留，卸槽、死亡或来源失效由既有 `PruneAuraActivations`/`SynchronizeAuras` 撤销。多种光环各自占槽、互不互斥。

**多诅咒**：不同 BuffId 可同时存在于同一目标；同种未失效时不重复施放（`missing_buff`）。刷新窗口未批准，因此不做“剩余 N 秒提前刷新”。

## 尚未批准的项（保持现状，不擅自定默认）

| 项 | 当前实际行为 | 待裁决 |
|---|---|---|
| AOE 收益门槛 | 只要有一个合法目标就进入候选 | 是否要求覆盖 N 个目标或高价值单体 |
| 嘲讽/旋风等自施法条件 | 冷却结束即施放 | 是否需要“队友受威胁”“有效敌群”前置条件 |
| 治疗阈值与目标并列 | 仅按当前生命比例升序，无最低阈值 | 是否引入阈值/治疗量优先 |
| 尸体技能收益 | 有未消费尸体即可施放 | 是否要求爆炸范围内有受益目标 |
| 诅咒刷新窗口 | 仅缺失时施放 | 是否允许结束前刷新 |
| 技能饥饿 | 无隐藏公平调度 | 是否需要最小间隔或配权 |
| 召唤补员优先级 | 输出技能在前、召唤按槽序竞争 | 是否让补员插队 |

这些都属于产品决策，需先批准再改，且改动只落在 `CastCondition`/`TargetPolicy` 的既有扩展点。

## 诊断

GM「接敌容量与入场 · 实时诊断」窗口新增“技能AI决策”段：按变化追加最近 24 条，内容为选择结果与逐项跳过原因（冷却中/召唤已满/无合法目标/光环已维持/Runtime 取消）。属于本房短期记录，换房或清房清空，不进存档、不写磁盘。

相关实现：`Scripts/Core/SkillRuntime.cs`（`RunActiveSkill`、`Commit`、`NoteSkillDecision`）、`Scripts/Core/SkillSupportRuntime.cs`（`ChooseSkillTarget`）、`Scripts/Core/SummonRuntime.cs`（`SoleSummonOf`、`SummonCountOf`、`SummonEffect`）、`Scripts/Core/CorpseRuntime.cs`（`ChooseCorpseTarget`）。
