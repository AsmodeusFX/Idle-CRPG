# 第一章配置与状态边界

正式第一章目录为 `Idle-CRPG/config/chapters/act1/`，由根world/chapter.csv的directory指定；根目录旧16×16世界表仅供Config.LoadLegacy及回归检查，不是正式第一章。所有CSV为UTF-8，importer=keep。运行规则见 [第一章](../design/act1.md)。

| 相对路径 | 字段及约束 |
|---|---|
| world/map_policy.csv | id,value；town_region_id=36，main_clear_poi=A08-C01，version=2。营地必须可走且是出生格；正式地图仅一个村庄且位于营地 |
| world/region.csv | id,x,y,name,terrain,enterable；x/y零基，坐标完整覆盖8×8；阻断格不得关联地牢/村庄/统治归属 |
| world/gate.csv | id,from_region,to_region；无序端点对唯一、共边、可走、不同归属；双向通行仍受区域门禁限制 |
| world/areapart.csv | id,castle_id,initially_unlocked,unlock_mode；unlock_mode为any/all；省略列兼容旧配置时默认any，空值或其他值拒绝 |
| world/region_prerequisite.csv | id,region_id,required_dungeon_id；同统治区各格条件合并；有显式条件时完整覆盖旧areapart_prerequisite，按该区域unlock_mode聚合 |
| world/area_style.csv | id为城堡ID；region_index为正整数；name；color为6位十六进制。负责区域底色，不覆盖已占领/计划攻击等状态色 |
| dungeon/poi_profile.csv | id为POI；optional、required_for_castle为0/1；floors为1～20；auto_order非负；boss_name、interior；status为prototype/blocked。required_for_castle只控制普通地牢的攻城资格 |
| dungeon/poi_challenge.csv | id唯一；poi_id,difficulty(0/1/2),challenge,recommended；每POI每难度恰一条，等级正数；挑战等级不随英雄自动涨怪 |
| dungeon/floor_plan.csv | 每行唯一id，PoiId+DifficultyId+FloorIndex唯一；DifficultyId为NORMAL/NIGHTMARE/HELL；按poi_profile层数完整连续覆盖；FloorDisplayName、MonsterLevel、BossActorCount用于实际楼层内容 |
| dungeon/floor_plan.csv 规划列 | LayoutRows/Cols、ActiveCellTarget及七类CellCount；格数和与容量校验；PlannedKillMin/Target/Max需单调并符合纯预算函数。BossActorCount最多1，不按Arena面积重复计Boss |
| dungeon/difficulty_bands.csv | 来源15个难度×章节区间；唯一组合、正数且上下界有序。仅规划目录，不替换章节2～5运行等级 |
| dungeon/spawn_planning.csv | 原始三难度密度建议保留；当前不作为运行时刷怪数量权威表，floor_plan各行预算独立复算 |
| 根world/recovery.csv | id,value；failure_threshold整数1～100，当前2 |

预算纯函数：CombatCellCount×普通怪/格 + EliteCellCount×(精英/格+护卫/格) + BossActorCount×(1+Boss护卫)。Entry、Corridor、Event、Exit不生成预算收益；该函数不修改游戏资源或进度。实际奖励只依赖已有战斗、救援、拾取和交易事实。

配置加载校验完整引用、拓扑、关口和区域推进可达性；AND不能靠OR式的可达判断绕过，模拟从出生营地逐轮应用真实门禁和关口。any/all可在同一地图不同区域混用。不支持同一行内嵌套AND/OR表达式。

## 存档

SaveDocument版本4，SessionSnapshot.CampaignVersion=2。WorldSnapshot.MapVersion=2区别正式8×8第一章与旧16×16标识，不把旧cN进度误映射到Axx-xxx。LatestMapIndex表示最新主线地图；ActiveBranch、BranchQueue、SuspendedWorld保存支线和被暂挂主线；历史地图允许主线完成而支线未清。

失败计数Failures、PausedAtLevel、RetryCastles、TeamLevel与RecoveryRandomState保存回刷决策。Floor记录Seed、ActualSpawned、ActualKillFacts、ElapsedTicks，仅用于实际运行报告，不是奖励凭据；战斗仍按进房检查点重开。

启动先尝试主档、再备份；均无法恢复时，先复制不可读档为.unreadable-时间戳，再自动重置并显示提示。单纯写盘权限/磁盘错误不等同地图不兼容，不因此删除进度。详情见 [存档规则](persistence_settings.md)。自动检查及截图只使用artifacts/qa专用存档。

## 来源映射（历史）

第一章配置由 V0.4/V0.5 源包一次性导入，映射关系如下。原始 HTML、JSON、CSV 与 sha256 清单属于参考文献，已于 2026-10-23 从工作区删除（仅保留在 Git 历史）；运行时只读取 `Idle-CRPG/config`，不读参考归档或 `docs/temp`。

| 来源 | 目标配置/用途 |
|---|---|
| `Interactive_Map_V0.3.html` / `MAP.json` | `world/region.csv`、`world/castle_region.csv`、`world/gate.csv`、`world/area_style.csv`；并入单营地裁定 |
| `REGION.csv` | `world/areapart.csv` 及前置；埋骨之地 optional |
| `POI.csv` | `dungeon/dungeon.csv`、`poi_profile`、`poi_challenge`；41 POI 坐标、层数与三难度等级 |
| `FLOOR_PLAN.csv` | `dungeon/floor_plan.csv`；补唯一 `id` 列，保留来源字段，共 207 行 |
| `CELL_SPAWN_PROFILES.csv` | `dungeon/spawn_planning.csv`；只保留建议，不替换刷怪规则 |
| `DIFFICULTY_CHAPTER_LEVEL_BANDS.csv` | `dungeon/difficulty_bands.csv`；规划参考，不改章节 2～5 |
| `KILL_SUMMARY.csv` | 校验预算合计，不发奖励 |
| `AUTO_ROUTE_SIMULATION.csv` | 自动要塞/支线策略；被“玩家手动授权”裁定覆盖 |

原始 AI 文本不是玩法授权，最终以 [第一章规则](../design/act1.md) 与本字段字典为准。
