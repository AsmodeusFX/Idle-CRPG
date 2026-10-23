# 2026-10-22 战斗索敌 V1.0 执行归档

输入包为外部方案 D2_Combat_Targeting_NoCollision_V1_0（原始 9 文件按新归档规范不再随工程保存）。状态：**批准的一期范围已实现；原始设计包整体 PARTIAL，含明确延后项与两条用户覆盖。** 规则见 `docs/design/combat_targeting.md`，字段合同见 `docs/technical/targeting_config.md`。

## 一、批准基线

- 审计基线 main / de0284e；实际工程为 Godot 4.7 .NET、二维房间战斗、1～5 人小队，不是来源假设的一维单角色。
- 用户批准前置审计 D1～D5：替换怪物普通索敌与 Separate 软推开；容量注册进既有 `FightAttr`（野蛮人 3、圣骑士 5、其余 0 为原型）；一期不做完整召唤与嘲讽；AI 参数用 4 世界单位 / 250ms / 每步 4 次并配置化；正式档不保存锁与怪物现场。
- 两条用户覆盖：①“开门等待队友”只在有怪房间触发，已清理路径不等；②进房后敌人延迟启动、小队自然散开（覆盖来源 C10 的“立即开战”）。
- 未采纳来源的“四人首版”“换人后传回原房间”。

## 二、已实现

| 项 | 实现 |
|---|---|
| 唯一怪物主锁 | 改造既有 `GameSession.Act`；主锁唯一存于 `SkillBattleState.Targeting`，无并行 AI、无仇恨账本 |
| 容量预约 | 4 单位内按最近合法对象、稳定 ID 预占；无名额仍可 Overflow 攻击，不设等待槽位 |
| 0 容量/多前排 | 后排可被多怪同时锁定；多坦各自独立容量，不做均摊 |
| 有界重评 | Normal 保持；候选/容量/死亡/名额释放触发 Overflow 重评，单锁 ≥250ms、每步 ≤4 次、轮转公平 |
| 生命周期 | 死亡、离房、清房、换层、全灭、换人清理；攻击方式改变释放/重建；禁止跨房复用现场 |
| 动作安全 | 只在无 Active 且 Recovery 结束的决策点重评；已提交动作与 Effect 不被改写 |
| 远程普攻 | 最近合法目标、不占近战容量；近远切换有 Core 夹具 |
| 容量属性 | `engagement_capacity` 注册进 `FightAttr`，`unit_attributes` 必填基础值，复用被动/Buff 聚合 |
| 连续入场 | 编队顺序穿门，只允许实际入房者成为候选、也限制门外者攻击新房 |
| 警觉与展开 | 有活怪房 800ms 警觉；前排纵深 2.8 / 后排 1.8 / 间距 0.85；五人通道不复用 |
| 已清路径 | 不启动警觉、不等队尾；救援、拾取、事件、楼梯集合职责保留 |
| 无推挤 | 删除 Separate 及调用；墙体、技能位移与二维扫掠保留 |
| 表现 | 稳定 ID/目标决定只读屏幕偏移，接敌展开并平滑过渡；不写回 `Actor.Position`、不参与命中 |
| 存档 | 正式档不存锁/预约/怪物现场；完整测试快照保存目标与入场时序并校验引用与房间归属 |
| UI | 角色悬停显示近战优先接敌容量，并注明不是受击人数上限 |

## 三、延后 / 未投放（不得对外称已实现）

完整可攻击召唤物（DEFERRED）、嘲讽 Forced（DEFERRED）、主动拦截救场（DEFERRED）、正式远程/混合怪物内容（NOT DEPLOYED）、Boss 专属点名与技能交替（NOT DEPLOYED）、装备词缀与容量天赋内容（NOT DEPLOYED）、GM 换目标日志面板（NOT IMPLEMENTED）、完整动画与无重叠模型（NOT IMPLEMENTED）、长时挂机与正式平衡（NOT RUN）。“一维化/四人上限/原房换人返场”属未采纳，不是遗漏。

C01～C15 反馈矩阵：C01～C06/C09/C11/C14/C15 IMPLEMENTED；C07 契约已测但未投放远程怪；C08 契约已测未投放混合怪切换；C10 用户覆盖；C12 PARTIAL（Boss 普攻统一，完整召唤延后）；C13 DEFERRED。

## 四、验证

- `dotnet build Idle-CRPG/Idle-CRPG.csproj --no-restore`：0 警告 0 错误。
- `dotnet run --project tests/IdleCrpg.Checks.csproj`（含 `--targeting-checks`）：全 PASS，新增 `CombatTargetingChecks`。
- Godot headless `--smoke-test`：PASS（技能/编队/设置存档/镜头/章节/GM/星图）。
- 实机窗口 `--capture --capture-targeting=<intro|tank-first|rear-entry|crowd|ranged|intro5|crowd20>`：全部 `CAPTURE_OK`，截图在忽略的 artifacts。观察要点：tank-first 为 3 Normal+5 Overflow 全锁 0；rear-entry 时原 3 Normal 保持、5 Overflow 转向后排；ranged 为 3 Ranged+3 Normal+2 Overflow；intro 警觉剩 0.35s 时主锁为空。
- `--performance-test`（RTX4060 Laptop / OpenGL）：travel 16.68ms、combat 16.67ms、map 16.67ms 帧间隔；combat process 均值 0.27ms、P95 0.48ms。有限抽样，不是硬件保证。
- TG-01～24 与 RG-01～08 大多 PASS；TG-12～14、VIS-04 因对应内容延后而 DEFERRED，不当 PASS 记。

## 五、变更面与后续

Core：新增 `CombatTargeting`；改 `GameSession`、`CombatConfiguration`、`SkillBattleState`、`SkillRuntime`、`SkillSnapshot`、`SessionSnapshot`、`Talents`；删除 Separate。UI：`MotionPresentation`、`Main`、`HudLayout`，新增隔离 QA `TargetingSmoke`。配置：`combat/fightattr*`、`unit_attributes`、`system/config`。未新增正式怪物/装备/召唤/嘲讽/Boss 表。

下一轮输入请基于“二维、五人、进房检查点重开、800ms 入场警觉”的事实，不要再引用一维/四人/立即激活/换人返场；优先补完整召唤合同、有限距离 Forced 数值、Boss 适用范围、正式远程/混合怪配置与容量成长内容。

未执行 commit / push / merge。
