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

## P0-B补充（2026-10-23_22-12）

新增D单人成长、E最小扩编策略，前30分钟每分钟快照及5分钟现场。正式Core/配置未改。build退出0；五场双轮退出0，事件与快照SHA256一致。报告见[统一统计](../data/2026-10-23_22-12_data_p0_b_growth_stall.md)。英雄存活排除本次永久死亡无人救援解释；疑似入口资格问题未确认。逐击杀经验及精准停滞起点、失败细分和部分定向检查仍未完成，不能视作完整验收。
