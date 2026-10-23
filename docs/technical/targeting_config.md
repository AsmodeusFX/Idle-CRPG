# 索敌与入场配置合同

2026-10-22。行为见[索敌设计](../design/combat_targeting.md)。全部CSV为UTF-8、importer=keep，样例包未直接覆盖正式配置。

## 单位属性

- `combat/fightattr.csv`新增`engagement_capacity`：category=base、value_type=number、aggregation=base_flat_pct、allowed_ops=base;flat;pct、default_value=0、display_format=integer、ui_visible=true。
- `combat/fightattr_rules.csv`同ID：baseline=0、min_value=0、max_value=100000。该上界是数值安全边界，不是正式成长上限承诺。
- `combat/unit_attributes.csv`新增必填列`engagement_capacity`：非负整数，范围0～100000；每名英雄/怪物须有有效行。野蛮人3、圣骑士5、其余英雄及当前怪物0。缺字段、负数、小数、无效单位ID明确拒绝。
- `Config.BaseEngagementCapacity`只提供配置基础值；`GameSession.EngagementCapacity`复用属性聚合，将基础值、同属性被动及Buff贡献聚合后向下取整。被动天赋effect允许engagement_capacity；本期不新增容量天赋内容节点。没有第二份当前容量存档或AI属性账本。

## system/config.csv新增规则

| id | 原型value | 单位与用途 |
|---|---|---|
| targeting_local_range_milli | 4000 | 千分之一世界单位，正常优先接敌范围；不是全房追击硬上限 |
| targeting_reevaluate_milli | 250 | 毫秒，单个Overflow锁的最短重评间隔 |
| targeting_budget | 4 | 每逻辑步最多消费的溢出重评请求数，不限制首次激活的合法索敌数量 |
| combat_entry_delay_milli | 800 | 毫秒，有活怪房间基础警觉时间，无条件保留 |
| combat_entry_alert_cap_milli | 2000 | 毫秒，敌方启动兜底上限，必须大于基础警觉；防止入房异常导致长期待机 |
| combat_entry_pass_depth_milli | 1500 | 千分之一世界单位，入口通行深度；无近距威胁时单位须自然入房该深度才可停下攻击 |
| combat_entry_threat_range_milli | 1200 | 千分之一世界单位，入口浅区近距离威胁距离，必须小于通行深度 |
| combat_entry_melee_depth_milli | 2800 | 千分之一世界单位，ranged=false英雄的门内展开纵深 |
| combat_entry_ranged_depth_milli | 900 | 千分之一世界单位，ranged=true远程/法系的门内展开纵深 |
| combat_entry_melee_spread_milli | 1100 | 千分之一世界单位，近战横向通道间距 |
| combat_entry_ranged_spread_milli | 450 | 千分之一世界单位，远程/法系横向通道间距 |
| dungeon_queue_spacing_milli | 650 | 千分之一世界单位，穿门行进队列间隔，不作为单位物理碰撞半径 |
| party_catchup_distance_milli | 2000 | 千分之一世界单位，跑图队员落后超过该距离才临时加速，必须大于队列间距 |
| party_catchup_speed_pct | 150 | 百分比，跑图落后追赶倍率，须≥100；只作用于移动执行，不改写Actor.Speed |
| dungeon_door_wait_offset_milli | 600 | 千分之一世界单位，领队门外等待位与目标门的距离 |
| dungeon_door_wait_timeout_milli | 3000 | 毫秒，门前等待超时；超时后由领队直接开门，避免无限卡门 |
| summon_entry_speed_pct | 150 | 百分比，入场部署时召唤物基础Speed的临时倍率上限，须≥100 |

新增规则均为必填整数1～100000，禁止隐藏默认值；近战纵深、横向间距均须大于对应远程值。旧front/back/shared-spread三个键已被上述四键替代。角色分组使用combat/unit_attributes.csv既有ranged字段，不读取接敌容量；提高容量不会把法系推入前排。上述均为原型参数，可后续调参。角色位置仍使用已有二维世界坐标和固定步长，倍速不重复乘移动预算。

召唤物入场复用同一警觉窗口与纵深/间距：`skill/summon.csv`必填`role`（melee/ranged/support）决定近战前压、远程靠后或贴近主人，不按显示名或主人职业推断。召唤物车道从英雄总数之后继续，临时加速取`基础×summon_entry_speed_pct%`（原型150%）；窗口结束不再兜底闪现，未接敌的召唤物继续沿真实房门走进房间，到达或进入有效战斗即交回战斗AI，不延长敌人AI延迟。该加速只在进入有活怪房间时触发，走廊与已清空房间仍按普通跟随/追赶。详见[召唤物跟随与入场部署](summon_follow.md)。

## 2026-10-23 V1.2 入场通行与出生避让

不新增 CSV 表；通行相关数值全部来自上表三条新规则，行为见[索敌设计](../design/combat_targeting.md)。

- `CombatTargetingState` 新增 `EntryPassMask`（已完成通行）与 `EntryEngagedMask`（提前接战豁免），按友方连续索引（英雄在前、召唤物在后）独立记录；`DeployedMask` 语义保留为英雄已交回战斗 AI。
- 单位入房深度由 `EntryAnchor`（本次实际房门位置）与 `EntryDirection`（指向房间中心）投影得到，四种入口共用；不新增门线字段或存档版本。
- 两个新掩码属于本房短期现场：完整快照保存并校验位宽与友方数量，正式检查点必须为 0，读档重建入场而不是续接。
- 刷怪避让复用 `Reveal` 的既有随机过程：四边 0.8 留白后，再按实际进房方向排除入口内侧通行区；重试上限 32 次，失败时确定性回退到通行线外，怪物数量始终等于快照数量。
- GM 接敌诊断窗口新增入口门线、朝内方向、通行边界、单位坐标/深度、当前目标、停止原因与敌方警觉状态；只读、不入档。

## 运行态和存档

`SkillBattleState.Targeting`为唯一持锁权威：Locks保存Owner、Target、Normal/Overflow/Ranged、重评标记与NextEvaluation；同时保存候选掩码、容量变化快照、轮转游标、决策时钟、本房/本层索引和入场短期状态。没有仇恨分数。GameSession的字典只索引同一锁对象，不是第二份预约来源。

本房ID复用原CombatId：英雄索引，怪物为Heroes.Count加怪物索引；死亡不复用列表位置。它不是永久角色ID或召唤ID。验证拒绝重复Owner、越界Target、无效枚举、非有限计时、跨房间锁和超预算现场。

内部Capture保留完整状态；正式CaptureProgress清空SkillState，包括锁、容量快照、入场时钟和Scope。正式Restore以已有RoomEntry位置及当前房间最近入口推导入场方向，重开警觉，不增加正式存档版本或保存怪物现场。旧缺Targeting字段按空临时态兼容；旧CombatVersion=0仍从模板初始化攻击元数据，当前完整快照保留Ranged攻击方式，避免近远程切换被模板覆盖。

`MotionPresentation`只缓存本房模型偏移，房间或会话变化时清空；它不参与Core命中。QA入口`--capture --capture-targeting=intro|tank-first|rear-entry|crowd|ranged`仅构造隔离检查场景，不定义正式怪物配置。

## 受击退让V0.1配置

system/config.csv新增必填整数规则，范围1～100000：ranged_retreat_distance_milli=1200（千分之一世界单位）、ranged_retreat_cooldown_milli=4000（毫秒）。均为原型值。SkillActionState新增RetreatCooldown、RetreatThreat（默认-1）、RetreatMoving、RetreatGoal；内部快照拒绝非法时间、坐标及敌方引用，旧档字段缺省兼容，正式进度不保存。

E2增量：Actor.TargetPolicy=Ordinary（旧档默认）或BacklinePriority；后者仅隔离猎手显式启用，锁类型Special不占Normal容量，Forced优先，候选仍限本房存活单位。完整快照保存并校验枚举，正式检查点重开规则不变。见 ../reviews/2026-10-22_monster_encounter_e2.md。

E3：敌方动态召唤在入场前预留有限单次槽位，战斗中不改变怪物列表长度，避免友方召唤物CombatId漂移；来源死亡保留/撤销由EnemySummonRule配置。敌方仆从正常奖励与产尸；详见 ../reviews/2026-10-22_monster_encounter_e3.md。

## 2026-10-23 Steam D2增量

不新增CSV字段或数值。targeting_local_range_milli继续为4000，用于普通近战优先预约及我方本地后排威胁筛选，不是普攻射程。CombatTargetingState.FriendlyTargets保存战斗ID到敌方战斗ID的稳定目标，只属于内部完整现场；正式检查点清空并验证不得携带。读取校验归属、数量和目标范围。GM事件历史不入档。友方目标与怪物Locks分别服务攻击选择与唯一容量预约，不重复记账。