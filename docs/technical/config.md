# 配置字段字典

所有表放在 Godot 工程 `config/` 内，首行为英文表头；不支持字段内逗号。数字使用小数点。

| 表 | 字段 | 说明 |
|---|---|---|
| dungeon.csv | floors_min / floors_max | 正整数，层数随机范围 |
| dungeon.csv | rooms_min / rooms_max | 每层房间数，包含安全起点，范围 3–20 |
| dungeon.csv | monsters_min / monsters_max | 普通房间基础怪物数，正整数 |
| dungeon.csv | event_chance | 普通房间事件概率，0–1 |
| dungeon.csv | revive_seconds | 清房后复活交互所需秒数，正数；首版 3 秒 |
| dungeon.csv | revive_range | 复活交互距离，正数；首版 0.65 世界单位 |
| dungeon.csv | revive_hp_ratio | 复活恢复最大生命的比例，(0,1]；首版 0.5 |
| heroes.csv | id / name | 唯一职业 ID / 显示名 |
| heroes.csv | hp / attack / defense | 初始生命、攻击、防御；HP 与攻击正数、防御非负 |
| heroes.csv | speed / range / cooldown | 移速、射程、攻击间隔（秒），均为正数 |
| monsters.csv | id / name | 唯一怪物 ID / 显示名 |
| monsters.csv | hp / attack / defense / speed / range / cooldown | 同角色字段 |

首版怪物等级 = 楼层编号 + 队伍平均等级 - 1，至少 1。数量 = 基础随机数 + floor(队伍平均等级 / 3)。生命每级 +18%，攻击每级 +12%，防御每级 +1；BOSS 生命 ×4、攻击 ×1.4。怪物经验 = 等级 ×2，BOSS 再 ×4。以上公式暂定，唯一实现位于 GameSession。
