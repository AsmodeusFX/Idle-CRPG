# 战斗、技能、属性与等级迁移验收归档

日期：2026-10-18；目标工程 `E:/Git/Idle-CRPG`，只读参考 `E:/Git/Idle-Diablo`。本记录归档本轮结果，不代表最终数值平衡。

## 用户裁定与实现差异

| 议题 | 本项目结论 |
|---|---|
| 三阶段 | 保留DMG1/2/3；DMG1改为max(1,攻击−护甲)，全部类型统一使用 |
| 事件 | 闪避→格挡→暴击；后两项可同时成立；基础暴击1.5，参数放CSV |
| 五池 | 技能加算、通用加算、独立暴击加算、独立倍率叠乘、同Debuff加算/不同Debuff叠乘 |
| 技能加值 | 300%+30%=330%，不是390% |
| 精度 | 延续本项目浮点伤害，不复制源Demo取整 |
| 投放 | 迁入15项主动技能及配置，默认职业技能留空，等待后续分配 |
| 空间时间 | 源一维/8Hz改二维/20Hz；作用域限定当前已激活房间，像素按100:1转换 |
| 暴击时点 | 为符合DMG2，在一发首次未闪避命中时确认暴击，后续同发/派生DoT沿用 |
| 英雄成长 | 保留每级ATK+4、HP+18，改累计等级表差值；经验规则不变 |
| 怪物成长 | 保留每级线性HP+18%、ATK+12%；护甲不再随等级变化；经验/掉落独立 |
| 上限 | 英雄和怪物分别以等级表尾为最高等级；原型覆盖1～10000 |
| Git | 按用户要求仅改工作区和汇报，不自动commit/push |

## 来源证据及文件定位

[来源清单](combat_skill_sources.csv)记录18组源/目标文件路径、SHA-256和迁移方式。源路径以Idle-Diablo为根，目标以Idle-CRPG仓库为根。哈希是本次验收时的本地文件证据，不是上游版本号或远端发布声明。源文件未修改。

实现迁移是适配现有架构，不是整套源引擎替换：DamageCalculator→DamagePipeline；SkillRuntime/Effect队列/BuffRuntime→SkillRuntime及SkillBattleState；AbilityRuntime→二维AbilityRuntime；SkillTables→SkillConfiguration。七张技能表保留稳定正式ID、参数和机制，基础测试ID重命名，Tick量化适配；属性/等级表参照源结构，采用本项目属性范围及用户确认的成长值。

新增配置均放工程config功能目录、配keep导入元数据。公开Core逻辑不引用Godot。普攻也进入Effect队列，统一DamagePipeline和SettleDamage，经验与掉落没有新增第二实现。

## 验证记录

实际执行 `dotnet run --project tests/IdleCrpg.Checks.csproj --no-restore`，全部通过：

- 原有地牢、房间/走廊、救援、全灭返村、世界门禁、城堡排队、拾取/交易、星图与GM回归。
- 五池手算3476.088、全部伤害类型扣护甲、概率调用顺序、闪避短路、格挡暴击共存、资格过滤、保底边界与非法来源。
- 15项技能逐项Commit并验证实际伤害或Buff；延迟命中、二维作用域、护盾吸收和破裂、分身不递归、前摇目标死亡取消、Buff到期、灼烧完整续接。
- 内部完整快照确定性续接、正式检查点清空临时现场并保留技能和成长、奖励/拾取防重复及备份恢复。
- 26属性注册和操作/边界；英雄累计差值、存活/阵亡升级处理、表尾上限、怪物仅攻击/HP成长；缺等级行、未知Buff属性、非法基线拒绝。

实际执行 `dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore`：0警告、0错误。随后Godot 4.7 .NET headless `--smoke-test` 退出码0：CLIENT、SCENE、HUD、GM、STAR_UI及SMOKE全部通过。检查均使用隔离QA存档。初次受限沙箱写盘导致CLIENT断言失败；授权QA写入后通过，无需修改保存断言或降低检查要求。

## 审阅入口与限制

先看 [公式及算例](../../design/combat_formula.md)，再看 [技能生命周期](../../design/skills_effects_buffs.md)、[属性与等级](../../design/attributes_levels.md)、[字段字典](../../technical/combat_config.md)。测试代码是可执行验收依据：`tests/CombatSkillChecks.cs`、`tests/AttributeLevelChecks.cs`及原有回归。

本轮没有职业技能分配/升级购买UI、装备系统、源音效与技能专用视觉资源、完整召唤AI或任意未来自定义技能组合穷举。保留来源音效ID不等于已播放对应资源。未支持机制明确拒绝；扩展时须同步实现、配置校验、文档和测试。

正式存档遵循未清房重开，不保存战斗现场；外部改表可能改变重建怪物。旧档保留聚合属性，不按新表回算历史。缩短等级表需检查存档兼容。原型默认数值不得标成最终策划。
