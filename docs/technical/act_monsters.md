# 章节怪物身份与区域投放池

2026-10-23 按外部 P1-5 交接包实现切片 C1（数据联结与普通怪投放）。只增加“身份选择层”，不改既有怪物行为、等级、难度、Boss 或奖励路径。

## 数据文件

| 文件 | 作用 | 说明 |
|---|---|---|
| `config/monster/act_monsters.csv` | 章节怪物身份表（49 行） | `id,name,hp,attack,defense,speed,range,cooldown`；与正式 `monsters.csv` 分离，不改变旧随机集合 |
| `config/chapters/act{1..5}/monster/region_pool.csv` | 统治区投放池 | `areapart_id,monster_id,weight`；同区可多行，权重为整数百分比且合计必须 100 |
| `config/combat/unit_attributes.csv` | 战斗属性与接敌容量 | 每个身份都必须有 `side=monster` 行，否则加载报错 |
| `config/monster/monster_drop.csv` | 掉落 | 身份掉落沿用其模板（rat/guard），不新造经济数值 |

身份 ID 形如 `a1_fallen`、`a5_baal_minion`（章号前缀 + 内容 slug），与 `rat`/`guard` 不冲突。中文名是 D2 风格的内容提案，商用前需另做命名与美术审核。

## 抽取规则

1. 世界远征进入地牢并首次触门生成内容时，先取本次地牢 `RegionId` 所属统治区（`Config.AreaPart`）。
2. 若该章存在投放池且有该统治区条目，按权重抽取一个身份；权重用 `Random.Next(100)` 累加判定，同种子可复现。
3. 没有池（单地牢模拟、legacy 夹具、旧图）时回落到原全局 `Monsters` 随机表，行为与之前一致。
4. 抽取只决定**身份**；数量、等级、BOSS 名额、奖励仍由既有 `SpawnRule`、`spawn_rule.csv`、`poi_challenge` 与 `floor_plan` 决定。
5. BOSS 名称仍走 `poi_profile.boss_name`，未由本表驱动。

## 校验（加载期直接报错）

- 身份表：ID 非空且唯一、不得与正式怪物重名、数值为正（防御可 0）。
- 身份必须同时具备战斗属性行、接敌容量与至少一条掉落；缺项在加载期报错，不静默降级。
- 投放池：表头固定；每个统治区都必须有池；同区身份不重复；权重 1～1000 且合计 100；引用的怪物必须存在。
- 读档：房间出生模板通过 `Config.FindMonsterSpec` 解析，正式怪物与章节身份共用同一入口；正式存档版本不变。

## 生成器

`tools/import_act_monsters.ps1`（PowerShell 5.1+，UTF-8 BOM）：

```text
pwsh tools/import_act_monsters.ps1 -Check   # 只比较差异，退出码 1 表示有待写变更
pwsh tools/import_act_monsters.ps1          # 写入配置
```

- 输入：外部包 `docs/temp/P1_5_FiveActs_Monsters_Boss_V1_0/data/` 的 catalog、region_pools、poi 快照；本工程 `preview_archetypes.csv`、`monsters.csv`。**外部包只读，不复制进工程**。
- 映射：`ACT01:REGION_NN` → `apNN`（五章均为一一对应，已逐章核对）；身份属性取 archetype 的 `template`（rat/guard）与 `ranged`，不引入外部数值。
- 幂等：先剔除 `^a[1-5]_` 行再整体重写，重复执行结果一致。

## 行为偏移（C2）

| 文件 | 作用 | 说明 |
|---|---|---|
| `config/monster/act_behaviors.csv` | `monster_id,policy,skill_id` | 只给 M05（范围）、M06（猎手）、M08（支援）三类身份各一行，共 10 行；其余身份没有行为行 |

- `policy` 只接受 `Ordinary`/`BacklinePriority`，与 `Actor.TargetPolicy` 同一枚举；猎手复用 Special 锁，不占近战容量。
- `skill_id` 引用正式技能表：`mob_area_pulse`（自身中心 `AreaPulse`）、`mob_support`（友方 `ApplyBuff`）。技能由既有 `RunActiveSkill`/`Commit` 调度，没有新的怪物 AI 分支。
- 未知身份、未知技能、非法策略在加载期报错；行为只在世界远征生成时套用，单地牢/legacy/GM 预览不受影响。

## 精英词缀（C2）

`config/monster/elite.csv`：`difficulty,affix_id,buff_id,policy,weight,chance_pct`

| 词缀 | 实现 | 可用难度 |
|---|---|---|
| HARDENED | `elite_hardened`（armor +6 flat） | normal/nightmare/hell |
| VIGOR | `elite_vigor`（move_speed +0.6 flat） | normal/nightmare/hell |
| ARCANE_SHIELD | `elite_shield`（mechanic=shield，吸收 40） | nightmare/hell |
| HUNTER | 只改 `TargetPolicy=BacklinePriority` | nightmare/hell |

- `chance_pct` 是出现率，同难度必须一致；`weight` 决定同难度内抽哪一个。
- 每房最多一个精英，Boss 房不参与；目标从存活非 Boss 单位中随机。
- 词缀只加 Buff 或策略，不新增乘区、不改掉落；持续时间覆盖整场战斗，换房重开时随 Buff 一起清空。
- 出现率是原型值（20/30/40%），不是最终平衡。

## Boss 技能包（C3）

`config/monster/boss_skill.csv`：`poi_id,skill_id`，按 `boss_mechanics_PROPOSAL` 的远程/近战语义映射到 `mob_area_pulse` 或 `mob_heavy`（单体重击，系数 1.8）。只加给 `Boss=true` 本体；`poi_profile.boss_name` 仍由源数据提供，实际命名与逐 Boss 机制另需内容裁定。

## 未投放（需单独批准）

M07 敌方召唤的生产投放（需进房预留槽位与存档现场合同）、其余 6 个精英词缀（元素附伤/死亡爆裂/统御光环，需要新机制）、四段特殊剧本阶段（议会/封印/三远古/巴尔五波）、逐 Boss 专属机制包、三难度数值调平与长时挂机验收。这些都需要新的运行时能力或内容裁定，本轮未动；候选数据仍在外部队列里，不在本工程配置中。
