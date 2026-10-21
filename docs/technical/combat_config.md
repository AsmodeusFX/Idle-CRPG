# 战斗配置字段字典

目录均相对于 `Idle-CRPG/config/`，UTF-8，Godot importer=keep。技能表使用严格 CsvTable 解析；列表以分号分隔，空值与0含义不同，不应随意互换。源字段保留不等于支持所有源系统。算法见 [战斗公式](../design/combat_formula.md)、[技能运行](../design/skills_effects_buffs.md)、[属性与等级](../design/attributes_levels.md)。

## 属性和等级

| 表 | 字段及合同 |
|---|---|
| combat/fightattr.csv | attr_id稳定唯一；name名称；category消费分类；value_type=number/ratio；aggregation=base_flat_pct/add/add_clamp/multiply；allowed_ops分号列表，与聚合匹配；default_value有限；display_format=number/integer/percentage；ui_visible=0/1 |
| combat/fightattr_rules.csv | id引用属性；baseline等于default_value；min_value/max_value允许空，非空为有限值且下界≤基线≤上界；必须与注册表一一对应 |
| hero/herolevel.csv | id从1连续有序；exp正整数；attack_bonus、hp_bonus非负有限累计加值、不递减，1级为0 |
| monster/monsterlevel.csv | id从1连续有序；hp_multiplier、attack_multiplier正有限、不递减，1级为1 |
| combat/damage_rule.csv | id,value；minimum_base_damage当前1；base_critical_multiplier当前1.5 |
| combat/unit_attributes.csv | id模板ID；side=hero/monster；dodge_rate、crit_rate、crit_damage、block_rate、block_reduction为已注册属性值；留空使用规则基线；ranged为0/1。每个模板必须有对应配置 |

注册表规则是实际校验输入，概率还必须满足伤害模型数学范围。基础模板属性也通过属性目录校验。等级两表末行决定各自最高等级。spawn_rule 不再包含 hp_per_level、attack_per_level、defense_per_level。

## Skill / Effect / Buff / Trigger

| 表 | 字段 |
|---|---|
| skill/skill.csv | id；role来源分类；kind=BasicAttack/ActiveSkill；cooldown_ticks正整数；windup_ticks/recovery_ticks非负；coefficient非负伤害本体系数；range正世界单位；effect_ids非空效果列表；name；cast_sound_id/hit_sound_id仅保留来源元数据 |
| skill/effect.csv | id,role,kind；damage_type=Physical/Fire/Cold/Lightning/Poison；share伤害分配系数；extent范围、speed速度；impact_effect碰撞效果；behavior弹道行为；spawn_tick；buff_id；entity_kind；lifetime_ticks；resolve_effect_ids生命周期结束效果；mechanic_id能力路由引用 |
| skill/buff.csv | id；duration_ticks正时长；stacking_policy=NonStacking/RefreshDuration；attributes为attribute:operation:value分号列表，必须注册且操作合法；periodic_effect；periodic_interval_ticks；trigger_ids；mechanic,value,extra,target为已实现机制参数 |
| skill/trigger.csv | id,event_kind,effect_id,owner_buff,origin_filter；引用有效，owner_buff与Buff.trigger_ids双向一致；事件和过滤仅允许已实现组合 |

Effect 的 kind 决定允许字段：Damage使用damage_type/share；ApplyBuff使用buff_id；SpawnProjectile使用extent/speed/impact_effect/behavior/spawn_tick；SpawnEntity使用entity_kind/lifetime_ticks/resolve_effect_ids；Ability路由使用mechanic_id。无关字段非空会拒绝，避免配置看似有效却无作用。Effect/Buff/Trigger引用缺失、执行环均拒绝。

## Ability 和机制参数

| 表 | 字段及含义 |
|---|---|
| skill/ability.csv | id,source_id,form,targeting,hits,cast_root,level,max_level,effect_ids,buff_ids；form/targeting/hits必须为当前实现支持的枚举，cast_root为秒，level在1～max_level，引用有效 |
| skill/ability_effect.csv | id,name,effect_type；condition/condition_value条件；power输出系数；skill_flat来源保留字段；duration/delay/tick_interval秒；range原像素范围；trajectory；projectile_count；hover_time秒；arc_min/arc_max原像素弧偏移；speed原像素/秒；spread；volley_interval/volley_jitter秒；spawn_jitter原像素；pierce_chance概率；aoe_radius原像素；aoe_all布尔；band原像素；knockback/gather原像素；buff_id引用 |
| skill/ability_settings.csv | id,value；像素转换、等级系数、自疗比例、增益冷却、分身偏移/延迟、重选范围/次数、在途上限、CDR上限以及默认飞速、分身每级系数、反击生命周期、斩杀倍率 |
| skill/runtime.csv | id,value；ticks_per_second固定20；effect_budget每Tick预算64；max_depth原因链深度4；max_pending积压上限1024；collision_radius世界单位0.3 |

具体机制参数以CSV和 `SkillConfiguration` 支持集合为准，未实现枚举或组合拒绝。`skill_flat` 当前必须为0，非零拒绝，不投放额外平伤；不得将保留列理解为第二套伤害入口。秒制事件向上量化至0.05秒；原Tick字段已从8Hz转换为20Hz，不能在加载时再乘一次。Ability像素字段在加载时统一除以pixels_per_unit（当前100）；Skill.range及runtime.collision_radius已是世界单位，不再转换。

所有定义加载后只读使用；运行实例、CD、Buff和弹道另存。改字段时同时修改加载器列清单、引用/互斥校验、本文及对应测试。只允许有限数字；有界概率、正数、数量和容量由加载器逐项检查。此轮未提供技能配置热更新界面。

角色三系天赋、新增主动与被动效果、额外等级及基础系统规则见[talent_config.md](talent_config.md)。原有15技能稳定ID和机制继续保留。
