# 本房战斗观测统计（有界诊断）

2026-10-23 新增。目的是给站位、AI 与房间规格开发提供当前版本的实际观测值。**不是埋点平台**：只在本房内存里累加，换房/清房重置，不进存档、不写磁盘、不影响任何判定。

## 数据来源与边界

| 项 | 规则 |
|---|---|
| 存储 | `GameSession.CombatStats`（`RoomCombatStats`），单一实例，随 `ResetSkillRoom` 重建 |
| 有界性 | 固定字段 + `EarlyEngage` 字典按原因键累积（原因只有“近距威胁/战斗锁定”两种）；无列表增长 |
| 只读 | 只读取位置、锁、召唤数量、容量与动作事实；不写回任何状态，不改变 AI/伤害/奖励 |
| 存档 | 不进入 `SessionSnapshot`，读档后从 0 重新累积 |
| 输出 | GM「接敌容量与入场 · 实时诊断」末尾追加 6 行；也可由检查直接读 `CombatStats` |

## 字段

| 字段 | 含义 |
|---|---|
| `Elapsed` | 本房累计观测秒数 |
| `FirstAttackActor` / `FirstAttackDepth` | 本房第一次友方普攻的单位名与当时距入口门线深度（负值表示门外） |
| `EarlyEngage` | 入口通行被提前结束的次数，按原因分：`近距威胁`（1.2U 内）与 `战斗锁定`（施法/来袭弹道/已被锁定） |
| `SameTargetMinDistance` | 同一目标的多只近战攻击者之间的最小间距 |
| `NearestNeighbourMin` | 全场存活友方之间的最小两两距离 |
| `OverlapPairs` | 同目标近战攻击者间距 < 0.35U 的累计对数（每步采样） |
| `MeleeSamples` | 采样步数，用于判断样本是否足够 |
| `RangedShallowCasts` | 远程单位在通行边界之前施法的次数 |
| `AoeCasts` / `AoeHits[1..5]` | AOE 施放次数与单次命中人数分布（5 表示 ≥5） |
| `HealCasts` / `HealRatioSum` | 治疗次数与施放时目标血量比例之和（均值 = 和/次数） |
| `RefillCasts` / `RefillDelaySum` | 有召唤死亡后再次召唤的次数与平均延迟秒数 |
| `CapacityPeak` / `CapacityReleases` / `CapacityOccupiedSeconds` | 普通容量占用峰值、释放次数、存在任意普通占用的累计秒数 |
| `BacklineMeleeSeconds` | 0 容量后排被近战普通锁定的累计秒数 |

## 已知边界

- 只在战斗步（`Fight`）内采样；警觉窗口与走廊不计入。
- AOE 只统计 `AreaPulse`（旋风/范围冲击类）；链式与尸爆未纳入同一分布，避免把不同机制混在一起。
- `RefillDelay` 以“本房上一条召唤消失时刻”为起点，跨房重置，不追踪具体是谁补的。
- 不统计 UI 重叠（名字/血条），那属于表现层，需实机截图。
