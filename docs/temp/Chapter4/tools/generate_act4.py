from __future__ import annotations
from pathlib import Path
from collections import Counter, defaultdict, deque
import csv, json, math
R = Path('/mnt/data/D2_Act4_Map_V0_1')
D=R/'data';D.mkdir(exist_ok=True)
DIFS=('NORMAL','NIGHTMARE','HELL')
# WorldTile 7x7, 1 SafeTown, 5 regions, rest VOID. WorldTile != DungeonFloor Cell.
GRID=[
 'xxxx444',
 'xxx3444',
 'xx3334x',
 'x22335x',
 '1122x55',
 'T112x55',
 'xx1xxxx',
]
REGIONS=[
 (1,'郊外大草原',False,[],'郊外破魔岗哨','地狱前哨：宽阔黑石平台，进入地狱的第一段征途'),
 (2,'绝望平原',False,[1],'衣卒尔·堕落之牢','腐化天使衣卒尔，经典任务作为本区 Castle Boss'),
 (3,'神罚之城',False,[2],'神罚之城·城塞','毁坏城市与诅咒建筑，经过城关抵达火焰之河'),
 (4,'火焰之河与混沌避难所',False,[3],'混沌避难所','火焰之河石桥接混沌圣殿，六阶段连续终局攻坚，迪亚波罗'),
 (5,'地狱熔炉',True,[3],'地狱熔炉·核心','独立可选支线，海法斯特与墨菲斯托灵魂石；不阻断迪亚波罗'),
]
Z={i:{'id':i,'name':n,'optional':optional,'parents':parents,'castleName':castle,'description':desc} for i,n,optional,parents,castle,desc in REGIONS}
# (name,row,col,floorCount,sourceType,boss,note,levelIndex)
POIS={
 1:[
 ('破碎石阶',5,1,2,'原典地表地牢化','','开放平台分层，不是D2原版独立洞穴',0),
 ('恶魔岩窟',6,2,3,'原创占位','','短小裂隙与恶魔巢房',0),
 ('焦骨裂隙',7,3,2,'原创占位','','狭路通向大型战斗台',0),
 ('郊外破魔岗哨',6,3,4,'原创占位','破魔哨官（占位）','区域 Castle，四阶段石台、战门与宽阔 Boss 场',1)],
 2:[
 ('绝望灵渊',4,2,3,'原创占位','','长廊＋散布的受刑灵魂',0),
 ('怨魂囚穴',5,3,2,'原创占位','','空间紧凑，精英施法怪占比提高',0),
 ('失落羽翼祭台',5,4,3,'原典元素改造','','衣卒尔背景的祭坛、冻霜/冲击型敌人',0),
 ('衣卒尔·堕落之牢',4,3,4,'原典Boss改造','衣卒尔','Castle 4 阶段；原作衣卒尔位于绝望平原地表，此处地牢化',1)],
 3:[
 ('罪徒集结场',3,3,3,'原创占位','','恶魔集合场、比较密集的近战敌群',0),
 ('诅咒礼拜堂',3,4,3,'原创占位','','狭长礼拜堂与高位施法怪',0),
 ('焦灼大街',4,4,2,'原典地表地牢化','','大街由连续室内/半露天格组成',0),
 ('破碎钟楼',4,5,3,'原创占位','','多阶段楼层，视觉地标',1),
 ('神罚之城·城塞',3,5,4,'原创占位','地狱城塞守将（占位）','城门 Castle，通往火焰之河的唯一关口',1)],
 4:[
 ('火焰之河·下游',3,6,3,'原典地表地牢化','','火焰之河：长条平台与岩浆河，自动战斗只能在地牢',0),
 ('熔岩渡桥',2,5,3,'原创占位','','狭长石桥，熔岩不能进入',0),
 ('深渊守卫哨',2,6,4,'原创占位','','恶魔骑士与较大精英编队',0),
 ('熔流断崖',1,5,3,'原创占位','','斜向平台和火焰障碍',1),
 ('哈德尔圣门',2,7,2,'原典NPC地点改造','','D2 Hadriel守护混沌避难所门前；改为战斗前哨',1),
 ('封印前庭',1,6,4,'原典主题改造','','封印/议事厅前段，非真正的五封印终局',1),
 ('混沌避难所',1,7,6,'原典终局改造','迪亚波罗','Castle 6 连续阶段：前厅、恶魔门廊、三阶段封印守卫、迪亚波罗Boss场；五个封印事实分布在第3–5阶段',1)],
 5:[
 ('熔炉外侧石道',4,6,3,'原典地表改造','','从火焰之河岔出的短支线桥',0),
 ('铁锤囚牢',5,6,4,'原创占位','','熔炉恶魔守卫与符文锤前置',0),
 ('灵魂石祭室',6,6,3,'原典任务改造','','墨菲斯托灵魂石事件前置；不影响主线',0),
 ('地狱熔炉·核心',6,7,4,'原典支线改造','海法斯特','Castle 核心：海法斯特守卫铁锤、灵魂石摧毁结算；需额外人工批准',1)],
}
LEVELS={
 1:{'NORMAL':(27,28),'NIGHTMARE':(62,62),'HELL':(89,89)},
 2:{'NORMAL':(28,29),'NIGHTMARE':(63,63),'HELL':(90,90)},
 3:{'NORMAL':(29,30),'NIGHTMARE':(64,64),'HELL':(91,91)},
 4:{'NORMAL':(30,31),'NIGHTMARE':(65,65),'HELL':(92,92)},
 5:{'NORMAL':(30,31),'NIGHTMARE':(64,65),'HELL':(91,92)},
}
RECOMMEND_MAX={'NORMAL':32,'NIGHTMARE':66,'HELL':93}
GATES=[
 {'id':'G-T-01','a':[6,1],'b':[6,2],'kind':'INFERNAL_GATE','unlock':'章节开始','description':'群魔堡垒东门→郊外大草原'},
 {'id':'G-01-02','a':[6,3],'b':[6,4],'kind':'STONE_BRIDGE','unlock':'郊外破魔岗哨已征服','description':'破魔岗哨后的单格石桥，通往绝望平原'},
 {'id':'G-02-03','a':[4,3],'b':[4,4],'kind':'CURSED_PASS','unlock':'衣卒尔·堕落之牢已征服','description':'绝望平原与神罚之城交界'},
 {'id':'G-03-04','a':[3,5],'b':[3,6],'kind':'LAVA_BRIDGE','unlock':'神罚之城·城塞已征服','description':'火焰之河的唯一主线通道；城塞守门'},
 {'id':'G-04-05','a':[3,6],'b':[4,6],'kind':'SIDE_LAVA_SPUR','unlock':'神罚之城·城塞已征服 ＋ 玩家批准地狱熔炉支线','description':'火焰之河下游→地狱熔炉岔路（可选，不阻断终局）'},
]
# Validate every region independently connected, every Gate side-adjacent, exactly one castle per region
assert len(GRID)==7 and all(len(row)==7 for row in GRID)
assert ''.join(GRID).count('T')==1
assert ''.join(GRID).count('x')==20
pois=[];used=set()
for rid in sorted(POIS):
    entries=POIS[rid]
    assert sum(x[0]==Z[rid]['castleName'] for x in entries)==1
    ni=0
    for ordinal,(name,row,col,fcount,source,boss,notes,level_idx) in enumerate(entries,1):
        assert GRID[row-1][col-1]==str(rid),(name,row,col)
        assert (row,col) not in used,(row,col)
        used.add((row,col))
        castle=name==Z[rid]['castleName'];ni+=int(not castle)
        pid=f'D{rid:02d}-'+('C01' if castle else f'D{ni:02d}')
        challenges={d:LEVELS[rid][d][level_idx] for d in DIFS}
        recommend={d:min(challenges[d]+1,RECOMMEND_MAX[d]) for d in DIFS}
        tasks=[]
        if name=='衣卒尔·堕落之牢': tasks=['ACT04_IZUAL_RESCUED']
        if name=='地狱熔炉·核心': tasks=['ACT04_SOULSTONE_SHATTERED']
        if name=='混沌避难所': tasks=['ACT04_CHAOS_SEALS_5','ACT04_DIABLO_KILLED']
        pois.append({'id':pid,'name':name,'type':'Castle' if castle else 'Dungeon','regionId':rid,'row':row,'col':col,'floors':fcount,'source':source,'boss':boss,'notes':notes,'challenge':challenges,'recommended':recommend,'requiredForCastle':not castle,'branch':'SIDE' if Z[rid]['optional'] else 'MAIN','autoOrder':rid*100+ordinal,'fixedBossArena':castle,'questFacts':tasks,'contentStatus':'PROPOSAL_UNBALANCED'})
assert Counter(p['type'] for p in pois)=={'Dungeon':19,'Castle':5}
for rid,z in Z.items():
    tiles={(ri+1,ci+1) for ri,row in enumerate(GRID) for ci,ch in enumerate(row) if ch==str(rid)}
    assert 3<=len(tiles)<=8,(rid,tiles)
    seen={next(iter(tiles))};todo=list(seen)
    while todo:
        r,c=todo.pop()
        for nbr in ((r+1,c),(r-1,c),(r,c+1),(r,c-1)):
            if nbr in tiles and nbr not in seen: seen.add(nbr);todo.append(nbr)
    assert seen==tiles,(rid,'noncontiguous',tiles-seen)
    z.update(tiles=len(tiles),dungeonCount=sum(p['regionId']==rid and p['type']=='Dungeon' for p in pois),castlePoiId=next(p['id'] for p in pois if p['regionId']==rid and p['type']=='Castle'))
assert sum(z['tiles'] for z in Z.values())==28
for g in GATES:
    assert abs(g['a'][0]-g['b'][0])+abs(g['a'][1]-g['b'][1])==1
    aa=GRID[g['a'][0]-1][g['a'][1]-1];bb=GRID[g['b'][0]-1][g['b'][1]-1]
    assert (aa,bb) in (('T','1'),('1','2'),('2','3'),('3','4'),('4','5')),(g,aa,bb)
# Validate data scope world 7x7: only the 5 gates permit region-border movement; other adjacent boundaries are decorative barriers.

floors=[]
for p in pois:
    rid=p['regionId'];iscastle=p['type']=='Castle';total=p['floors']
    for i in range(1,total+1):
        last=i==total
        finale=p['name']=='混沌避难所'
        if finale:
            phase_names=['混沌避难所·入口前厅','混沌避难所·恶魔门廊','混沌避难所·封印西翼','混沌避难所·封印东翼','混沌避难所·封印中央','混沌避难所·迪亚波罗王座']
            floorname=phase_names[i-1]
        elif p['name']=='地狱熔炉·核心' and last: floorname='地狱熔炉·锻炉核心与海法斯特'
        elif p['name']=='衣卒尔·堕落之牢' and last: floorname='堕落之牢·衣卒尔拘束场'
        else: floorname=f'{p["name"]}·第{i}阶段'
        # DungeonFloor.Cell: active = Entry+Corridor+Combat+Elite+Event+Exit+BossArena
        base={1:19,2:23,3:25,4:30,5:26}[rid]
        if iscastle:base+=12
        if p['name']=='破碎石阶':base=16
        if p['name']=='恶魔岩窟':base=18
        if p['name']=='混沌避难所':base=35
        if p['name']=='衣卒尔·堕落之牢':base=34
        if p['name']=='地狱熔炉·核心':base=36
        active=base+4*(i-1)+(12 if iscastle and last else 0)
        bossarena=14 if finale and last else 11 if iscastle and last else 0
        entry=1;exitcell=0 if iscastle and last else 1
        # five Diablo seals across stages 3,4,5 and unique seal guardians on stages 3–5.
        seals=2 if finale and i in (3,4) else 1 if finale and i==5 else 0
        fixed_unique=1 if finale and i in (3,4,5) else 0
        event=1+(1 if seals>0 else 0) if active>=30 and i in (1,3,4,5) else 0
        if p['name']=='地狱熔炉·核心' and last:event=2
        corridor=max(4,round(active*(0.23 if rid<4 else 0.17)))
        elites=1+(1 if active>=32 else 0)+(1 if iscastle and active>=52 else 0)
        combat=active-entry-exitcell-event-corridor-elites-bossarena
        assert combat>=5,(p['id'],i,combat)
        rows=max(5,math.ceil(math.sqrt(active/0.7)))
        cols=max(rows,math.ceil(active/(rows*0.70)))
        assert rows*cols>=active
        for d in DIFS:
            difadd={'NORMAL':0,'NIGHTMARE':1,'HELL':2}[d]
            combat_per={1:3,2:2,3:2,4:2,5:2}[rid]+difadd
            elite_per={1:4,2:5,3:5,4:5,5:5}[rid]+difadd
            escort=3 if iscastle and last else 0
            boss_actor=1 if iscastle and last else 0
            kill=combat*combat_per+elites*elite_per+fixed_unique+boss_actor+escort
            challenge=p['challenge'][d]
            floorlevel=max({'NORMAL':27,'NIGHTMARE':62,'HELL':89}[d],challenge-(1 if i==1 and total>2 else 0))
            # No eventual boss escalation above the approved act level interval.
            floors.append({'ChapterId':'ACT04','DifficultyId':d,'PoiId':p['id'],'FloorIndex':i,'FloorCount':total,'FloorDisplayName':floorname,'StageMode':'PHASE_SEQUENCE' if iscastle else 'DUNGEON_FLOOR','SpawnProfileId':f'ACT04_R{rid:02d}_{d}','LayoutRows':rows,'LayoutCols':cols,'ActiveCellTarget':active,'EntryCellCount':entry,'CorridorCellCount':corridor,'CombatCellCount':combat,'EliteCellCount':elites,'EventCellCount':event,'ExitCellCount':exitcell,'BossArenaCellCount':bossarena,'SealActivationCount':seals,'FixedUniqueActorCount':fixed_unique,'FloorMonsterLevel':floorlevel,'PoiChallengeLevel':challenge,'RecommendedPlayerLevel':p['recommended'][d],'CombatMonstersPerCellTarget':combat_per,'EliteGroupMonsterTarget':elite_per,'BossActorCount':boss_actor,'BossEscortTarget':escort,'PlannedKillTarget':kill,'ContentStatus':'PROPOSAL_UNBALANCED'})
assert sum(f['SealActivationCount'] for f in floors if f['PoiId']=='D04-C01' and f['DifficultyId']=='NORMAL')==5
assert sum(f['FixedUniqueActorCount'] for f in floors if f['PoiId']=='D04-C01' and f['DifficultyId']=='NORMAL')==3
assert all(f['ActiveCellTarget']==sum(f[k] for k in ['EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount']) for f in floors)
kill_by={p['id']:{d:sum(f['PlannedKillTarget'] for f in floors if f['PoiId']==p['id'] and f['DifficultyId']==d) for d in DIFS} for p in pois}
for p in pois:p['plannedKills']=kill_by[p['id']]

# Approval-aware simulator: player authorization is explicit, never auto-granted.
class Simulator:
 def __init__(self,d='NORMAL'):
  self.d=d;self.done=set();self.castleApprovals=set();self.sideApprovals=set();self.order=[]
 def unlocked(self,r): return all(Z[x]['castlePoiId'] in self.done for x in Z[r]['parents'])
 def approved(self,r): return not Z[r]['optional'] or r in self.sideApprovals
 def ready(self,r): return all(p['id'] in self.done for p in pois if p['regionId']==r and p['type']=='Dungeon')
 def candidates(self):
  return sorted([p for p in pois if p['id'] not in self.done and self.unlocked(p['regionId']) and self.approved(p['regionId']) and (p['type']=='Dungeon' or (self.ready(p['regionId']) and p['id'] in self.castleApprovals))],key=lambda p:(p['challenge'][self.d],1 if Z[p['regionId']]['optional'] else 0,p['autoOrder'],p['id']))
 def step(self):
  can=self.candidates()
  if not can:return None
  p=can[0];self.done.add(p['id']);self.order.append(p['id']);return p
 def approveCastle(self,r):
  assert self.unlocked(r) and self.approved(r) and self.ready(r)
  self.castleApprovals.add(Z[r]['castlePoiId'])
 def approveSide(self,r):
  assert Z[r]['optional'] and self.unlocked(r)
  self.sideApprovals.add(r)
maincount=sum(not Z[p['regionId']]['optional'] for p in pois)
for d in DIFS:
 s=Simulator(d)
 while s.step():pass
 assert len(s.done)==3 and not s.candidates() and not s.sideApprovals,(d,'first Castle must block')
 for i in range(20):
  for region_id in range(1,5):
   if s.unlocked(region_id) and s.ready(region_id) and Z[region_id]['castlePoiId'] not in s.done:
    s.approveCastle(region_id)
  if not s.step():
   if len(s.done)==maincount:break
   raise AssertionError((d,'mainline deadlock',len(s.done)))
  while s.step():pass
 assert len(s.done)==maincount,(d,'mainline wrong count',len(s.done),maincount)
 assert Z[4]['castlePoiId'] in s.done
 assert not any(p['id'] in s.done for p in pois if Z[p['regionId']]['optional']),'side leak'
 # Side still unlockable after main completed (even after switching chapters)
 assert s.unlocked(5)
 s.approveSide(5)
 while s.step():pass
 assert s.ready(5)
 s.approveCastle(5)
 while s.step():pass
 assert len(s.done)==len(pois),(d,'side retroclear blocked')
assert all(LEVELS[r][d][k] in range({'NORMAL':27,'NIGHTMARE':62,'HELL':89}[d],{'NORMAL':31,'NIGHTMARE':65,'HELL':92}[d]+1) for r in Z for d in DIFS for k in (0,1))

def dump(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
def writecsv(path,rows):
 with path.open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
full={'schemaVersion':'ACT04_V0.1_PROPOSAL','chapterId':'ACT04','rows':7,'columns':7,'gridRows':GRID,'town':{'id':'TOWN_PANDEMONIUM','name':'群魔堡垒','tiles':[[6,1]],'safe':True},'regions':list(Z.values()),'gates':GATES,'poi':pois,'policy':{'selection':'CHALLENGE_ASC_MAIN_FIRST_STABLE','castleRequiresExplicitApproval':True,'sideRegionRequiresExplicitApproval':True,'sideDoesNotBlockMain':True,'returnToPreviousChapterSide':True,'reuseAcrossDifficulty':True,'authorizationIsPerDifficulty':True},'planningDisclaimer':'参考 D2 Act IV 的原典地点、Boss 和任务主题，扩展地下城与城塞是新增候选；并非 Godot 正式已实装配置。'}
dump(D/'ACT04_MAP_V0.1.json',full)
regrows=[{'ChapterId':'ACT04','RegionId':f'REGION_{z["id"]:02d}','RegionName':z['name'],'IsOptional':int(z['optional']),'UnlockParentCastleIds':';'.join(Z[p]['castlePoiId'] for p in z['parents']),'RequiresManualSideApproval':int(z['optional']),'CastlePoiId':z['castlePoiId'],'TileCount':z['tiles'],'DungeonCount':z['dungeonCount'],'Status':'PROPOSAL'} for z in Z.values()]
writecsv(D/'ACT04_REGION_V0.1.csv',regrows)
poirows=[{'ChapterId':'ACT04','PoiId':p['id'],'RegionId':f'REGION_{p["regionId"]:02d}','Type':p['type'],'DisplayName':p['name'],'TileRow':p['row'],'TileCol':p['col'],'RequiredForCastle':int(p['requiredForCastle']),'IsOptionalRegion':int(Z[p['regionId']]['optional']),'ManualCastleApproval':int(p['type']=='Castle'),'FloorCount':p['floors'],'AutoOrder':p['autoOrder'],'ChallengeNormal':p['challenge']['NORMAL'],'ChallengeNightmare':p['challenge']['NIGHTMARE'],'ChallengeHell':p['challenge']['HELL'],'RecommendNormal':p['recommended']['NORMAL'],'RecommendNightmare':p['recommended']['NIGHTMARE'],'RecommendHell':p['recommended']['HELL'],'SourceType':p['source'],'QuestFacts':';'.join(p['questFacts']),'FinalBossDisplayName':p['boss'],'KillBudgetNormal':p['plannedKills']['NORMAL'],'KillBudgetNightmare':p['plannedKills']['NIGHTMARE'],'KillBudgetHell':p['plannedKills']['HELL'],'ConfigStatus':'PROPOSAL_UNBALANCED'} for p in pois]
writecsv(D/'ACT04_POI_V0.1.csv',poirows)
writecsv(D/'ACT04_FLOOR_PLAN_V0.1.csv',floors)
gaterows=[{'GateId':g['id'],'FromRow':g['a'][0],'FromCol':g['a'][1],'ToRow':g['b'][0],'ToCol':g['b'][1],'TravelKind':g['kind'],'UnlockDescription':g['unlock'],'Notes':g['description']} for g in GATES]
writecsv(D/'ACT04_GATES_V0.1.csv',gaterows)
kills={d:{'main':sum(p['plannedKills'][d] for p in pois if not Z[p['regionId']]['optional']),'side':sum(p['plannedKills'][d] for p in pois if Z[p['regionId']]['optional'])} for d in DIFS}
dump(D/'ACT04_KILL_ESTIMATE_V0.1.json',{'byDifficulty':kills,'perPoi':kill_by,'definition':'全规划遭遇清完：普通格×单格怪 + 精英格×编队怪 + 固定封印首领 + Boss + Boss固定随从，不含重刷与临时召唤。五处封印本身是事件事实，不按怪物计数。'})
print('SUCCESS: chapter4 grid 7x7 / regions5 / dungeon19 / castle5 / stages',len(floors)//3,'records',len(floors),'kills',kills)
