# 技能 BD 正式迁移验收报告（供设计AI确认）

日期：2026-10-22。状态：**实施完成，Core/UI/存档/构建/冒烟与全部17个正式技能的临时可观察表现均通过；最终数值平衡按用户要求保持待定，Proc ICD语义待裁决。**

本报告用于设计AI核对“执行进度是否符合已批准方案”，不替代最终平衡签收，也不把Core QA或截图PASS等同于完整生产准出。来源与逐项记录见[执行记录](05_execution.md)、[迁移清单](06_migration_inventory.md)和[用户裁决](04_approval_requests.md)。

## 1. 结论摘要

| 结论 | 说明 |
|---|---|
| 正式五职业三系已落地 | `talent.csv` 共61节点：野蛮人14、游侠12、法师10、圣骑士12、死灵13；保留全部旧48个节点ID。 |
| 17技能迁移已完成 | 16个主动节点通过`skill_id`绑定同名`skill.csv`主动（13个新ID + 3个原ID改行为），`mage_frost_nova`按裁决保留旧行为；新数值均为原型。 |
| 初始技能已替换 | 游侠→`ran_multishot`，死灵→`nec_raise_skeleton`；战/法/圣沿用同ID绑定。 |
| 旧档可迁移 | `SkillTreeVersion` 0→1，同ID等级保留、合法重排投入延续、仅不兼容节点退款并清理引用、未知/越界/外职业ID拒绝、二次读档幂等。 |
| 验证通过 | 全量Core Checks 30个PASS段；工程编译0警告0错误；Godot headless冒烟全部子项PASS。 |
| 表现层已覆盖全部正式技能 | 17个正式技能均有专属可观察的**临时几何表现**（施法/命中/治疗/增益/召唤/连锁/尸爆/脉冲、持续光环/诅咒/旋风/嘲讽连线/尸体标记）；非最终美术。 |
| 旧内容保持退役 | 旧节点/旧技能不重新启用；旧配置快照归档于`tests/fixtures/legacy_skill_bd_2026_10_22/`（含`talent.csv`、`skill.csv`与SHA256）。M5的48个稳定ID不删除；M7的15个`skill_XX`已从正式`skill.csv`移除。 |
| 复核更正（2026-10-22） | 曾误以“未绑定`skill_id`”为M5退役判据删除23个正式主动节点，导致元素箭术、冰霜、白骨分支缺失；现已全部恢复，正式表回到61节点，前置边与`talent_battle_shout`/`talent_weaken`Buff一并恢复。详见[退役备案](08_retired_content.md)。 |
| 仍未完成 | 最终数值平衡（用户明确后续统一重算，保持待定）、Proc ICD语义裁决、多召唤数量扩展裁决。 |

## 2. 已完成项

### A. 配置与稳定ID

| 编号 | 项目 | 执行结果 | 证据 |
|---|---|---|---|
| A1 | 61节点三系布局 | 完成；五职业各3系，旧48 ID全部保留 | `talent.csv`；`--skill-migration-checks`“61-node three-tree layout” |
| A2 | `skill_id`绑定 | 16个主动节点与同名`skill.csv`主动一致；加载器校验同名、ActiveSkill、冷却Tick、距离；`effect=data`必须绑定 | `Talents.cs`；迁移检查“skill_id binding” |
| A3 | 新主动定义 | 13个新主动 + 3个原ID绑定行；`effect.csv`新增20行，仅复用既有Damage/ApplyBuff/MaintainAura/Heal/SpawnProjectile/AbilityCast/Chain/Summon/CorpseExplosion/AreaPulse | `skill/effect/buff/chain/summon/ability*.csv` |
| A4 | 旧节点行为 | 未绑定节点继续`CastTalent`与自动生成模板；`barb_whirlwind`、`mage_frost_nova`等旧ID未被改行为 | `SkillRuntime.Commit`分支 |
| A5 | 初始技能 | `warrior→barb_bash`、`ranger→ran_multishot`、`mage→mage_fireball`、`priest→paladin_smite`、`necromancer→nec_raise_skeleton` | `hero_starting_skill.csv`；迁移检查“初始技能映射” |
| A6 | 初始技能校验 | 绑定主动允许作为初始技能，仍要求首排、无前置、投入0；旧档`StartingSkillGranted`不重复补发 | `StartingSkills`/`Talents`；迁移检查“旧初始技能被重复补发” |

### B. 存档迁移

| 编号 | 规则 | 执行结果 |
|---|---|---|
| B1 | 版本字段 | `SessionSnapshot.SkillTreeVersion`：缺省0，当前写1；`SaveDocument`仍为4，不整体升版本 |
| B2 | 恢复顺序 | 战斗公式迁移→天赋树迁移→完整校验，避免旧档先被新布局拒绝 |
| B3 | 同ID保留 | `Talents`等级、`SkillIds`顺序、`SkillSwapLocks`剩余、`ActiveAuraSkills`、`TalentBonuses`只要合法即原样保留 |
| B4 | 重排投入守恒 | 合并树/移动行列后仍满足前置与系内投入的节点不退款；死灵灵魂系样例保留6个节点与系内投入6 |
| B5 | 不兼容退款 | 仅前置/系内投入/总预算不合法节点退款；同步清理`SkillIds`、`SkillSwapLocks`、`ActiveAuraSkills`、`TalentBonuses(node)`；按行高→列高→ID处理并写一条“天赋树迁移”日志 |
| B6 | 非法存档 | 未知节点ID、越界等级、外职业节点抛`InvalidDataException`，不静默删除 |
| B7 | 幂等 | 版本1二次读档不改变任何键值；已发放初始技能的旧档不补新初始技能 |
| B8 | 隔离 | 自动检查只使用`artifacts/qa`或内存编码，不加载或覆盖玩家`user://` |

### C. UI与表现

| 编号 | 项目 | 执行结果 |
|---|---|---|
| C1 | 三系页 | 五职业均显示3系、节点、前置线、投入与主动栏；`TalentSmoke`/`ClassTalentSmoke`通过 |
| C2 | 装配弹窗 | 战士主动列表7项；死灵列表含`召唤骷髅`等绑定主动；槽位仍为5，星图解锁规则不变 |
| C3 | 召唤绘制 | `session.Summons`纳入地牢绘制，以友方色与名称显示；不并入`Heroes`或怪物列表 |
| C4 | 锁定索引修复 | `MotionPresentation`不再把含召唤物的友方目标索引直接用于`Heroes`；修复召唤在场时的越界异常 |
| C5 | 截图入口 | 新增`--capture-bd-skill=<id>`；`PrepareTalentCapture`支持`--qa-class=<id>`；仅用于隔离QA截图 |
| C6 | 表现事件流 | `VisualFxRuntime`只读、有界（最多256条）、带序号与Tick；只记录表现事实，不持久化、不参与伤害/奖励/随机/锁定；UI按序号增量消费并按Tick过期 |
| C7 | 17技能临时表现 | 8类事件（Cast/Hit/Heal/Buff/Summon/Chain/CorpseExplosion/Pulse）+持续Buff/尸体/嘲讽锁敌绘制；全部用圆、弧、折线、文字等几何图元，无正式美术资源 |
| C8 | 表现隔离 | `CombatFxView`不写回任何Actor/Buff/弹道；换房、换层、换`GameSession`时清空本地事件缓存，不跨房补播历史 |

### D. 测试与文档

| 编号 | 项目 | 执行结果 |
|---|---|---|
| D1 | 新检查 | `tests/SkillMigrationChecks.cs`与`--skill-migration-checks`入口 |
| D2 | 既有检查同步 | `TalentChecks`、`ClassFollowingChecks`、`CombatSkillChecks`、`TalentSmoke`预期更新 |
| D3 | 文档 | `README`、`talent_trees`、`talent_config`、`skills_effects_buffs`、`combat_config`、`persistence_settings`、`skill_bd_plan`、`05_execution`、`06_migration_inventory`已同步 |

## 3. 验证证据

执行环境：Windows 10 Pro，.NET 8，Godot 4.7.2 stable Mono console。以下均为**实际执行结果**，未使用`dotnet test`成功退出代替。

### 3.1 全量Core Checks

命令：

```text
dotnet run --project tests/IdleCrpg.Checks.csproj
```

结果：**PASS，30个PASS段**，覆盖地牢/世界/城镇/存档/星图/公式/A/B1/B2/C1/C2/C3/天赋/61节点迁移/五职业初始技能/经验/容量等。关键新段：

```text
PASS: 61-node three-tree layout, skill_id binding, legacy starter retention,
      merged-tree save migration, incompatible-node refund/log cleanup,
      unknown-node rejection, idempotence, bound support and volley execution
```

### 3.2 专项与构建

```text
dotnet run --project tests/IdleCrpg.Checks.csproj -- --skill-migration-checks
→ PASS

dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore
→ 已成功生成，0个警告，0个错误
```

### 3.3 Godot headless冒烟

```text
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path Idle-CRPG -- --smoke-test
```

结果：**PASS**。关键标记：

```text
CLASS_TALENT_UI_SMOKE_OK
TALENT_UI_SMOKE_OK
INTERACTION_UI_SMOKE_OK
HERO_UI_SMOKE_OK
CLIENT_SMOKE_OK
SCENE_SMOKE_OK
HUD_SMOKE_OK
CHAPTER_UI_SMOKE_OK
ACT1_UI_SMOKE_OK
GM_SMOKE_OK
STAR_UI_SMOKE_OK
SMOKE_OK
```

### 3.4 图形Mini截图

截图全部输出到忽略的`artifacts/`，存档使用`artifacts/qa/<guid>`，不触碰玩家进度。

| 截图 | 入口 | 人工观察结果 |
|---|---|---|
| `preview-bd-a-fan-mini.png` | `--capture --capture-bd-a --qa-mini` | 5支QA扇形箭；`BD_A_VISUAL_MODEL_OK: 5 authoritative/drawn arrows; shared cast=1` |
| `preview-bd-skill-ran_multishot-mini.png` | `--capture --capture-bd-skill=ran_multishot --qa-mini` | 正式多重箭3支在途；`flights=3` |
| `preview-bd-skill-ran_guided_arrow-mini.png` | 同上，`ran_guided_arrow` | 导引箭在途可见；`flights=1` |
| `preview-bd-skill-mage_fireball-mini.png` | 同上，`mage_fireball` | 火焰弹道离手可见；`flights=1` |
| `preview-bd-skill-barb_bash-mini.png` | 同上，`barb_bash` | 近战命中斩击弧+伤害数字；怪物HP 9976 |
| `preview-bd-skill-barb_taunt-mini.png` | 同上，`barb_taunt` | 红色范围圈、敌人减益环、强制锁敌连线；`buffs=barb_taunt_buff@1; forced=1` |
| `preview-bd-skill-barb_whirlwind_window-mini.png` | 同上，`barb_whirlwind_window` | 自身旋风Buff环+旋转弧线 |
| `preview-bd-skill-paladin_smite-mini.png` | 同上，`paladin_smite` | 近战命中斩击弧+伤害数字；怪物HP 9981 |
| `preview-bd-skill-pal_prayer-mini.png` | 同上，`pal_prayer` | 绿色持续光环+治疗`+`与治疗数字 |
| `preview-bd-skill-pal_fanaticism-mini.png` | 同上，`pal_fanaticism` | 金色持续光环与友方增益环 |
| `preview-bd-skill-pal_conviction-mini.png` | 同上，`pal_conviction` | 紫色范围圈+敌人减抗环 |
| `preview-bd-skill-mage_frost_nova-mini.png` | 同上，`mage_frost_nova` | 蓝色范围爆发环；该节点按M3保留旧行为 |
| `preview-bd-skill-sor_chain_lightning-mini.png` | 同上，`sor_chain_lightning` | 锯齿状闪电跳跃连线 |
| `preview-bd-skill-nec_raise_skeleton-mini.png` | 同上，`nec_raise_skeleton` | 召唤环+友方骷髅（友方色小人+名称）；`summons=1` |
| `preview-bd-skill-nec_amplify_damage-mini.png` | 同上，`nec_amplify_damage` | 红色诅咒环+敌人易伤环 |
| `preview-bd-skill-nec_lower_resist-mini.png` | 同上，`nec_lower_resist` | 紫色诅咒环+敌人减抗环 |
| `preview-bd-skill-nec_corpse_explosion-mini.png` | 同上，`nec_corpse_explosion` | 地面尸体标记、绿色尸爆范围圈与“尸爆”文字；`cooldowns=nec_corpse_explosion` |
| `preview-bd-skill-ran_valkyrie-mini.png` | 同上，`ran_valkyrie` | 召唤环+友方女武神；`summons=1` |
| `preview-talents-{warrior,ranger,mage,priest,necromancer}.png` | `--capture --capture-talents --qa-class=<id> --qa-mini` | 五职业三系标题、节点、投入、主动栏可见 |
| `preview-talent-editor-{warrior,necromancer}.png` | `--capture --capture-talent-editor --qa-class=<id> --qa-mini` | 战士7项主动；死灵主动列表含召唤骷髅 |

说明：A扇形使用`tests/fixtures/skill_bd_slice_a`的TEST_ONLY夹具，不是正式树；正式技能截图使用`config/skill`中的正式定义。17个正式技能截图均在2026-10-22本轮重跑，输出`BD_SKILL_VISUAL_OK`与`CAPTURE_OK`。所有表现均为**临时几何图元**，不能作为最终美术签收。

## 4. 状态矩阵

| 项目 | 状态 | 说明 |
|---|---|---|
| 配置/稳定ID/三系布局 | PASS | 61节点，旧48 ID保留 |
| 16绑定主动执行 | PASS | Core QA + 专项检查 |
| 初始技能替换 | PASS | 五职业映射与旧档不重复赠点 |
| 旧档迁移/退款/幂等 | PASS | 合并树、非法节点、二次读档覆盖 |
| UI三系/装配 | PASS | Godot smoke + 截图 |
| 构建 | PASS | 0警告0错误 |
| Godot冒烟 | PASS | `SMOKE_OK` |
| A扇形/多重箭/火球/导引箭视觉 | PASS | Mini截图已人工确认 |
| 骷髅/女武神召唤视觉 | PASS（占位） | 召唤环+友方色小人+名称，非最终美术 |
| 光环/连锁/尸爆/旋风脉冲/嘲讽表现 | PASS（临时几何） | 17技能逐一截图：范围圈、锯齿闪电、尸爆环与文字、旋风弧线、锁敌连线、治疗`+`、伤害数字 |
| 17个正式技能可观察性 | PASS（临时） | 每个技能都能在截图中看到专属视觉事实，而非只有Core结果 |
| 最终数值平衡 | 保持待定 | 用户明确后续统一重算所有数值；当前数值均为原型，不宣称平衡 |
| M5旧节点退役 | 退役备案 | 旧树/旧定义归档，不重新启用；批准布局沿用的稳定ID不属于退役对象 |
| M7旧`skill_XX` | 退役备案 | 15个导入技能不绑定、不赠予、无玩家入口；配置行保留仅作显式旧回归 |
| Proc ICD语义 | 待裁决 | 限次能力已实现但生产`proc_limit.csv`未配置任何行；`direct`过滤只防递归，无时间闸门 |
| 多召唤数量扩展 | 待裁决 | 当前保守单只原型 |

## 5. 待设计AI/用户裁决

### D1 最终数值平衡

**用户反馈：保持待定，后续由用户重算所有数值。**本轮不调整、不签收任何平衡结论。

所有新增技能系数、冷却、距离、召唤继承比例、连锁衰减、尸爆范围、旋风脉冲间隔均为**可调原型**。后续重算时仍需覆盖：

1. 五职业单体基准DPS与技能倍率区间；
2. 召唤物继承比例与上限；
3. 连锁衰减/跳距/目标数；
4. 光环与减抗的叠加上限；
5. 三难度与章节成长的对应关系。

建议按`docs/temp/P1_7_Integrated_Combat_QA_Balance_V1_0/04_数值调平顺序与问题归因.md`的阶段顺序执行，不在本报告内伪造平衡结论。

### D2 M5：旧节点退役

**用户反馈：旧节点保持退役状态，有备案即可。**执行口径如下：

- **退役对象**：旧树布局与被替换的旧节点定义。它们不再作为新内容来源，不重新启用。
- **不是退役对象**：批准布局中沿用的48个稳定ID。它们是新树的正式节点ID，继续参与学习、装配与旧档兼容。
- **备案**：`tests/fixtures/legacy_skill_bd_2026_10_22/config/skill/talent.csv`保存迁移前旧树快照；同目录`README.md`与`SHA256SUMS.txt`说明用途并固定哈希。
- **不物理删除**：稳定ID与旧档字段保留，避免已学内容、装配和旧存档失效。若未来要物理删除节点，必须另开独立版本号、完整ID映射/退款、迁移前备份与回滚方案（本轮未执行、未授权）。

### D3 M7：旧`skill_XX`退役

**用户反馈：旧技能保持退役状态，有备案即可。**执行口径如下：

- 15个`skill_XX`导入技能（幽魂骨矛、狂热战吼、追魂飞刃、刀锋之雨、死亡横扫、骸骨护甲、陨石火雨、冰霜漩涡、雷霆审判、末日陨星、杀戮狂热、震荡裂地波、暗影分身、灵魂收割、天启剑阵）不在任何正式天赋节点上绑定，不作为初始技能，不进入GM或玩家技能入口。
- 它们只被显式旧回归引用（`CombatSkillChecks`、`DamageFormulaChecks`、`CombatTargetingChecks`等）；生产玩家流程无法获得。
- **备案**：旧`skill.csv`、`effect.csv`等快照与SHA256保存在`tests/fixtures/legacy_skill_bd_2026_10_22/`，完整基线同时在`tests/fixtures/retired_skill_baseline_2026_10_22/skill/`。
- 15行已从正式`skill.csv`移除；旧回归通过`tests/LegacySkillConfig`的`LoadLegacy`路径叠加TEST_ONLY基线读取，正式加载器无此入口。M5的48个稳定节点ID不属于本项退役范围，已在复核中恢复。

### D4 Proc ICD语义（本轮需要用户裁决）

**Proc**（procedure）指由事件触发的副效果，本项目当前有两条：`OnCritical`触发冷却缩短（`tr_crit_shorten`）和`OnSkillCommit`触发镜像（`tr_skill_mirror`）。**ICD**（internal cooldown，内部冷却）是一段时间闸门：同一个触发在短时间内的多次合格事件里只允许生效一次。

**当前实现的问题**：

- `trigger.csv`只按`primary_cast`过滤来源，`proc_limit.csv`提供“每次根源施法最多N次 / 每目标最多N次”，但该文件目前只有表头、**没有任何限次行**，所以实际运行没有次数上限。
- 多段/多目标技能会让同一触发在一瞬间产生大量合格事件：多重箭3箭、连锁闪电多跳、光环脉冲每20 Tick、尸爆范围命中、多个英雄同时暴击。每次命中都可能独立触发冷却缩短或镜像。
- 次数上限按“一次根源施法”计数，下一次施法重新开始；所以即使配置了限次，连续施法仍可高频触发。现有限次只能防单次爆发，不能表达“每秒最多触发一次”。
- `direct`/`primary_cast`只防Proc伤害递归和镜像再镜像，不限制触发频率。

**需要用户决定的项**：

1. **作用域**：全局一个ICD，还是每个拥有者、每个触发、每个拥有者+目标各一个？推荐“每拥有者+每触发”，避免多英雄互相饿死，也避免同一英雄多段刷触发。
2. **时长**：原型建议0.5～1.0秒；需确认量级。
3. **计时起点**：触发成功时开始、尝试触发时开始，还是施法开始？推荐“成功生效时开始”，失败不占用窗口。
4. **是否所有命中共享**：多段、DoT、脉冲、连锁跳是否共享同一个ICD？推荐共享，否则多段技能天然绕过。
5. **与现有次数上限的关系**：ICD作为额外闸门叠加在`proc_limit.csv`之上，还是替代它？推荐叠加：ICD管频率，限次管单次施法总量。
6. **重置边界**：换房/换层/重开房间是否清零？推荐只在本房内计时，不写入存档、不跨房继承。
7. **适用范围**：所有触发统一ICD，还是按触发逐条配置？推荐在`trigger.csv`或`proc_limit.csv`增加可空`icd`列，逐条配置，未配置则无ICD。

**推荐默认**：每拥有者+每触发、0.75秒、成功生效时开始、多段共享、与次数上限叠加、仅本房有效、逐触发配置。该默认仍需用户确认后才能改实现；本轮只解释，不改代码。

### D5 其余技能表现

**用户反馈：这轮制作，全部用临时资产绘制，但每个技能都要可观测。已完成。**

实现方式：

- Core新增只读表现事件流`VisualFxRuntime`（有界256条、序号+Tick、不持久化、不参与权威计算）。
- UI新增`CombatFxView`，用圆、弧、折线、文字绘制8类事件：施法、命中、治疗、增益、召唤、连锁、尸爆、脉冲；持续光环/诅咒/旋风/嘲讽锁敌/未消费尸体直接读现有Buff/锁定/尸体状态。
- 每个正式技能都有对应颜色或图形区分，并有`BD_SKILL_VISUAL_OK`输出与Mini截图（见3.4）。
- 限制：全部为**临时几何表现**，不是最终美术；不新增第二套战斗状态，不改伤害/奖励/随机/锁定；正式美术接入时可逐项替换。

召唤物仍是友方色小人占位，不是最终美术。

## 6. 边界与合规

- 未执行`git commit`、`push`、`merge`或覆盖远端历史；工作区变更由用户自行提交。
- 未提交缓存、构建产物、截图、存档；截图位于忽略的`artifacts/`。
- 自动检查/截图使用`artifacts/qa`隔离存档，不加载或覆盖玩家`user://`。
- `Scripts/Core`未新增Godot引用；伤害/奖励仍走唯一`SettleDamage`与既有奖励路径。
- 未修改星图购买、最多5人、1～5技能槽、换队NPC、即时经验与实物走近拾取规则。
- 未把原型数值标成最终策划。

## 7. 设计AI确认清单

请逐项确认或驳回：

- [ ] A1 61节点布局与五职业数量（14/12/10/12/13）符合批准方案。
- [ ] A2 16个绑定主动、13个新ID与3个原ID改行为的范围正确；`mage_frost_nova`保留旧行为。
- [ ] A5 五个初始技能映射正确。
- [ ] B3～B7 旧档迁移、退款清理、非法拒绝与幂等规则可接受。
- [ ] C1～C4 三系页、装配弹窗、召唤占位绘制与锁定索引修复可接受。
- [ ] C6～C8 只读表现事件流、17技能临时表现与表现隔离可接受。
- [ ] 3.4 的Mini截图足以证明A扇形、17个正式技能与五职业三系表现均已可观察。
- [ ] D1 最终平衡保持待定，由用户后续统一重算；当前原型数值不作为签收依据。
- [ ] D2 M5按“旧节点退役、稳定ID保留、归档备案”口径记录，无物理删除。
- [ ] D3 M7按“旧`skill_XX`退役、仅TEST_ONLY回归引用、归档备案”口径记录，无物理删除。
- [ ] D4 Proc ICD的作用域/时长/起点/共享/叠加/重置/逐条配置需要裁决（推荐见D4）。
- [x] D5 其余技能临时表现已在本轮完成，每个正式技能可观察；最终美术待后续接入。

## 8. 关键文件索引

| 类别 | 文件 |
|---|---|
| 天赋节点 | `Idle-CRPG/config/skill/talent.csv` |
| 主动技能 | `Idle-CRPG/config/skill/skill.csv`、`effect.csv`、`buff.csv`、`chain.csv`、`summon.csv`、`ability.csv`、`ability_effect.csv` |
| 初始技能 | `Idle-CRPG/config/skill/hero_starting_skill.csv` |
| 绑定加载/校验 | `Idle-CRPG/Scripts/Core/Talents.cs` |
| 绑定执行 | `Idle-CRPG/Scripts/Core/SkillRuntime.cs` |
| 旧档迁移 | `Idle-CRPG/Scripts/Core/SessionSnapshot.cs` |
| 专项检查 | `tests/SkillMigrationChecks.cs`、`tests/Program.cs` |
| 截图入口 | `Idle-CRPG/Scripts/UI/SkillBdCapture.cs`、`TalentSmoke.cs`、`Main.cs` |
| 表现事件流 | `Idle-CRPG/Scripts/Core/VisualFxRuntime.cs` |
| 临时表现绘制 | `Idle-CRPG/Scripts/UI/CombatFxView.cs` |
| 表现修复 | `Idle-CRPG/Scripts/UI/MotionPresentation.cs` |
| 旧内容备案 | [旧节点与旧技能退役备案](08_retired_content.md)、`tests/fixtures/legacy_skill_bd_2026_10_22/` |
| 执行记录 | `docs/reviews/2026-10-22_skill_bd/05_execution.md` |
| 迁移清单 | `docs/reviews/2026-10-22_skill_bd/06_migration_inventory.md` |
