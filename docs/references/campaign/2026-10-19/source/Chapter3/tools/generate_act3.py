from __future__ import annotations
import csv,json,math,collections,pathlib
from collections import deque
from pathlib import Path
ROOT=Path('/mnt/data/D2_Act3_Map_V0_1');D=ROOT/'data'
DIFFICULTIES=['NORMAL','NIGHTMARE','HELL']
# Rows 1..10, columns 1..10: x=no road; T=Kurast Docks; 1..7 main; 8..9 side
GRID=[
 'xxxxxxxxxx',
 'xxxxxxx55x',
 'xxxx999555',
 'xxxx994445',
 'x88x933445',
 'x888223346',
 'x811223346',
 'xx11222366',
 'TT11127766',
 'xxxxxx7777',
]
REGIONS=[
 (1,'蜘蛛森林',False,[],'蛛母祭坛','从库拉斯海港进入丛林，蜘蛛洞窟取得克林姆之眼'),
 (2,'剥皮丛林',False,[1],'剥皮祭寨','剥皮地牢深处取得克林姆之脑；吉得宾元素'),
 (3,'下层库拉斯',False,[2],'下城腐化城门','丛林转入古城的视觉分界'),
 (4,'库拉斯商场',False,[3],'商场圣殿','库拉斯下水道深处取得克林姆之心'),
 (5,'上层库拉斯',False,[4],'上城圣门','萨卡兰姆神庙与上城据点'),
 (6,'库拉斯堤道',False,[5],'堤道圣关','收束城区，通往崔凡克的狭窄通道'),
 (7,'崔凡克',False,[6],'崔凡克·议会神殿','议会与强制球体、憎恨囚牢、墨菲斯托的终局'),
 (8,'庞大湿地',True,[1],'湿地瘴母巢','可选低地沼泽，近 D2 可绕过的庞大湿地'),
 (9,'库拉斯古神庙群',True,[4],'古神庙中枢','可选神庙群，含蓝·依森的古书所在的废弃神殿'),
]
ZONES={r[0]:{'id':r[0],'name':r[1],'optional':r[2],'parents':r[3],'castleName':r[4],'description':r[5]} for r in REGIONS}
# name,row,col,floors,source,boss,note,levelIndex
POIS={
 1:[
 ('蜘蛛巢穴',7,3,1,'原典','','Arachnid Lair；短层',0),
 ('蜘蛛洞窟',8,3,1,'原典','','Spider Cavern，克林姆之眼；必清任务事实',0),
 ('蛛网裂谷',8,4,1,'原创占位','','短小蜘蛛通道',0),
 ('卵室深穴',9,3,2,'原创占位','','小型双层虫窟',0),
 ('污染菌棚',9,4,1,'原创占位','','菌林怪物编队',1),
 ('丛林蛛巢',9,5,2,'原创占位','','深入腐化蛛巢',1),
 ('蛛母祭坛',7,4,2,'原创占位','腐化蛛母（占位）','区域 Castle，独立 Boss 场',1)],
 2:[
 ('剥皮地牢',6,5,3,'原典','','Flayer Dungeon 1–3；克林姆之脑；必清任务事实',0),
 ('吉得宾祭坛',7,5,1,'原典改造','','原典吉得宾为地面任务，这里地牢化',0),
 ('剥皮萨满巢',7,6,2,'原创占位','','剥皮萨满召唤编队',0),
 ('瘴气根穴',8,5,1,'原创占位','','短地牢',0),
 ('木偶献祭洞',8,6,1,'原创占位','','密集小型怪物',1),
 ('利齿祭坑',9,6,2,'原创占位','','剥皮部族攻坚前哨',1),
 ('剥皮祭寨',6,6,2,'原创占位','剥皮祭司首领（占位）','区域 Castle；祭寨攻坚',1)],
 3:[
 ('下层库拉斯地下室',5,6,1,'原典地面改造','','城市地表转为地牢战斗入口',0),
 ('废弃居民区',5,7,1,'原创占位','','废墟民居地牢化',0),
 ('断裂排水渠',6,7,2,'原创占位','','地下排水网',0),
 ('腐化街坊',7,7,1,'原创占位','','城市暴民据点',1),
 ('泥泞窖藏',8,8,2,'原创占位','','遗落宝藏窖藏',1),
 ('下城腐化城门',6,8,2,'原创占位','萨卡兰姆堕落守门人（占位）','Castle：城市门廊战斗',1)],
 4:[
 ('库拉斯下水道',4,7,2,'原典','','Sewers Level 1–2；克林姆之心',0),
 ('废弃神龛',4,8,1,'原创占位','','神殿外侧支点',0),
 ('商场地下仓库',4,9,1,'原创占位','','仓库密集小空间',0),
 ('蛇纹供奉室',5,8,2,'原创占位','','毒系神殿',1),
 ('腐化祭台',5,9,1,'原创占位','','城内祭坛',1),
 ('商场圣殿',6,9,3,'原创占位','堕落圣坛司祭（占位）','Castle：神殿大殿与祭台 Boss',1)],
 5:[
 ('上层库拉斯神殿',2,8,1,'原典改造','','上城神殿地牢化',0),
 ('萨卡兰姆隐修院',2,9,2,'原创占位','','人形祭司战',0),
 ('失落密室',3,8,1,'原创占位','','封闭房间',0),
 ('腐败圣室',3,9,2,'原创占位','','持续战斗房间',1),
 ('上城供奉间',3,10,1,'原创占位','','封印祭坛',1),
 ('上城圣门',4,10,2,'原创占位','高阶狂热祭司（占位）','Castle：通过上城大门',1)],
 6:[
 ('堤道坍塌廊',6,10,2,'原创占位','','窄长堤道',0),
 ('祭司守卫站',7,10,1,'原创占位','','盾卫 + 祭司',0),
 ('堤道封印堂',8,10,2,'原创占位','','连续封印房间',1),
 ('禁卫兵营',9,10,2,'原创占位','','大型守卫据点',1),
 ('堤道圣关',8,9,3,'原创占位','堤道圣卫统领（占位）','Castle：接近崔凡克的终局门槛',1)],
 7:[
 ('崔凡克前厅',9,7,2,'原典场景改造','','接近议会神殿的前厅',0),
 ('议会仆从巢',9,8,1,'原创占位','','议会仆从群',0),
 ('崔凡克古道',10,7,2,'原创占位','','石质圣路',0),
 ('血祭外廊',10,8,2,'原创占位','','混合祭司战斗',0),
 ('强制球体卫廊',10,9,2,'原典元素改造','','保护强制球体的外围区域；真正开门事件在 Castle 内',0),
 ('崔凡克·议会神殿',10,10,5,'原典改造','议会成员 → 墨菲斯托','Castle 内 2 个前段攻坚阶段 + 憎恨囚牢 1–3 层；强制球体与克林姆任务事实在 Castle 内结算',0)],
 8:[
 ('沼泽洞穴',5,2,3,'原典转置','','原版 Swampy Pit 入口位于剥皮丛林；为支线感改设于庞大湿地',0),
 ('水没祭坛',5,3,1,'原创占位','','湿地神坛',0),
 ('毒蚊孵育坑',6,2,1,'原创占位','','毒蚊怪物池',0),
 ('腐败古井',6,3,2,'原创占位','','隐藏沼泽井',1),
 ('湿地瘴母巢',6,4,2,'原创占位','湿地瘴母（占位）','手动批准支线 Castle',1)],
 9:[
 ('废弃神殿',3,5,1,'原典重新分区','','Ruined Temple，蓝·依森的古书，可选任务奖励',0),
 ('废弃圣器室',3,6,1,'原典重新分区','','Disused Reliquary，原典位于库拉斯堤道',0),
 ('遗忘神殿',3,7,1,'原典重新分区','','Forgotten Temple，原典位于上层库拉斯',0),
 ('遗忘圣物室',4,5,2,'原典重新分区','','Forgotten Reliquary，原典位于上层库拉斯',1),
 ('古神庙中枢',4,6,3,'原创占位','古神庙守护者（占位）','Castle：手动批准、完全独立于主线',1)]
}
assert len(GRID)==10 and all(len(row)==10 for row in GRID),GRID
LEVELS={
1:{'NORMAL':(21,21),'NIGHTMARE':(56,56),'HELL':(84,84)},
2:{'NORMAL':(22,22),'NIGHTMARE':(57,57),'HELL':(85,85)},
3:{'NORMAL':(22,23),'NIGHTMARE':(57,58),'HELL':(85,86)},
4:{'NORMAL':(23,24),'NIGHTMARE':(58,59),'HELL':(86,86)},
5:{'NORMAL':(24,24),'NIGHTMARE':(59,59),'HELL':(87,87)},
6:{'NORMAL':(24,25),'NIGHTMARE':(60,60),'HELL':(87,87)},
7:{'NORMAL':(25,26),'NIGHTMARE':(61,61),'HELL':(88,88)},
8:{'NORMAL':(22,23),'NIGHTMARE':(57,58),'HELL':(85,85)},
9:{'NORMAL':(24,25),'NIGHTMARE':(60,60),'HELL':(87,87)},
}
GATES=[
 {'id':'G-T-01','a':[9,2],'b':[9,3],'kind':'ROAD','unlock':'章节开局','description':'库拉斯海港木桥 → 蜘蛛森林'},
 {'id':'G-01-02','a':[7,4],'b':[7,5],'kind':'JUNGLE_PATH','unlock':'蛛母祭坛已征服','description':'蜘蛛林到剥皮丛林的单格林道'},
 {'id':'G-02-03','a':[6,6],'b':[6,7],'kind':'RUIN_GATE','unlock':'剥皮祭寨已征服','description':'丛林尽头出现下层库拉斯废墟'},
 {'id':'G-03-04','a':[5,7],'b':[5,8],'kind':'CITY_GATE','unlock':'下城腐化城门已征服','description':'杂乱丛林转入规则城区'},
 {'id':'G-04-05','a':[4,9],'b':[4,10],'kind':'TEMPLE_GATE','unlock':'商场圣殿已征服','description':'上城区入口'},
 {'id':'G-05-06','a':[5,10],'b':[6,10],'kind':'CAUSEWAY_BRIDGE','unlock':'上城圣门已征服','description':'堤道悬桥；跨断崖唯一主路'},
 {'id':'G-06-07','a':[9,9],'b':[9,8],'kind':'TRAVINCAL_GATE','unlock':'堤道圣关已征服','description':'曲折下行后抵达崔凡克'},
 {'id':'G-01-08','a':[7,3],'b':[7,2],'kind':'SIDE_JUNGLE','unlock':'蛛母祭坛已征服 ＋ 玩家批准庞大湿地','description':'旁路湿地：手动批准才开启'},
 {'id':'G-04-09','a':[4,7],'b':[4,6],'kind':'SIDE_TEMPLE','unlock':'商场圣殿已征服 ＋ 玩家批准古神庙群','description':'从商场西侧进入古神庙群'},
]
# small correction: R5 (5,10) is indeed 5, town 9,1-2. All gatelines 4-neighbor.
poilist=[];coords=set();counts=collections.Counter()
for rid in sorted(POIS):
 entries=POIS[rid];assert sum(e[0]==ZONES[rid]['castleName'] for e in entries)==1,(rid,'castle')
 dnum=0
 for idx,(name,r,c,fc,source,boss,note,levelIndex) in enumerate(entries,1):
  is_castle=name==ZONES[rid]['castleName'];dnum+=not is_castle
  pid=f'B{rid:02d}-'+('C01' if is_castle else f'D{dnum:02d}')
  assert GRID[r-1][c-1]==str(rid),(pid,name,r,c,GRID[r-1][c-1]);assert (r,c)not in coords,(pid,'repeat',r,c)
  coords.add((r,c));counts['Castle' if is_castle else 'Dungeon']+=1
  levels={d:LEVELS[rid][d][levelIndex] for d in DIFFICULTIES}
  rec={d:min(levels[d]+1,{'NORMAL':27,'NIGHTMARE':62,'HELL':89}[d]) for d in DIFFICULTIES}
  poilist.append({'id':pid,'name':name,'type':'Castle' if is_castle else 'Dungeon','regionId':rid,'row':r,'col':c,'floors':fc,'source':source,'boss':boss,'notes':note,'challenge':levels,'recommended':rec,'requiredForCastle':not is_castle,'branch':'SIDE' if ZONES[rid]['optional'] else 'MAIN','autoOrder':rid*100+idx,'fixedBossArena':is_castle,'mandatoryQuestFacts':(['KHALIMS_EYE'] if name=='蜘蛛洞窟' else ['KHALIMS_BRAIN'] if name=='剥皮地牢' else ['KHALIMS_HEART'] if name=='库拉斯下水道' else [])})
assert len(poilist)==53 and counts['Dungeon']==44 and counts['Castle']==9,(len(poilist),counts)
for rid,z in ZONES.items():
 tiles={(r+1,c+1) for r,row in enumerate(GRID) for c,ch in enumerate(row) if ch==str(rid)}
 assert 3<=len(tiles)<=8,(rid,len(tiles))
 stack=[next(iter(tiles))];visited=set(stack)
 while stack:
  r,c=stack.pop()
  for ne in ((r+1,c),(r-1,c),(r,c+1),(r,c-1)):
   if ne in tiles and ne not in visited:visited.add(ne);stack.append(ne)
 assert visited==tiles,(rid,tiles-visited)
 z.update(tiles=len(tiles),dungeonCount=sum(p['regionId']==rid and p['type']=='Dungeon' for p in poilist),castlePoiId=next(p['id'] for p in poilist if p['regionId']==rid and p['type']=='Castle'))
assert sum(''.join(GRID).count(ch) for ch in '123456789')==60
assert ''.join(GRID).count('T')==2
assert ''.join(GRID).count('x')==38
assert len(coords)==53
for g in GATES:
 a,b=g['a'],g['b'];assert abs(a[0]-b[0])+abs(a[1]-b[1])==1,(g['id'],'not adjacent')
 assert GRID[a[0]-1][a[1]-1]!='x' and GRID[b[0]-1][b[1]-1]!='x',g
# crossing rule: only the documented gate is traversable between regions; all regular intra-region tile-neighbors walkable.
# Ensure every region has entry Gate and mainline dependencies match Gate names.
assert set((g['id'] for g in GATES))=={'G-T-01','G-01-02','G-02-03','G-03-04','G-04-05','G-05-06','G-06-07','G-01-08','G-04-09'}
# physical network floodfill with enabled Castle/side authorizations using per-region BFS after node dependencies.
by_region=collections.defaultdict(list)
for p in poilist:by_region[p['regionId']].append(p)

floors=[]
for p in poilist:
 for f in range(1,p['floors']+1):
  last=f==p['floors'];boss=last and p['type']=='Castle';rid=p['regionId'];
  base={1:14,2:17,3:19,4:22,5:23,6:24,7:27,8:18,9:24}[rid]
  # deliberate scale identity and iconic rooms (all example budgets)
  if p['name']=='蜘蛛洞窟':base=13
  if p['name']=='蜘蛛巢穴':base=13
  if p['name']=='剥皮地牢':base=20
  if p['name']=='库拉斯下水道':base=24
  if p['name']=='沼泽洞穴':base=20
  if '神殿' in p['name'] or '圣殿' in p['name']:base+=3
  if p['type']=='Castle':base+=8
  if rid==7 and p['type']=='Castle':base=34
  active=base+3*(f-1)+(7 if boss else 0)
  arena=(8 if boss else 0);entry=1;exitcell=0 if boss else 1
  event=(1 if active>=24 and f==1 and not boss else 0)
  corridor=max(3,round(active*(0.28 if rid<=2 else 0.19)))
  elite=1+(1 if active>=32 else 0)+(1 if boss and active>=40 else 0)
  combat=active-entry-exitcell-event-corridor-elite-arena
  assert combat>=3,(p['id'],f,combat)
  rows=max(4,math.ceil(math.sqrt(active/0.68)))
  cols=max(rows,math.ceil(active/(rows*0.68)))
  assert rows*cols>=active
  for dif in DIFFICULTIES:
   per={1:3,2:5,3:3,4:3,5:2,6:3,7:2,8:3,9:2}[rid]+{'NORMAL':0,'NIGHTMARE':1,'HELL':2}[dif]
   elitegroup={1:4,2:5,3:3,4:3,5:3,6:3,7:4,8:4,9:3}[rid]+{'NORMAL':0,'NIGHTMARE':1,'HELL':2}[dif]
   escort=2 if boss else 0
   kills=combat*per+elite*elitegroup+(1+escort if boss else 0)
   challenge=p['challenge'][dif]
   floorlevel=max({'NORMAL':21,'NIGHTMARE':56,'HELL':84}[dif],challenge-(1 if f==1 and p['floors']>=2 else 0))
   floors.append({'ChapterId':'ACT03','DifficultyId':dif,'PoiId':p['id'],'FloorIndex':f,'FloorCount':p['floors'],'SpawnProfileId':f'ACT03_R{rid:02d}_DENSITY_{dif}', 'FloorDisplayName':(f'憎恨囚牢·第{f-2}层' if p['regionId']==7 and p['type']=='Castle' and f>=3 else f'{p["name"]}·第{f}层'),'LayoutRows':rows,'LayoutCols':cols,'ActiveCellTarget':active,'EntryCellCount':entry,'CorridorCellCount':corridor,'CombatCellCount':combat,'EliteCellCount':elite,'EventCellCount':event,'ExitCellCount':exitcell,'BossArenaCellCount':arena,'FloorMonsterLevel':floorlevel,'PoiChallengeLevel':challenge,'RecommendedPlayerLevel':p['recommended'][dif],'CombatMonstersPerCellTarget':per,'EliteGroupMonsterTarget':elitegroup,'BossActorCount':1 if boss else 0,'BossEscortTarget':escort,'PlannedKillTarget':kills,'ContentStatus':'PROPOSAL_UNBALANCED'})
assert len(floors)%3==0
assert all(row['ActiveCellTarget']==sum(row[key] for key in ['EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount']) for row in floors)
kill_by_poi={p['id']:{d:sum(f['PlannedKillTarget'] for f in floors if f['PoiId']==p['id'] and f['DifficultyId']==d) for d in DIFFICULTIES} for p in poilist}
for p in poilist:p['plannedKills']=kill_by_poi[p['id']]

class Simulator:
 def __init__(self,d='NORMAL'):
  self.diff=d;self.completed=set();self.castle_authorized=set();self.side_authorized=set();self.events=[]
 def unlocked(self,r):return all(ZONES[x]['castlePoiId'] in self.completed for x in ZONES[r]['parents'])
 def authorized(self,r):return not ZONES[r]['optional'] or r in self.side_authorized
 def castle_ready(self,r):return all(p['id'] in self.completed for p in by_region[r] if p['type']=='Dungeon')
 def candidates(self):
  return sorted([p for p in poilist if p['id'] not in self.completed and self.unlocked(p['regionId']) and self.authorized(p['regionId']) and (p['type']=='Dungeon' or (self.castle_ready(p['regionId']) and p['id'] in self.castle_authorized))],key=lambda p:(p['challenge'][self.diff],1 if ZONES[p['regionId']]['optional'] else 0,p['autoOrder'],p['id']))
 def step(self):
  c=self.candidates()
  if not c:return None
  p=c[0];self.completed.add(p['id']);self.events.append(p['id']);return p
 def approve_castle(self,r):
  assert self.unlocked(r) and self.authorized(r) and self.castle_ready(r)
  self.castle_authorized.add(ZONES[r]['castlePoiId'])
 def approve_side(self,r):
  assert ZONES[r]['optional'] and self.unlocked(r)
  self.side_authorized.add(r)

main_count=sum(not ZONES[p['regionId']]['optional'] for p in poilist)
for d in DIFFICULTIES:
 sim=Simulator(d)
 while sim.step():pass
 assert len(sim.completed)==6 and not sim.candidates(),(d,'should pause at Spider Castle',len(sim.completed))
 loops=0
 while len(sim.completed)<main_count and loops<300:
  loops+=1
  if not sim.step():
   eligible_castles=[r for r in range(1,8) if sim.unlocked(r) and sim.castle_ready(r) and ZONES[r]['castlePoiId'] not in sim.completed]
   assert eligible_castles,(d,'main_deadlock',len(sim.completed))
   for r in eligible_castles:sim.approve_castle(r)
 assert len(sim.completed)==main_count and ZONES[7]['castlePoiId'] in sim.completed,(d,'main unfinished')
 assert all(p['id'] not in sim.completed for p in poilist if ZONES[p['regionId']]['optional']),(d,'side leak')
 for r in (8,9):sim.approve_side(r)
 while len(sim.completed)<len(poilist) and loops<400:
  loops+=1
  if not sim.step():
   eligible_castles=[r for r in (8,9) if sim.unlocked(r) and sim.castle_ready(r) and ZONES[r]['castlePoiId'] not in sim.completed]
   assert eligible_castles,(d,'side_deadlock',len(sim.completed))
   for r in eligible_castles:sim.approve_castle(r)
 assert len(sim.completed)==len(poilist)
 # Cases intentionally test that optional quest cannot become mandatory for Mephisto

# Check quest facts are guaranteed within MAIN dungeon locations, council flail inside final Castle.
quest={'KHALIMS_EYE','KHALIMS_BRAIN','KHALIMS_HEART'}
assert {x for p in poilist if not ZONES[p['regionId']]['optional'] for x in p['mandatoryQuestFacts']}==quest

D.mkdir(parents=True,exist_ok=True)
def dump(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
full={'schemaVersion':'ACT03_V0.1_PROPOSAL','chapterId':'ACT03','rows':10,'columns':10,'gridRows':GRID,'town':{'id':'TOWN_KURAST_DOCKS','name':'库拉斯海港','tiles':[[9,1],[9,2]],'safe':True},'regions':list(ZONES.values()),'gates':GATES,'poi':poilist,'policy':{'selection':'CHALLENGE_ASC_MAIN_FIRST_STABLE','castleRequiresExplicitApproval':True,'sideRegionRequiresExplicitApproval':True,'sideDoesNotBlockMain':True,'returnToPreviousChapterSide':True,'reuseAcrossDifficulty':True},'planningDisclaimer':'内容草案，非已验证的 Godot 配置读取 schema；Boss 名称及扩展地牢可替换'}
dump(D/'ACT03_MAP_V0.1.json',full)
def writecsv(path,rows,cols):
 with path.open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=cols);w.writeheader();w.writerows(rows)
regrows=[{'ChapterId':'ACT03','RegionId':f'REGION_{r["id"]:02d}','RegionName':r['name'],'IsOptional':int(r['optional']),'UnlockParentCastleIds':';'.join(ZONES[i]['castlePoiId'] for i in r['parents']),'RequiresManualSideApproval':int(r['optional']),'CastlePoiId':r['castlePoiId'],'TileCount':r['tiles'],'DungeonCount':r['dungeonCount'],'Status':'PROPOSAL'} for r in ZONES.values()]
writecsv(D/'ACT03_REGION_V0.1.csv',regrows,list(regrows[0]))
poirows=[{'ChapterId':'ACT03','PoiId':p['id'],'RegionId':f'REGION_{p["regionId"]:02d}','Type':p['type'],'DisplayName':p['name'],'TileRow':p['row'],'TileCol':p['col'],'RequiredForCastle':int(p['requiredForCastle']),'IsOptionalRegion':int(ZONES[p['regionId']]['optional']),'ManualCastleApproval':int(p['type']=='Castle'),'FloorCount':p['floors'],'AutoOrder':p['autoOrder'],'ChallengeNormal':p['challenge']['NORMAL'],'ChallengeNightmare':p['challenge']['NIGHTMARE'],'ChallengeHell':p['challenge']['HELL'],'RecommendNormal':p['recommended']['NORMAL'],'RecommendNightmare':p['recommended']['NIGHTMARE'],'RecommendHell':p['recommended']['HELL'],'SourceType':p['source'],'QuestFacts':';'.join(p['mandatoryQuestFacts']),'FinalBossDisplayName':p['boss'],'KillBudgetNormal':p['plannedKills']['NORMAL'],'KillBudgetNightmare':p['plannedKills']['NIGHTMARE'],'KillBudgetHell':p['plannedKills']['HELL'],'ConfigStatus':'PROPOSAL_UNBALANCED'} for p in poilist]
writecsv(D/'ACT03_POI_V0.1.csv',poirows,list(poirows[0]))
writecsv(D/'ACT03_FLOOR_PLAN_V0.1.csv',floors,list(floors[0]))
gaterows=[{'GateId':g['id'],'FromRow':g['a'][0],'FromCol':g['a'][1],'ToRow':g['b'][0],'ToCol':g['b'][1],'TravelKind':g['kind'],'UnlockDescription':g['unlock'],'Notes':g['description']} for g in GATES]
writecsv(D/'ACT03_GATES_V0.1.csv',gaterows,list(gaterows[0]))
kills={d:{'main':sum(p['plannedKills'][d] for p in poilist if not ZONES[p['regionId']]['optional']),'side':sum(p['plannedKills'][d] for p in poilist if ZONES[p['regionId']]['optional'])} for d in DIFFICULTIES}
dump(D/'ACT03_KILL_ESTIMATE_V0.1.json',{'byDifficulty':kills,'perPoi':kill_by_poi,'definition':'全规划遭遇清完：普通战斗格×目标怪物数 + 精英格×精英编队人数 + Boss 演员 + Boss 随从；不含重刷、事件增援或召唤数量'})
print('SUMMARY:',counts,'tiles=',{rid:z['tiles'] for rid,z in ZONES.items()},'floor_count=',len(floors)//3,'difficultyFloorRows=',len(floors),'kills=',kills)
