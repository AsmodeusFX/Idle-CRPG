# P0 基础规则修复与现状数据审计 执行归档

2026-10-23。范围：召唤物接敌容量修复、跨场景移动速度审计与调平、D03/D04 运行数据与轻量统计、第 2 轮站位输入。第一轮只审计速度（未改），第二轮按批准把正式五章 `travel_speed` 调为 0.31；不涉及装备、星图、经济与站位系统本身。

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

## 五、第二轮：基础规则收口（2026-10-23 追加）

### 1. 跨场景移速调平（已执行）

- 只改**五个正式章节** `chapters/act{1..5}/world/world.csv` 的 `travel_speed`：0.65 → **0.31**；`world_zoom 36`、地牢 `dungeon_max_zoom 62`、城镇 `walk_speed`、`village_speed` 均未动。非正式 `world/world.csv` 与 `legacy/act*/world/world.csv` 保持 0.65（旧档与旧回归口径）。
- 结果：大地图名义速度 0.31×720 ≈ **223 px/s**，与地牢 197–260、主城 ≈223 同档。
- 旅行耗时预计 ×2.1（1 格 1.54→3.23s；8 格 12.3→25.8s；16 格 24.6→51.6s）。
- 回归：全量 Core 通过，含寻路/连续穿门/门先锋/区域切换/旅行速度重选/存读档一致性（`DungeonTravelChecks`、`OutdoorFollowing`、`ChapterChecks` 等）。

### 2. 召唤物接敌回归（补验）

在原有三骷髅容量、Normal 预约、主动承接、死亡释放、补召恢复之外，新增**女武神超时释放**：限时 5 秒到期后召唤物被撤销、容量从 4 回到 3、指向它的锁被清除。检查名 `--summon-capacity-checks`，全部通过。

### 3. D03/D04 轻量统计（已落地）

新增 `Scripts/Core/CombatStatsRuntime.cs`（`RoomCombatStats`），按房累积、内存有界、不入存档，GM 诊断窗口追加 6 行。字段合同见[本房战斗观测统计](../technical/combat_stats.md)。

覆盖：首次友方普攻深度、提前接战原因（近距威胁/战斗锁定）、同目标近战最小间距、友方最近邻、明显重叠对数、远程浅区施法次数、AOE 命中人数分布、治疗时目标血量比例、补召延迟、容量峰值/释放/累计占用、0 容量后排被近战锁定秒数。

尚未覆盖：UI 名字/血条重叠（表现层，需截图）、链式与尸爆未并入 AOE 分布、实机长时间采样。

### 4. 第 2 轮站位开发输入

新增 `--position-scenarios`（确定性，五英雄＋三骷髅 × 五类敌人：单 Boss、五普通近战、近远混合、M06 猎手、M05 范围怪），输出每单位位置、容量与目标关系。

实测样例（同一房间、220 tick）：

| 场景 | 前排 | 后排 | 骷髅 | 观察 |
|---|---|---|---|---|
| 单 Boss | 野蛮人 (0.49,−12.35) 容量3、圣骑士 (0.23,−12.32) 容量5，目标均为 Boss | 游侠/法师/死灵 x≈−1.6～−1.0，目标同 Boss | 三只 x=0.51/0.57/0.63，y=−11.87 | 前排承伤、后排不堵门 |
| 五普通近战 | 野蛮人/圣骑士分别锁 #5/#6 | 游侠/法师/死灵锁 #7/#8 | 三只间距 0.07–0.14U | **骷髅之间已出现贴脸**，是后续站位层的首要输入 |
| 近远混合 | 同上前排 | 远程英雄锁 #7/#8 | 同上 | 近远锁分离正常 |
| M06 猎手 | 猎手优先锁后排 | 后排被锁定 | — | 猎手策略生效 |
| M05 范围怪 | 范围技能施放 | — | — | 读 AOE 分布 |

截图：**NOT RUN**。现有 `--capture` 入口没有对应五场景预设，本轮未新增拍摄器；等站位层开发时一并加。

### 5. V1.2 入场堵门复核

本轮五场景实测未再出现“全队停在门口”的现象：前排进入房间约 1.0–1.5U 后接敌，后排按各自通行独立推进，骷髅独立走到前排；队首接战不会冻结队尾。观察到的真正问题不是堵门，而是**骷髅之间与后排三人之间的站位重叠**（0.07–0.3U），属于站位层，按用户要求本轮不处理，也未修改入场状态机或增加部署补丁。

### 6. 第二轮变更文件与验证

- 配置：`chapters/act{1..5}/world/world.csv`（travel_speed）。
- 代码：新增 `Scripts/Core/CombatStatsRuntime.cs`；钩子接入 `CombatTargeting`（提前接战原因、锁采样、诊断输出）、`GameSession`（首次普攻、每步采样）、`SkillRuntime`（施法统计、房间重置）、`PulseRuntime`（AOE 命中数）、`SkillSupportRuntime`（治疗比例）。
- 测试：新增 `tests/SummonCapacityChecks.cs`（含女武神超时）、`tests/CombatStatsChecks.cs`、`tests/PositionScenarioChecks.cs`；`tests/Program.cs` 注册三个入口。
- 验证：`dotnet build` 0 警告 0 错误；`--summon-capacity-checks`、`--combat-stats-checks`、`--position-scenarios` 全部 PASS；完整 Core Checks 退出码 0；Godot 冒烟 `SMOKE_OK` 退出码 0。

## 六、变更文件

- 配置：`config/skill/summon.csv`（容量 0→1）。
- 测试：新增 `tests/SummonCapacityChecks.cs`，`tests/Program.cs` 注册并入默认回归。
- 文档：本文件、`docs/data/` 两份专项数据、`docs/technical/summon_follow.md`、`docs/technical/combat_config.md`、`docs/reviews/2026-10-23_room_entry_v1_2.md`（补后续说明）、`docs/README.md`。
- 未改：移动速度、镜头缩放、房间尺寸、怪物数量、P1-5 C2–C4、战斗公式与存档结构。
