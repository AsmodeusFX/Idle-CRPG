> 2026-10-23收尾更新：GM技能预览已支持M01–M08；Godot冒烟与8类实际渲染/Mini观察通过。最终范围、证据和后续项见 [收尾归档](2026-10-23_monster_encounter.md)。下文为阶段记录。

# E1 怪物与实验遭遇实施记录（2026-10-22）

授权：用户在首轮审计后回复“同意”，批准E1；E2/E3未进入实施。

## 实施方案与结果

采用现有GameSession、Actor、CombatTargeting、SkillRuntime；M01–M04仅为测试原型，不增加生产AI或第二份伤害、奖励实现。

- tests/fixtures/monster_encounters/archetypes.csv：四原型测试属性。
- tests/fixtures/monster_encounters/members.csv：T01=8近战，T02=12快怪，T03=3重型，T04=4近战+4射手。
- tests/MonsterEncounterChecks.cs：读取正式Config.Load，在内存会话真实走门后替换测试怪群；保留原入场警觉和二维空间规则。
- tests/Program.cs：加入默认核心回归与 --monster-encounter-checks 专项入口。

原型字段：id为测试原型唯一键；template引用正式怪物模板；hp/attack/speed/range/cooldown为正有限测试值；ranged为布尔攻击模式。遭遇字段scene为T01–T04，archetype引用测试原型，count为正整数。数值均为测试占位，不是正式平衡值。

所有配置位于tests，生产加载器未引用。收益只在临时内存会话内沿原实现结算，不写玩家进度。未修改正式CSV、存档结构、UI或现有战斗代码。

## 验证范围

专项命令：dotnet run --project tests/IdleCrpg.Checks.csproj -- --monster-encounter-checks

PASS：四房×单前排/单法师/双人/五人共16组合；出生数量及位置分散、真实进门警觉期间不索敌移动、激活后真实攻击、Normal容量不超限、远程锁不占容量、二维移动边界、相同seed完整快照一致；另有射程内射手站定检查。

编译：dotnet build Idle-CRPG/Idle-CRPG.csproj，PASS，0警告0错误。

现有CombatTargetingChecks保留容量成长、Overflow安全重评、零容量攻击、退让、模式切换、五人展开与表现偏移不写Core的回归。新专项未将AOE AI08作为E1通过项，归E2。

Godot/Mini视觉验收：NOT RUN；本次只交付Core隔离夹具，没有可从GM进入的T01–T04可视场景。墙体弹道专项、重型怪专属攻击前摇与视觉可读性未由新专项证明。M03当前仅体现HP/攻击/速度/普攻间隔差异，没有新增技能前摇。E1因此属于Core实施完成、视觉验收待完成，不宣称完整实机签收。

## 后续切片

E2：复用范围Effect，增量实现显式后排优先并保留Forced，覆盖T04C/T05。
E3：先确认敌方召唤来源、收益资格及死亡后子单位规则，再复用实体/Buff/完结链；真实Boss缺失则单项BLOCKED。

完整核心回归：dotnet run --project tests/IdleCrpg.Checks.csproj，PASS（退出码0）；包含原索敌、退让、五章流程、召唤、存档及跑图检查。
