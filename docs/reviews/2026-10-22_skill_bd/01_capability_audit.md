# 技能 BD 交接包 G0：实际能力图谱

本文保留G0初审时点的能力与操作记录；后续用户已批准文档合并和旧配置测试归档，最新执行状态见[裁决记录](04_approval_requests.md)及[技术计划](../../technical/skill_bd_plan.md)。下文“本轮仅新增四份报告”描述初审阶段，不代表后续归档未发生。

日期：2026-10-22。状态：只读源码审计完成，未授权实施。来源：`docs/temp/D2_Skill_BD_Interaction_V1_0_HandOff/` 的 README、00～05、首轮指令及三份示例数据。整合版为分册汇编；以下以分册编号定位需求。

本轮仅新增本目录四份审计报告；未修改工程、配置、资源、存档、已有测试或正式设计文档。上轮远程退让代码属于本轮开始前的状态，不算本轮新增实现。未执行构建、测试、Godot 或玩家存档操作，因此不产生本轮构建/测试缓存；本报告中的“存在”是源码证据，不表示本轮运行验证 PASS。

## 真实工程边界

- `Idle-CRPG/Idle-CRPG.csproj`：Godot.NET.Sdk/4.7.2、net8.0；纯逻辑在 `Scripts/Core`，UI 在 `Scripts/UI`。
- `tests/IdleCrpg.Checks.csproj`：net8.0 控制台回归项目，通过 Compile Include 编译同一份 Core；实际验收入口为 `dotnet run --project tests/IdleCrpg.Checks.csproj`。不是包内假设的 Game.Core / Game.Godot / Game.Core.Tests 三项目，也不是测试发现器驱动的单测工程。不能把 `dotnet test` 无用例成功退出当回归通过。
- 当前索引为 `docs/README.md`，开发约束为根目录 `AGENTS.md`。文件枚举未发现包内假设的 CLAUDE.md、docs/00_meta/DOCUMENT_INDEX.md、IMPLEMENTATION_STATUS.md；不新造对应文件替换现有索引。
- `.git/HEAD` 指向 `refs/heads/main`。当前 PATH 无可用 git 命令，常见安装目录探测未找到；Git status/diff 未取得，工作区是否干净未验证，不能将既有修改归属于本任务。未提交、推送、清理或重置。

## G01～G06

源码路径均相对 `Idle-CRPG/Scripts/Core/`。

| 需求 | 状态与证据 | 复用方式与缺口 |
|---|---|---|
| G01 属性与增伤聚合 | 部分。`FightAttributes.cs/Aggregate` 已支持 base_flat_pct、add、add_clamp、multiply；`DamagePipeline.cs:45–54` 有唯一公式 | 基础聚合可复用；护甲、抗性、倍率/易伤聚合与新包有冲突，见冲突报告。现行通用易伤 Buff 读取未限定物理类型 |
| G02 来源与生命周期 | 部分。`SkillBattleState.cs/SkillBuffState` 有 Instance、Owner、Target、Ability；`SkillRuntime.cs:165/ApplyBuff` 支持 NonStacking、RefreshDuration，到期/死亡清除。贡献按有效实例查询，无额外订阅总线 | 同目标同 ID 只有一个实例，刷新重启周期；不支持保留多来源择强及弱来源回退、StackGroup、范围进出撤销。`Talents.cs:153` 所有 shout 共用 talent_battle_shout，所有 weaken 共用 talent_weaken，不同职业技能会互相覆盖 |
| G03 多段 Hit / Proc | 部分。`AbilityRuntime.cs/AdvanceFlights` 有逐弹、逐目标扫掠及地面周期；`SkillBattleState` 有 Flight.Id、Hit、Depth、Attribution、PrimaryFirst；Trigger 有 primary_cast 过滤 | 不能把 Flight ID 当完整 Cast/OriginChain ID。缺少每 Cast/Target 上限、ICD、明确的直接/周期/派生来源资格。不同路径事件不统一：普通 Damage 调 React，HitAbility 以 events=false 调伤害并单独处理首发暴击。DoT 继承原暴击、仍逐次闪避/格挡是现行已确认行为，不是空白待定 |
| G04 独立召唤 | 不存在完整能力。`AbilityRuntime.cs:81/SpawnGeneric` 的 SpawnEntity 只是 Flights 中的有限寿命对象，到期派生效果 | 无召唤 Actor、主人、独立 HP/AI/容量、跨房保留。`SkillRuntime.cs:29–32` CombatId 与阵营以 Heroes.Count 分界；`CombatTargeting.cs` 候选为英雄掩码，不能直接塞召唤物。现有 SpawnEntity 不应删除，也不能冒充新需求 |
| G05 唯一死亡/尸体消费 | 奖励部分存在。`SkillRuntime.cs:76/SettleDamage` 拒绝死亡目标再次受伤，扣血后统一发经验/待拾取掉落并释放锁；`HeroExperience.cs/GainMonsterExperience` 发给全部上阵者含倒地成员 | 无独立 DeathConfirmed 记录或尸体 token，无 ConsumeCorpse、竞争消费、奖励资格。房间 Monster 列表中的尸体和 Godot 表现都不能直接视为可消费资源。重开房间允许再次获得击杀经验是既有明确规则 |
| G06 有界队列 | 部分。`SkillRuntime.cs:151/Enqueue`、DrainEffects，`SkillConfiguration.cs/ValidateGraph`；现有预算64条/步、积压1024、深度4，超步预算留到下步，超积压/深度报错 | 可复用唯一 Pending 队列及固定顺序，不能直接当完整有界连锁授权。缺少根源关联、分支预算、Proc 再触发授权；清房 ResetSkillRoom 会清队列，须明确未提交派生效果的收尾策略。现有数值是原型，不能被包内 TBD 自动抹掉 |

## 动作、AI、存档

| 能力 | 真实状态 |
|---|---|
| 动作与自动施法 | `SkillRuntime.cs/RunActiveSkill/Commit`：按装配顺序选择、前摇/Commit/恢复，Commit 开 CD；普攻沿既有 Timer，无新增前后摇。所有主动当前需要敌方目标，不直接支持无敌人时召唤或友军治疗 |
| 入场与容量 | `CombatTargeting.cs`：0.8秒警觉、按编队散开、正常/溢出/远程稳定锁、0容量可受攻击、溢出预算重评。无 Forced 嘲讽层 |
| 远程退让 | `RangedRetreat.cs/RunRetreat`、`SkillRuntime.cs:91–95`、`GameSession.cs/Act` 已实现。1.2距离/4秒 CD 可配；前摇恢复后移动；入场排除；不主动改锁或容量。以 ranged 与 depth==0 识别伤害资格，不是完整的未来 Proc 来源分类 |
| 真实二维弹道 | `AbilityRuntime.cs/SegmentDistance` 使用 System.Numerics.Vector2 线段扫掠，有直线、贯穿、弧形追踪、范围/地面效果；不是包内提到的一维逻辑假设 |
| 房间重开 | `SessionSnapshot.cs:96/CaptureProgress` 清 SkillState，回到进房节点；完整 Capture 保留内部战斗现场。`SkillSnapshot.cs` 校验引用和数量。跨房召唤若放 SkillState 会被清掉，必须单独评审其检查点语义 |
| 队伍 | `HeroRoster.cs/PartyCapacity`、`FormationPlans.cs`：单人开局，星图最多5人，每职业最多一名；四人只是可达到的中间规模；候补与上阵成长独立 |
| 初始技能与槽 | `StartingSkills.cs/GrantStarterSkill` 消耗1级那1点，可洗掉且不重复补学；主动1槽、星图逐步到5槽。点数当前按等级增长，没有独立点数封顶；节点学习上限由各行 max_rank 配置（当前常见20、部分10、个别1） |
| 装备 | 尚无正式武器装备/武器类别门禁。不能把游侠“必须装备弓才可施放”报告为已支持 |

## 17个候选技能映射

草案 ID 仅用于映射，不直接注册成第二组生产 ID。

| 草案 | 现有对应或扩展点 | 缺口/冲突 |
|---|---|---|
| BAR_BASH | `barb_bash`，CastTalent bash | 基本单体物理行为已存在；名称“重击/猛击”不构成重建技能理由 |
| PAL_SMITE | `paladin_smite`，当前显示“至圣斩”，bash | 基础单体可复用；盾击命名、装备条件、附加控制需明确，不自动改初始技能 |
| SOR_FIREBALL | `mage_fireball`，shot+Fire+radius | 已有投射物命中指定目标后范围火伤；没有群体收益评分 AI；保留现有初始身份 |
| RAN_MULTISHOT | EmitAbility 多弹道、generic 扫掠 | 没有该职业技能；需同 Cast 来源、扇形方向、同目标命中规则和弓箭门禁决策；不能以单体 ranger_shot 替代 |
| BAR_TAUNT | 普通锁/释放与安全重评 | 无 Forced 效果与恢复规则，须扩展现有锁敌，不另造仇恨系统 |
| PAL_PRAYER | Buff 周期调度；现有仅自身增益施放回血 | 无友军 Heal Effect、持续光环和范围生命周期；不能用自疗5%充当祈祷 |
| PAL_FANATICISM | attack_speed_pct 与 Buff 贡献 | `paladin_zeal` 实际是临时攻击 shout，不等于持续攻速光环；需独立效果来源与范围 |
| SOR_FROST_NOVA | `mage_frost_nova` Cold nova、buff_chill | 当前 nova 以敌方目标为圆心、无减速；草案是自身近敌防御触发+减速，不能直接宣称完成 |
| NEC_RAISE_SKELETON | Actor、AI、DMG、容量可供扩展 | 独立召唤、无敌人时释放、数量与跨房状态全部缺失 |
| NEC_AMPLIFY_DAMAGE | vulnerable_damage_pct / DamageBucket.Vulnerable | 尚无该诅咒；现有易伤读取不限物理，不同现有诅咒又共用 weaken；必须扩展资格及独立来源 |
| RAN_GUIDED_ARROW | arc_homing、有限失效目标重选 | 没有该职业技能；现有追踪目标死亡会有限重选，但 ranger shot 固定方向且只检查指定目标，需批准使用哪条已存在弹道合同 |
| BAR_WHIRLWIND | `barb_whirlwind` | 目前 Commit 一次自身范围伤害，无持续移动/多段窗口；名称相同不等于新行为已支持 |
| PAL_CONVICTION | Buff/属性聚合扩展点 | 无元素抗性与持续光环；被防御公式决策阻塞 |
| SOR_CHAIN_LIGHTNING | 队列、逐目标伤害 | 无链状态/跳数/重复命中资格；追踪重选不是连锁闪电 |
| NEC_LOWER_RESIST | Buff 生命周期 | 无四元素抗性、减抗解析与该诅咒；同目标独立共存需扩展 |
| NEC_CORPSE_EXPLOSION | 唯一击杀入口、范围伤害 | 无尸体 token/竞争消费/混合分量伤害；失败扣 CD 也未定 |
| RAN_VALKYRIE | 与骷髅共用未来实体扩展 | 无独立召唤能力；不改变游侠本人弓箭方向 |

## 验证状态

本轮 dotnet build、核心回归、dotnet test、Godot/Mini 验收全部 NOT RUN（G0只读）。现有检查可定位于 CombatSkillChecks、CombatTargetingChecks、TalentChecks、ClassFollowingChecks、FormationChecks、ProgressChecks、HeroExperienceChecks；它们不能替代包内尚未实现的 BD-01～22 验收。
