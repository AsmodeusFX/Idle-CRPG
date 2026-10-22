# Claude Code 第一轮指令｜07 五章怪物与Boss内容（只读审计）

你正在一个已有 Godot 4.7 .NET / C# Steam D2 项目工作。请先完整阅读本执行包 README、00、01、02、03、04、05、06、07 与 data/source_manifest_REFERENCE.json。

**第一轮只读审计，不修改任何源码、配置、资源、存档，也不创建新Monster/Encounter/Boss控制器。**

请检查真实工程里已有的五章 Region/POI/Floor/Challenge、Castle人工批准与支线授权、Area/Encounter/SpawnProfile、MonsterPolicy/TargetPolicy、MonsterDefinition、Skill/Effect/Buff/Trigger、Elite/BossActor/StageSequence、奖励/存档/重试、Godot表现。要特别确认当前已有**敌方AI启动延迟、进场散开**，以及远程受击退让的实际状态。

先比较本包参考快照与当前项目：42 Region、231 POI、189 Dungeon、42 Castle、三难度505层/共1515份楼层记录；Act2–5 源中大量内容为候选，不能擅自批准。核对复合PoiKey跨章节重复。对Act III议会/墨菲斯托、Act IV五封印三守卫迪亚波罗、Act V三远古、巴尔五波给出实际事件/刷怪/计数映射与缺口。

请提交以下成果：

1. **现有实现事实表**：每一项要求→真实代码/配置路径→现行行为→可复用机制/缺口，附源码证据。
2. **冲突审批表**：所有 CONFLICT/ABSENT/内容源与当前生产不一致，列出Before/After、影响、存档与测试迁移、可选最小修复方案；**有冲突向用户询问，不得继续改动**。
3. **候选内容映射表**：哪些 `monster_catalog_PROPOSAL` 的49种身份能直接指向现有MonsterID/技能/美术，哪些必须占位或延期；42区域/231POI已有数据是否完全覆盖。
4. **阶段计划C1–C4**：仅为用户已批准的范围安排最小修改，标清依赖与后续测试；不能把候选怪物权重与Boss特殊技能当成已批准平衡值。
5. **垃圾代码预防清单**：可能重复的AI/Spawn/State系统、需保留的现有接口、如果未来替换需要同步删除的旧分支。
6. **第一轮状态**：`G0=REPORTED; IMPLEMENTATION=NOT_STARTED; BUILD/TEST=NOT_RUN`，不能先声称实际编译已通过。

严格继承已确认规则：世界地图无战斗、玩家批准Castle、支线可选、小队4人首发上限、AI容量/目标锁定、敌AI延迟与入场散开、5伤害类型、多光环和多诅咒可以共存、召唤/尸体/收益边界，以及现有P0-2与P1-4实现。发现文档、配置与真实工程冲突时**先停下报告并向用户询问**。不允许为赶交付直接重写现有系统、另起平行逻辑或留下垃圾代码。
