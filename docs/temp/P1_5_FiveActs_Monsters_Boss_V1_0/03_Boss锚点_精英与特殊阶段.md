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
