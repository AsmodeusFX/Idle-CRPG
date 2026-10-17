# 工程架构

`Idle-CRPG/Scripts/Core/` 是不依赖 Godot 的 C# 模型。Config 负责 CSV 读取和拒绝非法值，Dungeon 负责拓扑，GameSession 负责唯一的推进、移动、门触发、战斗与结算入口。世界坐标使用 System.Numerics.Vector2。

`Scripts/UI/Main.cs` 只处理 Godot 控件和等距绘制，每固定 0.05 秒推进逻辑，速度倍率改变推进次数。画面黑色区域为未探索区域；菱形角色和怪物为程序占位资源。

用 Godot 4.7 .NET 打开 `Idle-CRPG/project.godot` 并运行。命令行 `dotnet build Idle-CRPG/Idle-CRPG.csproj`；核心检查 `dotnet run --project tests/IdleCrpg.Checks.csproj`。Godot 可通过 `--headless --path Idle-CRPG -- --smoke-test` 自动推进并退出。

配置错误直接报错，不静默使用默认数值。模型不读 UI，不在绘制期间发奖励，不把房间渲染状态作为战斗状态。
