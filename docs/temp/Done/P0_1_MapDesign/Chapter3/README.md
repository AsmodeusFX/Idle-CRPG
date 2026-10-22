# Chapter 3（Act III）库拉斯地图与地牢设计 V0.1 — 待评审

**先打开 `Chapter3_Act3_Interactive_Map_V0.1.html`**（Chrome/Edge 双击打开，无需联网），查看 10×10 区域地图，点击 Dungeon/Castle 查看三难度挑战等级、逐层格数与预计击杀；可模拟“自动清理→玩家批准 Castle→进入下一地区”，同时可选手动批准庞大湿地/古神庙支线。

## 包含文件

- `Chapter3_Act3_Interactive_Map_V0.1.html`：无依赖交互地图，支持三难度、人工批准、点击地牢、静态冒险模拟。
- `preview/Chapter3_Act3_Interactive_Preview_V0.1.png`：浏览器中的完整页面截图。
- `preview/Chapter3_Act3_Map_Grid_V0.1.png`：10×10 地块与 POI 截图。
- `docs/Chapter3_Act3_Design_and_ClaudeCode_Spec_V0.1.md`：完整方案（位置/名字/地牢层数/怪物预算/解锁/任务/实现边界）。
- `docs/VALIDATION_REPORT_V0.1.md`：结构及浏览器验证记录。
- `data/ACT03_MAP_V0.1.json`：世界格、区域、Gate 和全部 POI 的参考配置。
- `data/ACT03_REGION_V0.1.csv`：区域及授权和前置规则。
- `data/ACT03_POI_V0.1.csv`：53 POI 坐标、三难度挑战/推荐等级、楼层数等。
- `data/ACT03_FLOOR_PLAN_V0.1.csv`：91 内容楼层×3难度的 273 条格子组成和怪物数量预算。
- `data/ACT03_GATES_V0.1.csv`：9 个跨区域显式关口。
- `data/ACT03_KILL_ESTIMATE_V0.1.json`：按难度聚合击杀预算。
- `tools/*.py`：可复现的数据生成、文档生成、交互页面、测试与预览脚本。

## 状态与注意事项

- **正式沿用的规则**：5 章 3 难度、全自动选择最低挑战等级、手动授权所有 Castle 及可选支线、可跨章节返回支线、三难度共用地图结构。
- **尚未获用户批准的本轮方案**：10×10 的具体地形、9 区 POI 布局、44 地牢 + 9 Castle 的数量与名字、逐层格数/密度/挑战值；均为候选内容。
- **不是正式 Godot 配置 schema**：Claude Code 必须先审计实际 Godot C# 工程，确认已有解析器/运行时的数据结构，避免未经审批的整体重构。
- **没有实机完成**：浏览器交互模拟和离线数值预算不能替代真实地牢生成、3–5 人自动战斗、路径规划、技能 BD 与收益测试。
- 原作地名为内部设计参考，商业发布时需处理相关名称和美术的授权问题。
