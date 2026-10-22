# 公式专项与A阶段Core QA执行记录

2026-10-22。状态：公式专项、A阶段、B1与B2隔离Core QA完成；正式新技能树、召唤生产存档与C阶段未投放。

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
| QA数据 | tests/fixtures/skill_bd_slice_a四个QA ID、slice_b1光环/诅咒ID、slice_b2召唤ID均显式注入，正式Config重新加载不出现QA ID，既有技能/已学/装备记录未切换 |
| 归档 | 原15份旧配置SHA256保持，未用新表覆盖旧输入；正式退出旧技能和玩家ID映射仍按独立迁移方案执行 |

复用现有SkillRuntime、AbilityRuntime、SkillBattleState、Buff、Pending队列、DamagePipeline与SessionSnapshot；新增CombatFormulaMigration只做一次性旧数据转换，不另建战斗权威。

## 验证

- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --formula-checks`：PASS。手算104.13、攻击者等级K、负抗/截断、池内加算、混合共享事件/RNG、零伤害、混合/重复致死奖励一次、出手等级/升级与重放、旧档幂等转换、非法K。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-a-checks`：PASS。重击/盾击/火球/多重箭Core行为，5箭同目标逐箭结算、方向不同/共同施法来源、在途改攻击等级不追溯、完整快照、正式配置无QA注入、CSV非法弹数/角度/分量拒绝。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-support-checks`：PASS。双光环择强/范围回退、周期治疗不复活、跨房激活与检查点、分类型诅咒/减抗、Forced安全边界、防御新星、导引箭有限重选及非法激活存档拒绝。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-bd-summon-checks`：PASS。单骷髅上限、主人基础值固定比例、自动普攻与全队经验、死亡不产奖/尸、跨房实际HP、正式检查点只恢复进房召唤、卸槽/主人死亡/全灭清理、完整快照重放、QA定义不泄漏生产配置。
- `dotnet run --project tests/IdleCrpg.Checks.csproj`：PASS。既有地图/成长/容量/散开/退让、换队、房间重开、资源、奖励、备份等全量回归及新增检查。
- `dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore`：PASS，0警告0错误。
- Godot 4.7.2 console `--headless --path Idle-CRPG -- --smoke-test`：PASS，SMOKE_OK，角色/天赋/装配/城镇/设置/保存读取等现有UI集成正常；自动入口使用artifacts/qa，不触碰玩家user://。
- A阶段QA专属扇形弹道的图形Mini演示：NOT RUN。当前仅Core QA，没有投放新的界面/技能表现入口，普通Godot烟测不能替代该项视觉验收。

实际回归项目为控制台Checks，未用无测试发现的dotnet test成功退出充当测试通过。新数值均是用户批准的可调原型，不宣称平衡已验收。

## 遗留范围

- 新技能稳定ID/三系布局、旧点数与装配映射、玩家初始技能切换、旧生产技能退出仍需独立迁移方案；本次没有删除已学内容。
- OriginCast目前提供关联，不等于完整Proc ICD/每Cast/每Target上限或合法连锁授权；现有15技能的事件资格仍沿用原合同。
- 多召唤数量扩展、尸体竞争消费、完整Proc ICD与C阶段复合技能未实现，不能用计时SpawnEntity伪装。B2只完成单骷髅隔离QA，未把QA召唤ID写入正式树或生产存档。
- 没有修改星图购买、最多5人、初始扣点、换队NPC/放弃旧地牢、即时经验和实物走近拾取规则；没有提交/推送/合并Git。

## 工作区

开工时存在上轮退让及本轮前序文档/归档未提交变更，均保留。Git后来定位于GitHub Desktop附带路径，分支main；此前审计报告“PATH内git不可用”是当时事实，不再代表当前无法查询。当前diff包含前序变更，不将它们重复算成本次新增。构建/截图/存档缓存保持忽略，不提交玩家进度。
