# P0 基础规则修复与现状数据审计 执行归档

2026-10-23。范围：召唤物接敌容量修复、跨场景移动速度审计、D03/D04 运行数据。**速度配置未改**（需批准）；不涉及装备、星图、经济与站位系统。

数据报告：[移动速度审计](../data/2026-10-23_movement_speed_audit.md)、[D03/D04 现状数据](../data/2026-10-23_runtime_state_audit_d03_d04.md)。规则更新：[召唤跟随与容量](../technical/summon_follow.md)、[技能配置字段](../technical/combat_config.md)。

## 一、召唤物接敌容量（已修复）

| 项 | 变更 |
|---|---|
| `config/skill/summon.csv` | `nec_skeleton`、`ran_valkyrie` 的 `engagement_capacity` 由 0 改为 **1**；两者 `role=melee`，属近战前排定位，与用户建议一致，无冲突 |
| 运行时链路 | 未改代码：`EngagementCapacity(actor)` 对召唤物取 `SummonDefinition.EngagementCapacity` 作基础值，再聚合 Buff/被动后向下取整 |
| 影响面 | 三只骷髅合计 3 点普通容量；怪物可正常以骷髅为 Normal 主锁；满额后仍可 Overflow/Ranged 攻击但不超额承接 |

验证：新增 `tests/SummonCapacityChecks.cs`（`--summon-capacity-checks`，已并入默认回归）覆盖：

- 两条召唤定义容量均为 1 且为近战定位；
- 三只骷髅各自容量 1、合计 3；
- 普通近战怪以骷髅为 Normal 主锁，且单只骷髅不超过 1 个普通预约；
- 击杀一只后总容量降为 2、指向它的锁被释放，剩余预约不超过 2；
- 补召一只后容量恢复到 3。

既有回归全部保持（含 `--room-entry-checks` 里“容量来源与 summon.csv 一致”的断言）。

## 二、跨场景移动速度审计（只审计，未改）

结论摘要（完整数据见[审计报告](../data/2026-10-23_movement_speed_audit.md)）：

| 场景 | 当前名义屏幕速度 | 依据 |
|---|---|---|
| 地牢（6×5/7×6/8×7） | 223–260 px/s | 职业 3.6–4.2 U/s × zoom 62 |
| 地牢（9×8） | 197–230 px/s | zoom 自动降到 54.7 |
| 主城（5 城） | ≈223 px/s | walk_speed 与 zoom 反比，五城一致 |
| 大地图 | **468 px/s** | 0.65 cell/s × `world_units 20` × `world_zoom 36` |

- 职业速度在三种场景保持一致倍率（大地图由队伍最快者领跑）；城镇为独立固定值。
- 无隐藏场景加速；入场 150% 只在入场/追赶窗口生效。
- 世界旅行速度参与寻路与抵达判定，**不影响**奖励结算与存档进度。

推荐（待批准，本轮未执行）：仅把 `world.csv travel_speed` 0.65 → **0.31**（目标 220 px/s），不改 `world_zoom`、不改地牢 `dungeon_max_zoom`、不改城镇 walk_speed。代价：同路程旅行时长约 ×2.1（1 格 1.54s→3.23s；8 格 12.3s→25.8s；16 格 24.6s→51.6s）。若只想缓解而非对齐，可先取 0.45（≈324 px/s，×1.44）。

## 三、D03/D04 运行数据

见[D03/D04 现状数据](../data/2026-10-23_runtime_state_audit_d03_d04.md)。要点：

- D03：入场参数全部配置化（通行 1.5U、近距威胁 1.2U、近战纵深 2.8U、远程 0.9U、警觉 0.8/2.0s、出生避让 0.8+1.5U）；四方向×四种房间、逐单位交接、提前接战、边界停手、避让可复现均已由 Core 断言。**首次索敌/停止/攻击深度的逐帧数值、交火分布、最近邻距离、名字血条重叠未测量**。
- D04：49 身份→模板/行为已定（`act_monsters.csv` + `act_behaviors.csv`）；可运行行为清单为 近战/远程/范围/猎手/支援/Boss 技能包/4 类精英词缀；**M07 召唤生产投放、其余 6 词缀、四段剧本未实现**。自动施法的频次、跳过/取消占比、AOE 单目标比例、治疗血线、补召延迟**均未做累计埋点**，只在 GM 有界诊断里可见最近 24 条。

## 四、验证

| 命令 | 结果 |
|---|---|
| `dotnet build Idle-CRPG/Idle-CRPG.csproj` | 0 警告 0 错误 |
| `dotnet run --project tests/IdleCrpg.Checks.csproj -- --summon-capacity-checks` | PASS |
| `dotnet run --project tests/IdleCrpg.Checks.csproj` | 全量 PASS |
| Godot `--headless -- --smoke-test` | 全 UI 冒烟 + `SMOKE_OK`，退出码 0 |

未验证：实机速度手感对比录像、长时间挂机、多显卡后端、玩家主观节奏。

## 五、变更文件

- 配置：`config/skill/summon.csv`（容量 0→1）。
- 测试：新增 `tests/SummonCapacityChecks.cs`，`tests/Program.cs` 注册并入默认回归。
- 文档：本文件、`docs/data/` 两份专项数据、`docs/technical/summon_follow.md`、`docs/technical/combat_config.md`、`docs/reviews/2026-10-23_room_entry_v1_2.md`（补后续说明）、`docs/README.md`。
- 未改：移动速度、镜头缩放、房间尺寸、怪物数量、P1-5 C2–C4、战斗公式与存档结构。
