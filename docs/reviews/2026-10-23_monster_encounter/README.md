# 怪物原型与实验遭遇 V1.0 收尾归档

日期：2026-10-23。本文为本轮最终状态，替代E1/E2/E3阶段记录中的“视觉NOT RUN”和“召唤收益待审批”。设计包已归档于source/；原temp路径保留用于历史引用。原始包中的四人、1D、无收益建议等历史描述不得覆盖下述裁定。

## 结论与边界

M01–M08原型设计与已授权Core实施、T01–T07及T04C隔离检查、GM技能预览加载和Mini观察入口已交付。T08使用显式Boss标记代理检验现有范围施法接口；不是五章真实Boss的机制验收。正式怪物池没有导入这些实验模板。

正式五章投放与真实Boss转交P1_5专项；最终数值、怪物独立美术/动画、猎手冲刺、敌方复活变体留待后续内容实施。本轮不能作为这些内容已完成的证明。

用户最终裁定：敌方仆从正常经验、金币、掉落及尸体；召唤者死亡保留策略可配置，敌方默认保留；我方主人死亡撤销其召唤；上限目前测试存活3、每来源总9，后续正式数值接入。五人上限与二维Core优先于来源旧规范。

## 使用入口

设置旁GM → 技能预览 → 怪物种类选择M01–M08 → 应用参数/重新开战。数量、等级、职业技能、暂停、单步与自动重播沿用现有控件。M07/M08自动附带4名普通近战，方便观察召唤或支援。非该类型的陪同怪不计入数量输入。

右侧480×240原生Mini区与左侧正式战斗视角同时显示，类型以颜色+文字+几何编码：剑/爪/锤/弓/焰/猎/召/援；方形近战、菱形远程、圆形友方。N普通容量、O溢出、R远程、F强制、S猎手；圆环表示施法动作，+表示Buff，虚线连接召唤来源。Mini图标会显示避让，不能当作新的Core位置。

原会话与配置在进入时保留，试场使用重新加载的独立Config；退出恢复原会话。自动化只读写artifacts/qa。预览CSV放config/monster且importer=keep，只由显式试场加载，不注入正式刷怪配置。

## 字段字典

preview_archetypes.csv：id唯一原型键；name显示名；template既有怪物模板引用；hp/attack/speed/range/cooldown为正有限测试值；ranged布尔；policy为Ordinary/BacklinePriority；effect引用既有Effect；skill_cooldown_ticks/windup_ticks/recovery_ticks为20Hz动作参数；color为显示色；symbol为Mini类型符号。数值均为原型占位。

preview_enemy_summon.csv：沿E3字段合同，alive_cap/total_cap为每来源上限；persist_after_owner_death决定来源死亡后保留；status注明临时值待正式数值。槽位一次激活、不复用，保障友方召唤ID与死亡/尸体唯一事实。原型预览的技能绑定由沙盒显式完成。

## 验证证据

- dotnet run --project tests/IdleCrpg.Checks.csproj：PASS，完整核心回归；日志artifacts/qa/monster-final-core.log。
- dotnet build Idle-CRPG/Idle-CRPG.csproj：PASS，0警告0错误。
- Godot 4.7 .NET --headless --path Idle-CRPG -- --smoke-test：PASS，SKILL_PREVIEW_UI_OK/SMOKE_OK。覆盖现有入口、所有职业技能、8怪物原型加载、保存保护与退出原会话恢复。日志artifacts/qa/monster-godot-smoke.log。
- OpenGL Compatibility实际渲染：M01–M08逐类--capture --capture-skill-preview --qa-monster=M0X，8次退出码0/CAPTURE_OK。逐图检查原型名称、Mini文字/形状、血条、猎手主锁及支援/召唤显示；截图位于artifacts/preview-skill-preview-M01.png至M08.png，日志artifacts/qa/monster-M0X-render.log。截图和日志均为忽略产物，不提交。
- T01–T04：数量、分散出生、真实走门/警觉、攻击、容量、边界、固定seed复现PASS。
- T04C/T05：范围多目标、普通主锁分离、猎手、Forced覆盖/到期恢复、动作前摇、非法后排回退、快照PASS。
- T06：有限上限、来源、唯一ID、经验/掉落/尸体、补员、总上限停止、完整快照/正式重开、两种死亡策略PASS。
- T07：来源、刷新、定时Buff寿命与来源死亡、实际攻击强化对照PASS。收尾修复了测试Buff操作名Flat→flat，此前仅断言Buff存在不足以证明数值生效。
- T08 TEST_PROXY：Boss标记对象真实提交范围技能PASS；真实章节Boss未在本轮制作或验收。

视觉证据是默认两只原型（召唤/支援含陪同）的渲染与观察；不是所有种子、极端数量或所有分辨率的穷举。12怪堵门、Forced与退让的完整动态不变量主要由Core回归证明，不把单张截图替代行为测试。

## 改动索引与后续

Core：CombatTargeting/Actor特殊策略、EnemySummonRuntime有限敌方召唤、MonsterPreview试场原型工厂、SessionSnapshot现场校验与重开。UI：SkillPreview、MonsterPreviewVisual、SceneCanvas预览镜头及Main捕获参数。测试与数值：tests/MonsterEncounterChecks、MonsterSupportEncounterChecks及tests/fixtures/monster_encounters；两份显式预览CSV。

未执行commit/push/merge。阶段记录保留历史过程，以本页最终范围和证据为准。

补充字典：preview_archetypes.csv的escort_count为陪同M01数量，M07/M08当前配置4，其余0；不把陪同数量写死在逻辑中。
