# Idle-CRPG 开发规范

- 开发前阅读 `docs/README.md`、`docs/design/dungeon_combat.md`、`docs/technical/architecture.md`。
- Godot 工程仅在 `Idle-CRPG/`；归档文档在根目录 `docs/`。
- 使用 C#。纯逻辑在 `Scripts/Core/`，不得引用 Godot；节点、绘制、输入在 `Scripts/UI/`。
- 数值统一使用 `Idle-CRPG/config/*.csv`，UTF-8。字段改变同步更新字段字典和校验。
- UI 只能通过 GameSession 命令改变游戏状态；伤害和奖励只有一份实现。
- 结构在进入地牢前生成；内容在首次跨门进入时快照生成。回访禁止重复刷怪或奖励。
- 不得预先揭示邻居。探索历史在本次挑战各层保留；走廊无怪。
- 连续移动，不允许用房间切换代替角色走门。全层清空后才允许换层。
- 清房后优先走到阵亡队友处读条复活，全部救援完成才继续探索、换层或通关。全灭自动回同一地牢一层入口，刷新各层内容和探索，保留队员、等级、经验及拾取等累计资源。
- 公开接口、公式、状态边界写中文注释。原型默认值不能标成最终策划。
- 核心变更运行 `dotnet run --project tests/IdleCrpg.Checks.csproj`；界面变更编译并执行 Godot 冒烟检查。
- 不提交缓存、构建产物、截图、存档。
- 完成改动并通过相关自检后，允许自动提交 Git，无需再次确认；自检失败时不得提交。不自动推送 Git，推送需用户明确授权。
