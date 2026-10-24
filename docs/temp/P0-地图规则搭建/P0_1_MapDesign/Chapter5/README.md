# Chapter 5 / Act V — Claude Code Pack V0.1

**交付**：地图静态配置、交互HTML、完整Markdown规范、三难度楼层怪物预算、离线验证、思维备份。**不是已实现Godot游戏功能。**

## 推荐打开

- `Chapter5_Act5_Interactive_Map_V0.1.html`：双击用 Chrome/Edge 打开，点击区域/地牢，尝试「自动推进」「批准Castle」「批准支线」。
- `docs/Chapter5_Act5_Design_and_ClaudeCode_Spec_V0.1.md`：具体技术配置/任务清单。
- `docs/Chapter5_Design_Decision_Log_V0.1.md`：设计依据与待统一体验反馈项。
- `docs/VALIDATION_REPORT_V0.1.md`：静态测试证明及实机未测试说明。

## 快速统计

11×11 世界地图、11区域(9主线2支线)、53 Dungeon、11 Castle、191独立楼层、573条三难度楼层配置。

## 导入注意事项

1. 当前脚本使用内部候选字段命名，Claude Code 应先只读审计 Godot 实际 schema，再做转换，不要直接覆写正式数据。
2. 测试按 L1/L2/L3 分层如实报告，不得将HTML浏览器模拟等同于Godot实机通过。
3. 本轮先体验五章，之后仅用差异修正文档修改；不要默默改写 V0.1 基线。

## 重新生成与测试

```bash
python tools/generate_act5.py
python tools/build_html.py
python tools/test_browser.py
python tools/build_docs.py
```

脚本里的容器绝对路径是此交付环境的示例；交给 Claude Code 时应改为基于包根目录的相对路径。
