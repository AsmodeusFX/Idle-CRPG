# Boss 阶段与城堡威胁组合（S15-C2-A / A2）

2026-10-26 落地。目标：让四章末 Boss 具备真实阶段切换、让 31 座城堡的敌群按 S09 威胁形态组合，
同时**不新增**怪物等级、刷怪数量、伤害或奖励的第二权威。数值（伤害/阈值/CD/召援数）全部为 `[H]` 原型，留待 C3 调整。

## 1. 数据文件

| 文件 | 作用 | 说明 |
|---|---|---|
| `config/monster/boss_phase.csv` | Boss 阶段表（11 行，4 个章末首领） | 主键 `chapter + poi_id + phase`；POI ID 只在章内唯一 |
| `config/monster/archetypes.csv` | 身份→职能档位（51 行） | `monster_id,archetype`；`melee/ranged/heavy/support` |
| `config/chapters/act{1..4}/dungeon/castle_threat.csv` | 城堡威胁组合（31 座城堡 + 6 条按层覆盖） | `id,poi,floor,threat,composition,count_bias,elite_bias`，`floor=0` 表示该城堡默认 |
| `config/skill/{skill,effect,buff}.csv` | 阶段技能包 | 6 条阶段技能、5 条效果、3 条 Buff；复用 `AreaPulse`/`ApplyBuff`/`Chain`/`Damage` 既有类型 |
| `config/monster/act_monsters.csv` | 新增 `boss_mephisto` | 阶段接替的独立 Boss 身份（属性/掉落行同步），不是把普通怪改名 |

外部 `boss_phase.csv.import` / `castle_threat.csv.import` / `archetypes.csv.import` 全部是 `importer="keep"`；
A1 遗留的 `floor_rhythm.csv`/`hero_unlocks.csv` 曾被 Godot 自动按翻译表导入，本轮改回 keep，
误生成的 `*.translation` 移到忽略目录 `artifacts/legacy-csv-translations/`。

## 2. `boss_phase.csv` 字段

| 列 | 取值 | 说明 |
|---|---|---|
| `id` | 唯一 | 例 `A08-C01-P2` |
| `chapter` | 1–5 | 与 POI 所属章一致 |
| `poi_id` | 城堡/地牢 ID | 必须存在于该章 `dungeon.csv` |
| `phase` | 从 1 连续 | 同章同 POI 内唯一 |
| `trigger` | `fight_start` / `hp_pct` / `sequence` / `floor_stage` | 见下表 |
| `threshold` | 依 trigger | `fight_start`/`sequence` 必须 0；`hp_pct` 1–95（逐段降低）；`floor_stage` 楼层（逐段提高） |
| `scope` | `boss` / `room` | `room` 仅允许 `floor_stage`（该层无 Boss 本体也能推进） |
| `name` | 非空 | 阶段显示名；接替阶段同时是该 Boss 的显示名 |
| `skill_id` | 可空 | 阶段技能，追加到 Boss 的 `SkillIds`（CD 由 `skill.csv` 决定） |
| `buff_id` | 可空 | 阶段短期 Buff，施加给当前 Boss 本体 |
| `escort_template` | 可空 | 阶段随从身份，或 `role=successor` 的接替身份 |
| `escort_count` | 0–6 | 阶段召援上限（同一 POI 合计 ≤10） |
| `role` | 空 / `add` / `successor` | `successor` 必须是最后一段且由 `sequence` 触发，全 POI 唯一 |
| `telegraph` | 非空 | 预警文案，进入日志；技能前摇本身仍是既有 `windup_ticks` |

### 触发方式（都不依赖玩家手动机关）

| trigger | 判定 | 用于 |
|---|---|---|
| `fight_start` | 进房警觉窗口结束后的第一次判定 | 安达利尔毒雾、都瑞尔冲锋、议会结阵 |
| `hp_pct` | Boss 当前 HP% ≤ 阈值 | 安达利尔 50%、都瑞尔 50%、迪亚波罗 50% |
| `sequence` | 房间里所有**已激活**敌人死亡（待命预留槽不算） | 议会成员全灭 → 墨菲斯托 |
| `floor_stage` | `FloorIndex+1 ≥ threshold` | 混沌避难所第 3/4/5/6 层 |

## 3. 运行时唯一消费者（`Scripts/Core/BossPhaseRuntime.cs`）

- `PrepareBossPhaseActors`：进房生成与读档重建时，为阶段随从/接替者预留槽位（0 血待命、带 `BossPhaseId`）。
  顺序固定为"基础怪 → 阶段单位 → 敌方召唤"，因此 `GainKillKp` 的实例索引在存档往返后保持一致。
- `AdvanceBossPhases`：每帧最多推进一段；`BossPhaseTriggerMet` 判定后 `ApplyBossPhase` 激活槽位、
  追加技能、施加 Buff、写日志与表现事件。玩家侧/怪物侧伤害仍只走 `DamagePipeline`。
- `Room.PhasePending`：遭遇序列仍有未登场接替者时为 true，令 `Room.Clear` 不提前成立，
  否则议会倒下的瞬间房间就会结算（该标记不进入存档，读档由预留槽位重算）。
- 召援上限：预留槽位数即上限，激活一次后永不复用；同房间重复推进由阶段进度拦截。
- 阶段 Buff 是短期战斗现场，不改变 `Actor.Hp/MaxHp` 公式。

## 4. 存档与幂等

- `SessionSnapshot.BossPhaseProgress`：`poi:floor:room:phase` 字符串列表；旧档缺字段按空列表兼容。
- 读档只恢复**当前房间**的阶段进度（其它房间的阶段只可能属于已清房或未进入）；
  恢复时按顺序重放已生效阶段，重新挂上技能/随从/Buff，但不重新生成 Boss、不重复发奖励。
- `BeginDungeon` 清空本轮阶段进度：复刷是新的合法遭遇，仍需重新走阶段。
- 校验：条目数 ≤4096、格式 4 段、阶段号在配置范围内、房间/楼层存在。

## 5. 城堡威胁组合

`castle_threat.csv` 的 `composition` 是**身份档位循环**（`M`近战/`R`远程/`H`重型/`S`辅助，1–8 格循环）：

- 房间怪物总数仍由 `spawn_rule`（含 A1 的逐层 `count_bias`）决定；组合只决定"第 i 只取哪一档"。
- 同档内仍按统治区池权重随机；池内没有该档时**加载期报错**，不静默改名。
- 加载期校验：城堡存在、楼层 ≤ 该 POI 层数、`count_bias` −2..2、`elite_bias` −20..30、
  每个 token 的档位在该城堡所属统治区池里真实存在。
- 组合只覆盖城堡，不覆盖 226 层普通地牢节拍表（两表分开登记，普通层计数仍为 226）。

## 6. 与其它权威的边界

| 关注点 | 权威 | 本轮是否新增 |
|---|---|---|
| 怪物等级 | `Run.[Lmin,Lmax]` 首次生成快照 | 否（阶段随从取 Boss 本体等级） |
| 刷怪数量 | `spawn_rule` + 逐层 `count_bias` | 否（组合只决定身份档位） |
| 伤害 | `DamagePipeline.Resolve` | 否（阶段技能复用既有 Effect） |
| 奖励 | 击杀收据（KP/XP/掉落） | 否（阶段随从走同一击杀路径） |
| 城堡准入 | `CanChallengeCastle` / P0 前置 | 否（未改） |

## 7. 复跑与证据

```text
pwsh tools/Apply-S15C2A-A2.ps1 -Check    # 配置差异预演（无写入）
pwsh tools/Apply-S15C2A-A2.ps1           # 幂等写入配置
dotnet run --project tests/IdleCrpg.Checks.csproj -- --c2a-boss-checks
godot --path Idle-CRPG -- --c2a-boss-smoke     # 真实窗口阶段前后截图
```

`--c2a-boss-checks` 输出 `artifacts/qa/s15-c2a-boss-*/`：
`castle_threats.csv`、`c2a_a2_castle_composition.csv`（31 座城堡运行时身份档位）、
`andariel_phases.csv`、`council_to_mephisto.csv`、`chaos_sanctuary_stages.csv`、`c2a_a2_spawn_ledger.csv`。
