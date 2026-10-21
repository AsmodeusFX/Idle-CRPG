# 配置字段字典

五章正式地图：map_policy增加town_regions（分号分隔安全格集合）、version=3；gate增加kind（外观类型，进入权限仍由区域推导）；floor_plan兼容FixedUniqueActorCount、ScriptedWaveCount、ScriptedWaveMonsterTarget、StageMode，波次目标为总怪量。字段、导入器与规划边界见 [五章合同](../design/campaign_five_acts.md)。legacy目录仅QA旧回归，不是正式地图。

固定城镇town_scene/town_npc/town_prop、传送portal_rule及各章waypoint字段和校验见 [城镇传送配置](../design/towns_portals.md)。

最新扩展：areapart.csv新增unlock_mode（any/all，缺列默认any），显式地牢及旧统治区前置均按此聚合；不再限定OR。正式第一章目录chapters/act1（8×8、独立营地），根旧世界表仅保留回归用途。POI、逐层计划、门禁与失败阈值字段见 [第一章配置](act1_config.md)，覆盖下文旧目录/尺寸描述。

地形扩展：`world/region.csv` 新增必填enterable（0/1），terrain新增mountain/ocean且二者必须不可进入；阻断格仍覆盖矩形，但不得关联村庄、地牢或统治城堡。新增 `world/region_prerequisite.csv` 字段id,region_id,required_dungeon_id，唯一行ID、可进入格引用、本章地牢引用；同区域所有行OR合并并覆盖旧areapart前置，可只有表头。规则、校验和迁移见 [地形与前置](../design/map_access.md)。

章节扩展：`world/difficulty.csv`字段id,name，必须按normal/nightmare/hell三行排列；`world/chapter.csv`字段id,name,terrain,width,height,directory，必须按act1～act5五行排列。地形为mixed或grass/desert/tropical/coast/snow/hell；尺寸1～64且须与本章world.csv和region覆盖一致。目录`.`保留第一章，其他目录须在config内。各章地图表的字段沿用下表，物品、NPC模板、刷怪和战斗数值共享根配置，详见 [章节地图配置](../design/world_chapters.md)。

所有 CSV 为 UTF-8，按功能放在 `Idle-CRPG/config/` 子目录。首行为英文表头，字段不含逗号，数字使用小数点。ID 唯一、跨表引用有效，缺字段直接报错。CSV 的 Godot importer 使用 keep，不作为翻译资源。

星图三表使用迁移自 Idle-Diablo 的严格 CSV 解析器，支持引号内逗号、多行文本和双引号转义；其余已有简表仍按上述限制。星图的多值字段使用分号。

`.translation` 是 Godot 的多语言翻译资源，不是游戏数值配置。早期配置 CSV 被默认 `csv_translation` 导入器误识别，生成的 18 个无引用翻译文件已移出工程，归档到根目录忽略的 `artifacts/legacy-csv-translations/`。config 仅保留配置 CSV 与其 `.csv.import` 文件；后者是必要的导入元数据，应随 CSV 提交，内容使用 `importer="keep"`，防止再次误导入。未来正式本地化资源统一放 `Idle-CRPG/Assets/Localization/`。

| 表 | 字段 | 含义与约束 |
|---|---|---|
| world/world.csv | width,height,start_region_id | 矩形尺寸，各为 1–64，当前 16×16；出生区域引用 |
| world/world.csv | travel_speed,village_speed,trade_seconds,trade_range | 世界坐标每秒区域距离、村庄移速、交易秒数、交互范围；均为正数 |
| world/region.csv | id,x,y,name,terrain | 正区域 ID、网格坐标、名称、地貌；坐标不重复且覆盖整个矩形。地貌限 snow/tropical/desert/coast/grass/hell |
| world/castle_region.csv | region_id,castle_id | 每区恰好一行统治关联；城堡必须存在且包含其所在区域 |
| world/areapart.csv | id,castle_id,initially_unlocked | 统治区唯一 ID、城堡引用、默认开放 0/1；统治区和城堡一一对应，仅出生统治区为 1 |
| world/areapart_prerequisite.csv | areapart_id,required_areapart_id | 每行一个可选前置，多个前置按 OR 判定：任一前置的城堡被攻克即可；必须共边接壤，无重复边/自引用/失效引用，所有统治区按 OR 从初始区可达 |
| dungeon/dungeon.csv | id,name,region_id,kind | 地牢唯一 ID、名称、区域、normal/castle；每格可无普通地牢，至多一个城堡，每统治区至少一个普通地牢 |
| dungeon/dungeon.csv | level_min,level_max,x,y | 临时等级区间，1≤下限≤上限；区域内入口坐标，均在 (0,1) |
| dungeon/dungeon_rule.csv | floors_min,floors_max,rooms_min,rooms_max | 层数 1–20、每层房间数 3–20，包含安全起点；下限不大于上限 |
| dungeon/dungeon_rule.csv | room_width_min,room_width_max,room_height_min,room_height_max | 结构生成时抽取的实际房间长宽，整数 4–10，下限不大于上限；默认宽 6–9、高 5–8 |
| dungeon/dungeon_rule.csv | event_chance | 普通房间事件概率，0–1 |
| dungeon/dungeon_rule.csv | revive_seconds,revive_range,revive_hp_ratio | 正复活秒数、正交互距离、恢复最大 HP 比例 (0,1]；当前 3、0.65、0.5 |
| hero/heroes.csv | id,name,hp,attack,defense,speed,range,cooldown | 职业与基础属性；HP、攻击、移速、射程、攻击间隔为正，防御非负 |
| monster/monsters.csv | id,name,hp,attack,defense,speed,range,cooldown | 怪物基础属性，约束同 hero |
| monster/spawn_rule.csv | count_min,count_max,level | 独立数量上下限与基础等级，初始 1、5、1；数量上限含 BOSS |
| hero/herolevel.csv、monster/monsterlevel.csv | 英雄累计攻击/HP加值、怪物攻击/HP倍率 | 见 [战斗配置字段](combat_config.md)；替代旧spawn_rule属性成长列，护甲不随等级成长 |
| monster/spawn_rule.csv | boss_hp,boss_attack,xp_base,boss_reward | BOSS 属性倍率≥1，基础经验正整数，BOSS 经验/物品价值倍率正整数 |
| monster/spawn_rule.csv | drop_chance_multiplier,value_per_level | 基础掉率倍率、每级金币/物品售价增长，非负 |
| monster/monster_drop.csv | monster_id,item_id,chance,quantity_min,quantity_max | 怪物引用、物品引用、概率 0–1、正整数数量范围；每行独立掷骰 |
| item/itemtype.csv | id,name,kind,auto_sell | 类型唯一 ID、名称、currency/junk、自动出售 0/1；当前仅杂物可自动出售 |
| item/item.csv | id,name,type_id,sell_price | 唯一 ID、名称、类型引用、非负整数售价；唯一货币为 gold，金币不出售 |
| village/village.csv | id,name,region_id,x,y,npc_id | 村庄、所属区域、区域内坐标、NPC 引用；每区恰好一个村庄 |
| village/npc.csv | id,name,kind,x,y | NPC 模板、名称、trade、村庄局部坐标；当前 256 个村庄共用交易 NPC 模板 |
| audio/audio.csv | id,path,kind,volume_db,min_gap | 音频 ID、res://Assets/Audio/ 资源路径、music/sfx、单音增益 dB、非负限频秒数；恰好一个 music |
| system/window.csv | width,height | 窗口尺寸预设，至少 800×500；等比缩放完整界面 |
| system/client.csv | autosave_min_seconds,checkpoint_seconds | 关键变更保存最短间隔、周期检查点秒数，均为正且检查点不少于最短间隔；默认 0.5/5 |
| system/camera.csv | follow_rate,transition_rate,dungeon_max_zoom,world_zoom,world_units | 跟随/入房指数响应、地牢缩放上限、世界缩放、每区场景单位，均为正，world_units≥12 |
| gm/gm.csv | gold_grant,experience_grant | GM 单次金币及每名上阵英雄经验发放量，正整数且不超过 1000000；原型调试默认均为 10000 |
| gm/gm.csv | monster_scale_steps | 分号分隔倍率档位，至少两档，有限、严格递增，范围 0.5–50，必须包含正常倍率 1；当前 0.5;1;2;3;5;8;12;20;50 |
| gm/gm.csv | count_step,count_limit | GM 数量增减正整数步长、额外数量绝对值上限；步长不得超过上限，上限不超过 999。实际每房总数仍限制在 1–1000 |
| world/obstacle.csv | id,kind,x,y,width,height | 唯一 ID、水面 water/岩石 rock、全局区域坐标及正矩形尺寸；边界不得越出世界，不能阻挡地牢/村庄入口导航格 |
| star/star.csv | id,name,max_level,cost_currency,cost | 稳定 ID、玩家名、正最大等级、当前 gold、从 1 级到满级的分号整数价目；费用非负，数量等于 max_level |
| star/star.csv | attr_id,operation,value_per_level,icon | 首版 attack/hp、flat、每级非负有限固定贡献、attack/defense/utility。占位属性/操作为空且数值为 0，不能购买；首批节点每级 +1 |
| star/star_layout.csv | id,col,row,prereq,label | 与内容表一一对应；整数格子坐标可负、不重叠；分号前置 ID 任一点亮即可；唯一根、无环；label 仅编辑显示 |
| star/star_plan.csv | id,intent | 编辑侧车，不参与启动运行；标准 CSV 可转义逗号/引号/换行。缺文件可按空意图编辑，存在但损坏拒绝保存 |

当前临时世界有 256 区、256 村庄、251 普通地牢、35 城堡；城堡位置参考截图错位排列，统治区为连续的 2–12 格不规则区域，每个统治区有 3–10 个普通地牢，城堡另计。数量由关联行数决定，不在 region 重复维护。6 个边缘格不配置普通地牢，仍保留村庄。

当前 areapart 为 ap1–ap35，与 c1–c35 一一关联；ap1 默认开放，通关后开放 ap2、ap3、ap4。前置表包含实际接壤关系（非初始区的所有邻区均为候选前置），同一统治区列出的任意一个前置城堡通关均可开放该区。没有列出的条件不会隐式补全。相邻关系的双向引用是合法的，按 OR 可达性检查避免无入口闭环。生成工具 tools/Generate-WorldLayout.ps1 使用固定锚点、seed 生长连续不规则区域并按邻接层次编号，支持复现 CSV；游戏不在运行时执行生成器。

## 首次进房刷怪快照

实际数量 = 随机整数 [count_min + MinBonus, count_max + MaxBonus]。实际怪物等级 = 地牢等级区间内随机整数 + level − 1 + LevelBonus。科技增量独立、非负，合并后数量下限不得超过上限，总数不得超过 1000；当前没有科技加点 UI。楼层和英雄等级不参与刷怪。

生命 = 基础生命 × monsterlevel对应等级HP倍率 × BOSS生命倍率；攻击同理。护甲 = 模板基础防御，不随等级增加。经验 = xp_base × 等级 × BOSS奖励倍率。等级表末行即最高等级，超范围生成拒绝。

实际掉率 = min(1, chance × drop_chance_multiplier × 科技掉率倍率)。价值倍率 = [1 + value_per_level × (等级−1)] × 科技价值倍率 × BOSS奖励倍率。金币数量及杂物单价乘价值倍率向上取整；杂物数量不额外乘等级。怪物的经验、掉率和价值在生成时锁定，物品单价在拾取时锁定。售卖只结算锁定价格 × 数量。

生成和奖励成长唯一实现位于 MonsterSpawning 和 GameSession；伤害公式唯一实现为DamagePipeline，扣血及击杀奖励应用入口为GameSession.SettleDamage。属性注册、战斗事件与技能八表见 [战斗配置字段字典](combat_config.md)。数值均为原型配置，不代表最终平衡。

## 独立主城配置（2026-10-19）

| CSV | 字段与校验 |
|---|---|
| village/town_scene.csv | id,chapter_id,name,width,height,entry_x,entry_y,exit_x,exit_y,interaction_range,service_seconds，以及新增 theme,layout_version,return_x,return_y,waypoint_x,waypoint_y,exit_name；每章唯一场景，尺寸16–64，交互范围(0,1]，服务时间>0；主题camp/sand/docks/sanctum/snow；布局版本非负整数。入口与出口不再限定东侧。每章独立回城和出发阵坐标，三人落点均检查可走及可达 |
| village/town_zone.csv | scene_id,id,kind,x,y,width,height；新增可走地板表，矩形并集决定区域；kind=stone/wood/bridge/stairs/sand/snow；尺寸≥1，不越场景边界，同场景ID唯一；camp使用原有边界，其余主题必须有地板 |
| village/town_npc.csv | scene_id,id,name,role,x,y,description；role现在支持分号多职能，允许heal/mercenary/sell/gamble/travel/identify/magic/dialogue；单个角色内不得重复；heal/mercenary/sell/gamble/identify均须恰好一个提供者，travel可0或1（无实体时由出口承担），magic/dialogue可额外配置。每场景NPC ID唯一，名字非空，所有服务点可达；不强制六人 |
| village/town_prop.csv | scene_id,id,kind,x,y,width,height,blocks；建筑矩形在场景范围内、正尺寸、ID唯一；blocks占地增加0.2单位导航余量，与建筑绘制共用坐标。新增宫殿/房屋/码头/船只/石柱/圣火等程序绘制原型 |
| village/portal_rule.csv | phase_seconds,use_range继续定义共用表演时间及使用距离。历史town_x/town_y/waypoint_x/waypoint_y保留兼容列；正式城镇坐标以town_scene各章字段为准 |

上述 CSV importer 均为 keep，正式运行不读 TownDesign 的 JSON 或 PNG。TownLayoutVersion 只保存布局版本，几何和职能仍来自CSV；迁移与边界见 design/towns_portals.md。

2026-10-21：鲁高因town_scene尺寸48×40、layout_version=2；town_zone扩建主城区、大道、广场与港口步道，town_prop及town_npc使用新布局坐标。未增加CSV字段，版本1及更早现场走既有安全迁移。五章外围补绘只读视口与主题，不扩展town_zone的导航边界。
## 英雄解锁与扩编字段（2026-10-21）

`hero/hero_unlocks.csv`：`id` 必须与 heroes.csv 职业 ID 一一对应；`kills` 为非负整数，0表示初始可用，否则达到本轮累计击杀后可上阵。当前仅支持累计击杀条件，新增职业必须同步补齐 heroes、hero_unlocks、combat/unit_attributes 三表；职业数量可扩展，最大同时上阵五人。

星图 `attr_id` 新增 `party_slots`，`operation=flat`，必须 `max_level=1`、`value_per_level=1`。当前投放四节点，形成一条从根开始的扩编链，布局和意图仍分表。效果不影响攻击或生命。存档 Heroes/RoomEntry/VillagePaths 与救援索引按实际人数校验，范围1～5；ViewMode新增4（英雄页），原0～3保持兼容。

## 编队计划（2026-10-21）
`village/town_npc.csv` 的 role 新增 `formation`，每章恰好一位；当前与凯恩的 identify 职能共用NPC，可通过配置调整。目标位置、有效交互范围、服务读条复用城镇NPC、interaction_range、service_seconds，不引入另一套瞬时变阵入口。TownVisitPhase枚举末尾新增Formation=3，旧Heal/Sell/Exit值不变。

编队容量由星图扩编加旧档最低容量推导，上限5；布局编辑不能使现有或已确认计划超过容量。旧增员命令DeployHero统一转为RequestFormation，不再立即增加成员。

## 技能与天赋树（2026-10-21）
新增system/config.csv通用规则、skill/talent.csv三系内容/布局/前置与效果，详见[talent_config.md](talent_config.md)。星图增加全队主动槽、洗点解锁及折扣效果，CSV继续使用keep导入。

### 英雄经验倍率字段

`hero/experience_level_rate.csv`（UTF-8，importer=keep）：`id` 为英雄等级减怪物等级的整数差（−10000～10000）；`percent` 为经验百分比整数（0～10000，100表示100%）。必须连续覆盖负数、0和正数，0行固定100%，等级差增大时倍率不得递增。超出表范围沿用边界行。当前原型最高300%、英雄高出怪物5级起0%。
