# 给 Claude Code：P0-2 第一轮只读审计（可直接复制）

你正在接手 Windows Godot 4.7 .NET C# Steam D2自动小队ARPG的专项交接包 `D2_P0_2_Monster_Encounter_V1_0`。请从 `README.md` 阅读全部材料，特别是`00_主Todo与交付状态.md`、`01_工程只读审计_冲突门禁.md`、`02_M01-M08_怪物行为完整规格.md`、`03_T01-T08_实验遭遇完整规格.md`。

**本轮只允许只读审计，不允许写入源码、配置、场景、资源、存档或已有文档。**

1. 阅读当前仓库真实`CLAUDE.md`、文档索引、已确认Combat/AI/Encounter/Skill/Effect/Buff规范、实际Game.Core / Game.Godot / Tests实现和当前`git status`。以真实代码和最新经确认规则为准。
2. 核实当前敌人AI启动延迟与小队入场散开——这是已经用于解决堵门的最新规则；早期“敌人立即索敌”文档不能反向覆盖。核实此前交给你的远程受击退让V0.1是否已实现，未实现就报告，禁止重复造一套。
3. 针对M01–M08与T01–T08，输出【真实能力映射表】，列现有路径、类/方法、真实实现状态（已实现/部分/缺失）及最小复用方式；重点核查MonsterPolicy、ActionRequest、TargetPolicy、容量/Overflow/Forced、Skill/Effect/Buff、Room/Encounter、召唤、奖励与Boss接口。
4. 输出【冲突与审批表】，包含旧单英雄规范、AI延迟/散开、远程退让、1D逻辑与2D视觉、远程投射物、召唤防刷、房间完成、实际Boss状态、护甲独立专项；每项附源文件证据、方案A/B、成本/存档影响，并指出需要我明确批准的选择。
5. 输出【E1/E2/E3最小文件修改预案】，具体到拟修改路径、是否需要新字段/Effect、已有代码复用、Core单元测试、Godot实机验证和经批准才能删除的旧逻辑。不要新建并行MonsterAI/Encounter系统。
6. 输出【审批问题清单】，只问实际阻断事项，不要让我重复确认已确定的怪物基本规则、多光环、多诅咒、接敌容量、房间AI启动延迟和入场散开。

**报告后立即暂停，等待我的明确批准。不得擅自实施E1，也不得为了让计划跑通而写假Boss、强改收益或护甲公式；TEST_ONLY数据不得导入正式五章内容。**

后续如获得批准，再按`06_切片实施计划与交付标准.md`逐项实现，执行真实`dotnet build`、`dotnet test`和Godot/Mini实机验证，并报告逐项PASS/FAIL/BLOCKED/NOT RUN。维护`00_主Todo与交付状态.md`，严格区分文档完成、已编译和实机验收。
