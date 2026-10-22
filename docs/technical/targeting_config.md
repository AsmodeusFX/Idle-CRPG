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
| combat_entry_delay_milli | 800 | 毫秒，有活怪房间警觉/小队展开窗口 |
| combat_entry_front_depth_milli | 2800 | 千分之一世界单位，有容量英雄的门内展开纵深 |
| combat_entry_back_depth_milli | 1800 | 千分之一世界单位，0容量英雄的门内展开纵深 |
| combat_entry_spread_milli | 850 | 千分之一世界单位，展开横向通道间距 |
| dungeon_queue_spacing_milli | 650 | 千分之一世界单位，穿门行进队列间隔，不作为单位物理碰撞半径 |

新增规则均为必填整数1～100000，禁止隐藏默认值。上述均为原型参数，可后续调参。角色位置仍使用已有二维世界坐标和固定步长，倍速不重复乘移动预算。

## 运行态和存档

`SkillBattleState.Targeting`为唯一持锁权威：Locks保存Owner、Target、Normal/Overflow/Ranged、重评标记与NextEvaluation；同时保存候选掩码、容量变化快照、轮转游标、决策时钟、本房/本层索引和入场短期状态。没有仇恨分数。GameSession的字典只索引同一锁对象，不是第二份预约来源。

本房ID复用原CombatId：英雄索引，怪物为Heroes.Count加怪物索引；死亡不复用列表位置。它不是永久角色ID或召唤ID。验证拒绝重复Owner、越界Target、无效枚举、非有限计时、跨房间锁和超预算现场。

内部Capture保留完整状态；正式CaptureProgress清空SkillState，包括锁、容量快照、入场时钟和Scope。正式Restore以已有RoomEntry位置及当前房间最近入口推导入场方向，重开警觉，不增加正式存档版本或保存怪物现场。旧缺Targeting字段按空临时态兼容；旧CombatVersion=0仍从模板初始化攻击元数据，当前完整快照保留Ranged攻击方式，避免近远程切换被模板覆盖。

`MotionPresentation`只缓存本房模型偏移，房间或会话变化时清空；它不参与Core命中。QA入口`--capture --capture-targeting=intro|tank-first|rear-entry|crowd|ranged`仅构造隔离检查场景，不定义正式怪物配置。

## 受击退让V0.1配置

system/config.csv新增必填整数规则，范围1～100000：ranged_retreat_distance_milli=1200（千分之一世界单位）、ranged_retreat_cooldown_milli=4000（毫秒）。均为原型值。SkillActionState新增RetreatCooldown、RetreatThreat（默认-1）、RetreatMoving、RetreatGoal；内部快照拒绝非法时间、坐标及敌方引用，旧档字段缺省兼容，正式进度不保存。
