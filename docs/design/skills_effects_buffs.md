# Skill / Effect / Buff / Trigger 迁移运行规则

2026-10-22技能BD补充：[G01～G06规则](skill_bd_interactions.md)及[新技能迁移计划](../technical/skill_bd_plan.md)已归档。下文15技能与现有职业原型是当前实现及旧回归基线；旧CSV已有TEST_ONLY快照，尚未退出生产引用。正式三系迁移已落地（见 §9）：新增13个主动、1个Ability和7个Buff，旧`barb_bash`/`mage_fireball`/`paladin_smite`改由绑定定义执行但保留原ID；新技能数值均来自隔离QA原型，不是最终平衡。多光环、多诅咒、Proc资格、独立召唤及尸体消费的审计缺口已由B1/B2/C1/C2/C3 Gate补齐，仍按各自投放下限使用。现有唯一伤害、Buff与队列继续复用。

更新及确认：2026-10-18。源为 `E:/Git/Idle-Diablo` 当前代码和 `idle-diablo/config/skill/`，以实际代码和本次用户裁定优先于源文档中的过时说明。来源与差异见 `docs/reviews/2026-10-18_act1_combat_chapters.md`。

## 1. 投放范围

迁入15个主动技能、7个 Buff、2个 Trigger、14个 AbilityEffect，以及两种普攻定义。职业默认主动技能列表为空，不把源测试角色9100的“全装备15技能”配置复制给玩家；也不迁入旧测试飞弹。源 TESTONLY 普攻/伤害 ID 改为 basic_player / basic_monster / hit_player / hit_monster，15个正式 skill_NN ID 保持不变。

未来装备技能必须调用 `GameSession.EquipSkills`；此轮没有自动职业分配、技能升级购买或新技能 UI。`Actor.SkillLevels` 保留等级数据，初始读取 Ability 等级1、上限50；英雄等级不会自动提高技能等级。

## 2. 定义与执行职责

| 文件 / 类型 | 负责内容 |
|---|---|
| `SkillConfiguration.cs` | 严格读取七张源表和本地 runtime 表；定义、引用、互斥字段、属性注册和执行环校验 |
| `SkillRuntime.cs` | 单动作槽、冷却、效果队列、Buff生命周期、事件、唯一扣血/奖励应用 |
| `AbilityRuntime.cs` | 二维选敌、直射/贯穿/追踪/天降/区域、控制、护盾反击、冷却返还、分身 |
| `SkillBattleState.cs` | 动作、Buff、飞行和队列现场的数据；不放入正式进度战斗现场 |
| `SkillSnapshot.cs` | 内部完整快照的动作/实例/引用合法性检查 |

所有伤害进入 `DamagePipeline`，不另存一份怪物生命。Effect 和 Buff 不写配置模板，不在 UI 绘制时结算。

## 3. 时间、单位与房间边界

源时间轴使用8Hz，本项目20Hz。源 CSV 中 Tick 字段在迁移时转换为 `ceil(旧Tick×20/8)`；例如10Tick冷却→25Tick=1.25秒，1Tick前摇→3Tick=0.15秒，48Tick Buff→120Tick=6秒，7Tick反击周期→18Tick=0.9秒。秒制参数运行时向上量化到0.05秒边界，不按渲染帧推进。

原像素距离/速度仍保留在源能力表中，加载时除以 `pixels_per_unit=100`，转为二维世界单位；范围与选敌按二维中心距离计算。源一维 X 排序改为欧氏距离，距离或生命相同时用稳定角色顺序打破并列。

“全场”只指当前房间已激活的合法敌人；最高生命不限射程仍受这个作用域限制。不预生成邻房，不攻击走廊或其他层。击退和聚怪在二维方向上计算，并限制于当前房间内墙范围，不触发跨门探索。

## 4. Skill 动作时间轴

每个角色只有一个主动动作槽：可用→前摇→Commit→恢复。Commit 才扣冷却并安排根效果；前摇目标死亡或施法者不能行动时取消，不扣冷却。同一角色同时有多个可用主动技能时，按显式装备列表顺序选择；均不可用时回到普攻。

普攻仍使用原单位 `cooldown` 秒间隔、`range` 和攻速，保留当前游戏节奏；不叠加源 Demo 普攻的5Tick前摇/恢复。普攻系数和 Damage Effect 从两种普攻定义读取，进入同一 Effect 队列和伤害管线，不存在旧伤害旁路。

输出技能冷却按每秒 `1+CDR` 消退，上限受配置约束；自身增益的冷却不再被攻速/CDR重复加速。增益冷却为 `max(持续时间×1.25, 基础冷却×[1−0.02×(技能等级−1)])`。输出威力为 `效果power×技能coefficient×[1+0.15×(技能等级−1)]`；这些本体值之后再进入 DMG3 技能加算池。

## 5. Effect 队列与 Tick 内顺序

2026-10-22修订：有怪房间先执行可配置的警觉/小队展开窗口；正常交战Tick恢复64条Effect预算，推进Buff、CD、恢复、前摇、Commit和飞行，执行已排队效果，再按英雄稳定顺序行动；怪物普通目标清理/确定性预约/有界重评后，按怪物稳定顺序行动，最后处理剩余效果。已删除改变权威坐标的Separate避让，改为只读视觉偏移；清房丢弃本房临时状态。普通主锁不覆盖技能临时目标或已提交效果，详见[索敌与入场](combat_targeting.md)。

预算耗尽的队列留到下个逻辑 Tick，不丢失；最大积压1024、原因链深度4，超出明确报错。配置执行环在加载时拒绝；分身和反击不会重新触发“主施放”监听。后置事件不回写已完成的那次伤害。

支持 Damage、ApplyBuff、SpawnProjectile、有限生命周期 SpawnEntity 和五类 Ability 路由。SpawnEntity 只处理生命周期结束后的配置效果，不代表完整召唤AI。未实现的效果、叠层策略或字段组合明确拒绝，不因枚举/表头存在就伪装成可用机制。

## 6. Buff 与 Trigger 生命周期

Buff 的属性通过统一注册表派生，不把临时加成写回永久 Attack/MaxHp 等字段。RefreshDuration 会撤销同目标同ID旧实例、替换快照并重启周期；NonStacking 保留现有实例，不追加层数。到期先删除，再判断周期，因此到期 Tick 不补最后一跳。

生命上限类 Buff 不因上限增加而自动治疗；显式治疗可使用有效上限。Buff 移除时生命收敛到剩余有效上限。清房、进入新房/层、全灭及显式换技能会清理动作、Buff、弹道、队列和监听。已倒地队员不能被增益或普通自疗隐式复活。

当前两种 Trigger：`tr_crit_shorten` 监听主施放首发暴击，`tr_skill_mirror` 监听主输出施放Commit。owner_buff 与 Buff.trigger_ids 必须双向一致。监听随 Buff 实例存在，移除或破盾后不会继续执行旧周期。

## 7. 十五项内容清单

| ID / 名称 | 执行行为 |
|---|---|
| skill_01 幽魂骨矛 | 单发直射，首次碰撞消散，配置允许一次穿透概率 |
| skill_04 狂热战吼 | 自身攻速+25%，6秒；施放治疗5%最大生命 |
| skill_11 追魂飞刃 | 三发弧线追踪，最低生命优先，目标不足时循环选取 |
| skill_06 刀锋之雨 | 五次错时天降，固定落点与范围打击 |
| skill_17 死亡横扫 | 贯穿，每发对同一敌人最多一次；目标低于30%生命时独立倍率×2 |
| skill_14 骸骨护甲 | 6秒护盾=施放攻击×2，周期反击；护盾耗尽立即移除 |
| skill_02 陨石火雨 | 最远目标处固定落点，落地转为5秒火海，周期范围伤害并刷新灼烧 |
| skill_12 冰霜漩涡 | 4秒固定区域，周期伤害/聚怪，命中施加5秒25%减速 |
| skill_07 雷霆审判 | 延迟0.6秒雷击并施加1.5秒眩晕 |
| skill_18 末日陨星 | 三枚陨星尽量各锁一敌，固定落点爆炸 |
| skill_09 杀戮狂热 | 8秒暴击率+40%；首发暴击随机返还一个输出技能1秒冷却，受返还底线限制 |
| skill_05 震荡裂地波 | 贯穿、击退；主施放对本房敌人定身1秒，分身不重复控制 |
| skill_19 暗影分身 | 10秒复制输出技能首效果，初始70%，每级加1%；偏移/延迟，不复制增益、不递归 |
| skill_10 灵魂收割 | 本房最高当前生命目标，不限射程；命中前已有状态时独立倍率+30% |
| skill_15 天启剑阵 | 四次天降，每次命中本房全部合法敌人；开始施放仍要求射程内有目标 |

所有原始 power 保留，没有因为名称替技能添加元素伤害或另一套减伤。具体速度、次数、半径、时长和延迟以表为准。

灼烧每秒为原因攻击快照系数的25%，继承其暴击，不额外重掷或平方暴击，不继承斩杀/已有状态条件倍率。源1Tick灼烧周期从0.125秒量化为0.15秒，单跳按实际周期乘算以保持该DPS约定；到期 Tick 丢弃末跳，因此总伤害由实际发生的跳数决定。

## 8. 保存、限制及验证

正式进度保留装备技能 ID、技能等级、累计属性和资源；未清房按进房位置/生命和战斗随机检查点重开，Buff、剩余CD、在途物、待执行效果不保留。内部 `Capture()` 保留完整现场，用于确定性回归；不能用它替代正式 `CaptureProgress()`。

旧存档缺字段时为空技能、空临时现场，现有等级和聚合成长保留。角色技能表中不存在的 ID、非法等级、动作引用或非有限数值会拒绝。音效 ID 原样保留为来源元数据；本轮没有迁入源项目专用声音或技能表现资源，不把它们误称为已接入。

验证入口为 `CombatSkillChecks`：15项逐一Commit和实际伤害/Buff检查、二维在途和火海/DoT完整快照续接、延迟命中、护盾破裂、分身不递归、前摇取消、Buff到期、作用域不越房、正式进度清场及非法CSV引用拒绝。它们验证了这些明确边界，不代表全部未来自定义技能组合均已穷举。

## 角色天赋与玩家装配（2026-10-21）
原有15技能仍保留供旧档与回归。玩家天赋主动通过配置追加到Skills目录；伤害共用SettleDamage，吼叫复用Buff生命周期。被动在有效属性计算时添加、不写回永久基础属性。玩家装配使用SetTalentLoadout，保留原动作CD和房间Buff，新增战斗换入完整冷却。详见[talent_trees.md](talent_trees.md)。

## 9. 正式三系绑定技能（2026-10-22）

`talent.csv.skill_id` 与节点同名的主动不再生成旧模板，`SkillRuntime.Commit` 直接按 `skill.csv` 的 `effect_ids` 入队，系数取 `TalentValue`（`value+per_rank*(生效等级-1)`）。旧ID留空`skill_id`，继续走 `CastTalent`，行为不变。

| 技能ID | 效果组合（`effect.csv`） | 说明 |
|---|---|---|
| barb_bash | Damage Physical | 原ID绑定，瞬发近战 |
| mage_fireball | SpawnProjectile → Damage Fire | 保留`Effect=shot`的半径扩散特例 |
| paladin_smite | Damage Physical | 原ID绑定，瞬发近战 |
| barb_taunt | ApplyBuff `barb_taunt_buff` | 40 Tick强制锁敌，不打断已提交前摇 |
| barb_whirlwind_window | ApplyBuff `barb_whirlwind_buff` | Buff周期触发`barb_whirlwind_pulse`（AreaPulse） |
| pal_prayer | MaintainAura `pal_prayer_buff`（周期`pal_prayer_heal`） | 友方择强，不复活 |
| pal_fanaticism | MaintainAura `pal_fanaticism_buff` | 攻速加算 |
| pal_conviction | MaintainAura `pal_conviction_buff` | 敌方四抗降低，与减抗诅咒独立叠加 |
| sor_chain_lightning | Chain `sor_chain_lightning` | 有界跳数/距离/衰减 |
| nec_raise_skeleton | Summon `nec_skeleton` | 上限1，继承主人基础属性比例 |
| nec_amplify_damage | ApplyBuff `nec_amplify_buff` | `cast_condition=missing_buff` |
| nec_lower_resist | ApplyBuff `nec_lower_resist_buff` | 四抗降低 |
| nec_corpse_explosion | CorpseExplosion | 消耗本房真实尸体，无尸体不扣冷却 |
| ran_multishot | SpawnProjectile ×3（20°） | 初始技能 |
| ran_guided_arrow | AbilityCast `ran_guided_arrow` | 复用`arc_homing`有界重选 |
| ran_valkyrie | Summon `ran_valkyrie` | 上限1 |

所有效果继续走唯一 `SettleDamage`/奖励路径；召唤击杀经验归全队、不产尸、不掉奖。`SkillMigrationChecks` 验证绑定光环与三箭齐发的实际执行；全部17个正式技能已有临时几何表现并通过Mini截图检查（`CombatFxView`只消费`VisualFxRuntime`只读事件，不参与权威计算）。这些是临时占位表现，不是最终美术；最终数值平衡仍待统一重算。
