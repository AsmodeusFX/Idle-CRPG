# 天赋树字段字典

所有路径相对 `Idle-CRPG/config/`，UTF-8 CSV、importer=keep。规则说明见 [角色技能与天赋树](../design/talent_trees.md)。

## system/config.csv

字段 `id,value`：唯一规则名与非负整数。以下规则必须完整配置，缺失、负值及超过100000拒绝加载。

| id | 首版原型 | 含义 |
|---|---:|---|
| talent_initial_points | 1 | 1级获得的天赋点 |
| talent_points_per_level | 1 | 每提升一级新增点数 |
| talent_row_points | 5 | 默认相邻排投入门槛增量 |
| active_skill_max_slots | 5 | 主动技能最大槽数，1～5 |
| active_skill_initial_slots | 1 | 初始槽数，0～最大值 |
| talent_reset_base_cost | 25 | 单英雄洗点基础金币 |
| talent_reset_cost_per_point | 5 | 洗点每已投入点额外金币 |
| talent_leap_ticks | 10 | 跳斩前摇/连续跳跃时长，20Hz Tick，正数 |
| talent_windup_ticks | 3 | 其他天赋主动前摇，正数 |
| talent_recovery_ticks | 3 | 天赋主动公共动作后摇 |

点数总额=`initial + (hero.Level-1)*per_level`；默认排门槛=`row*row_points`；洗点价=`ceil((base+spent*per_point)*(1-clamp(discount,0,1)))`。

## skill/talent.csv

| 字段 | 合同 |
|---|---|
| id | 全局稳定唯一节点ID；主动节点同时作为技能运行ID，不得撞已有技能 |
| hero | heroes.csv职业ID；野蛮人为warrior |
| tree / tree_name | 系编号0～2、中文系名，同职业同系名称一致 |
| row / col | 从0开始，row=0～100，col=0～2；同职业同系不能重叠 |
| name / description | 非空显示名称/效果描述 |
| max_rank | 可学习最高等级，1～100；额外等级不消耗此上限 |
| bonus_allowed | 0/1；0忽略任何来源额外技能等级 |
| kind | active / passive |
| prerequisite | 空或同职业同系更早行节点ID；最多一个显式前置 |
| required_rank | 前置节点最低手动学习等级；无前置为0，非空时1～前置上限 |
| required_points | 本系累计投入要求；-1采用默认排规则，其他为非负整数 |
| effect | 主动：bash / leap / whirlwind / shout / weaken / shot / nova；被动：atk / armor / crit_rate |
| value / per_rank | 非负有限基础效果与每级增量；效果=`value+per_rank*(effective_level-1)`，未学习为0 |
| cooldown | 主动冷却秒数，0.05～5000；转换为20Hz Tick向上取整；被动可填0 |
| range / radius | 非负世界单位，主动range必须大于0；radius为范围攻击或吼叫半径 |

主动伤害的value为攻击倍率；shout/weaken为攻击提升/降低比例；atk/armor为固定加值，crit_rate为暴击率加值。挫锐吼叫最大削弱到零攻击基础值，最终伤害仍遵循已有DMG1保底。两种吼叫持续时长来自 `buff.csv` 中 `talent_battle_shout` / `talent_weaken` 的 `duration_ticks`。

布局、引用、前置行序、数值、开关和系统槽数在加载时校验；存档另验证总学习预算与可达学习顺序。

## 星图新增效果

既有内容/布局/意图三表继续使用，operation=flat。

| attr_id | 含义与约束 |
|---|---|
| skill_slots | 全队主动技能槽+1，单级、value_per_level=1 |
| talent_reset | 解锁城镇洗点，单级、value_per_level=1 |
| talent_reset_discount | 洗点费用降低比例，所有配置节点最大折扣合计不得超过1 |

GM编辑器支持以上效果，继续执行草稿保护、冲突检测及已购记录校验；刷新配置不能使已装配天赋主动技能数超过解锁槽数。旧存档无需自动购买或消耗金币。

## 永久状态和扩展命令

Actor新增：`Talents`（节点ID→手动等级）、`TalentBonuses`（来源列表）、`SkillSwapLocks`（技能ID→剩余换入等待秒数）。`SkillIds` 继续保存有序主动装配；旧 `SkillLevels` 保留给原有15技能兼容，不存储天赋生效等级。

`TalentBonus(Source,Scope,Target,Levels)`：Scope=node/tree/all；Target为节点ID、系编号字符串0/1/2或空；Levels为0～1000整数，Source非空。`GameSession.SetTalentBonuses` 每次替换指定来源，空列表卸下该来源，不是累加调用。额外等级只对已学习且允许加成的节点生效。

UI通过 `LearnTalent`、`SetTalentLoadout`、`ResetTalents` 命令修改状态，不能直接写Actor。逻辑步推进换入冷却不增加Revision；学习、装配、来源替换和洗点才触发关键状态保存。存档保持版本4的追加字段兼容，缺字段为空集合；进房重开保留所有学习/加成/装配/换入冷却，但不保留临时Buff或怪物现场。

学习命令补充（2026-10-21）：首次学习主动且有解锁空槽时，LearnTalent内部调用SetTalentLoadout追加新技能；不覆盖已有技能，也不因升级而重新装配。无新增CSV或存档字段，战斗自动装配复用原换入冷却与检查点保存规则。

## 五职业与室外跟随字段（2026-10-21）

`skill/talent.csv` 新增必填 `damage_type`（Physical/Fire/Cold/Lightning/Poison）与 `projectile_speed`（非负有限世界单位/秒；shot必须大于0，其他节点填0）。range/radius/projectile_speed还需能表示为有限float。shot生成有速度的指定目标投射物，radius>0命中扩散；nova按目标坐标取范围；原有bash/whirlwind/leap继续保留。所有新职业内容均使用该表，每系最多三列。

`skill/hero_starting_skill.csv`：`id`为职业ID，`skill_id`为初始攻击节点。每职业恰好一条，必须引用本职业首排、无前置、投入要求0的攻击主动（bash/leap/whirlwind/shot/nova），不能用增益替代初始攻击。节点消耗1级的1点，不是额外赠送。

Actor新增bool `StartingSkillGranted`，缺字段为false，仅用于一次性初始化；洗点不清除此标记。字段不强制保留节点，故已初始化且天赋为空的存档合法。初始投入进入Talents、SkillIds的现有永久合同，洗点与手动技能同样返还点数。

`system/config.csv` 新增 `world_follow_spacing_milli`，原型55，范围1～1000；换算为每名跟随者之间的世界路径距离（value/1000）。排头行走路径保留短折线，队员按index×间距采样，抵达入口时沿同一条折线逐步靠拢；步长共用世界travel_speed，不按帧或角色渲染改速度。

WorldSnapshot新增 `OutdoorTrail`（从排头向队尾的有限世界坐标折线，最多1024点）、`FollowGather`（到达终点后的靠拢距离）。恢复检查可通行性、线段障碍、连接排头和有限集合进度。缺字段为空轨迹/0，队员从当前位置逐步展开；旧地形/布局/门禁迁移前清除旧轨迹，以免跟随旧坐标。存档仍为版本4追加兼容。
