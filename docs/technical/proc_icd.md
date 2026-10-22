# Proc ICD V1.0：审计与实现

## 裁决及配置合同

按本轮用户《Proc ICD 开发裁决 V1.0》实施。ICD归属为既有Proc效果拥有者 `work.Owner + TriggerID`，不与目标或根源施法绑定。同拥有者的多段命中、连锁、不同根源施法共享同TriggerID的ICD；不同拥有者、不同TriggerID独立。

新增可选 `config/skill/proc_icd.csv`：

| 字段 | 含义与校验 |
|---|---|
| id | 已存在的TriggerID，唯一，不允许悬空引用 |
| icd_seconds | 非负有限秒数；缺文件、缺行或0不启用；按ceil(秒数×20)量化，量化结果不得超过int.MaxValue |

不是全局0.75秒。正式表目前仅表头：上轮已删除M7的两个首批触发器，不为本轮重新开放旧技能。`tests/fixtures/proc_icd_v1/`显式组合旧回归基线，以`tr_crit_shorten=0.75`、`tr_skill_mirror=0.75`验证，缩冷另配置`max_per_cast=1`、`max_per_target=0`。正式数值留待综合调平。

审计确认原TriggerDefinition没有概率字段。为完整表达“概率失败不消耗ICD”，在原trigger.csv末尾允许可选`proc_chance`列（0～1，空值或旧五列表头缺省1）。只有0<概率<1时使用既有combatRandom；缺省1不新增随机消耗，不改变旧技能概率或效果数值。未新增全局概率或另一个触发管理器。

## 原系统审计

- `SkillRuntime.React`依据Buff事件、primary_cast/direct来源过滤，向既有Pending队列排入效果；原ProcAllowed在入队时即计数，无法辨别最终效果是否执行。
- `DrainEffects`按稳定队列顺序、EffectBudget执行。同Tick多命中/连锁可先排入多个Proc，不能仅在React检查ICD，否则队列中的多项会穿透同Tick限制。
- `Fight → AdvanceSkills`为战斗模拟推进，正常固定0.05秒；界面暂停不调用，倍速增加固定步数。ICD不读取墙钟或表现帧时间。
- `BuffEffect.AbilityCooldown`可能没有冷却候选，也可能候选已处于保底；这些情况不产生实际冷却变化。
- `EmitAbility`镜像仅取首个Ability Effect，但仍生成该Effect声明的全部Count，并非固定一个镜像。镜像是Mirrored弹体，不是独立召唤Actor。

## 执行及成功判定

React保持事件与来源过滤，并在原SkillEffectWork携带TriggerID。真正出队时重新检查拥有者、来源Buff、ICD、根源施法/目标限次和已有弹体总预算；再检查合法目标和概率。未通过则直接丢弃该工作项，不预占ICD或计数。

复用原效果实现，在真正改变状态的节点记录成功标记：HP减少或护盾吸收、实际治疗、Buff成功施加/刷新、光环首次激活、召唤创建及弹体创建、实际冷却缩短。没有合法目标、零伤害、未治疗、NonStacking拒绝、失效来源、冷却无候选/保底、召唤/弹体满额均不成功。没有改动伤害公式、技能效果数值、职业技能或真实多段伤害数量。

成功后才写入ICD的ReadyTick及既有ProcFires。0.75秒成功于Tick T后，T+15起再次允许触发。同Tick后续工作项会看见前项成功后的ICD；前项失败不阻断后项。原ProcLimit仍以OriginCast/TriggerID/Target计算，不改根源身份，不用ICD代替限次。概率缺省1或ICD未配置时保留原有无ICD执行路径。

## 生命周期及存档

ProcTick和ProcIcds位于现有SkillBattleState，ResetSkillRoom在换房、换层、清房及房间重建时清空；正式天赋换槽不重置全房状态，不能通过换槽绕过ICD。完整内部Capture可保存、校验及确定性续跑，用于QA；正式CaptureProgress始终创建空SkillBattleState，不写长期进度。旧内部快照缺字段时默认空ICD，正式存档版本不变。

## D4补充裁决：沿用现有镜像预算与生命周期

现有约束只有全局`max_flights=256`，以及各源弹体既有Life/Delay/Hold；生成同刻镜像复制首个Effect的全部Count。不存在独立镜像数量上限，也不存在包含等待/悬停的统一总生命周期硬上限。

用户D4补充裁决已明确：本轮沿用现有全局弹体预算与原弹体生命周期，不新增镜像专属数量或生命周期上限；保留镜像不可递归生成镜像。ICD之外不调整镜像数量、伤害、生成频率或其他原型数值。满全局预算的Proc现在先预检、跳过，不创建部分弹体、不抛预算异常或占用ICD；其他正常施法预算语义不变。专项验证现有镜像按源弹体生命周期消失，不把它宣称为新增的硬上限。

## 验证入口

- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --proc-icd-checks`
- 完整Core Checks包含同一专项，同时保留原C3每施法/每目标限次回归。
- 专项覆盖同Tick队列和连锁、15Tick边界、拥有者/TriggerID隔离、每施法及每目标限次叠加、失败后同Tick重试、概率失败、非法目标、零效果、冷却保底、召唤/弹体满额、镜像过期、换房/换层/重建清理、完整快照续跑、旧字段兼容、正式检查点清空以及配置非法值拒绝。
- 自动检查仅写artifacts/qa，不写玩家进度或正式技能配置。

2026-10-22验证结果：工程编译0警告/0错误；完整Core Checks通过；--proc-icd-checks输出PROC_ICD_OK；既有--skill-bd-proc-checks通过；Godot --smoke-test输出全部UI专项标记及SMOKE_OK。日志位于忽略的artifacts/qa/proc-icd-final-smoke.log。未执行Git提交或推送。后续D4裁决已关闭原约束冲突：明确沿用现有全局预算、原生命周期与禁止镜像递归，不新增专属上限。

D4补充专项结果：90个模拟Tick内连续注入360次施法，0.75秒ICD成功产生6批镜像且每步均不超过全局预算；仅在QA关闭ICD后验证一次根源施法始终只产生一批镜像，避免ICD掩盖递归错误；按实际全局max_flights填满现场时镜像不能生成且不占ICD，释放一个名额后可立即共用该名额；停止施法后所有弹体按原机制自然销毁，带有存活镜像换房后弹体与ICD均清空。输出MIRROR_D4_OK，无需改动生产运行逻辑或配置数值。

D4最终复验通过：--proc-icd-checks输出MIRROR_D4_OK及PROC_ICD_OK；完整Core Checks通过；工程编译0警告/0错误；Godot冒烟SMOKE_OK，日志artifacts/qa/proc-icd-d4-smoke.log。此次仅补充测试与裁决记录，生产运行逻辑及配置数值未改动。
