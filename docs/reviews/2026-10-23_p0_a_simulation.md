# P0-A 统一模拟测试配置结构

状态：部分实施，尚未达到 V1.0 完成定义。

## 实现与审计

新增 tests/Simulation/SimulationRunner.cs 和 A/B/C JSON。测试专用不可变配置，未知字段、未知版本和非法周期拒绝加载；四类动作默认关闭。固定20Hz，按完成Tick计时，0至240分钟每30分钟采样。复用 EconomySimChecks.Grow，拆分成长、星图、编队开关。正常客户端、Core及正式数值未改。

城堡共用 HudLayout.cs 按钮调用的 GameSession.RequestCastleChallenge，经 WorldCampaign.RequestCastle 的真实解锁、占领和重复排队校验，不直接清关。请求可排队，后续仍需完成当前地牢及返村交易。

旧模拟以循环下标整数分钟记录购买，缺乏精确模拟秒；新入口以完成Tick/20记录事件。旧 --economy-sim 保留兼容入口，尚未完全迁移。

## 验证与数据

Windows / .NET 8目标。dotnet build tests/IdleCrpg.Checks.csproj：退出0，零警告。

三个场景均执行下列命令，退出0，运行240游戏分钟：

`dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/A_pure_idle.json`

B/C分别替换文件名。本轮按场景拆分的机器数据及简报已按用户要求清理，不再作为归档附件；本次尚未交付符合新规范的单份统计报告。

## 未完成验收

尚缺配置与四开关隔离定向测试、相同Seed重复结果比较、细分有效经验/金币产消/挑战失败及死亡采集、首次经验归零可靠诊断、关键事件完整分类与数据结论。现有编队策略仍沿用 warrior/ranger/mage 候选并交正式接口校验，未完成真实解锁名单审计。缺失指标此前已标注 unavailable，不能视作全部验收通过。

UI未修改，Godot smoke与视觉验收未执行。其他平台未验证。不曾提交或推送。

## 数据归档清理状态（2026-10-23）

用户已手动删除本轮不规范场景数据目录及旧 economy CSV 附件，现已确认两目录均不存在，并清理文档中的失效附件引用。旧 economy 附件无测试或工具依赖；核心体验统计报告及其中的历史统计结果保留。后续模拟采用 AGENTS.md 中的单份 Markdown 规则及 YYYY-MM-DD_HH-mm_data_自定义名称.md 命名。

## 重新运行与统计交付（2026-10-23_21-54）

使用当前代码与正式配置重跑A/B/C，各240游戏分钟，命令均退出0；构建退出0，零警告。单份报告见[模拟玩家对照统计](../data/2026-10-23_21-54_data_p0_a_simulation.md)。B最终第3章、L30，记录25条城堡胜利；A/C后段出现长期无击杀及金币增长，原因尚未定位。未补采集器字段或修改正式玩法，原有未完成验收仍有效。本次不包含Core全量检查、Godot smoke或确定性复跑。

## P2-1-M2 模拟工具能力审计与补齐（2026-10-23）

本轮属性：测试工具开发。未改正式 Core、配置、数值、存档格式、城堡手动点击规则与既有断言；`--economy-sim` 原样保留为历史兼容入口。机器数据在忽略的 `artifacts/simulation/<场景>_<Seed>/`，报告内不复制 CSV。

### 1 M2-A 模拟工具能力清单（真实代码为准）

| 能力 | 真实代码/字段 | 审计前 | 缺口 | 本轮方案 | 涉及 Core |
|---|---|---|---|---|---|
| 场景配置加载 | `tests/Simulation/SimulationConfig.cs` | 部分 | 只有四开关，无版本 2、无策略对象 | 版本 1/2 双格式，未知字段拒绝，策略与开关不可混用 | 否 |
| 城堡交互 | `GameSession.RequestCastleChallenge`→`WorldCampaign.RequestCastle`；`PendingCastles`/`WaitingApproval`/`WaitApprovalTicks`/`RegionState` | 部分 | 只有一条 `castle_blocked` 汇总；请求≠开战≠胜利不可分辨 | 记录 available→click(accepted/rejected+理由)→queued→enter→result/failure，并用 `ManualGate` 标记正常门槛 | 否 |
| 星图购买 | `GameSession.BuyStar(id,out reason)`、`Stars.Level/CanBuy`、`Config.Stars.Nodes[id].Costs/MaxLevel` | 部分 | 只能跑历史固定顺序；无显式节点表、无逐笔花费 | `legacy` 与 `explicit` 两种模式；显式按顺序+等级上限购买，逐笔记录节点/花费/等级与拒绝理由 | 否 |
| 编队 | `RequestFormation(ids,out reason)`、`HeroUnlocked`、`PartyCapacity`、`PendingFormation` | 缺失 | 只能“取前 N 个”；无候选名单、无未编入原因 | `explicit` 名单+目标人数；未解锁→`locked`，解锁但席位不足→`over_capacity`，扩编后继续补人 | 否 |
| 天赋/主动技能 | `LearnTalent(out reason)`、`SetTalentLoadout(out reason)`、`TalentPoints`、`ActiveSkillSlots`、`TalentLevel` | 部分 | 只有行/列自动策略；无显式计划与拒绝原因 | `none/legacy/explicit`；显式按 learn/equip 计划执行，记录行门槛等官方拒绝理由 | 否 |
| 动作时序 | 运行器主循环 | 部分 | 单一周期；同一 Tick 可多动作 | 每组独立周期、固定优先级、**同一 Tick 只执行一组** | 否 |
| 英雄经验 | `Actor.Experience/ExperienceFraction/Level`、`Config.MonsterExperiencePercent`、`HeroLevels` | 部分 | 只有等级/整数经验；余额被当成累计 | 用“升级门槛前缀+当前经验”求**增量**，逐 Tick 与击杀基础值×等级差倍率核对，区分 verified/level_up/事件/无法解释 | 否 |
| 金币流水 | `Gold`、`Inventory`、`RoomLoot.Collected/Gold/Items`、`WorldCampaign.Trades` | 缺失 | 只有余额 | `ledger.csv`：拾取/售卖/星图/洗点逐笔带符号记录，期末差额必须为 0 | 否 |
| 战斗遭遇 | `Kills`、`Room.Monsters/Origins`、`RoomCombatStats`、`HitCount`、英雄 HP | 部分 | 无逐击杀事实、无房间起止、攻击者不可观测 | 逐击杀事件（怪物 ID/等级/Boss/奖励经验）、房间进入/清理、失败返村；攻击者与逐技能伤害标 `unavailable` | 否 |
| 推进与阻断 | `World.Stage/CurrentRegion/Target/Progress.Cleared`、`ChapterIndex`、`Retries` | 部分 | 只有状态串 | 章节解锁、地牢通关、城堡链、交易、重试分别成事件 | 否 |
| 技能与召唤 | `ActiveBuffCount/ActiveFlightCount`、`Summons`、`SkillAiDiagnostics`（文本有界） | 部分 | 无逐次施放/召唤结构化事实 | 复用现有有界诊断，不复制技能逻辑；逐施放计数**未采集**（见第 7 节提案） | 待批准 |
| 可疑停滞诊断 | `EntryDiagnostics`/`EngagementDiagnostics`（文本） | 缺失 | 无分级、无限频 | `AnomalyDiagnoser`：MANUAL_GATE / XP_RATE_ZERO / COMBAT_FAILURE_LOOP / SUSPECTED_STUCK / RUNNING / UNDETERMINED，有界现场+限频+限次 | 否 |
| 结果版本化 | 本轮新增 | 缺失 | 无指纹/哈希 | `metadata.json` 含配置指纹 SHA256、代码版本（直读 `.git`）、策略摘要、业务一致性哈希、`unavailable` 列表 | 否 |
| 旧经济模拟 | `tests/EconomySimChecks.cs` | 已具备 | 事件时间只有整数分钟 | 保留为历史兼容入口，不再扩展；精确秒级事件由新运行器提供 | 否 |

### 2 M2-B/C/D/E 补齐内容与修改文件

| 文件 | 内容 |
|---|---|
| `tests/Simulation/SimulationConfig.cs`（新） | 版本 1/2 配置与 `player_policy`；20 项非法配置拒绝；策略归一化为 `SimulationPolicy`（含 `Summarize()` 追溯串） |
| `tests/Simulation/SimulationRunner.cs`（重写） | 逐 Tick 事件/经验/金币采集，策略执行器，采样，`metadata.json`/`fields.md`/`diagnostics.md` 输出，`SIM_HASH`/`SIM_LEDGER`/`SIM_XP`/`SIM_DIAG` 控制台摘要 |
| `tests/Simulation/SimulationRecord.cs`（新） | CSV/JSON 写出、配置指纹、代码版本、业务一致性哈希（不含墙钟与文件名） |
| `tests/Simulation/AnomalyDiagnoser.cs`（新） | 分级器：进度签名（**重试不计入进展**）、经验平台单独预算、限频与限次、有界现场 |
| `tests/SimulationToolChecks.cs`（新，并入默认回归） | 加载/拒绝、显式策略精确性、输出完整性、同 Seed 复现、分级器区分度 |
| `tests/Program.cs` | 新增 `--simulation-tool-checks` 并放入默认回归序列 |
| `tests/Simulation/E_min_party.json`（新，版本 2） | 精确 E：只买 `s_root`/`s_party_1`/`s_party_2` 各 1 级、编入 warrior/ranger/mage、不做天赋与装配 |
| `tests/Simulation/F_policy_probe.json`（新，版本 2） | 工具缺口定向：星图前置拒绝、职业未解锁跳过、天赋行门槛拒绝，并保留一条成功购买作对照 |
| `tests/Simulation/T_tool_probe.json`（新，版本 2） | 工具自检专用短场景（6 分钟），不参与数值评价 |

`A_pure_idle` / `B_guided_progress` / `C_castle_only` / `D_solo_growth` 保持版本 1 原文件未改；`E_party_only` 保留为**历史近似 E**（四开关语义），与 `E_min_party` 并存，两者不可做严格数值对照。

### 3 配置示例（E_min_party 关键片段）

```json
"player_policy": {
  "castle":  { "enabled": true, "interval_seconds": 60, "max_requests": 0 },
  "starmap": { "mode": "explicit", "interval_seconds": 60,
               "nodes": [ { "id": "s_root", "max_level": 1 },
                          { "id": "s_party_1", "max_level": 1 },
                          { "id": "s_party_2", "max_level": 1 } ] },
  "party":   { "mode": "explicit", "interval_seconds": 60,
               "roster": ["warrior", "ranger", "mage"], "target_size": 3 },
  "talents": { "mode": "none" },
  "diagnostics": { "stall_window_seconds": 600, "recheck_seconds": 300, "max_alerts": 5 }
}
```

### 4 测试与验收结果

| 检查 | 结果 |
|---|---|
| 游戏工程 / 检查工程构建 | 均 0 警告 0 错误（检查工程 `-t:Rebuild`） |
| 全量 Core Checks | 退出 0，31.6s，全部 PASS（含新 `--simulation-tool-checks`） |
| 工具自检 | 版本 1 兼容、版本 2 显式、20 项非法配置拒绝、缺省动作关闭；显式购买/编队与配置**完全一致**（无 `s_skill_1` 等额外购买，`talents.mode=none` 无学习/装配）；7 份输出文件齐全；同 Seed 复跑四份机器数据逐字节一致；分级器 5 类判别正确且告警有界 |
| Godot headless smoke | 退出 0，3.7s，`SMOKE_OK` |
| 六场景 240/30 分钟运行 | 全部退出 0，详见第 5 节 |
| 同 Seed 确定性（B/C/E） | 业务哈希双跑一致：B `dc338e6c…a49381`、C `afa87089…ae27f0b`、E `15ae9ef7…7af7e2a`（哈希覆盖 snapshots/events/ledger/diagnostics） |
| 金币对账 | A/B/C/D/E/F 六场差额全部为 **0**（见第 5 节 `diff`） |
| 经验对账 | 六场 `unexplained_ticks` 全部为 0；每次经验增量都能被击杀基础值×等级差倍率或宝箱事件解释 |
| 挑战链抽样（C） | `castle_click` 240.05s accepted → `castle_enter` 301.45s → `castle_result` 348.40s victory，三段同 POI 可追 |
| 经验抽样（C） | 21.85s 宝箱 `room_event basis=8` 与同 Tick `level_up` 相邻：`hundredths_delta=800`（等级差 0 → 100%）、升级后余 `2+0/100`；击杀类增量按 Tick 逐笔核对（`verified_ticks`），未逐条落盘以控制体积 |
| 金币抽样（C） | `12.75s loot_pickup room#1 +2 → 余额 2`、`67.25s junk_sale 罗格营地 +40 → trades=1`，每笔带发生后的余额 |
| 默认关闭与隔离 | 缺省 `player_policy` 子对象一律不执行；运行器不调用任何存档写入，玩家 `user://` 不受影响 |

### 5 修复后模拟统计（正式运行事实）

| 场景 | 时长 | 章节 | 等级 | 击杀 | 金币 | 重试 | 通关地牢 | 城堡胜利 | 金币差额 | 经验无法解释 | 诊断标签 |
|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|
| A_pure_idle | 240 | 1 | 7 | 4133 | 30356 | 0 | 3 | 0 | 0 | 0 | XP_RATE_ZERO×1 |
| B_guided_progress | 240 | 3 | 30/30/30 | 4811 | 123355 | 0 | 50 | 17 | 0 | 0 | XP_RATE_ZERO×4 |
| C_castle_only | 240 | 2 | 18 | 3703 | 56895 | 44 | 7 | 8 | 0 | 0 | XP_RATE_ZERO×2 |
| D_solo_growth | 240 | 2 | 24 | 3980 | 78077 | 35 | 40 | 9 | 0 | 0 | XP_RATE_ZERO×4 |
| E_min_party | 240 | 3 | 30/30/30 | 4716 | 118182 | 0 | 46 | 16 | 0 | 0 | XP_RATE_ZERO×4 |
| F_policy_probe | 30 | 1 | 7/7 | 566 | 4086 | 0 | 3 | 0 | 0 | 0 | XP_RATE_ZERO×1 |

- 金币分项：A 拾取 30356；B 拾取 123937 − 星图 582；C 拾取 56895；D 拾取 78077；E 拾取 118260 − 星图 78；F 拾取 4114 − 星图 28。
- **未出现** `SUSPECTED_STUCK`、`MANUAL_GATE`、`COMBAT_FAILURE_LOOP` 告警。A 的 Lv7 平台被正确标记为 `XP_RATE_ZERO`（等级差 5 → 0%，仍在合法击杀/拾取），不是死锁；C/D 的 44/35 次全灭返村属合法失败，因击杀持续而未被误报。
- E 的购买与扩编链完全等于配置：`s_root(3)`/`s_party_1(25)` 在 60.1s 成交、`s_party_2(50)` 在 120.1s 成交，合计 78 金币；编队 `warrior|ranger` → `warrior|ranger|mage` 随席位扩编分两步生效，无额外天赋或装配事件。
- 目录覆盖说明：`artifacts/simulation/{A,B,C,D}_20261023/` 由本轮新运行器重新生成（新增 ledger/metadata/diagnostics 与精确 Tick 时间），上一轮同目录 CSV 已被覆盖；上一轮结论已完整转录在[P0-B 统一统计](../data/2026-10-23_22-12_data_p0_b_growth_stall.md)，死锁现场存档与探针日志仍分别在 `artifacts/qa/audit-aud001-stall/` 与 `artifacts/audit-probe/`。

### 6 数据字段与复跑

- 每个场景目录含 `metadata.json`（策略、Seed、指纹、代码版本、计数、`unavailable`）、`snapshots.csv`、`events.csv`、`ledger.csv`、`diagnostics.csv`、`fields.md`（字段与单位）、`diagnostics.md`（告警现场）。
- 采样：`t=0`，前 30 分钟每 1 分钟，此后每 30 分钟；事件时间一律 `tick×0.05s`。
- 复跑：

```powershell
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj                                   # 全量，含 --simulation-tool-checks
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation-tool-checks       # 只跑工具自检
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/E_min_party.json
```

   控制台尾行给出 `SIM_HASH`（业务哈希）、`SIM_LEDGER`（收支对账）、`SIM_XP`（经验对账）、`SIM_DIAG`（诊断标签）、`SIM_END`（终局状态），可直接用于双跑比较。

### 7 未覆盖项与 Core 只读扩展提案（未实施，待批准）

仍不可观测（已在 `metadata.json` 的 `unavailable` 明示，**不以 0 充数**）：击杀的攻击者身份、逐次技能施放/命中/召唤事件、逐技能伤害与承伤分解、城堡失败的具体致死序列。

最小接口提案（**仅在获批后实施**）：

| 项 | 内容 |
|---|---|
| 调用点 | `SkillRuntime.SettleDamage`（击杀/命中落点）、`HeroExperience.GrantHeroExperience`（经验授予落点）、`SkillRuntime.Commit`（施法落点）、`SummonRuntime.SummonEffect`（召唤落点） |
| 只读字段/事件 | `IReadOnlyList<CombatFact>` 有界环形缓冲（容量建议 256～512，仅内存）：`{Tick,Kind(kill/hit/xp/cast/summon),ActorId,TargetId,SkillId,Amount,Level}` |
| 用途 | 逐笔归因攻击者、逐技能伤害与施放计数、召唤数量与存活，替代当前 `attacker=unavailable` |
| 性能/存档影响 | 只在测试入口开启写入（`Enabled` 默认 false），固定容量、无分配扩容、不进存档、不参与判定 |
| 回归 | 现有 `SettleDamage`/`GrantHeroExperience` 数值不变；新增检查只验证缓冲内容与结算事实一致 |

### 8 下一阶段阻塞项

1. 上述 Core 只读接口未获批 → 技能/召唤/攻击者维度仍只能是文本诊断，不能做逐技能数值分析。
2. 旧 `--economy-sim` 仍是整数分钟事件时间，若后续报告仍引用它必须标注精度。
3. `E_party_only`（历史近似 E）与 `E_min_party`（精确 E）并存，跨版本对比只能看趋势。
4. 未做视觉验收与实机长稳；无头通过不代表画面正确。

### 9 结论

M2 的工具能力已补齐：任何一次模拟都能回答“用了哪套策略、实际做了什么、产生哪些结算、出现什么异常、能否复现”，并明确指出还无法观测什么（第 7 节）。金币对账与经验对账在六个场景全部闭合，异常分级能区分设计平台、合法失败与真实停滞。**验收结论：除第 7 节列明的 Core 只读采集缺口外，M2 测试侧能力通过**；不涉及任何数值设计结论。

## P2-1-M3 模拟环境系统验收（2026-10-23）

结论：**`M3_TEST_ENV_PASS`**，可进入 P2-2；同时保留 `M3_COMBAT_FACTS_PENDING`（逐技能/攻击者/召唤事实仍需 Core 只读接口，未获批前不得宣称具备 P2-4 全技能逐项核验能力）。完整矩阵、六类数据与复跑命令见[M3 统计与矩阵](../data/2026-10-23_23-32_data_p2_1_m3_simulation_acceptance.md)，本文件只记验收事实。

测试侧变更（正式 Core、配置、数值、存档与旧 A–E 场景语义均未改）：

| 文件 | 变更 |
|---|---|
| `tests/Simulation/SimulationRunner.cs` | 新增 `--seed=`/`--tag=`（多 Seed 批次与证据目录隔离）；`policy_order` 事件记录“同 Tick 多组到期只执行一组”；`metadata.json` 增加源码指纹、工作区 unknown 声明与 `perf` 段（耗时/µs·Tick/行数/字节/托管堆峰值/截断说明）；`SUSPECTED_STUCK` 自动落盘 `stalls/stall-<tick>.md`（阶段/章节/POI/房间/坐标与 HP/怪物/预约账本/入房掩码/最后进展/最近 5 条事件/双指纹/复跑命令，导航目标标 `unavailable`） |
| `tests/Simulation/SimulationRecord.cs` | 新增 `SourceFingerprint`（`Scripts/**/*.cs` + `tests/**/*.cs`，排除 bin/obj） |
| `tests/SimulationToolChecks.cs` | 新增断言：城堡关闭零点击、同 Tick 次序留痕、配置指纹可检出改动、源码指纹与 `git_worktree=unknown` 入盘、性能计数非零、**采样密度不改变事件/金币流水** |
| `tests/Simulation/T_tool_probe_sparse.json` | 采样独立性对照场景 |
| `tests/Program.cs` | `--seed`/`--tag` 参数解析 |

验收数据要点：

- 构建 0 警告 0 错误；全量 Core Checks 退出 0（30.7~50.8s，含冷/热与重建差异）；`--room-entry-checks` 退出 0（2.1s，384 边界带场景 + 旧档恢复）；`--simulation-tool-checks` 退出 0（3.4s）；Godot headless smoke 退出 0（3.6s，`SMOKE_OK`）。
- 标准 7 场（Seed 20261023）：**金币差额 0、经验无法解释 0**；A 仍为 Lv7 设计平台但击杀 4133／金币 30356 持续增长（无入房冻结）。
- 多 Seed 30 场：A/C/E × 8 Seed 与 B/D × 3 Seed 全部 `diff=0`、`unexplained=0`，0 条 `SUSPECTED_STUCK`/`MANUAL_GATE`/`COMBAT_FAILURE_LOOP`。
- 确定性：C 同 Seed 双跑业务哈希一致且四份机器文件逐字节相同；同 Seed 不同目录（`m3` vs `m3s`）哈希一致。E 精确性：仅 78 金币三节点购买、编队两步扩编、无天赋/装配动作。
- 失败链可持续（C）：`wipe`(2161.95s) → `village_trade`(+338) → `TravelDungeon` → `dungeon_enter A04-D04` → 继续击杀，无重复结算。
- 性能有界：240 分钟场 4.4–6.6s（16.7–22.8 µs/Tick），产物体积 1.14–1.51 MB/场，托管堆峰值 27–30 MB，诊断 ≤5 条/场并固定截断。

未覆盖并已标 `UNAVAILABLE`：逐技能施放/命中、攻击者身份、逐技能伤害与承伤、召唤生命周期、城堡失败致死序列、洗点扣费路径（通路存在，`TalentChecks` 覆盖）、实机窗口与多平台视觉验收。Core 只读采集提案（`SettleDamage`/`GrantHeroExperience`/`Commit`/`SummonEffect` + 256~512 环缓冲 + 默认关闭 + 开关前后哈希一致验证）停在审批门，**本轮未改任何正式 Core**。

本轮未提交、未推送。

## P0-B补充（2026-10-23_22-12）

新增D单人成长、E最小扩编策略，前30分钟每分钟快照及5分钟现场。正式Core/配置未改。build退出0；五场双轮退出0，事件与快照SHA256一致。报告见[统一统计](../data/2026-10-23_22-12_data_p0_b_growth_stall.md)。英雄存活排除本次永久死亡无人救援解释；疑似入口资格问题未确认。逐击杀经验及精准停滞起点、失败细分和部分定向检查仍未完成，不能视作完整验收。
