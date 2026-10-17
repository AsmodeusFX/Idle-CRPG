# Idle-CRPG

Godot 4.7 .NET 放置小队 RPG 核心原型。

使用 Godot .NET 版打开 `Idle-CRPG/project.godot`，运行后选择三个职业，输入 seed 并开始。小队从初始村庄自动出发，在连续的 16×16 世界中自动赶路、避障、挑战地牢、回村售卖并补给。镜头默认跟随角色，战斗时锁定完整房间；进度地图仅手动打开。清空整个城堡统治区后统一变橙，点击右侧按钮安排城堡挑战并占领；等待期间继续自动冒险或复刷。支持暂停和 1/2/4 倍速。

```powershell
dotnet build Idle-CRPG/Idle-CRPG.csproj
dotnet run --project tests/IdleCrpg.Checks.csproj
```

进度自动保存，关闭后重开自动继续。右上角设置可调整窗口尺寸、全屏、音乐与音效；“重置进度”需要二次确认，保留显示和音量设置。音乐和基础音效授权复用自 Idle-Sword。

需要 .NET 8 或更高运行环境。CSV 按 world、dungeon、hero、monster、village、item、audio、system 功能分目录。策划规则、首版默认值、配置字段及架构见 [docs/README.md](docs/README.md)。当前使用程序占位美术；尚无科技树界面、离线收益和职业主动技能。
