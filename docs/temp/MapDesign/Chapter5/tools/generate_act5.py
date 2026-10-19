from __future__ import annotations
from pathlib import Path
from collections import Counter,defaultdict,deque
import csv,json,math
ROOT=Path(__file__).resolve().parents[1];D=ROOT/'data';D.mkdir(exist_ok=True)
DIFS=('NORMAL','NIGHTMARE','HELL')
# Row/Col are 1-indexed. World map tile != DungeonFloor active cell.
TOWN=(9,2)
TILES={
1:[(8,3),(8,4),(9,3),(9,4),(9,5),(10,3),(10,4)],
2:[(6,4),(6,5),(7,4),(7,5),(7,6),(8,5),(8,6),(8,7)],
3:[(4,4),(4,5),(5,4),(5,5),(5,6),(6,6),(6,7),(7,7)],
4:[(3,6),(3,7),(3,8),(4,6),(4,7),(4,8),(5,7),(5,8)],
5:[(3,9),(3,10),(4,9),(4,10),(4,11),(5,9),(5,10)],
6:[(5,11),(6,9),(6,10),(6,11),(7,9),(7,10),(7,11)],
7:[(8,9),(8,10),(8,11),(9,9),(9,10),(9,11),(10,10)],
8:[(9,7),(9,8),(10,7),(10,8),(10,9)],
9:[(10,6),(10,11),(11,6),(11,7),(11,8),(11,9),(11,10),(11,11)],
10:[(1,6),(1,7),(2,6),(2,7),(2,8)],
11:[(9,1),(10,1),(10,2),(11,1),(11,2)],
}
REGIONS=[
(1,'血腥丘陵',False,[],'督军山克营寨','从哈洛加斯出城，连续燃烧坡道、战场断垣与恶魔营寨'),
(2,'冰冻高地',False,[1],'冰冻高地·恶魔指挥所','战壕冰坡与散落军营；邪恶督军艾德瑞奇等经典名字'),
(3,'亚瑞特高原',False,[2],'亚瑞特高原·破军壁垒','起伏高原、恶魔远征阵地和雪山断崖'),
(4,'水晶通道',False,[3],'水晶通道·晶核圣所','冰晶洞穴，分岔可通冰河支线'),
(5,'冰川之路',False,[4],'冰川之路·冰冢堡垒','狭长冰川迷宫与岩壁冰窖'),
(6,'冰冻苔原',False,[5],'冰冻苔原·霜卫城寨','冻土开阔风场与冰原前哨'),
(7,'远古之路',False,[6],'远古之路·祭坛关隘','岩壁/冰洞交替，指向山巅狭窄古道'),
(8,'亚瑞特山巅',False,[7],'亚瑞特山巅·远古试炼','三位远古守护者为城塞固定终局事件，不能随机刷成普通怪'),
(9,'世界之石要塞',False,[8],'世界之石大殿','三层世界之石要塞、毁灭王座五波仆从、世界之石大殿巴尔'),
(10,'冰河',True,[4],'冰河·安雅囚笼','水晶通道分岔，可选营救安雅；玩家手动批准'),
(11,'尼拉塞克神殿',True,[10],'瓦特之厅·尼拉塞克','需冰河营救线完成才开放哈洛加斯传送，独立第二次支线批准'),
]
Z={r:{'id':r,'name':name,'optional':opt,'parents':parents,'castleName':castle,'description':desc,'tiles':len(TILES[r])} for r,name,opt,parents,castle,desc in REGIONS}
GRID=[['x']*11 for _ in range(11)]
GRID[TOWN[0]-1][TOWN[1]-1]='T'
for rid,locs in TILES.items():
 for row,col in locs:
  assert 1<=row<=11 and 1<=col<=11
  assert GRID[row-1][col-1]=='x',('overlap',rid,row,col)
  GRID[row-1][col-1]=str(rid) if rid<10 else ('A' if rid==10 else 'B')
GRID=[''.join(row) for row in GRID]
assert sum(len(x) for x in TILES.values())==75 and ''.join(GRID).count('x')==45
for rid,locs in TILES.items():
 assert 3<=len(locs)<=8,(rid,len(locs))
 seen={locs[0]};todo=list(seen);tile=set(locs)
 while todo:
  row,col=todo.pop()
  for v in [(row+1,col),(row-1,col),(row,col+1),(row,col-1)]:
   if v in tile and v not in seen:seen.add(v);todo.append(v)
 assert seen==tile,(rid,sorted(tile-seen))
# (to Region, coords, gate kind, note); Gate controls cross-region walk. Other colored shared borders are cliffs/walls.
GATES=[
('G-T-01',0,1,(9,2),(9,3),'TOWN_ROAD','哈洛加斯城门→血腥丘陵'),
('G-01-02',1,2,(8,4),(8,5),'MOUNTAIN_PASS','血腥丘陵山道→冰冻高地'),
('G-02-03',2,3,(6,5),(5,5),'ICE_BRIDGE','冰冻高地雪桥→亚瑞特高原'),
('G-03-04',3,4,(5,6),(5,7),'CRYSTAL_CLEFT','亚瑞特高原断崖→水晶通道'),
('G-04-05',4,5,(3,8),(3,9),'GLACIAL_PASS','水晶通道→冰川之路'),
('G-05-06',5,6,(5,10),(6,10),'WIND_GAP','冰川之路峡谷→冰冻苔原'),
('G-06-07',6,7,(7,10),(8,10),'FROST_BRIDGE','冻土隘口→远古之路'),
('G-07-08',7,8,(9,9),(9,8),'ANCIENT_PASS','远古之路→亚瑞特山巅'),
('G-08-09',8,9,(10,7),(11,7),'WORLDSTONE_GATE','亚瑞特山巅→世界之石要塞'),
('G-04-10',4,10,(3,7),(2,7),'SIDE_FROZEN_RIVER','水晶通道→冰河；需玩家批准'),
('G-T-11',0,11,(9,2),(9,1),'SIDE_ANYA_PORTAL','哈洛加斯安雅传送门→尼拉塞克神殿；需冰河 Castle 已通关且再次批准'),
]
for gid,a,b,pa,pb,kind,note in GATES:
 assert abs(pa[0]-pb[0])+abs(pa[1]-pb[1])==1,(gid,pa,pb)
 assert (pa==TOWN if a==0 else pa in TILES[a]),gid
 assert pb in TILES[b],gid
# Data-driven POI placements: (name, floors, originClass, boss, notes), castle is always last.
CONTENT={
1:[('血腥丘陵·燃烧坡道',2,'D2_OUTDOOR_ADAPTED','',''),('荒野攻城营',2,'ORIGINAL_PLACEHOLDER','',''),('督军战壕',2,'ORIGINAL_PLACEHOLDER','',''),('裂骨哨所',3,'ORIGINAL_PLACEHOLDER','',''),('恶魔冲锋谷',2,'ORIGINAL_PLACEHOLDER','',''),('督军山克营寨',4,'D2_BOSS_ADAPTED','督军山克','山克驻守原典地表，作为区域Castle终局；宽阔Boss格')],
2:[('冰冻高地·寒霜坡道',2,'D2_OUTDOOR_ADAPTED','',''),('冰雪囚营',3,'ORIGINAL_PLACEHOLDER','',''),('邪魔瞭望台',2,'ORIGINAL_PLACEHOLDER','',''),('远征补给场',2,'ORIGINAL_PLACEHOLDER','',''),('战壕地窖',3,'ORIGINAL_PLACEHOLDER','',''),('亚巴顿（Abaddon）',2,'D2_DUNGEON_ADAPTED','',''),('冰冻高地·恶魔指挥所',4,'D2_THEME_ADAPTED','督军艾德瑞奇','D2特有恶魔首领改造作城塞首领')],
3:[('亚瑞特高原·雪界营地',2,'D2_OUTDOOR_ADAPTED','',''),('山脊督战所',3,'ORIGINAL_PLACEHOLDER','',''),('阿刻戎之坑',3,'D2_DUNGEON_ADAPTED','',''),('远征前锋堡',2,'ORIGINAL_PLACEHOLDER','',''),('高原断崖洞',3,'ORIGINAL_PLACEHOLDER','',''),('亚瑞特高原·破军壁垒',4,'ORIGINAL_PLACEHOLDER','破军恶魔统领（占位）','较大BossArena')],
4:[('水晶通道·前庭',3,'D2_DUNGEON_ADAPTED','',''),('水晶洞窟',3,'D2_DUNGEON_ADAPTED','',''),('冰霜晶脉',3,'ORIGINAL_PLACEHOLDER','',''),('魔化水晶室',3,'ORIGINAL_PLACEHOLDER','',''),('晶壁回廊',2,'ORIGINAL_PLACEHOLDER','',''),('水晶通道·晶核圣所',4,'ORIGINAL_PLACEHOLDER','水晶守护者（占位）','与冰河支线分岔连接')],
5:[('冰川之路·入口冰洞',3,'D2_DUNGEON_ADAPTED','',''),('漂泊者之窟',3,'D2_DUNGEON_ADAPTED','',''),('冰川冰窖',3,'D2_THEME_ADAPTED','',''),('碎冰长廊',3,'ORIGINAL_PLACEHOLDER','',''),('凝霜石殿',2,'ORIGINAL_PLACEHOLDER','',''),('冰川之路·冰冢堡垒',4,'ORIGINAL_PLACEHOLDER','冰冢看守者（占位）','冰川狭长战场与冰冻阻隔')],
6:[('冰冻苔原·破冰雪原',2,'D2_OUTDOOR_ADAPTED','',''),('冻土暗穴',3,'ORIGINAL_PLACEHOLDER','',''),('雪界烽火台',3,'ORIGINAL_PLACEHOLDER','',''),('地狱魔窟',3,'D2_DUNGEON_ADAPTED','',''),('冰川裂谷',2,'ORIGINAL_PLACEHOLDER','',''),('冰冻苔原·霜卫城寨',4,'ORIGINAL_PLACEHOLDER','冰原守将（占位）','暴风雪大平台')],
7:[('远古之路·封雪入口',3,'D2_DUNGEON_ADAPTED','',''),('远古冰窖',4,'D2_DUNGEON_ADAPTED','',''),('石碑试炼场',3,'ORIGINAL_PLACEHOLDER','',''),('破碎祭祀所',3,'ORIGINAL_PLACEHOLDER','',''),('隐秘山道',3,'ORIGINAL_PLACEHOLDER','',''),('远古之路·祭坛关隘',4,'ORIGINAL_PLACEHOLDER','古道守护者（占位）','通向远古山巅的单格山口')],
8:[('山巅前哨',3,'D2_OUTDOOR_ADAPTED','',''),('远古祭坛石阶',3,'D2_THEME_ADAPTED','',''),('三贤者圣坛前厅',2,'ORIGINAL_PLACEHOLDER','',''),('亚瑞特山巅·远古试炼',4,'D2_BOSS_ADAPTED','科力克／马道克／塔力克','末阶段固定三位远古守护者，不应随机刷额外Boss')],
9:[('要塞外侧护阵',3,'ORIGINAL_PLACEHOLDER','',''),('世界之石外廊',3,'D2_THEME_ADAPTED','',''),('破碎圣纹大厅',4,'ORIGINAL_PLACEHOLDER','',''),('堕落守卫壁垒',3,'ORIGINAL_PLACEHOLDER','',''),('世界之石祭所',3,'ORIGINAL_PLACEHOLDER','',''),('毁灭王座·前哨',4,'D2_THEME_ADAPTED','','终局Boss前的独立据点；不重复算Castle内王座'),('世界之石大殿',6,'D2_FINALE_ADAPTED','巴尔','要塞1–3层→毁灭王座入口→五波仆从→世界之石大殿巴尔')],
10:[('冰河·入口冰洞',2,'D2_DUNGEON_ADAPTED','',''),('冰窟回环',3,'ORIGINAL_PLACEHOLDER','',''),('霜魔巢穴',3,'ORIGINAL_PLACEHOLDER','',''),('冰封囚道',2,'ORIGINAL_PLACEHOLDER','',''),('冰河·安雅囚笼',4,'D2_QUEST_ADAPTED','冰冻怪物（Frozenstein）','营救安雅，事实触发下阶段尼拉塞克神殿支线解锁；可选')],
11:[('尼拉塞克神殿·前庭',3,'D2_THEME_ADAPTED','',''),('悲痛之厅',3,'D2_DUNGEON_ADAPTED','',''),('痛苦之厅',3,'D2_DUNGEON_ADAPTED','',''),('瓦特之厅·前哨',3,'D2_THEME_ADAPTED','',''),('瓦特之厅·尼拉塞克',4,'D2_BOSS_ADAPTED','尼拉塞克','四阶段Castle最终尼拉塞克；神殿从哈洛加斯传送门抵达')],
}
# Specific POI location assignment 1-to-1 in visually natural region layout, leaving empty traversable map tiles.
COORDS={
1:[(10,3),(9,3),(8,3),(8,4),(9,4),(9,5)],
2:[(8,5),(8,6),(7,4),(7,5),(7,6),(6,4),(6,5)],
3:[(5,5),(5,4),(4,4),(4,5),(5,6),(6,7)],
4:[(5,7),(4,6),(3,6),(3,7),(4,8),(3,8)],
5:[(3,9),(3,10),(4,9),(4,10),(5,9),(5,10)],
6:[(6,10),(6,9),(5,11),(6,11),(7,9),(7,10)],
7:[(8,10),(8,11),(8,9),(9,11),(10,10),(9,10)],
8:[(9,7),(9,8),(10,7),(10,8)],
9:[(11,7),(11,6),(11,8),(11,9),(11,10),(10,11),(11,11)],
10:[(2,7),(2,6),(1,6),(1,7),(2,8)],
11:[(9,1),(10,1),(10,2),(11,1),(11,2)],
}
LEVELS={
1:{'NORMAL':(32,33),'NIGHTMARE':(66,66),'HELL':(93,93)},
2:{'NORMAL':(33,34),'NIGHTMARE':(66,67),'HELL':(93,94)},
3:{'NORMAL':(34,35),'NIGHTMARE':(67,68),'HELL':(94,94)},
4:{'NORMAL':(35,36),'NIGHTMARE':(68,68),'HELL':(94,95)},
5:{'NORMAL':(36,37),'NIGHTMARE':(68,69),'HELL':(95,95)},
6:{'NORMAL':(37,38),'NIGHTMARE':(69,69),'HELL':(95,96)},
7:{'NORMAL':(38,39),'NIGHTMARE':(69,70),'HELL':(96,96)},
8:{'NORMAL':(40,41),'NIGHTMARE':(70,71),'HELL':(96,97)},
9:{'NORMAL':(41,43),'NIGHTMARE':(71,72),'HELL':(97,97)},
10:{'NORMAL':(36,38),'NIGHTMARE':(68,69),'HELL':(94,95)},
11:{'NORMAL':(39,42),'NIGHTMARE':(70,71),'HELL':(95,96)},
}
RECOMMEND_MAX={'NORMAL':44,'NIGHTMARE':73,'HELL':98}
pois=[];taken=set()
for rid in sorted(CONTENT):
 entries=CONTENT[rid];locations=COORDS[rid];assert len(entries)==len(locations)
 assert entries[-1][0]==Z[rid]['castleName'],rid
 for ordinal,((name,fcount,origin,boss,note),(row,col)) in enumerate(zip(entries,locations),1):
  assert (row,col) in TILES[rid] and (row,col) not in taken,(rid,name,row,col)
  taken.add((row,col));castle=ordinal==len(entries)
  pid=f'W{rid:02d}-'+('C01' if castle else f'D{ordinal:02d}')
  challenges={d:LEVELS[rid][d][1 if castle else 0] for d in DIFS}
  # Final castle of worldstone may have an upper tier challenge of 43 / 72 / 97
  recommend={d:min(challenges[d]+1,RECOMMEND_MAX[d]) for d in DIFS}
  quest=[]
  if rid==8 and castle:quest=['ACT05_ANCIENTS_DEFEATED']
  elif rid==9 and castle:quest=['ACT05_THRONE_FIVE_WAVES_CLEARED','ACT05_BAAL_DEFEATED']
  elif rid==10 and castle:quest=['ACT05_ANYA_RESCUED']
  elif rid==11 and castle:quest=['ACT05_NIHLATHAK_DEFEATED']
  elif rid==1 and castle:quest=['ACT05_SHENK_DEFEATED']
  pois.append({'id':pid,'name':name,'type':'Castle' if castle else 'Dungeon','regionId':rid,'row':row,'col':col,'floors':fcount,'source':origin,'boss':boss,'notes':note,'challenge':challenges,'recommended':recommend,'requiredForCastle':not castle,'branch':'SIDE' if Z[rid]['optional'] else 'MAIN','autoOrder':rid*100+ordinal,'fixedBossArena':castle,'questFacts':quest,'contentStatus':'PROPOSAL_UNBALANCED'})
for rid,z in Z.items():
 z.update(dungeonCount=sum(p['regionId']==rid and p['type']=='Dungeon' for p in pois),castlePoiId=next(p['id'] for p in pois if p['regionId']==rid and p['type']=='Castle'))
assert Counter(p['type'] for p in pois)=={'Dungeon':53,'Castle':11},Counter(p['type'] for p in pois)
assert len(taken)==64
# Approximate D2 Act V style: some outdoor zones have denser trash; ancient summit and final boss emphasize scripted combat.
FLOOR_STYLE={1:('血腥丘陵',3,4),2:('冰冻高地',3,4),3:('亚瑞特高原',3,4),4:('水晶通道',2,5),5:('冰川之路',2,5),6:('冰冻苔原',3,4),7:('远古之路',2,5),8:('亚瑞特山巅',2,5),9:('世界之石要塞',2,5),10:('冰河',2,5),11:('尼拉塞克神殿',2,5)}
DIFFICULTY_ADD={'NORMAL':0,'NIGHTMARE':1,'HELL':2}
floors=[]
for p in pois:
 rid=p['regionId'];castle=p['type']=='Castle';total=p['floors']
 for i in range(1,total+1):
  final=i==total;is_baal=p['id']=='W09-C01';is_ancients=p['id']=='W08-C01';is_anya=p['id']=='W10-C01';is_nihl=p['id']=='W11-C01'
  if is_baal: floorname=['世界之石要塞·第1层','世界之石要塞·第2层','世界之石要塞·第3层','毁灭王座·入口','毁灭王座·巴尔五波仆从','世界之石大殿·巴尔'][i-1]
  elif is_ancients:floorname=['山巅台阶','试炼外圈','远古者铭文祭坛','远古三守护者广场'][i-1]
  elif is_nihl:floorname=['尼拉塞克神殿前庭','悲痛之厅','痛苦之厅','瓦特之厅·尼拉塞克'][i-1]
  elif is_anya and final:floorname='冰河·安雅囚笼与Frozenstein'
  else:floorname=f'{p["name"]}·第{i}'+('阶段' if castle else '层')
  base={1:16,2:19,3:20,4:21,5:22,6:20,7:24,8:26,9:28,10:22,11:23}[rid]
  if castle:base+=13
  if is_baal:base=43
  if is_ancients:base=34
  if is_nihl:base=38
  if is_anya:base=30
  active=base+3*(i-1)+(14 if castle and final else 0)
  bossarena=17 if is_baal and final else 16 if is_ancients and final else 12 if castle and final else 0
  entry=1;exitcell=0 if castle and final else 1
  wave_count=5 if is_baal and i==5 else 0
  event=1 if active>=30 else 0
  if wave_count:event=2
  if is_ancients and final:event=2
  corridor=max(3,round(active*(0.23 if rid<=3 else 0.19)))
  elites=1+(1 if active>=36 else 0)+(1 if active>=58 else 0)
  combat=active-entry-exitcell-event-corridor-elites-bossarena
  assert combat>=5,(p['id'],i,active,combat)
  rows=max(5,math.ceil(math.sqrt(active/0.66)))
  cols=max(rows,math.ceil(active/(rows*0.66)))
  assert rows*cols>=active
  for d in DIFS:
   delta=DIFFICULTY_ADD[d]
   comb_per=FLOOR_STYLE[rid][1]+delta
   elite_per=FLOOR_STYLE[rid][2]+delta
   boss_actor=1 if castle and final and not is_ancients else 0
   # Three simultaneous Ancients; NOT also spawned by elite-grid generator.
   fixed_unique=3 if is_ancients and final else 0
   # Five waves of Baal's minions, target headcount 5, 6, 7 by difficulty.
   wave_monsters=wave_count*(5+delta)
   escort=2 if boss_actor and not is_baal else 0
   kill=combat*comb_per+elites*elite_per+boss_actor+fixed_unique+escort+wave_monsters
   challenge=p['challenge'][d]
   floorlevel=max({'NORMAL':32,'NIGHTMARE':66,'HELL':93}[d],challenge-(1 if i<=2 and total>3 else 0))
   floors.append({'ChapterId':'ACT05','DifficultyId':d,'PoiId':p['id'],'FloorIndex':i,'FloorCount':total,'FloorDisplayName':floorname,'StageMode':'PHASE_SEQUENCE' if castle else 'DUNGEON_FLOOR','SpawnProfileId':f'ACT05_R{rid:02d}_{d}','LayoutRows':rows,'LayoutCols':cols,'ActiveCellTarget':active,'EntryCellCount':entry,'CorridorCellCount':corridor,'CombatCellCount':combat,'EliteCellCount':elites,'EventCellCount':event,'ExitCellCount':exitcell,'BossArenaCellCount':bossarena,'ScriptedWaveCount':wave_count,'ScriptedWaveMonsterTarget':wave_monsters,'FixedUniqueActorCount':fixed_unique,'FloorMonsterLevel':floorlevel,'PoiChallengeLevel':challenge,'RecommendedPlayerLevel':p['recommended'][d],'CombatMonstersPerCellTarget':comb_per,'EliteGroupMonsterTarget':elite_per,'BossActorCount':boss_actor,'BossEscortTarget':escort,'PlannedKillTarget':kill,'ContentStatus':'PROPOSAL_UNBALANCED'})
assert all(f['ActiveCellTarget']==sum(f[k] for k in ['EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount']) for f in floors)
assert all(f['PlannedKillTarget']==f['CombatCellCount']*f['CombatMonstersPerCellTarget']+f['EliteCellCount']*f['EliteGroupMonsterTarget']+f['FixedUniqueActorCount']+f['BossActorCount']+f['BossEscortTarget']+f['ScriptedWaveMonsterTarget'] for f in floors)
assert sum(f['ScriptedWaveCount'] for f in floors if f['PoiId']=='W09-C01' and f['DifficultyId']=='NORMAL')==5
assert sum(f['FixedUniqueActorCount'] for f in floors if f['PoiId']=='W08-C01' and f['DifficultyId']=='NORMAL')==3
assert sum(f['BossActorCount'] for f in floors if f['PoiId']=='W08-C01' and f['DifficultyId']=='NORMAL')==0
kill_by={p['id']:{d:sum(f['PlannedKillTarget'] for f in floors if f['PoiId']==p['id'] and f['DifficultyId']==d) for d in DIFS} for p in pois}
for p in pois:p['plannedKills']=kill_by[p['id']]
# Gate unlock policy derived explicitly from Region ParentCastleIds + SideRegionApprovals; no physical bypasses.
gates=[]
for gid,a,b,pa,pb,kind,note in GATES:
 parent=Z[b]['parents'];unlock=';'.join(Z[i]['castlePoiId'] for i in parent) if parent else 'CHAPTER_UNLOCKED'
 if b in (10,11):unlock+=f';SIDE_APPROVAL_REGION_{b:02d}'
 gates.append({'id':gid,'a':list(pa),'b':list(pb),'fromRegionId':a,'toRegionId':b,'kind':kind,'unlockRequirements':unlock,'description':note})
# Explicit full authorization-aware simulator for every difficulty. Side B requires side A Castle and NEW approval.
class Simulator:
 def __init__(self,d):self.d=d;self.done=set();self.castleApprovals=set();self.sideApprovals=set();self.order=[]
 def unlocked(self,r):return all(Z[x]['castlePoiId'] in self.done for x in Z[r]['parents'])
 def sideok(self,r):return not Z[r]['optional'] or r in self.sideApprovals
 def castleReady(self,r):return all(p['id'] in self.done for p in pois if p['regionId']==r and p['type']=='Dungeon')
 def legal(self):
  return sorted((p for p in pois if p['id'] not in self.done and self.unlocked(p['regionId']) and self.sideok(p['regionId']) and (p['type']=='Dungeon' or (self.castleReady(p['regionId']) and p['id'] in self.castleApprovals))),key=lambda p:(p['challenge'][self.d],int(Z[p['regionId']]['optional']),p['autoOrder'],p['id']))
 def step(self):
  legal=self.legal()
  if not legal:return None
  p=legal[0];self.done.add(p['id']);self.order.append(p['id']);return p
 def run(self):
  while self.step():pass
 def approveCastle(self,r):
  assert self.unlocked(r) and self.sideok(r) and self.castleReady(r)
  self.castleApprovals.add(Z[r]['castlePoiId'])
 def approveSide(self,r):
  assert Z[r]['optional'] and self.unlocked(r)
  self.sideApprovals.add(r)
MAINCOUNT=sum(not Z[p['regionId']]['optional'] for p in pois)
SIMLOG={}
for d in DIFS:
 s=Simulator(d);s.run()
 assert len(s.done)==Z[1]['dungeonCount'] and not s.legal(),('castle not gated',d,len(s.done))
 pre=list(s.order)
 for i in range(1,10):
  assert s.castleReady(i),(d,i,'wrong mainline ready')
  s.approveCastle(i)
  s.run()
 assert len(s.done)==MAINCOUNT,(d,'main not done',len(s.done),MAINCOUNT)
 assert Z[9]['castlePoiId'] in s.done
 assert not any(Z[p['regionId']]['optional'] for p in pois if p['id'] in s.done),(d,'side leaked')
 assert s.unlocked(10) and not s.unlocked(11)
 # Return to chapter after already defeating Baal: manual authorization of side A then A Castle, then side B.
 s.approveSide(10);s.run();assert s.castleReady(10)
 s.approveCastle(10);s.run();assert s.unlocked(11)
 assert not any(pid.startswith('W11-') for pid in s.done)
 s.approveSide(11);s.run();assert s.castleReady(11)
 s.approveCastle(11);s.run();assert len(s.done)==len(pois)
 SIMLOG[d]={'stopsAtFirstCastleAfterDungeons':pre,'mainSequence':list(s.order[:MAINCOUNT]),'sideSequenceAfterMain':list(s.order[MAINCOUNT:]),'allCount':len(s.done)}
assert all(LEVELS[r][d][t] in range({'NORMAL':32,'NIGHTMARE':66,'HELL':93}[d],{'NORMAL':43,'NIGHTMARE':72,'HELL':97}[d]+1) for r in Z for d in DIFS for t in (0,1))

def dump(p,obj):p.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
def writecsv(p,rows):
 with p.open('w',encoding='utf-8-sig',newline='') as fh:
  writer=csv.DictWriter(fh,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
full={'schemaVersion':'ACT05_V0.1_PROPOSAL','chapterId':'ACT05','rows':11,'columns':11,'gridRows':GRID,'town':{'id':'TOWN_HARROGATH','name':'哈洛加斯','tiles':[list(TOWN)],'safe':True},'regions':list(Z.values()),'gates':gates,'poi':pois,'policy':{'selection':'CHALLENGE_ASC_MAIN_FIRST_STABLE','castleRequiresExplicitApproval':True,'sideRegionRequiresExplicitApproval':True,'optionalRegionsNeverBlockMain':True,'returnToPreviousChapterSide':True,'reuseAcrossDifficulty':True,'authorizationIsPerDifficulty':True,'noWalkToUnauthorizedSide':True},'planningDisclaimer':'D2世界地名仅用于内部对齐。此为内容候选静态模型，原创地牢、Boss行为、怪物预算未经Godot实机验证。'}
dump(D/'ACT05_MAP_V0.1.json',full)
writecsv(D/'ACT05_REGION_V0.1.csv',[{'ChapterId':'ACT05','RegionId':f'REGION_{z["id"]:02d}','RegionName':z['name'],'IsOptional':int(z['optional']),'UnlockParentCastleIds':';'.join(Z[i]['castlePoiId'] for i in z['parents']),'RequiresManualSideApproval':int(z['optional']),'CastlePoiId':z['castlePoiId'],'TileCount':z['tiles'],'DungeonCount':z['dungeonCount'],'ContentStatus':'PROPOSAL'} for z in Z.values()])
writecsv(D/'ACT05_POI_V0.1.csv',[{'ChapterId':'ACT05','PoiId':p['id'],'RegionId':f'REGION_{p["regionId"]:02d}','Type':p['type'],'DisplayName':p['name'],'TileRow':p['row'],'TileCol':p['col'],'RequiredForCastle':int(p['requiredForCastle']),'IsOptionalRegion':int(Z[p['regionId']]['optional']),'ManualCastleApproval':int(p['type']=='Castle'),'FloorCount':p['floors'],'AutoOrder':p['autoOrder'],'ChallengeNormal':p['challenge']['NORMAL'],'ChallengeNightmare':p['challenge']['NIGHTMARE'],'ChallengeHell':p['challenge']['HELL'],'RecommendNormal':p['recommended']['NORMAL'],'RecommendNightmare':p['recommended']['NIGHTMARE'],'RecommendHell':p['recommended']['HELL'],'SourceType':p['source'],'QuestFacts':';'.join(p['questFacts']),'FinalBossDisplayName':p['boss'],'KillBudgetNormal':p['plannedKills']['NORMAL'],'KillBudgetNightmare':p['plannedKills']['NIGHTMARE'],'KillBudgetHell':p['plannedKills']['HELL'],'ConfigStatus':'PROPOSAL_UNBALANCED'} for p in pois])
writecsv(D/'ACT05_FLOOR_PLAN_V0.1.csv',floors)
writecsv(D/'ACT05_GATES_V0.1.csv',[{'GateId':g['id'],'FromRegionId':g['fromRegionId'],'ToRegionId':g['toRegionId'],'FromRow':g['a'][0],'FromCol':g['a'][1],'ToRow':g['b'][0],'ToCol':g['b'][1],'TravelKind':g['kind'],'UnlockRequirements':g['unlockRequirements'],'Notes':g['description']} for g in gates])
kills={d:{'main':sum(p['plannedKills'][d] for p in pois if not Z[p['regionId']]['optional']),'side':sum(p['plannedKills'][d] for p in pois if Z[p['regionId']]['optional'])} for d in DIFS}
dump(D/'ACT05_KILL_ESTIMATE_V0.1.json',{'byDifficulty':kills,'perPoi':kill_by,'definition':'规划首次全清怪物预算＝普通格×每格数量+精英格×每组数量+独立三远古守卫+Boss与固定随从+巴尔五波仆从。地牢失败重试、复刷、召唤怪与地图跳过房间不计。'})
dump(D/'ACT05_SIMULATION_V0.1.json',SIMLOG)
print('SUCCESS','11x11','regions',len(Z),'dungeons',sum(p['type']=='Dungeon' for p in pois),'castles',sum(p['type']=='Castle' for p in pois),'independent floors',len(floors)//3,'floor records',len(floors),'kills',kills)
print('\n'.join(f'{i+1:02}  '+row for i,row in enumerate(GRID)))
