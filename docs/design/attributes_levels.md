# 属性注册与等级成长

确认日期：2026-10-18。参照 Idle-Diablo 的属性注册/规则分表方式，数值沿用本项目，未复制源项目平衡。本文与 [战斗公式](combat_formula.md)、[技能运行](skills_effects_buffs.md) 共同构成审阅依据。

## 属性注册

`config/combat/fightattr.csv` 注册稳定 ID、名称、分类、数据类型、聚合方式、合法操作、默认值和显示格式；`fightattr_rules.csv` 一对一配置基线、最小值和最大值。规则基线必须等于注册默认值。所有当前投入战斗计算的数值属性均在此注册；角色 ID、等级、伤害类型、远程标志、资源与技能 ID 属于元数据，不是可聚合战斗属性。

| 类别 | ID |
|---|---|
| 基础 | atk、hp、armor、move_speed、attack_range、basic_cooldown |
| 事件与速度 | dodge_rate、crit_rate、crit_damage、block_rate、block_reduction、attack_speed_pct、cooldown_reduction_pct |
| 通用增伤 | all_damage_pct、skill_damage_pct、basic_attack_damage_pct、physical_damage_pct、fire_damage_pct、cold_damage_pct、lightning_damage_pct、poison_damage_pct、ranged_damage_pct、boss_damage_pct |
| 其他伤害池 | skill_coefficient_bonus、damage_multiplier、vulnerable_damage_pct |

2026-10-22增加`engagement_capacity`，共27项。它属于基础战斗属性，提供普通近战优先预约，不限制实际受击人数；原型基础值野蛮人3、圣骑士5、其余英雄0。复用基础/固定/百分比聚合，最终向下取整；详见[索敌配置](../technical/targeting_config.md)。`skill_damage_pct` 表示主动技能通用增伤，进入通用池；技能本体系数的加值使用 `skill_coefficient_bonus`，进入技能池。指定技能的贡献还需 `DamageModifier.SkillId` 等资格，不把“所有技能增伤”和“指定技能系数”混为一项。易伤按 Buff 稳定 ID 分组。

`base_flat_pct` 聚合为 `(基础值+Σflat)×(1+Σpct)`；`add` / `add_clamp` 为基础加总；`multiply` 为基础乘各来源。最终按规则边界夹取。Buff 不得重设 base；未知 ID、非法操作、非有限数、非正倍率和不支持的负伤害加值拒绝加载。

现有模板字段 `attack` 映射 `atk`，`defense` 映射 `armor`，`speed/range/cooldown` 映射移动速度/攻击距离/普攻间隔。星图旧格式 `attack` 保持兼容，通过现有固定攻击贡献进入 Actor.Attack；不是第二套未注册伤害机制。生命注册项 `hp` 表示最大生命，当前 HP 是运行状态。

新增属性必须先注册，再实现来源及消费位置并补充校验。仅增加 CSV 行不会自动创造新机制。首版无元素抗性层。

## 英雄等级表

`config/hero/herolevel.csv`：`id,exp,attack_bonus,hp_bonus`。

- id 从1连续递增；exp 是从本级升到下一级需要的个人经验，维持 `18×本级`。
- attack_bonus / hp_bonus 是相对1级的累计加值，不是每次升级直接加整行。首版为 `4×(等级−1)`、`18×(等级−1)`，1级均为0。
- 跨级逐级处理，应用相邻两行的差值。因此每级仅增加4攻击、18最大生命；护甲、速度等不随等级增长。
- 存活英雄当前 HP 增加本次最大生命差值；倒地者保持0，不通过升级复活。星图增上限仍不治疗，临时 Buff 也不因增上限自动治疗。
- 表尾是最高等级，不再推导不存在的下一行。达到上限后经验保留，整数累积饱和于 int.MaxValue，不溢出。

## 怪物等级表

`config/monster/monsterlevel.csv`：`id,hp_multiplier,attack_multiplier`。

| 项目 | 公式 |
|---|---|
| 最大生命 | 模板HP × 等级行hp_multiplier × BOSS生命倍率 |
| 攻击 | 模板攻击 × 等级行attack_multiplier × BOSS攻击倍率 |
| 护甲 | 模板defense，不随等级增长 |
| 首版倍率 | HP=`1+0.18×(等级−1)`；ATK=`1+0.12×(等级−1)` |

倍率为相对1级的线性累计比例，不是逐级复利。1级两项均为1。BOSS乘一次；GM临时有效倍率另在战斗入口处理。删除 spawn_rule 旧的三种属性成长字段，避免两处同时生效。

怪物等级仍由地牢区间、独立刷怪基础等级和刷怪增量决定，不依赖英雄等级。首次进入房间生成后保持快照；等级超过表尾明确拒绝，不静默夹低。配置启动和 SetSpawnModifiers 都校验地牢可生成等级范围。

经验仍为 xp_base×怪物等级×BOSS奖励倍率；掉率、金币和杂物价格成长仍由原刷怪/掉落规则决定，不放入等级属性表。

## 表尾、存档与维护

首版两表各覆盖1～10000，以维持原先允许的怪物等级范围；10000是原型覆盖范围，后续可通过扩展/缩短表调整上限。用户确认的是“表尾即上限”，并非最终平衡上限10000。

存档保留已有聚合攻击、最大生命和等级，不在读档时重新叠加从1级到当前的成长。升级只应用下一等级差值，因此旧星图和累计成长不会重复。旧档缺少战斗元数据时补配置；已有聚合攻击/生命保留。保存等级超过新表尾会拒绝恢复，应先修复表覆盖范围，不能悄悄降级。

外部改表不会自动重算已保存英雄的历史属性。正式检查点重建未清房怪物时使用当前配置和保存的出生模板/等级，因此离线改表会影响重开战斗；不承诺跨配置版本数值现场一致。

实现：`FightAttributes.cs`、`LevelConfiguration.cs`、`CombatConfiguration.cs`、`MonsterSpawning.cs`、`HeroExperience.cs`。验证：`tests/AttributeLevelChecks.cs`，覆盖累计差值、表尾、非法缺行、未知属性及恢复不重复增长。

## 个人经验与等级差（2026-10-21）

每名英雄独立记录等级、当前级经验和百分之一经验余数。怪物基础经验发给所有上阵英雄，包括倒地者，再按各自等级应用倍率；候补与未上阵英雄不参与。达到升级条件自然升级，倒地升级不复活。新加入英雄从1级、0经验开始，候补重新上阵保留自身成长。

`config/hero/experience_level_rate.csv` 以英雄等级减怪物等级作为等级差：

| 等级差 | ≤−5 | −4 | −3 | −2 | −1 | 0 | 1 | 2 | 3 | 4 | ≥5 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 原型倍率 | 300% | 260% | 220% | 180% | 140% | 100% | 80% | 60% | 40% | 20% | 0% |

超出配置范围沿用边界行。小数经验按百分之一保留。宝箱经验按房间快照等级应用相同倍率，避免低级宝箱绕过衰减。GM经验直接给每名上阵英雄发放等额经验。倍率均为原型值，尚未接入星图收益修正。
