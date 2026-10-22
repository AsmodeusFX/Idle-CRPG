# 文档索引

- [怪物实验遭遇E1实施与验收](reviews/2026-10-22_monster_encounter_e1.md)：M01–M04、T01–T04隔离Core夹具；实机视觉验收待完成。

- [召唤物跟随与入场部署](technical/summon_follow.md)：非战斗跟随当前跑图领队（近战靠前、远程靠后，领队速度×100%追赶）、无跨房瞬移、进入有怪房间时近战/远程/支援入场展开、150%临时入场加速与战斗交接、`duration_seconds`生命周期（0永续/正数限时）与永续召唤脱战补员；归属仍绑定原召唤者，规则数值均为原型。
- [技能BD交互规则](design/skill_bd_interactions.md)：G01～G06；[公式提案来源](design/combat_formula_vnext_proposal.md)、[迁移计划](technical/skill_bd_plan.md)、[用户裁决](reviews/2026-10-22_skill_bd/04_approval_requests.md)、[执行与验收](reviews/2026-10-22_skill_bd/05_execution.md)、[迁移前置清单](reviews/2026-10-22_skill_bd/06_migration_inventory.md)、[验收报告](reviews/2026-10-22_skill_bd/07_acceptance.md)、[旧内容退役备案](reviews/2026-10-22_skill_bd/08_retired_content.md)。新公式已切换；A、B1、B2、C1、C2、C3 Core QA完成。正式五职业技能配置已完成迁移与退役清理（38节点、16个正式主动、`skill_id`绑定、新初始技能），新数值均为原型；正式技能已有临时几何表现；M5的23个旧主动节点与M7的15个`skill_XX`及无引用配套行已从正式表删除，旧回归数据隔离在TEST_ONLY夹具，预览仅展示16个正式主动。最终平衡由用户后续统一重算，Proc ICD V1.0已实现并通过专项检查；D4已确认镜像沿用现有全局弹体预算、原生命周期及不可递归规则，见[Proc ICD审计与实现](technical/proc_icd.md)。

- [战斗索敌、接敌容量与入场展开](design/combat_targeting.md)：稳定锁敌、溢出重评、无单位推挤及有怪房AI警觉；[字段合同](technical/targeting_config.md)、[来源与执行记录](reviews/2026-10-22_combat_targeting/Execution_Record.md)。

- [五章正式配置与V0.2补丁](design/campaign_five_acts.md)：五章地图、并行AND、来源与用户规则冲突裁定、迁移及原型边界。
- [五章验收和性能检查](reviews/2026-10-19_campaign.md)：全15地图回归、实测帧耗时、已修复bug及待人工体验项目。

- [第二至五章主城实施与待验收](reviews/2026-10-19_chapter_towns.md)：独立场景、第四章四人多职能、第五章固定NPC、旧布局迁移。

- [固定城镇、传送与最新攻略规则](design/towns_portals.md)：六NPC、先治疗后售卖、两处城镇设施、区域点解锁、自动支线与同级随机、GM通关本章。

- [第一章正式规则](design/act1.md)：V0.5地图、单营地、手动要塞与跨章支线、失败回刷及待补充开发。
- [第一章配置字典](technical/act1_config.md)：POI/逐层数据、any/all门禁及存档版本4。
- [第一章实施验收](reviews/2026-10-18/act1_implementation_audit.md)：来源归档、实测结果和未投放范围。

- [地形阻断与区域前置](design/map_access.md)：不可进入格、普通地牢/要塞OR前置及旧档迁移。

- [难度、章节与独立地图](design/world_chapters.md)：3档难度、5章地图、顺序解锁、自动推进及旧档兼容。

- [现行三阶段战斗公式](design/combat_formula.md)：攻击者等级护甲K、四元素抗性、加算4+1、共享事件的混合伤害及手算验收。
- [技能、效果与增益](design/skills_effects_buffs.md)：15技能迁移、二维/20Hz适配、生命周期与保存边界。
- [属性注册与等级成长](design/attributes_levels.md)：统一属性目录、英雄/怪物等级表、表尾上限。
- [战斗配置字段](technical/combat_config.md)：属性、等级及Skill/Effect/Buff相关CSV合同。
- [2026-10-18迁移验收归档](reviews/2026-10-18/combat_skill_migration.md)：来源哈希、用户裁定、差异与验证范围。

- [主界面五区构图](design/ui_layout.md)：功能标签、战斗视口、货币及独立交互卡、五席角色状态和药剂占位。
- [GM 调试与编辑器菜单](design/gm.md)：当局资源与战斗调试、临时参数边界、上下分区及编辑器入口。

- [地牢与战斗规则](design/dungeon_combat.md)：用户确认需求与首版默认规则；含动态高速跑图领队、门前近战先锋、超时直接开门与清房快速通过。
- [世界与自动远征](design/world.md)：世界推进、城堡统治、村庄交易、物品和独立刷怪规则；刷怪规则以此文档最新修订为准。
- [镜头与连续世界](design/camera_world_view.md)：房间固定战斗镜头、连续室外场景、避障及城堡交互。
- [工程架构](technical/architecture.md)：职责、运行与验证。
- [CSV 字段](technical/config.md)：字段与约束。
- [存档、设置与音频](technical/persistence_settings.md)：自动保存、恢复、重置、设置界面和音频资源管理。
- [星图与 GM 节点编辑器](design/star_map.md)：金币加点、全队属性、三表结构、可视化编辑与配置保存。

参考：用户提供的《末日危机2》截图及 https://www.u77game.net/h5/100.html 。参考空间探索和自动战斗体验，不复制其资源或未确认数值。

- [角色技能与天赋树](design/talent_trees.md)：三系投入、技能装配与冷却、星图解锁/洗点、正式五职业三系与`skill_id`绑定、旧档`SkillTreeVersion`迁移；[字段字典](technical/talent_config.md)。

- 五职业基础树、初始技能占点/可洗、室外队列跟随：见[天赋树](design/talent_trees.md)、[镜头与世界](design/camera_world_view.md)和[字段合同](technical/talent_config.md)。
