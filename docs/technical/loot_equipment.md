# 装备模板与按实际怪级掉落（S15-C2-B）

2026-10-26 落地。范围：六部位装备 × 四品质的真实掉落、28 个设计模板的落库映射、31 个实际怪级的掉落分布。
所有概率与数值仍为 `[H]` 原型；怪物等级唯一来源仍是 Run `[Lmin,Lmax]` 的生成快照。

## 1. 三张表

| 文件 | 作用 |
|---|---|
| `config/item/equipment.csv` | 六部位基础件（`id,slot,attr,base_value,per_level,name`），一件只有一个主属性 ATK 或 HP |
| `config/item/equipment_quality.csv` | 四品质（`common/uncommon/rare/legendary`）的显示名、数值倍率与默认权重 |
| `config/item/drop_table.csv` | 31 行实际怪级分布（本包新增）：装备概率、四品质概率、卷轴/药剂概率与**品类准入** |

数值唯一公式仍是 `Config.EquipmentValue(base, level, quality) = round((base_value + per_level×(level-1)) × multiplier, 2)`；
`level` 取被击杀怪物的**实际等级**（`Actor.Level`，即生成时的快照），不是章号或 POI 旧级。

## 2. `drop_table.csv` 字段

`monster_level,equipment_chance,common,uncommon,rare,legendary,scroll_chance,potion_chance,scroll_ids,potion_ids`

| 列 | 单位 | 说明 |
|---|---|---|
| `monster_level` | 1..31 连续 | 从 1 起连续递增，不跳号；超出表尾的实际等级按最高行处理 |
| `equipment_chance` | 0–1 小数 | 单次合法击杀掉出装备的概率（设计源 1.8%–2.16%） |
| `common/uncommon/rare/legendary` | 0–1 小数 | 该级四品质概率，**必须和为 1**；列序与 `QualityFractionsOrder` 固定绑定 |
| `scroll_chance` / `potion_chance` | 0–1 小数 | 设计源为百分数（0.3/0.15…），换算时除以 100 |
| `scroll_ids` / `potion_ids` | `|` 分隔 | 该级允许掉落的品类；引用不存在的 ID 直接加载失败 |

加载校验：等级连续、概率范围、品质和=1、品质列与装备品质 ID 集合一致、每级至少一个卷轴与一个药剂、
所有引用 ID 必须真实存在（`Config.LoadDropTable`）。

## 3. 消费点

| 行为 | 位置 | 说明 |
|---|---|---|
| 装备掉落 | `GameSession.TryDropEquipment` | 概率取等级表；品质按该级四品质概率抽样；旧组合缺表时回退 `Progression.EquipmentDropPct` + 品质整数权重 |
| 消耗品掉落 | `GameSession.TryDropConsumable` | 概率与**准入品类**取等级表；旧组合缺表时回退各品类自身 `drop_chance` |
| 物品等级 | 同上 | `Math.Clamp(monster.Level,1,MaxMonsterLevel)`，怪物等级仍由 Run 快照决定 |

两处都只掷一次骰子组，不新增第二套等级、数量或奖励权威；金币/XP/KP 仍走既有收据。

## 4. 28 个设计模板 → 24 个可掉组合

设计源 `C2B_28装备模板到生产品质ID逐项转换.csv` 是 7 个"部位×主属性"组合 × 4 品质：

| 设计组合 | 工程基础件 | 状态 |
|---|---|---|
| WEAPON_ATK | `eq_weapon`（weapon/atk） | 已实现 ×4 品质 |
| HELM_HP | `eq_helm`（helm/hp） | 已实现 ×4 |
| CHEST_HP | `eq_chest`（chest/hp） | 已实现 ×4 |
| GLOVES_ATK | `eq_gloves`（gloves/atk） | 已实现 ×4 |
| BOOTS_HP | `eq_boots`（boots/hp） | 已实现 ×4 |
| CHARM_ATK | `eq_ring`（ring/atk） | 已实现 ×4（CHARM 映射到既有指环槽） |
| CHARM_HP | — | **被用户裁决覆盖**（"装备CHARM映射使用ATK参数"），不单独投放 |

- 品质映射沿用用户已确认的生产 ID：普通/稀有/罕见/传奇 → `common/uncommon/rare/legendary`（设计 `RARE→uncommon`、`EPIC→rare`）。
- 因此实际可掉组合 = 6 部位 × 4 品质 = **24**；28 行逐行账本由 `--c2b-checks` 输出 `c2b_equipment_templates.csv`。
- 没有改品质 ID、没有对已保存物品做重映射，旧档零迁移；也没有新增第 7 部位。

## 5. 证据与复跑

```text
dotnet run --project tests/IdleCrpg.Checks.csproj -- --c2b-checks
```

产出 `artifacts/qa/s15-c2b-*/`：

- `c2b_equipment_templates.csv`：28 行映射账本（24 realized / 4 superseded_by_ruling）。
- `c2b_equipment_chain.csv`：等级表 → 实际怪级 Lv.31 → 候选 → 穿戴的既有资产审计事件（不新增账本）。
- `c2b_consumable_evidence.csv`：卷轴/药剂/装备/存档逐项断言值。

断言口径：31 个等级各跑一轮，逐件核对"品质=该级强制品质、物品等级=怪物实际等级"，并要求 24 个组合全部出现过。
