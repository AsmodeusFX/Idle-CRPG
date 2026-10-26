# S15-C3 首轮数值接入与运行验收（工程侧报告）

- 日期：2026-10-26 23:12（Asia/Shanghai）
- 输入包：`docs/temp/P2-4 S15_C3_首轮数值配置与运行验收_V0.7/`（设计 Owner，`DESIGN_READY_WITH_UNVALIDATED_BALANCE`；本轮由用户直接指示执行）
- 工程自检入口：`dotnet run --project tests/IdleCrpg.Checks.csproj -- --c3-checks`
- 机器数据：`artifacts/qa/s15-c3-9caae4cd400646b78b0773ef80e16245/`（另有 `artifacts/simulation/C3_*` 逐场输出）
- 结论口径：本报告区分 **实测（SIMULATED_RUNTIME）**、**配置推算（CONFIG_DERIVED）**、**设计代理（DESIGN_PROXY）** 与 **不可用（unavailable）**。
  未做真实 Godot 十小时人工试玩；`G_10H` 与 `G_TTK` 不成立，本轮**不宣称四章 600 分钟目标通过**。

## 1. 执行与覆盖

### 1.1 环境

| 项 | 值 |
|---|---|
| OS | Windows（win32） |
| dotnet SDK | 10.0.301，目标框架 net8.0 |
| Godot | 4.7.2.stable.mono（本轮未用于 C3 数值验收） |
| 配置指纹 | 由 `SimulationRecorder.ConfigFingerprint` 记录在每场 `metadata.json` |
| 源码指纹 | 同上（含工作区未提交改动标记） |

### 1.2 命令

```text
dotnet build Idle-CRPG/Idle-CRPG.csproj
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --project tests/IdleCrpg.Checks.csproj            # 默认全量 Core
dotnet run --project tests/IdleCrpg.Checks.csproj -- --regression-matrix   # 58/58
dotnet run --project tests/IdleCrpg.Checks.csproj -- --c3-checks           # 本报告
# 缩短冒烟（定位缺口用）：$env:C3_MINUTES='60' 后再跑 --c3-checks
```

### 1.3 场景与初始状态（全部为显式玩家点击代理）

| 维度 | 取值 |
|---|---|
| 队伍 | P4（战士/游侠/法师/牧师）、P2R2P（双游侠双圣骑）、P4SAME（四战士）、P1（单战士） |
| 决策节奏 | 每 10 分钟 / 每 45 分钟一次（升级、根节点星图、城堡、Lmax 购买同周期） |
| 游戏时长 | 600 游戏分钟（基线对照 240 分钟） |
| 采样 | 每 30 分钟一行 + 收尾快照 |
| 种子 | 3301、3302（仅 P4 取两枚，其余一枚） |
| 明确禁止的代理 | 自动升级、自动换装、自动使用卷轴/药剂、自动加点——均未开启 |

### 1.4 未覆盖项（显式 unavailable / NOT_MODELED）

- **可选内容策略**（NO_OPTIONAL / HALF_OPTIONAL）：自动推进只有一个选路策略，无开关 → `ACTION_NOT_MODELED`。
- **装备策略**（NORMAL_GEAR / LOW_GEAR_NO_CONSUMABLE）：产品禁止自动换装与自动用药，模拟器不得代为点击 → `ACTION_NOT_MODELED`。
- **QA_REDUCED_ENCOUNTER_COVERAGE_80/70**：需要生成期降覆盖率开关，当前无 → `NOT_MODELED`。
- **LMIN_QA_LOCK / RECEIPT_REVISIT**：本轮未单独跑；收据去重以“KP 击杀收据键唯一”在基线场验证（见 §3）。
- **P50/P90 分位**：每场景种子数 1–2，`unavailable`（设计建议 ≥30 Seed 才谈分位）。

## 2. 实测结果（SIMULATED_RUNTIME，600 游戏分钟）

| 场景 | 章节到达 | 最终 Lmax | 队伍等级 | 击杀 | 金币 | 公共XP | 第1章 | 第2章 | 第3章 | 第4章 |
|---|---|---|---|---|---|---|---|---|---|---|
| P4 / 10min / 3301 | 2 | 15 | 16/16/16/15 | 11605 | 112650 | 103448 | 0min | 480min | — | — |
| P4 / 10min / 3302 | 2 | 15 | 16/16/16/15 | 11522 | 111052 | 102018 | 0min | 480min | — | — |
| P4 / 45min / 3301 | 1 | 4 | 5/4/4/4 | 11279 | 64493 | 40294 | 0min | — | — | — |
| P4 / 45min / 3302 | 1 | 4 | 5/4/4/4 | 11087 | 62795 | 39372 | 0min | — | — | — |
| P2R2P / 10min / 3301 | 2 | 15 | 16/16/16/15 | 11970 | 116684 | 106124 | 0min | 480min | — | — |
| P2R2P / 45min / 3301 | 1 | 4 | 5/4/4/4 | 11575 | 66063 | 41298 | 0min | — | — | — |
| P4SAME / 10min / 3301 | 2 | 15 | 16/16/16/15 | 11485 | 111532 | 101440 | 0min | 480min | — | — |
| P4SAME / 45min / 3301 | 1 | 4 | 5/4/4/4 | 11191 | 64007 | 39994 | 0min | — | — | — |
| P1 / 10min / 3301 | 3 | **60** | 60 | 7961 | 162243 | 176568 | 0min | 210min | 360min | — |
| P1 / 45min / 3301 | 1 | 14 | 14 | 9893 | 94098 | 84386 | 0min | — | — | — |

- 章节到达时间取快照 `chapter` 列首次出现的 `sim_time`，是**进入该章**的时刻，不等于“通关该章”。
- 全部 10 场在 600 分钟内**没有任何一场到达第 4 章**（`unavailable`，不是 0）。

## 3. 账本与守恒（SIMULATED_RUNTIME）

基线场（P4/240min/seed 3301）同种子跑两次，`asset_ledger.csv` 字节级一致（`7b3b95f3cfec18ca`），共 15133 行事件。

| 资产 | 事件数 | ΣΔ | 首条 before | 末条 after | 结果 |
|---|---|---|---|---|---|
| xp | 5170 | 23482 | 0 | 23482 | ok |
| kp | 4813 | 4758 | 0 | 4758 | ok |
| gold | 5054 | 33378 | 0 | 33378 | ok |

- 三账闭合：`ΣΔ == 末余额 − 首余额`，无未归属差额。
- 收据去重：`monster_kill/receive/kp` 的 `origin` 键 4813 条无重复（同一次合法击杀不会二次入账）。
- 金币分账（`SIM_LEDGER`）显示 `in/out/diff=0`，星图与药剂乘率只在入账点应用一次。

## 4. 参数→消费者映射（CONFIG_DERIVED，21 行）

`c3_consumer_map.csv` 对设计 `C3_配置参数与消费者生产映射.csv` 的 21 个参数族逐行给出消费路径与源文件行号，全部 `verified=yes`：

| 参数族 | 消费点 | 现状 |
|---|---|---|
| XP_POOL | `GainExperience → AuditCurrency(xp)` | 收入与支出都进同一账本 |
| XP_PER_MONSTER_LEVEL | `MonsterSpawning.RewardXp=XpBase*level` | 按实际生成怪级，`spawn_rule.xp_base=2` |
| HERO_LEVEL_COST | `UpgradeHero → herolevel.exp` | 生产为 18×等级线性 |
| HERO_BASE_GROWTH | `herolevel.attack_bonus/hp_bonus` | 增量生效，装备独立 |
| AR_DR_RATING | `DamagePipeline` | 全员独立 AR/DR |
| MONSTER_RATING | `PrototypeProgression.MonsterRatingPerLevel` | 与快照同源 |
| LMAX_KP_COST | `BuyKpUpgrade` | 生产 `lmax_cost=10` 平铺 |
| LMIN_PLAYER | 购买与存档均拒绝 | `C_DISABLED` 保持 |
| MONSTER_HP_ATK | `MonsterLevels`（1..10000） | 未因 31 档 XP 输入截断 |
| P0_ENCOUNTER_PACING | `EncounterFor → spawn_rule` | C2-A 已落地 |
| GOLD_KILL / GOLD_STAR_BONUS | `CollectLoot` 入账点 | 单点一次 |
| STAR_PRICE / STAR_XP_BONUS | `BuyStar` / `XpGainMultiplier` | 先买后赚 |
| EQUIPMENT_DROP / QUALITY / SALE | `drop_table` / 品质ID / `village_trade` | C2-B 已落地 |
| SCROLL_DROPS / POTION_DROPS | `UseScroll` / `AdvanceConsumables` | C2-B 已落地，禁自动使用 |
| OFFLINE_YIELD | `Rebirth.SettleOffline` | 幂等、独立预算 |
| L1_TO_L31_TIMELINE | 无独立配置消费者 | 只能由实跑事件观测 |

来源哈希（`c3_source_hashes.csv`）覆盖 5 份设计冻结输入 + 7 份生产配置，全部存在。

## 5. 关键发现

1. **四章 600 分钟目标未达成**（G-PROGRESS）：最乐观的 P4/10min 也在第 2 章止步；单人极限到第 3 章。
   第 1 章耗时 ~480 分钟（P4），与设计 `90/165/195/150` 的拆分相差约 5 倍。
2. **决策节奏是第一敏感项**：同为四职业队，10 分钟检查 → 第 2 章 / Lv16；45 分钟检查 → 仍停第 1 章 / Lv4。
   `PublicXP` 相差 2.5 倍（103k vs 40k），说明“玩家点击频率”比阵容更主导进度。
3. **等级与怪级缺少设计上限**（G-VALUE）：生产 `herolevel.csv` 10000 行、`monsterlevel.csv` 10000 行，
   单人场景 600 分钟即达 Lv60 / Lmax60，远超设计 `HeroLv25 / Lmax31`。这是**正式数值取舍**，须用户批准后才能改。
4. **阵容差异被节奏盖过**：P2R2P 与 P4SAME 的 10min 结果与 P4 接近（章节、等级、击杀 ±5%），
   当前数值下“职业搭配”不是瓶颈，`55–65 Rank` 的价值验证仍不成立（`G_VALUE` 未通过）。
5. **可选内容与装备/耗材策略无法建模**：自动推进只有一个选路策略；换装、用药、卷轴产品上禁止自动，模拟器不得代理。
   这使“装备不佳/不用耗材”的对照无法给出实测，只能标 `ACTION_NOT_MODELED`。
6. **三账闭合与同种子可复现成立**：xp/kp/gold 逐笔闭合、收据键唯一、同种子两次运行字节一致。

## 6. 与设计候选的差异（只提出，不覆盖生产配置）

`c3_tuning_proposals.csv`：

| 参数 | 生产现状 | 设计候选 | 影响 | 裁决 |
|---|---|---|---|---|
| `lmax_cost` | 10（每级固定） | 14/28/42/56…（逐阶，累进 CumLmaxKPCost） | Lmax 推进节奏与 KP 总需求 | 需用户批准 |
| 英雄升级 XP | 18×等级（1→2=18） | 100/160/220/260/310…（1→25 累计 54120） | 四英雄升级时间与 XP 预算 | 需用户批准 |
| 英雄/怪级上限 | 10000 | 25 / 31 | 长时轮次的等级封顶 | 需用户批准 |
| `hero_rating_per_level` | 10 | 10 | — | 一致，不改 |
| `monster_rating_per_level` | 10 | 10 | — | 一致，不改 |
| `equipment_drop_pct` | 0.25（仅旧组合回退） | 0.018–0.0216（按怪级） | 旧组合掉落偏高 | 不改（C2-B 等级表已生效） |

按授权边界，以上**影响玩家体验的正式数值取舍未实施**，工程侧未修改任何生产数值。

## 7. 功能断点与风险

| 编号 | 类型 | 描述 | 证据 |
|---|---|---|---|
| G-PROGRESS-01 | 断点 | 600 分钟内四章不可达；第 1 章 ~480 分钟（P4/10min） | §2 表 |
| G-PROGRESS-02 | 断点 | 45 分钟检查节奏下 10 小时仍停第 1 章 | §2 表 |
| G-VALUE-01 | 断点 | 无等级/怪级上限，单人 10 小时达 Lv60/Lmax60 | §2 P1 行 |
| G-MODEL-01 | 模型缺口 | 可选内容/装备/耗材策略无法建模（产品禁止自动） | §1.4 |
| G-TTK-01 | 未验证 | Boss TTK 与四章节奏没有真实画面证据 | 本轮未跑 Godot |
| G-10H-01 | 未验证 | 无 10 小时人工试玩，设计目标不能宣称通过 | 全局 |

## 8. 后续建议（按优先级）

1. **先定数值裁决**：`lmax_cost` 逐阶、英雄升级 XP 曲线、`HeroLv25/Lmax31` 上限——三项决定后续所有节奏结论，其它调参在此之前意义有限。
2. 裁决后重跑本报告的同一命令（600 分钟 × 4 阵容 × 2 节奏），并扩到每场景 ≥30 Seed 再谈 P50/P90。
3. 为“可选内容”“装备/耗材”补 QA 脚本化策略（必须是显式点击序列，不能开启自动换装/自动用药），否则对照永远缺失。
4. Boss 与章节节奏必须补真实 Godot 画面与操作证据（`G_TTK`）。

## 9. 复跑方法

```text
# 完整（约 7 分钟）
dotnet run --project tests/IdleCrpg.Checks.csproj -- --c3-checks

# 快速定位（60 游戏分钟）
$env:C3_MINUTES='60'; dotnet run --project tests/IdleCrpg.Checks.csproj -- --c3-checks
```

输出：
- `artifacts/qa/s15-c3-<hash>/c3_consumer_map.csv`、`c3_source_hashes.csv`、`c3_conservation.csv`、`c3_matrix.csv`、`c3_tuning_proposals.csv`、`c3_summary.csv`
- `artifacts/simulation/C3_<场景>_<seed>_c3/`：`snapshots.csv`、`events.csv`、`ledger.csv`、`asset_ledger.csv`、`metadata.json`
