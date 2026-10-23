# Idle-CRPG项目路由

以仓库根目录AGENTS.md为项目约束来源。本文件是导航，不重复维护全部业务规则。

| 修改范围 | 先读（仓库根相对路径） |
|---|---|
| 所有开发 | docs/README.md、docs/design/dungeon_combat.md、docs/design/world.md、docs/technical/architecture.md |
| 战斗/容量/召唤 | docs/design/combat_targeting.md、docs/technical/targeting_config.md、docs/technical/summon_follow.md |
| 公式/技能 | docs/design/combat_formula.md、docs/design/skills_effects_buffs.md、docs/technical/combat_config.md |
| 空间/拥堵/镜头 | docs/design/camera_world_view.md、docs/data/2026-10-23_spatial_configuration_audit.md |
| 存档/设置 | docs/technical/persistence_settings.md |
| 五章地图 | docs/design/campaign_five_acts.md、docs/reviews/2026-10-19_campaign.md |
| 城镇/传送 | docs/design/towns_portals.md |
| 星图/编辑器 | docs/design/star_map.md |

工程位于Idle-CRPG/；Core使用System.Numerics.Vector2，不引用Godot；UI通过GameSession命令改变状态。正式配置在config功能目录，legacy仅用于明确旧回归。FloorPlans是规划预算，FloorReports实际生成量不能由预算代替。

战斗不是一维X轴模型，显示不是Camera3D。空间调整先核对代码当前值，报告公式、单位、加载来源和实际现场。接敌Locks是唯一预约账本，友方攻击目标不是第二份容量。屏幕偏移不改变真实命中距离。未经新授权不恢复碰撞或全场目标重排。

正式存档重开未结束房间而保留已获得成长，完整快照用于测试确定性。QA参数的存档隔离必须先核对ClientSettings.cs。不得以截图夹具的怪物数、位置或HP证明正式随机房间的行为。

本Skill随仓库归档；在其他项目复用时替换此路由，勿继承本游戏的职业、容量、地图或交易规则。