# 公式专项与A阶段Core QA执行记录

2026-10-22。状态：公式专项、A阶段、B1、B2、C1、C2与C3隔离Core QA完成；正式五职业三系与17技能迁移已按迁移清单 §3.5～3.7 落地（新数值均为原型）；全部17个正式技能已有临时几何表现并通过Mini截图人工检查；旧节点/旧技能按用户反馈保持退役并归档备案。最终平衡由用户后续统一重算，Proc ICD语义仍待裁决。完整验收结论见[验收报告](07_acceptance.md)。

## 本次授权

用户先批准公式修改方向、保留既有设定、新技能另做迁移且旧数据归档测试、文档合并，随后明确“开始执行”。执行中两项明确选择：

1. 采用可调原型K(L)=5+(L−1)、元素抗性截断−100%～75%、取消最低1点保底、威能与易伤同池加算。
2. A阶段“允许同次施法多箭命中同一目标，先做QA切片”，不替换正式技能树、不新增装备系统。
3. B2先完成独立召唤Core QA，采用“保守单骷髅”：上限1只，按主人基础攻击/生命/防御固定比例继承；主人死亡、卸槽、换队、全灭即消失；跨房保留实际HP/数量；不产尸、不产奖；骷髅击杀经验归全队。

这些授权不等于批准正式召唤技能树、召唤生产存档、尸体、完整Proc或17技能全量生产投放；本记录不将Core QA称为完整生产准出。

## 实际变更

| 范围 | 变更与边界 |
|---|---|
| 统一公式 | DamagePipeline按物理护甲K/四元素抗性解析，威能/易伤加算；DamagePortion拆类型、共享命中/暴击/格挡事实后求和，唯一SettleDamage应用HP/奖励；无旧公式生产旁路 |
| 配置 | 新combat/armor_curve.csv+keep元数据；damage_rule明确暴击/抗性截断，删除旧最低伤害键；四抗注册/单位列，damage_multiplier替换为power_damage_pct额外比例；同步字典/校验 |
| 混合Effect | damage_parts类型唯一、权重和1、首项对应damage_type，单次Damage不拆成两个致死实例；无新伤害类型 |
| 快照与兼容 | 出手攻击/等级随投射物与待命中Work保存；CombatVersion=2转换旧倍率x→x−1，旧等级缺字段取旧快照攻方等级，二次恢复不重复转换。SaveDocument仍4、正式进度仍重开房间 |
| A弹道 | 泛用SpawnProjectile支持CSV弹数/扇形角，每箭按真实二维线段扫掠，每箭自身去重，不限制同次其他箭打同目标；OriginCast沿普通动作/技能/飞行/命中/派生Effect传播 |
| B1光环/诅咒 | 复用唯一Buff来源与组策略，加入多光环择强/离开回退、周期治疗不复活、跨房激活、分类型易伤/减抗、Forced安全边界、防御新星和导引箭有限重选；不新增第二套Aura/Curse/AI权威 |
| B2独立召唤 | 新增空表skill/summon.csv与Summon Effect路由；独立Actor追加在英雄、当前房怪物之后共用战斗ID，不并入Heroes/怪物列表。属性按主人施法时基础值快照，上限1，骷髅普攻走basic_player与SettleDamage；跨房保留实际HP，进房检查点只保留进房时已存在的召唤；主人死亡/卸槽/换队/全灭撤销，死亡不进入怪物奖励或尸体路径 |
| C1有界链 | 新增空表skill/chain.csv与Chain Effect路由；仅此Effect获得连锁资格，链根为目标选择结果，逐跳按最近未命中敌人、跳跃距离和总目标上限推进，系数按damage_decay衰减。每跳调用唯一SettleDamage并继承OriginCast，链根死亡后继续，目标不重复；信念减抗复用既有MaintainAura/Buff与目标抗性解析，不新增第二套光环或伤害权威 |
| C2尸体/尸爆 | 新增本房CorpseState token：每次当前房怪物真实死亡登记一条，召唤/英雄/跨房不入列；CorpseExplosion按尸体坐标原子移除token后走唯一SettleDamage，同一尸体上的竞争消费只有一次生效。corpse目标只在存在未消费尸体且在施放/施法距离内时成立，无尸体跳过且不进入冷却；完整快照保留，正式检查点清空。女武神复用B2召唤运行时，按定义分别计数上限 |
| C3旋风/Proc限次 | 新增AreaPulse Effect与空表skill/proc_limit.csv：周期Buff每拍以work.Target当前所在位置为圆心，对范围内敌人走唯一SettleDamage，Buff到期或卸槽即停止，移动中的施法者后续脉冲跟随新位置。Proc限次按(根源施法,触发,目标)计数，max_per_cast/max_per_target分别限制单次施法与单目标，0表示该维度不限且不得两项同时为0；direct过滤只允许depth=0直接命中触发，阻断Proc伤害递归。计数现场有界修剪，完整快照保留、正式检查点清空。不改变现有天赋旋风/旧AoE行为，ICD语义未裁决不实现 |
| QA数据 | tests/fixtures/skill_bd_slice_a四个QA ID、slice_b1光环/诅咒ID、slice_b2召唤ID、slice_c1连锁/信念ID、slice_c2尸爆/女武神ID、slice_c3旋风/Proc限次ID均显式注入，正式Config重新加载不出现QA ID，既有技能/已学/装备记录未切换 |
| 归档 | 原15份旧配置SHA256保持，未用新表覆盖旧输入；正式退出旧技能和玩家ID映射仍按独立迁移方案执行 |

复用现有SkillRuntime、AbilityRuntime、SkillBattleState、Buff、Pending队列、DamagePipeline与SessionSnapshot；新增CombatFormulaMigration只做一次性旧数据转换，不另建战斗权威。

## 验证

- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --formula-checks`：PASS。手算104.13、攻击者等级K、负抗/截断、池内加算、混合共享事件/RNG、零伤害、混合/重复致死奖励一次、出手等级/升级与重放、旧档幂等转换、非法K。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-a-checks`：PASS。重击/盾击/火球/多重箭Core行为，5箭同目标逐箭结算、方向不同/共同施法来源、在途改攻击等级不追溯、完整快照、正式配置无QA注入、CSV非法弹数/角度/分量拒绝。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-support-checks`：PASS。双光环择强/范围回退、周期治疗不复活、跨房激活与检查点、分类型诅咒/减抗、Forced安全边界、防御新星、导引箭有限重选及非法激活存档拒绝。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-summon-checks`：PASS。单骷髅上限、主人基础值固定比例、自动普攻与全队经验、死亡不产奖/尸、跨房实际HP、正式检查点只恢复进房召唤、卸槽/主人死亡/全灭清理、完整快照重放、QA定义不泄漏生产配置。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-chain-checks`：PASS。链根/跳跃距离/目标上限、逐跳衰减与目标去重、单目标终止、链根死亡后继续且击杀只结算一次、目标抗性解析、信念光环减抗与卸槽/离场撤销、完整快照恢复、非法目标数/衰减/引用拒绝、QA定义不泄漏生产配置。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-corpse-checks`：PASS。真实击杀登记唯一尸体token、尸爆原子消费并按唯一伤害路径命中两个存活敌人、无尸体不施放不进入冷却、同尸体两个待执行尸爆只生效一次、完整快照保留/正式检查点清空尸体、女武神与骷髅按定义共存与属性比例、非法尸爆范围/缺少尸爆效果拒绝、QA定义不泄漏生产配置。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-proc-checks`：PASS。旋风窗口到期停止、范围内每拍脉冲与范围外不受影响、移动后跟随施法者当前位置、Proc每Cast与每目标上限、direct过滤阻断递归、完整快照恢复、正式检查点清零、非法限次/脉冲范围拒绝、QA定义不泄漏生产配置。
- `dotnet run --project tests/IdleCrpg.Checks.csproj`：PASS。既有地图/成长/容量/散开/退让、换队、房间重开、资源、奖励、备份等全量回归及新增检查。
- `dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore`：PASS，0警告0错误。
- Godot 4.7.2 console `--headless --path Idle-CRPG -- --smoke-test`：PASS，SMOKE_OK，角色/天赋/装配/城镇/设置/保存读取等现有UI集成正常；自动入口使用artifacts/qa，不触碰玩家user://。
- A阶段QA专属扇形弹道的图形Mini演示：后续已运行并通过，见下节“Mini视觉验收与收尾”；本节其余记录保留为当时状态。

实际回归项目为控制台Checks，未用无测试发现的dotnet test成功退出充当测试通过。新数值均是用户批准的可调原型，不宣称平衡已验收。

## 遗留范围

- 新技能稳定ID/三系布局、旧点数与装配映射、玩家初始技能切换已执行（见下节）；48个稳定节点ID保留并继续生效，15个`skill_XX`从正式`skill.csv`移除但没有删除玩家已学节点或资源。旧配置归档见`tests/fixtures/legacy_skill_bd_2026_10_22/`。
- OriginCast提供根源施法关联；C3的每Cast/每Target上限与direct过滤已按显式proc_limit.csv实现，但Proc ICD语义仍未裁决，现有15技能的事件资格仍沿用原合同且不默认限次。
- 多召唤数量扩展、旧一次AoE迁移、Proc ICD与C阶段其余复合技能未实现，不能用计时SpawnEntity伪装。B2只完成单骷髅隔离QA，C1只完成有界连锁与减抗光环组合QA，C2只完成尸体/尸爆与女武神隔离QA，C3只完成持续旋风窗口与Proc限次隔离QA；均未把QA ID写入正式树或生产存档。
- 没有修改星图购买、最多5人、初始扣点、换队NPC/放弃旧地牢、即时经验和实物走近拾取规则；没有提交/推送/合并Git。

## 正式三系迁移（2026-10-22，后续追加）

用户裁决：小写现有前缀为稳定ID；本次直接按新三系重排；原ID保旧行为、新ID加新技能；五个初始技能替换为对应项（游侠`ran_multishot`、死灵`nec_raise_skeleton`，其余同ID绑定新定义）。

| 范围 | 变更与边界 |
|---|---|
| 配置 | `talent.csv`重写为61节点（野蛮人14、游侠12、法师10、圣骑士12、死灵13），保留全部旧48个ID；新增可选`skill_id`。新节点`effect=data`，绑定同名`skill.csv`主动。 |
| 技能数据 | `skill.csv`新增13个主动与3个原ID绑定行；`effect.csv`新增20行，只复用Damage/ApplyBuff/MaintainAura/Heal/SpawnProjectile/AbilityCast/Chain/Summon/CorpseExplosion/AreaPulse；`buff.csv`7个、`chain.csv`1个、`summon.csv`2个、`ability.csv`/`ability_effect.csv`各1个。 |
| Core | `TalentNode`增加`SkillId`；加载器校验同名/ActiveSkill/冷却Tick/距离，`effect=data`必须绑定；绑定节点不再生成旧模板。`SkillRuntime.Commit`对绑定节点按`skill.csv`入队，系数取`TalentValue`，未绑定节点保持`CastTalent`。 |
| 初始技能 | 游侠→`ran_multishot`，死灵→`nec_raise_skeleton`；初始技能校验允许绑定主动，但仍要求首排、无前置、投入0。 |
| 存档 | `SessionSnapshot.SkillTreeVersion`（0旧/1当前，缺省0）。`Restore`在完整校验前执行一次迁移：同ID保留等级；合并树投入自然延续；不兼容节点按行退款并清理装配/换入冷却/光环/额外等级并写日志；未知/越界/外职业ID直接拒绝；版本1二次读档幂等。 |
| UI/测试 | `TalentSmoke`主动列表期望更新为7；`CombatSkillChecks`迁移数量更新并跳过与节点同名的Ability；`ClassFollowingChecks`支持召唤初始技能与绑定投射物；新增`SkillMigrationChecks`与`--skill-migration-checks`入口。 |

验证：`--skill-migration-checks` PASS；`--talent-checks`、`--combat-skill-checks`、`--class-follow-checks` PASS；全量`dotnet run --project tests/IdleCrpg.Checks.csproj` PASS；`dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore` 0警告0错误；Godot 4.7.2 headless `--smoke-test` PASS（`TALENT_UI_SMOKE_OK`、`SMOKE_OK`）。检查只写`artifacts/qa`或内存编码。

当时未完成：光环/连锁/尸爆/旋风脉冲的专属表现、最终数值平衡、M5旧节点退役与M7旧`skill_XX`去留、Proc ICD语义。后续追加已补齐表现与退役备案（见下节）；最终平衡保持待定，Proc ICD仍待裁决。

## Mini视觉验收与收尾（2026-10-22，后续追加）

为让设计方直接确认表现层进度，新增隔离截图入口，并补齐召唤物的最小绘制：

| 范围 | 变更 |
|---|---|
| 截图入口 | `--capture --capture-bd-skill=<id>` 构造单技能QA战斗现场，推进到召唤实体出现或弹道离手后截屏；`--qa-mini` 使用960×600。只写`artifacts/qa`与忽略的`artifacts/*.png`，不触碰玩家`user://`。 |
| 职业树截图 | `PrepareTalentCapture` 支持 `--qa-class=<id>`，保留默认战士行为；非战士按行序尝试学习一次用于展示三系布局，截图文件名追加职业。 |
| 召唤绘制 | 地牢绘制纳入`session.Summons`，以友方色和名称显示；不并入`Heroes`或怪物列表，不改锁定/伤害权威。 |
| 表现缺陷 | 修复`MotionPresentation`把怪物锁定索引直接用于`Heroes`导致的越界异常；召唤物被锁定时按友方总表取位。该缺陷在B2召唤进入实测后才会触发。 |

实跑证据（均由本机Godot 4.7.2 Mono console执行）：

- `--capture --capture-bd-a --qa-mini`：`BD_A_VISUAL_MODEL_OK: 5 authoritative/drawn arrows; shared cast=1`，`CAPTURE_OK`；截图可见五支扇形箭。
- `--capture --capture-bd-skill=ran_multishot --qa-mini`：3支在途箭，对应正式`ran_multishot`。
- `--capture --capture-bd-skill=mage_fireball --qa-mini`：1条在途火焰弹道。
- `--capture --capture-bd-skill=ran_guided_arrow --qa-mini`：1条在途导引箭。
- `--capture --capture-bd-skill=nec_raise_skeleton --qa-mini`：`summons=1`，骷髅以友方单位绘制。
- 五职业`--capture-talents --qa-class=<id> --qa-mini`：三系标题、节点、投入和主动栏均显示；战士/死灵`--capture-talent-editor`：主动列表分别显示7项与含`召唤骷髅`的绑定主动。

对应截图：`artifacts/preview-bd-a-fan-mini.png`、`preview-bd-skill-{ran_multishot,mage_fireball,ran_guided_arrow,nec_raise_skeleton}-mini.png`、`preview-talents-{warrior,ranger,mage,priest,necromancer}.png`、`preview-talent-editor-{warrior,necromancer}.png`。截图不提交Git。

## 技能专属临时表现（2026-10-22 本轮追加）

用户要求“其余技能专属表现这轮制作，都用临时资产绘制，但每个技能都要可观测”。本轮按此执行，不新增正式美术，也不改战斗权威。

| 范围 | 变更 |
|---|---|
| 表现事件流 | 新增`Scripts/Core/VisualFxRuntime.cs`：8类事件`Cast/Hit/Heal/Buff/Summon/Chain/CorpseExplosion/Pulse`，最多256条，带序号与Tick，只读快照位置/数值/类型/暴击/格挡/闪避/击杀标记。不写入存档、不参与伤害/奖励/随机/锁定。 |
| 事件接入 | `SettleDamage`发命中事件；技能`Commit`发施法事件；治疗、Buff、召唤、连锁逐跳、尸爆、旋风脉冲分别发对应事件。`EnterFloor`与`ResetSkillRoom`清空，避免跨房补播。 |
| 绘制 | 新增`Scripts/UI/CombatFxView.cs`：施法圈与技能名、命中斩击弧与伤害/暴击文字、治疗`+`、Buff环、召唤环、锯齿闪电、尸爆范围圈与“尸爆”、脉冲扩散环；持续光环/诅咒/旋风/嘲讽锁敌/未消费尸体直接读现有Buff/锁定/尸体状态。 |
| 隔离 | UI只在`GameSession`新实例、换层或换房时重置缓存；不写回Actor/Buff/弹道，不复制Core状态。 |

逐技能截图与关键输出（全部`BD_SKILL_VISUAL_OK` + `CAPTURE_OK`，2026-10-22重跑）：

| 技能 | 可观察表现 | 关键输出 |
|---|---|---|
| `barb_bash` | 近战斩击弧+伤害数字 | 怪物HP 9976；`cooldowns=barb_bash` |
| `barb_taunt` | 红色范围圈+敌人减益环+强制锁敌连线 | `buffs=barb_taunt_buff@1; forced=1` |
| `barb_whirlwind_window` | 自身旋风Buff环+旋转弧线 | `buffs=barb_whirlwind_buff@0` |
| `paladin_smite` | 近战斩击弧+伤害数字 | 怪物HP 9981 |
| `pal_prayer` | 绿色持续光环+治疗`+` | `buffs=pal_prayer_buff@0` |
| `pal_fanaticism` | 金色友方光环 | `buffs=pal_fanaticism_buff@0` |
| `pal_conviction` | 紫色范围圈+敌人减抗环 | `buffs=pal_conviction_buff@1` |
| `mage_fireball` | 火焰弹道在途 | `flights=1` |
| `mage_frost_nova` | 蓝色范围爆发环 | 旧行为节点，按M3保留 |
| `sor_chain_lightning` | 锯齿闪电逐跳连线 | `cooldowns=sor_chain_lightning` |
| `nec_raise_skeleton` | 召唤环+友方骷髅 | `summons=1` |
| `nec_amplify_damage` | 红色诅咒环+易伤环 | `buffs=nec_amplify_buff@1` |
| `nec_lower_resist` | 紫色诅咒环+减抗环 | `buffs=nec_lower_resist_buff@1` |
| `nec_corpse_explosion` | 尸体标记+绿色尸爆圈+“尸爆” | `cooldowns=nec_corpse_explosion` |
| `ran_multishot` | 3支扇形箭在途 | `flights=3` |
| `ran_guided_arrow` | 1支导引箭在途 | `flights=1` |
| `ran_valkyrie` | 召唤环+友方女武神 | `summons=1` |

截图：`artifacts/preview-bd-skill-<id>-mini.png`，共17张；不提交Git。表现均为**临时几何图元**，不是最终美术，也不能用Core PASS替代表现签收。

## 退役备案（用户反馈）

- M5：旧树/旧节点定义保持退役，不重新启用；批准布局沿用的48个稳定ID保留。旧树快照在`tests/fixtures/legacy_skill_bd_2026_10_22/config/skill/talent.csv`，含README与SHA256。
- M7：15个`skill_XX`导入技能保持退役，不绑定、不作为初始技能、无玩家入口；15行已从正式`skill.csv`移除，旧回归仅通过`LoadLegacy`叠加TEST_ONLY基线读取。
- 复核更正：此前误把“未绑定`skill_id`”当成M5退役判据，删除了23个仍走`CastTalent`的正式主动节点；现已恢复61节点与全部前置边，并恢复`talent_battle_shout`/`talent_weaken`Buff。`SkillPreviewChecks`改为直接校验生产表（39个主动×1级/满级），`LegacySkillConfig.Load`不再叠加退役基线。
- 汇总口径与逐项列表见[旧节点与旧技能退役备案](08_retired_content.md)。
- 本轮未执行Git提交。

仍未完成：最终数值平衡（用户明确后续统一重算，保持待定）、Proc ICD语义（已整理问题与选项，待用户裁决）、多召唤数量扩展。

## 工作区

开工时存在上轮退让及本轮前序文档/归档未提交变更，均保留。Git后来定位于GitHub Desktop附带路径，分支main；此前审计报告“PATH内git不可用”是当时事实，不再代表当前无法查询。当前diff包含前序变更，不将它们重复算成本次新增。构建/截图/存档缓存保持忽略，不提交玩家进度。
