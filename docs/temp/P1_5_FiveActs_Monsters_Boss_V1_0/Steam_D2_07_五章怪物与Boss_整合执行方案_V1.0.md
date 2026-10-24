# Steam D2｜07 五章怪物、精英与Boss完整执行方案 V1.0

> 本合订版包括所有核心规范。精确配置见随包 data/，第一轮 Claude Code 必须只读审计；PROPOSAL 不等于已批准游戏数值。


---

<!-- BEGIN 00_主Todo与状态.md -->
# 战斗阶段主 Todo｜07 交接后更新（2026-10-22）

本文件是前期确定的「下一阶段5工作流＋8份设计文档」规划的持续版本。每次交付一份新的 Claude Code 执行包，都必须更新状态；不能用文档完成代替源码验收。

| 编号 | 工作流或文档 | 设计状态 | Claude执行规范 | Godot/C#代码与实机 | 下个动作 |
|---|---|---|---|---|---|
| P0-1 / 01 | 属性与战斗公式、护甲减法→递减百分比 | 规则确认；K随**攻击者等级**曲线待定 | 既有公式规范；**独立护甲迁移文档是否最终交付仍需核实** | UNKNOWN | 并行核实迁移专项，审批K曲线 |
| P0-2 / 02 | 8类怪物行为与T01–T08房间 | 设计完成 | **已交付 V1.0** | UNKNOWN | E1–E3源码与测试回报 |
| P0-3 / 03 | 五职业技能树公共规范 | 共通规则已确认，节点规模/点数具体值未定 | G01–G06与首批17技能包已交付 | UNKNOWN | 结合实机持续调整 |
| 04 | 法师火冰雷三系技能 | 玩法样板 | 包含在五职业首批方案中 | UNKNOWN | 扩展能力待实测 |
| P1-4 / 05 | 角色自动技能 AI | 内容已设计 | **已交付 V1.0** | UNKNOWN | G0→A1–A3源码回报 |
| 06 | 野蛮人/圣骑士/死灵/游侠 | 4个职业初稿完成；游侠仅弓 | 包含在五职业首批方案中 | UNKNOWN | 首批技能验收后扩展 |
| **07** | **五章怪物、精英与Boss内容** | **本包完成候选内容方案，Boss/掉落/难度具体值需审计** | **本次 V1.0 已交付** | **UNKNOWN** | **Claude只读审计→用户冲突审批→切片实施** |
| P1-5 | 装备属性接口与BD | 既有Modifier/SkillModifier/TriggerRule设计 | **尚未交付专项** | UNKNOWN | **下一份交付** |
| 08 | 全战斗综合回归与数值调平 | P0-2、P0-3、P1-4、07 各有分项 | **全局整合执行包尚未交付** | UNKNOWN | 装备包后最后制作 |

## 固定下一步队列

- 已交：五职业首批技能 → P0-2 8怪物实验房 → P1-4 自动技能AI → **本次07五章怪物/Boss**。
- **NEXT：P1-5 装备属性接口／词缀／技能强化／装备BD 执行规范**。
- **LAST：08 跨五职业×章节怪物×装备×三难度综合验收与数值调平**。
- **并行阻塞：P0-1护甲专项必须留在TODO，直到有审计、具体K曲线审批和真实测试证据**。

## 下次更新必填格式

```text
日期 / 当前Todo与版本：
设计规则状态（CONFIRMED/PROPOSAL/TBD）：
交接文件（DELIVERED/NEEDS_UPDATE）：
Claude只读审计（NOT_RUN/REPORTED + 文件/commit）：
用户冲突审批（PENDING/APPROVED具体范围）：
代码实施（UNKNOWN/IN_PROGRESS/IMPLEMENTED + commit）：
Core.Tests（NOT_RUN/PASS/FAIL + 输出）：
Godot实测（NOT_RUN/PASS/FAIL + 场景/录像）：
无用平行代码/配置清理（NOT_REVIEWED/DONE + diff）：
下一专项及阻塞：
```
<!-- END 00_主Todo与状态.md -->

---

<!-- BEGIN 01_工程审计与历史冲突门禁.md -->
# 01｜工程只读审计与强制冲突审批（G0）

## 1. 初始阶段严格只读

在获得用户审批前，不可编辑项目源码、生产配置、存档、地图、场景、生成器、资产。可读取/运行现有自测，但新增测试文件也应先报计划。输出**事实性现有能力表**：真实文件路径、符号、调用链、实际运行行为、设计包对应位置、缺口。

必查：五章 Region/POI/Floor/Challenge 加载器；关卡Dungeon/ Castle进度；Area/Encounter、SpawnProfile、MonsterPolicy、PlayerPolicy、TargetPolicy、AI激活延迟；房间入场散开与受击退让；Skill/Effect/Buff/Trigger、Damage; Summon; MonsterReward; Elite; Boss AI与阶段、剧情事件；存档首通与多项Castle审批队列；Godot场景表现。

## 2. 上游资料版本差异

| 来源 | 需要保留/核对 | 不得擅自推断 |
|---|---|---|
| ACT01 V0.5 | 8区41 POI、血乌支线、女伯爵高塔、安达利尔终局 | 候选Boss/原创地牢已批准 |
| ACT02 V0.1 | 9区49 POI、七墓（6假墓1真墓）、都瑞尔 | 占位怪物与Boss、全部挑战值定稿 |
| ACT03 V0.1 | 9区53 POI、崔凡克→墨菲斯托 | 议会与墨菲斯托需要创建第二个世界POI |
| ACT04 V0.1 | 5区24 POI、五封印/三守卫/迪亚波罗 | 封印应计入击杀或三守卫再从普通池生成 |
| ACT05 V0.1 | 11区64 POI、三远古者、巴尔五波 | 三远古者计为1个BossActor，波次重复计入普通刷怪预算 |
| P0-2 V1.0 | M01–M08与T01–T08、近战容量/远程/后排规则 | 需要第二套MonsterPolicy |
| P1-4 V1.0 | 技能候选/目标/动作边界/普攻兜底 | 需要第二套ActionQueue或Buff系统 |

## 3. 审计输出合同（不可省略）

每项内容输出如下表，不允许把猜测写为已实现。

| 内容要求ID | 工程真实路径 / 方法 | 现行行为 | 设计要求 | 判定 | 最小复用方案 | 用户审批项 |
|---|---|---|---|---|---|---|
| W07-001 | *代码审计填写* | *审计填写* | 城堡关卡引用唯一BossAnchor | EXACT / EXTEND / CONFLICT / ABSENT | *审计填写* | YES / NO |

对全部 `CONFLICT`（包括历史方案和源CSV与当前生产数据不一致）先输出 **Before / After / 背景 / 影响 / 存档迁移 / 选项**，并**停下问用户**。冲突审批只允许针对用户批准的条目，绝不能把本包整个PROPOSAL默认为全批准。

## 4. 高风险冲突门禁

1. `ACT01:A01-C01` 与 `ACT02:A01-C01` 等同名 POI 编码重复：必须沿用已有章级命名空间/键策略。不能擅自重命名生产ID导致存档损坏。
2. `ACT02`–`ACT05` 大量来源字段是候选配置：以当前实际Godot导入的版本为准。如果和快照不同，先出差异，不覆盖地图或挑战等级。
3. 用户已经实机加入「敌AI启动延迟 + 小队进门散开」：不得恢复旧“进门怪物立即冲锋”。当前受击退让完成情况未知，应审计复用。
4. 三难度共享世界地图，但敌人等级与刷新目标取既有Floor/Challenge配置。不能新增与原数值并行的“章级缩放器”。
5. 第一章血乌可选、第二章真/假墓、第四章封印、第五章远古三人组和五波仆从均有独特事实/预算。不能偷懒全部当做普通Boss单演员。
6. 物理护甲新方案 `Armor/(Armor+K(attackerLevel))` 已设计确认、工程是否改完未知；本包只审计，不改战斗公式。
7. 目前正式伤害类型5种（物理/火/冰/雷/毒）；不得为了经典 Boss 硬加魔法伤害；完整元素免疫首版也未批准。
8. Boss战中的嘲讽、控制、生命比例伤害需复用已有规则（静电力场/控制配置），不增加不可见的Boss特权。
9. Castle手动授权、支线手动授权、全局最低合法挑战等级路线、自动继续地牢不能受怪物内容导入影响。
10. 当前已存在的死亡奖励、掉落、宝箱、保存/重试系统不能被新内容重复发奖。

## 5. 垃圾代码防护

- 旧能力已存在→在原有类、配置加载器、验证层中增量映射；**不得同时保留旧分支与替代分支且双双执行**。
- 若确需替换：列出删除列表（废弃枚举/无引用Data/旧Policy、测试与序列化迁移），经批准后与新实现同一次变更清理。
- 不允许通过重复创建Manager/系统绕过冲突，不能注释掉测试来掩盖冲突。
- 最终交付需：有效 Diff、可复用路径列表、编译测试输出、房间战斗录像/日志、所有废弃逻辑检查结果。
<!-- END 01_工程审计与历史冲突门禁.md -->

---

<!-- BEGIN 02_章节怪物内容与区域投放.md -->
# 02｜五章节怪物目录与区域投放

## 1. 已读取的上游规模（只读源事实，不修改）

| Act | 世界地图 | 区域 | Dungeon | Castle | POI | 楼层记录/三难度 |
|---|---|---:|---:|---:|---:|---:|
| Act I 鲜血荒地→修道院 | 8×8 | 8 | 33 | 8 | 41 | 207 |
| Act II 鲁·高因→古墓 | 9×9 | 9 | 40 | 9 | 49 | 231 |
| Act III 库拉斯丛林→崔凡克 | 10×10 | 9 | 44 | 9 | 53 | 273 |
| Act IV 火焰之河→混沌 | 7×7 | 5 | 19 | 5 | 24 | 231 |
| Act V 哈洛加斯→世界之石 | 11×11 | 11 | 53 | 11 | 64 | 573 |
| **合计** | — | **42** | **189** | **42** | **231** | **1515** |

绝不可为了新增怪物种类去增加Dungeon或Castle入口。`region_source_snapshot_REFERENCE.csv` 与 `poi_source_snapshot_REFERENCE.csv` 记录复合键，内容层仅给既有POI附加怪物投放池。

## 2. 可实现的普通怪内容单位

本包提议**49个候选怪物身份**，不是49个AI类，也不是49套新美术。每个身份引用 P0-2 已定义的 `M01–M08`：基础近战、快怪、重近战、射手、法师、后排猎手、召唤巫师、支援巫师。其余差别由已有单位属性、技能、Effect、表现与掉落引用承担。

| 章节 | 核心投放主题 | 引导节奏 | 禁止 |
|---|---|---|---|
| I | 沉沦魔/僵尸/骷髅/罗格弓手/巫师/羊头人 | 基础近战→远程→少量支援和复活 | 首间就放大量猎手、堵门压死初始英雄 |
| II | 沙漠掠夺者/闪电甲虫/大木乃伊/蝮蛇/古墓法师 | 重近战、远程、复活和元素特殊技能 | 甲虫“受击雷击”没有Effect还强行特判 |
| III | 剥皮者/剥皮巫师/蛛群/萨卡兰姆/议会法师 | Swarm组合＋支援核心＋范围伤害 | 一律高频跃迁导致前排容量失效 |
| IV | 末日骑士/遗忘骑士/地狱领主/火焰幽魂 | 精英、法术、诅咒与特殊阶段 | 擅自为此加入新伤害类型和魔免 |
| V | 攻城恶魔/冰霜射手/狂乱牛头怪/魅魔/巴尔仆从 | 全能力组合，间歇性高难精英与终局波次 | 将五波仆从同时当普通怪格重复生成 |

本包的怪物中文名是**内部内容提案和D2情怀参考**，`source_kind` 明确区分 D2风格引用与新造占位；商用正式名字与美术素材后续另做IP/内容审核。不要将这份文字转为版权资产拷贝需求。

## 3. 区域刷怪池（已给出可查的42套配置）

- `region_pools_PROPOSAL.csv`：按 `ACT##:REGION_XX` 精确绑定42区域，每区域默认3–5种候选身份，权重求和100。
- 同一批怪物可以跨多个POI复用，但**每个POI通过`poi_bindings_PROPOSAL.csv`指向所属区域的怪物池**，不根据文本名称或地理推断。
- 初始按区域池给随机房间抽取敌人；当现有地牢具有独立怪物池时，**优先沿用其已实现配置**，本包仅作为提案差异，不覆盖。
- `SPECIAL_LIMITED`的敌人（猎手/召唤/支援）应当受既有 Encounter 限制；CSV 权重并非保证生成数。若现有系统没有独立限额能力，应提交审批，不能默默增加无限刷新。
- 本包权重只是**让策划能够开始调整的示例初值**，不代表正常、噩梦、地狱的最终投放概率；不可直接标记BALANCED。

## 4. 房间与刷怪要继续复用P0-2

普通房间从当前楼层既有 `CombatCellCount` / 房间生成入口、既有怪量预算获取可刷怪数量，然后由当前区域池**挑选身份**，最后由已实现Monster/Encounter生成实体。精英格继续使用 `EliteCellCount` 与既有护卫目标，Boss场/ScriptedWave/FixedUniqueActor**不进入普通池抽取**。

阵容约束：在狭小房间优先确保至少一个合法基础攻击单位；M06猎手不能自动绕过接敌容量，必须明确特殊TargetPolicy；普通远程不持续后撤风筝；敌方召唤量受上限，不能靠召唤制造无限击杀预算。

## 5. 不制造数据或关卡层级的双份事实

- 同一章三难度世界地图几何不变；地牢难度优先使用既有 POI、Floor 属性。
- 42 Castle+231 POI 是世界层，真正战斗场地由 Floor/Room生成器实例化；**世界Grid绝不能被误当战斗房间坐标**。
- `boss_anchors_PROPOSAL.csv`仅标出当前来源的 Boss 内容候选与状态，不直接改变 Castle/Chapter 完成条件。
- 不为遗忘高塔/安达利尔地下墓穴/真古墓/混沌避难所/世界之石大殿额外制造新的大地图POI。

---

## 附录A｜42区域候选投放池速览（均未正式锁定）

| 区域 | 世界地区 | 候选怪物（按权重顺序） |
|---|---|---|
| ACT01:REGION_01 | 鲜血荒地 | 沉沦魔 45%、僵尸 35%、骷髅战士 20% |
| ACT01:REGION_02 | 冰冷之原 | 沉沦魔 35%、骷髅射手 30%、沉沦魔巫师 20%、羊头人 15% |
| ACT01:REGION_03 | 埋骨之地 | 骷髅战士 35%、骷髅射手 30%、腐化罗格弓手 20%、腐化猎手 15% |
| ACT01:REGION_04 | 石块旷野 | 沉沦魔 35%、沉沦魔巫师 30%、羊头人 20%、腐化猎手 15% |
| ACT01:REGION_05 | 黑暗森林 | 沉沦魔 35%、羊头人 30%、邪教祭司 20%、骷髅战士 15% |
| ACT01:REGION_06 | 黑色荒地 | 羊头人 35%、骷髅战士 30%、吸血鬼法师 20%、邪教祭司 15% |
| ACT01:REGION_07 | 塔摩高地 | 羊头人 35%、腐化罗格弓手 30%、腐化猎手 20%、僵尸 15% |
| ACT01:REGION_08 | 修道院 | 骷髅战士 30%、骷髅射手 25%、腐化罗格弓手 20%、吸血鬼法师 15%、邪教祭司 10% |
| ACT02:REGION_01 | 碎石荒地 | 沙漠掠夺者 45%、沙漠骷髅 35%、沙漠骷髅射手 20% |
| ACT02:REGION_02 | 干燥高地 | 沙漠掠夺者 35%、沙漠骷髅 30%、闪电甲虫 20%、沙漠骷髅射手 15% |
| ACT02:REGION_03 | 遥远绿洲 | 闪电甲虫 45%、沙暴法师 35%、沙漠掠夺者 20% |
| ACT02:REGION_04 | 遗失城市 | 沙暴法师 35%、不朽尸巫 30%、荒漠猎手 20%、沙漠骷髅 15% |
| ACT02:REGION_05 | 群蛇峡谷 | 利爪蝮蛇 35%、闪电甲虫 30%、古墓祭司 20%、沙漠骷髅 15% |
| ACT02:REGION_06 | 皇宫与神秘庇护所 | 秘法法师 35%、沙暴法师 30%、古墓祭司 20%、荒漠猎手 15% |
| ACT02:REGION_07 | 术士峡谷 | 沙漠骷髅 30%、不朽尸巫 25%、利爪蝮蛇 20%、秘法法师 15%、古墓祭司 10% |
| ACT02:REGION_08 | 鲁·高因下水道 | 沙漠骷髅 45%、不朽尸巫 35%、沙漠掠夺者 20% |
| ACT02:REGION_09 | 古代通道 | 沙漠骷髅 35%、利爪蝮蛇 30%、古墓祭司 20%、秘法法师 15% |
| ACT03:REGION_01 | 蜘蛛森林 | 腐化巨蛛 35%、剥皮者 30%、剥皮巫师 20%、剥皮投矛者 15% |
| ACT03:REGION_02 | 剥皮丛林 | 剥皮者 35%、剥皮巫师 30%、阴影蝙蝠 20%、丛林猎手 15% |
| ACT03:REGION_03 | 下层库拉斯 | 剥皮者 35%、萨卡兰姆狂信徒 30%、神庙祭司 20%、剥皮投矛者 15% |
| ACT03:REGION_04 | 库拉斯商场 | 萨卡兰姆狂信徒 35%、议会法师 30%、神庙祭司 20%、丛林猎手 15% |
| ACT03:REGION_05 | 上层库拉斯 | 萨卡兰姆狂信徒 35%、议会法师 30%、神庙祭司 20%、剥皮者 15% |
| ACT03:REGION_06 | 库拉斯堤道 | 萨卡兰姆狂信徒 35%、议会法师 30%、神庙祭司 20%、湿地腐化者 15% |
| ACT03:REGION_07 | 崔凡克 | 萨卡兰姆狂信徒 30%、议会法师 25%、神庙祭司 20%、丛林猎手 15%、剥皮投矛者 10% |
| ACT03:REGION_08 | 庞大湿地 | 腐化巨蛛 35%、湿地腐化者 30%、剥皮巫师 20%、阴影蝙蝠 15% |
| ACT03:REGION_09 | 库拉斯古神庙群 | 萨卡兰姆狂信徒 35%、神庙祭司 30%、湿地腐化者 20%、议会法师 15% |
| ACT04:REGION_01 | 郊外大草原 | 末日骑士 45%、地狱小鬼 35%、魔狱射手 20% |
| ACT04:REGION_02 | 绝望平原 | 末日骑士 35%、剧毒领主 30%、遗忘骑士 20%、火焰幽魂 15% |
| ACT04:REGION_03 | 神罚之城 | 剧毒领主 35%、深渊猎手 30%、遗忘骑士 20%、魔狱射手 15% |
| ACT04:REGION_04 | 火焰之河与混沌避难所 | 末日骑士 30%、地狱领主 25%、遗忘骑士 20%、火焰幽魂 15%、深渊猎手 10% |
| ACT04:REGION_05 | 地狱熔炉 | 剧毒领主 35%、地狱领主 30%、火焰幽魂 20%、遗忘骑士 15% |
| ACT05:REGION_01 | 血腥丘陵 | 攻城恶魔 45%、传送小鬼 35%、霜原射手 20% |
| ACT05:REGION_02 | 冰冻高地 | 攻城恶魔 35%、传送小鬼 30%、血族女巫 20%、霜原射手 15% |
| ACT05:REGION_03 | 亚瑞特高原 | 攻城恶魔 35%、狂乱牛头怪 30%、山巅猎手 20%、血族女巫 15% |
| ACT05:REGION_04 | 水晶通道 | 冰霜幽魂 35%、冰河守卫 30%、霜原射手 20%、传送小鬼 15% |
| ACT05:REGION_05 | 冰川之路 | 冰霜幽魂 35%、狂乱牛头怪 30%、冰河守卫 20%、山巅猎手 15% |
| ACT05:REGION_06 | 冰冻苔原 | 狂乱牛头怪 35%、传送小鬼 30%、血族女巫 20%、霜原射手 15% |
| ACT05:REGION_07 | 远古之路 | 狂乱牛头怪 35%、山巅猎手 30%、恶魔巫师 20%、冰河守卫 15% |
| ACT05:REGION_08 | 亚瑞特山巅 | 狂乱牛头怪 35%、山巅猎手 30%、冰霜幽魂 20%、血族女巫 15% |
| ACT05:REGION_09 | 世界之石要塞 | 毁灭仆从 30%、恶魔巫师 25%、狂乱牛头怪 20%、魅魔 15%、山巅猎手 10% |
| ACT05:REGION_10 | 冰河 | 冰霜幽魂 35%、冰河守卫 30%、霜原射手 20%、狂乱牛头怪 15% |
| ACT05:REGION_11 | 尼拉塞克神殿 | 魅魔 35%、恶魔巫师 30%、毁灭仆从 20%、冰霜幽魂 15% |
<!-- END 02_章节怪物内容与区域投放.md -->

---

<!-- BEGIN 03_Boss锚点_精英与特殊阶段.md -->
# 03｜42座 Castle Boss 锚点、精英词缀与特殊关卡阶段

## 1. 42座Castle / Boss身份严格锚定已有POI

`boss_anchors_PROPOSAL.csv`从五章源 POI 表提取一一对应的42座Castle，保留**现有Boss名称及其内容状态**。诸如「某地区守将（占位）」不能当成已经确认的最终Boss，也不能为了使42座Castle“不同”而强制开发42套全新 Boss 控制器。

优先采用可复用的三层Boss配置：①当前Monster基础行为（M01–M08中匹配近远程者）；②若已有Skill/Effect可表达的1–2项特色技能；③极少数剧本式阶段需求（封印、三远古、巴尔五波）。普通Boss攻击仍遵循既有近战容量/远程锁敌；特色技能可拥有独立TargetPolicy。

**本轮必须重点保留的经典锚点**：第一章血乌（支线）、女伯爵、安达利尔；第二章牙皮、召唤者、都瑞尔、拉达门特；第三章议会成员→墨菲斯托；第四章衣卒尔、迪亚波罗与地狱熔炉海法斯特；第五章山克、远古三人组、冰冻怪物、尼拉塞克、五波仆从与巴尔。`named_dungeon_bosses_REFERENCE.csv`另外保留第一章原有“毕须博须、格瑞斯华尔德、树头木拳、铁匠”等 Dungeon 命名锚点；不增新入口。

## 2. 精英词缀：先做可安全复用的候选池

`elite_affixes_PROPOSAL.csv`提出10个候选。其一部分（护甲强化/移动加速/元素附伤）可以用既有 Modifier/Effect；高级属性（护盾、死亡爆炸、猎手目标策略）如果工程缺乏对应 Effect/Trigger/TargetPolicy 则必须先申请批准，**不得在本轮默默实现新机制**。

- 精英沿用M01–M08原型，不能新造独立 `EliteAIManager`。
- 同一词缀多次出现不默认累积同类效果，按G01/G02规则处理。
- 元素附魔只修改指定伤害/Effect，火伤不默认附加燃烧，冰伤不默认冻结；首版不提供元素完全免疫。
- 词缀数量、出现概率、怪群护卫数沿用既有楼层预算，不因难度升高擅自添加第二套精英投放公式。
- 高风险词缀先保持 DISABLED/PROPOSAL，直至用户审批当前 Effect 复用方案。

## 3. 四个需要特殊关卡合同的关键节点

### Act III｜崔凡克 `ACT03:B07-C01`

现有POI名称「崔凡克·议会神殿」，其来源楼层为5层，最后一层 BossActor=1、护卫=2，Boss 名称记录“议会成员 → 墨菲斯托”。**不要假设议会已经有独立且确定的波次数据**；Claude需审计实际关卡状态、是否需要“议会击败”事件。如果已有剧情/阶段脚本，复用；否则把“议会清除→最终墨菲斯托”作为待审批增强。不能同一议会成员既刷为普通怪又生成唯一Actor。

### Act IV｜混沌避难所 `ACT04:D04-C01`

源6个PHASE_SEQUENCE阶段：阶段1前厅、2恶魔门廊、3西翼（2封印+1守卫）、4东翼（2封印+1守卫）、5中央（1封印+1守卫）、6迪亚波罗（BossActor=1）。**5次封印为事件，不是5只怪；3守卫为固定Actor，不应再从普通池生成；迪亚波罗仅最终1个BossActor。** 进入阶段6本身不能自动创造完成事实，必须以事件消费满足前置为准。

### Act V｜山巅三远古 `ACT05:W08-C01`

源4个阶段，末阶段 `FixedUniqueActorCount=3`，`BossActorCount=0`：科力克、马道克、塔力克应作为**固定三个独特Actor**，不是额外3个BossActor，更不是1只Boss分身。三人全部真实死亡/阶段条件满足后再发城堡完成事实；同一演员不可被重复刷怪器生成。

### Act V｜毁灭王座/巴尔 `ACT05:W09-C01`

源6个阶段：世界之石要塞三层、毁灭王座入口、阶段5五波仆从、阶段6巴尔（BossActor=1）。普通难度阶段5源 `ScriptedWaveCount=5 / ScriptedWaveMonsterTarget=25`，噩梦/地狱读取各自原Floor预算。**五波仆从不能再计入普通格随机敌人池**；必须验证各波实际击杀确认后才推进到下一波，失败重试/存档不得重复入账。

## 4. Boss能力包候选（不是具体完成数值）

`boss_mechanics_PROPOSAL.csv`包括15个标志性锚点。常规Boss阶段优先通过已有基础近战/远程策略 + 标准技能体现，不新增Boss AI总管。例：安达利尔的毒素范围、都瑞尔重击、迪亚波罗可预警范围法术、巴尔的波次门槛、尼拉塞克的受限尸爆。**伤害/CD/HP/护卫策略尚未审批**；不批准Boss对嘲讽/冻结等控制的额外隐式免疫，也不得用新伤害类型凑“神圣/魔法”原典。

## 5. 演出与Mini窗口约束

多怪、精英、多个固定Actor必须在Mini战斗窗有清晰前后景和技能预警；虚拟视觉Y散开不改变真实逻辑X范围。封印和巴尔五波需要可观察的短暂节奏分段，但不要无条件停掉整体挂机循环。Boss技能前摇/预警复用现有 Windup/Commit/Recovery；无合法玩家反应输入需求，不引入手动躲圈操作或新实时微操系统。

---

## 附录B｜42座Castle Boss原始锚点逐项对照

以下为源包Boss名，**不是本次新确认的正式剧情/数值**。含“占位”“待定”的条目必须先询问用户或沿用现有占位。
| Castle POI复合键 | Castle名字 | 来源Boss | 内容状态 | 特殊阶段 |
|---|---|---|---|---|
| ACT01:A01-C01 | 鲜血荒地岗哨 | 待定 | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT01:A02-C01 | 冰冷之原哨站 | 待定 | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT01:A03-C01 | 血乌墓园 | 血乌 | SOURCE_NAMED | STANDARD_CASTLE |
| ACT01:A04-C01 | 石冢守望堡 | 拉卡尼休 | PROPOSED_ASSIGNMENT | STANDARD_CASTLE |
| ACT01:A05-C01 | 黑林营寨 | 待定 | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT01:A06-C01 | 遗忘高塔 | 女伯爵 | SOURCE_NAMED | STANDARD_CASTLE |
| ACT01:A07-C01 | 高地关隘 | 待定 | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT01:A08-C01 | 大教堂 | 安达利尔 | SOURCE_NAMED | STANDARD_CASTLE |
| ACT02:A01-C01 | 碎石关隘 | 堕落荒野守卫（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT02:A02-C01 | 高地岩堡 | 高地守卫（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT02:A03-C01 | 绿洲守卫塔 | 绿洲守卫（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT02:A04-C01 | 遗失之城城门 | 失落城守卫（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT02:A05-C01 | 利爪蝮蛇神殿 | 牙皮（Fangskin） | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT02:A06-C01 | 神秘庇护所 | 召唤者（The Summoner） | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT02:A07-C01 | 塔·拉夏古墓（真墓） | 都瑞尔（Duriel） | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT02:A08-C01 | 下水道深处 | 拉达门特（Radament） | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT02:A09-C01 | 遗迹之心 | 遗迹祭司（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B01-C01 | 蛛母祭坛 | 腐化蛛母（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B02-C01 | 剥皮祭寨 | 剥皮祭司首领（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B03-C01 | 下城腐化城门 | 萨卡兰姆堕落守门人（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B04-C01 | 商场圣殿 | 堕落圣坛司祭（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B05-C01 | 上城圣门 | 高阶狂热祭司（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B06-C01 | 堤道圣关 | 堤道圣卫统领（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B07-C01 | 崔凡克·议会神殿 | 议会成员 → 墨菲斯托 | SOURCE_CANDIDATE | MEPHISTO_COUNCIL |
| ACT03:B08-C01 | 湿地瘴母巢 | 湿地瘴母（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT03:B09-C01 | 古神庙中枢 | 古神庙守护者（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT04:D01-C01 | 郊外破魔岗哨 | 破魔哨官（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT04:D02-C01 | 衣卒尔·堕落之牢 | 衣卒尔 | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT04:D03-C01 | 神罚之城·城塞 | 地狱城塞守将（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT04:D04-C01 | 混沌避难所 | 迪亚波罗 | SOURCE_CANDIDATE | CHAOS_FIVE_SEALS |
| ACT04:D05-C01 | 地狱熔炉·核心 | 海法斯特 | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT05:W01-C01 | 督军山克营寨 | 督军山克 | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT05:W02-C01 | 冰冻高地·恶魔指挥所 | 督军艾德瑞奇 | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT05:W03-C01 | 亚瑞特高原·破军壁垒 | 破军恶魔统领（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT05:W04-C01 | 水晶通道·晶核圣所 | 水晶守护者（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT05:W05-C01 | 冰川之路·冰冢堡垒 | 冰冢看守者（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT05:W06-C01 | 冰冻苔原·霜卫城寨 | 冰原守将（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT05:W07-C01 | 远古之路·祭坛关隘 | 古道守护者（占位） | PLACEHOLDER_REQUIRES_APPROVAL | STANDARD_CASTLE |
| ACT05:W08-C01 | 亚瑞特山巅·远古试炼 | 科力克／马道克／塔力克 | SOURCE_CANDIDATE | ANCIENTS_THREE_ACTORS |
| ACT05:W09-C01 | 世界之石大殿 | 巴尔 | SOURCE_CANDIDATE | BAAL_FIVE_WAVES |
| ACT05:W10-C01 | 冰河·安雅囚笼 | 冰冻怪物（Frozenstein） | SOURCE_CANDIDATE | STANDARD_CASTLE |
| ACT05:W11-C01 | 瓦特之厅·尼拉塞克 | 尼拉塞克 | SOURCE_CANDIDATE | STANDARD_CASTLE |
<!-- END 03_Boss锚点_精英与特殊阶段.md -->

---

<!-- BEGIN 04_三难度_房间生成_奖励边界.md -->
# 04｜三难度、Encounter投放与奖励边界

## 1. 三难度规则

NORMAL / NIGHTMARE / HELL **共用同章世界地图，按源Floor和POI的挑战等级、怪物等级、人数预算独立实例化**。不能仅依 `ActIndex` 给所有怪物做一档固定等级加成，不能用提案怪物权重覆盖既有 Floor 刷怪数量。

- `poi_bindings_PROPOSAL.csv` 保存每个POI当前三个难度的 Challenge 参考值；实际以项目中正在使用的数据为准。
- `special_stages_SOURCE.csv` 保存四个特殊Castle×三难度的阶段预算；Claude不应混淆 `BossActorCount`、`FixedUniqueActorCount`、`ScriptedWaveMonsterTarget`、`BossEscortTarget`。
- 地图数据候选源已有逐层怪物数量目标；本包只追加**怪物身份与合法技能组合**，不自行重算计划击杀、楼层面积、精英格数。
- 噩梦、地狱可通过已批准的精英词缀池与技能组合提高压力，但不引入元素完全免疫，不让任一单元素职业因未知机制永久卡死。

## 2. Encounter投放契约

```text
既有Dungeon/ Castle及Difficulty已合法授权
→ 加载既有 Poi / Floor / Stage / Cell预算
→ 根据区域/现有特殊副本池选可用内容MonsterIdentity
→ 通过既有 Encounter/SpawnProfile 生成合格的普通/精英怪
→ 正确应用敌AI启动延迟和小队入场散开
→ 房间普通完成条件核查（动态召唤与ScriptedWave特殊处理）
→ 若是Castle固定Boss阶段，按脚本事实生成唯一固定Actor/波次
→ 确认真实战斗结算→首次通关/奖励（不同事实）
```

**关键约束**：怪物身份池只回答“刷哪一种”，不回答“需要增加多少只”。数量权威仍然是当前Floor预算/Encounter生成器。boss/unique/wave演员必须在独立槽位，不能占用普通池再自动补刷。

## 3. 激活/索敌/移动兼容

- 入口堵门问题已由用户现有AI延迟与小队入场散开修正：完整复用，不因切换怪物池恢复立即冲锋。
- 普通近战遵循接敌容量，容量满后其他合法目标优先；远程站定射击最近合法目标；快速近战仍遵循容量；猎手仅以独立TargetPolicy特殊突破。
- 无真实物理碰撞；视觉Y错位不改变逻辑碰撞/技能射程；投射物碰撞与墙体合法性保持原有规则。
- 玩家远程角色受击退让的完成状态未知，需与现行PlayerPolicy核对；本包不实现第二套玩家AI。

## 4. 奖励/掉落/复活的准确边界

- 真实单位生命归零只产生一次正式死亡事实。召唤怪物可以导致更多战斗演员，**但不能无控制产生经验/金币/装备**。
- 若现行Reward系统已经区分自然刷怪与召唤者单位，则直接使用；若没有，请报用户审批“奖励资格规则”，勿隐藏写死“召唤单位0经验”。
- Boss首通与重复刷取、支线Boss奖励、精英词缀装备池尚未定稿；默认复用现有RewardProfile，不从本包临时发奖。
- 精英或Boss怪物产尸体、尸爆效果也遵循G05唯一消费、G06有界连锁，不能重复创伤害事件或无限奖励。
- 失败重试收益不回滚的已有原则继续保持（如仓库现状确为该规则），但遇存档/奖励合同不一致必须先报告。

## 5. 投放与数值调平的最小验收记录

每章至少抽取早、中、后区域各一个 Dungeon，另抽该章经典 Castle；每难度用固定随机种子输出：预期/实际普通怪数、精英数、固定演员数、Boss数、波次数、怪物等级、平均房间通关秒数、前后排受击比、失败/重试、实际掉落来源。范围值待实机测量后确定，不造新默认数值。
<!-- END 04_三难度_房间生成_奖励边界.md -->

---

<!-- BEGIN 05_数据合同_接入与迁移.md -->
# 05｜数据合同、工程接入与无垃圾代码迁移

## 1. 只提供「附加身份层」，不重写上游数据

源数据仍以 ACT01 V0.5 和 ACT02–05 V0.1 已导入项目的实际版本为准。本包的 `region_source_snapshot_REFERENCE.csv`、`poi_source_snapshot_REFERENCE.csv` 是对原始CSV的字段裁剪：42区域/231 POI，便于审计对齐；**绝对不作为新的 Region/POI 生产配置替换原文件**。

`ACT##:<PoiId>` 是本包参考复合键。若游戏工程已经使用 `ChapterId+PoiId`，直接映射它；不得无故修改生产PoiId和存档键。类似 `ACT03:B07-C01` 与 `ACT04:D04-C01` 的特殊锚点，在运行时继续使用当前项目认可的BossID/StageID。

## 2. 数据文件 / 源状态

| 文件 | 行数 | 作用 | 首次开发动作 |
|---|---:|---|---|
| region_source_snapshot_REFERENCE.csv | 42 | 源区域信息与Castle关联 | 比对当前仓库，不导入 |
| poi_source_snapshot_REFERENCE.csv | 231 | 源POI、Boss候选名、三难度Challenge | 比对当前仓库，不导入 |
| monster_catalog_PROPOSAL.csv | 49 | 候选怪物身份和M01–M08映射 | 仅查现有MonsterID可否映射 |
| region_pools_PROPOSAL.csv | 167 | 42区域候选身份与权重 | 现有池缺少时提交Overlay设计 |
| poi_bindings_PROPOSAL.csv | 231 | 每个POI对应现有Region内容池 | 若已有Dungeon专属池则先列冲突 |
| boss_anchors_PROPOSAL.csv | 42 | 每座Castle Boss身份来源与状态 | 复用现有BossActor配置 |
| named_dungeon_bosses_REFERENCE.csv | 根据源数量 | 地牢中已命名的特殊Boss | 保留源事实，不额外复制 |
| special_stages_SOURCE.csv | 63 | 三难度四个特殊Castle分段与演员预算 | 对照现有关卡脚本与保存 |
| elite_affixes_PROPOSAL.csv | 10 | 候选精英词缀 | 按Effect实现度分级审批 |
| boss_mechanics_PROPOSAL.csv | 15 | 经典Boss技能包建议 | 缺能力先审批，不直接写BossController |
| difficulty_policy_PROPOSAL.csv | 3 | 三难度读取权威数据的语义合同 | 核对Floor优先级 |
| acceptance_matrix.csv | 36 | 静态/整合/Boss/回归测试要求 | 映射到现有Core.Tests |

## 3. CSV/JSON字段的执行原则

`monster_content_id`是内容候选键而非Godot真实资源ID；`archetype_id=M01…M08`是P0-2行为语义键，而非必须新增的枚举。`region_key`、`poi_key`用于本交接数据关联；`pool_id`与`elite_affix_pool_id`仅是建议资源绑定点。

禁止：读取 `monster_catalog_PROPOSAL.csv`后直接增殖49个runtime prefab、直接覆盖原有敌人AI、采用CSV里的Boss名字自动创建未经批准的Boss技能/美术、按`status=PROPOSAL`的数据自动标记生产已验收。

## 4. 推荐的最小安全集成

- 若仓库已经有 `MonsterDefinition` 和 `SpawnProfile`，先给既有内容身份配置引用已存在的 `BehaviorProfile/SkillSet`，**不造第二份MonsterDefinition**。
- 若已有地域池，制作**差异补丁**；若根本没有区域池选择机制，提出让已有 Encounter 在特定副本读取候选池的最小API扩展；向用户审批后才实现。
- 关卡生成时使用既有POI合法性、DungeonFloor/RoomSpawnBudget、StageMode、BossActor/FixedUniqueActor计数；只有怪物身份选择拓展。
- 所有美术引用可以复用临时怪物表现，但必须标记占位，不能作为正式资源已完成。

## 5. 同步更新与存档迁移

以 Before / After 配置差异（不是替换整包）交付：每次只增加已批准的怪物定义、区域池、Boss机制引用。任何涉及已存档的 Boss、奖励、POI ID、手动批准状态的改动要先给出迁移或向下兼容方案，用户同意后实施。

所有不再引用的临时MonsterID、已撤销Boss机制、重复SpawnManager分支、无效ScriptedWave配置、测试用假数据要和变更一起清理；清理清单及 `git diff` 需审查可追溯。
<!-- END 05_数据合同_接入与迁移.md -->

---

<!-- BEGIN 06_测试矩阵与观测指标.md -->
# 06｜测试矩阵、观测与通过标准

`data/acceptance_matrix.csv` 有36项：**12静态、10集成、8 Boss、6回归**。这是测试需求，不是测试通过证明。离线 `validate_examples.py` 可验证源事实映射及候选配置基本引用完整性，不能代替Godot实机。

## 1. 关键源静态断言

- 42 Region、231 POI、189 Dungeon、42 Castle、三个难度505个Floor/共1515项楼层记录，五章地图尺寸和挑战等级不变。
- 复合键唯一；42个Region池都存在且各候选权重和为100；231个POI绑定指向现有Region且Castle对应Boss锚点唯一。
- `ACT04:D04-C01`封印5次、守卫3人、迪亚波罗1人；`ACT05:W08-C01`远古3个固定演员且BossActor=0；`ACT05:W09-C01`五波和唯一巴尔，不重复加入普通池。
- 每个出现在候选池中的MonsterContentID都拥有可解析的M01–M08来源；Act2–5候选源状态不提升为用户批准事实。

## 2. CoreTests / Godot应有的战斗断言

- 正常房间：已存在Encounter刷新数量保持不变、只变怪物类别；怪物组合法定等级、难度和生成位置有效。
- 近战、远程、特殊后排AI实际遵循P0-2合同；进门先散开/敌激活延迟不被更换怪物池影响；远程角色受击退让由已有PlayerPolicy实现。
- 支援Buff停止、召唤物死亡清理、召唤上限、房间完成性、Boss追击/技能安全边界、固定Tick结果可重现。
- 同一个Boss不会被Castle固定位和普通敌人池同时刷出；全队阵亡/中途退出/重试不双发首通与Boss奖励。
- 城堡批准（含批量授权）、支线授权与自动探险排序仍走原逻辑，未经批准不生成Castle战斗实例。

## 3. 特殊序列回归

| 测试场景 | 强制观测事实 |
|---|---|
| Act I 血乌 | 侧线可拒绝，不影响主线安达利尔；可选Castle仍需独立人工授权 |
| Act II 真/假古墓 | 6假墓Dungeon+1真墓Castle，不增新入口 |
| Act III 崔凡克 | 议会→墨菲斯托；源最后阶段BossActor仅一次；如需额外议会阶段则先审批 |
| Act IV 混沌 | 5封印、3独特守卫、1迪亚波罗。封印未完成不能出现迪亚波罗 |
| Act V 远古试炼 | 三个固定独特Actor，BossActor不增加；全部击杀才达成阶段事实 |
| Act V 巴尔 | 世界之石三层→王座入口→五波仆从→巴尔；五波敌人不重复纳入普通格预算 |

## 4. Debug最低可视/可导出字段

一条可用于测试的事件记录至少能关联 `Seed / Difficulty / Chapter / Region / Poi / Floor / Room / Encounter / MonsterContent / SourcePrototype / SpawnReason / ActorId / IsSummoned / IsElite / IsBoss / Phase / SpawnSource / TargetPolicy / EventCause / DeathOnce / RewardEligible`。如果工程有等价日志，优先复用，不需要新增常驻高开销日志。

分章固定种子选取至少「早区普通地牢、中区精英地牢、经典Castle、尾区Castle」，在Normal/Nightmare/Hell各验证一次。专门统计真实生成/预计击杀差异、Boss特殊事件完成率、场均通关秒数、卡死率、重复掉落次数、房间入口堆叠、Mini窗实体密度。

## 5. 完成条件

- 所有已批准Slice的对应测试全部PASS，报告用可重复命令与源commit绑定；未开发Slice可标`BLOCKED/NOT_RUN`，不能标PASS。
- 真实 `dotnet build`、`Game.Core.Tests` 和Godot场景启动均有证据；仅脚本验证成功不能视为完成。
- 无地图POI数量/坐标、章级挑战等级、首通手动授权或奖励回归；无新建平行Policy/Encounter/Boss控制器；无重复发奖/死锁。
- 未获批准的内容候选保持Proposal，不导入生产。
<!-- END 06_测试矩阵与观测指标.md -->

---

<!-- BEGIN 07_开发切片与完成定义.md -->
# 07｜Claude Code分阶段实施计划与交付定义

## G0 — 只读源工程审计（必须先完成）

扫描当前Godot 4.7 C# / Game.Core / Game.Core.Tests / CSV JSON加载与五章图数据，输出准确源码映射、历史差异、需审批选项、可删除旧逻辑名单和不破坏进度存档的迁移方案。**G0不得写源码或生产配置**。

## C1 — 数据联结与正常怪投放（用户批准后）

- 验证当前五章 Region/POI/Floor 精确数量、ID和真实版本。对所有差异留Before/After。
- 从已存在MonsterDefinition/SpawnProfile映射少量第一章身份（M01–M04优先），将区域怪池作为**身份选择**接入既有Encounter；先以首章两个地牢和T01–T04实测。
- 第2–5章优先应用既有可复用MonsterID、现有Skill/Effect。缺资源则列明占位图、不得无审批硬造全部49种新视觉素材。
- 接敌容量、远程索敌、AI延迟、队伍进场散开全部保持。

## C2 — 精英与特殊普通怪（用户批准后）

- M05范围、M06后排猎手、M07召唤、M08支援复用P0-2的行为与P1-4 Skill调用。
- 仅启用当前源码支持且用户批准的精英词缀；先验证Buff生命周期、召唤上限、房间完结与奖励唯一。
- 不能因为缺既有Hook就新增第二套EliteManager、SummonManager。

## C3 — Boss内容与四个经典剧本阶段（用户批准后）

- 将Castle中的既有Boss Actor与 `boss_anchors_PROPOSAL` 对齐；占位守将维持占位，用户批准候选后逐个替换。
- 安达利尔、都瑞尔等普通Boss通过基础AI＋标准技能；先验证一章，再扩章。
- **阶段验收优先级**：Act III议会/墨菲斯托事实 → Act IV五封印三守卫迪亚波罗 → Act V三远古 → Act V巴尔五波。仅在工程缺失且用户批准后扩展现有阶段调度器。

## C4 — 三难度数值与连续挂机回归（用户批准后）

- 三难度的已有Floor人数和挑战等级保持权威；扫描所有五章已导入的 POI/Floor 名称、Boss演员和特殊阶段。
- 长时间挂机日志检查无召唤重复奖励、无场地死锁、无入口堵门复现；世界地图只行走，不误入新地面战斗。
- **不在本切片提前决定新Boss掉落池、原版魔法伤害、技能CD/护甲K曲线**。

## 对每一个切片统一交付

1. 具体批准ID＋变更范围与 `git diff`。
2. 复用的工程接口清单与**确认删除**的废弃代码/数据，证明没有双轨运行。
3. 生产数据前后差异摘要，保留原ID且给出存档兼容测试。
4. `dotnet build` 和 `Game.Core.Tests` 实际运行命令/输出（无项目环境时需明确`NOT_RUN`）。
5. Godot战斗观测证据：正常怪、精英、特殊阶段、困难输入、连续自动挂机、三难度。
6. 状态回写至 `00_主Todo与状态.md`；未经用户确认不得把该切片标记为完成。

**拒绝行为**：先拷贝PROPOSAL CSV到data再宣称开发完成；全量替换地图/层数；把所有占位Boss编码为正式剧情；把每章一个新Policy当作扩展；把`SourcePlannedKills`误当正在运行的实际刷新计数。
<!-- END 07_开发切片与完成定义.md -->

---

# 交接数据静态验收报告（2026-10-22）

**通过项：** 5章42区域、231 POI（189 Dungeon +42 Castle）、1515三难度Floor来源记录；49候选怪物、8个上游行为原型、42区域怪物池合计167个条目、42 Castle Boss锚点、63个特殊阶段事实、36条验收计划均通过离线字段和引用自洽校验。

已验证 Act IV 五封印 / 3守卫 / 1迪亚波罗；Act V 三远古者 / 0额外BossActor；Act V 五波仆从 / 1巴尔；三个难度都通过了预算记录一致性检查。

**限制：** 本检查使用源zip中的历史CSV和本包候选Overlay，不包含用户 Windows Godot 4.7 C# 当前工程，也没有编译、启动Godot、执行Core.Tests、测算伤害与实际怪物密度。源地图/POI当前版本仍须Claude Code逐项核对。候选权重、49怪物身份及Boss技能尚未获得生产审批。

执行命令：`python data/validate_examples.py`。

