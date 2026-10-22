# Chapter 4（Act IV）群魔堡垒 — 地图与地牢配置 V0.1

**状态：设计候选。非用户 Godot 工程改动，非实机验收。**

## 快速打开

- `Chapter4_Act4_Interactive_Map_V0.1.html`：无需服务器的单文件交互地图；浏览器可查看挑战等级、各层有效格、每格怪物目标数、击杀预算、人工批准模拟。
- `preview/Chapter4_Act4_Map_Grid_V0.1.png`：静态 7×7 地图预览。
- `docs/Chapter4_Act4_Design_and_ClaudeCode_Spec_V0.1.md`：完整章节设计与 Claude Code 集成规范。
- `docs/VALIDATION_REPORT_V0.1.md`：离线数据和浏览器测试记录。

## 核心摘要

| 项 | 数量 / 结论 |
|---|---|
| 世界地图 | 7×7，1群魔堡垒，5区域，20块不可通行深渊与熔岩 |
| POI | 19 Dungeon + 5 Castle，共24 |
| 内容深度 | 77 Floor / Castle Stage，三难度231条楼层记录 |
| 主线 | 郊外大草原→绝望平原（衣卒尔）→神罚之城→火焰之河/混沌避难所（迪亚波罗） |
| 支线 | 地狱熔炉（海法斯特），解锁后需批准，且Castle另需独立批准 |
| 三难度怪物范围 | 普通27–31 / 噩梦62–65 / 地狱89–92 |
| 普通首通击杀预算 | 主线3470，支线795（数值候选） |

## 文件数据

- `data/ACT04_MAP_V0.1.json`：网格、城镇、区域、关口、POI、自动攻略策略。
- `data/ACT04_REGION_V0.1.csv`：区域数量、解锁关系、Castle前置。
- `data/ACT04_POI_V0.1.csv`：POI身份、坐标、关卡层数、三难度挑战等级/推荐等级与总预算。
- `data/ACT04_GATES_V0.1.csv`：唯一的跨区域通行边以及人工授权要求。
- `data/ACT04_FLOOR_PLAN_V0.1.csv`：逐楼层/阶段有效格、怪物密度、BossArena、固定封印守卫及预算。
- `data/ACT04_KILL_ESTIMATE_V0.1.json`：主线与支线分开计算的计划击杀量。

## Claude Code 执行建议

1. 先审计当前实际工程，禁止盲目覆盖已经存在的配置 schema。
2. 确认「小队自主选择目标」「Castle单独人工批准」「支线单独人工批准」及按难度存档已实现。
3. 导入这份 Chapter 4 候选数据时，注意世界格与 Floor 有效格是不同单位，Castle 阶段使用 `PHASE_SEQUENCE`。
4. 五封印/三守卫是战斗事件事实，不是五层地图；D2 原典第四章没有此包大量原创洞穴。
5. 分别做 Core L1、Godot L2、人工视觉 L3 验证，尤其检查火焰之河长桥与大型 Boss 场景。
