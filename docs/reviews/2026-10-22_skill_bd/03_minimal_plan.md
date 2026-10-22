# 技能 BD 交接包 G0：最小增量方案

2026-10-22后续裁决：D文档合并已批准并执行，伤害公式修改方向已批、完整提案另审。P3改为新技能单独迁移、旧数据TEST_ONLY归档；下文“直接复用旧ID”的建议仅作原审计记录，不作为实际执行默认值。最新方案见[skill_bd_plan.md](../../technical/skill_bd_plan.md)，实际裁决见[执行记录](04_approval_requests.md)。

状态：提案，未实施。决策依赖见[冲突报告](02_conflicts_and_migration.md)和[审批清单](04_approval_requests.md)。本次用户意图为后续规则补充，建议先合并可追溯设计，再逐切片授权开发；不以阅读交接包视为17技能生产投放授权。

## D：本轮建议先做的设计归档

批准后更新 `docs/README.md`、新增 `docs/design/skill_bd_interactions.md` 与 `docs/technical/skill_bd_plan.md`，在现有 combat_formula、skills_effects_buffs、talent_trees、combat_targeting、architecture、persistence_settings 的对应段落添加准确交叉引用和“当前实现/未来目标/待定”边界。

- 记录 G01～G06、多光环、多诅咒、独立召唤、唯一尸体消费与有界连锁原则；不新增运行真值。
- 明确新防御及池内聚合作为待单独批准参数/迁移的目标，保留现行公式说明，不写成当前实机已经采用。
- 保留5人、初始扣点、现行换队与存档流程；包内过时说法列出用户裁定，不回写原交接包作为伪造的来源。
- 保留原型技能/节点ID/点数上限等现状；列17候选映射，不覆盖现有树、不把示例数字导入配置。
- 不改代码、CSV、资源、测试、玩家数据，不执行删除；文档链接与术语人工/文本检查即可，无需构建游戏。

## A：基础动作、统一伤害、投射物

代表：BAR_BASH、PAL_SMITE、SOR_FIREBALL、RAN_MULTISHOT。

拟改位置：`Scripts/Core/SkillConfiguration.cs`、`SkillBattleState.cs`、`SkillRuntime.cs`、`AbilityRuntime.cs`、`SkillSnapshot.cs`，必要时 `Talents.cs`；扩展已有技能CSV及字段字典；批准可玩投放后按引用修改现有 `Scripts/UI/FollowScene.cs`、`TalentUi.cs` 等表现入口，不预设新的技能视图模块。

- 前三者优先映射 barb_bash、paladin_smite、mage_fireball；没有必要换ID、重建同义动作或删除旧15技能。
- 多重箭先确定每Cast同目标有效命中、发射方式及装备条件范围；复用真实二维扫掠，不造第二ProjectileRuntime。
- 以现有 work/flight 增量保留Cast与来源关联，覆盖直接、周期、派生事实；不把Depth当Proc语义，不重写统一DMG。
- 若防御公式/4+1还未完成专项决策，隔离行为验证明确运行于现行公式；不得宣称新伤害合同验收完成。
- 测试：单次动作Commit/CD、N箭N目标实际伤害与同Cast去重、来源事件一次性、目标失效、相同种子与完整快照续行；回归现有CombatSkillChecks/CombatTargetingChecks/TalentChecks。
- 配置/兼容：字段新增必须默认兼容旧内部快照并强校验；正式进度保留技能稳定ID。生产系数/CD未定则仅用隔离QA配置，不添加隐藏正式默认值。
- 清理：本切片无拟删除旧路径；若替换某个弹道分支，先报告具体调用者和兼容依赖。

## B：Buff、光环、诅咒、嘲讽、基础召唤

代表：BAR_TAUNT、PAL_PRAYER、PAL_FANATICISM、SOR_FROST_NOVA、NEC_RAISE_SKELETON、NEC_AMPLIFY_DAMAGE、RAN_GUIDED_ARROW。

拟改位置：上述A文件，加 `FightAttributes.cs`、`CombatTargeting.cs`、`GameSession.cs`、`Dungeon.cs`、`SessionSnapshot.cs`、`FormationPlans.cs`、`HeroExperience.cs` 的必要接入点及对应UI绘制/插值；config/skill、combat、必要的召唤专用功能目录CSV与字段字典。最终文件清单须在此切片批准时缩小，不默认全改。

- B1先做效果来源：唯一Buff列表内表达独立效果与来源/组策略，支持按单项声明的刷新/择强/有限层；补范围出入和弱来源回退，不另建Aura/Curse状态账本。
- 统一Heal Effect、友军/自身/无敌人时施法资格；把持久光环维持与重复Windup分离，明确卸槽、主人死亡、房间切换和离场撤销。
- Forced接入已有锁，按安全边界覆盖/恢复；普通容量预约不变成实体碰撞或仇恨表。
- B2独立召唤必须先通过实体ID/阵营/容量候选及进房检查点方案；保留现有英雄经验规则，不让召唤最后一击漏发/重复发奖。不能仅改Heroes列表或把SpawnEntity变成装饰宠物。
- 召唤属性继承清单、快照时点、数/HP跨房、主人死亡判负、卸槽/换队/全灭清理未定时，限制在隔离Core测试，禁止生产召唤存档。
- 导引箭明确复用arc_homing的失效重选或采用独立已批准策略，不自行改变所有旧追踪弹。
- 测试：BD-01～05/13～17、Forced恢复、独立诅咒、物理易伤资格、光环进出/来源死亡、两种召唤受击/容量/奖励、跨房HP/数量与重开。双圣骑士场景使用测试注入验证多来源，不开放生产重复职业。
- 清理提案：批准替代后才移除CastTalent共用Buff别名及对应硬编码；旧stable ID需明确迁移。计时SpawnEntity、现有回归技能、战斗换入CD不得顺手删除。

## C：新防御、复合伤害、尸体与链式技能

代表：BAR_WHIRLWIND、PAL_CONVICTION、SOR_CHAIN_LIGHTNING、NEC_LOWER_RESIST、NEC_CORPSE_EXPLOSION、RAN_VALKYRIE。

拟改位置：`DamagePipeline.cs`、`CombatConfiguration.cs`、`FightAttributes.cs`、`SkillRuntime.cs`、`AbilityRuntime.cs`、`SkillBattleState.cs`、`SkillSnapshot.cs`、`SessionSnapshot.cs`、`Dungeon.cs` 及对应 CSV/校验/文档/表现。

- 防御专项可提前于A/B执行，但必须先批准完整公式、K曲线、抗性截断、混合分量与旧倍率归属；在唯一DamagePipeline内迁移，不设置新旧公式生产双跑开关。
- 同一DamageInstance的混合分量各自防御，命中/暴击/格挡、一次HP应用/击杀的共享与时序需先批准；禁止拆成两次独立致死奖励来伪造混合伤害。
- 尸体token挂现有房间死亡事实，Commit原子竞争消费；奖励资格、Boss尸体、复活怪是否产尸/产奖、失败CD、清房后还能否消费都需明确。
- 新持续旋风迁移现有一次AoE行为要保留ID或提供另ID方案；连锁闪电增加有界链状态和明确重击资格，复用唯一Pending。
- 达预算时保留已提交伤害/奖励/清理事实；未提交派生的延期/拒绝/清房取消政策先审批。技术深度上限不替代技能链规则。
- 测试：BD-06～12/18～22及负抗、攻击者等级快照、物理/火混合、竞争尸体消费、同Tick双致死、链根/分支预算、长挂机性能和收益防重。
- 清理提案：公式批准迁移后替换减法防御与旧聚合实现、同步手算例和旧测试期望；保留实际有意义的兼容读取，不留下可调用旧伤害旁路。

## 执行与验收

开发授权后按实际项目运行 `dotnet run --project tests/IdleCrpg.Checks.csproj`、`dotnet build Idle-CRPG/Idle-CRPG.csproj`；UI/表现变化另执行Godot烟测及Mini视觉验证。只使用artifacts/qa隔离存档和配置副本。每切片记录PASS/FAIL/NOT RUN；当前本轮全部NOT RUN。禁止主动commit/push/merge。
