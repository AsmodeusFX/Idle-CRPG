# P2-2-M2｜参数、公式与运行消费链路审计

2026-10-24 00:42（Asia/Shanghai）。阶段：P2-2 全系统配置结构盘点与扩展能力审计／M2。性质：**只读审计 + 测试侧导出工具**——未改正式 Core、配置、数值、存档与既有断言。

**验收标记：`P2_2_M2_PASS`**（完成「配置→加载→消费→结果」全量映射，仅放行 P2-2-M3）。

---

## 1 环境与指纹

| 项 | 值 |
|---|---|
| 仓库 / 分支 / 提交 | `e:/Git/Idle-CRPG` / `main` / `f53c11445330adc58bfce027672a72b057025a03`（`.git` 直读；`git` CLI 不可用） |
| 工作区状态 | **unknown**（无法用 git 判定未提交改动，以双指纹兜底） |
| 配置指纹 | `b8916dcf8d46c14058efe4185f7ed18deb9d839522d42b5223298838bb93f5d3` —— **与 M1 完全一致，正式配置零改动** |
| 源码指纹 | `b469248f84652cb9aa47577ce38fc63655955488f01caf58e836f205a12eeb87`（M1 为 `0815f4ce…`；差异全部来自本轮新增的测试侧审计工具 `tests/ParameterAudit.cs` 与 `tests/Program.cs` 入口，`Scripts/**` 未改） |
| .NET | SDK 10.0.301 / net8.0 |

---

## 2 与 M1 的对账与纠错（以 CSV 明细与真实代码为准）

| 项 | M1 表述 | 本轮核实 | 结论 |
|---|---|---|---|
| 配置文件总数 | 391（正文一处 390） | **391** = 195 CSV + 195 `.import` + 1 `.translation` | M1 明细正确 |
| ACTIVE CSV | 正文一处 141 | **142**（`config_manifest.csv` 明细 142） | 以 142 为准 |
| 系统条数 | 清单一处 30 | **31** | 以 CSV 明细为准 |
| 内容登记 | 4,278 | **4,278** | 一致；其中含规划记录（楼层预算 1,515） |
| 天赋绑定 | “38 个已绑定”/“24 个旧路径” | **39 主动（22 被动，共 61）；16 个 `skill_id` 绑定；23 个旧 `CastTalent`** | 两处旧数字均需更正，逐项清单见 §6.1 |
| 属性空投放 | “约 17 项无投放” | 36 Key：**18 个已投放且被消费；18 个零投放但代码已有消费分支；0 个“无消费”** | 三种状态必须分开（M1 未区分“未投放”与“未消费”） |

---

## 3 M2-A 参数台账

| 指标 | 结果 |
|---|---|
| 覆盖 | **142/142 份 ACTIVE CSV**，逐文件生成，**962 个列级记录**（`(source_path, column)` 为物理单位，`schema_group` 仅做跨章归一） |
| 语义覆盖 | 962/962 有语义与角色；`UNVERIFIED=0`、`PENDING=0` |
| 状态分布 | `ACTIVE_CONSUMED` 530、`VALIDATION_ONLY` 160、`PLANNED_ONLY` 122、`DISPLAY_ONLY` 83、`LOADED_ONLY` 54、`NO_CONSUMER` 13 |
| 验证级别 | `STATIC_VERIFIED` 450（本轮读码确认消费点）、`LOADER_VERIFIED` 512（加载/校验点确认，运行行为按 schema 归类） |
| 优先级归档 | 章节表按章加载（`chapters/actN` 为正式来源，根目录同名旧表仅旧地图）；共享表（`item/*`、`monster/monster_drop.csv`、`village/npc.csv`）由 `sharedDirectory` 提供；其余为配置根唯一来源 |

**跨章 Schema 漂移（重要发现）**：`dungeon/floor_plan.csv` 五章列数不同——act1 **40**、act2 **37**、act3 **38**、act4 **41**、act5 **42**。加载器（`FloorPlanning.LoadFloorPlans`）以**文件自身表头**为 schema，可选列用 `OptionalN` 读取，因此缺列不报错、多列被静默忽略：

- act1 独有 6 列：`ScaleTier`、`CompletionCondition`、`RequiredCombatFullCoverage`、`FirstClearExplorationCoverageAssumption`、`SourceSeedPolicy`、`ConfigStatus`；
- act2+ 新增 `FloorMonsterLevel`、`EliteGroupMonsterTarget`、`ContentStatus`；act3+ 增 `SpawnProfileId`；act4+ 增 `FixedUniqueActorCount`、`SealActivationCount`；act5 增 `StageMode`、`ScriptedWaveCount`、`ScriptedWaveMonsterTarget`；
- **12 列无任何消费者**（4×`FloorMonsterLevel`、4×`EliteGroupMonsterTarget`、3×`SpawnProfileId`、1×`SealActivationCount`），`FloorMonsterLevel` 与真正生效的 `MonsterLevel` 语义冲突、命名易误读；
- `FixedUniqueActorCount/ScriptedWaveCount/ScriptedWaveMonsterTarget` **只参与 PlannedKill 预算校验**（`KillBudget(...) + Fixed + Wave` 必须等于 `PlannedKillMin/Target/Max`），不参与生成；`StageMode` 只写入记录无消费方。

其余 `NO_CONSUMER`：`system/client.csv` 的 `checkpoint_seconds`（1 列，M1 ISS-009）。

---

## 4 M2-B 公式依赖（23 条）

`formula_dependency.csv` 按**实际代码**登记 23 条，每条含输入、运算顺序、默认值、Clamp/舍入、随机调用、触发时机、输出、写入点、受影响系统、源码行号、现有检查与验证级别。核心 6 条摘要：

1. **英雄经验**（`HeroExperience.cs:23-26,41-53`）：`percent = rate[clamp(heroLv-monsterLv)]` → 以 **1/100 点**为单位累加 `Experience*100+Fraction+basis*percent` → `while Experience>=HeroLevels[Lv].exp` 逐级扣减并叠加 `attack_bonus/hp_bonus` **差值**（不是绝对值）。零差 100%，倍率随等级差单调不增；倍率 0 时击杀仍发生、经验为 0（A 场景 Lv7 平台属设计）。
2. **金币**（`GameSession.cs:567-620`、`Stars.cs:11-19`、`Talents.cs:115-124`）：掉落 `chance×drop_chance_multiplier` → 拾取入账 → 回村售卖 `sell_price×count` → 星图 `Gold-=Costs[level]`、洗点 `ceil((base+invested×per_point)×(1-min(discount,1)))`。无“击杀直接给金币”的路径，与 M3 账本口径一致。
3. **伤害**（`DamagePipeline.cs:69-92`）：`k=interp(armor_curve, attackerLv)`；每分量 `basis×weight×(coef+Σskill)×(1+Σgeneral)×(1+Σpower)×(1+Σvuln)×crit×defense×blocked`；物理 `k/(armor+k)`，元素 `1-clamp(resist,min,max)`；命中/格挡/暴击为独立 `NextDouble`，`rate>=1` 必中；总伤必须 finite 且 <float.MaxValue。
4. **技能/Effect**（`SkillRuntime.cs:160-374`）：选目标 → Windup → Commit → 按 `effect_ids` 依次结算；10 类 effect kind 各有分支（见 §5）。
5. **刷怪**（`GameSession.cs:459-510`）：等级权威 **`floor_plan.MonsterLevel` > `poi_challenge.challenge` > `dungeon.level_min/max`**，统一 `+spawn_rule.level-1+LevelBonus`；数量 `rng.Next(count_min+bonus, count_max+bonus+1)`，Boss 占最后一个名额，入口内侧通行区避让最多 32 次后确定性回退。
6. **失败恢复**（`FailureRecovery.cs:29-62`）：仅在 `challenge>小队最低等级` 计数；达阈值（默认 2）暂停目标并改选已首通、可达、`challenge≤teamLevel` 的副本（否则取最低档），同级集合用固定种子 731913 抽取。

---

## 5 M2-C1/C2 属性与 Effect

`attribute_effect_matrix.csv` = 36 属性 Key + 10 Effect kind + 3 组机制行（buff mechanic / trigger / proc）。

**属性 36 Key 的三种状态（本轮逐 Key 复核）**

| 状态 | 数量 | 清单 |
|---|---:|---|
| 已投放且被消费 | **18** | atk、hp、armor、move_speed、attack_range、basic_cooldown、dodge_rate、crit_rate、crit_damage、block_rate、block_reduction、attack_speed_pct、engagement_capacity、fire/cold/lightning/poison_resistance、physical_vulnerable_damage_pct |
| 零投放但代码有消费分支 | **18** | cooldown_reduction_pct、all_damage_pct、skill_damage_pct、basic_attack_damage_pct、physical/fire/cold/lightning/poison_damage_pct、ranged_damage_pct、boss_damage_pct、skill_coefficient_bonus、power_damage_pct、vulnerable_damage_pct、fire/cold/lightning/poison_vulnerable_damage_pct |
| 无消费 | **0** | —（M1 曾把 all/vulnerable 近似当作“无消费”，实际两者都被构造为 `DamageBucket.General/Vulnerable`，只是无类型或由目标侧施加） |

投放来源：单位基础值（`heroes/monsters/act_monsters/unit_attributes`）、`buff.attributes`（仅 8 个 Key）、天赋被动（仅 atk/armor/crit_rate）、星图（attack/hp 走 MaxHp/Attack 直改，不经过属性 Key）。**新增 Key 的最小变更面**：`combat/fightattr.csv` 注册 + `fightattr_rules` 基线/边界 + `FightAttributeCatalog` 的分聚合白名单（若新聚合方式）+ 至少一个消费点（伤害桶或行为）+ 模拟可观测字段；仅注册不产生任何玩法效果。

**Effect 10 类**（每种至少一条完整链已静态跟踪，`--skill-bd-*-checks` 覆盖运行）：Damage(7)、ApplyBuff(5)、MaintainAura(3)、Summon(2)、SpawnProjectile(2)、AreaPulse(2)、Chain(1)、CorpseExplosion(1)、Heal(1)、AbilityCast(1) —— 合计 25 个效果，全部有消费分支。`trigger.csv`/`proc_icd.csv`/`proc_limit.csv` 为 **0 行空表**：加载与消费链存在（`SkillRuntime.React/ProcAllowed`，`--skill-bd-proc-checks` 覆盖同 Tick 队列/ICD/限次），但**当前零投放**——不能据此断言触发逻辑未实现。

---

## 6 重点系统结论

### 6.1 天赋/技能（含 23 个旧路径节点逐项）

61 节点 = 39 主动 + 22 被动；**16 个主动绑定 `skill_id`**（走正式 `SkillRuntime`），**23 个无 `skill_id`**（走 `Talents.CastTalent` 简化路径）。两套路径**不等价**：

| 节点 | 职业 | effect | value/per_rank | cd | 距离/半径 | 伤害类型 | 旧路径实际行为 | 与正式版差异 |
|---|---|---|---|---:|---|---|---|---|
| barb_shout | warrior | shout | 0.15/0.03 | 12 | 6/6 | Physical | 半径内友方施加 `talent_battle_shout` | 无正式 skill 对照 |
| barb_weaken | warrior | weaken | 0.15/0.02 | 14 | 6/6 | Physical | 半径内敌人施加 `talent_weaken` | 同上 |
| barb_leap | warrior | leap | 1.8/0.18 | 7 | 5/1.6 | Physical | **以目标为圆心**半径结算一次 | 正式 `leap` 无同 ID skill |
| barb_whirlwind | warrior | whirlwind | 1.2/0.12 | 5 | 2.4/2.4 | Physical | 落入通用分支=**以自身为圆心单次 AoE** | 正式 `barb_whirlwind_window` 为 AreaPulse 持续窗口 |
| ranger_shot | ranger | shot | 1.3/0.12 | 2 | 6/0 | Physical | `SpawnGeneric` 投射物 | 正式版支持齐射/穿透 |
| ranger_explosive | ranger | shot | 1.4/0.14 | 6 | 6/1.7 | Fire | 投射物（半径字段在 shot 分支不参与） | 半径未生效 |
| ranger_poison | ranger | shot | 1.3/0.13 | 4 | 6/0 | Poison | 投射物 | — |
| ranger_rally | ranger | shout | 0.12/0.02 | 14 | 6/6 | Physical | 同 shout | — |
| ranger_suppress | ranger | weaken | 0.12/0.015 | 14 | 6/6 | Physical | 同 weaken | — |
| mage_flame_burst | mage | nova | 1.6/0.16 | 7 | 6/2 | Fire | 以目标为圆心半径结算 | 正式版无同 ID |
| mage_icebolt | mage | shot | 1.3/0.13 | 3 | 6/0 | Cold | 投射物 | — |
| mage_frost_nova | mage | nova | 1.3/0.13 | 7 | 5/2.2 | Cold | 同上 | — |
| mage_lightning | mage | shot | 1.5/0.15 | 4 | 6/0 | Lightning | 投射物 | — |
| mage_storm | mage | nova | 1.5/0.15 | 8 | 6/2.3 | Lightning | 同上 | — |
| paladin_hammer | priest | nova | 1.4/0.14 | 6 | 4/1.8 | Physical | 同上 | — |
| paladin_resolve | priest | shout | 0.12/0.02 | 14 | 6/6 | Physical | 同 shout | — |
| paladin_shock | priest | nova | 1.3/0.13 | 6 | 5/1.8 | Lightning | 同上 | — |
| paladin_zeal | priest | shout | 0.2/0.025 | 18 | 6/6 | Physical | 同 shout（半径 6 覆盖全队） | 名称为“热忱”但实现为增益 shout |
| necro_teeth | necromancer | shot | 1.35/0.13 | 2.5 | 6/0 | Physical | 投射物 | — |
| necro_soulbolt | necromancer | shot | 1.4/0.14 | 4 | 6/0 | Poison | 投射物 | — |
| necro_spear | necromancer | shot | 2/0.2 | 6 | 7/0 | Physical | 投射物 | — |
| necro_weakness | necromancer | weaken | 0.15/0.015 | 13 | 6/6 | Physical | 同 weaken | — |
| necro_frailty | necromancer | weaken | 0.22/0.02 | 18 | 6/6 | Physical | 同 weaken | — |

要点：`effect=whirlwind` 是**唯一落入通用伤害分支的“非标准”值**（`CastTalent` 只对 `bash/leap/nova/shot/shout/weaken` 有专门分支）；`ranger_explosive` 的 `radius=1.7` 在 shot 分支不参与，属“配置已填但旧路径不读”的字段。P2-3 若迁移这 23 个节点，需要逐节点对齐效果表达，不能假定新旧等价。

### 6.2 星图

17 节点：**attack 4、hp 3、party_slots 4、skill_slots 4、talent_reset 1、talent_reset_discount 1**（共 6 种效果类型）。加载校验：仅接受 `attack/hp/party_slots/skill_slots/talent_reset/talent_reset_discount` + `operation=flat`；`party_slots` 必须单级+1；技能槽/洗点解锁必须单级+1；洗点折扣总和 ≤100%；恰好一个根节点、格子不重叠、连线无环。

| 效果类型 | 消费点 | 证据 |
|---|---|---|
| attack / hp | `ApplyStarTotals` 按 (totals-old) 差量改全队 `Attack/MaxHp`，并夹取当前 HP | `Stars.cs:33-38` |
| party_slots | `FormationCapacity = min(5, max(floor, 1+Σ))` | `FormationPlans.cs:16` |
| skill_slots | `ActiveSkillSlots = min(max_slots, initial+Σ)` | `Talents.cs:86` |
| talent_reset | `CanResetTalents` 门槛（≥1 才可洗点） | `Talents.cs:120,152` |
| talent_reset_discount | `TalentResetCost` 乘 `(1-clamp(Σ,0,1))` | `Talents.cs:115` |

**扩容三分类**：①新增 attack/hp 节点＝纯数据；②新增同类型功能节点（第二组席位/技能槽）＝纯数据但受单级+1 与上限约束（席位上限 5 由 `FormationCapacity` 硬编码）；③**全新功能**（如第二编队、装备槽）＝必须同时改 `StarConfiguration` 白名单 + 新消费点 + 存档字段，标 `REQUIRES_EXISTING_SYSTEM_EXTENSION`。

### 6.3 房间事件

`dungeon_rule.event_chance`（当前 0.5）→ 非 Boss 房 `rng.NextDouble()<chance` → `rng.Next(2)==0 ? 营火 : 宝箱`（各 50%）。营火：存活者回复 **30% MaxHp**；宝箱：按房间等级结算 **基础 8 经验**（`GainMonsterExperience(8, SnapshotLevel)`，各英雄按自身等级差倍率）。Boss 房不抽事件；事件每房只结算一次（`EventUsed`）。

P2-3 若要事件表驱动，最小字段：`event_id / kind / weight 或 chance / 条件(难度/层/房间类型/是否 Boss) / 效果类型与参数(经验/回复/掉落/减益) / 一次性标记 / 展示文案`；最小 Core 扩展点：`Reveal` 的抽选（读表替代硬编码）+ `TickDungeon` 的执行分支 + 加载校验 + `Room.Event` 从字符串改为 id。**本轮只提案，未实现。**

### 6.4 怪物 / 精英 / Boss

| 维度 | 数量 | 说明 |
|---|---:|---|
| 身份（`act_monsters`） | **49** | 纯数据；决定基础属性与外观 |
| 行为行（`act_behaviors`） | **17** | 12 行 Ordinary + 5 行 BacklinePriority；`policy` 只解析到枚举（Ordinary/BacklinePriority/Hunter/Support），**行为分支只有 area/hunter/support 三类 + 默认** |
| 精英词缀 | **10** | 同难度出现率必须一致；8 行默认策略 + 2 行 BacklinePriority；每房最多一个；只在世界远征、非 Boss 房掷骰 |
| Boss 技能包 | **34** | 按 `poi_id` 附加一个技能；不改等级/奖励 |
| 区域加权池 | **167 行** | 按 `areapart` 分组，每组权重和必须 100 |

结论：**新增身份=纯数据**（身份表+池行），**新增行为=改 Core**（枚举 + 分支 + 观测），**Boss 阶段=新 Core**（当前无阶段概念）。不要把“身份数/表行数/行为种类数”混为一谈。

### 6.5 关卡与生成

- 楼层数：有 POI 档案时 = `poi_profile.floors`（`GameSession.cs:138`），否则 `rng.Next(floors_min, floors_max+1)`；房间数 = `rng.Next(rooms_min, rooms_max+1)`；房间半宽/高 = `rng.Next(min,max+1)*0.5`；最深层最高 id 为 Boss 房（`Dungeon.cs:71-99`）。
- 等级权威链见 §4.5；`floor_plan.MonsterLevel` 是唯一真正影响刷怪等级的规划列。
- **规划≠生成**：`LayoutRows/Cols`、`ActiveCellTarget`、各 `*CellCount`、`*PerCell*`、`PlannedKill*`、`ScaleTier` 等均不驱动生成；`FloorReports` 明确把 `plan.Active/TargetKills` 与实际 `Rooms.Count/ActualSpawned` 并列展示，源码注释写明“不伪造达到规划格数”。
- 难度与地图切换：`DifficultyIndex` 来自会话，决定 `poi_challenge.difficulty` 与 `floor_plan.DifficultyId` 的选取；章节切换由 `ChapterConfiguration` 重建 Config（每章独立目录）。

---

## 7 经济与经验运行链（与 M3 账本一致）

`runtime_consumption_matrix.csv` 共 23 条链（C-01..C-23），覆盖经验、金币收支、伤害、技能/Buff/召唤、天赋、星图五项、关卡生成、事件、失败恢复、城堡、传送、自动保存、模拟隔离。金币四分账（拾取/售卖/星图/洗点）在 M3 已用 39 场模拟闭合（差额 0），本轮只做静态确认，不重跑长场。

---

## 8 M2-E P2-3 扩容接口

`expansion_interface_matrix.csv` 15 行，按分类：**纯数据 6**（属性现有 Key、星图属性、星图现有功能、怪物身份、关卡/POI、经济物品）、**数据+待 M2 确认 2**（天赋新被动 Key、精英/Boss 数据面）、**需扩展既有系统 6**（新 Effect kind、星图全新功能、事件表、怪物新行为、楼层预算接入、新增章节）、**超范围 1**（装备/商店）。

每条含：现有能力、候选内容类别、分类、schema 支持、是否需要新消费者、最小代码触点、可测性、缺失事实、P2-3 决策建议。

---

## 9 M2-D 运行验证与未覆盖

| 检查 | 结果 |
|---|---|
| `dotnet build`（游戏 + 检查工程） | 退出 0，0 警告 0 错误 |
| 全量 Core Checks | 退出 0（27.4s；含 room-entry 384 场景、tool-checks、技能/召唤/城堡/存档等） |
| 短场模拟（工具自检内 T_tool_probe 6 分钟 ×3 次） | 退出 0；`gold_diff=0`、`xp_unexplained=0` |
| `--param-audit` | 退出 0；142/142 文件、962 列、0 UNVERIFIED |
| Godot smoke | **未运行**：本轮零 UI/表现改动，按计划“涉及 UI 适配时才运行”跳过 |

**未覆盖/限制**：①逐技能施放、攻击者、逐技能伤害与承伤、召唤生命周期仍 `UNAVAILABLE`（`M3_COMBAT_FACTS_PENDING`），本轮只做静态链与聚合计数，未宣称逐技能实测；②`LOADER_VERIFIED` 的 512 列只验证了加载与 schema，未逐列构造运行用例；③反射/动态路径：星图编辑器（`StarEditorStore`）与 GM 面板可写配置，未做运行时篡改测试；④未评估任何占位数值强弱。

---

## 10 验收对照（计划 §8）

| 条目 | 结论 |
|---|---|
| 142 份 ACTIVE 与 `config_manifest.csv` 对账、每列有语义/归类/原因 | **PASS**（142/142、962/962、0 UNVERIFIED） |
| 影响玩法的参数指向真实加载点与消费者，或标 `LOADED_ONLY/PLANNED_ONLY/NO_CONSUMER` | **PASS**（530 消费 / 160 校验 / 122 规划 / 83 展示 / 54 仅加载 / 13 无消费） |
| 关键公式有依赖与顺序证据 | **PASS**（23 条带行号） |
| 36 属性 Key 与 10 Effect 全覆盖；旧 CastTalent 节点逐项归档 | **PASS**（36+10；**23 个**节点逐项，纠正 M1 的 24） |
| 17 星图节点与消费者逐项核对；新功能节点成本分类 | **PASS**（5 类效果 + 4 个功能消费者） |
| 事件硬编码、身份/行为分离、规划≠刷新的代码级证据 | **PASS**（§6.3–6.5） |
| M3 观测缺口继续标 `UNAVAILABLE` | **PASS**（§9） |
| 不改正式 Core/配置/存档/玩法 | **PASS**（配置指纹与 M1 一致；源码变更仅 `tests/`） |
| 生成机器表与单份 MD Review | **PASS**（5 CSV + 本文） |

---

## 11 裁决

**`P2_2_M2_PASS`**：配置到运行的全量映射已完成，主要缺口是“逐技能/攻击者事实”与“纯静态确认的运行级验证”，已在 §9 明确标注，不阻断 P2-2-M3。

进入 **P2-2-M3「数值设计接口与内容覆盖验收」** 前的三项前置：

1. 明确 `floor_plan` 跨章 schema 统一方案（12 个无消费者列去留，`FloorMonsterLevel` 与 `MonsterLevel` 二选一），否则 P2-4 无法保证“设计值→配置”唯一入口。
2. 明确天赋双路径裁决（23 个旧节点迁移或标注冻结），否则同一技能会存在两套行为。
3. 星图新功能节点若进入 P2-3，须先定消费者与存档兼容（当前白名单 + 硬编码上限是实际边界）。

---

## 12 复跑与数据目录

```powershell
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --param-audit
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --param-audit --param-out=artifacts/p2_2_m2
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj            # 全量检查
```

机器表（与本文同目录 `docs/data/2026-10-24_00-42_data_p2_2_m2_parameter_runtime_audit/`）：

| 文件 | 行数 | 内容 |
|---|---:|---|
| `parameter_registry.csv` | 962 | 142 份 ACTIVE 的逐列台账（21 字段） |
| `formula_dependency.csv` | 23 | 公式、输入、顺序、Clamp、随机、写入点、源码证据 |
| `attribute_effect_matrix.csv` | 49 | 36 属性 Key + 10 Effect kind + 3 机制组 |
| `runtime_consumption_matrix.csv` | 23 | 配置→加载→规则→运行→结果→可观测证据 |
| `expansion_interface_matrix.csv` | 15 | P2-3 扩容接口与成本分类 |

变更文件：新增 `tests/ParameterAudit.cs`、`tests/Program.cs` 增加 `--param-audit`（含 `--param-out`）、上述 5 CSV 与本文；**未改** `Idle-CRPG/Scripts/**`、`Idle-CRPG/config/**`、存档、模拟策略、既有断言。未提交、未推送。
