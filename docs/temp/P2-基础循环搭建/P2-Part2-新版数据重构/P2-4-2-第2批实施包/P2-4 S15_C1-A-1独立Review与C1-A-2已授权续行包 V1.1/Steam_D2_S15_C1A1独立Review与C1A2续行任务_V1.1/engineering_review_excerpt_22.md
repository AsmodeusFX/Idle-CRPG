# 工程唯一Review 第22节（引用副本，勿在此修改）

## 22. S15-C1-A-1 实施：四职业16固定技能＋4职业特性T（2026-10-26）

### 22.1 授权与边界

用户指令（原文）：“**全部同意，批准C1-A实施**”；授权标记 `IMPLEMENT_APPROVED_SCOPE`，来源 `docs/temp/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/`（含 00 批准范围、01 五项裁决、02 转发指令、03 分阶段验收、04 权责、C1A核心输入/01–03）。授权覆盖 C1-A-1→C1-A-2 的阶段转换，不覆盖 C1-B/C2/C3、Git 发布、真实玩家存档、P0 拓扑与 Lmin 开放；D-G3 只允许隔离观察、S02/P03 继续暂缓。

### 22.2 设计ID → 生产ID 显式映射（16/16）

| 设计 | 槽 | 生产技能ID | 行为承载 | 判定 |
|---|---|---|---|---|
| BAR-S1 重击 | warrior S1 | `barb_bash` | 单体近战 Damage | 复用 |
| BAR-S2 旋风斩 | warrior S2 | `barb_whirlwind_window` | AreaPulse 周期（原S3移入） | 复用+重绑 |
| BAR-S3 战吼 | warrior S3 | `barb_taunt` | 范围嘲讽 `forced`（原S2移入） | 复用+重绑 |
| BAR-S4 先祖援军 | warrior S4 | `barb_ancestor`（新） | Summon 上限1/限时8s | 新增配置 |
| RAN-S1 弓箭射击 | ranger S1 | `ran_shot`（新） | 单发 SpawnProjectile | 新增配置 |
| RAN-S2 多重箭 | ranger S2 | `ran_multishot` | 3弹道扇面（原S1移入） | 复用+重绑 |
| RAN-S3 导引箭 | ranger S3 | `ran_guided_arrow` | AbilityCast arc_homing（原S2移入） | 复用+重绑 |
| RAN-S4 女武神 | ranger S4 | `ran_valkyrie` | Summon（原S3移入） | 复用+重绑 |
| SOR-S1 火球 | mage S1 | `mage_fireball` | 投射物+火焰伤害 | 复用 |
| SOR-S2 冰霜新星 | mage S2 | `sor_frost_nova`（新） | AreaPulse Cold + `chill` | 新增配置 |
| SOR-S3 连锁闪电 | mage S3 | `sor_chain_lightning` | Chain 4目标（原S2移入） | 复用+重绑 |
| SOR-S4 陨星术 | mage S4 | `sor_meteor`（新） | 2s前摇 + AreaPulse Fire | 新增配置（近似） |
| PAL-S1 圣击 | priest S1 | `paladin_smite` | 单体近战 | 复用 |
| PAL-S2 盾击 | priest S2 | `pal_shield_bash`（新） | Damage + `stun` | 新增配置 |
| PAL-S3 祈祷 | priest S3 | `pal_prayer`（改） | 主动 Heal 脉冲（`ally_lowest_hp`） | 语义修正 |
| PAL-S4 祝福之锤 | priest S4 | `pal_hammer`（新） | 绕体 AreaPulse Physical | 新增配置（近似） |

职业特性T：`warrior_trait`（攻击被动，保留原ID）、`ranger_trait`/`mage_trait`（暴击率被动，原型值）、`priest_trait`（改为 **守护光环** `pal_guard_aura`：MaintainAura 周期治疗）、`pal_trait_zeal`（新，**进攻光环** `pal_zeal_aura`：攻速+20%），两者 `exclusive_group=pal_trait_branch` 互斥且同一时刻只维持一条；T 光环不进入 `SkillIds`、不占 S1–S4、不参与自动施法队列。

### 22.3 实际改动

**配置（`Idle-CRPG/config/skill/`）**
- `skill_slot.csv` 重写为四职业固定顺位；`hero_starting_skill.csv` 游侠改 `ran_shot`。
- `skill.csv`：新增 6 条主动 + 2 条T光环；`pal_prayer` 改为 `pal_prayer_heal`（`ally_lowest_hp`）。
- `effect.csv`：新增 9 条（单发弹道/范围冰爆/减速Buff/陨星/盾击/眩晕/飞锤/守护与狂热光环）。
- `buff.csv`：新增 `sor_frost_nova_buff`(chill 35%/60t)、`pal_shield_bash_stun_buff`(stun/30t)、`pal_guard_buff`(周期治疗)、`pal_zeal_buff`(攻速+20%)。
- `summon.csv`：新增 `barb_ancestor`（上限1、HP/ATK 0.6/0.5、限时8s）。
- `talent.csv`：新增 7 节点（6 个固定技能节点 + `pal_trait_zeal`），`priest_trait` 改为光环主动，圣骑士T拆分为守护/进攻。

**引擎（`Scripts/Core/`）**
- `Talents.cs`：`Tree` 允许 0–3（第4棵树承载 C1-A 新增节点）；职业特性T允许绑定**不同名**的光环技能（仅 `ClassTrait`，仍校验技能存在）；`ClassTraitUnlocked` 增加过渡规则——该职业已投放资格节点时必须购买，尚未投放时保持可学（避免“有T节点永远不可学”的死内容）。
- `FixedSkills.cs`：新增 `TalentAuraSkills`/`IsTalentAura`；`SyncFixedSkills` 维持T光环；`MigrateFixedSkills` 升到 **FixedSkillsVersion=2**，对 v1 旧档按新槽位重排队列并把不再属于 S1–S4 的旧主动转入退休隔离（免费返还点数，不销毁记录），迁移不清除T光环。
- `SessionSnapshot.cs`：写入 `FixedSkillsVersion=2`；激活光环校验允许T光环（不要求在 `SkillIds` 内）；T投资资格校验与过渡规则对齐。
- `SkillSupportRuntime.cs`/`SkillRuntime.cs`/`GameSession.cs`：光环维持与索敌遍历对T光环、非主动引用做防御性跳过。
- `SkillPreview.cs`：试场按节点绑定的真实技能ID装配（T节点 id≠光环技能 id）。
- `SkillSnapshot.cs`：光环校验异常补充 buff/owner/aura/三个子条件，便于定位。

**工具与测试**
- 新增 `tools/Apply-S15C1A1.ps1`（可重复执行的配置落地脚本，已存在行不重复追加）。
- 新增 `tests/C1ASkillChecks.cs` + `--c1a-checks`：16槽映射、逐技能400tick真实行为、未学不出手、同职实例隔离、局部洗点保底、T互斥/光环撤销、v2读档、v1槽位重排迁移。
- 旧断言按规则变化更新（均保留其它保护）：`TalentSmoke`（S2→旋风、T资格过渡）、`SkillPreviewChecks`（节点73/主动47、T绑定技能）、`FixedSkillClosureChecks`（退休25、多来源光环改用守护T、召唤区域4）、`SkillMigrationChecks`（初始技能 ran_shot、T绑定豁免、祈祷改脉冲）、`ClassFollowingChecks`（tree≤3）、`Wp4TalentMechanicsChecks`（四职业20槽、T节点≥1）、`Wp4StarChecks`（资格过渡）;旧技能基线夹具补 `skill_slot.csv`，使 2026-10-22 旧组合自洽。

### 22.4 自主技术方案与被覆盖旧内容

1. **槽位重绑优先于新代码**：S2/S3/S4 轮换用 `skill_slot.csv` 完成，存档按 `skill_id` 而非槽号，故重绑不迁移存档；旧档仅需 `FixedSkillsVersion` 1→2 重排。
2. **祈祷语义修正**：设计明确“主动治疗脉冲、非持续Aura本体”，故 `pal_prayer` 由光环改为 Heal（目标=最缺血友军）；持续恢复移交T守护光环，避免两套常驻光环。
3. **近似项（已在验收中标注）**：陨星术用“2秒前摇+命中点范围爆发”近似“固定落点”（无地面预警系统）；祝福之锤用“自身周围径向脉冲”近似“绕体飞锤”；旋风斩沿用周期脉冲，不实现“施放中位移”。三者均不新增未授权系统。
4. **T资格过渡规则**：未新增星图资格节点（C1-B范围），改为“未投放即可学、已投放须购买”。野蛮人仍被 `s_trait_warrior` 严格门禁。该取舍需设计Owner确认是否接受为 C1-A 期间临时口径。
5. **未复活旧机制**：`CastTalent`、旧装配、技能槽付费、退休节点仍不可用；旧光环 `pal_fanaticism/pal_conviction` 退出固定槽并进入退休隔离。

### 22.5 验证（工程自检）

| 入口 | 命令 | 结果 |
|---|---|---|
| 两工程构建 | `dotnet build Idle-CRPG/Idle-CRPG.csproj` + tests 工程 | 0 警告 0 错误 |
| C1-A专项 | `dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1a-checks` | 退出0，`C1A_SKILL_PASS`（16/16槽位与真实行为、未学不出手、T互斥/实例独立/洗点撤销、v2读档与v1槽位重排迁移） |
| 全量Core | `dotnet run --project tests/IdleCrpg.Checks.csproj` | 退出0，`FIXED_SKILLS_PASS`(25退休)/`LEGACY_SELECTION_PASS`/`WP4_HISTORICAL_TALENT_PASS`/`WP4_STAR_PASS`/`WP5/6/7`/`INT_01_08_PASS` |
| Godot无头 | `--headless --path Idle-CRPG -- --smoke-test` | 退出0，12段UI冒烟 + `SMOKE_OK` |
| 真实窗口UI | `--path Idle-CRPG -- --first-batch-smoke` | 退出0，`FIRST_BATCH_UI_PASS`（1440×900/Windows） |

**真机证据**（`artifacts/qa/first-batch-ui-20261026-080743-0327463-53f517bbb43842bf9bb7c533e6a17d20/`）：`fixed-skills-s3-unlearned-t-locked.png`（S1猛击/S2持续旋风/S3战吼/S4先祖援军 四卡 + T“星图锁定”）、`fixed-skills-s3-learned.png`（真实点击学会S3战吼、自动施放、Rank 1/10）、`fixed-skills-local-reset.png`（免费重置S3、S1保底）、`four-same-class-buff-sources-and-cooldowns.png`（四同职守护光环四来源）。

### 22.6 C1-A-2 状态：**OPEN（未实施）**

98 节点本轮**未落地**，不宣称完成。已确认的两项工程前置缺口（来自 §21 只读对账并在本轮再次核实）：

1. **技能级修饰器缺失**：设计 S2xx/S3xx/S4xx 的“提高某技能伤害/范围/时长/目标数”不能复用现有 `passive`（白名单仅 atk/armor/crit_rate/engagement_capacity，且 `passive` 禁止绑定 `skill_id`）。需要新增 `kind=modifier`（绑定目标技能、在 `Commit` 应用系数/范围/命中数）或等价方案。
2. **触发器可由T光环承载，但缺少“按已学天赋注册触发器”的通用入口**：`React()`/`proc_limit`/`proc_icd` 运行时已完整存在（本轮核实，见 `SkillRuntime.cs:243-283`），`trigger.csv`/`proc_*` 仅缺配置；29 个 NEW_EFFECT 需要“天赋→常驻触发源”的注册路径与目标评分（精英/威胁）扩展。

**逐节点状态与所需Effect**：沿用 `artifacts/qa/s15-c0-20261026-131223/effect_coverage_engine_complete.csv`（154行，含每行所需能力、消费者入口、缺口、代表测试、状态）；本轮未改写其判定，也未把总分类强行指派给具体节点（遵守03号门禁第2条）。

**建议的下一步（需用户确认是否继续同一授权内推进）**：先做 `modifier` + 触发源注册两项运行时扩展（各配 1 组单测），再按 BAR→RAN→SOR→PAL 分批落 98 节点，每批跑"前置/互斥/点数守恒/读档/局部洗点"四类断言，最后跑 S07 八套24点构筑与四同职/四职业各一/跨Run隔离。

### 22.7 三方状态

- **E（工程自检）：C1-A-1 = PASS；C1-A-2 = OPEN**。16/16 技能与 4/4 T 均有固定Seed真实行为与真机UI证据；98 节点未实施，不计入通过。
- **V-Design：未复核**。需重点裁决：①T资格过渡规则（未投放即学）是否接受；②陨星/祝福之锤/旋风三项近似是否满足身份要求；③C1-A-2 是否继续按上述两步扩展推进。
- **V-User：未验收**。C1-A-1 可实机体验（四职业技能卡与T），不等于首轮10小时体验通过。
