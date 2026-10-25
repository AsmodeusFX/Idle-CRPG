# 跨平台Agent开发工作规范

## 入口

协作职责与授权读取[三方协作Skill V1.1](../../.agents/skills/game-design-validation-workflow/SKILL.md)。本工程目录中的AI认领执行AI，支持用户批准的设计交接与用户直接需求双入口；在授权范围内完成调查、适配、实现、验证与归档，不因计划、规模或常规工程差异重复等待审批。新产品取舍或超范围事项按实际需要提请用户裁决。

每次执行更新模块唯一review，并提供用户可回传设计AI的摘要；分别记录工程自检、设计复核、用户验收。实际接口与设计描述不符时记录事实、采用方案、理由和影响；无设计AI接收证据不得声称已同步。模板见[执行与Review](../../.agents/skills/game-design-validation-workflow/EXECUTION_REVIEW_TEMPLATE.md)和[项目交接](../../.agents/skills/game-design-validation-workflow/PROJECT_HANDOFF_TEMPLATE.md)。本次授权、规则覆盖与验证见[流程归档](../reviews/2026-10-25_design_execution_workflow.md)。

[仓库AGENTS](../../AGENTS.md)为项目规则入口；[游戏开发Skill](../../.agents/skills/game-development/SKILL.md)为可复用工作流，按需读取[跨平台参考](../../.agents/skills/game-development/references/cross-platform.md)及[项目路由](../../.agents/skills/game-development/references/idle-crpg.md)。

文件随仓库携带。客户端不自动发现Skill时显式要求读取SKILL.md，不保证任意AI客户端有相同发现机制。本次不安装个人技能目录；迁移其他游戏时替换项目路由，不继承本游戏数值和业务规则。

## 当前工程事实

- Godot工程在Idle-CRPG/，Godot.NET.Sdk/4.7.2，目标net8.0；tests为net8.0控制台并链接Core和MotionPresentation。
- Core/UI分离、GameSession命令、唯一伤害/奖励/Locks、固定步长与插值是现有约束。
- 正式章节、legacy、QA夹具、规划预算和实际生成分开；CSV字段更新同步字典、校验及迁移。
- ClientSettings.cs将smoke/capture/performance/reopen自动化存档指向artifacts/qa；正式运行用user://。新自动化参数必须确认隔离，不仅凭名称判断。
- .gitignore忽略artifacts、.godot、bin、obj，.gitattributes为text=auto；本次不重写换行。
- PowerShell脚本不是POSIX原生脚本，使用时确认pwsh或另行实现等价入口。Windows静态审计不证明Linux/macOS实际运行通过。

## 后续开发

1. 读AGENTS和专项文档，确认规则版本、目标和授权。
2. 审计真实调用链与配置加载路径，选择最小责任层修改；空间问题先核对位置、时序、投影，存档问题先确认检查点。
3. 保持唯一状态权威；测试验证行为与不变量。旧测试冲突须说明规则变化并保留其他保护，不能删除失败断言制造通过。
4. Core改动运行Core Checks；UI改动编译并跑Godot冒烟；视觉任务加实际渲染。记录平台、版本、命令、退出码、结束标记、证据及未验证项。
5. 通用方法更新Skill，项目约束更新AGENTS，专项设计与执行分别归档。未经明确授权不commit、push或merge。

规范变更检查frontmatter、链接、入口与既有条款；纯规范修改不要求游戏编译。临时环境异常与未验收状态写入执行记录，不泛化成永久限制。创建来源见[自身归档](../reviews/2026-10-23_agent_skill_archive.md)。
