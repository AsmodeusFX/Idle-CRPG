from pathlib import Path
from collections import defaultdict
import json,csv
R=Path('/mnt/data/D2_Act4_Map_V0_1');D=R/'data';DOC=R/'docs';DOC.mkdir(exist_ok=True)
M=json.loads((D/'ACT04_MAP_V0.1.json').read_text(encoding='utf-8'))
K=json.loads((D/'ACT04_KILL_ESTIMATE_V0.1.json').read_text(encoding='utf-8'))
with (D/'ACT04_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as fh:F=list(csv.DictReader(fh))
regions=M['regions'];pois=M['poi'];by=defaultdict(list)
for p in pois:by[p['regionId']].append(p)
L=[];out=L.append
out('# Chapter 4（Act IV）群魔堡垒：地图、地牢、Castle 与全自动攻略开发规范 V0.1')
out('')
out('> **状态：内容候选，等待地图评审与实机平衡，不代表已经接入 Godot。** 本文复用 Chapter 1 V0.5、Chapter 2 V0.1、Chapter 3 V0.1 的 Chapter/Region/POI/Gate/Floor 框架，服从《全自动攻略与地牢规模配置规则 V0.3》及后续用户确认的 Castle、支线人工批准规则。')
out('> **原典参考**：Blizzard Arreat Summit Act IV [地图](https://classic.battle.net/diablo2exp/maps/act4.shtml)、[任务](https://classic.battle.net/diablo2exp/quests/act4.shtml)。D2 原版第四章并无大量独立洞穴；以下普通地牢多为原创占位，地表场景已地牢化；混沌避难所的六阶段不是 D2 原作的六层地下室。')
out('')
out('## 0. 本章定位与五章统一规则')
out('')
out('| 项目 | 本章 V0.1 |')
out('|---|---|')
for a,b in [
 ('地图','7×7 = 49 世界格，5 区域共 28 攻略地块 + 安全城镇 1 格 + 20 个 x 不可通行格'),
 ('区域与 POI','4 主线区域 + 1 支线区域；**19 Dungeon + 5 Castle = 24 POI**'),
 ('楼层/阶段',f'**{len(F)//3}** 个独立内容楼层/阶段，三难度共 {len(F)} 条 Floor 配置记录'),
 ('主城','群魔堡垒 1 格 (6,1)，禁止地面战斗'),
 ('主线','郊外大草原 → 绝望平原（衣卒尔） → 神罚之城 → 火焰之河与混沌避难所（迪亚波罗）'),
 ('支线','地狱熔炉：神罚之城 Castle 通关后可见，**手动批准进入**，独立 Castle 仍需再次批准；不阻断迪亚波罗'),
 ('章节表现','小地图、黑色地狱断崖 + 熔岩沟壑，单桥连接，深地牢/多阶段 Castle'),
 ('战斗','只有地牢/要塞内随机房间与走廊进行 3–5 人小队自动寻路、索敌和战斗'),
 ('排序','合法候选中按照挑战等级升序；同级主线优先；AutoOrder/POI ID 稳定排序'),
 ('规划边界','等级、格数、怪物预算仅是策划初始标定；真实平衡要以 Godot 生成器及实机为准'),
 ]:out(f'| {a} | {b} |')
out('')
out('### 三难度已确认基线')
out('| 难度 | 章节怪物等级 | 推荐玩家等级 |')
out('|---|---:|---:|')
for n,a,b in [('普通','27–31','26–32'),('噩梦','62–65','61–66'),('地狱','89–92','88–93')]:out(f'| {n} | {a} | {b} |')
out('')
out('**不要混淆 D2 原版区域等级和本项目统一升级曲线。** 原版第四章主要区域普通约 26–28，怪物实际等级/Boss 有例外；本章遵循已批准的 27–31 / 62–65 / 89–92 设计工作基线。')
out('')
out('## 1. 7×7 大地图（格子和 Gate 是唯一移动权威）')
out('')
out('```text')
out('    '+' '.join(f'{i:02d}' for i in range(1,8)))
for i,row in enumerate(M['gridRows'],1):out(f'{i:02d}   '+'  '.join(row))
out('```')
out('')
out('图例：`T`=安全城镇，`1`=郊外大草原，`2`=绝望平原，`3`=神罚之城，`4`=火焰之河与混沌避难所，`5`=地狱熔炉支线，`x`=熔岩/深渊/不可通行断崖。坐标 `(row,col)` 从 1 开始，横向列 col，纵向行 row。')
out('')
out('**空间特征：** 群魔堡垒位于西下侧，连续地狱平台沿西南→中部→东北斜向推进；最后火焰之河抵达北部混沌避难所，地狱熔炉由火焰之河下游向南分出独立岔路。5 区均为 4 邻接连通。')
out('')
out('**跨区边缘阻断：** 只有配置在 `GATES` 的相邻地块可以跨区域通过。不同区域即使格子贴边但没有 Gate，视觉上也必须由断崖、岩浆河、城墙等阻隔。严禁直接用颜色格子的四邻接判断跨区域道路。')
out('')
out('### 区域清单')
out('| 区域ID | 中文名称 | 主支线 | 地块 | 普通地牢 | Castle / Boss | 前置征服 |')
out('|---|---|---|---:|---:|---|---|')
for z in regions:
 parents='、'.join(M['regions'][p-1]['castleName'] for p in z['parents']) if z['parents'] else '章节初始'
 boss=next(p['boss'] for p in by[z['id']] if p['type']=='Castle')
 out(f"| {z['id']} | **{z['name']}** | {'玩家批准支线' if z['optional'] else '主线'} | {z['tiles']} | {z['dungeonCount']} | {z['castleName']}（{boss}） | {parents} |")
out('')
out('### 唯一合法的跨区关口')
out('| Gate | From → To | 外观 | 开放条件 |')
out('|---|---|---|---|')
for gate in M['gates']:out(f"| `{gate['id']}` | {tuple(gate['a'])} → {tuple(gate['b'])} | {gate['kind']} | {gate['unlock']} |")
out('')
out('**解锁示意：** `T → 1 → 2 → 3 → 4`；`3 → 5` 是分支许可，但其物理入口位于已解锁的 4 区地块，通过 `G-04-05` 岔出。不能要求完成 4 区 Castle 才能进入 5 区，更不能要求支线 5 区先完成才能进终局 4 区。')
out('')
out('## 2. 原典地名与项目改造对应关系')
out('| D2 原典 | Chapter4 V0.1 改造 | 是否原典独立地牢 |')
out('|---|---|---|')
for row in [
 ('群魔堡垒 Pandemonium Fortress','安全城镇单地块，无战斗','原典城镇'),
 ('郊外大草原 Outer Steppes','区域1：3战斗地牢＋Castle，新增破魔岗哨','原典地表，无洞穴'),
 ('绝望平原 Plains of Despair','区域2：衣卒尔迁入区域 Castle 最终阶段','原典地表，Boss战改造'),
 ('神罚之城 City of the Damned','区域3：将恶魔城市拆解为战斗地牢及城塞','原典地表，无洞穴'),
 ('火焰之河 River of Flame','区域4前段：火河平台、渡桥、恶魔哨站等入口','原典地表，无洞穴'),
 ('混沌避难所 Chaos Sanctuary','区域4终局 Castle，五封印/三守卫/迪亚波罗分为连续6阶段','原典单片圣殿，非6层地牢'),
 ('地狱熔炉 Hellforge','区域5人工批准支线，海法斯特 Castle Boss','原典是火焰之河地表任务，非独立区域'),
 ]:out('| '+' | '.join(row)+' |')
out('')
out('**世界观沿用属于内部情怀原型设计**。将来独立商业发行前，对 Blizzard 的专有地名、角色和素材须评估授权与替代方案；不应在 Core DTO 使用只有原作才成立的特殊名称做条件判断。')
out('')
out('## 3. 全部 24 POI 的具体地点、等级、楼层')
out('')
out('每个难度栏为 `怪物挑战等级/推荐玩家等级`。楼层数是本游戏连续战斗阶段，与 D2 原典地下城楼层一一对应的情形在来源列中明确说明。为防止 Castle 自引用，只有外部 Dungeon 的 `RequiredForCastle=true`。')
out('')
for z in regions:
 out(f"### Region {z['id']:02d} · {z['name']} — {z['tiles']} 格，{z['dungeonCount']} Dungeon")
 out('| POI ID / 名称 / 坐标 | 类别 | 层数 | 普通 | 噩梦 | 地狱 | 来源 / Boss |')
 out('|---|---|---:|---:|---:|---:|---:|')
 for p in by[z['id']]:
  lev=' | '.join(f"{p['challenge'][d]}/{p['recommended'][d]}" for d in ('NORMAL','NIGHTMARE','HELL'))
  last=p['source']+(f"；Boss：{p['boss']}" if p['boss'] else '')
  out(f"| `{p['id']}` {p['name']} ({p['row']},{p['col']}) | {p['type']} | {p['floors']} | {lev} | {last} |")
 out('')
out('## 4. Floor 活跃格子、怪物密度与杀怪预算')
out('')
out('### 4.1 三种格子单位及建模定义')
out('')
out('- `WorldTile`：大地图7×7的一个格。只承载 POI、连接与地形，不自动生成战斗怪物。')
out('- `Floor Layout BoundingBox`：每层随机布局的行列边界 (`LayoutRows`×`LayoutCols`)，含障碍、深渊，不等于可战斗格。')
out('- `ActiveCell`：该 Floor 实际有效的逻辑格，拆成入口/走廊/战斗/精英/事件/出口/Boss 场；不是渲染像素，也不是角色寻路用的最小单位。')
out('- `BossArenaCell`：一处大 Boss 空间可能占 11 或 14 个有效格，但只配置 1 个 Boss 演员，不按格数生成 Boss。')
out('- `DungeonFloor` 或 `CastlePhase`：二者共享房间/遭遇预算基础数据，Castle 表现为连续阶段 `PHASE_SEQUENCE`；不强制意味着上下楼梯。')
out('')
out('### 4.2 精确预算公式')
out('')
out('```text')
out('ActiveCellTarget = EntryCellCount + CorridorCellCount + CombatCellCount')
out('                 + EliteCellCount + EventCellCount + ExitCellCount + BossArenaCellCount')
out('PlannedKillTarget = CombatCellCount * CombatMonstersPerCellTarget')
out('                  + EliteCellCount * EliteGroupMonsterTarget')
out('                  + FixedUniqueActorCount + BossActorCount + BossEscortTarget')
out('DungeonPlannedKills = Σ Floor.PlannedKillTarget')
out('ChapterFirstClearKills = Σ DungeonPlannedKills + Σ CastlePlannedKills')
out('```')
out('')
out('其中 `SealActivationCount` 是**封印事件次数**，不等同击杀数。混沌避难所 5 处封印合计 5 次，3 位封印守卫计入 `FixedUniqueActorCount`。避免将一个封印事件同时作为精英格和额外独特演员重复计数，未来实机需使 SpawnProfile 的规则与预算匹配。')
out('')
out('### 4.3 按区域区分的刷怪密度')
out('| 区域 | 普通战斗格每格：普通/噩梦/地狱 | 精英格每编队：普通/噩梦/地狱 | 类型特征 |')
out('|---|---|---|---|')
for rid,n,e,desc in [(1,3,4,'郊外地狱恶魔、较高怪群密度'),(2,2,5,'衣卒尔区域更多强力灵体和精英'),(3,2,5,'地狱城市守卫，危险单位占比高'),(4,2,5,'火焰之河与混沌避难所强调精英/固定守卫，而不是怪海'),(5,2,5,'地狱熔炉强调精英锻造守卫')]:
 out(f'| {rid} {regions[rid-1]["name"]} | {n}/{n+1}/{n+2} | {e}/{e+1}/{e+2} | {desc} |')
out('')
out('### 4.4 重点 Castle 的逐阶段面积（普通难度）')
out('')
for name in ['郊外破魔岗哨','衣卒尔·堕落之牢','神罚之城·城塞','混沌避难所','地狱熔炉·核心']:
 p=next(p for p in pois if p['name']==name)
 fs=sorted((f for f in F if f['PoiId']==p['id'] and f['DifficultyId']=='NORMAL'),key=lambda t:int(t['FloorIndex']))
 out(f'**{name}：{p["floors"]} 阶段**')
 out('')
 out('| 阶段 | 名称 | 有效格 | 战斗格 | 精英格 | Boss格 | 固定封印守卫 | 预计击杀 |')
 out('|---|---|---:|---:|---:|---:|---:|---:|')
 for f in fs:out(f"| {f['FloorIndex']} | {f['FloorDisplayName']} | {f['ActiveCellTarget']} | {f['CombatCellCount']} | {f['EliteCellCount']} | {f['BossArenaCellCount']} | {f['FixedUniqueActorCount']} | {f['PlannedKillTarget']} |")
 out('')
out('### 4.5 一次性首通怪物预算（不含重复挂机）')
out('| 难度 | 主线4区预计击杀 | 熔炉支线额外击杀 | 全章全清估算 |')
out('|---|---:|---:|---:|')
for d,n in [('NORMAL','普通'),('NIGHTMARE','噩梦'),('HELL','地狱')]:
 v=K['byDifficulty'][d];out(f"| {n} | {v['main']:,} | {v['side']:,} | {v['main']+v['side']:,} |")
out('')
out('本章虽然普通地牢数量少于第三章，但多层 Castle 与较大 BossArena 让它有足够的持续战斗时间。**静态计划击杀不等于玩家实机必杀数**：尚未扣除随机地图未访问房间、未清的可选遭遇，也不包含召唤、刷回、失败重试和行走时长。')
out('')
out('## 5. 混沌避难所特殊流程设计')
out('')
out('### 5.1 六阶段含义（明确为本游戏改造）')
out('| 阶段 | 表现 | 事件 / 怪物 | 必须完成条件 |')
out('|---|---|---|---|')
for r in [
 ('1','入口前厅','沿柱廊的密集护卫','完成阶段事件'),
 ('2','恶魔门廊','大型恶魔群与守门精英','完成阶段事件'),
 ('3','封印西翼','激活 2 个封印，击杀首位封印守卫','封印2/5'),
 ('4','封印东翼','激活 2 个封印，击杀第二位封印守卫','封印4/5'),
 ('5','封印中央','激活第 5 个封印，击杀第三位封印守卫','封印5/5、三个守卫已击败'),
 ('6','迪亚波罗王座','固定宽阔 BossArena，迪亚波罗出场','击败迪亚波罗，征服 Castle，结束第四章')
]:out('| '+' | '.join(r)+' |')
out('')
out('- 原典是同一片混沌避难所内的封印事件，而不是六层地下城。`StageMode=PHASE_SEQUENCE`，由自动小队按顺序推进。')
out('- Seal 激活与守卫击杀必须由 Runtime 真实事件满足才能进入最终 Boss 阶段。不能只凭“进入第6阶段”直接假设完成。')
out('- Boss 场地独立放大：终局阶段有效格数/长宽可扩大，多格形成空阔战场；对 3–5 角色小队应保留横向/纵向移动空间与火焰/闪电预警。')
out('- 固定怪物与动态击杀统计要分开：三个封印守卫为固定角色，不要再通过普通敌人池重复刷出。')
out('')
out('### 5.2 地狱熔炉支线事件')
out('')
out('`REGION_05` 在 Region3 Castle 被征服后变为可见状态，但不能自动加入候选。**仅玩家批准后才开放 Gate** `G-04-05` 与区域5。支线 Castle 地牢全部完成后也要获得新的玩家批准。')
out('')
out('最终事件顺序：击败海法斯特 → 获得/使用符文锤的任务事实 → 摧毁墨菲斯托的灵魂石 → 结算支线奖励（奖励种类、发放方式、次数待定）。此奖励不得参与 `ACT04_DIABLO_KILLED` 的必需前置。')
out('')
out('若从第三章延续灵魂石任务物件，需要显式建模跨章节任务事实，例如 `ACT03_SOULSTONE_OBTAINED`；若未实现剧情资源，也可以把需要的道具抽象为自动剧情事件，不能因物品栏缺少道具而使已授权支线死锁。')
out('')
out('## 6. 全自动目标选择与人工批准——优先级顺序')
out('')
out('```text')
out('候选 POI：')
out('  1) 本难度属于已解锁 Region；')
out('  2) WorldMap 按 Gate 有真实可达路径；')
out('  3) POI 在本难度尚未首次通关；')
out('  4) 若 Region.IsOptional，SideRegionApproval=true；')
out('  5) 若 POI.Type=Castle，必须 all RequiredForCastle Dungeon 已完成，且 CastleApproval=true。')
out('排序：ChallengeLevel ASC → IsOptional ASC（主线优先）→ AutoOrder ASC → PoiId ASC')
out('选中后：大地图寻路 → 进入该 Dungeon/Castle → 自动移动/索敌/战斗 → 按 Floor/Phase 顺序推进。')
out('无合法候选：停止自动首次攻略，显示待批准要塞 / 支线；可由后续挂机策略选择重刷，不擅自批准。')
out('```')
out('')
out('**手动批准的语义：** 许可是本难度该支线/该 Castle 的持续授权，不代表立即插队；每座 Castle 需要单独的授权，不得一次授权全章；授权状态存档并跨重启恢复；跨难度互不影响。')
out('')
out('**与 Act IV 地形的特殊关系：** 区域5是从已解锁的区域4入口分出，虽然它的 `UnlockParentCastleIds` 指向区域3 Castle，但并不要求区域4 Castle 完成。`Gate` 是移动边；`Region.ParentCastleIds` 是解锁条件，这两个概念不能混合。')
out('')
out('**任务事件与区域推进分离：** `衣卒尔` Boss / `封印5` / `迪亚波罗` 等事实由明确的战斗完成事件发放；不因为 UI 手动批准而直接产生成功事实。支线独立于主线，进下一章后仍可回到本章批准并完成地狱熔炉。')
out('')
out('## 7. Claude Code 的集成与工程边界')
out('')
out('- 请先 T0 **只读审计**当前 Godot 4.7 .NET、C#、`Game.Core`、`Game.Godot`、现有地牢生成/编队 AI/StageState/Save DTO / Content Loader；不得假设之前的文件 pack 已导入工程。')
out('- T1 配置 schema 映射：`ACT04_MAP_V0.1.json`、`ACT04_REGION_V0.1.csv`、`ACT04_POI_V0.1.csv`、`ACT04_GATES_V0.1.csv`、`ACT04_FLOOR_PLAN_V0.1.csv`、`ACT04_KILL_ESTIMATE_V0.1.json`。上述文件是**候选语义配置，非已存在的正式游戏 schema**；字段名请与当前工程已有定义映射。')
out('- T2 Core：实现或复用解锁、`SideRegionApproval`、`CastleApproval`、目标候选过滤、挑战等级稳定排序、可达性校验、跨章返回旧支线。')
out('- T3 Floor：补上可配置的 `PHASE_SEQUENCE`、固定封印守卫、封印事件计数与 BossArena 多格布置；无逻辑需求不要再加无关的新战斗系统。')
out('- T4 存档：必须按（Difficulty, Chapter, Region, Poi）持久化首通、已获准区域、已获准 Castle、封印/剧情事实；若现有存档只以章为键，先提出迁移方案。')
out('- T5 实机：先 L1 Core/Config 无头测试，再 L2 Godot 加载、场景进入和状态变化，最后 L3 人工视觉检查（熔岩峡谷 / Stone Bridge / 3–5 角色 Boss 场）。每项报告清楚通过/未测/阻塞。')
out('')
out('## 8. 必须通过的静态与实机验收')
out('')
checks=[
 '7×7 共49格：28 区域格 + 1 安全城镇 + 20不可通行，所有区域内部连通。',
 '24个 POI：19 Dungeon + 5 Castle，POI 坐标均属于自己的区域且互不重复。',
 '五条 Gate 唯一跨区域移动接口；无授权区域不可穿行，不存在直接四邻接越界。',
 '主线 1→2→3→4 在普通/噩梦/地狱都可完成；每座 Castle 若未获批都能确实暂停。',
 '仅手动批准地狱熔炉才允许进入 R5，且该审批不自动给 R5 Castle 授权。',
 '不批准 R5 仍能征服混沌避难所并通关 Act IV；通关后仍可回来批准并完成 R5。',
 'Castle 内部 Floor 不算自己的区域 `RequiredForCastle` Dungeon，不能形成自前置死锁。',
 '77 内容楼层×3难度=231条 Floor 记录，POI 层数、难度、挑战等级一致。',
 '每层格子类型之和等于 ActiveCellTarget，布局包围盒面积覆盖活跃格数。',
 '每层计划击杀预算可由 Combat/Elite/FixedUnique/Boss/Escort 数量精确复算。',
 '混沌避难所普通难度五个封印事实、三个封印守卫，只出现一个迪亚波罗 Boss。',
 '支线奖励结算不成为迪亚波罗主线的隐藏阻断条件。',
 '授权和通关状态分难度存档且允许返回旧章节支线，不要求玩家主动选择自动小队行走路线。',
 'Godot 实机 3–5 人自动小队在多个 Boss 场地不会互卡，封印阶段衔接自然。',
 '归档本章全部新增原创地牢名称为 PROPOSAL，不能误写成D2官方既有洞穴。'
]
for i,v in enumerate(checks,1):out(f'{i}. [ ] {v}')
out('')
out('## 9. 包内文件对照')
out('')
out('| 文件 | 作用 |')
out('|---|---|')
for x,y in [('data/ACT04_MAP_V0.1.json','地图二维网格、区域、Gate、POI、可选支线配置'),('data/ACT04_REGION_V0.1.csv','独立区域依赖、Castle、地牢数量'),('data/ACT04_POI_V0.1.csv','24个 POI、三难度等级、推荐玩家等级、楼层数'),('data/ACT04_GATES_V0.1.csv','5条跨区域/跨支线唯一通行边'),('data/ACT04_FLOOR_PLAN_V0.1.csv','231条 Floor 层级、有效格、密度和怪物预算'),('data/ACT04_KILL_ESTIMATE_V0.1.json','首通主线、支线击杀预算与逐POI汇总'),('Chapter4_Act4_Interactive_Map_V0.1.html','本地 HTML：地图、三难度、要塞批准、支线批准及模拟器'),('preview/Chapter4_Act4_Map_Grid_V0.1.png','7×7 地图渲染预览'),('docs/VALIDATION_REPORT_V0.1.md','静态/浏览器校验和实机未测声明')]:out(f'| `{x}` | {y} |')
out('')
out('**设计状态总结**：D2 气质通过原典地名、衣卒尔、地狱熔炉、火焰之河及迪亚波罗封印终局承载；新创地牢只是适配桌面全自动地牢战的“结构填充”，其数量、名字、怪量可在实机后进一步精简。')
(DOC/'Chapter4_Act4_Design_and_ClaudeCode_Spec_V0.1.md').write_text('\n'.join(L)+'\n',encoding='utf-8')
print('DOC',len(L),'lines',len('\n'.join(L)))
