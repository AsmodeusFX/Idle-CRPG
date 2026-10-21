# D2 战斗索敌 V1.0 执行记录

日期：2026-10-22。状态：**本次批准的一期范围已实现；原始设计包整体为PARTIAL，含明确批准的后续项与两条新规则覆盖。** 本文可直接提供给设计AI作为工程反馈，不能将“批准一期完成”理解为所有原始测试项或内容均已上线。

## 1. 来源、批准和优先级

- 输入包：D2_Combat_Targeting_NoCollision_V1_0，原始9文件完整保留在[source](source/README.md)，逐文件SHA-256见[source_manifest.csv](source_manifest.csv)。原docs/temp来源未删除、未改写。
- 审计基线：main / de0284e。实际工程为Godot 4.7 .NET、二维房间战斗、1～5人小队，非来源历史假设的一维单角色工程。
- [前置审计](Combat_Targeting_Preflight_Report.md)列出D1～D5。用户回复“全部同意，执行完后，需要输出一份执行记录归档，明确哪些执行了，哪些未执行”，批准最小替换、统一容量属性、原型参数、分期范围及检查点边界。
- 后续补充一：“开门等待队友”只应在挑战有怪房间触发；通过已解锁走廊/房间不等待。实施为：已清理路径不进行全队门口集合，不启动警觉窗口。
- 后续补充二：进房后敌人AI延迟启动，小队自然散开，配合呈现优化。**这一条覆盖来源C10的“立即开战/不短暂展开”。** 当前采用可配置0.8秒警觉窗口；没有擅自按旧文档恢复秒锁敌。
- 保留此前已确认的五人上限、初始单人/星图扩编、换人回城放弃旧地牢、独立角色经验、二维命中和房间检查点重开。来源的“四人首版”“换人后传回原房间”未采纳。

正式生效规则：[combat_targeting.md](../../design/combat_targeting.md)。配置与恢复合同：[targeting_config.md](../../technical/targeting_config.md)。

## 2. 已执行

| 事项 | 实际实现 |
|---|---|
| 唯一怪物普通主锁 | 改造原GameSession.Act，主锁唯一存于SkillBattleState.Targeting；没有并行旧AI或仇恨账本 |
| 近战容量优先 | 4单位局部范围内按最近合法对象/稳定ID预占；没有名额仍可Overflow攻击，不设置等待槽位状态 |
| 0容量与多前排 | 后排可同时被多怪锁定；多坦各自独立容量，不做强制均摊或伤害/治疗仇恨 |
| 稳定与有界重评 | Normal保持；候选变化、容量变化、死亡/名额释放触发Overflow重评，单锁最短250ms，每步最多4次，轮转处理 |
| 生命周期 | 死亡、离房、清房、换层、全灭及换人清理；普通近远程方式改变释放/重建对应锁；禁止跨房间复用索敌现场 |
| 动作安全 | 只在无当前Active且Recovery结束的决策点消费重评；已有技能目标及已提交Effect不被普通换目标改写 |
| 远程普通攻击 | 最近合法目标、不占近战容量；近远程方式切换有可运行Core夹具；不把技能的一次性特殊选敌当普通主锁 |
| 容量统一属性 | engagement_capacity注册于现有FightAttr；unit_attributes必填整数基础值，统一聚合被动/Buff；未建立独立AI属性修饰体系 |
| 原型配置 | 野蛮人3、圣骑士5、其余英雄0；4单位、250ms、每步4次及入场/队列参数均CSV配置，非正式平衡结论 |
| 连续入场 | 编队首位先穿门，其他成员继续真实移动；只允许实际进入当前房间者成为攻击候选，也限制门外成员攻击新房 |
| 警觉与自然展开 | 有活怪房间警觉800ms；敌人不索敌/移动/发起攻击，英雄向门内各自通道移动；前排纵深2.8、后排1.8、横向间距0.85，五人通道不复用 |
| 清理路径连续通行 | 已清理房间/走廊不启动警觉、不等队尾；保留救援、拾取、事件和换层楼梯集合职责 |
| 无单位逻辑推挤 | 删除Separate实现及调用；房间墙体、技能击退/聚怪、二维线段扫掠与AoE仍保留 |
| 围攻表现 | 本房稳定ID/目标决定只读屏幕偏移，接敌时展开并平滑过渡；不写回Actor.Position，不参与命中；警觉期间不画伪普攻连线 |
| 进度兼容 | 正式档不保留锁、预约或怪物现场；重开检查点时重建警觉，保留成长/资源/换入等待；完整测试快照保存目标/入场时序、攻击方式并校验引用及房间归属 |
| 玩家说明 | 小队角色悬停显示近战优先接敌容量，并明确它不是受击人数上限 |

## 3. 未执行 / 非本期投放

以下项目不应反馈为“已经实现”：

| 项目 | 状态与原因 / 后续需要 |
|---|---|
| 完整可攻击召唤物 | **DEFERRED，用户已批准分期。** 现有SpawnEntity仍只是有限生命周期效果；未新增召唤Actor池、独立实例ID、生成/回收/死亡AI、骷髅或石魔内容。须另定上限、技能、成长、经验和生命周期 |
| 嘲讽Forced | **DEFERRED。** 本期锁类型只有Normal/Overflow/Ranged；未添加假Forced状态、TauntManager或空壳技能。需后续确定技能范围、数量、持续、CD、Boss适用性并接入Skill/Effect |
| 主动拦截救场 | **DEFERRED。** 未新增盾击/救场位移或自动路过拉怪；已有普通技能不被改成无限距离守护 |
| 正式远程/混合怪物内容 | **NOT DEPLOYED。** 当前正式rat/guard仍为原配置；近远程契约和运行方式切换已测，但没有投放新弓箭手、狙击怪或自动切换阈值 |
| Boss专属点名/AoE交替内容 | **NOT DEPLOYED。** Boss普攻复用同一主锁；现有技能策略保留，未新增特殊Boss内容或权重 |
| 装备词缀、容量天赋内容节点 | **NOT DEPLOYED。** 属性/Buff/被动聚合已支持并用测试节点验证3→5，但未顺带开发装备系统或投放新容量天赋节点 |
| GM目标线/N-O-F诊断面板、换目标原因日志 | **NOT IMPLEMENTED。** 当前只提供有界运行态只读诊断及QA控制台快照；没有正式逐次换目标日志或GM面板 |
| 完整动画/复杂内外圈/彻底无重叠模型 | **NOT IMPLEMENTED。** 做稳定平滑屏幕偏移与真实入场展开；允许局部模型重叠，密集敌群姓名仍可能重叠；没有重做美术、动画或景深系统 |
| 数小时/数日压力挂机、正式平衡验收 | **NOT RUN。** 已做确定性循环和有界状态检查、真实窗口抽样，不能替代长期Soak和策划调参 |
| 原房间换人返场、一维化、四人上限 | **NOT ADOPTED。** 与现有用户明确规则冲突，不是遗漏实现 |

## 4. C01～C15反馈矩阵

| 规则 | 状态 | 说明 |
|---|---|---|
| C01 | IMPLEMENTED | 无仇恨积分 |
| C02 | IMPLEMENTED | 容量仅优先预约，不是硬受击上限 |
| C03 | IMPLEMENTED | 统一容量属性及后排0 |
| C04 | IMPLEMENTED | 局部剩余容量优先，远处坦克不跨房吸怪 |
| C05 | IMPLEMENTED | 无其他对象仍可溢出攻击 |
| C06 | IMPLEMENTED | 正常稳定、溢出事件/预算重评 |
| C07 | IMPLEMENTED / CONTENT NOT DEPLOYED | 普通远程契约已测，未加新远程怪 |
| C08 | IMPLEMENTED CONTRACT | 普攻Ranged方式切换及释放已测，未加正式混合怪切换AI |
| C09 | IMPLEMENTED | 锁定时按稳定顺序预占 |
| C10 | USER OVERRIDE | 连续入场保留；“立即开战”改为800ms警觉/展开；清理路径不等人 |
| C11 | IMPLEMENTED | 无逻辑推挤，稳定只读表现偏移 |
| C12 | PARTIAL | Boss普攻统一；完整召唤延后 |
| C13 | DEFERRED | 嘲讽/主动救场未实施 |
| C14 | IMPLEMENTED | 无压力均摊，0容量可多怪围攻 |
| C15 | IMPLEMENTED | 普通重评不打断技能动作 |

## 5. TG/RG/VIS验证边界

| 原始用例 | 本次结果 |
|---|---|
| TG-01～04 | PASS：单坦3N+5O、后排迟到、后排可多怪围攻/不均摊、纯0容量可战斗 |
| TG-05～08 | PASS：被动容量3→5、多坦3+5独立预约、单坦超额兜底、0容量不伪造单怪限制 |
| TG-09～10 | PASS（Core及QA Actor）：远程不占位、近远程切换/释放和完整快照攻击方式保留 |
| TG-11 | PARTIAL：原技能独立targeting回归通过；没有新增正式后排狙击怪 |
| TG-12～14 | DEFERRED：缺少本期明确不投放的嘲讽、救场和完整召唤，不伪造PASS |
| TG-15～19 | PASS：实际击杀释放/补位、容量骤降、前摇安全、同种子10怪确定性、单人战败清理 |
| TG-20 | PARTIAL：Boss普攻共用已测代码，已有AOE/技能独立策略回归；没有新增Boss交替技能内容 |
| TG-21 | PASS：0容量后排允许多怪围攻 |
| TG-22 | ADAPTED/PASS：按最新用户要求验证有怪房AI延迟、小队展开、延迟结束恢复索敌 |
| TG-23 | ADAPTED/PASS：换人放弃旧地牢、换房/全灭清理、检查点重开及完整快照续行；不验证已被撤回的返场续战 |
| TG-24 | PASS：非法目标引用、负容量、缺字段拒绝；没有投放可任填的策略字符串，锁枚举/Schema严格验证 |
| RG-01～08 | 原完整核心回归PASS：固定Tick/确定性、二维弹道、DMG三阶段、技能效果/CD、即时与地面奖励、英雄技能、旧档/备份、短期循环有界；不等于数日挂机已完成 |
| VIS-01 | 实机QA已运行：坦克先入时3N+5O都锁0，后排到达后原3N不变、5O转向后排；正常新房另观察警觉窗口 |
| VIS-02～03 | 实机QA已运行：8怪及20怪围攻可展开、穿行，无单位物理堵门；姓名密集重叠仍是表现限制 |
| VIS-04 | DEFERRED：召唤物未实施 |
| VIS-05 | PARTIAL：高速二维扫掠及视觉不写逻辑坐标Core检查通过；未另录高速弹道重叠专项视频 |
| VIS-06 | PARTIAL：确定性/锁稳定Core检查通过；没有新增换目标原因日志面板或专项抖动录像 |
| VIS-07 | Core容量3→5因果验证PASS；未录制独立3→5对比视频，不能称正式视觉平衡验收 |
| VIS-08 | PARTIAL：远程/近战QA混编已运行；特殊刺客/Boss新内容未投放 |

旧Program/SaveChecks/ProgressChecks/GmChecks中，针对“立即交战/全员已在房内”的测试夹具已显式完成警觉或将人物摆入当前房间，再验证原来的伤害、即时收益和救援断言；没有删除原断言或放宽收益/存档判断。新专项独立验证真实警觉、门外资格及清理路径连续通过。

## 6. 实际运行及观察

命令和结果：

- `dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore`：PASS，0警告/0错误。
- `dotnet run --project tests/IdleCrpg.Checks.csproj`：PASS，全套原回归及新增CombatTargetingChecks。
- `dotnet run --project tests/IdleCrpg.Checks.csproj -- --targeting-checks`：PASS，专项入口。
- Godot `--headless --path Idle-CRPG -- --smoke-test`：PASS，含技能、编队、设置/存档、镜头、章节、GM与星图UI。
- Godot真实窗口 `--path Idle-CRPG -- --capture --capture-targeting=<场景>`：intro、tank-first、rear-entry、crowd、ranged、intro5、crowd20均实际运行并输出CAPTURE_OK。截图在忽略的artifacts目录，未作为提交产物；未生成视频。
- Godot真实窗口 `--path Idle-CRPG -- --performance-test`：PASS。RTX4060 Laptop / OpenGL Compatibility本机采样：travel平均帧16.68ms、combat 16.67ms、map 16.67ms；combat process平均0.27ms、P95 0.48ms。这是有限帧抽样，不是所有硬件或长时间性能保证。

关键实机控制台事实：tank-first为3Normal+5Overflow全部目标0；rear-entry原3Normal保持目标0、5Overflow转向后排2；ranged为3Ranged+3Normal+2Overflow；crowd20为3Normal+17Overflow；intro/intro5警觉剩0.35秒时主锁列表为空。

## 7. 变更文件与保持事项

- Core：新增CombatTargeting；修改GameSession、CombatConfiguration、SkillBattleState、SkillRuntime、SkillSnapshot、SessionSnapshot、Talents。原Act复用/改造，Separate删除，未创建平行TargetSelector或独立仇恨事件总线。
- UI：MotionPresentation、Main、HudLayout，新增隔离QA TargetingSmoke。
- 配置：combat/fightattr、fightattr_rules、unit_attributes；system/config。未增加正式怪物、装备、召唤、嘲讽或Boss内容表。
- 检查：新增CombatTargetingChecks，原Program/GmChecks/ProgressChecks/SaveChecks夹具与入口适配；所有原回归仍运行。
- 文档：新增正式设计与字段合同，更新入口索引、架构、存档、地牢战斗、技能、属性和镜头说明；来源原文及哈希清单保留。
- 保持：伤害管线、二维弹道、世界地图门禁、要塞手动批准、城镇交易、独立经验、地面拾取和检查点重开语义。没有写玩家进度，没有执行Git commit/push/merge。

## 8. 建议给设计AI的下一轮输入

请基于本工程二维、五人、进房检查点重开以及800ms入场警觉的新事实继续设计；不要再以一维单角色、四人上限、立即激活或换人返场作为现状。后续优先补充完整召唤实体合同、有限距离Forced/救场技能数值、Boss适用范围、正式远程/混合怪物配置，以及容量成长内容；需要独立视觉与长期挂机验收时另列执行项，不把本次有限帧截图/短期循环替代为长期PASS。
