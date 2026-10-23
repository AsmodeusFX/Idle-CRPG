# P2-2-M3 数值设计接口与内容覆盖验收

- 判定：`P2_2_M3_READY_FOR_P2_3`（31 项系统全部给出接口清单；内容覆盖缺口分级完成；1 项待批：D-13 战斗只读事实）
- 交付：本报告 + 同名机器表目录 `docs/data/2026-10-24_01-25_data_p2_2_m3_design_readiness/`
- 只读范围：本轮未修改任何正式配置、Core、外网资源与存档；仅扩展 `tests/` 下的审计导出脚本
- 复跑入口：`dotnet run --project tests/IdleCrpg.Checks.csproj -- --param-audit`

## 1. 结论摘要

| 维度 | 事实 | 判定 |
| --- | --- | --- |
| 参数台账 | 142/142 ACTIVE CSV、962 个列级参数、0 `UNVERIFIED` | 与 M2 一致，本轮复核通过 |
| 公式/消费链 | 23 条公式、23 条运行消费链 | 与 M2 一致 |
| 属性 Key | 36 个：18 投放（8 `RUNTIME_VERIFIED` + 10 `STATIC_VERIFIED`）/ 18 零投放且**均有消费分支** / 0 无消费者 | 三态列已拆开，供 P2-3 直接取用 |
| Effect | 10 类已投放 kind + 5 个零投放但有代码分支的标签（`AbilityAttack/AbilityDot/AbilityCooldown/AbilityMirror/SpawnEntity`） | 新增登记，避免“未投放”被误读为“未实现” |
| 扩容接口 | 15 条，已按 `DATA_ONLY / DATA_PLUS_CONFIG_SCHEMA / EXISTING_SYSTEM_EXTENSION` 归类 | 修正 M2 的旧数量（见 §7） |
| 设计接口就绪 | 31 项系统逐项给出权威配置、消费者、边界、验证级别、观测方法 | `design_interface_readiness.csv` |
| 工作包 | 16 个 P2-3 工作包，均含“是否需新运行规则/待定问题/P2-4 验收/UNAVAILABLE” | `content_work_packages.csv` |
| 裁决登记 | 19 条：已修 2（D-14/D-15）、已归类 2（D-16/D-19）、建议推进 10（含 D-04 P2-3 优先、D-01 不阻塞 P2-3）、维持不变 4、待批准 1 | `schema_and_decision_register.csv` |

## 2. 三态口径（本轮统一）

- `has_content_deployment`：是否有正式配置行引用该 Key/种类/分支。
- `has_code_consumer`：是否有代码读取并施加影响（含仅校验、仅展示）。
- `runtime_verification_level`：`RUNTIME_VERIFIED`（本轮实测跑过）/ `STATIC_VERIFIED`（读码确认分支可达、无内容触发）/ `UNAVAILABLE`（无观测手段）。

规则：零投放且零消费 = 内容缺口；零投放但有消费 = 待投放的接口（不是未实现）；有投放但无消费 = 接线缺陷（本轮 `NO_CONSUMER` 13 条即此类，且全部落在规划列与 `checkpoint_seconds`）。

## 3. 环境与证据链

| 项 | 值 |
| --- | --- |
| OS / Shell | Windows / PowerShell |
| .NET SDK | 10.0.301（工程 target `net8.0`，Roslyn 编译） |
| 审计命令 | `--param-audit` |
| 全量检查命令 | `dotnet run --project tests/IdleCrpg.Checks.csproj` |
| 构建结果 | 0 警告 0 错误 |
| 全量检查结果 | 全部 PASS（含模拟 A 场景：`SIM_HASH`/`SIM_LEDGER diff=0`/`SIM_XP unexplained=0`） |
| 正式配置一致性 | 142/142 ACTIVE 文件逐文件 SHA256 与 M1 清单**完全一致**（0 差异、0 缺失） |
| 源码一致性 | `Idle-CRPG/Scripts/**` 最后修改时间 2026-10-23 22:25（早于 M2 交付）；本轮仅改 `tests/ParameterAudit.cs` 并新增 `tests/DesignInterfaceAudit.cs` |
| 工作区指纹 | 本环境 git CLI 不可用（`.git` 为指针形态），改以逐文件哈希 + 最后修改时间取证（D-19） |

说明：M1/M2 使用聚合指纹；本轮因无法复现其拼接细节，改用逐文件比对（更细，且能定位到具体文件），结论等价：正式配置零改动。

## 4. 数值设计接口清单（摘要）

完整 31 行见 `design_interface_readiness.csv`。按扩展代价归类：

- **纯数据即可（`DATA_ONLY`）**：`entry_flow`、`targeting`、`heroes_levels`、`hero_unlock`、`items_loot`、`village_trade`、`town_scene`、`castle_progression`、`portals`、`monster_pools`、`monster_preview`、`rescue_revive`、`failure_retry`、`gm_tools`、`simulation`、`summons`、`buff_trigger`（补行即投放）。
- **需要新 schema 或新前提类型**：`world_campaign`（新前提类型）、`follow_formation`（上限公式硬编码）、`persistence`（新存档字段迁移）、`floor_planning`（统一列）。
- **必须动 Core**：`room_events`（表驱动事件）、`monster_content`（新行为/Boss 阶段）、`combat_stats`（逐技能事实，待批）、`skills`（新 Effect kind）、`stars`（新效果类型）。
- **UNAVAILABLE（无头环境无法验证）**：`settings_audio`、`camera_render`（视觉与音频）。

每行都给出了本轮可用观测：`--simulation` 系列、`--*-checks`、`FloorReports`、`events.csv` 事件流，或明确写 `STATIC_VERIFIED`。

## 5. 内容覆盖矩阵

### 5.1 属性 Key（36）

- 已投放 18：`atk/hp/armor/move_speed/attack_range/basic_cooldown/attack_speed_pct/engagement_capacity`（8 个运行验证）+ `dodge_rate/crit_rate/crit_damage/block_rate/block_reduction/fire_resistance/cold_resistance/lightning_resistance/poison_resistance/physical_vulnerable_damage_pct`（10 个静态验证）。
- 零投放 18：增伤乘区 `all_damage_pct/skill_damage_pct/basic_attack_damage_pct/physical_damage_pct/fire_damage_pct/cold_damage_pct/lightning_damage_pct/poison_damage_pct/ranged_damage_pct/boss_damage_pct/power_damage_pct/skill_coefficient_bonus` + 易伤 `vulnerable_damage_pct/fire_vulnerable_damage_pct/cold_vulnerable_damage_pct/lightning_vulnerable_damage_pct/poison_vulnerable_damage_pct` + `cooldown_reduction_pct`。消费者在 `DamagePipeline` 与 `SkillRuntime` 中已就绪，**P2-3 可直接用现有 Key 做内容**，无需改代码（D-05）。
- 注意：物理易伤 `physical_vulnerable_damage_pct` 已由 buff 投放，是 18 个中唯一已落地的易伤口径；新增 Key 的最小改动面仍是三处：`fightattr` 注册、`fightattr_rules` 聚合、`DamagePipeline/SkillRuntime` 消费。

### 5.2 Effect / Buff / Trigger

- 投放 kind 10 类：`Damage/ApplyBuff/Heal/MaintainAura/AbilityCast/Summon/SpawnProjectile/AreaPulse/Chain/CorpseExplosion`。
- 零投放分支 5 个：`AbilityAttack/AbilityDot/AbilityCooldown/AbilityMirror`（依赖 `ability.csv` 的 `form=buff`，当前仅 1 行 projectile）与 `SpawnEntity`（与 `SpawnProjectile` 同分支）。
- Buff 13 行：9 行纯属性、4 行带 mechanic（`haste/crit_reduce/forced/shield`）。
- `trigger.csv` / `proc_icd.csv` / `proc_limit.csv` 均为 0 行：链已实现（`React`/`ProcAllowed`），属“零投放”，补行即可试验触发流派（D-07）。

### 5.3 天赋与星图

- 天赋 61 = 39 主动 + 22 被动；16 主动绑定正式 `skill_id`，**23 主动仍走旧 `CastTalent`**（D-04，明细见 §6.2）。
- 星图 17 节点：`attack 4 / hp 3 / party_slots 4 / skill_slots 4 / talent_reset 1 / talent_reset_discount 1`；6 类效果白名单校验；功能节点受硬上限（D-08/D-09）。

## 6. 技术裁决卡

### 6.1 floor_plan 跨章 Schema（D-01 / D-02 / D-12）

- 事实：五章表头列数 40 / 37 / 38 / 41 / 42；`FloorPlanning.LoadFloorPlans` 按列名取值，缺列不崩，`FixedUniqueActorCount`/`ScriptedWaveCount`/`ScriptedWaveMonsterTarget`/`StageMode` 为条件读取。
- 逐章未被读取的列（含主键 `id`）：act1 11、act2 8、act3 9、act4 10、act5 9；跨章并集 16 个名称，其中非主键 15 个（`ChapterId/FloorCount/FloorMonsterLevel/PoiChallengeLevel/RecommendedPlayerLevel/EliteGroupMonsterTarget/ContentStatus/ConfigStatus/CompletionCondition/RequiredCombatFullCoverage/FirstClearExplorationCoverageAssumption/SourceSeedPolicy/ScaleTier/SpawnProfileId/SealActivationCount`）。
- 等级权威冲突：act2~act5 有 `FloorMonsterLevel` 但零消费者，实际取 `MonsterLevel`（另有区域池等级权威优先级）→ 建议先标注为规划列，生成对齐时再定删除或改驱动。
- 预算 vs 生成：1515 条规划记录（`FloorCount` 求和 4083 层）只进报表与加载期校验，真实生成仍按 `dungeon_rule` 的房间/走廊规模 → **不建议在 P2-3 让预算驱动生成**（会改现有楼层结构与模拟基线）。
- 裁决：P2-3 期保留漂移（方案 B，零改动、可继续出内容）；P2-4 前做列统一或拆公共/章节扩展表（方案 A/C）。

### 6.2 23 个旧 `CastTalent` 节点（D-04）

| 分组 | 数量 | 现有语义 | 迁移条件 |
| --- | --- | --- | --- |
| `shot` | 8 | 生成 `<id>_projectile` + `<id>_impact` 投射物，按倍率结算 | 新建同名 `skill.csv` 行 + `SpawnProjectile`/`Damage` 行 |
| `shout` | 4 | 半径内友军施加 `talent_battle_shout`（攻击 ×(1+value)，120 tick） | 需把该 buff 的 `target` 由 `self` 改为 `ally`（共享行，旧路径不读 target，安全） |
| `weaken` | 4 | 半径内敌人施加 `talent_weaken`（暴击降低） | 直接 `ApplyBuff(target=enemy)` |
| `nova` | 5 | 目标点范围 `SettleDamage`，`aoe:fx` | `Damage` + `extent`=半径 |
| `leap` | 1 | 跳到目标点后范围伤害 | `Damage` + `extent` |
| `whirlwind` | 1 | 自身范围伤害（附带窗） | `AreaPulse`（已有绑定先例 `barb_whirlwind_window`） |

关键约束（读码确认）：

1. `skill_id` 必须为空或**与节点 id 同名**，否则加载期校验失败 → 不能“指向另一个已有技能”，只能新建同名行。
2. 旧路径的 `Skill.Effects` 固定为 `hit_player`，因此施法表现的伤害类型恒为物理（元素旧节点表现不一致），迁移后可一并修正。
3. 23 个节点均可用现有 10 类 Effect 复刻，无需新 kind。

裁决：建议 A（纯数据迁移，约 46 行配置），P2-3 优先。

### 6.3 星图扩容（D-08 / D-09）

- 纯属性节点：加行即可（`attack`/`hp` 走 `flat` 聚合），风险仅在价格与等级节奏。
- 功能节点：`party_slots`（编队上限 1+Σ，封顶 5）、`skill_slots`（技能槽上限）为硬编码，扩上限需同时改战斗承载与 UI。
- 新效果类型（经验/金币/掉落）：需白名单 + 新消费点 + 存档字段三处扩展，且会打破“星图只加成战斗”的现状 → P2-3 建议仍用现有 6 类。

### 6.4 Combat Facts 只读事实（D-13，待批）

- 现状：`CombatStats`/`FloorRunReport` 只有本房聚合计数（命中/伤害/治疗/击杀/耗时、接敌名额占用与峰值、最近邻间距）。
- 缺口：逐技能施放与命中、攻击者身份、召唤生命周期、城堡失败序列、触发链逐次事件。
- 提案：固定容量环形缓冲 + 开关 + `artifacts/` 导出；须证明开关前后业务哈希一致、存档与事件流不变。
- 状态：`PENDING_APPROVAL`（技术负责人），不阻塞 P2-3 设计内容，仅影响 P2-4 的逐技能核验精度。

## 7. 本轮修正（审计侧）

1. `attribute_effect_matrix.csv` 拆列：`has_content_deployment` / `has_code_consumer` / `runtime_verification_level`（原 M2 把“是否投放”与 `status` 混写，`used_by_current_content` 实际等于 `status==ACTIVE`）。
2. 新增 `effect_branch_undeployed` 分类（5 行），与 `effect_kind`（10 行）分开登记。
3. 扩容接口旧文案：`易伤（17 Key 未投放）` 数字与表述均不准确 → 改为逐 Key 列举的 18 个零投放 Key（增伤 12 + 易伤 5 + 冷却缩减 1，物理易伤已投放），并注明消费者已就绪；`NEEDS_M2` → 具体扩展条件。
4. 测试入口名修正：`--star-checks` / `--save-checks` 并无独立开关，实际由默认全量检查调度（`StarChecks` / `SaveChecks`），相关引用统一改为“默认全量检查(...)”。
5. 统计口径修正：统治区为 **42**（`areapart`），415 为区域（`region`）；楼层为 **1515 条规划记录、合计 4083 层**（`FloorCount` 求和），此前“1515 楼层”为误读。
6. `NO_CONSUMER` 13 条复核：12 条 `floor_plan` 规划列（`FloorMonsterLevel×4`、`EliteGroupMonsterTarget×4`、`SpawnProfileId×3`、`SealActivationCount×1`）+ `checkpoint_seconds`，**不含**任何“零投放但已消费”的 Key。

## 8. UNAVAILABLE 清单

| 观测 | 缺失原因 | 影响 |
| --- | --- | --- |
| 逐技能施放/命中/伤害事实 | Core 仅有聚合计数 | P2-4 无法逐技能核验（D-13） |
| 召唤物生命周期（出生/接敌/死亡/超时） | 无事件导出 | 召唤强度只能按聚合反推 |
| 城堡失败序列与失败原因分布 | 未进报表 | 失败率归因只能靠模拟终态 |
| 触发/Proc 逐次触发 | 三表零投放 + 无逐次事件 | 触发流派上线前无法定量 |
| 视觉/音频验收 | 无头环境未跑 Godot | `settings_audio`、`camera_render` 标 UNAVAILABLE |
| 真实玩家时长与操作分布 | 模拟为策略近似 | 章节节奏目标只能给区间 |
| 长周期经济稳态 | 模拟窗内未触发多层星图饱和 | 经济曲线为推算，非实测 |

## 9. 机器表清单

| 文件 | 行数 | 用途 |
| --- | --- | --- |
| `parameter_registry.csv` | 962 | 列级参数台账（沿袭 M2） |
| `formula_dependency.csv` | 23 | 公式输入/顺序/默认/钳制 |
| `attribute_effect_matrix.csv` | 54（36+10+5+3） | 属性/Effect 三态矩阵 |
| `runtime_consumption_matrix.csv` | 23 | 运行消费链 |
| `expansion_interface_matrix.csv` | 15 | 扩容接口 |
| `design_interface_readiness.csv` | 31 | 系统就绪矩阵（M3 新增） |
| `content_work_packages.csv` | 16 | P2-3 工作包（M3 新增） |
| `schema_and_decision_register.csv` | 19 | Schema 与裁决登记（M3 新增） |

## 10. 复跑方法

```powershell
cd <repo>
dotnet build tests/IdleCrpg.Checks.csproj
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj -- --param-audit    # 重出 8 份机器表
dotnet run --no-build --project tests/IdleCrpg.Checks.csproj                     # 全量检查
```

输出目录为 `docs/data/<ArchiveFolder>/`，目录名由 `tests/ParameterAudit.cs` 的 `ArchiveFolder` 常量决定；重跑会覆盖同名目录下 8 份 CSV，不改正式配置与存档。

## 11. 待批准与后续

1. **D-13**：逐技能只读事实采集（技术负责人批准后进入 P2-4 实施）。
2. **D-01**：P2-4 前完成 `floor_plan` 列统一（或确认长期保留漂移）。
3. **D-04**：23 个旧天赋节点迁移（建议 P2-3 首个工作包）。
4. **D-10**：房间事件表驱动 schema（涉及 `Room.Event` 存档兼容）。
