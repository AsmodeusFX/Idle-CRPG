# 03｜配置草案与现有工程对接原则

> **非正式生产表**：字段名、单位、默认值、数据承载位置须在 G0 对照真实工程后提交负责人审批。`data/` 中是教学/测试夹具，**禁止直接替换项目正式表**。

## 1. 最小数据需求：尽量不用新表

| 逻辑数据 | 用途 | 优先复用目标 | 状态 |
|---|---|---|---|
| `EngagementCapacity` (int>=0) | 普通近战可优先承接的数量；0无优先槽位 | 当前角色/怪物/召唤物有效战斗属性或战斗配置聚合入口 | 玩法已确认，**归属待审计** |
| 当前攻击方式 Melee/Ranged | 目标分配走哪条规则 | 已有 BasicAttack/Skill 行为定义 | 已确认，具体读取入口待审计 |
| 目标可攻击/可追击资格 | 避免锁死亡、不可达或跨房间对象 | 现有 TargetValidation / Movement | 已有原则，现状待审计 |
| `TargetLockKind` | 正常预约/临时超额/强制锁定 | 现有 `AIRuntimeState` 的扩展字段或等价标识 | 推荐表示方式 |
| `ReservedTargetId` / 等价映射 | 预约归属及释放去重 | 现有实体实例ID、TargetLock生命周期 | 推荐，勿重复权威 |
| `NeedsReevaluation` + 原因 | 有界刷新队列 | 已有AI状态重评标志 | 推荐复用 |
| 怪物目标行为模板 | 普通近战/普通远程/特殊技能 | 已有 `MonsterPolicy` / AIProfile | 已确认差异，Schema待审计 |
| 视觉站位索引 | 视觉Y分散、稳定围攻 | Godot已有表现节点/战场表现组件 | 表现建议，不参与伤害 |

### 特别注意：FightAttr 是强类型合同

历史 `FIGHT_ATTRIBUTES.md` 曾明确列出固定数量的 FightAttr 字段。如果当前仓库仍然这样设计，**不得擅自往 `fightattr.csv` 增加未批准字段**。可以提出：

- **选项 A**：负责人批准将接敌容量纳入已有 FightAttr 系统，并正式修订强类型合同、聚合与存档影响；
- **选项 B**：作为单位AI/战场承接能力配置及受到天赋/装备修饰的**独立战斗属性**，不更改现有 FightAttr 表；要确认未来能否被 Modifier 影响。

需要 Claude Code 在审计报告中给出权责边界和最小改动比较；**不能自行选一个写死**。

## 2. 示例配置（仅作测试，不是权威参数）

`data/Unit_Engagement_Example.csv`：模拟基础容量。`EXAMPLE_ONLY` 明确指出非正式生产平衡。

`data/Targeting_Config_Example.csv`：模拟普通近战优先槽→后排→可超额，普通远程默认最近，以及视觉散开的配置意图。

**正式数据应贴合既有 CSV/JSON + 统一 C# 校验格式**。避免双重配置：同一个 `EngagementCapacity` 不要在英雄基础属性、AIProfile 和技能 JSON 同时分别存一份“当前值”。唯一权威定义 + 受控 Modifier/快照；不可变定义和实例运行态分离。

## 3. 概念伪代码（不授权照抄类名或实现方式）

```csharp
// PSEUDOCODE ONLY — replace with current Battle AI/TargetPolicy contracts.
// This is NOT a complete/compilable implementation.
TargetSelection SelectOrdinaryMeleeTarget(Monster actor, WorldState world)
{
    var candidates = ExistingTargetEligibility(world, actor); // legal & observed
    if (candidates.Empty) return NoTarget();

    // Existing stable distance / tie rules must be reused.
    var captors = candidates.Where(x => x.EngagementCapacity > NormalReservations(x))
                            .Where(x => ExistingChaseEligibility(actor, x));
    if (!captors.Empty)
        return ReserveAtomically(ChooseNearestStable(captors), kind: Normal);

    // Units with 0 capacity are still fully attackable; no one-to-one limit.
    var other = candidates.Where(x => !IsFullCapturingFrontliner(x));
    if (!other.Empty)
        return LockTarget(ChooseNearestStable(other), kind: Overflow);

    // Tank-only / no others: temporary overflow attack is always legal.
    return LockTarget(ChooseNearestStable(candidates), kind: Overflow);
}

// When a new allied player/attackable summon enters, a normal slot frees,
// or an existing target becomes invalid, mark only eligible overflowing
// attackers for reevaluation. Do not interrupt their committed actions.
void OnCandidateSetChanged(...) => ExistingAI.MarkReevaluation(...);
```

### 伪代码的两个例外要审计

1. **同队全员都为0容量**：`captors` 为空，`other` 需要包括所有可攻击角色，最近目标正常成为 `Overflow`，不会产生全军“无坦”死锁。
2. **两名坦克都满载、没有后排**：`other` 为空，`fallback` 可超额攻击最近的合法坦克。若另一名坦克未满但距离/可达性不合法，不能强行把敌人吸至远端。

`IsFullCapturingFrontliner` 必须基于目标在此房间对**该攻击者**的真实合法性及槽位状态；不能仅用“角色职业是不是野蛮人”判断。若后排通过装备获得容量，那么其不再默认被视为0容量。只允许一份权威预占，不能让两个目标选择器竞态修改。

## 4. 目标刷新与预约原子性

建议使用现有 Core 固定 Tick 的单线程有序阶段（如果实际是并行模拟，需另报一致性方案）；**永远不要**在 Godot 动画播放完的回调里决定目标预约。示例过程：

```text
Tick N：收集已提交的入场/死亡/召唤/属性变更事实
  → 在当前已有阶段释放失效预约
  → 用稳定的攻方实例 ID 顺序取出待选目标的近战怪
  → 检查有效候选及预约剩余容量
  → 选择 → 立即提交该怪本次预约 / Overflow / Forced 目标
  → 产出下一合法动作请求
  → 其他 Combat Runtime 依既有合同推进
```

**动作安全边界**：当怪物已发出不可中断攻击，允许它继续完成动作；待下一合法 AI 决策点才生效新目标。旧目标死亡/失效时不能凭空造成伤害。目标变更不能单凭 `OnTargetChanged` 递归再触发分配。

**必要不变量**：没有重复预约、预约不指向注销实体、容量减小不会永久锁住超出的预约、Forced不持有一个不存在目标的锁、同Tick输入相同则决策结果相同。

## 5. 与现有子系统的最小接口

| 系统 | 输入/输出 | 禁止事项 |
|---|---|---|
| Encounter / Room | Actor进场与出场、目标激活、当前房间身份 | 不把上个房间的预约带到下个房间 |
| MonsterPolicy | 主目标/攻击方式/当前候选快照 | 不在策略里直接执行伤害、装备替换或世界进度 |
| TargetLock Runtime | 正常预约、Overflow、Forced生命周期 | 不增加第二套权威“目标列表/仇恨表” |
| Movement | 按已批准目标及射程移动 | 不引入单位刚体的真实路径挤压 |
| Skill/Effect/Buff | 嘲讽/救场的强制目标效果与过期 | 不绕过Windup→Commit→Recovery/CD |
| Summon | 可攻击召唤物生成/死亡/注销事实 | 不把召唤物混成永久角色ID |
| Godot view | 目标锁定和虚拟围攻点/视觉Y偏移 | 视觉Y不影响Core的X射程或命中 |
| Projectile/Damage | 现有一维连续扫掠和伤害管线 | 不得因“无物理碰撞”删除/简化这些算法 |

## 6. 存档、切房间、回城换人

- 每次进入新房间必须清理或重建本房间的临时目标预约；不能把上房间 `TargetId` 带入本房间。
- 用户有“房间战斗结束/小队死亡后回城换人，并通过传送门回到原房间续玩”的正式方向；**本包不决定**途中存档/重进时是否保留怪物当前HP/位置/锁定。应先审计现有恢复语义。
- 原则上 `EngagementCapacity` 定义与永久技能/装备数据来自各自权威源；`Normal`/`Overflow` 锁与视觉站位属短期 Encounter Runtime。如果实际需要存储这些内容才能完成恢复，列迁移报告并请负责人批准。
- 不能触碰世界地图 Waypoint / Castle 手动批准队列 / Loot 已结算收益的权威逻辑。

## 7. 审计后的正式接口选择

Claude Code应在G0报告中填表再向负责人确认：

| 职责 | 现有真实类/文件 | 拟扩展内容 | 是否改变已确认接口 | 是否影响存档 |
|---|---|---|---|---|
| 单位有效接敌容量 | 待审计 | 新字段或复用 | 待审计 | 待审计 |
| 近战锁定/预约 | 待审计 | 正常/溢出分配 | 待审计 | 待审计 |
| 后排入场事实 | 待审计 | 标记超额重评 | 待审计 | 待审计 |
| 嘲讽Forced | 待审计 | 复用已有技能 | 待审计 | 待审计 |
| 视觉Y散开 | 待审计 | 表现层增强 | 待审计 | 不应，但待审计 |

## 8. 不应由本包顺手加入的扩展

- 仇恨值、治疗仇恨、嘲讽命中率、坦克防护光环的额外属性。
- 真实单位碰撞或二维实时避障；新的战场导航框架。
- 玩家手控位置、手动指定Boss刷取、装备掉率平衡、技能树改造。
- 重生/AP、共享经验池、线性装备自动替换等此前已撤回方向。
