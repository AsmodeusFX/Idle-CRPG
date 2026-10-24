# 05｜Claude Code 必须遵守的 G0–G4 执行门禁

> **用户首轮授权为“设计文档落地”而非“修改真实 Godot 项目”。本包允许审计和提出方案；写码必须另获明确批准。**

## Gate 0（只读审计；首次任务必须执行）

### G0.1 先读现有工程真值

读取仓库现行 `CLAUDE.md`、`docs/00_meta/DOCUMENT_INDEX.md`、`IMPLEMENTATION_STATUS.md`（若有）以及相关已签署文档和 C# 实际源码、现有 CSV/JSON/Resource。

重点核查：

- `Game.Core`、`Game.Godot`、`Game.Core.Tests` 当前工程边界、SDK、实际 `.csproj` 目标框架。
- Git `status`、相关未提交修改、当前分支；仅报告现状，不创建提交，不清理任何文件。
- 伤害公式当前是否采用递减护甲、`K(L)` 是否使用攻击者等级、4+1 乘区是否已有完整唯一真值。
- Battle AI 是否仍有旧单英雄假设；新小队入场延迟、散开、接敌容量、嘲讽、远程受击退让真实状态。
- `SkillDefinition`/`Effect`/`Buff`/`SkillModifier`/`TriggerRule`、EffectQueue 的实际合法能力及注册/卸载路径。
- 光环与诅咒能否按来源独立存在、范围生效、同名效果刷新/择强/有限叠加、退出范围完整撤销。
- 投射物与多段伤害是否已带来源ID/资格；召唤物当前是否存在并独立参与AI、如何跨房间。
- 死亡事件/奖励、尸体消费权是否已存在、能否原子消费；原房间换队恢复规则是否已实现。
- 技能槽、初始学习技能、技能节点最高等级、装备限制的真实现状。

### G0.2 输出四份只读报告

1. **实际能力图谱**：需求ID（G01–G06、17候选技能）→现有源码文件/关键方法/已签署文档→已实现/部分/不存在→复用方式。
2. **冲突与迁移报告**：冲突编号 X01–X10 + 实际发现；每条给出旧规则、最新确认规则、当前行为、受影响存档/配置/测试、两个以上选择的成本；明确哪些必须用户裁决。
3. **最小改动方案**：按 A/B/C 三阶段，逐条列具体拟改文件、增量设计、会删哪些旧路径（需删除授权）、会新增哪些测试、是否需要配置迁移；优先无新公共 API。
4. **审批问题清单**：仅对确实挡住本次切片的问题提问；含推荐选择、风险、影响；不可要求用户重新确认已确认的多光环、多诅咒等原则。

**提交报告后暂停。第一轮禁止修改代码、配置、Godot资源、存档、旧正式文档和已有测试；如只读命令触发缓存/工具副作用，必须报告。**

## Gate 1（取得用户对冲突与实施切片的明确批准）

只有用户明确批准后，才可：

- 合并当前版本的设计文档差异进工程；更新 `DOCUMENT_INDEX.md` / `CLAUDE.md` 的**对应段落**，不得覆盖较新版本。
- 对批准的系统做最小修改；发现需求实质变更需再次报批。
- 执行符合批准范围的旧实现清理。禁止`git reset --hard`、`git clean`删文件、强推等破坏性操作，除非用户单独明确授权。

**涉及技能配置/接口/装备词缀/存档结构/公共事件/算法语义的实质变更**要有对应批准证据，不能仅因某技术实现“较优雅”就自主重构。

## Gate 2（实施建议：三切片，而非一次性17技能）

### 切片 A：基础动作、统一伤害、投射物

- 代表技能：`BAR_BASH`、`PAL_SMITE`、`SOR_FIREBALL`、`RAN_MULTISHOT`。
- 优先只复用已存在基础 `Skill/Effect/Projectile/DMG`；如多重箭语义超出现有1D命中模型，先做可证明一致性的 Core 单元测试再推进表现。
- 完成已有 `DamageInstance` 与来源事实映射，不增添新计算管线；验证随机可复现和事件唯一性。

### 切片 B：Buff、光环、诅咒、嘲讽与基础召唤

- `BAR_TAUNT`、`PAL_PRAYER`、`PAL_FANATICISM`、`SOR_FROST_NOVA`、`NEC_RAISE_SKELETON`、`NEC_AMPLIFY_DAMAGE`、`RAN_GUIDED_ARROW`。
- 必须通过 G01/G02/G04 的核心测试。若召唤跨房间的现有冒险存档合同尚未形成，切片仅限隔离无头测试，不得写隐藏临时持久化真值。
- 回归：敌方 AI 延迟/入场散开/远程退让/近战容量优先与临时溢出。

### 切片 C：复合交互、减抗、尸体、链式命中

- `BAR_WHIRLWIND`、`PAL_CONVICTION`、`SOR_CHAIN_LIGHTNING`、`NEC_LOWER_RESIST`、`NEC_CORPSE_EXPLOSION`、`RAN_VALKYRIE`。
- 完整验证 G03/G05/G06，确保同时施加不同光环和诅咒、多来源召唤、尸体一次消费及有界 TriggerQueue。
- 尚缺合法尸体/奖励事实时需专项审批，不能为了尸爆直接创建新 `CorpseManager` 与已存在死亡系统并行。

## Gate 3（验证真实结果）

- 使用实际存在的 solution/project 执行 `dotnet build`、`dotnet test`（记录原始命令、结果、失败栈）。
- Godot 场景/资源/Godot C# 集成与 Mini 实机演示要单独验收，不得把“Core 测试 PASS”当“美术/战斗表演 PASS”。
- 对实际无法运行的工具明确记为 `NOT RUN` 或 `BLOCKED`，不写“通过”。
- 压测多召唤物、多弹道、光环范围变动、尸爆连锁与固定 Tick 下的小窗口表现；保留诊断开关，不把调试状态写到永久存档。
- 如果有失败：先检查本次修改而不是偷偷改变已经确认的游戏规则以绕过测试。

## Gate 4（无垃圾代码与交付报告）

**净零平行机制**检查：

| 项目 | 禁止状态 |
|---|---|
| Damage | 旧 DamageCalculator 与新“技能 BD DamageCalculator”同时计算或双重注册 |
| Buff | 新AuraManager/CurseManager与旧BuffRuntime各有自己的有效属性真值 |
| Trigger | 新EventBus与现有TriggerRule/EffectQueue并行消费同一事实 |
| Summon | 召唤单位实际状态存在场景Node内，与Core EntityRuntime两套权威 |
| Corpse | Godot动画尸体、战斗逻辑尸体、第二套尸体库存互相不一致 |
| AI | 旧单人索敌+新小队索敌双跑，入口延迟/散开/退让被重复调度 |
| Config | 同字段同时在常量、CSV、JSON维护多个正式默认值 |
| Cleanup | 废弃代码注释保留、死配置、测试临时数值进入正式表或存档 |

清理仅针对**明确在本次批准范围、已验证无兼容依赖的旧逻辑**；其他历史模块不得“顺手”删除。任何删改首先提供变更列表及引用检查；对有歧义或存档影响的清理先再问用户。

最后报告（模板）：

```text
任务 / 状态：
当前 Git 分支与任务前 status：
读取的现行确认合同与真实源代码证据：
G01–G06 映射与冲突表：
审批项及用户决策：
实施切片（A/B/C）及实际修改文件：
是否新建公共API/配置迁移/存档影响：
废弃路径清理清单（经批准）：
dotnet build：PASS / FAIL / BLOCKED / NOT RUN （命令与日志）
dotnet test：PASS / FAIL / BLOCKED / NOT RUN （命令与结果）
Godot 场景 + Mini 视觉验收：PASS / FAIL / BLOCKED / NOT RUN
遗留问题与风险：
```
