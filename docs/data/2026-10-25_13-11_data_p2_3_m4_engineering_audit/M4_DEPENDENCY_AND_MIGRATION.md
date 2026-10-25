# P2-3-M4 状态所有权、双权威清理与迁移顺序（V2 Review 回填版）

- 版本：**V2**，2026-10-25 13:54（回填设计 Owner Review V1.0 的 R-01/R-02/R-04/R-06）
- 性质：只读审计结论 + 后续工作包建议；**不构成编码授权**。工时为粗量级，不作工期承诺。
- 关联：`M4_DIFF_MATRIX.csv`（D01–D27，含 `门禁分层` 列）、`M4_CURRENT_ENGINEERING_STATE.md`、`M4_OPEN_ISSUES.md`

---

## 1. 目标状态所有权（Account vs Run）

当前只有一个权威：`SaveDocument.Session` → `GameSession`。下表按现有字段逐项归属（右列即待迁移点）：

| 目标层 | 数据 | 现状位置（须迁移/复制） |
|---|---|---|
| **Run（本轮，重生清零）** | 英雄实例（等级/经验/天赋/技能/装备）、上阵与候补编队 | `GameSession.Heroes`、`FormationPlans.reserveHeroes`、`SessionSnapshot.Heroes/ReserveHeroes` |
| Run | 公共 XP 池（新增，替代 `Actor.Experience`） | 无；现为 `HeroExperience` 每人独立 |
| Run | 金币、背包实物、待拾取战利品 | `GameSession.Gold/inventory`、`Room.Loot` |
| Run | KP 余额、`[Lmin,Lmax]`（新增） | 无；现为 `SpawnModifiers` 临时增量 |
| Run | 本轮星图（`StarProgression` + starAttack/starHp 差量） | `Stars.cs`、`SessionSnapshot.Stars/StarAttack/StarHp` |
| Run | 章节/难度/地图完成、统治区占领、城堡队列、失败回刷计数、`Kills` | `ChapterCampaign`、`WorldProgress`、`FailureRecovery`、`GameSession.Kills` |
| Run | 场景/行进/村庄流程/传送相位、随机种子 | `WorldSnapshot`、`Seed/DungeonSeed` |
| **Account（跨轮保留）** | 成就事实记录、成就点余额、已购永久商品（含第5席）、已解锁职业集合 | 无（`hero_unlocks` 现读轮内 `Kills`） |
| Account | 轮次历史（通关次数、已完成轮次的章节记录） | 无（`CompletedMaps` 属本轮） |
| **不属于任何一层** | 显示/音频偏好、窗口尺寸、全屏 | `AppSettings/SettingsStore`（`settings.json`，已正确独立） |
| 跨层桥 | 存档版本、备份与重置策略 | `SaveDocument.Version`、`.bak`、`.unreadable-*` |

**前置**：`Actor` 只有 `SpecId`，无实例 ID。D02（4 槽 + 可重复职业）与 D24（重复职业 Buff/召唤不混淆）都卡在此处 —— **先加稳定实例 ID，再谈编队与叠层**（技术自决 T-01）。

---

## 2. 双权威 / 多重权威清理清单

| # | 权威漂移 | 现状 | 目标 | 处理 |
|---|---|---|---|---|
| A1 | 怪物等级 5 套 | `dungeon.level_min/max` → `poi_challenge.challenge` → `floor_plan.MonsterLevel`（最终）→ `spawn_rule.level` 偏移；`FloorMonsterLevel`/`difficulty_bands` 有列无消费者 | 单轮唯一 `[Lmin,Lmax]` | `GameSession.Reveal` 收敛为区间一次抽样；规划列标死或删除；5 章 `floor_plan` 列统一 |
| A2 | 经验归属双算 | `Actor.Experience` 每人独立 + `GameSession.Experience` 投影 `Heroes[0]` | 公共池 + 每人等级/已花XP | 删除投影；旧档一次性折算入池 |
| A3 | 经验衰减 | `experience_level_rate.csv`（+5 档=0%）仍在消费 | 无衰减 | 删除消费点，表降级为 LEGACY 或删除 |
| A4 | 自动 vs 手动升级 | `GrantHeroExperience` 内 `while` 连升 | 仅玩家命令升级 | 拆"入池"与"玩家升级命令"；读档不得自动补升 |
| A5 | Armor 与将来 AR/DR 并存 | `DamagePipeline.cs:85` 物理 `K/(armor+K)` | 唯一 AR/DR 倍率 | 先接 AR/DR 倍率，再让 `armor` Key 与 `armor_curve.csv` 退场 |
| A6 | 编队容量双源 | `1+Σ party_slots`（硬编码）+ `FormationCapacityFloor`（旧档兜底） | 固定 4 槽 + 第 5 席来自账号永久商品 | 移除星图 `party_slots` 4 节点；floor 仅用于旧档 |
| A7 | 技能槽双源 | `active_skill_max_slots=5` + 星图 `skill_slots` 4 节点 + `SkillIds` 有序装配 | 4 固定技能 + 1 特性，学会即用 | 下架 `skill_slots`；`SkillIds` 改派生集合；`SkillSwapLocks` 退役 |
| A8 | 天赋前置双语义 | 单值 `Prerequisite`，无互斥/区域洗点 | 互斥组 + 免费局部洗点 | 新增分组字段与洗点范围；`TalentLayoutLegal` 同步 |
| A9 | 职业解锁双语义 | `hero_unlocks.kills` 读轮内 `Kills` | 账号职业解锁 | 拆账号集合 + 本轮可选池 |
| A10 | 旧 `CastTalent` 与 `SkillRuntime` 并存 | 23 个主动节点未绑 `skill_id`（`Talents.cs:165`） | 只保留统一技能路径 | 按 P2-2 D-04 迁移（纯数据约 46 行），与 D08 同批 |
| A11 | 存档迁移无统一入口 | 外层 `Version` 1~4 同解，靠 12+ 内部字段 | 明确分层迁移入口 | 新增 `SaveDocument.Version=5`，配 `Account` 区段与逐版本分支 |
| A12 | 星图编辑器写生产根 | `StarUi.cs:114` 默认 `res://config` | 仅 QA 副本/显式发布 | 生产根写盘前要求显式开关；自动检查固定 QA |

### 2.1 替代性验收条件（R-06 落地：只许替代，不许叠加）

每个 WP 的完成判定必须包含下列"旧路径已死"的可验证事实，**不接受"新功能可用 + 旧路径仍在"**：

| WP | 必须同时证明的"旧路径已死"证据 |
|---|---|
| WP4（AR/DR 替代 Armor） | `armor_curve.csv` 不再被 `DamagePipeline` 读取（或读取后因子恒为 1 且测试断言其不参与）；物理伤害不再随 `armor` 变化；`armor` Key 在 `fightattr.csv` 标记退役或移除；`DamageFormulaChecks` 全部改为 AR/DR 口径 |
| WP3（公共 XP 替代个人 XP） | 代码中不存在 `while` 自动连升路径（`GrantHeroExperience` 不再出现在击杀回调）；`experience_level_rate.csv` 无消费者；`HeroExperienceVersion=0` 迁移分支只用于旧档读入且写出后不再产生 |
| WP5（Run 区间替代 5 套来源） | `poi_challenge.challenge`、`floor_plan.MonsterLevel`、`spawn_rule.level` 不再是等级权威（要么删除，要么只做非等级用途）；单测覆盖"仅改 `[Lmin,Lmax]` 即可改变全部生成等级" |
| WP6（4+1 替代技能槽与旧 CastTalent） | `skill_slots` 星图节点与 `ActiveSkillSlots` 上限不再影响可学技能；`CastTalent` 分派删除；`SkillMigrationChecks` 从"旧节点保留"改为"旧节点退役" |
| WP1（实例 ID 替代 SpecId 定位） | Buff 来源、召唤归属、装备绑定、天赋存档均以实例 ID 定位；不存在任何 `ToDictionary(SpecId)` 式唯一化 |
| WP2（Account/Run 替代单层） | `SaveDocument` 无"半层"字段；`Reset`（调试重置）、放弃本轮、正式重生三者可区分且互不覆盖 |

---

## 3. 存档迁移方案

### 3.1 形态选择

| 方案 | 描述 | 优点 | 风险 |
|---|---|---|---|
| **A（推荐）** | 单文件 `progress_v1.json` 升 `Version=5`，根下拆 `Account`/`Run` 两区段 | 复用既有原子替换/`.bak`/`.unreadable` 事务 | 两区段需一次写一致 |
| B | 双文件 `account.json` + `run.json` | 可单独重置本轮 | 新事务层、跨文件一致性、双份备份策略 |
| C | 单文件 + `Run` 内嵌子文档 | 折中 | 重置时精细改写 |

**注意（R-04）**：A 仍是**待授权的技术方案**，不是既定结论；B/C 在 M5 仍可选。

### 3.2 迁移硬约束（R-04 采纳）

1. **旧角色等级不得因 XP 折算下降**。`Σ(Actor.Experience + ExperienceFraction/100)` 转公共池时，必须同时写入每英雄"已花 XP/等级"以保持属性不变；任何"用池余额重算等级"的做法禁止。
2. `Σ个人未花XP` 与新公共 XP 初始余额的换算关系**必须用真实旧档验证**，不能只用新档或测试生成档。可用素材：`artifacts/**` 914 份 `progress*.json`（Version 1=32、2=109、3=39、4=321，含 `CombatVersion/HeroExperienceVersion/SkillTreeVersion` 多组合）；**282 份 `invalid` 为故意损坏档**，用于备份/重置恢复，不是版本样本。
3. **禁止静默清档**：任何"旧档不兼容即重置"的行为必须经用户明确授权；默认路径是"保留可保留的、拒绝加载不可解释的部分、给出可读提示"。
4. 旧档迁移/开发期清档的**产品取舍必须显式裁决**（G-06）；工程侧只提供选项与影响清单。
5. `SaveStore.Reset()`（调试/设置内重置）、**放弃本轮**、**正式重生**三者：不同名、不同确认文案、不同清除范围、不同回执。`Reset` 保留设置文件，放弃本轮保留 `Account` 但不发奖励，重生保留 `Account` 并按达成结算。

### 3.3 迁移步骤（建议顺序）

1. 旧档读取时把现有 `SessionSnapshot` 全量视作 `Run`。
2. 从旧档推导初版 `Account`：成就事实按 `CompletedMaps`/`Kills` 生成最小事实集；成就点=0；永久商品为空；已解锁职业取 `hero_unlocks` 已达者。
3. 个人经验折算为公共池（遵守 §3.2 第 1、2 条）。
4. `Stars`、`Kills`、`WorldProgress`、`ChapterCampaign` 留 `Run`。
5. 新增字段（KP、`[Lmin,Lmax]`、装备实例、卷轴/药剂槽、成就点）缺省为初始值，校验层拒绝非法值。
6. 规则上非法的旧现场按既有惯例处理：保留累计成长与资源，放弃本轮地牢回村。

---

## 4. 依赖顺序（DAG，含门禁标注）

```text
① 实例标识与状态分层（D01+D26 分层、D02 实例ID/4槽/重复职业）      [G-06 挡切主档]
   ├─② AR/DR 替代 Armor（D06 → D07）                              [G-03 挡正式数值，不挡骨架]
   ├─③ 公共 XP 手动升级（D04、D05）                                [G-04 挡正式成本]
   │     └─④ KP 与怪物区间（D10、D11 → D12）                       [G-01 只挡 Lmin 开放]
   ├─⑤ 技能/天赋 4+1（D08、A10、D09）                              [内容定稿前置]
   │     └─⑥ 星图重排（D15、D25）
   ├─⑦ 经济内容（D13 → D14；D18、D19）
   ├─⑧ 轮次与账号（D17 → D16 → 第5席）                             [B-06 仅挡商店经济]
   └─⑨ 交互与生产窗口（D21 [G-05]、D20、D23 已在位）
⑩ 观测与验收（D22 [G-07]、KP 账本、引擎回归 [已具备]）
```

**必须串行**：① 先于其余全部；② 与 ③ 可并行但都要在 ④ 之前；⑦ 内 D13 先于 D14；⑧ 依赖 ①。
**可并行**：D21 的 UI 位可随 ②③ 并行设计（但实现需 G-05）；D18/D19 与 ⑤ 并行；D22 只读事实可随时做。

---

## 5. 工作包与人日（R-02 修正）

| WP | 名称 | 覆盖 D | 人日 | 门禁 |
|---|---|---|---|---|
| WP1 | 英雄实例标识与编队容量改造 | D02, D24 | 4–6 | 无（T-01） |
| WP2 | Account/Run 分层与迁移 v5 | D01, D26, D03 | 6–9 | G-06 |
| WP3 | 公共 XP 池与手动升级命令 | D04, D05 | 5–7 | G-04（仅正式成本） |
| WP4 | AR/DR 属性与对抗替换 | D06, D07 | 7–10 | G-03（仅正式曲线） |
| WP5 | 单轮怪物区间与 KP | D10, D11, D12 | 7–11 | G-01（仅 Lmin 开放） |
| WP6 | 技能/天赋 4+1 与旧节点退役 | D08, D09 | 8–12 | 内容定稿（非门禁 G） |
| WP7 | 星图重排与写盘保护 | D15, D25 | 4–6 | 无 |
| WP8 | 装备与回城换装 | D13, D14 | 9–13 | 首版部位组待定（M5） |
| WP9 | 卷轴/药剂与离线基础 | D18, D19, D20 | 7–11 | 最小内容口径（M5） |
| WP10 | 轮次/成就/重生接口 | D16, D17 | 7–12 | 无（商店经济后置） |
| WP11 | 右侧成长队列与交互 | D21 | 4–6 | G-05 |
| WP12 | 观测补齐（逐技能事实、KP 账本） | D22 | 3–5 | G-07 |

**合计（逐项相加）：71–108 人日**。V1 曾写"65–100"，为算术笔误，本版已更正（Review R-02）。该区间仍为粗量级，**不含**完整成就商店、五职业全部技能小树、最终数值标定、实机长跑与跨平台验收；M5 需按拆包与并行度重新校准。

---

## 6. 风险与回滚

| 风险 | 说明 | 缓解 |
|---|---|---|
| 存档分层一次性大改 | 迁移错误会伤真实进度 | 先只读解析 + 影子写入 QA 档，再切主档；保留 `.unreadable`；真实旧档样本先行（G-06） |
| Armor→AR/DR 双活 | 伤害基线不可解释并掩盖回归 | 同一提交内新增 AR/DR 并把 Armor 因子置 1；验收见 §2.1 |
| 自动升级残留 | 公共池/手动升级验收全部失真 | WP3 一次性删除 `while` 路径并同步改断言 |
| 旧天赋节点残留 | 无法达成"4 技能" | 与 WP6 同批；测试断言改为"退役" |
| KP 断供 | M3 已有 `[4,4]` 零击杀反例 | 未过红线检查前 Lmin 购买不开放（G-01） |
| 星图编辑器写生产 | 误操作污染源配置 | 生产根写盘需显式开关（建议 WP7 首切片） |
| UI 无 UE 规格就接线 | 返工 | G-05：先出任务链与线框再实现 |
| 观测不足 | 无法定位失败原因 | WP12 先补只读事实，再开大规模数值标定 |

回滚策略：每 WP 独立可回退；存档只允许"向前迁移"，回滚代码前先确认旧代码会**拒绝并提示**新版本存档，而不是静默错读。

---

## 7. 变更记录

| 版本 | 时间 | 变更 |
|---|---|---|
| V1 | 2026-10-25 13:11 | 首版：所有权表、12 项双权威清理、迁移方案 A/B/C、DAG、WP1–WP12、风险 |
| V2 | 2026-10-25 13:54 | R-02 修正合计为 **71–108 人日**并补 WP 门禁列；R-04 新增迁移硬约束（等级不下降/真实旧档/禁静默清档/三者不同名）与 914 份样本统计；R-06 新增 §2.1 替代性验收条件；DAG 标注 G-01~G-07 |
