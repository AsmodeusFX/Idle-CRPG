# P1-5 五章怪物与 Boss 执行归档（C1–C3）

2026-10-23。来源：用户外部交付的 P1-5 V1.0 执行包（`docs/temp/P1_5_FiveActs_Monsters_Boss_V1_0/`）。**本文件是本轮唯一工程归档**；外部方案原件留在 temp，由用户自行处置。字段合同见[章节怪物身份与区域投放池](../technical/act_monsters.md)。同一模块的后续切片按约定继续写入本文件，不再新建并列文件。

范围：C1（身份与区域投放）已完成；C2 中**行为偏移与精英词缀已完成**，M07 生产投放未做；C3 中**Boss 名称与技能包已完成**，四段特殊剧本未做；C4 只做难度门控与回归，未改等级权威。

## 一、G0 审计结论（真实代码）

| 包内要求 | 工程现状 | 判定 |
|---|---|---|
| 五章 Region/POI/Floor | `chapters/act{1..5}/world|dungeon/*.csv`，42 区/231 POI/1515 楼层记录与包内一致 | EXACT |
| 怪物身份 | 只有 `rat`、`guard` 两个正式怪物；无章节身份层 | ABSENT → 本轮新增 |
| 区域投放池 | 无；`Reveal` 从全局 `Config.Monsters` 随机 | ABSENT → 本轮新增 |
| POI/Dungeon 绑定 | `dungeon.csv.region_id` + `castle_region.csv` + `areapart.csv`；源 `REGION_NN` 与 `apNN` 五章均一一对应 | EXACT（无需新 ID） |
| Boss 锚点 | `poi_profile.boss_name` 已存在，仅少量占用；`Kills`/通关仍按 POI 计 | EXTEND（本轮不动） |
| 精英词缀 | 无精英系统，`floor_plan` 的 Elite 列只是预算 | ABSENT → C2 |
| 特殊阶段（封印/三远古/巴尔五波） | 无阶段调度器 | ABSENT → C3 |
| 三难度投放策略 | 难度只影响挑战等级与楼层预算 | EXTEND → C4 |
| 敌 AI 延迟、入场散开、受击退让 | 已实现（`AdvanceRoomEntry`、`RunRetreat`） | 保持，未改动 |
| 伤害类型 | 5 种，公式唯一 `DamagePipeline` | 保持，未新增魔伤/免疫 |

冲突处理：包内 `*_PROPOSAL.csv` 只作语义参考，**没有覆盖任何正式地图、POI、挑战等级或 Boss 名**；POI 复合键沿用现有章级目录，不引入 `ACT##:` 运行时键。

## 二、本轮实现（C1）

| 项 | 落地 |
|---|---|
| 身份表 | 新增 `config/monster/act_monsters.csv`（49 行，五章 10/10/10/8/11）；与正式 `monsters.csv` 分离 |
| 属性与容量 | `unit_attributes.csv` 增 49 行（`side=monster`、容量 0、抗性 0、远程跟随 archetype） |
| 掉落 | `monster_drop.csv` 增 149 行，按 archetype 的 `template`（rat/guard）复制，不新造经济数值 |
| 投放池 | 新增 `chapters/act{1..5}/monster/region_pool.csv`，167 行，按统治区 `apN` 加权，权重合计 100 |
| 加载与校验 | `Scripts/Core/MonsterPools.cs`：身份加载、池加载、覆盖/重复/权重/引用校验；`Config.Load` 与世界校验接入 |
| 刷怪 | `GameSession.Reveal` 按 `ActiveDungeon.RegionId` 的统治区池抽身份；无池回落原全局随机 |
| 读档 | 出生模板改用 `Config.FindMonsterSpec`（正式怪物 + 章节身份），正式存档版本不变 |
| 生成器 | `tools/import_act_monsters.ps1`（`-Check` 只比较；幂等；外部包只读） |

关键取舍（避免双轨与静默改动）：

1. **不把 49 个身份写进 `monsters.csv`**：那会改变单地牢模拟与 legacy 回归的随机集合；改为独立表 + 池，只有世界远征按区域抽取。
2. **不引入新数值**：身份属性取既有 archetype 的模板（rat/guard），血量/攻击与当前正式怪一致，避免未经批准的平衡改动。
3. **不新增运行时 ID 体系**：池键沿用 `areapart`，POI/存档键不变。
4. **不驱动 Boss**：`poi_profile.boss_name` 未改，城堡仍为占位守将与既有通关条件。

## 二之二、C2 内容层（行为与精英）

| 项 | 落地 |
|---|---|
| M05 范围 | 新技能 `mob_area_pulse`（`self` + `AreaPulse`，半径 2.2、CD 240 tick、前摇/恢复 6）；只有 archetype=M05 的 11 个身份获得 |
| M06 猎手 | 只改 `TargetPolicy=BacklinePriority`，复用既有 Special 锁与容量规则，不新增容量账本 |
| M08 支援 | 新技能 `mob_support`（`self` + `ApplyBuff ally` 半径 3，`mob_support_buff` 攻速 +20%）；只给 M08 身份 |
| 配置入口 | `monster/act_behaviors.csv`（`monster_id,policy,skill_id`），生成器按 archetype 产出，共 10 行 |
| 精英词缀 | `monster/elite.csv`（`difficulty,affix_id,buff_id,policy,weight,chance_pct`）：HARDENED(armor+6)、VIGOR(移速+0.6)、ARCANE_SHIELD(shield 40)、HUNTER(后排优先)；normal 20%、nightmare 30%、hell 40%，每房最多一个、Boss 房不参与 |
| Buff | 新增 `mob_support_buff`、`elite_hardened`、`elite_vigor`、`elite_shield`；护甲/移速用 `flat` 操作（属性规则只允许 base/flat/pct） |

行为只在世界远征生成时套用；单地牢模拟、legacy 夹具与 GM 预览不掷精英骰，旧回归不受影响。精英只额外给 Buff 或策略，不新增乘区、不改掉落。

## 二之三、C3 Boss 层

- **名称**：`poi_profile.boss_name` 在源数据中已有命名（血乌、拉卡尼休、都瑞尔等），本轮核对后**未覆盖**；仍为占位的“待定”保持原状，等后续内容裁定。
- **技能包**：`monster/boss_skill.csv`（`poi_id,skill_id`）按 `boss_mechanics` 的 `basic_target_policy_semantic` 映射：远程语义 → `mob_area_pulse`，其余 → `mob_heavy`（单体重击，系数 1.8）。生成时只加给 `Boss=true` 本体，普通怪不继承。
- **未做**：四段特殊剧本（议会/五封印三守卫/三远古/巴尔五波）。三远古需要同房 3 个固定角色、巴尔需要波次门控与存档现场、封印需要“三守卫全灭后生成 Boss”，都要新增阶段状态与存档迁移，单独立项。

## 三、未实施（需单独批准）

| 项 | 原因与依赖 |
|---|---|
| M07 召唤巫师生产投放 | 敌方召唤要求进房预留槽位、来源/规则ID随快照保存与重建；当前只在隔离预览注册，生产存档合同需单独立项 |
| 其余 6 个精英词缀（FIERY/FROST/LIGHTNING/VENOM/DEATH_BURST/LEADER_AURA） | 需要“命中附带元素伤害”“死亡触发链”“来源光环”等新机制与触发深度限制；本轮只用既有 Buff/策略承载力 |
| C3 四段剧本（议会/五封印三守卫/三远古/巴尔五波） | 需要阶段状态机与存档现场（三远古 3 固定角色、封印全灭后生成 Boss、巴尔波次门控）；`special_stages_SOURCE.csv` 只是源分段，不含组成 |
| 每个 Boss 的专属机制包 | 现有只按“远程/近战”二分映射到 2 个复用技能；逐 Boss 数值与技能组合需内容裁定 |
| C4 长期挂机与三难度调平 | 难度仅控制精英出现率；等级与楼层仍由 `poi_challenge`/`floor_plan` 唯一决定，未做数值调平与长时 soak |
| 怪物中文名正式化 | 现为 D2 风格内容提案，商用前需 IP/命名审核，不作为美术资源需求 |

## 四、验证

| 命令 | 结果 |
|---|---|
| `pwsh tools/import_act_monsters.ps1 -Check` | 待写变更时退出码 1；写入后再次执行为 `no changes` |
| `dotnet build Idle-CRPG/Idle-CRPG.csproj` | 0 警告 0 错误 |
| `dotnet run --project tests/IdleCrpg.Checks.csproj -- --act-monster-checks` | PASS |
| `dotnet run --project tests/IdleCrpg.Checks.csproj -- --act-content-checks` | PASS |
| `dotnet run --project tests/IdleCrpg.Checks.csproj` | 全量 PASS |
| Godot `--headless -- --smoke-test` | PASS：各 UI 冒烟子项与 `SMOKE_OK`，退出码 0 |

新增检查覆盖：49 身份具备属性/容量/掉落；五章每个统治区池覆盖且权重合计 100；加权抽样同种子可复现且分布贴合权重；世界远征进房怪物必须来自所在统治区池；legacy 第一章无池时回落原怪物表；重复身份/权重非 100/未知身份/统治区缺池四类配置均被拒绝。内容层另覆盖：M05/M06/M08 行为只落在对应身份、Boss 技能只加 Boss 本体、三难度精英规则存在且出现率一致、精英掷骰同种子稳定且真实产生 Buff/策略、Boss 房不出精英、未知行为技能/无效果词缀/未知 Boss 技能均被拒绝。

## 五、变更文件

- 新增：`Scripts/Core/MonsterPools.cs`、`Scripts/Core/MonsterContent.cs`、`tests/ActMonsterPoolChecks.cs`、`tests/ActMonsterContentChecks.cs`、`tools/import_act_monsters.ps1`、`config/monster/{act_monsters,act_behaviors,elite,boss_skill}.csv(+.import)`、`config/chapters/act{1..5}/monster/region_pool.csv(+.import)`、本文件、[字段合同](../technical/act_monsters.md)。
- 修改：`Scripts/Core/Config.cs`、`CombatConfiguration.cs`、`ChapterConfiguration.cs`、`GameSession.cs`、`WorldConfig.cs`、`SessionSnapshot.cs`、`tests/Program.cs`、`tests/LegacySkillConfig.cs`、`config/combat/unit_attributes.csv`、`config/monster/monster_drop.csv`、`config/skill/{skill,effect,buff}.csv`、`docs/README.md`。
- 未改动：`monsters.csv`、地图/POI/挑战等级、`poi_profile.boss_name`、伤害与护甲公式、AI 与入场规则、存档版本。
