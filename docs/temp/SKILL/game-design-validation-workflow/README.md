# 游戏设计协作 Skill（可移植）

- `SKILL.md`：**自包含核心工作法**，可单独分发/装入兼容Agent Skills的环境。
- `PROJECT_HANDOFF_TEMPLATE.md`：新项目或新聊天的项目事实交接模板。
- `EXECUTION_REVIEW_TEMPLATE.md`：给工程执行AI的只读审计/授权开发/验收任务包模板。

用法：在支持 Agent Skills 的环境中，将整个目录作为一个技能使用；如果只允许上传一份文件，直接上传 `SKILL.md`。在 ChatGPT 新聊天中也可直接上传SKILL.md并说明按此工作，但**需要同时提供对应项目的最新设计/工程状态**，否则无法凭工作法自动找回项目事实。
