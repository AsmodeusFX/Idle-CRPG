# 配置字段字典

所有 CSV 为 UTF-8，按功能放在 `Idle-CRPG/config/` 子目录。首行为英文表头，字段不含逗号，数字使用小数点。ID 唯一、跨表引用有效，缺字段直接报错。CSV 的 Godot importer 使用 keep，不作为翻译资源。

`.translation` 是 Godot 的多语言翻译资源，不是游戏数值配置。早期配置 CSV 被默认 `csv_translation` 导入器误识别，生成的 18 个无引用翻译文件已移出工程，归档到根目录忽略的 `artifacts/legacy-csv-translations/`。config 仅保留配置 CSV 与其 `.csv.import` 文件；后者是必要的导入元数据，应随 CSV 提交，内容使用 `importer="keep"`，防止再次误导入。未来正式本地化资源统一放 `Idle-CRPG/Assets/Localization/`。

| 表 | 字段 | 含义与约束 |
|---|---|---|
| world/world.csv | width,height,start_region_id | 矩形尺寸，各为 1–64，当前 16×16；出生区域引用 |
| world/world.csv | travel_speed,village_speed,trade_seconds,trade_range | 世界坐标每秒区域距离、村庄移速、交易秒数、交互范围；均为正数 |
| world/region.csv | id,x,y,name,terrain | 正区域 ID、网格坐标、名称、地貌；坐标不重复且覆盖整个矩形。地貌限 snow/tropical/desert/coast/grass |
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
| monster/spawn_rule.csv | hp_per_level,attack_per_level,defense_per_level | 每级生命/攻击比例增量、防御数值增量；非负 |
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
| world/obstacle.csv | id,kind,x,y,width,height | 唯一 ID、水面 water/岩石 rock、全局区域坐标及正矩形尺寸；边界不得越出世界，不能阻挡地牢/村庄入口导航格 |

当前临时世界有 256 区、256 村庄、251 普通地牢、35 城堡；城堡位置参考截图错位排列，统治区为连续的 2–12 格不规则区域，每个统治区有 3–10 个普通地牢，城堡另计。数量由关联行数决定，不在 region 重复维护。6 个边缘格不配置普通地牢，仍保留村庄。

当前 areapart 为 ap1–ap35，与 c1–c35 一一关联；ap1 默认开放，通关后开放 ap2、ap3、ap4。前置表包含实际接壤关系（非初始区的所有邻区均为候选前置），同一统治区列出的任意一个前置城堡通关均可开放该区。没有列出的条件不会隐式补全。相邻关系的双向引用是合法的，按 OR 可达性检查避免无入口闭环。生成工具 tools/Generate-WorldLayout.ps1 使用固定锚点、seed 生长连续不规则区域并按邻接层次编号，支持复现 CSV；游戏不在运行时执行生成器。

## 首次进房刷怪快照

实际数量 = 随机整数 [count_min + MinBonus, count_max + MaxBonus]。实际怪物等级 = 地牢等级区间内随机整数 + level − 1 + LevelBonus。科技增量独立、非负，合并后数量下限不得超过上限，总数不得超过 1000；当前没有科技加点 UI。楼层和英雄等级不参与刷怪。

生命 = 基础生命 × [1 + hp_per_level × (等级−1)] × BOSS生命倍率；攻击同理。防御 = 基础防御 + defense_per_level × (等级−1)。经验 = xp_base × 等级 × BOSS奖励倍率。

实际掉率 = min(1, chance × drop_chance_multiplier × 科技掉率倍率)。价值倍率 = [1 + value_per_level × (等级−1)] × 科技价值倍率 × BOSS奖励倍率。金币数量及杂物单价乘价值倍率向上取整；杂物数量不额外乘等级。怪物的经验、掉率和价值在生成时锁定，物品单价在拾取时锁定。售卖只结算锁定价格 × 数量。

公式唯一实现位于 MonsterSpawning 和 GameSession。数值均为原型配置，不代表最终平衡。
