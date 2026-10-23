# Steam D2｜P0-A 模拟玩家对照统计

本轮重新运行 A/B/C，各 240 游戏分钟。仅运行测试及整理报告，未修改正式配置、数值、玩法规则或玩家存档。以下为本轮实测，不能沿用旧报告中“B 停在 L7”的结论。

## 1 执行与测试覆盖

| 项 | 值 |
|---|---|
| 入口 | tests/Simulation/SimulationRunner.cs；命令见附录 |
| 环境 | Windows；dotnet SDK 10.0.301；测试目标 net8.0 |
| 构建 | dotnet build tests/IdleCrpg.Checks.csproj；退出 0，零警告、零错误 |
| 配置 | 正式 Idle-CRPG/config，经 LegacySkillConfig.Load 与 CreateWorld 加载 |
| 种子 | 三场景均为 20261023 |
| 初始状态 | warrior 单英雄，L1/经验0，金币0，星图0 |
| 驱动 | GameSession.Tick(0.05f)，每场景288000 Tick；事件时间为完成Tick/20秒 |
| 采样 | 0/30/60/90/120/150/180/210/240 游戏分钟 |
| 操作检查 / 城堡冷却 | 均为60模拟秒；首次检查在第1分钟 |
| 城堡命令 | GameSession.RequestCastleChallenge；正式门禁与真实战斗，不直接设置通关 |
| 运行结果 | A/B/C 均退出0，PASS simulation；仅表示跑完时长，不代表无停滞或全部验收通过 |
| 未覆盖 | Godot/视觉、其他平台、同Seed重复验证、完整Core回归、逐击杀有效经验、逐次死亡及城堡失败归因 |

| 场景 | 城堡模拟点击 | 成长 | 星图 | 编队 |
|---|---|---|---|---|
| A_pure_idle | 关 | 关 | 关 | 关 |
| B_guided_progress | 开 | 开 | 开 | 开 |
| C_castle_only | 开 | 关 | 关 | 关 |

B 沿用原固定节点购买顺序及天赋/技能策略，编队候选仅 warrior/ranger/mage；不能代表完整玩家培养策略。动作关闭不影响游戏自身自动行为。

## 2 成长时间轴（实测）

英雄列格式为职业:等级:当前等级经验；该经验不是累计有效经验收益。章节为1起算序号，地图ID保留原值。

| 场景 | 分钟 | 英雄 | 金币 | 击杀 | 章节 | 队伍 | 星图节点 | 重试 | 区域 / 地图 / 状态 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---|
| A | 0 | warrior:1:0 | 0 | 0 | 1 | 1 | 0 | 0 | 36 / A01-D02 / Village |
| A | 30 | warrior:7:0 | 3737 | 511 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 60 | warrior:7:0 | 7427 | 1009 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 90 | warrior:7:0 | 11123 | 1535 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 120 | warrior:7:0 | 15150 | 2061 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 150 | warrior:7:0 | 18729 | 2557 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 180 | warrior:7:0 | 19211 | 2621 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 210 | warrior:7:0 | 19211 | 2621 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| A | 240 | warrior:7:0 | 19211 | 2621 | 1 | 1 | 0 | 0 | 42 / A01-D03 / Dungeon |
| B | 0 | warrior:1:0 | 0 | 0 | 1 | 1 | 0 | 0 | 36 / A01-D02 / Village |
| B | 30 | warrior:12:0 / ranger:12:0 / mage:12:0 | 5729 | 583 | 1 | 3 | 15 | 0 | 22 / A06-D01 / TravelDungeon |
| B | 60 | warrior:16:1 / ranger:16:2 / mage:16:2 | 16366 | 1215 | 1 | 3 | 15 | 0 | 53 / A08-D03 / Dungeon |
| B | 90 | warrior:19:1 / ranger:19:0 / mage:19:0 | 27611 | 1868 | 2 | 3 | 15 | 0 | 12 / A02-D03 / Dungeon |
| B | 120 | warrior:22:2 / ranger:22:0 / mage:22:0 | 42984 | 2474 | 2 | 3 | 15 | 0 | 45 / A09-C01 / Dungeon |
| B | 150 | warrior:24:10 / ranger:24:9 / mage:24:9 | 58903 | 3043 | 2 | 3 | 15 | 0 | 71 / A07-C01 / Dungeon |
| B | 180 | warrior:27:6 / ranger:27:4 / mage:27:4 | 78928 | 3661 | 3 | 3 | 15 | 0 | 56 / B02-C01 / Dungeon |
| B | 210 | warrior:29:3 / ranger:29:1 / mage:29:1 | 99483 | 4225 | 3 | 3 | 15 | 0 | 27 / B09-D03 / Dungeon |
| B | 240 | warrior:30:1 / ranger:30:9 / mage:30:9 | 121697 | 4833 | 3 | 3 | 15 | 0 | 97 / B07-D03 / Dungeon |
| C | 0 | warrior:1:0 | 0 | 0 | 1 | 1 | 0 | 0 | 36 / A01-D02 / Village |
| C | 30 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 60 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 90 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 120 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 150 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 180 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 210 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |
| C | 240 | warrior:10:24 | 2980 | 341 | 1 | 1 | 0 | 0 | 9 / A03-D03 / Dungeon |

## 3 资源收支

下列为相邻30分钟金币余额变化，包含收入与消耗的净结果，不能当作毛收入。分项金币获得/花费、累计有效经验：unavailable，现有采集器未记录交易与经验流水。

| 场景 | 截止分钟 | 金币净增 | 击杀增量 |
|---|---:|---:|---:|
| A | 30 | 3737 | 511 |
| A | 60 | 3690 | 498 |
| A | 90 | 3696 | 526 |
| A | 120 | 4027 | 526 |
| A | 150 | 3579 | 496 |
| A | 180 | 482 | 64 |
| A | 210 | 0 | 0 |
| A | 240 | 0 | 0 |
| B | 30 | 5729 | 583 |
| B | 60 | 10637 | 632 |
| B | 90 | 11245 | 653 |
| B | 120 | 15373 | 606 |
| B | 150 | 15919 | 569 |
| B | 180 | 20025 | 618 |
| B | 210 | 20555 | 564 |
| B | 240 | 22214 | 608 |
| C | 30 | 2980 | 341 |
| C | 60 | 0 | 0 |
| C | 90 | 0 | 0 |
| C | 120 | 0 | 0 |
| C | 150 | 0 | 0 |
| C | 180 | 0 | 0 |
| C | 210 | 0 | 0 |
| C | 240 | 0 | 0 |

## 4 挑战与效率曲线

城堡“接受”表示正式入口接受安排，不等于开战。胜利以正式已通关集合中的新增城堡记录为准；单次失败、开战次数与死亡计数未采集，均为 unavailable，不能用重试0代替死亡0。

| 场景 | 最终金币 | 金币净增/分 | 击杀/分 | 城堡安排接受 | 城堡胜利事件 | 最终章节 | 全灭返村重试 |
|---|---:|---:|---:|---:|---:|---:|---:|
| A | 19211 | 80.05 | 10.92 | 0 | 0 | 1 | 0 |
| B | 121697 | 507.07 | 20.14 | 25 | 25 | 3 | 0 |
| C | 2980 | 12.42 | 1.42 | 2 | 2 | 1 | 0 |

## 5 关键成长事件

事件时间按模拟秒换算分钟；以下取首次可挑战、首次点击、首次胜利、跨章及成长操作的首次/末次事件。星图购买使用外层统一模拟时钟，不取嵌套字符串的时间。

| 场景 | 模拟分钟 | 事件 | 对象 / 结果 |
|---|---:|---|---|
| A | 3.2533 | castle_available | A01-C01 / available |
| B | 1 | player_growth | B_guided_progress,1,star,s_root / observed |
| B | 2 | player_growth | B_guided_progress,2,formation,warrior / ranger / mage / observed |
| B | 3.0092 | castle_available | A01-C01 / available |
| B | 4 | castle_click | A01-C01 / accepted |
| B | 4.9433 | castle_result | A01-C01 / victory |
| B | 6 | player_growth | B_guided_progress,6,star,s_skill_4 / observed |
| B | 73.6783 | chapter_unlock | 2 / observed |
| B | 151.7942 | chapter_unlock | 3 / observed |
| C | 3.2533 | castle_available | A01-C01 / available |
| C | 4 | castle_click | A01-C01 / accepted |
| C | 5.8058 | castle_result | A01-C01 / victory |

## 6 异常清单

| 类型 | 事实 | 证据与限制 |
|---|---|---|
| A 后段停滞 | 180/210/240分钟均为2621击杀、19211金币、L7、A01-D03/Dungeon | 最后一次被采集状态变化在153.4458分钟；状态采集不含房间内动作，无法定位精确停止时间和原因 |
| C 较早停滞 | 30至240分钟均为341击杀、2980金币、L10经验24、A03-D03/Dungeon | 最后一次被采集状态变化在21.7892分钟；不能据此归因为实力不足或城堡门禁 |
| 经验零值易误读 | A 在30分钟时当前等级经验为0；B在30分钟三位英雄当前等级经验也为0，但后来持续升级 | 当前等级经验0不等于当次击杀有效经验0；首次有效经验归零时间/处理链为 unavailable |
| 编队策略不完整 | B 最终仅3人 | 固定候选未覆盖所有职业，未证明其他职业不满足解锁条件 |
| 统计字段不足 | 城堡失败/死亡、金币毛收支及累计经验未采集 | 标为 unavailable，不推断为0 |

## 7 功能断点与对照结论

1. 实测 B 在120分钟达到第2章、L22，240分钟达到第3章、L30，25条城堡胜利事件；加入合法城堡点击与培养后能够跨章。本轮不能沿用旧模拟“B四小时停L7”的结论。
2. C 接受2次城堡安排且记录2次胜利，说明仅模拟城堡点击也能突破第一座城堡；后续长时间停在普通地牢。实际停止原因尚未验证。
3. A 没有发出城堡挑战命令，符合关闭开关的基线；但后段连金币和击杀也停止增长，不能仅用“等待手动攻城”解释全部现象。
4. B 的第2/3章同时仍出现 A/B 前缀地图ID；这里如实记录章节序号与地图ID，不将ID前缀直接等同章节，不据此判定配置错误。

## 8 后续建议（本轮不修改玩法）

| 优先级 | 事项 | 原因 |
|---|---|---|
| P0 | 针对A/C停滞保存房间、存活单位、位置、目标、寻路及救援状态诊断 | 当前只有阶段/等级变化日志，无法定位停止原因 |
| P0 | 补逐击杀有效经验及金币产消流水 | 当前经验余额和金币净增不足以解释收益处理链 |
| P1 | 补城堡真实开战/失败、死亡、合法阻断原因记录 | 安排接受与战斗结果应分别观测 |
| P1 | 完成相同Seed复跑、四开关与门禁定向回归 | 本次单轮运行不能证明确定性和全部隔离要求 |

## 附：如何复跑

```text
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/A_pure_idle.json
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/B_guided_progress.json
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --simulation tests/Simulation/C_castle_only.json
```

运行器将原始数据写到 artifacts/simulation/<scenario>_20261023/；该位置复跑会覆盖。本轮另留带时间的原始副本如下，均不进入 docs/data/。
本轮时间（Asia/Shanghai）：2026-10-23_21-54；原始快照与事件：`artifacts/simulation-runs/2026-10-23_21-54/<scenario>_20261023/{snapshots,events}.csv`。
