# 交接数据静态验收报告（2026-10-22）

**通过项：** 5章42区域、231 POI（189 Dungeon +42 Castle）、1515三难度Floor来源记录；49候选怪物、8个上游行为原型、42区域怪物池合计167个条目、42 Castle Boss锚点、63个特殊阶段事实、36条验收计划均通过离线字段和引用自洽校验。

已验证 Act IV 五封印 / 3守卫 / 1迪亚波罗；Act V 三远古者 / 0额外BossActor；Act V 五波仆从 / 1巴尔；三个难度都通过了预算记录一致性检查。

**限制：** 本检查使用源zip中的历史CSV和本包候选Overlay，不包含用户 Windows Godot 4.7 C# 当前工程，也没有编译、启动Godot、执行Core.Tests、测算伤害与实际怪物密度。源地图/POI当前版本仍须Claude Code逐项核对。候选权重、49怪物身份及Boss技能尚未获得生产审批。

执行命令：`python data/validate_examples.py`。
