# 文档索引

- [P2-3-M2 新版产品设计纲要 V1.2（非开发授权）](suggest/p2_3_m2_product_outline.md)：公共XP无等级差衰减、当前无池上限，允许在线低怪囤XP；后台正常生产，禁止擅自转离线。保留V1.1锁队/轮内成长/重生框架，更新C02与M3效率、安全验证口径；不表示模型已通过或获得编码授权。


- [P2-3-M1 内容蓝图与规模规划建议（未批准）](suggest/p2_3_m1_content_blueprint.md)：历史建议（星图先行路线已由M2取代）。31系统、16包归7主线；五专项与30分钟/2小时候选规模、23旧节点去留、星图跨系统功能成本、14张待审批裁决卡；审计工作包结构修正版附于同名资产目录。[执行归档](reviews/2026-10-24_p2_3_content_blueprint.md)。仅为设计建议，不是正式设计规范或必须执行的待办；未经用户后续明确批准不得据此实施。目录说明见 [建议归档说明](suggest/README.md)。


- [工程代码健康审计（只读）](reviews/2026-10-23_code_health_audit.md)：AUD-001~012；**P0 入口坐标带死锁**（单人队伍在门口永久停滞，存读档不可恢复，已复现并定位到 0.001/0.0001/0.00001 三处阈值死区）、P1 阈值语义分散与纯挂机 Lv.7 平台期口径、P2/P3 存档捕获/缺表降级/读档重复重建等；含环境快照、调用链、金币四分账与复跑方法。

- [P2-2-M1 全系统配置与内容资产普查](data/2026-10-24_00-03_data_p2_2_m1_inventory.md)：`P2_2_M1_PASS`。391 份配置资产（ACTIVE 142/LEGACY 9/TEST_ONLY 41/PREVIEW 2/未引用 1/残留 1/import 195）、31 个系统、4278 条内容 ID（悬挂 0）、7 项扩容缺口、14 项问题；机器字典归档在 `docs/data/2026-10-24_00-03_data_p2_2_m1_inventory/{system_inventory,config_manifest,content_registry,content_expansion_gaps,issues_and_decisions}.csv`（与报告同名同夹），只读复跑 `--inventory-audit`。

- [P2-2-M2 参数、公式与运行消费链路审计](data/2026-10-24_00-42_data_p2_2_m2_parameter_runtime_audit.md)：`P2_2_M2_PASS`。142/142 份 ACTIVE CSV、962 个列级参数台账（0 UNVERIFIED）、23 条公式、36 属性 Key（18 已投放/18 零投放）+ 10 类 Effect、23 条运行消费链、15 条 P2-3 扩容接口；纠正 M1 的天赋绑定数（16 绑定/23 旧路径）与“未投放/未消费”混淆；发现 `floor_plan` 跨章 schema 漂移（40/37/38/41/42 列，12 列无消费者）。机器表在同名文件夹内，复跑 `--param-audit`。

- [P2-2-M3 数值设计接口与内容覆盖验收](data/2026-10-24_01-25_data_p2_2_m3_design_readiness.md)：`P2_2_M3_READY_FOR_P2_3`。31 项系统的设计接口清单（权威配置/消费者/边界/验证级别）、16 个 P2-3 工作包、19 条 schema 与裁决登记；属性矩阵改为「投放/消费/运行验证」三态（18 投放/18 零投放但已消费/0 无消费者），补登 5 个零投放 Effect 分支；裁决 `floor_plan` 跨章漂移（每章 8～10 个非主键列未读取）、23 个旧 `CastTalent` 节点迁移（shot 8/shout 4/weaken 4/nova 5/leap 1/whirlwind 1）、星图扩容边界；逐技能只读事实待批准。机器表 8 份在同名文件夹，复跑 `--param-audit`。

- [核心体验模拟与数值基线](data/2026-10-23_core_experience_sim.md)：`--economy-sim` 实测 30/120/240 游戏分钟（纯挂机 vs 含成长操作）、成长时间轴、收支与效率曲线、异常与断点；统计结果保留在该单份报告中。

- [核心体验闭环就绪度审计](data/2026-10-23_core_loop_readiness_audit.md)：五系统完成度矩阵、经验/金币/天赋/星图/难度链路与断点、当前正式数值、缺失与风险清单；只读统计，未改配置与数值。

- [P0 基础规则与运行数据](reviews/2026-10-23_p0_rules_and_runtime_audit.md)：召唤物接敌容量 0→1（含女武神超时释放）、五章大地图 `travel_speed` 0.65→0.31 调平、本房战斗统计与五场景站位输入。数据见[移动速度审计](data/2026-10-23_movement_speed_audit.md)、[D03/D04 现状](data/2026-10-23_runtime_state_audit_d03_d04.md)；统计字段见[战斗观测统计](technical/combat_stats.md)。

- [章节怪物投放](technical/act_monsters.md)：49个章节身份、统治区加权池、行为偏移（范围/猎手/支援）、精英词缀与Boss技能包；[P1-5执行归档](reviews/2026-10-23_p1_5_act_monsters.md)（C1–C3；M07召唤、其余6词缀与四段特殊剧本未实施）。

- [角色自动技能 AI](design/auto_skill_ai.md)：槽序选技、条件与目标、射程外兜底、召唤名额资格、光环与多诅咒维持、GM有界诊断；[P1-4执行与审计](reviews/2026-10-23_p1_4_auto_skill_ai.md)：含C01–C12逐项结论与实施范围，外部方案原件留在 `docs/temp/` 不随工程归档。

- [Steam D2 房间入场与门口拥堵修正 V1.2](reviews/2026-10-23_room_entry_v1_2.md)：入口通行1.5U与逐单位交接、敌方警觉0.8s基础/2.0s兜底、入口内侧1.5U出生避让、GM入场诊断；含召唤物接敌容量的独立审计结论（已于 P0 修复为 1）。规则写入[索敌与入场展开](design/combat_targeting.md)、[字段合同](technical/targeting_config.md)。**V1.3（同日追加）**：修复 AUD-001 入口坐标死锁（`SpatialTolerance` 统一阈值 + `EntryMoveTarget` 门线不作终点 + `Move` 到达吸附），新增边界带与旧档恢复确定性回归；五场景与 24 格 Seed×队伍扫描无冻结。

- [跨平台Agent开发规范](technical/agent_workflow.md)：仓库规则、游戏开发Skill、平台验证和维护；[创建与自身归档](reviews/2026-10-23_agent_skill_archive.md)。

- [召唤物跟随与入场部署](technical/summon_follow.md)：非战斗跟随当前跑图领队（近战靠前、远程靠后，领队速度×100%追赶）、无跨房瞬移、进入有怪房间时近战/远程/支援入场展开、150%临时入场加速与战斗交接、`duration_seconds`生命周期（0永续/正数限时）与永续召唤脱战补员；归属仍绑定原召唤者，规则数值均为原型。
- [技能BD交互规则](design/skill_bd_interactions.md)：G01～G06；[公式提案来源](design/combat_formula_vnext_proposal.md)、[迁移计划](technical/skill_bd_plan.md)、[执行、迁移与退役归档](reviews/2026-10-22_skill_bd.md)。新公式已切换；A、B1、B2、C1、C2、C3 Core QA完成。正式五职业技能配置已完成迁移与退役清理（38节点、16个正式主动、`skill_id`绑定、新初始技能），新数值均为原型；正式技能已有临时几何表现；M5的23个旧主动节点与M7的15个`skill_XX`及无引用配套行已从正式表删除，旧回归数据隔离在TEST_ONLY夹具，预览仅展示16个正式主动。最终平衡由用户后续统一重算，Proc ICD V1.0已实现并通过专项检查；D4已确认镜像沿用现有全局弹体预算、原生命周期及不可递归规则，见[Proc ICD审计与实现](technical/proc_icd.md)。

- [战斗索敌、接敌容量与入场展开](design/combat_targeting.md)：稳定锁敌、溢出重评、无单位推挤及有怪房AI警觉；[字段合同](technical/targeting_config.md)、[执行记录](reviews/2026-10-22_combat_targeting.md)。

- [五章正式配置与V0.2补丁](design/campaign_five_acts.md)：五章地图、并行AND、来源与用户规则冲突裁定、迁移及原型边界。
- [五章验收和性能检查](reviews/2026-10-19_campaign.md)：全15地图回归、实测帧耗时、已修复bug及待人工体验项目。

- [第二至五章主城实施与待验收](reviews/2026-10-19_chapter_towns.md)：独立场景、第四章四人多职能、第五章固定NPC、旧布局迁移。

- [固定城镇、传送与最新攻略规则](design/towns_portals.md)：六NPC、先治疗后售卖、两处城镇设施、区域点解锁、自动支线与同级随机、GM通关本章。

- [第一章正式规则](design/act1.md)：V0.5地图、单营地、手动要塞与跨章支线、失败回刷及待补充开发。
- [第一章配置字典](technical/act1_config.md)：POI/逐层数据、any/all门禁及存档版本4。
- [第一章实施验收](reviews/2026-10-18_act1_combat_chapters.md#一第一章地图与自动攻略)：8×8单营地、实测结果和未投放范围。

- [地形阻断与区域前置](design/map_access.md)：不可进入格、普通地牢/要塞OR前置及旧档迁移。

- [难度、章节与独立地图](design/world_chapters.md)：3档难度、5章地图、顺序解锁、自动推进及旧档兼容。

- [现行三阶段战斗公式](design/combat_formula.md)：攻击者等级护甲K、四元素抗性、加算4+1、共享事件的混合伤害及手算验收。
- [技能、效果与增益](design/skills_effects_buffs.md)：15技能迁移、二维/20Hz适配、生命周期与保存边界。
- [属性注册与等级成长](design/attributes_levels.md)：统一属性目录、英雄/怪物等级表、表尾上限。
- [战斗配置字段](technical/combat_config.md)：属性、等级及Skill/Effect/Buff相关CSV合同。
- [2026-10-18迁移验收归档](reviews/2026-10-18_act1_combat_chapters.md#二战斗技能属性与等级迁移)：来源哈希、用户裁定、差异与验证范围。

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

- [怪物原型与实验遭遇归档](reviews/2026-10-23_monster_encounter.md)：M01–M08、T01–T08、有限召唤与收益裁定、GM技能预览和480×240 Mini、Core/Godot/实际渲染证据；E1–E3 阶段过程已并入同一文件；正式五章投放另属P1_5。

## 归档约定

- **一个功能模块只有一个 `reviews/` 文件**：同一模块的后续开发（新版本、切片、复跑、补救）更新该文件，用版本或阶段小节追加；不得新建 `_e1`、`_v2` 这类并列文件。已有拆分已合并（怪物遭遇 E1–E3、入场软部署与 V1.2）。
- 外部方案留在 `docs/temp/`，工程内不复制；专项统计与数据提取放 `docs/data/`。

## 数据统计（docs/data/）

专项统计与数据提取结果统一放 `docs/data/`：用于对当前版本的部分数据做汇总、便于后续分析或执行修正，不当作玩法规则，也不属于执行归档。

- [空间配置与显示比例审计](data/2026-10-23_spatial_configuration_audit.md)：房间/走廊/镜头/投影/显示比例的逐项实测汇总，标注单位、来源与“规划预算≠实际生成”的边界。

- [P0-A 统一模拟测试配置结构](reviews/2026-10-23_p0_a_simulation.md)：部分实施与验收缺口；A/B/C 240分钟数据。**P2-1-M2（同日追加）**：模拟工具能力矩阵与补齐——`player_policy` 版本 2 显式策略（城堡/星图/编队/天赋，同一 Tick 只执行一组）、逐 Tick 事件与金币/经验对账（六场差额 0、经验无法解释 0）、可疑停滞分级（MANUAL_GATE/XP_RATE_ZERO/COMBAT_FAILURE_LOOP/SUSPECTED_STUCK/RUNNING/UNDETERMINED）、`metadata.json` 配置指纹与业务哈希、`E_min_party`/`F_policy_probe` 场景；逐技能/攻击者采集仍待 Core 只读接口批准。**P2-1-M3（同日追加）**：`M3_TEST_ENV_PASS`——标准 7 场 + 多 Seed 30 场全部金币差额 0/经验未解释 0、同 Seed 双跑逐字节一致、源码+配置双指纹与 `stalls/` 停滞现场，详见[M3 统计与矩阵](data/2026-10-23_23-32_data_p2_1_m3_simulation_acceptance.md)。

- [P0-A 模拟玩家对照统计](data/2026-10-23_21-54_data_p0_a_simulation.md)：A/B/C 各240游戏分钟，30分钟采样；B跨章与A/C停滞实测，缺失指标明确标注。

- [P0-B合法成长与停滞归因](data/2026-10-23_22-12_data_p0_b_growth_stall.md)：五场景双轮一致；存活现场与疑似入口资格问题，未确认Core缺陷。
