# 战斗配置字段字典

2026-10-22：新增统一属性`engagement_capacity`、unit_attributes同名必填列，以及system/config的索敌/警觉/展开规则。字段、范围和旧档边界详见[索敌配置合同](targeting_config.md)。

目录均相对于 `Idle-CRPG/config/`，UTF-8，Godot importer=keep。技能表使用严格 CsvTable 解析；列表以分号分隔，空值与0含义不同，不应随意互换。源字段保留不等于支持所有源系统。算法见 [战斗公式](../design/combat_formula.md)、[技能运行](../design/skills_effects_buffs.md)、[属性与等级](../design/attributes_levels.md)。

## 属性和等级

| 表 | 字段及合同 |
|---|---|
| combat/fightattr.csv | attr_id稳定唯一；name名称；category消费分类；value_type=number/ratio；aggregation=base_flat_pct/add/add_clamp/multiply；allowed_ops分号列表，与聚合匹配；default_value有限；display_format=number/integer/percentage；ui_visible=0/1 |
| combat/fightattr_rules.csv | id引用属性；baseline等于default_value；min_value/max_value允许空，非空为有限值且下界≤基线≤上界；必须与注册表一一对应 |
| hero/herolevel.csv | id从1连续有序；exp正整数；attack_bonus、hp_bonus非负有限累计加值、不递减，1级为0 |
| monster/monsterlevel.csv | id从1连续有序；hp_multiplier、attack_multiplier正有限、不递减，1级为1 |
| combat/damage_rule.csv | 恰有3条id,value：base_critical_multiplier原型1.5（≥1）、resistance_minimum原型-1（<0）、resistance_maximum原型0.75（≥0且<1）；无minimum_base_damage |
| combat/armor_curve.csv | id为严格递增的攻击者等级控制点、k为正有限且不递减；从1级覆盖英雄/怪物最高等级，点间线性插值。原型两点1→5、10000→10004，即K(L)=L+4；缺表/不覆盖/非法K拒绝 |
| combat/unit_attributes.csv | id模板ID；side=hero/monster；dodge_rate、crit_rate、crit_damage、block_rate、block_reduction为已注册属性值；留空使用规则基线；ranged为0/1。每个模板必须有对应配置 |

注册表规则是实际校验输入，概率还必须满足伤害模型数学范围。基础模板属性也通过属性目录校验。等级两表末行决定各自最高等级。spawn_rule 不再包含 hp_per_level、attack_per_level、defense_per_level。

2026-10-22公式迁移：unit_attributes新增必填列fire_resistance/cold_resistance/lightning_resistance/poison_resistance，当前均0，允许负值且必须有限；同名属性为add聚合，基线0，属性注册不提前截断，伤害解析合并全部来源后按damage_rule截断一次。旧damage_multiplier从正式注册表替换为power_damage_pct，add聚合、基线0、允许add贡献，数值为额外比例（+50%填0.5）；下界-1用于有限减益及旧小于1倍率兼容，最终P=1+Σ贡献不得为负。旧字段及multiply输入只保留在TEST_ONLY归档，不同时维持两个生产威能字段。

DamageInput可携带最多五个类型唯一的DamagePortion(Type,Weight)，权重非负且合计1；AttackerLevel必须落在K曲线覆盖范围。DamageBreakdown.Components记录逐分量防御与伤害，FinalDamage合计后一次应用HP。SkillAttackSnapshot新增Level；CombatVersion=2负责旧倍率语义及缺等级快照的一次性转换，正式存档版本仍为4，房间重开边界不变。

## Skill / Effect / Buff / Trigger

| 表 | 字段 |
|---|---|
| skill/skill.csv | id；role来源分类；kind=BasicAttack/ActiveSkill；cooldown_ticks正整数；windup_ticks/recovery_ticks非负；coefficient非负伤害本体系数；range正世界单位；effect_ids非空效果列表；name；cast_sound_id/hit_sound_id仅保留来源元数据 |
| skill/effect.csv | id,role,kind；damage_type=Physical/Fire/Cold/Lightning/Poison；share伤害分配系数；extent范围、speed速度；impact_effect碰撞效果；behavior弹道行为；spawn_tick；buff_id；entity_kind；lifetime_ticks；resolve_effect_ids生命周期结束效果；mechanic_id能力/召唤路由引用 |
| skill/summon.csv | id；name显示名；max_count同一主人同原型存活上限1～5；hp_ratio/attack_ratio/defense_ratio按主人施法时基础生命/攻击/防御的比例，生命与攻击须为正、防御非负；speed/range/cooldown_seconds为召唤物自身移动、攻击距离与普攻冷却；engagement_capacity非负，仍可被有效属性聚合修正。当前仅QA切片填写，正式表保持空表不投放玩家召唤 |
| skill/buff.csv | id；duration_ticks正时长；stacking_policy=NonStacking/RefreshDuration；attributes为attribute:operation:value分号列表，必须注册且操作合法；periodic_effect；periodic_interval_ticks；trigger_ids；mechanic,value,extra,target为已实现机制参数 |
| skill/trigger.csv | id,event_kind,effect_id,owner_buff,origin_filter；引用有效，owner_buff与Buff.trigger_ids双向一致；事件和过滤仅允许已实现组合 |

Effect 的 kind 决定允许字段：Damage使用damage_type/share；ApplyBuff使用buff_id；SpawnProjectile使用extent/speed/impact_effect/behavior/spawn_tick；SpawnEntity使用entity_kind/lifetime_ticks/resolve_effect_ids；Ability路由使用mechanic_id；Summon也仅使用mechanic_id并必须引用summon.csv。无关字段非空会拒绝，避免配置看似有效却无作用。Effect/Buff/Trigger引用缺失、执行环均拒绝。

Effect新增必填表头damage_parts，仅Damage允许非空，格式如Physical:0.6;Fire:0.4。类型枚举严格匹配且不重复，权重有限非负且合计1，首类型与damage_type一致；空表示原单类型。所有生产旧行已补空列，归档副本保留原schema。该字段只改变单次伤害实例的分量，不新增Effect/奖励入口。

SpawnProjectile新增必填表头projectile_count（整数1～64）、spread_degrees（有限0～180度）；仅该kind可非空，单发夹角必须0。多发以目标方向为中轴在总夹角内等角分布；每箭自己的Hit列表只过滤本箭重复扫掠，同次施法其他箭仍可命中同目标。默认现有单发为1/0，天赋运行时生成的单发也保持1/0；5箭20度仅存在A阶段QA数据，不进入正式技能数值。

SkillFlightState与SkillEffectWork新增OriginCast，正数对应本房NextId范围内的同一次施法/普攻；旧现场0表示未知来源。泛用箭携带SkillAttackSnapshot，碰撞后的Damage Work继承Snapshot/OriginCast，防止命中时重新读取已变动的攻击。该关联不等于已支持完整Proc ICD或每Cast限制。

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
