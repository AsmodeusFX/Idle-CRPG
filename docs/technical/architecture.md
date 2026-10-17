# 工程架构

`Idle-CRPG/Scripts/Core/` 是不依赖 Godot 的 C# 模型。Config / WorldConfig 负责 CSV 与跨表校验，Dungeon 负责拓扑，MonsterSpawning 负责刷怪快照与奖励成长，WorldCampaign / ExpeditionPlanner 负责自动远征、相邻路径、统治进度与交易。GameSession 是 UI 唯一状态命令入口，持有固定队员、金币、背包、经验与当前地牢。坐标使用 System.Numerics.Vector2。

`Scripts/UI/Main.cs` / `WorldView.cs` 只处理 Godot 控件、地图选择和绘制，每固定 0.05 秒推进逻辑，速度倍率改变推进次数。视图切换不影响模拟；世界外层用完整网格，场景视图使用等距绘制。角色与地貌为程序占位资源。

用 Godot 4.7 .NET 打开 `Idle-CRPG/project.godot` 并运行。命令行 `dotnet build Idle-CRPG/Idle-CRPG.csproj`；核心检查 `dotnet run --project tests/IdleCrpg.Checks.csproj`。Godot 可通过 `--headless --path Idle-CRPG -- --smoke-test` 自动推进并退出。

配置错误直接报错，不静默使用默认数值。模型不读 UI，不在绘制期间发奖励，不把房间渲染状态作为战斗状态。

GameSession 在清房后优先执行救援状态机：前往尸体 → 范围内读条 → 恢复生命。全灭时丢弃当前地牢结构与内容，清零临时行动状态，保留原队员实例及所有会话累计资源，通过 WorldCampaign.RespawnInVillage 直接进入当前区域村庄；复活与正常返村复用 EnterVillage，必须完成交易后才再次选目标。失败地牢不记通关。世界远征通过 CreateWorld 进入；单地牢独立模拟若全灭则接入出生区域的村庄流程。Tick 根据世界阶段推进赶路、地牢或村庄。地牢通关不会终止世界 Tick，而是标记通关并安排返村。

金币以 long 计数；杂物按 (item_id, 锁定售价) 堆叠，掉落按每只怪物的首次生成快照结算一次。BOSS 占数量上限中的一个名额。未来科技树通过 SetSpawnModifiers 命令输入独立增量，不追溯修改已经生成的怪物。没有存档或离线结算。
