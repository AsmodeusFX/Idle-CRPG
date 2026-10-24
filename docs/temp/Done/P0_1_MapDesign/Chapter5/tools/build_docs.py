from __future__ import annotations
from pathlib import Path
from collections import defaultdict
import json,csv
R=Path(__file__).resolve().parents[1]; D=R/'data';P=R/'docs';P.mkdir(exist_ok=True)
M=json.loads((D/'ACT05_MAP_V0.1.json').read_text(encoding='utf-8'))
K=json.loads((D/'ACT05_KILL_ESTIMATE_V0.1.json').read_text(encoding='utf-8'))
SIM=json.loads((D/'ACT05_SIMULATION_V0.1.json').read_text(encoding='utf-8'))
with (D/'ACT05_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as f:F=list(csv.DictReader(f))
REG=M['regions'];POIS=M['poi'];by=defaultdict(list)
for p in POIS:by[p['regionId']].append(p)
DIFS=[('NORMAL','普通'),('NIGHTMARE','噩梦'),('HELL','地狱')]

def table_headers(*xs):return ['| '+' | '.join(xs)+' |','|'+'|'.join(['---']*len(xs))+'|']
def f0(pid):return sorted((r for r in F if r['PoiId']==pid and r['DifficultyId']=='NORMAL'),key=lambda v:int(v['FloorIndex']))

doc=[];o=doc.append
o('# Chapter 5（Act V）哈洛加斯：地图、地牢、Castle 与自动冒险落地开发规范 V0.1')
o('')
o('> **交付状态：全套静态配置＋HTML原型已创建、验证；Godot 4.7 C# 工程尚未接入，本章各地牢数值和原创名称为待实机体验的候选方案。**')
o('> **不覆盖前四章**：Chapter 1 V0.5、Chapter 2 V0.1、Chapter 3 V0.1、Chapter 4 V0.1 为既有独立基线。五章共同规则按最新已确认用户约束执行，若旧文件存在冲突，以“主线自动地牢／Castle手动批准／支线手动批准／全局挑战等级优先”这些较新规则为准。')
o('> **原典核对**：[Blizzard Arreat Summit · Act V Maps](https://classic.battle.net/diablo2exp/maps/act5.shtml) 与 [Act V Quests](https://classic.battle.net/diablo2exp/quests/act5.shtml)。官方地名/首领是内部参考；大量普通战斗地牢为本项目适配，不代表原作存在这些独立洞穴。商业发布前需单独评估授权与专有名词。')
o('')
o('## 0. Chapter 5 定位与全局配置边界')
o('')
o('| 项目 | V0.1 方案 |');o('|---|---|')
for k,v in [
 ('章节名称','Chapter 5 / Act V · 哈洛加斯／亚瑞特山远征'),
 ('地图规模','11×11 世界地块 = 121 格；75 格攻略区域，1 格哈洛加斯主城，45 格 `x` 山脉/悬崖/不可通行'),
 ('区域','11 区：9 主线＋2 手动支线；各区 5–8 格，内部四邻接连通'),
 ('地牢与要塞','53 Dungeon 入口＋11 Castle＝64 POI；**Castle 内部楼层不算其自身清图前置**'),
 ('独立楼层',f'{len(F)//3} 个 DungeonFloor/CastlePhase；三难度共 {len(F)} 条楼层配置'),
 ('玩法','世界地图只寻路、解锁、浏览 POI；小队 3–5 人只在随机房间和走廊地牢内自动战斗'),
 ('攻略授权','所有 Castle 攻坚必须玩家逐个批准；支线 Region 解锁后需手动批准，支线 Castle 需要再次单独批准'),
 ('分支独立','冰河／尼拉塞克两条连续支线均非巴尔主线前置，进入其他章节后可回来补完'),
 ('自动优先级','已解锁／可达／获准／未首次通关候选：挑战等级 ASC → 主线优先 → AutoOrder ASC → POI ID ASC'),
 ('跨难度','同一地图与 POI 坐标共用；怪物等级、推荐玩家等级、密度按普通／噩梦／地狱配置；授权和首通按难度存档'),
 ('掉落','本版只提供源信息与击杀预算，不对宝箱掉率、Boss专属装备、支线任务最终奖励下结论'),
 ('长期修订','保留本版不覆写；统一体验反馈后以增量修订文档标注变更条目及受影响配置，再导出后续版本')]:o(f'| {k} | {v} |')
o('')
o('### 三难度整体工作基线（已批准的跨章数值曲线）')
o('| 难度 | 本章怪物等级 | 推荐玩家等级 |')
o('|---|---:|---:|')
for a,b,c in [('普通','32–43','31–44'),('噩梦','66–72','65–73'),('地狱','93–97','92–98')]:o(f'| {a} | {b} | {c} |')
o('')
o('注：该表是项目**调整后的挑战等级曲线**，不是说 D2 原版所有 Act V 区域的 mlvl 等于上述等级。例如原版普通难度血腥丘陵的区域等级低于 32；为保证我们已确认的逐章数值递增，这里有意使用本游戏等级体系。')
o('')
o('## 1. 世界地图 11×11（row,col 均从1开始）')
o('')
o('```text');o('     '+' '.join(f'{i:02d}' for i in range(1,12)))
for row,code in enumerate(M['gridRows'],1):o(f'{row:02d}   '+'  '.join(code))
o('```')
o('')
o('- `T`=哈洛加斯（9,2，仅1格）; `1..9`=九个主线区；`A`=支线10冰河；`B`=支线11尼拉塞克神殿；`x`=不可通行（雪山断崖/峡谷/地图边缘）。')
o('- **格子贴边不等于跨区可通行**：只有 `ACT05_GATES_V0.1.csv` 里存在显式 Gate 的边才可跨区域，其余贴边处需要使用雪壁、封闭门或地形断面阻挡。')
o('- 地图外形强调海拔与山路走势：哈洛加斯在西南低处，主线向东北翻越高原后转向东南、最终绕入世界之石大殿；冰河在水晶通道北侧，尼拉塞克神殿经哈洛加斯传送。')
o('- 所有 `x` 都是不允许寻路穿越的物理阻挡，不应该因为视觉遮罩不同而产生穿模捷径。')
o('')
o('### 各区域及解锁关系')
o('| ID | 区域名称 | 性质 | 世界地块 | 普通地牢数 | Castle 最终目标 | 解锁来源 |')
o('|---|---|---|---:|---:|---|---|')
for z in REG:
 par='、'.join(M['regions'][t-1]['castleName'] for t in z['parents']) if z['parents'] else '本章初始'
 boss=next(p['boss'] for p in by[z['id']] if p['type']=='Castle')
 o(f'| {z["id"]} | **{z["name"]}** | {"手动批准支线" if z["optional"] else "主线"} | {z["tiles"]} | {z["dungeonCount"]} | {z["castleName"]} / {boss} | {par} |')
o('')
o('**主线** `T → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9`。此为解锁依赖，不表示玩家要连续手动导航；小队自动选合法最低挑战目标。每一区结束时必须等待该 Castle 的手动攻坚许可，才允许继续解锁下一片区域。')
o('')
o('**支线**：`4 → 10（冰河）` 玩家第一份区域许可 → 10 Castle 获准并击败 Frozenstein / 营救安雅 → `11（尼拉塞克神殿）` 显示为待批准 → 玩家第二份区域许可并经哈洛加斯传送进入 → 11 Castle 再次获准 → 击败尼拉塞克。以上都不在 8/9 区 Castle 的前置条件里。')
o('')
o('### 11 条显式 Gate / Portal')
o('| GateId | from `(row,col)` | to `(row,col)` | 类型 | 开放条件 |')
o('|---|---|---|---|---|')
for g in M['gates']:o(f'| `{g["id"]}` | {tuple(g["a"])} | {tuple(g["b"])} | `{g["kind"]}` | `{g["unlockRequirements"]}` |')
o('')
o('**重要例外**：`G-T-11` 的起点在城镇 T，通往 11 区是传送门而非通常的山路。它在 10 区 Castle 完成并得到 11 区区域手动授权后才允许使用；离线测试应验证未授权时它不能被路径搜索利用。')
o('')
o('## 2. D2 原典记忆点映射与特殊战斗')
o('| D2 原典地点 / 内容 | 当前区域 / 地牢策略 | 改造边界 |')
o('|---|---|---|')
for x in [
 ('哈洛加斯 Harrogath','T 主城1格','固定城镇，不在大地图战斗'),
 ('血腥丘陵 / 督军山克','R1，山克成为Castle压轴Boss','原作位于地表，此处改造成多人小队自动攻坚'),
 ('冰冻高地 / 亚巴顿 Abaddon','R2，亚巴顿保留为地牢入口','其他战壕和营寨多为本项目原创'),
 ('亚瑞特高原 / 阿刻戎之坑','R3，保留阿刻戎之坑','特殊地狱洞穴融入本章内容'),
 ('水晶通道 / 冰河','R4主线及R10支线的分岔','R10可选且须玩家批准'),
 ('冰川之路 / 漂泊者之窟','R5，漂泊者之窟保留入口','其他洞窟模板可复用'),
 ('冰冻苔原 / 地狱魔窟','R6，保留经典附属洞穴','开阔雪地以内部多房间关卡表达'),
 ('远古之路 / 冰窖','R7，保留远古冰窖','向山巅收束、门洞更窄'),
 ('亚瑞特山巅三远古者','R8 Castle 末阶段固定科力克、马道克、塔力克','三个 Boss 同一阶段，不额外生成普通BossActor'),
 ('世界之石要塞 / 毁灭王座 / 世界之石大殿','R9 Castle 六阶段：要塞三层、王座入口、五波仆从、巴尔','一处 Castle 入口，不重复计算为独立前置地牢'),
 ('安雅营救 / 冰冻怪物 Frozenstein','R10 Castle：战胜守卫、自动营救安雅事实','连到R11的是剧情状态不是主线锁'),
 ('尼拉塞克神殿 / 悲痛、痛苦、瓦特之厅','R11：神殿、三层大厅与尼拉塞克最终阶段','地图上的远程传送源自哈洛加斯')]:o('| '+' | '.join(x)+' |')
o('')
o('## 3. 完整 POI 名录——53 Dungeon + 11 Castle（**唯一坐标**）')
o('')
for z in REG:
 rid=z['id'];entries=by[rid];o(f'### R{rid:02} {z["name"]} · {z["tiles"]} 世界格 · {z["dungeonCount"]} Dungeon + 1 Castle')
 o('');o('| POI ID | 类型 | 名称 | 坐标 | 楼层/阶段 | N 怪/推荐 | NM 怪/推荐 | H 怪/推荐 | 普通首通击杀估算 |')
 o('|---|---|---|---|---:|---|---|---|---:|')
 for p in entries:
  fmt=lambda diff:f'{p["challenge"][diff]}/{p["recommended"][diff]}'
  o(f'| `{p["id"]}` | {"Castle" if p["type"]=="Castle" else "Dungeon"} | {p["name"]} | ({p["row"]},{p["col"]}) | {p["floors"]} | {fmt("NORMAL")} | {fmt("NIGHTMARE")} | {fmt("HELL")} | {p["plannedKills"]["NORMAL"]} |')
 o('');o(f'Castle：**{z["castleName"]}**，对应区域所有标为 `RequiredForCastle=true` 的普通地牢首次完成后，变为“待玩家批准”；批准后才进入自动挑战队列。')
 o('')
o('## 4. Floor / Cell / Spawn 的面积与怪物预算')
o('')
o('### 4.1 数据单位与格式定义')
o('')
o('```text')
o('WorldTile                 大地图通行格：仅承载一处POI或安全空地，绝不直接刷怪')
o('Dungeon / Castle          大地图唯一入口，进入后触发一至多层真实随机DungeonFloor')
o('LayoutRows / LayoutCols   楼层逻辑包围盒（不等于实际有效房间/格数）')
o('ActiveCellTarget          实际有效格数量；由Entry+Corridor+Combat+Elite+Event+Exit+BossArena组成')
o('CombatCellCount           允许生成普通战斗编队的格子数')
o('EliteCellCount            精英编队格数；并非每格只出现1名敌人')
o('BossArenaCellCount        Boss场占用有效格总数；Boss演员数量需独立指定')
o('ScriptedWaveCount         只用于固定剧情波次，不能计为额外随机精英格')
o('ScriptedWaveMonsterTarget 固定波次产生的额外怪物总数，计入计划击杀')
o('FixedUniqueActorCount     固定独特角色，远古三人组以3人计，不再计入BossActorCount')
o('DungeonChallengeLevel     整座地牢参与自动冒险排序的挑战等级，不用第1层替代')
o('```')
o('')
o('**面板的物理格尺寸、房间边长、走廊宽度、路线曲折度**仍由 Godot 地牢生成器配置实现；表中格数是房间/遭遇层面的抽象逻辑单元，不是贴图像素或NavMesh网格。')
o('')
o('### 4.2 首次全清预算公式（防重计）')
o('')
o('```text')
o('ActiveCellTarget = EntryCellCount + CorridorCellCount + CombatCellCount')
o('                 + EliteCellCount + EventCellCount + ExitCellCount + BossArenaCellCount')
o('PlannedKillTarget = CombatCellCount × CombatMonstersPerCellTarget')
o('                  + EliteCellCount × EliteGroupMonsterTarget')
o('                  + FixedUniqueActorCount + BossActorCount + BossEscortTarget')
o('                  + ScriptedWaveMonsterTarget')
o('PoiPlannedKills = Σ (its Floor.PlannedKillTarget)')
o('MainFirstClearKills = Σ (all mandatory Dungeon and Castle of R1..R9)')
o('SideFirstClearKills = Σ (R10 and R11)')
o('```')
o('')
o('`ScriptedWaveMonsterTarget` 已包含五波仆从实际生成的演员，不能再次从CombatCell中抽取同一组；三远古者用固定UniqueActor计，不再额外计BossActor。`BossArenaCellCount=17` 仍然代表**一个场地空间**，不会生成17个巴尔。')
o('')
o('### 4.3 每个区域的基础刷怪密度')
o('| 区域 | 普通格 × 每格怪：N/NM/H | 精英格 × 每组怪：N/NM/H | 体验取向 |')
o('|---|---|---|---|')
styles={1:(3,4,'雪山战场怪群'),2:(3,4,'战壕与魔军'),3:(3,4,'高原魔军'),4:(2,5,'水晶洞高危险精英'),5:(2,5,'冰道和精英混合'),6:(3,4,'冻土开阔怪群'),7:(2,5,'远古洞穴精英'),8:(2,5,'少量高难敌人与固定三守护者'),9:(2,5,'世界之石精英与固定波次'),10:(2,5,'冰河寒霜精英'),11:(2,5,'神殿危险精英/尸体爆炸控制')}
for rid,(a,b,desc) in styles.items():o(f'| {rid}. {REG[rid-1]["name"]} | {a}/{a+1}/{a+2} | {b}/{b+1}/{b+2} | {desc} |')
o('')
o('### 4.4 四座代表性 Castle 的逐层规模（普通）')
o('')
for poi_id in ['W01-C01','W08-C01','W09-C01','W10-C01','W11-C01']:
 p=next(x for x in POIS if x['id']==poi_id);o(f'**{p["name"]}（{p["floors"]} 阶段）**');o('')
 o('| 阶段 | 名称 | 布局包围盒 | 有效格 | 普通格×怪/格 | 精英格×怪/组 | Boss格 | 固定独特 | 固定波次怪 | 预计击杀 |')
 o('|---|---|---|---:|---|---|---:|---:|---:|---:|')
 for row in f0(poi_id):o(f'| {row["FloorIndex"]} | {row["FloorDisplayName"]} | {row["LayoutRows"]}×{row["LayoutCols"]} | {row["ActiveCellTarget"]} | {row["CombatCellCount"]}×{row["CombatMonstersPerCellTarget"]} | {row["EliteCellCount"]}×{row["EliteGroupMonsterTarget"]} | {row["BossArenaCellCount"]} | {row["FixedUniqueActorCount"]} | {row["ScriptedWaveMonsterTarget"]} | {row["PlannedKillTarget"]} |')
 o('')
o('### 4.5 三难度计划击杀合计（静态模型）')
o('| 难度 | 主线首次全清 | 可选两支线合计 | 全章所有 POI 首通 |')
o('|---|---:|---:|---:|')
for d,name in DIFS:
 k=K['byDifficulty'][d];o(f'| {name} | {k["main"]:,} | {k["side"]:,} | {k["main"]+k["side"]:,} |')
o('')
o('风险提示：第五章的主线击杀预算明显高于第四章，**这只是为了提供第一轮“地牢数×格数×怪/格”的数值模型**。未来应加入每只怪平均战斗时间、寻路速度、区域切换等待、Castle手动批准等待时间、失败回刷等参数，按预期章节首通时长反推地牢密度。')
o('')
o('## 5. 关键任务事件与终局阶段（不新增手动移动操作）')
o('')
o('### 5.1 远古三守护者（W08-C01）')
o('')
o('- Castle 第4阶段固定空旷山顶竞技场（本版 BossArena 16有效格），同时出现科力克、马道克、塔力克；三者全部被击败后产生 `ACT05_ANCIENTS_DEFEATED`，作为该 Castle 攻克事实。')
o('- 不为三人再额外刷普通 BossActor；不做“一个死了就自动通关”或“只剩两个时房间结束”的错误逻辑。')
o('- 原版 D2 的远古者存在离场重置等规则；本游戏改造成连续自动挑战，失败处理复用后续统一的 `AutoExpeditionFailurePolicy`，**不会假定已经批准的 Castle 需要二次批准**。')
o('')
o('### 5.2 世界之石终局（W09-C01）')
o('| 阶段 | 设定 | 通过条件 |')
o('|---|---|---|')
for a,b,c in [('1','世界之石要塞 1','完成固定遭遇及出口'),('2','世界之石要塞 2','完成固定遭遇及出口'),('3','世界之石要塞 3','完成固定遭遇及出口'),('4','毁灭王座入口','清理王座前厅'),('5','毁灭王座·五波巴尔仆从','五波顺序触发且每波击杀完成，产生 `ACT05_THRONE_FIVE_WAVES_CLEARED`'),('6','世界之石大殿·巴尔','独立大Boss场击败巴尔，产生 `ACT05_BAAL_DEFEATED` 并结束本难度Act V')]:o(f'| {a} | {b} | {c} |')
o('')
o('- D2 原典确有三层世界之石要塞、毁灭王座与世界之石大殿；本版将毁灭王座划分成入口与五波仆从两个连续阶段是为了桌面挂机表现。')
o('- 第5阶段五波怪物数量示例为普通25、噩梦30、地狱35；这部分属于 `ScriptedWaveMonsterTarget` 固定额外预算。后续应进一步给五位波次首领分别绑定 MonsterId，而不是简化为五组完全相同杂兵。')
o('- 由自动小队依序推进，但世界之石 Castle 必须先经玩家单独批准。')
o('')
o('### 5.3 冰河→尼拉塞克连续支线')
o('')
o('- R4 Castle 完成 → R10 冰河区域解锁且显示“待手动批准”。只有批准R10，小队才可能进入冰河地牢。')
o('- R10 四个普通地牢清完，Castle 显示待批准；玩家另行批准后，挑战冰冻怪物并完成安雅营救，触发 `ACT05_ANYA_RESCUED`。')
o('- 只有 R10 Castle 通关才解锁 R11 尼拉塞克神殿，但不自动进入；玩家批准 R11 后，使用哈洛加斯至神殿的单独Portal。')
o('- R11 完成普通地牢后再次单独批准 Castle；终局进入瓦特之厅击败尼拉塞克。可以在击败巴尔、进入其他章节或难度后回来完成（按难度记录进度）。')
o('- 如未来要增加“救安雅奖励”“尼拉塞克任务奖励”，使用 QuestReward 配置补充，不要悄悄把它们加入巴尔 Castle 的必须前置。')
o('')
o('## 6. 自动攻略选择器、许可与失败策略接口')
o('')
o('```text')
o('For each candidate POI in current Difficulty/Chapter:')
o('  Region parents have been conquered in this Difficulty')
o('  AND WorldMap explicit Gate path is physically reachable')
o('  AND POI first-clear not yet completed')
o('  AND (Region.IsOptional == false OR SideApproval[Difficulty,Region] == true)')
o('  AND (POI.Type == Dungeon OR')
o('       (All RequiredForCastle dungeons in same Region done')
o('        AND CastleApproval[Difficulty,Poi] == true))')
o('Sort by (PoiChallengeLevel ASC, IsOptional ASC, AutoOrder ASC, PoiId ASC).')
o('Execute: walk world tile -> enter Dungeon/Castle -> auto path-find, acquire targets, fight and progress floors.')
o('If zero candidates: wait for authorization or hand over to separately designed farming policy.')
o('```')
o('')
o('**特别注意**：等待Castle批准是刻意的玩家决策点。批准只获得挑战资格，绝不代替实际通关，也不直接授权下一座Castle或其他支线。持久授权在保存/读取之后仍生效；失败后如何自动回刷、多久重试为后续待定规则，本章不能自行定义数值。')
o('')
o('## 7. Claude Code 接入顺序（候选数据≠现成正式实现）')
o('')
for item in [
 'T0 **只读审计**当前 Godot 4.7 stable .NET C# 工程：`Game.Core`/`Game.Godot`/存档/StageState/关卡数据导入接口/随机地牢生成器，确认是否已经集成前四章；不要假定用户手里的Claude工程与本包同构。',
 'T1 将 `ACT05_MAP`、`ACT05_REGION`、`ACT05_POI`、`ACT05_GATES`、`ACT05_FLOOR_PLAN` 映射进工程实际数据模型，保留稳定PoiId，加载时验证坐标、区域归属、Castle依赖、楼层数量。',
 'T2 `ExpeditionPlanner`：授权过滤→关口寻路→挑战等级全局排序；`SideApproval`与`CastleApproval`必须分开，必须支持旧章支线补完，所有授权按难度存档。',
 'T3 DungeonGeneration：按 `ActiveCellTarget`、格子类型和 SpawnProfile 生成；BossArena占多格只生成指定演员；脚本五波怪及三远古者使用固定剧情序列，不被随机精英池重复发出。',
 'T4 逐层计数：将实际击杀KillFacts与 `PlannedKillTarget` 建模值区分；第五章首通、失败、重复刷怪要分别统计，数据不回滚已合法Commit的奖励。',
 'T5 UI：大地图区域锁定、已批准、Castle待批准、支线待批准、传送口状态、已征服标记；单窗口 Mini/Management 切换不能重置当前探索状态。',
 'T6 测试报告按 L1 Core/配置静态，L2 Godot实机，L3 人工体验分开呈现，绝不把HTML模拟误写成Godot已实现。'
]:o('- '+item)
o('')
o('## 8. 验收清单')
o('')
for i,x in enumerate([
 '11×11 共121格：75 区域格＋1主城格＋45 x；11区各5–8格且内部四邻接连通。',
 '53 Dungeon＋11 Castle＝64 POI，无重复坐标，不存在 POI 落在 x 或错区。',
 f'{len(F)//3} 个内容楼层 × 3难度 = {len(F)} 条 Floor 记录；所有 Floor 的布局面积≥有效格。',
 '每层有效格分解完全相等，所有计划击杀可由对应编队密度与固定怪事实精确复算。',
 '仅11条Gate可以跨区通行，非Gate贴边位置实际渲染有雪壁/断崖，寻路不可跳越。',
 '普通、噩梦、地狱三难度全部会先清完血腥丘陵 Dungeon，然后确实停在督军山克Castle等待玩家批准。',
 '逐Castle批准，三难度都能不批准任何支线到达巴尔并通关第五章。',
 'R10未批准前不可进入；R10批准不等于该支线Castle批准。',
 'R10 Castle未完成之前R11应保持未解锁；之后需要R11单独区域批准以及R11 Castle另一次批准。',
 '主线通关后可返回R10、R11补完；三难度审批和进度完全隔离。',
 '亚瑞特山巅三个远古守卫必须都被击败，一次Castle失败不会错误发放 `ACT05_ANCIENTS_DEFEATED`。',
 '世界之石Castle 6阶段、3层要塞+王座入口+王座五波+大殿巴尔；五波固定数量正确计入预算，不能重复刷巴尔。',
 '支线任务奖励即使未实现，也不会伪造奖励或阻塞主线。',
 'Godot L2 生成的随机地牢可让3–5角色和怪群寻路，山巅/巴尔场地尺寸视觉上合理（尚未测试）。',
 '以上数据导入游戏之前为 `PROPOSAL_UNBALANCED`，后续按实机记录出增量修正文档，不修改本版文件。'
],1):o(f'{i}. [ ] {x}')
o('')
o('## 9. 文件结构与导入说明')
o('')
o('| 文件 | 用途 |');o('|---|---|')
for a,b in [('Chapter5_Act5_Interactive_Map_V0.1.html','交互本地地图，三难度切换、Castle/支线批准、自动完成演示'),('data/ACT05_MAP_V0.1.json','唯一地图坐标权威 + Region + POI + Gate + policy'),('data/ACT05_REGION_V0.1.csv','区域主支线及Castle前置'),('data/ACT05_POI_V0.1.csv','64POI位置、名字、等级、楼层和预算'),('data/ACT05_GATES_V0.1.csv','11条跨区入口、山路与安雅传送门'),('data/ACT05_FLOOR_PLAN_V0.1.csv',f'{len(F)}条三难度楼层规模与怪物预算'),('data/ACT05_KILL_ESTIMATE_V0.1.json','主线/支线首通总击杀估算'),('data/ACT05_SIMULATION_V0.1.json','静态批准/自动推进顺序备份'),('docs/Chapter5_Design_Decision_Log_V0.1.md','本版设计取舍、未验证假设与统一体验反馈索引'),('docs/VALIDATION_REPORT_V0.1.md','配置及HTML测试结果、未测事项'),('tools/generate_act5.py','可复现候选内容配置'),('tools/build_html.py','生成交互HTML'),('tools/test_browser.py','Chromium交互模拟测试')]:o(f'| `{a}` | {b} |')
o('')
o('**不建议一次性将本包的全部新增地牢当作必须单独制作的独特场景**。大量原创洞穴是同一主题房间池的配置实例，核心异质内容集中在远古者、冰河安雅、尼拉塞克与巴尔王座。')
(P/'Chapter5_Act5_Design_and_ClaudeCode_Spec_V0.1.md').write_text('\n'.join(doc)+'\n',encoding='utf-8')

memo=[];m=memo.append
m('# Chapter 5 / 五章节首轮版本：设计决策备忘录 V0.1')
m('')
m('> **用途：用户明确要求先完成 Chapter1–5 的完整初版体验，再集中反馈。本文提供“思维备份”，方便未来仅出修正文档。它记录可外显的设计依据、版本差异与待验证假设，而非 Claude Code 内部工程状态的臆测。**')
m('')
m('## A. 已确认、不应在单章临时推翻的跨章框架')
for x in [
 '五章尺寸：Act1 8×8 / Act2 9×9 / Act3 10×10 / Act4 7×7 / Act5 11×11。第四章短而高强度，第五章为最大规模终局。',
 '同一章地图跨普通、噩梦、地狱三个难度复用，怪物挑战等级使用已确认的平滑数值曲线，而不是1:1复刻D2原版区域mlvl。',
 '大地图仅承载行走和解锁，**绝不大地图战斗**；地牢内由3–5角色小队自动移动、索敌、战斗。',
 '区域自身所有 RequiredForCastle 普通地牢首次通关后，Castle变为待批准；**每座Castle必须玩家手动批准**，批准不自动通关也不自动批准下一座。',
 '支线解锁仅显示待批准，需要玩家主动批准才能进入支线；支线Castle需要再批准。支线永不成为后续主线或章节的必须前置；主线推进后可返回旧章节完成支线。',
 '自动攻略仅在有资格候选里按 ChallengeLevel ASC → MainFirst → AutoOrder ASC → POI ID排序。',
 'Dungeon 的每层楼面面积、怪物密度、BossArena采用可复算预算；世界地图Tile与Floor Cell彼此独立。',
 '失败后回刷/CD/重试条件、Boss独特奖励池、任务奖励均未锁定，不能在具体章节生成时擅自推定。'
]:m('- '+x)
m('')
m('## B. Chapter 5 本版重大选择，为什么这样做')
m('')
m('| 编号 | 本版选择 | 目的 / D2 情怀对齐 | 后续检验点 |')
m('|---|---|---|---|')
for row in [
 ('D05-01','11×11中75区格+1主城+45不可通行','保持大型雪山远征感，同时每个区域5–8格、64POI高密度','地图是否显得太满？是否需要扩大空白山脉或减少伪关卡？'),
 ('D05-02','9主线区按血腥丘陵→冰冻高地→亚瑞特高原→水晶通道→冰川之路→冰冻苔原→远古之路→山巅→世界之石','以原典地点顺序为情怀轴线','实际游玩是否过长？中段地区是否同质化？'),
 ('D05-03','2个连续支线：冰河→尼拉塞克神殿','忠于营救安雅、安雅传送门与尼拉塞克任务串联','是否适合玩家完成主线之后回旧章？奖励动机是否足够？'),
 ('D05-04','R10由水晶通道北侧岔出、R11由哈洛加斯传送进入','让地图视觉既有实体岔路又有D2式特殊传送','传送入口从主城直达远处支线是否易理解？'),
 ('D05-05','R8 Castle四阶段，末场三远古者固定3演员','三远古者是经典体验，不应弱化为普通精英','AI和场地尺寸能否支撑三Boss同时战斗？'),
 ('D05-06','R9 Castle六阶段：要塞三层→王座入口→五波仆从→巴尔','尽量保留原典终局的连续高潮；不把城塞内部场景当做自己前置入口','Boss前连续战斗时长、补给/HP续航是否过分压迫？'),
 ('D05-07','53普通Dungeon和11 Castle，全章64POI','和前四章使用一致的“地块-入口-要塞”比率，避免大地图走路稀疏','预计通关时长很长，是否需砍普通地牢数？'),
 ('D05-08','普通怪格密度2–3，精英格4–5，噩梦与地狱每档+1/+2','对齐之前的格子×密度击杀量建模','是否产生严重的角色拥挤、战斗排队或低效寻路？'),
 ('D05-09','第五章N/NM/H分别32–43、66–72、93–97','遵循已确认的15章战斗等级，而不是机械复制D2原始AreaLevel','最终97级怪时推荐98，经验升级曲线及掉落要另做模型'),
 ('D05-10','普通地牢名分原典直接借鉴、原典地表改造、原创占位三种','避免把原创洞窟误称D2原典，保留可复用模板','后续需要统一命名风格，并处理商业发行名称授权'),
 ('D05-11','R11支线从R10城塞完成后解锁，但主线R8/R9从不引用R10/11','保证支线手动决策与主线独立','检查不同章节/难度存档下的回访与审批语义')
]:m('| '+' | '.join(row)+' |')
m('')
m('## C. 本轮未批准、不能被Claude Code误当最终数值的内容')
m('')
for x in [
 '所有 POI 名称为本版候选，其中 `ORIGINAL_PLACEHOLDER` 必须保留来源标签；怪物基础属性、伤害倍率、技能组、Boss具体机制完全未定。',
 '所有 Floor 的ActiveCell、普通格怪/格、精英格怪/组及预计击杀只用于模型起点，没有通过3–5角色真实移动/战斗/碰撞压力测试。',
 '世界之石固定波次人数（普通每波5、噩梦6、地狱7）仅为预算，真实波次首领、站位与召唤节奏待设计。',
 '地下洞窟需要不同几何拓扑和Boss竞技场布局，不能仅把ActiveCell视为Godot 2D TileMap真实地格；要先做生成器形态验证。',
 '支线任务奖励、专属掉落、箱子种类、难度失败后的回刷策略尚未给出，不能默默新增承诺。',
 '截至本版，测试范围是离线配置、数据模拟、浏览器交互；Godot场景与战斗不在当前交付环境内。'
]:m('- '+x)
m('')
m('## D. 实机统一体验后应收集的反馈维度')
m('')
for k,desc in [
 ('MAP','区域外形/颜色/空白地块比例、POI密度、主城与Gate逻辑、地图视觉路线'),
 ('PACING','每一区首次通关时间、地牢间行走耗时、Castle手动批准频率、章节整体通关时长'),
 ('DUNGEON','房间与走廊数量、单层格子面积、随机生成一致性、3–5人索敌卡位'),
 ('COMBAT','实际击杀、每秒击杀、精英数量、三远古Boss拥挤、巴尔五波压力'),
 ('BRANCH','支线授权发现率、回访旧章是否顺畅、连续冰河-尼拉塞克触发是否理解'),
 ('REWARD','Boss/任务独特奖励意愿、箱子过满、掉落质量与成本'),
 ('UI','Castle批准反馈、难度切换、Gate状态、支线与主线区分度'),
 ('SCOPE','重复场景过多、原创占位内容是否需要削减/合并/换成D2原典')
]:m(f'- `{k}`：{desc}')
m('')
m('## E. 后续仅输出修正文档的增量更新约定')
m('')
m('```text')
m('ChangeId: 例如 ACT05-PACING-003')
m('SourceBaseline: ACT05_V0.1_PROPOSAL')
m('Reason: 实机体验反馈，可附截图/录像/监测数据')
m('Affected: RegionIds, PoiIds, GateIds, FloorIndex, DifficultyIds')
m('Before: 原字段/旧值（从这版CSV/JSON读取，不凭记忆）')
m('After: 拟修改字段/新值')
m('Migration: 对已有通关、授权、正在挑战的存档处理')
m('Validation: 静态计数、授权/主线通关模拟、Godot实机表现')
m('Status: PROPOSED / APPROVED / IMPLEMENTED / VERIFIED')
m('```')
m('')
m('严禁直接在本版 V0.1 上无记录覆写。修订可输出 `ACT05_V0.1_to_V0.2_PATCH.md`（附差异CSV/JSON或补丁脚本），既能保持思维/设计演变历史，也减少重复生成四五章全量文档。')
m('')
m('## F. 跨章节备忘总览（供集中评审）')
m('')
m('| Act | 当前候选/基线 | 地图形态 | 手动支线记忆点 | 主要终局 |')
m('|---|---|---|---|---|')
for row in [('I','8×8 · 已有 V0.5','边境荒地→森林/高地→修道院','埋骨之地/血乌','大教堂→地下墓穴/安达利尔'),('II','9×9 · V0.1','西部鲁·高因→两线任务→七座古墓','下水道/古代通道','都瑞尔'),('III','10×10 · V0.1','丛林曲折→库拉斯城区','庞大湿地/古神庙群','崔凡克→憎恨囚牢/墨菲斯托'),('IV','7×7 · V0.1','深渊熔岩桥→混沌','地狱熔炉','五封印/迪亚波罗'),('V','11×11 · V0.1','雪山蜿蜒远征→山巅→世界之石','冰河救安雅→尼拉塞克神殿','远古三人组＋五波仆从/巴尔')]:m('| '+' | '.join(row)+' |')
m('')
m('**下一步不应直接扩展第六章，也不应重新定义BD**。用户计划先将五章完整实现体验，统一提出反馈，然后只交付有清晰Before/After与迁移测试的修正文档。')
(P/'Chapter5_Design_Decision_Log_V0.1.md').write_text('\n'.join(memo)+'\n',encoding='utf-8')

report=[];q=report.append
q('# Chapter 5 V0.1 离线校验与未验证清单')
q('')
for s in [
 '地图11×11共121格；75区域格+1城镇+45x；11区域5–8格均四邻接连通：PASS（generator assert）。',
 '64POI=53 Dungeon+11 Castle；每个POI坐标归属合法、无重复：PASS。',
 f'独立楼层{len(F)//3}×三难度={len(F)}条配置；分区挑战等级在32–43 / 66–72 / 93–97范围内：PASS。',
 'DungeonFloor有效格分项相等、布局包围盒面积≥有效格、怪物计划击杀预算由公式复算：PASS。',
 '世界之石要塞五个固定波次，远古三人组作为3固定Unique演员、不重复加BossActor：PASS。',
 '显式11条跨区Gate均对应两个真正四邻接世界Tile，含两条支线Gate：PASS。',
 '三难度主线可以在逐要塞手动批准后击败巴尔，且未批准支线不会被自动进入：PASS（静态模拟）。',
 '巴尔通关后仍能授权冰河、营救安雅，再单独授权尼拉塞克神殿；无主线依赖反向锁死：PASS（静态模拟）。',
 '浏览器交互检查：121 tile、三难度独立、手动批准每座Castle、两个独立支线批准、64目标完成、无JS异常：PASS。',
 'Godot L1 Core测试：未执行（没有导入用户当前工程）。',
 'Godot L2场景/寻路/战斗实机：未执行。',
 'L3人工视觉与玩家体验评估：未执行。',
 '本包的地图名字与未公开原典版权未完成发行合规检查，商业化前需另行处理。'
]:q('- '+s)
q('')
q('## 预算汇总')
q('| 难度 | 主线 | 支线 | 总计 |');q('|---|---:|---:|---:|')
for d,name in DIFS:
 k=K['byDifficulty'][d];q(f'| {name} | {k["main"]:,} | {k["side"]:,} | {k["main"]+k["side"]:,} |')
q('')
q('以上为首次清理全部规划遭遇的预算，不代表玩家游戏中的最终击杀数；地图随机生成、房间绕过、重刷、失败复战都会改变实际值。')
(P/'VALIDATION_REPORT_V0.1.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
readme=f'''# Chapter 5 / Act V — Claude Code Pack V0.1\n\n**交付**：地图静态配置、交互HTML、完整Markdown规范、三难度楼层怪物预算、离线验证、思维备份。**不是已实现Godot游戏功能。**\n\n## 推荐打开\n\n- `Chapter5_Act5_Interactive_Map_V0.1.html`：双击用 Chrome/Edge 打开，点击区域/地牢，尝试「自动推进」「批准Castle」「批准支线」。\n- `docs/Chapter5_Act5_Design_and_ClaudeCode_Spec_V0.1.md`：具体技术配置/任务清单。\n- `docs/Chapter5_Design_Decision_Log_V0.1.md`：设计依据与待统一体验反馈项。\n- `docs/VALIDATION_REPORT_V0.1.md`：静态测试证明及实机未测试说明。\n\n## 快速统计\n\n11×11 世界地图、11区域(9主线2支线)、53 Dungeon、11 Castle、{len(F)//3}独立楼层、{len(F)}条三难度楼层配置。\n\n## 导入注意事项\n\n1. 当前脚本使用内部候选字段命名，Claude Code 应先只读审计 Godot 实际 schema，再做转换，不要直接覆写正式数据。\n2. 测试按 L1/L2/L3 分层如实报告，不得将HTML浏览器模拟等同于Godot实机通过。\n3. 本轮先体验五章，之后仅用差异修正文档修改；不要默默改写 V0.1 基线。\n\n## 重新生成与测试\n\n```bash\npython tools/generate_act5.py\npython tools/build_html.py\npython tools/test_browser.py\npython tools/build_docs.py\n```\n\n脚本里的容器绝对路径是此交付环境的示例；交给 Claude Code 时应改为基于包根目录的相对路径。\n'''
(R/'README.md').write_text(readme,encoding='utf-8')
print('DOCS CREATED',len(doc),'spec lines',len(memo),'memo lines',len(report),'validation lines')
