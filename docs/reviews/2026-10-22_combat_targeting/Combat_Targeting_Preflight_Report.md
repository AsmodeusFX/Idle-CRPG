# Combat Targeting V1.0 前置审计

日期：2026-10-22。审计完成后用户已批准D1～D5全部建议；本报告保留批准前审计内容，实施及后续两条用户规则覆盖见[执行记录](Execution_Record.md)。

已阅读本轮设计、审计门禁、集成说明、验收矩阵和3份CSV样例，并对照真实工程。当前阶段只新增来源归档、哈希清单和本报告，没有修改业务代码、正式配置、玩家存档或已确认设计。

## 1. 审计基线与来源

- 分支：`main`；提交：`de0284e`（`#战斗规则文档更新`）。审计开始时工作树干净，无需隔离/覆盖既有未提交修改；没有执行提交、推送或合并。
- 用户输入：`docs/temp/D2_Combat_Targeting_NoCollision_V1_0/`。
- 原始9文件逐字节复制至[来源目录](source/README.md)，保留原目录不动；[SHA-256清单](source_manifest.csv)可用于核对。归档不等于已实施或通过玩法验收。
- 包内提及的CLAUDE.md、docs/00_meta/DOCUMENT_INDEX.md、docs/combat/BATTLE_AI.md、BATTLE_RUNTIME.md、SKILL_EFFECT_BUFF.md、FIGHT_ATTRIBUTES.md、ATTRIBUTE_MODIFIERS.md及docs/engineering/PROJECT_ARCHITECTURE.md在本工程不存在。这是来源工程路径差异，不能当作本工程已有合同。
- 本工程对应权威依据：根AGENTS.md、docs/README.md、docs/design/dungeon_combat.md、skills_effects_buffs.md、combat_formula.md、attributes_levels.md、talent_trees.md、ui_layout.md、towns_portals.md及docs/technical/architecture.md、persistence_settings.md。
- 新包[02门禁](source/docs/02_Preflight_Conflict_Gate.md)要求“发现任何实质冲突，停在决策点提问”。用户也明确要求列出冲突供决策；因此本报告先给出可批准的具体修改范围，不把输入文档的历史断言直接套用为新事实。

## 2. 当前真实调用链

```text
GameSession.Travel（出口全员集合，再向新房入口移动）
  → Reveal（首人触达新房门：首次内容快照）
  → CurrentRoom更新 + CaptureRoomEntry
  → Fight
      AdvanceSkills → DrainEffects
      → 按Heroes列表行动，再按Monsters列表行动
      → Act：主动技能Choose；否则每步选择最近活敌
      → Move：二维连续移动，房间边界夹取
      → 基础攻击Effect / SkillAction Windup → Commit → Recovery
      → SettleDamage → DamagePipeline → 死亡与即时经验 / 地面掉落
      → Separate：同阵营单位两两推开，真实改变Actor.Position
      → 清房ResetSkillRoom
```

| 职责 | 证据 | 当前情况 |
|---|---|---|
| 小队实体与坐标 | Scripts/Core/GameSession.cs:9 | Actor，二维Vector2，正式1～5人；不是单角色或一维游戏 |
| 连续穿门及揭示 | GameSession.cs:261 | Travel先在旧房门等待全员，再首人触新房门；未单独记录每人的新房激活资格 |
| 首次刷怪快照 | GameSession.cs:283 | Reveal沿用房间随机源和独立刷怪配置；邻房不预揭示 |
| 战斗阶段顺序 | GameSession.cs:314 | Fight固定顺序；没有独立MonsterPolicy或现成仇恨表 |
| 普攻索敌与移动 | GameSession.cs:336 | Act每步最近活敌，相同距离稳定列表顺序；不存在持久普通目标锁 |
| 目标及技能ID | SkillRuntime.cs:30 | CombatId为本房英雄索引与怪物索引偏移；死亡不删除本房怪物列表，房间内可复用；不能跨房间或直接当永久召唤ID |
| 技能选敌及动作 | SkillRuntime.cs:113、127、133 | RunActiveSkill/Choose/Commit；技能临时目标有自己的前后摇与冷却，不能冒充普通锁敌 |
| 逻辑推开 | GameSession.cs:325 | Separate每步同阵营O(n²)距离检查和推位，会改变射程、AOE、弹道命中和回放 |
| 单位物理组件 | Scripts/UI、Scripts/Core检索 | 未发现战斗RigidBody/NavMesh单位阻挡；已有的是Core软推开，并非Godot刚体 |
| 投射物命中 | AbilityRuntime.cs:109、147、170 | 二维SegmentDistance线段扫掠及各Ability机制；必须保留，不能按来源一维示意改写 |
| 伤害奖励 | SkillRuntime.cs:76 | SettleDamage唯一扣血、死亡与经验/掉落入口，目标分配不得另写奖励 |
| 控制 | SkillRuntime.cs:34、137、172；AbilityRuntime.cs:193 | stun/chill/击退/聚怪已有；没有嘲讽/强制目标机制 |
| 召唤 | docs/design/skills_effects_buffs.md:45 | SpawnEntity仅有限生命周期效果，不是可攻击、可寻路的召唤Actor；没有完整召唤AI和独立召唤实体池 |
| 战斗属性 | CombatConfiguration.cs:16；FightAttributes.cs:16、49 | 当前26项注册属性，校验必须项，但并非来源假设的固定21项枚举；增加属性仍需同步注册、规则和消费入口 |
| 近战/远程 | config/combat/unit_attributes.csv | 可复用Ranged元数据；当前正式rat/guard均为近战，没有正式远程怪或运行时普攻模式切换配置 |
| 存档与重开 | SessionSnapshot.cs:98、124；SkillBattleState.cs:7 | 完整快照供确定性测试；正式进度清空SkillState、回到进房检查点，不保存未结束战斗的怪物现场 |
| 换人 | FormationPlans.cs:66；persistence_settings.md:92 | 清房/救援/拾取/事件后回城换人，放弃未完成旧地牢，补给后重新选目标 |

以上代码路径均相对`Idle-CRPG/`，文档路径相对仓库根目录。

## 3. C01～C15逐项对照

A=已兼容；B=需要扩展；C=与当前行为或已确认合同冲突；D=需决定范围/数据合同。

| 规则 | 分类 | 真实现状、最小修改及影响 |
|---|---|---|
| C01 无伤害/治疗仇恨账本 | A | 当前没有仇恨累计表；不新增ThreatTracker |
| C02 容量不是受击上限 | B | 新增普通近战优先预约语义；伤害合法性不按容量裁剪 |
| C03 单位容量、后排0 | B/D | 现有无容量字段；需决定统一战斗属性或独立AI属性，以及原型数值 |
| C04 近战优先本地剩余容量 | C/D | 替换怪物Act每步最近目标；需确认局部承接范围，不能整房间跨距吸怪 |
| C05 无其他目标可溢出 | B | 添加溢出原因，继续移动/攻击；不能用等待名额暂停自动循环 |
| C06 溢出事件重评、正常稳定 | C/B | 当前没有普通持锁或重评队列；在同一GameSession路径增加锁定/有界事件重评 |
| C07 普通远程最近、不占容量 | A/B | 最近原则兼容，补稳定锁和不预约规则；当前无正式远程怪，用真实Actor夹具验收，不虚构内容已投放 |
| C08 当前攻击方式决定预约 | B/D | Ranged可复用，但混合怪切换普攻方式尚不存在；一期验证模式切换契约，不自动新增怪物技能或变身规则 |
| C09 取得锁定立即确定性预约 | B | 保留怪物稳定列表/本房CombatId次序，选中即登记，不能先并行选择再统一记账 |
| C10 首人入场即战、其余陆续入 | C/D | 目前旧房出口全员集合，首人进新房后全队均在Enemies候选中；需扩展真实穿门/本房资格，不能只加视觉延迟 |
| C11 无单位逻辑推挤、视觉散开 | C | Separate改变权威坐标；替换为表现偏移，不移除房间边界、技能击退或投射物碰撞 |
| C12 召唤容量、Boss普攻一致 | B/D | Boss普通AI可直接统一；完整召唤不存在，不能将SpawnEntity改名当作实现 |
| C13 嘲讽/有限距离救场 | B/D | 没有现成Forced技能；决定本期只建兼容边界还是新增完整技能与平衡 |
| C14 不均摊、后排0仍可围攻 | B | 顺序选正常槽→其他合法单位→最近合法兜底；不按承伤人数或容量比值算压力 |
| C15 锁定稳定、不打断动作 | A/B | 主动动作已有安全边界；普通攻击无独立Windup且立即排Effect，按现有执行顺序保留；新增重评不得改写已排效果目标 |

## 4. 已有用户决定，直接作为兼容边界

这些不是需要用户重复批准的新问题：

1. **队伍上限保持5人、初始1人，星图扩编。** 来源“首版4人、长期5人”为过时背景。
2. **换人回城仍放弃旧地牢，治疗交易后重新选目标。** 来源两处“返回原房继续”的断言与用户已撤回的设计冲突，不恢复该玩法；TG-23按真实房间重开/换人放弃语义验收。
3. **保留二维坐标、线段扫掠、现有伤害/技能/控制/冷却/奖励实现。** 来源一维X轴示意不是迁移要求；不退化二维战斗。
4. 保留怪物出生快照、首次触门探索、清房救援拾取、星图手动购买、独立角色经验和正式进房检查点重开的规则。
5. 未发现需要保留的旧仇恨/预约系统，不新增一套与Act并行竞争的权威AI。

## 5. 需要用户决定的具体事项

| 决策 | 推荐方案 | 其他方案与差异 |
|---|---|---|
| D1 新规则替换范围与入门顺序 | 替换怪物普通索敌及Separate软推开；英雄自身选敌保持现有策略；按玩家编队顺序连续进门，首人进入即激活，只将实际入房者列为合法目标，不自动按职业重排 | 保留现有穿门集合：可做容量，但无法真实验收“坦克先入、后排迟到”；保留软推开则不满足C11 |
| D2 容量属性归属及初版值 | 将engagement_capacity正式注册进现有FightAttr，单位基础值从unit_attributes读取，复用天赋/Buff贡献，不新增独立属性账本；野蛮人3、圣骑士5、其余英雄0为可调原型，非最终平衡 | 独立AI属性不改FightAttr，但未来装备/天赋需维护另一条修饰入口；也可由用户另给初值 |
| D3 本期是否扩展完整召唤/嘲讽 | 一期实现普通锁定、容量/溢出、安全重评、入门及视觉；Boss普通攻击和远近程契约纳入。完整召唤与嘲讽/救场暂不投放，不用空壳接口或假召唤冒充已实现 | 本期完整投放需增加召唤池、独立ID、生命周期、技能效果和配置；嘲讽目标数/范围/持续/CD、Boss免疫、召唤上限及经验还需策划数值 |
| D4 无既有权威值的AI参数 | 暂用本地正常承接距离4.0世界单位、溢出重评最短0.25秒、每逻辑步最多处理4个重评请求；距离并列复用CombatId/列表稳定顺序；参数配置化并标原型，溢出合法兜底仍可追本房目标 | 用户提供范围/间隔/预算；不采用无限距离前排吸怪，也不擅自引入远程风筝等行为 |
| D5 控制和存档边界 | 眩晕/减速/技能击退不自动释放普通预约，沿用当前控制动作合同；死亡、离房和普通攻击方式改变释放。正式档不保存锁/预约/视觉位置，读档重开房间重建；测试完整快照可扩展临时状态并做旧缺字段兼容 | 控制即释放需另外定义控制结束的抢占与抖动规则；保存实战锁敌及怪物现场会改变既有检查点合同，本轮不建议 |

D2备注：容量只作为优先名额，0容量目标始终可被近战攻击；不得改造成硬受击人数上限。后续装备尚未投放，本次不顺带实现装备词缀系统。

D1备注：不把穿门变成房间跳转或全队瞬移。进门队尾要继续真实移动，资格判断同时供普通攻击与技能候选使用，不能出现门外队员被新房怪攻击、又无法合法还手的双重标准。

D5备注：入场顺序变化可能影响进房检查点。实施时优先复用已存每人位置和房间几何推导资格；若必须追加正式存档字段，先补迁移方案，不能为了少字段丢失后排进房状态。

## 6. 用户批准后拟实施的最小修改

- `Scripts/Core/GameSession.cs`：改造Travel/Fight/Act，区分英雄与怪物普通策略；移除被正式取代的Separate调用与实现；保留Move、Reveal、伤害和奖励入口。不保留默认开启的旧AI回退分支。
- Core房间级目标运行态：仅一份主锁定/Normal预约/Overflow状态，优先并入现有SkillBattleState的本房生命周期；确有必要拆文件只拆职责，不建立第二权威系统。模式变化、目标失效、清房/换层/全灭/换人统一清理。
- `CombatConfiguration.cs`、`FightAttributes.cs`及`config/combat/{fightattr,fightattr_rules,unit_attributes}.csv`：按D2注册唯一容量定义及整数边界；加载校验缺字段/负容量/无效引用，字段字典同步。输入包样例不直接覆盖正式表。
- `config/system/config.csv`及其规则加载：按D4加入AI原型参数，明确单位和范围，不硬编码正式平衡值。
- `SkillRuntime.cs`：复用技能独立目标与动作安全边界；普通锁变化不覆盖已Commit/排队的效果目标；只有实际进入本房者是合法攻击候选。
- `SessionSnapshot.cs`、`SkillSnapshot.cs`：如扩完整测试快照，则校验锁ID、房间归属、唯一预约和预算；CaptureProgress仍清空短期战斗态。旧正式档复用既有房间检查点，不重复成长/拾取。
- `Scripts/UI/MotionPresentation.cs`、战斗绘制：用稳定本房实例顺序确定表现偏移并平滑；伤害、弹道、AoE与逻辑Position不变。调试视图如确有需要，只从GM菜单打开。
- 同步`docs/design/{dungeon_combat,skills_effects_buffs,attributes_levels,camera_world_view}.md`和`docs/technical/{combat_config,architecture,persistence_settings}.md`；来源原文不直接覆盖现有正式文档。
- 不修改世界门禁、要塞批准、地图尺寸、城镇服务、经验、掉落或装备系统。

## 7. 测试计划与当前状态

本轮没有改实现，未运行编译、Core回归或Godot验收；不存在“已通过新AI验收”的结论。

推荐批准一期后：

- TG-01～10、15～19、21、22、24：普通目标、单人0容量、多坦、稳定预占、动态容量/攻击方式变化、死亡释放、安全边界、非法配置，使用真实Core对象及确定性夹具。
- TG-11/20：验证既有技能自己的targeting不被普通主锁覆盖；未投放的狙击怪/Boss专属内容不伪报为上线。
- TG-12～14：如D3采用分期则标`DEFERRED`，说明缺少嘲讽/救场和完整召唤；不把空壳测试记PASS。
- TG-23：按已确认“换人放弃旧地牢”重写验收预期；覆盖换房/全灭/回城/换人清理、正式检查点重开、内部快照复现。
- RG-01～08：固定Tick、二维高速扫掠、DMG1/2/3、技能Windup/Commit/CD、收益一次到账、英雄策略、旧档、长时间有界队列全回归。
- 真实命令以本仓库为准：`dotnet run --project tests/IdleCrpg.Checks.csproj`，不以一个没有测试的`dotnet test`成功退出冒充核心检查。
- `dotnet build Idle-CRPG/Idle-CRPG.csproj`、Godot `--smoke-test`，再运行坦克先进、后排迟到、多人围攻、远程混编四种QA场景，检查自然散开和目标稳定。
- 本工程尚无正式远程怪、召唤、嘲讽配置。可用QA夹具验证远近程契约，需区分真实运行观察与正式内容投放。

## 8. 交付边界

已完成阅读、真实代码审计和原始来源归档。D1～D5随后全部获用户批准，已按一期范围实现及验证；另按用户补充加入有怪房警觉展开和清理路径不等人的规则，覆盖原文C10。完整执行/未执行清单以[执行记录](Execution_Record.md)为准；本报告前文的只读状态描述是审计时点记录。
