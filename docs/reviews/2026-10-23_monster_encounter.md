# 2026-10-23 怪物原型与实验遭遇 V1.0 收尾归档

原 `2026-10-23_monster_encounter/`（收尾 README + 外部方案 source/）精炼为本文件；外部方案原文按归档规范不再随工程保存。阶段记录 `2026-10-22_monster_encounter_e1/e2/e3.md` 保留历史过程，其“视觉 NOT RUN”“召唤收益待审批”已被本文覆盖。

## 一、范围与结论

M01–M08 原型、T01–T07 与 T04C 隔离检查、T08 Boss 代理、GM 技能预览与 480×240 Mini 观察入口已交付。**正式五章怪物池没有导入这些实验模板**；正式投放与真实 Boss 转交 P1_5 专项。最终数值、怪物独立美术/动画、猎手冲刺、敌方复活变体留待后续。

| 原型 | 行为定位 | 依赖能力 |
|---|---|---|
| M01 | 普通近战（容量） | 既有 |
| M02 | 与 M01 同行为、数据差异 | 既有 |
| M03 | 近战 + 可选重击 | 复用 Skill |
| M04 | 普通远程最近目标 | Projectile |
| M05 | 远程 + 区域攻击 | Skill/Effect |
| M06 | 显式特殊 `TargetPolicy`（后排猎手） | 仅缺能力时审批 |
| M07 | 召唤/复活 | 复用 Summon/Reward |
| M08 | 标准 Buff 支援 | 复用 Buff/Modifier |

遭遇：T01 普通近战、T02 快速群怪、T03 重型近战、T04 近战+射手、T04C 范围多目标、T05 后排猎手、T06 召唤巫师、T07 支援巫师、T08 Boss 代理。

## 二、用户裁定（覆盖来源历史描述）

- 敌方仆从**正常**给经验、金币、掉落与尸体；召唤者死亡后是否保留由配置决定，敌方默认保留。
- 我方主人死亡立即撤销其召唤。
- 敌方召唤上限：当前测试值为每来源存活 3、总 9，正式数值后续接入。
- 五人上限与二维 Core 优先于来源的四人/一维假设；“无收益”建议作废。

## 三、实现要点

- Core：`CombatTargeting` 增加 `Actor.TargetPolicy`（`Ordinary`/`BacklinePriority`，猎手专用、`Special` 不占 Normal 容量）、`EnemySummonRuntime` 有限敌方召唤（槽位一次激活不复用，保证友方召唤 ID 与死亡/尸体唯一事实）、`MonsterPreview` 试场原型工厂、`SessionSnapshot` 现场校验与重开。
- UI：`SkillPreview`、`MonsterPreviewVisual`、`SceneCanvas` 预览镜头与 `Main` 捕获参数；右侧 480×240 原生 Mini 与正式战斗视角同时显示，用颜色+文字+几何编码类型（方=近战、菱=远程、圆=友方；N/O/R/F/S 标注锁类型）。
- 配置：`config/monster/preview_archetypes.csv`（`escort_count` 为陪同 M01 数量，M07/M08 现为 4，其余 0，不写死逻辑）与 `preview_enemy_summon.csv`（`alive_cap`/`total_cap`/`persist_after_owner_death`），importer=keep，仅由显式试场加载，不注入正式刷怪配置。
- 入口：设置旁 GM → 技能预览 → 选择 M01–M08 → 应用/重开；批量、等级、职业技能、暂停、单步、自动重播沿用现有控件；退出恢复原会话，试场使用独立重载 Config。

## 四、验证证据

- `dotnet run --project tests/IdleCrpg.Checks.csproj`：PASS（完整核心回归），日志 `artifacts/qa/monster-final-core.log`。
- `dotnet build Idle-CRPG/Idle-CRPG.csproj`：0 警告 0 错误。
- Godot headless `--smoke-test`：PASS（`SKILL_PREVIEW_UI_OK`/`SMOKE_OK`），覆盖真实入口、各职业技能、8 原型加载、保存保护与退出恢复。
- 实际渲染：M01–M08 逐类 `--capture --capture-skill-preview --qa-monster=M0X`，8 次 `CAPTURE_OK`；逐图检查名称、Mini 文字/形状、血条、猎手主锁与召唤/支援显示。截图与日志在忽略的 `artifacts/`。
- Core 用例：T01–T04 数量/分散出生/真实走门与警觉/攻击/容量/边界/固定 seed 复现；T04C/T05 范围多目标、主锁分离、猎手、Forced 覆盖与到期恢复、前摇、非法后排回退、快照；T06 上限、来源、唯一 ID、经验掉落尸体、补员、总上限停止、两种死亡策略；T07 来源、刷新、定时 Buff 寿命与实际攻击强化对照（修复过 Buff 操作名 `Flat→flat` 导致“仅存在性断言”的假通过）。
- T08 为 **TEST_PROXY**：Boss 标记对象真实提交范围技能 PASS，真实章节 Boss 未制作未验收。

## 五、边界

- 视觉证据为默认两只原型（召唤/支援含陪同）的渲染观察，不是所有种子、极端数量或分辨率穷举；12 怪堵门、Forced 与退让的完整动态仍以 Core 回归为准。
- 未执行 commit / push / merge。
- 下一包 P1-5 才负责五章真实怪物与 Boss 的内容/配置/验收。
