# 工程架构

`Idle-CRPG/Scripts/Core/` 是不依赖 Godot 的 C# 模型。Config / WorldConfig 负责 CSV 与跨表校验，Dungeon 负责拓扑，MonsterSpawning 负责刷怪快照与奖励成长，WorldCampaign / ExpeditionPlanner 负责自动远征、相邻路径、统治进度与交易。GameSession 是 UI 唯一状态命令入口，持有固定队员、金币、背包、经验与当前地牢。坐标使用 System.Numerics.Vector2。

`Scripts/UI/Main.cs` / `WorldView.cs` / `FollowScene.cs` 处理 Godot 控件、地图选择和场景绘制，每固定 0.05 秒推进逻辑。SceneCanvas 位于裁切容器中，镜头独立于角色 AI：赶路跟随排头，战斗锁定房间中心和完整尺寸。默认显示连续室外或地牢，只手动打开进度网格 UI。WorldNavigation 使用统一坐标和共享障碍定义生成细网格 A* 路径；村庄交易位置在同一世界坐标平面内推进，不另换一张地图。

`HudLayout.cs` 管理游戏五区构图、货币与角色状态占位、独立城堡交互卡及首次刷新次序；挑战仍只调用 GameSession 命令。SceneCanvas 按游戏战斗视口适配镜头。`WorldPage.cs` 管理全局城堡列表和地图，列表与地图联动选择；地图绘制及命中共用原点与格宽。城堡、玩家星图及星图编辑器使用顶部栏以下的整个内容区，通过页面切换统一控制游戏 HUD 与倍速的可见性。详见 design/ui_layout.md。

用 Godot 4.7 .NET 打开 `Idle-CRPG/project.godot` 并运行。命令行 `dotnet build Idle-CRPG/Idle-CRPG.csproj`；核心检查 `dotnet run --project tests/IdleCrpg.Checks.csproj`。Godot 可通过 `--headless --path Idle-CRPG -- --smoke-test` 自动推进并退出。

配置错误直接报错，不静默使用默认数值。模型不读 UI，不在绘制期间发奖励，不把房间渲染状态作为战斗状态。

GmPanel 参考 Idle-Sword 的双区菜单布局，上区当局指令、下区编辑器入口。GmCommands 作为 GameSession 的命令扩展，管理临时无敌/怪物倍率/房间数量参数及资源发放；正常战斗与 GM 共用唯一伤害及经验实现。GmConfig 从 gm/gm.csv 校验发放量、倍率档位和数量步长。临时参数不进入快照，到账资源和已生成房间仍走正常保存，详见 design/gm.md。

AreaParts 管理城堡统治区与多选 OR 前置配置、相邻引用和可达性校验。WorldProgress 按城堡通关历史推导已开放统治区，ExpeditionPlanner 只在开放范围选择开拓/复刷目标，WorldNavigation 的 allowedRegions 在路径层禁止越界，CastleUnlocked 同时检查统治区前置和辖区普通地牢完成度。UI 仅展示解锁与多选前置说明；旧存档门禁迁移仍经 GameSession 恢复处理。

StarConfiguration/StarProgression 管理内容布局合同与已购等级，GameSession.BuyStar 是玩家购买命令，先校验再扣费并调整全队属性。贡献差量独立记账，恢复/热更新不会重复叠加，HP 上限变化保留当前 HP。StarEditorDocument/StarEditorStore 为纯逻辑编辑和文件事务层，StarCsv 支持标准 CSV 转义；StarCanvas 与 StarUi 迁移参考工程的画布和交互，GM 菜单统一承载开发工具。编辑器只写配置副本/源表，不直接修改玩家等级；会话刷新仍走 GameSession 命令。详见 design/star_map.md。

GameSession 在清房后优先执行救援状态机：前往尸体 → 范围内读条 → 恢复生命。全灭时丢弃当前地牢结构与内容，清零临时行动状态，保留原队员实例及所有会话累计资源，通过 WorldCampaign.RespawnInVillage 直接进入当前区域村庄；复活与正常返村复用 EnterVillage，必须完成交易后才再次选目标。失败地牢不记通关。世界远征通过 CreateWorld 进入；单地牢独立模拟若全灭则接入出生区域的村庄流程。Tick 根据世界阶段推进赶路、地牢或村庄。地牢通关不会终止世界 Tick，而是标记通关并安排返村。

金币以 long 计数；杂物按 (item_id, 锁定售价) 堆叠，掉落按每只怪物的首次生成快照结算一次。BOSS 占数量上限中的一个名额。未来科技树通过 SetSpawnModifiers 命令输入独立增量，不追溯修改已经生成的怪物。无离线结算。

SessionSnapshot 的 Capture 保留内部确定性测试现场，CaptureProgress 则复制正式精简检查点：放弃未清房战斗现场，保留已到账成长和待拾取奖励，读取时重开当前房间。击杀经验即时到账、走近拾取后实物入库。SaveStore 负责原子替换、备份和重置；ClientSettings 用单后台任务编码与写盘，仅在关键持久状态变化时安排，退出/重置等待任务完成。MotionPresentation 将 20 Hz 逻辑坐标逐帧插值，镜头临界阻尼过渡，HUD 每 0.1 秒刷新；表现不改变命中和交互判定。村庄缓存每个角色的路径，避免每逻辑步重新寻路导致回头。SettingsStore 和 Audio 只管理偏好与表现。细节见 persistence_settings.md。
