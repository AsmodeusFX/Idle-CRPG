# P1-5 五章怪物与 Boss 执行归档（切片 C1）

2026-10-23。来源：用户外部交付的 P1-5 V1.0 执行包（`docs/temp/P1_5_FiveActs_Monsters_Boss_V1_0/`）。**本文件是本轮唯一工程归档**；外部方案原件留在 temp，由用户自行处置。字段合同见[章节怪物身份与区域投放池](../technical/act_monsters.md)。

范围：只做该包 C1（数据联结与普通怪投放）。C2 精英/特殊怪、C3 Boss 与四段剧本、C4 三难度与长期回归未实施，原因与依赖见文末。

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

## 三、未实施（需单独批准）

| 项 | 原因与依赖 |
|---|---|
| C2 精英词缀与 M05–M08 特殊普通怪 | 无精英系统；词缀需 Buff/Effect 组合与生命周期验证；猎手 `BacklinePriority` 目前只在隔离预览启用 |
| C3 Boss 锚点替换与经典 Boss 技能包 | 需要内容命名/美术裁定与 Boss 技能数据；特殊阶段需新的阶段调度能力 |
| C3 四段剧本（议会/五封印三守卫/三远古/巴尔五波） | 现有系统没有阶段编排与波次预算；包内 `special_stages_SOURCE.csv` 只是源分段 |
| C4 三难度投放策略表 | 需先有 C2/C3 内容，且不得与既有 `poi_challenge`/`floor_plan` 形成第二份等级权威 |
| 怪物中文名正式化 | 现为 D2 风格内容提案，商用前需 IP/命名审核，不作为美术资源需求 |

## 四、验证

| 命令 | 结果 |
|---|---|
| `pwsh tools/import_act_monsters.ps1 -Check` | 待写变更时退出码 1；写入后再次执行为 `no changes` |
| `dotnet build Idle-CRPG/Idle-CRPG.csproj` | 0 警告 0 错误 |
| `dotnet run --project tests/IdleCrpg.Checks.csproj -- --act-monster-checks` | PASS |
| `dotnet run --project tests/IdleCrpg.Checks.csproj` | 全量 PASS |
| Godot `--headless -- --smoke-test` | PASS：各 UI 冒烟子项与 `SMOKE_OK`，退出码 0 |

新增检查覆盖：49 身份具备属性/容量/掉落；五章每个统治区池覆盖且权重合计 100；加权抽样同种子可复现且分布贴合权重；世界远征进房怪物必须来自所在统治区池；legacy 第一章无池时回落原怪物表；重复身份/权重非 100/未知身份/统治区缺池四类配置均被拒绝。

## 五、变更文件

- 新增：`Scripts/Core/MonsterPools.cs`、`tests/ActMonsterPoolChecks.cs`、`tools/import_act_monsters.ps1`、`config/monster/act_monsters.csv(+.import)`、`config/chapters/act{1..5}/monster/region_pool.csv(+.import)`、本文件、[字段合同](../technical/act_monsters.md)。
- 修改：`Scripts/Core/Config.cs`、`CombatConfiguration.cs`、`ChapterConfiguration.cs`、`GameSession.cs`、`WorldConfig.cs`、`SessionSnapshot.cs`、`tests/Program.cs`、`config/combat/unit_attributes.csv`、`config/monster/monster_drop.csv`、`docs/README.md`。
- 未改动：`monsters.csv`、地图/POI/挑战等级、`poi_profile.boss_name`、伤害与护甲公式、AI 与入场规则、存档版本。
