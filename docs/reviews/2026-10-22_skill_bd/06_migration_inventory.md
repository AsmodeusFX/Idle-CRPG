# 技能 BD 正式迁移：文件清单、ID 映射、旧档样例与回滚方案

2026-10-22。状态：**提案，未实施**。本文件只完成 `skill_bd_plan.md` 要求的迁移前置材料，未修改任何 CSV、代码、存档或玩家进度。17 个候选技能仍不是已投放内容；三系布局、旧节点去留、初始技能替换和点数上限仍未裁决。

## 1. 现状事实（迁移依据）

| 事实 | 证据 |
|---|---|
| 正式可学习内容为 5 职业 48 个天赋节点（野蛮人 12，其余各 9） | `Idle-CRPG/config/skill/talent.csv` |
| 主动技能节点同时是技能定义：加载时按节点 ID 生成 `SkillDefinition`，战斗按 `TalentNode.Effect` 硬编码分派（bash/leap/whirlwind/shout/weaken/shot/nova） | `Scripts/Core/Talents.cs:28-58,147-160`；`SkillRuntime.cs:180` |
| 数据驱动技能 `skill.csv` 的 15 个 `skill_XX` 导入行当前没有玩家学习/装配入口，只在旧回归与文档中出现 | `skill.csv`；`tests/CombatSkillChecks.cs`、`DamageFormulaChecks.cs`、`CombatTargetingChecks.cs`、`SkillBdSupportChecks.cs` |
| 存档中的技能相关事实分散在 `Actor`：`Talents`、`SkillIds`、`SkillLevels`、`StartingSkillGranted`、`TalentBonuses`、`SkillSwapLocks`、`ActiveAuraSkills`，召唤另有 `SummonDefinitionId/SummonSkill/SummonOwner` | `Scripts/Core/GameSession.cs:19-31` |
| 读档强校验：未知 `Talents`/`SkillIds` 引用会拒绝存档；洗点通过删除投入自然返还点数；初始技能只初始化一次 | `Talents.cs:62-172`；`StartingSkills.cs:14-24`；`SessionSnapshot.cs:217-250` |
| 存档格式 `SaveDocument.Version=4`，已有按字段版本迁移的先例（`CombatVersion`、`HeroExperienceVersion` 等），未按格式版本整体升级 | `SaveStore.cs:9-35`；`SessionSnapshot.cs:90-149` |
| 旧配置已冻结为 TEST_ONLY 副本，现行生产配置未被替换 | `tests/fixtures/legacy_skill_bd_2026_10_22/` |

## 2. 完整拟改文件清单

以下为“若执行正式迁移”需要触达的文件；实际范围应在裁决后再次缩小，不默认全改。

### 配置

| 文件 | 变更 | 说明 |
|---|---|---|
| `config/skill/talent.csv` | 新增节点行；可能新增可选列 `skill_id`；可能重命名 `tree_name`；可能移动 `tree/row/col/prerequisite` | 只有 `skill_id` 非空时，主动节点才绑定数据驱动技能；旧行留空保持原行为。列变更须同步字段字典与校验 |
| `config/skill/skill.csv` | 新增/补全 17 技能的正式定义 | 每个主动节点最终必须有可加载的 `SkillDefinition`；未裁决前不得填正式数值 |
| `config/skill/effect.csv`、`buff.csv`、`trigger.csv`、`chain.csv`、`summon.csv`、`proc_limit.csv` | 按批准技能增加正式行 | 当前除基础行外为空表；B/C 能力已有 Core 实现，但正式表为空 |
| `config/skill/hero_starting_skill.csv` | 仅在初始技能替换获批后修改 | 当前 5 职业各 1 个初始技能 |
| `config/system/config.csv` | 仅在新点数上限/槽位规则获批后修改 | 现有 `talent_initial_points`、`talent_points_per_level`、`active_skill_*` |
| `config/skill/ability*.csv` | 若退役 `skill_XX` 旧数据，需同步迁移引用 | 需先确认没有隐藏入口；不要顺手删除 |

### Core

| 文件 | 变更 | 说明 |
|---|---|---|
| `Scripts/Core/Talents.cs` | 加载/校验 `skill_id`；主动节点改走通用技能定义；`CastTalent` 保留旧分派作为空列兼容；前置/点数/初始技能校验 | 迁移的核心；不得出现第二套伤害路径 |
| `Scripts/Core/SkillConfiguration.cs` | 技能/效果/召唤/连锁/限次定义加载与图校验 | 已有能力，补正式行校验 |
| `Scripts/Core/SkillRuntime.cs` | 主动施放入口优先解析 `SkillDefinition`；保留天赋节点回退 | 现在先判 `Config.Talents.Nodes`，需避免新技能 ID 被旧分派遮蔽 |
| `Scripts/Core/StartingSkills.cs` | 初始技能 ID 变更时的旧档处理 | 只初始化一次，不重复赠点 |
| `Scripts/Core/SessionSnapshot.cs` | 新增 `SkillTreeVersion`；迁移与校验；旧档转换 | 仿 `CombatVersion`/`HeroExperienceVersion`，不改 `SaveDocument.Version` |
| `Scripts/Core/SaveStore.cs` | 仅在需要新版本号时改；迁移前导出备份 | 原子写/备份已存在 |
| `Scripts/Core/GameSession.cs` | 仅新增快照字段/迁移入口；`Actor` 字段不删 | 保留旧字段避免读档断裂 |

### UI / 表现

| 文件 | 变更 |
|---|---|
| `Scripts/UI/TalentUi.cs`、`HeroPage.cs` | 节点名/系名/前置来自 CSV，通常无需改；若新增 `skill_id` 需显示来源技能 |
| `Scripts/UI/TalentSmoke.cs`、`InteractionSmoke.cs`、`FormationUi.cs` | 固定 ID 的冒烟期望需同步 |
| `Scripts/UI/SkillBdCapture.cs`、`Main.cs` | 新技能的 Mini 视觉入口；C 阶段持续旋风/召唤尚无视觉验收 |

### 测试

| 文件 | 变更 |
|---|---|
| `tests/TalentChecks.cs` | 48 节点、初始技能、前置、洗点、装配、快照 |
| `tests/ClassFollowingChecks.cs` | 五职业初始技能与三系 |
| `tests/CombatSkillChecks.cs`、`DamageFormulaChecks.cs`、`CombatTargetingChecks.cs` | `skill_XX` 旧技能回归；退役时需迁到 TEST_ONLY 入口 |
| `tests/SkillBd*Checks.cs` | 隔离 QA 夹具不依赖正式树，保持独立 |
| 新增 `tests/SkillMigrationChecks.cs` | 旧档映射、幂等、返还、未知 ID、回滚恢复 |
| `tests/Program.cs` | 注册新检查入口 |

### 文档

`docs/README.md`、`docs/design/talent_trees.md`、`docs/design/skills_effects_buffs.md`、`docs/design/skill_bd_interactions.md`、`docs/technical/skill_bd_plan.md`、`docs/technical/combat_config.md`、`docs/technical/persistence_settings.md`、本目录 `05_execution.md`。

## 3. 稳定 ID 与 17 技能映射建议

### 3.1 命名规则

建议稳定 ID 继续使用现有小写蛇形与职业前缀（`barb_`/`ranger_`/`mage_`/`paladin_`/`necro_`），不把草案大写 ID（`BAR_BASH` 等）直接写进存档。理由：

- 现有 48 节点、初始技能表、存档、UI 冒烟与测试均使用小写 ID；大小写敏感，无需引入一次全量改号。
- 草案大写 ID 只作为跨文档引用名，可在字段字典中登记别名，不进入玩家存档。
- 圣骑士英雄 ID 是 `priest`，节点前缀是 `paladin_`，迁移保持现状，不趁机改名。

### 3.2 逐技能处置

“沿用”指保留现有节点 ID 与已学等级；“新增”指新节点 ID，不影响旧记录；“换行为”指同一 ID 下改变实战表现，必须单独批准并在更新说明中标注。

| 草案 | 建议稳定 ID | 处置 | 现有对应/备注 |
|---|---|---|---|
| BAR_BASH | `barb_bash` | 沿用，绑定数据驱动定义 | 现有主动；行为可保持单体物理 |
| BAR_TAUNT | `barb_taunt` | 新增 | 现有 `barb_shout` 是攻击增益，不是强制锁定，不能顶替 |
| BAR_WHIRLWIND | `barb_whirlwind` / `barb_whirlwind_window` | 旧 ID 保旧行为，新 ID 加持续窗口 | 现有 `barb_whirlwind` 继续一次自身 AoE；C3 持续窗口以新节点 `barb_whirlwind_window` 投放 |
| PAL_SMITE | `paladin_smite` | 沿用，绑定数据驱动定义 | 现有主动 |
| PAL_PRAYER | `pal_prayer` | 新增 | 现有 `paladin_resolve` 是短时战吼，不是持续治疗光环 |
| PAL_FANATICISM | `pal_fanaticism` | 新增 | 现有 `paladin_zeal` 是短时战吼；光环语义不同 |
| PAL_CONVICTION | `pal_conviction` | 新增 | 现有 `paladin_shock` 是伤害新星，不是减抗光环 |
| SOR_FIREBALL | `mage_fireball` | 沿用，绑定数据驱动定义 | 现有主动 |
| SOR_FROST_NOVA | `mage_frost_nova` | 沿用，保旧行为 | 现有为纯伤害冰霜新星；减速未批准，不在此节点上追加 |
| SOR_CHAIN_LIGHTNING | `sor_chain_lightning` | 新增 | 现有 `mage_lightning` 是单体闪电，不替换 |
| NEC_RAISE_SKELETON | `nec_raise_skeleton` | 新增 | B2 已具备单骷髅运行时；正式树仍空 |
| NEC_AMPLIFY_DAMAGE | `nec_amplify_damage` | 新增 | 现有 `necro_weakness`/`necro_frailty` 降攻击，不是物理易伤 |
| NEC_LOWER_RESIST | `nec_lower_resist` | 新增 | 无现有对应 |
| NEC_CORPSE_EXPLOSION | `nec_corpse_explosion` | 新增 | C2 已具备尸体/尸爆运行时；正式表仍空 |
| RAN_MULTISHOT | `ran_multishot` | 新增 | 现有 `ranger_shot` 是单体初始射击；是否替换初始技能另行裁决 |
| RAN_GUIDED_ARROW | `ran_guided_arrow` | 新增 | 无现有追踪箭节点 |
| RAN_VALKYRIE | `ran_valkyrie` | 新增 | C2 已具备召唤运行时；正式表仍空 |

### 3.3 旧 48 节点的处置分类

在“加性首次切换”策略下，全部 48 节点 **保留 ID、等级与位置**，不退款：

- 全部被动节点（`barb_voice/courage/mastery/attack/defense/focus/power`、`ranger_archery/hide/instinct/precision`、`mage_fire_mastery/frost_armor/storm_mastery`、`paladin_weapon/armor/fortitude/might`、`necro_bone_armor/curse_mastery/soul_mastery/will`）：原样保留。
- 未列入首批的主动节点（`barb_shout/weaken/leap`、`ranger_shot/explosive/poison/rally/suppress`、`mage_flame_burst/icebolt/lightning/storm`、`paladin_hammer/resolve/shock/zeal`、`necro_teeth/weakness/frailty/soulbolt`）：原样保留；其中 `mage_fireball`、`paladin_smite`、`barb_bash`、`barb_whirlwind` 四个按上表绑定/换行为。
- 若改为“重排三系”，必须逐节点给出 迁移/合并/退役 结论、前置重算与退款，不能直接覆盖 `talent.csv`。

### 3.4 三系名称与布局

现有与草案差异：

| 职业 | 现有三系 | 草案方向 | 建议 |
|---|---|---|---|
| 野蛮人 | 战吼 / 战斗专精 / 战斗技能 | 战吼（嘲讽）/ 战斗技能（重击、旋风） | 系名可保留；新节点追加到空闲行列 |
| 游侠 | 弓术 / 生存 / 战术 | 物理箭术 / 狩猎与生存 / 待定 | 系名待裁决；新增节点可先追加 |
| 法师 | 火焰 / 冰霜 / 闪电 | 火焰 / 冰霜 / 闪电 | 一致，仅加节点 |
| 圣骑士 | 战斗 / 守护 / 审判 | 战斗 / 防御光环 / 进攻光环 | 可只改 `tree_name`，不改 `tree` 索引；新光环节点追加 |
| 死灵 | 白骨 / 诅咒 / 灵魂 | 召唤 / 诅咒 / 毒素与白骨 | 可只改 `tree_name`；骨系 Physical 仍是旧原型，不据此定最终伤害类型 |

### 3.5 按已裁决方向落成的具体布局（已实施，2026-10-22）

> 实施状态：已按本表写入`talent.csv`（61节点）及对应`skill/effect/buff/chain/summon/ability`行；数值仍为QA原型，不是最终平衡。

用户已选择：小写现有前缀（M1）、本次直接按新三系重排（M2）、原 ID 保旧行为且新 ID 加新技能（M3）、初始技能替换为对应新技能（M4）。下表是据此落成的完整节点位置；`NEW` 为新增节点，其余保留原 ID。数值仍未批准，实施时只使用明确标注的 QA 原型占位，不写成最终策划。

**野蛮人**（0 战吼 / 1 战斗专精 / 2 战斗技能）

| tree | row,col | 节点 | 类型 | 前置 |
|---|---|---|---|---|
| 0 | 0,0 | barb_voice | 被动 | — |
| 0 | 0,1 | barb_shout | 主动 | — |
| 0 | 1,0 | barb_taunt NEW | 主动 | barb_shout 1 |
| 0 | 1,1 | barb_weaken | 主动 | barb_shout 1 |
| 0 | 2,1 | barb_courage | 被动 | barb_weaken 1 |
| 1 | 0,0 | barb_mastery | 被动 | — |
| 1 | 0,2 | barb_attack | 被动 | — |
| 1 | 1,1 | barb_defense | 被动 | barb_mastery 1 |
| 1 | 2,1 | barb_focus | 被动 | barb_defense 1 |
| 2 | 0,1 | barb_bash | 主动（初始） | — |
| 2 | 1,0 | barb_leap | 主动 | barb_bash 1 |
| 2 | 1,2 | barb_whirlwind | 主动 | barb_bash 1 |
| 2 | 2,0 | barb_whirlwind_window NEW | 主动 | barb_whirlwind 1 |
| 2 | 2,1 | barb_power | 被动 | barb_whirlwind 1 |

**游侠**（0 物理箭术 / 1 元素箭术 / 2 狩猎与生存）

| tree | row,col | 节点 | 类型 | 前置 |
|---|---|---|---|---|
| 0 | 0,0 | ranger_archery | 被动 | — |
| 0 | 0,1 | ran_multishot NEW | 主动（初始） | — |
| 0 | 0,2 | ranger_shot | 主动 | — |
| 0 | 1,1 | ran_guided_arrow NEW | 主动 | ran_multishot 1 |
| 0 | 2,0 | ranger_precision | 被动 | —（required_points 0） |
| 1 | 0,0 | ranger_explosive | 主动 | —（clear old prereq，required_points 0） |
| 1 | 0,1 | ranger_poison | 主动 | —（required_points 0） |
| 2 | 0,0 | ranger_hide | 被动 | — |
| 2 | 0,1 | ranger_rally | 主动 | — |
| 2 | 0,2 | ran_valkyrie NEW | 主动 | — |
| 2 | 1,0 | ranger_instinct | 被动 | ranger_hide 1 |
| 2 | 1,1 | ranger_suppress | 主动 | ranger_rally 1 |

**法师**（0 火焰 / 1 冰霜 / 2 闪电；旧节点位置不变）

| tree | row,col | 节点 | 类型 | 前置 |
|---|---|---|---|---|
| 0 | 0,0 | mage_fire_mastery | 被动 | — |
| 0 | 0,1 | mage_fireball | 主动（初始） | — |
| 0 | 1,1 | mage_flame_burst | 主动 | mage_fireball 1 |
| 1 | 0,0 | mage_frost_armor | 被动 | — |
| 1 | 0,1 | mage_icebolt | 主动 | — |
| 1 | 1,1 | mage_frost_nova | 主动 | mage_icebolt 1 |
| 2 | 0,0 | mage_storm_mastery | 被动 | — |
| 2 | 0,1 | mage_lightning | 主动 | — |
| 2 | 1,1 | mage_storm | 主动 | mage_lightning 1 |
| 2 | 2,1 | sor_chain_lightning NEW | 主动 | mage_storm 1 |

**圣骑士**（0 战斗 / 1 防御光环 / 2 进攻光环；旧节点仅改系名）

| tree | row,col | 节点 | 类型 | 前置 |
|---|---|---|---|---|
| 0 | 0,0 | paladin_weapon | 被动 | — |
| 0 | 0,1 | paladin_smite | 主动（初始） | — |
| 0 | 1,1 | paladin_hammer | 主动 | paladin_smite 1 |
| 1 | 0,0 | paladin_armor | 被动 | — |
| 1 | 0,1 | paladin_resolve | 主动 | — |
| 1 | 0,2 | pal_prayer NEW | 主动光环 | — |
| 1 | 1,0 | paladin_fortitude | 被动 | paladin_armor 1 |
| 2 | 0,0 | paladin_might | 被动 | — |
| 2 | 0,1 | paladin_shock | 主动 | — |
| 2 | 0,2 | pal_fanaticism NEW | 主动光环 | — |
| 2 | 1,1 | paladin_zeal | 主动 | paladin_shock 1 |
| 2 | 2,2 | pal_conviction NEW | 主动光环 | pal_fanaticism 1 |

**死灵法师**（0 毒素与白骨 / 1 诅咒 / 2 召唤）

| tree | row,col | 节点 | 类型 | 前置 |
|---|---|---|---|---|
| 0 | 0,0 | necro_bone_armor | 被动 | — |
| 0 | 0,1 | necro_teeth | 主动 | — |
| 0 | 0,2 | necro_soulbolt | 主动 | —（required_points 0） |
| 0 | 1,1 | necro_spear | 主动 | necro_teeth 1 |
| 0 | 1,2 | necro_soul_mastery | 被动 | —（required_points 0） |
| 0 | 2,2 | necro_will | 被动 | necro_soul_mastery 1 |
| 1 | 0,0 | necro_curse_mastery | 被动 | — |
| 1 | 0,1 | necro_weakness | 主动 | — |
| 1 | 1,1 | necro_frailty | 主动 | necro_weakness 1 |
| 1 | 1,2 | nec_amplify_damage NEW | 主动 | necro_weakness 1 |
| 1 | 2,2 | nec_lower_resist NEW | 主动 | necro_frailty 1 |
| 2 | 0,1 | nec_raise_skeleton NEW | 主动（初始） | — |
| 2 | 1,1 | nec_corpse_explosion NEW | 主动 | nec_raise_skeleton 1 |

**初始技能替换**

| 职业 | 旧初始 | 新初始 |
|---|---|---|
| 野蛮人 | barb_bash | barb_bash（同 ID，绑定新定义） |
| 游侠 | ranger_shot | ran_multishot |
| 法师 | mage_fireball | mage_fireball（同 ID，绑定新定义） |
| 圣骑士 | paladin_smite | paladin_smite（同 ID，绑定新定义） |
| 死灵 | necro_teeth | nec_raise_skeleton |

旧档兼容规则：`StartingSkillGranted=true` 的旧角色保持已学初始节点，不免费改赠新初始节点；新角色按新表初始化。旧档若洗点后重新初始化也不补赠，沿用现有“初始化只一次”。

### 3.6 重排后的旧档合法性规则（必须实现）

直接重排会改变树归属/行列，但必须保证旧档读入后仍通过 `ValidateTalents`：

1. **同 ID 保留学习等级**：`Talents` 的键值不因移动改写；新位置的前置按上表校验。
2. **前置保持成对迁移**：上表所有保留的前置边两端都在同一新树且行号递增；游侠 `ranger_explosive`、死灵 `necro_soulbolt` 等跨树移动节点显式清空前置并把 `required_points` 设为 0，避免旧档因缺少新前置被拒。
3. **系内投入只增不减**：旧树节点整组迁入一个或合并进更大的新树（死灵“灵魂”并入“毒素与白骨”），旧档原系投入在新树中仍被计入，不会因重排造成投入不足。
4. **失败兜底**：迁移后仍无法通过前置/投入校验的节点，按行从高到低逐项退款（删除 `Talents` 键、从 `SkillIds` 移除、清理 `SkillSwapLocks`/`ActiveAuraSkills`），并写入迁移警告；不得静默删除或重置整棵树。
5. **新节点不影响旧档**：所有 `NEW` 节点无旧记录，旧档无需补点。
6. **幂等**：以 `SkillTreeVersion=1` 标记；第二次读档只校验不迁移。

### 3.7 新主动节点的绑定方式（已实施，2026-10-22）

> 实施状态：`talent.csv.skill_id`已落地；绑定节点要求同名、`ActiveSkill`、冷却Tick与距离一致，`effect=data`必须绑定。`Commit`对绑定节点按CSV效果入队并以`TalentValue`为系数，未绑定节点继续`CastTalent`。实现与验收见[执行记录](05_execution.md)。

新增列 `talent.csv.skill_id`（空默认）。旧主动节点留空，继续走 `CastTalent` 旧分派，行为不变（M3）。新主动节点填写同名技能 ID 或独立技能 ID，`Commit` 优先按 `skill_id` 引用 `skill.csv/effect.csv` 的定义；效果组合只复用现有 Effect 类型（ApplyBuff/MaintainAura/Heal/Chain/Summon/CorpseExplosion/AreaPulse/SpawnProjectile），不新增第二套伤害路径。每个新节点的效果、来源资格与数值在实施时逐项列出，未批准前只使用隔离 QA 原型占位。

## 4. 受影响存档字段与样例

### 4.1 字段与迁移动作

| 字段 | 迁移动作 |
|---|---|
| `Talents` | 键为节点 ID、值为已学等级。沿用节点保持；退役节点删除键（点数按“等级总额−已投入”自然返还）；合并节点需明确等级换算 |
| `SkillIds` | 只保留仍存在且已学的主动节点；被退役/合并的 ID 替换或移除；数量不得超过当前星图槽位 |
| `SkillLevels` | 键为 `ability.csv` 技能，与天赋节点无关；退役旧 ability 时才需迁移 |
| `StartingSkillGranted` | 保持 `true`，迁移不得重置，否则会重复赠点 |
| `TalentBonuses` | 目标为退役节点/树的条目必须删除或改指；来源由装备系统后续接入，当前无生产写入 |
| `SkillSwapLocks` | 删除已不存在技能的键；保留仍存在技能的剩余冷却 |
| `ActiveAuraSkills` | 过滤为仍装配且仍存在的光环技能 |
| 召唤字段 | 若召唤技能被替换，按卸槽规则清理，不把旧召唤带入新定义 |

### 4.2 合成样例（仅示意，不是玩家存档）

迁移前（节选，字段名与 `JsonOptions` 的 PascalCase 一致）：

```json
{
  "Version": 4,
  "Session": {
    "Heroes": [{
      "SpecId": "warrior",
      "Level": 10,
      "Talents": { "barb_voice": 3, "barb_bash": 1, "barb_whirlwind": 2 },
      "SkillIds": [ "barb_bash", "barb_whirlwind" ],
      "SkillLevels": {},
      "StartingSkillGranted": true,
      "TalentBonuses": [],
      "SkillSwapLocks": { "barb_whirlwind": 2.5 },
      "ActiveAuraSkills": []
    }]
  }
}
```

加性迁移后（无退役、仅新增版本标记；ID/等级/冷却不变）：

```json
{
  "Version": 4,
  "Session": {
    "SkillTreeVersion": 1,
    "Heroes": [{
      "SpecId": "warrior",
      "Level": 10,
      "Talents": { "barb_voice": 3, "barb_bash": 1, "barb_whirlwind": 2 },
      "SkillIds": [ "barb_bash", "barb_whirlwind" ],
      "SkillLevels": {},
      "StartingSkillGranted": true,
      "TalentBonuses": [],
      "SkillSwapLocks": { "barb_whirlwind": 2.5 },
      "ActiveAuraSkills": []
    }]
  }
}
```

若裁决退役 `barb_whirlwind` 并合并到新节点，则必须同时：`Talents` 删除旧键、按换算写入新键、`SkillIds` 替换、`SkillSwapLocks` 改键、`ActiveAuraSkills` 过滤。任何“只删键不处理装配/冷却”的实现都会让读档校验失败或让技能凭空消失。

## 5. 迁移实施顺序（建议）

1. 冻结输入：记录当前 `config/skill`、`config/system` 及受影响代码的哈希；在 `artifacts/qa` 构造覆盖五个职业的旧档样例。
2. 先加基础设施：`talent.csv` 可选 `skill_id` 列（空默认）、`SessionSnapshot.SkillTreeVersion`、迁移函数与只读校验。此步旧档行为不变，可独立回归。
3. 再落正式目录：按批准的稳定 ID 增加技能/效果/节点行；不删除任何旧 ID（首次切换推荐加性）。
4. 切换入口：主动节点有 `skill_id` 时走数据驱动定义，否则走旧分派；确保只有一条 `SettleDamage` 路径。
5. 读档迁移：`SkillTreeVersion=0` 时执行一次映射、过滤、返还，写入 `1`；再次读档只校验，保证幂等。
6. UI/回归/Mini 验收通过后，才讨论退役旧节点与旧 `skill_XX`；退役使用独立版本号和完整映射表。
7. 全程只用 `artifacts/qa` 自动存档；不触碰玩家 `user://`。

## 6. 回滚方案

| 层 | 措施 |
|---|---|
| 配置 | 迁移前生成 SHA256 清单；旧配置已在 `tests/fixtures/legacy_skill_bd_2026_10_22/`，回滚按清单恢复 |
| 存档 | 迁移首次写盘前，把主档与备份复制为带时间戳的 `pre-skill-migration` 副本；保留现有原子写/备份机制 |
| 代码 | 迁移函数与目录切换分开提交范围；回滚代码后可用旧配置启动 |
| 版本 | `SkillTreeVersion` 只前进不后退；旧版本读到高版本时明确拒绝并提示恢复备份，不静默改数据 |
| 兼容边界 | 一旦新技能 ID 被装配，旧构建无法识别；因此**首次切换建议不退役任何旧 ID**。若必须退役，回滚要求同时恢复迁移前存档，或提供一次性反向导出工具 |
| 验证 | 回滚后跑旧全量回归与三份旧档样例，确认等级/点数/装配/冷却一致 |

## 7. 需要裁决的事项

| 编号 | 问题 | 裁决/推荐 |
|---|---|---|
| M1 | 稳定 ID 用小写现有前缀还是草案大写 | **已裁决：小写现有前缀**；草案大写仅作文档别名 |
| M2 | 首次切换加性保留 48 节点，还是按新三系重排 | **已裁决：本次直接按新三系重排**；具体布局见 3.5 |
| M3 | `barb_whirlwind`/`mage_frost_nova` 是否在原 ID 上换行为 | **已裁决：原 ID 保旧行为，新能力用新 ID**；旋风窗口为 `barb_whirlwind_window` |
| M4 | 五个初始技能是否替换为 17 技能中的对应项 | **已裁决：替换为对应新技能**；游侠→`ran_multishot`，死灵→`nec_raise_skeleton`，其余同 ID 绑定新定义 |
| M5 | 旧节点退役后的点数/装备/前置处理 | **已裁决：旧节点保持退役并归档备案**。旧树/旧定义不重新启用，旧树快照在`tests/fixtures/legacy_skill_bd_2026_10_22/`；批准布局沿用的48个稳定ID保留，不物理删除。3.6 的失败兜底仅针对重排后仍非法的旧档 |
| M6 | 技能点总上限与旧档超上限 | 维持现行“按等级获得、无独立上限”，上限另批 |
| M7 | `skill_XX` 15 个导入技能是否退出生产 `skill.csv` | **已裁决：保持退役并归档备案**。不绑定、不作为初始技能、无玩家入口，仅被显式旧回归引用；配置行本轮保留，旧`skill.csv`等快照在`tests/fixtures/legacy_skill_bd_2026_10_22/`。物理移除需迁移回归并另开版本 |

## 8. 迁移验收计划（实施时执行）

- 新增 `--skill-migration-checks`：五职业旧档映射、点数守恒、退役返还、装配/冷却/光环过滤、初始技能不重复赠点、幂等、未知 ID 明确报错、备份恢复。
- 现有 `--talent-checks`、`--class-follow-checks`、`--formula-checks`、全量 Checks。
- `dotnet build Idle-CRPG/Idle-CRPG.csproj` 0 警告 0 错误。
- Godot headless 冒烟；新技能表现另做 Mini 视觉验收，不用 Core QA 替代。
- 自动检查只写 `artifacts/qa`，不覆盖玩家进度。

## 9. 本轮边界

> 本节记录清单编制时的边界。后续实施已按 §3.5～3.7 修改配置、Core、测试与文档，详见[执行记录](05_execution.md)；A扇形、17个正式技能与三系UI已完成Mini截图检查。M5的48个稳定节点ID保留并继续生效；M7的15个`skill_XX`从正式`skill.csv`移除、仅由`LoadLegacy`叠加TEST_ONLY基线供旧回归。复盘更正见[退役备案](08_retired_content.md)。点数上限与 Proc ICD 语义仍未裁决，完整状态见[验收报告](07_acceptance.md)。

- 编制本文件时未修改任何配置、代码、测试或存档；未运行构建。
- 未决定三系布局、技能数值、点数上限、初始技能替换、旧节点退役或 `skill_XX` 去留。
- 未执行 Git 提交/推送/合并。
