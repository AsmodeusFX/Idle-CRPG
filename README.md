# Idle-CRPG

Godot 4.7 .NET 放置小队 RPG 核心原型。

使用 Godot .NET 版打开 `Idle-CRPG/project.godot`，运行后选择三个职业，输入 seed 并开始。左侧为等距探索地图，右侧显示阵容、成长与日志；支持暂停和 1/2/4 倍速。

```powershell
dotnet build Idle-CRPG/Idle-CRPG.csproj
dotnet run --project tests/IdleCrpg.Checks.csproj
```

需要 .NET 8 或更高运行环境。策划规则、首版默认值、配置字段及架构见 [docs/README.md](docs/README.md)。当前使用程序占位美术；尚无存档、离线收益和职业主动技能。
