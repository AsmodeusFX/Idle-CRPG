# 跨平台Agent与Skill自身归档

日期：2026-10-23。

用户要求分析当前工程，形成适合游戏开发的Agent规则和Skill，支持跨机器开发并纳入后续规范。采用skill-creator指导。

## 交付

- AGENTS.md：保留原业务条款，追加跨平台与Skill入口。
- .agents/skills/game-development/SKILL.md：通用游戏开发、诊断、实现和验收流程。
- references/cross-platform.md：工具发现、路径大小写/编码、存档隔离、平台验证矩阵。
- references/idle-crpg.md：本项目设计与技术路由，区分预算、实际运行和QA。
- docs/technical/agent_workflow.md：跨机器入口及规范维护。
- docs/README.md与architecture.md：后续规范索引。
- 本文件：来源、验证与限制。

审计依据：原AGENTS、文档索引和架构、地牢/世界规则、两份csproj、忽略及换行策略、工具清单、ClientSettings.cs自动化存档隔离及空间审计经验。

## 验证与范围

本次纯规范修改，不修改游戏逻辑、配置或玩家存档，不运行Core/Godot。未安装个人技能目录，未改全局环境，未提交或推送Git。Skill自动发现取决于客户端，可显式读取SKILL.md使用。不声称Linux/macOS已验证。

Python命令指向WindowsApps别名，未据此运行quick_validate.py；改用PowerShell检查必需frontmatter、命名、链接存在和原AGENTS前缀保留。沙箱命令初始化与补丁路径检查曾失败，经授权使用命令工具完成写入。