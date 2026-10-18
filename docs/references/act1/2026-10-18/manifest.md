# 第一章来源冻结

本目录冻结V0.4、V0.5、示例HTML、自动攻略V0.3及data原件；docs/temp原件未删除。校验见sha256.csv。原始AI文本不是最终玩法授权；裁定见 [正式规则](../../../design/act1.md)。

| 来源 | 正式配置/用途 |
|---|---|
| Interactive_Map_V0.3.html及MAP.json | region、castle_region、gate、area_style；补入单营地裁定 |
| REGION.csv | areapart及前置；埋骨之地optional |
| POI.csv | dungeon、poi_profile、poi_challenge；41POI坐标/层数/三难度等级 |
| FLOOR_PLAN.csv | floor_plan；加唯一id列，保留来源字段，共207行 |
| CELL_SPAWN_PROFILES.csv | spawn_planning，保留建议，不直接替换刷怪规则 |
| DIFFICULTY_CHAPTER_LEVEL_BANDS.csv | difficulty_bands，规划参考，不改章节2～5 |
| KILL_SUMMARY.csv | 校验预算合计，不发奖励 |
| AUTO_ROUTE_SIMULATION.csv | 自动要塞/支线策略被用户手动授权规则覆盖 |

逐POI对照见 [导入审计](../../../reviews/2026-10-18/act1_v05_poi_import.csv)。运行只读取config，不读取参考归档或temp。
