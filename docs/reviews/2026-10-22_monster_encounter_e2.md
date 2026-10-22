> 2026-10-23收尾更新：GM技能预览已支持M01–M08；Godot冒烟与8类实际渲染/Mini观察通过。最终范围、证据和后续项见 [收尾归档](2026-10-23_monster_encounter/README.md)。下文为阶段记录。

# E2 范围法师与后排猎手（2026-10-22）

授权：用户明确要求“继续推进E2”。保留五人上限、二维逻辑、入场警觉、现有退让及统一伤害收益实现。

## 实施

- Actor.TargetPolicy新增强类型Ordinary/BacklinePriority，默认0保持旧单位普通行为。字段随Actor.Copy及完整内部快照保存，恢复校验拒绝非法枚举。正式房间检查点仍按既有规则重开，不升级存档版本，不修改生产怪物池。
- CombatTargeting.Select在Forced之后处理显式猎手策略：本房存活候选中选择最近远程职责单位；没有合法后排则回退最近合法对象。锁类型Special追加于既有枚举末尾，不计Normal容量。普通M01/M02不启用此策略。主锁仍保存在同一Targeting.Locks内。
- 现有动作前摇/恢复边界、死亡/离房清锁与Forced到期恢复路径共用，无独立目标管理器。
- tests/fixtures/monster_encounters/archetypes_e2.csv定义M05/M06占位属性；members_e2.csv定义T04C=4M01+2M04+2M05、T05=6M01+2M06。字段沿E1字典，M05技能和M06策略由隔离夹具显式绑定。生产加载器不读取这些文件。
- M05测试技能QA_E2_AREA在内存配置中复用AreaPulse，测试半径4、CD60tick、前摇3tick、恢复3tick；都是测试占位值。没有新增伤害Effect或生产技能。

## 验证

专项：dotnet run --project tests/IdleCrpg.Checks.csproj -- --monster-encounter-checks，PASS。
覆盖T04C/T05数量、警觉不提前索敌、真实伤害、相同seed完整快照一致；特殊锁选后排、不占普通容量、Forced覆盖及到期恢复、非法后排回退、完整快照续行、范围攻击多目标、范围技能不覆盖普通远程锁。补充Forced不改写已开始前摇的动作目标。
工程编译：dotnet build Idle-CRPG/Idle-CRPG.csproj，PASS，0警告0错误。

视觉验收：Godot/Mini NOT RUN，本切片没有新增可视测试入口。猎手冲刺未实现，本次只验证合法普通移动与专属索敌。不是完整Boss或正式章节投放。E3未实施。

完整核心回归：dotnet run --project tests/IdleCrpg.Checks.csproj，PASS；日志保存在忽略目录 artifacts/qa/monster-e2-core.log，包含原有退让、入场、存档、奖励及五章流程检查。
