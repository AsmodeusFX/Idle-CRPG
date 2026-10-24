# Chapter 2 / Act II 地图设计 V0.1（候选）

推荐先打开 `Chapter2_Act2_Interactive_Map_V0.1.html` 查看地块、地牢与人工批准模拟。然后阅读 `docs/Chapter2_Act2_Design_and_ClaudeCode_Spec_V0.1.md`。

## 包内文件
- `Chapter2_Act2_Interactive_Map_V0.1.html` — 独立 HTML，无网络依赖；可点击地图/POI，模拟三难度的 Castle 与支线手动批准。
- `preview/Chapter2_Act2_Interactive_Preview_V0.1.png` — 页面视觉预览。
- `docs/Chapter2_Act2_Design_and_ClaudeCode_Spec_V0.1.md` — 完整设计与 Claude Code 接入规范。
- `data/ACT02_MAP_V0.1.json` — 9×9 格子、区域、Gate 与 49 个 POI 的参考结构。
- `data/ACT02_REGION_V0.1.csv` — 9 区结构、前置和支线权限。
- `data/ACT02_POI_V0.1.csv` — 49 POI 三难度等级及地牢层数。
- `data/ACT02_FLOOR_PLAN_V0.1.csv` — 77 地牢层×三难度，共 231 行格数/怪量/击杀预算。
- `data/ACT02_GATES_V0.1.csv` — 9 组显式跨区关口和魔法传送。
- `data/ACT02_KILL_ESTIMATE_V0.1.json` — 从 Floor 预算派生的首通怪物击杀估算。
- `tools` — 可复现生成脚本与 HTML 模板。

## 状态说明
- 已定系统规则：地面不战斗、自动攻略等级排序、Castle 和支线人工授权、可返回旧章节支线、三难度地图复用。
- **未获用户最终批准**：本章 9×9 具体地图、49 POI、具体层数、格数、怪物数和数值；数据是设计工作样例。
- 所有校验均为离线结构/浏览器演示校验，**未对用户 Godot 工程进行实现或测试**。
- Windows 可以直接双击 HTML 文件，用 Chrome / Edge 查看。
