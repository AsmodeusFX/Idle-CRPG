# 可直接复制给 Claude Code 的第一轮指令（只读，不允许写代码）

你正在开发 Godot 4.7 .NET/C# 的 Steam D2 桌面挂机 RPG，业务核心为 Game.Core，测试为 Game.Core.Tests，Godot 负责 UI/表现。请阅读本目录下 README.md、00_主Todo与项目状态.md、01_现有S3S4继承与工程只读审计.md、02–08 各规范，并同时核对真实项目已有的 S3/S4 装备纵切、FIGHT_ATTRIBUTES、ATTRIBUTE_MODIFIERS、DAMAGE_FORMULA、SKILL_EFFECT_BUFF、BattleRuntime、PlayerPolicy、Loot/Persistence 代码和测试。

**本轮只读审计，不准编辑任何代码、配置、资源、存档或生成会被工程读取的生产配置。** 输出：
1. 实际存在的 ItemDefinition/ItemInstance/ChestStorage/OpenChest/装备槽位/Modifier/SkillModifier/Trigger/Save 等模块文件、调用链和测试证据；没有就写 MISSING，不能用历史文档推测实现。
2. 逐条列出本包规则与现有代码差异（包括 S3 仓满仍战斗且丢弃、S4 手动开箱才生成装备、实例固化、武器/防具 TEST_ONLY 槽、战斗换装安全边界、读档重建、4+1 伤害、攻击者等级护甲 K(L)、多光环、多诅咒、游侠仅弓）。
3. G01–G06 扩展所需的真实最小改动与可复用接口，**禁止第二套装备/Skill/Effect/伤害/Proc 系统**。
4. 核对用户当前实际工程已实现的敌 AI 激活延迟、小队入场散开及远程受击退让，不能退回旧设定。
5. 输出冲突审批清单：每项明确现有事实、建议方案、备选方案、影响文件、Save/数据兼容影响、测试和需用户批准的决定；不要自行定稿正式品质、随机权重、装备槽位、召唤换装继承、在途投射物快照或 K(L) 生产曲线。
6. 设计 E1（基础实例词缀）→E2（技能词缀）→E3（Proc）→E4（召唤）→E5（验收清理）的最小文件变更与测试计划，指出真正可执行阶段及阻塞项。
7. 给出审计报告，**停止并等待用户确认冲突与实施阶段**。审计报告未获批准前严禁实现、迁移、改写旧代码或留下占位架构。

切勿因为“设计 V1.0”而声称工程已经编译/测试/实机通过。
