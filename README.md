# Idle-CRPG

Godot 4.7 .NET 放置小队 RPG 核心原型。

使用 Godot .NET 版打开 `Idle-CRPG/project.godot`，运行后选择三个职业，输入 seed 并开始。小队从初始村庄自动出发，探索 16×16 世界、挑战地牢、回村售卖杂物并补给，清空城堡统治区后攻城占领；无新目标时持续复刷。可切换世界地图和地牢/村庄/旅途场景，支持暂停和 1/2/4 倍速。

```powershell
dotnet build Idle-CRPG/Idle-CRPG.csproj
dotnet run --project tests/IdleCrpg.Checks.csproj
```

需要 .NET 8 或更高运行环境。CSV 按 world、dungeon、hero、monster、village、item 功能分目录。策划规则、首版默认值、配置字段及架构见 [docs/README.md](docs/README.md)。当前使用程序占位美术；尚无科技树界面、存档、离线收益和职业主动技能。
