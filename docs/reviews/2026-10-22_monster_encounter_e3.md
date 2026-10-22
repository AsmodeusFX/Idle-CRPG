> 2026-10-23收尾更新：GM技能预览已支持M01–M08；Godot冒烟与8类实际渲染/Mini观察通过。最终范围、证据和后续项见 [收尾归档](2026-10-23_monster_encounter/README.md)。下文为阶段记录。

# E3 召唤与支援实施记录（2026-10-23）

授权：用户明确裁定敌方仆从需要经验/金币/掉落/尸体；敌方来源死亡后的保留与撤销由配置选择，模板默认保留；我方主人死亡仍统一撤销；数量上限先用临时配置并标记后续接正式数值。此前建议“仆从无收益”已废止。

## 已实施

EnemySummonRuntime复用现有Summon Effect、Actor、Room.Monsters、普通索敌、SettleDamage、RecordCorpse和Room.Clear。没有第二套伤害、奖励或房间导演。

- 进房预留每来源有限槽位，槽位从死亡/未激活状态开始，每槽只激活一次；不从列表删除或复用死亡实体ID。因此战斗中Monsters.Count不变化，友方召唤物ID不漂移。
- 仆从激活后成为普通敌方Actor，生成属性按来源等级及既有Spawn配置快照，击杀照常获得经验、掉落与唯一尸体。金币/杂物仍待清房救援后空间拾取。
- EnemySummonRule.PersistAfterOwnerDeath=true保留仆从参与清房；false撤销仆从与关联Buff/锁。撤销不视为击杀，不产出收益或尸体。我方SummonRuntime主人死亡撤销规则保留。
- 来源、规则ID和一次激活事实随完整Actor快照保存并验证；正式CaptureProgress不存现场，读档按原始Origins重建怪群、技能绑定和预留槽，保留已有成长/资源及原拾取边界。
- 当前只在隔离Config实例注册规则；未投放五章怪物池、正式技能或生产配置。

## 临时CSV字段字典

文件：tests/fixtures/monster_encounters/enemy_summon_e3.csv。

id为Summon Effect mechanic_id映射键（复用已有召唤定义ID）；caster_template引用来源怪物模板；skill引用施法技能；child_template引用普通怪物模板；alive_cap为每来源同时存活上限；total_cap为每来源本房最多激活的槽位；persist_after_owner_death为布尔；status标记临时数值待正式策划接入。

当前测试仅1名来源，alive_cap=3、total_cap=9，因此该场景整房最多9只仆从；多来源时总数为各来源上限之和，并受既有1000实体硬上限约束。不是正式全房通用9只限制。规则校验拒绝无效上限、引用和超出房间实体预算。后续正式投放需接入正式配置加载与平衡数值，不能把测试注册器当正式内容入口。

## 验证

专项命令：dotnet run --project tests/IdleCrpg.Checks.csproj -- --monster-encounter-checks，PASS。

T06：存活上限、唯一槽位、真实击杀经验/掉落/尸体、死亡后补员、总上限停止生成、完整快照确定性续行、正式检查点重开、来源死亡保留及配置撤销均PASS。T07：敌方友军Buff来源、刷新不堆积、离开范围后定时Buff存续、来源真实死亡后按时到期均PASS。

工程编译：dotnet build Idle-CRPG/Idle-CRPG.csproj，PASS，0警告0错误。

T08真实Boss：NOT RUN；尚未增加可验收的真实Boss组合。Godot/Mini视觉：NOT RUN。M07复活变体未实现；当前只完成敌方有限召唤。E3整体仍待Boss与视觉验收，不能标为全部完成。未提交Git。

完整核心回归PASS（退出码0）：dotnet run --project tests/IdleCrpg.Checks.csproj；日志 artifacts/qa/monster-e3-core.log（忽略目录），包含我方召唤死亡撤销、存档、拾取交易及五章回归。
