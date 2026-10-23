# 2026-10-23 怪物原型与实验遭遇 V1.0 收尾归档

原 `2026-10-23_monster_encounter/`（收尾 README + 外部方案 source/）精炼为本文件；外部方案原文按归档规范不再随工程保存。原分阶段记录 E1/E2/E3 已并入本文“阶段过程”一节（同一功能模块只保留一个 review 文件），其“视觉 NOT RUN”“召唤收益待审批”已被本文覆盖。

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

## 六、阶段过程（E1–E3，原独立记录已并入）

### E1 怪物与实验遭遇（2026-10-22）

授权：首轮审计后用户回复“同意”，仅批准 E1。实现复用 `GameSession`、`Actor`、`CombatTargeting`、`SkillRuntime`；M01–M04 只是测试原型，不新增生产 AI，也不新增第二份伤害/奖励实现。

- 夹具：`tests/fixtures/monster_encounters/archetypes.csv`（四原型属性）、`members.csv`（T01=8 近战、T02=12 快怪、T03=3 重型、T04=4 近战+4 射手）。
- 检查：`tests/MonsterEncounterChecks.cs` 读取正式 `Config.Load`，真实走门后在内存会话替换测试怪群，保留入场警觉与二维空间规则；`--monster-encounter-checks` 入口已并入默认回归。
- 验证：四房×单前排/单法师/双人/五人共 16 组合 PASS（数量与分散、警觉期不索敌移动、激活后真实攻击、Normal 容量不超限、远程锁不占容量、二维边界、同 seed 快照一致）；编译 0 警告 0 错误。
- 当时边界：视觉 NOT RUN、M03 仅有属性差异无新前摇、AOE 未作为 E1 通过项（留 E2）。

### E2 范围法师与后排猎手（2026-10-22）

- `Actor.TargetPolicy` 增 `Ordinary`/`BacklinePriority`，默认 0 保持旧行为，随 `Actor.Copy` 与完整快照保存并校验；正式检查点不升级版本。
- `CombatTargeting.Select` 在 Forced 之后处理猎手：本房存活候选中选最近远程职责单位，无合法后排则回退最近合法对象；锁类型 `Special` 追加在枚举末尾、不占 Normal 容量；未新增目标管理器。
- 夹具：`archetypes_e2.csv`（M05/M06 占位）、`members_e2.csv`（T04C=4M01+2M04+2M05、T05=6M01+2M06）；M05 测试技能 `QA_E2_AREA` 复用 `AreaPulse`（半径 4、CD 60 tick、前摇/恢复 3 tick），均为占位值，生产加载器不读。
- 验证：T04C/T05 数量、警觉期不提前索敌、真实伤害、同 seed 快照一致；Special 锁选后排且不占普通容量、Forced 覆盖与到期恢复、非法后排回退、范围技能不覆盖普通远程锁、Forced 不改写已开始前摇。编译 0 警告 0 错误。
- 当时边界：视觉 NOT RUN、猎手冲刺未实现。

### E3 召唤与支援（2026-10-23）

用户裁定：敌方仆从正常给经验/金币/掉落/尸体；来源死亡后保留或撤销由配置选择（默认保留）；我方主人死亡统一撤销；上限先用临时值。

- `EnemySummonRuntime` 复用现有 `Summon` Effect、`Actor`、`Room.Monsters`、`SettleDamage`、`RecordCorpse` 与 `Room.Clear`，无第二套伤害/奖励/房间导演。
- 进房按来源预留有限槽位，槽位从死亡/未激活开始且每槽只激活一次，不从列表删除或复用 ID，因此战斗中 `Monsters.Count` 不变、友方召唤物 ID 不漂移。
- 仆从激活后是普通敌方 Actor：按来源等级与既有 Spawn 配置快照属性，击杀照常给经验/掉落/唯一尸体；`PersistAfterOwnerDeath=false` 时来源死亡撤销仆从与关联 Buff/锁，撤销不算击杀、不产收益。
- 临时字段字典（`tests/fixtures/monster_encounters/enemy_summon_e3.csv`）：`id` 对应 Summon 定义、`caster_template`/`child_template` 引用怪物模板、`skill` 引用施法技能、`alive_cap` 每来源存活上限、`total_cap` 每来源本房激活上限、`persist_after_owner_death` 布尔、`status` 标注临时。当时上限 3/9，仅隔离注册，不进生产池。
- 验证：T06 上限、唯一槽位、真实击杀收益与尸体、死亡补员、总上限停止、快照续行、检查点重开、两种死亡策略 PASS；T07 Buff 来源/刷新/离场/来源死亡后到期 PASS；编译 0 警告 0 错误。
- 当时边界：T08 真实 Boss NOT RUN、视觉 NOT RUN、M07 复活变体未实现——这些已由本文正文的 T08 代理与 8 类渲染观察补上，但仍不是五章真实 Boss 验收。
