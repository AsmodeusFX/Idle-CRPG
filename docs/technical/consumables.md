# 消耗品字段与消费点（S15-C2-B 切片）

2026-10-26 落地。范围：5 种卷轴 + 4 种药剂。数值全部 `[H]` 原型，正式数值归 C3。
槽位数量、掉落权威与"不自动使用"边界不变。

## 1. `config/item/scroll.csv`

`id,name,effect,damage_pct,element,radius,chain_targets,buff_id,target,drop_chance`

| 列 | 取值 | 说明 |
|---|---|---|
| `effect` | `damage` / `chain` / `aoe` / `slow` / `protect` | 五种快捷品类各一条 |
| `damage_pct` | ≥0 | 伤害系数（`protect` 必须为 0） |
| `element` | `Physical/Fire/Cold/Lightning/Poison` | 只决定伤害类型与抗性路由 |
| `radius` | ≥0 | 仅 `aoe` 使用（目标点固定半径单次脉冲），其余必须为 0 |
| `chain_targets` | 0 或 2–12 | 仅 `chain` 使用；只对**不同**存活目标生效，不虚构弹跳 |
| `buff_id` | 可空 | `slow`（减速）与 `protect`（全队防护）必填，引用 `skill/buff.csv` |
| `target` | `enemy` / `party` | 只有 `protect` 指向队伍；其余必须指向敌人 |
| `drop_chance` | 0–1 | 击杀掉落概率 |

消费点（唯一）：`GameSession.UseScroll` / `UseScrollOnParty` / `UseScrollOnNearest`。
成功（至少造成一次真实伤害或施加一次真实状态）后库存恰好 −1；非法目标、无战斗现场、库存不足一律拒绝且不扣。
所有伤害经 `DamagePipeline.Resolve`，不新增第二条伤害或奖励路径。

| 卷轴 | effect | 行为 |
|---|---|---|
| `scroll_bolt` 闪电卷轴 | damage | 单体伤害（沿用旧口径） |
| `scroll_arc` 电弧卷轴 | chain | 最多 3 名不同敌人，逐跳 ×0.8 衰减 |
| `scroll_flame` 烈焰卷轴 | aoe | 目标点 2.6U 内一次火伤 |
| `scroll_frost` 寒霜卷轴 | slow | 单体冰伤 + `buff_scroll_frost`（chill 0.3 / 3s） |
| `scroll_banner` 战旗卷轴 | protect | 战斗中全队 `buff_scroll_banner`（闪避 +0.15 / 8s） |

`scroll_banner` 需要"队伍在战斗房且房内有敌人"，不点选敌人；UI 第 5 槽用同一按钮触发。

## 2. `config/item/potion.csv`

`id,name,effect,duration_ticks,value,drop_chance`

| 列 | 取值 | 说明 |
|---|---|---|
| `effect` | `attack` / `guard` / `kill_gold` / `swift` | 四类效果 |
| `duration_ticks` | ≥1 | 20Hz 逻辑步；暂停/关闭不 Tick 即冻结 |
| `value` | >0 | 比例（0.15 = +15%） |
| `drop_chance` | 0–1 | 击杀掉落概率 |

| 药剂 | effect | 唯一读取点 |
|---|---|---|
| `potion_might` 力量药剂 | attack | `EffectiveValue("atk")` × `1+PotionAttackPct` |
| `potion_tough` 坚韧药剂 | reduce_damage | `SettleDamage` 中英雄实际承伤 × `(1-PotionDamageReductionPct)`，15% 且截断到 ≤90%（不改命闪与 MaxHp） |
| `potion_greed` 搜金药剂 | kill_gold | 拾取入账点 `StarKillGoldPct+PotionKillGoldPct`（不放大出售/离线） |
| `potion_swift` 疾行药剂 | swift | `MoveMultiplier` 的非战斗分支（不影响战斗 Tick） |

装载校验：`effect` 白名单、时长/强度/掉率为正、卷轴表不得超过 5 条（快捷栏固定 5 槽）、`buff_id` 必须真实存在。

> 掉落概率与**品类准入**已由按实际怪级的 [`item/drop_table.csv`](loot_equipment.md) 决定（C2-B）；
> 上表的 `drop_chance` 只在旧组合缺该表时作为回退口径。

## 3. 存档与边界

- 卷轴库存 `SessionSnapshot.Scrolls`：按物品 ID 计数；旧档缺字段按空库存兼容。
- 药剂槽固定 4 个 `SessionSnapshot.Potions`：空槽不得残留计时；同种药剂同时只允许一瓶生效。
- 新增卷轴/药剂都是**新增 ID**，没有改名或品质重映射，旧档不需要迁移。
- 不在本切片内：装备 28 模板落库（CHARM 槽位映射待裁决）、自动使用、S02/P03 出售。

## 4. 复跑

```text
dotnet run --project tests/IdleCrpg.Checks.csproj -- --c2b-checks
godot --path Idle-CRPG -- --first-batch-smoke    # 五卷轴槽与四药剂/截图
```
