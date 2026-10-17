# 配置字段字典

所有 CSV 为 UTF-8，按功能放在 `Idle-CRPG/config/` 子目录。首行为英文表头，字段不含逗号，数字使用小数点。ID 唯一、跨表引用有效，缺字段直接报错。CSV 的 Godot importer 使用 keep，不作为翻译资源。

| 表 | 字段 | 含义与约束 |
|---|---|---|
| world/world.csv | width,height,start_region_id | 矩形尺寸，各为 1–64，当前 16×16；出生区域引用 |
| world/world.csv | travel_speed,village_speed,trade_seconds,trade_range | 世界坐标每秒区域距离、村庄移速、交易秒数、交互范围；均为正数 |
| world/region.csv | id,x,y,name,terrain | 正区域 ID、网格坐标、名称、地貌；坐标不重复且覆盖整个矩形。地貌限 snow/tropical/desert/coast/grass |
| world/castle_region.csv | region_id,castle_id | 每区恰好一行统治关联；城堡必须存在且包含其所在区域 |
| dungeon/dungeon.csv | id,name,region_id,kind | 地牢唯一 ID、名称、区域、normal/castle；每区至少一个普通地牢，至多一个城堡 |
| dungeon/dungeon.csv | level_min,level_max,x,y | 临时等级区间，1≤下限≤上限；区域内入口坐标，均在 (0,1) |
| dungeon/dungeon_rule.csv | floors_min,floors_max,rooms_min,rooms_max | 层数 1–20、每层房间数 3–20，包含安全起点；下限不大于上限 |
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

当前临时世界有 256 区、256 村庄、512 普通地牢、16 城堡，每个城堡统治一个 4×4 区域组。数量由关联行数决定，不在 region 重复维护。

## 首次进房刷怪快照

实际数量 = 随机整数 [count_min + MinBonus, count_max + MaxBonus]。实际怪物等级 = 地牢等级区间内随机整数 + level − 1 + LevelBonus。科技增量独立、非负，合并后数量下限不得超过上限，总数不得超过 1000；当前没有科技加点 UI。楼层和英雄等级不参与刷怪。

生命 = 基础生命 × [1 + hp_per_level × (等级−1)] × BOSS生命倍率；攻击同理。防御 = 基础防御 + defense_per_level × (等级−1)。经验 = xp_base × 等级 × BOSS奖励倍率。

实际掉率 = min(1, chance × drop_chance_multiplier × 科技掉率倍率)。价值倍率 = [1 + value_per_level × (等级−1)] × 科技价值倍率 × BOSS奖励倍率。金币数量及杂物单价乘价值倍率向上取整；杂物数量不额外乘等级。怪物的经验、掉率和价值在生成时锁定，物品单价在拾取时锁定。售卖只结算锁定价格 × 数量。

公式唯一实现位于 MonsterSpawning 和 GameSession。数值均为原型配置，不代表最终平衡。
