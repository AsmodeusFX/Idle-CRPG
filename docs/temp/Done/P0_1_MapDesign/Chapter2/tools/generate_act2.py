from __future__ import annotations
import csv,json,math,collections,pathlib,html
from collections import deque
from pathlib import Path

ROOT=Path('/mnt/data/D2_Act2_Map_V0_1')
D=ROOT/'data'; DOC=ROOT/'docs'; PRE=ROOT/'preview'
for p in [D,DOC,PRE]:p.mkdir(parents=True,exist_ok=True)
GRID=['xxx333xxx','xx223334x','882234449','882114499','xTT114559','x6115555x','x6665777x','x6667777x','xxxxx7xxx']
REGIONS=[
 (1,'碎石荒地',False,[], '碎石关隘', '从鲁·高因驶出，短而密集的第一片荒地'),
 (2,'干燥高地',False,[1], '高地岩堡','死亡之殿保留三层深度'),
 (3,'遥远绿洲',False,[2], '绿洲守卫塔','虫巢和绿洲地形的反差'),
 (4,'遗失城市',False,[3], '遗失之城城门','两条主线分岔的枢纽'),
 (5,'群蛇峡谷',False,[4], '利爪蝮蛇神殿','蝮蛇护符线，偏紧凑神殿'),
 (6,'皇宫与神秘庇护所',False,[4], '神秘庇护所','从鲁·高因皇宫侧进入，而非群蛇峡谷之后'),
 (7,'术士峡谷',False,[5,6], '塔·拉夏古墓（真墓）','六座假墓＋真正的古墓要塞，都瑞尔'),
 (8,'鲁·高因下水道',True,[], '下水道深处','拉达门特支线，章节初始即可发现'),
 (9,'古代通道',True,[4], '遗迹之心','从遗失城市进入的考古支线'),
]
ZONE={r[0]:{'id':r[0],'name':r[1],'optional':r[2],'parents':r[3],'castleName':r[4],'description':r[5]} for r in REGIONS}
# {id: [(name, row, col, floors, originalType, boss, flavor, levelIndex)]}
POIS={
 1:[
 ('碎石遗迹',5,4,1,'原创占位','','开局短小石窟',0),
 ('石制古墓',4,4,2,'原典','', 'D2 Stony Tomb，两层',0),
 ('流沙洞穴',4,5,1,'原创占位','','沙漠小型洞穴',0),
 ('旱地猎手营',6,3,1,'原创占位','','敌人营地',1),
 ('破碎石廊',6,4,2,'原创占位','','狭窄岩石通道',1),
 ('碎石关隘',5,5,2,'原创占位','堕落荒野守卫（占位）','区域 Castle',1)],
 2:[
 ('死亡之殿',4,3,3,'原典','','赫拉迪克方块于深层',0),
 ('干燥洞穴',3,3,1,'原创占位','','过渡地牢',0),
 ('远古埋骨窟',2,3,1,'原创占位','','石墓变体',0),
 ('沙盗哨站',3,4,1,'原创占位','','人形精英据点',1),
 ('高地岩堡',2,4,2,'原创占位','高地守卫（占位）','区域 Castle',1)],
 3:[
 ('蛆虫巢穴',2,5,3,'原典','', 'D2 Maggot Lair，三层与窄道',0),
 ('枯泉虫巢',1,4,1,'原创占位','','小型虫群地牢',0),
 ('干枯绿洲',1,5,2,'原创占位','','远景水源及洞穴',0),
 ('隐沙墓室',1,6,1,'原创占位','','石墓',1),
 ('绿洲危径',2,6,1,'原创占位','','小型狭路地牢',1),
 ('绿洲守卫塔',2,7,2,'原创占位','绿洲守卫（占位）','区域 Castle',1)],
 4:[
 ('失落街区',3,6,1,'原典改造','','城市废墟地牢化',0),
 ('古城地窖',3,7,2,'原创占位','','古城地下回廊',0),
 ('碎裂神殿',3,8,1,'原创占位','','祭祀室',0),
 ('沙丘遗殿',4,6,1,'原创占位','','古代神殿',1),
 ('古代碑廊',4,7,2,'原创占位','','古代碑廊',1),
 ('遗失之城城门',5,6,2,'原创占位','失落城守卫（占位）','区域 Castle',1)],
 5:[
 ('蛇谷岗哨',5,7,1,'原创占位','','蛇人看守据点',0),
 ('毒蛇祭坑',5,8,1,'原创占位','','毒系怪物',0),
 ('缠蛇洞穴',6,5,1,'原创占位','','洞穴',0),
 ('毒牙侧殿',6,6,2,'原创占位','','蝮蛇神庙前殿',1),
 ('利爪蝮蛇神殿',6,7,2,'原典','牙皮（Fangskin）','Castle：第二层末端获取蝮蛇护符',1)],
 6:[
 ('皇宫后宫',6,2,2,'原典','','Harem，两层',0),
 ('皇宫地窖',7,2,3,'原典','','Palace Cellar，三层',0),
 ('皇家护卫营',7,3,1,'原创占位','','宫殿内护卫战',0),
 ('宫廷仪式厅',7,4,1,'原创占位','','仪式场地',1),
 ('传送秘廊',8,2,1,'原创占位','','魔法门通道',1),
 ('神秘庇护所',8,4,2,'原典改造','召唤者（The Summoner）','Castle：召唤者，胜利激活术士峡谷传送门',1)],
 7:[
 ('塔·拉夏古墓Ⅰ（假墓）',7,6,1,'原典改造','','六座假墓之一',0),
 ('塔·拉夏古墓Ⅱ（假墓）',7,7,1,'原典改造','','六座假墓之一',0),
 ('塔·拉夏古墓Ⅲ（假墓）',7,8,2,'原典改造','','六座假墓之一',0),
 ('塔·拉夏古墓Ⅳ（假墓）',8,5,1,'原典改造','','六座假墓之一',0),
 ('塔·拉夏古墓Ⅴ（假墓）',8,6,2,'原典改造','','六座假墓之一',0),
 ('塔·拉夏古墓Ⅵ（假墓）',8,7,2,'原典改造','','六座假墓之一',0),
 ('塔·拉夏古墓（真墓）',8,8,3,'原典改造','都瑞尔（Duriel）','Castle：古墓、密室、都瑞尔专属竞技场',1)],
 8:[
 ('排污渠入口',3,1,1,'原典改造','','下水道入口展开',0),
 ('旧水渠',3,2,1,'原创占位','','下水道支线小房间',0),
 ('污秽洞窟',4,1,1,'原创占位','','下水道精英',0),
 ('下水道深处',4,2,3,'原典改造','拉达门特（Radament）','Castle：下水道三层深处拉达门特',1)],
 9:[
 ('古代通道',3,9,1,'原典','','Ancient Tunnels',0),
 ('被沙掩埋的庭院',4,8,1,'原创占位','','古城前厅',0),
 ('封存祭室',4,9,2,'原创占位','','古代祭坛',0),
 ('遗迹之心',5,9,2,'原创占位','遗迹祭司（占位）','Castle：古代通道支线收束',1)]
}
LEVELS={
 1:{'NORMAL':(13,14),'NIGHTMARE':(50,50),'HELL':(79,79)},
 2:{'NORMAL':(14,15),'NIGHTMARE':(51,51),'HELL':(80,80)},
 3:{'NORMAL':(15,16),'NIGHTMARE':(52,52),'HELL':(80,81)},
 4:{'NORMAL':(16,17),'NIGHTMARE':(53,53),'HELL':(81,82)},
 5:{'NORMAL':(18,19),'NIGHTMARE':(54,54),'HELL':(82,82)},
 6:{'NORMAL':(18,19),'NIGHTMARE':(54,54),'HELL':(82,82)},
 7:{'NORMAL':(19,20),'NIGHTMARE':(55,55),'HELL':(83,83)},
 8:{'NORMAL':(13,14),'NIGHTMARE':(50,50),'HELL':(79,79)},
 9:{'NORMAL':(17,18),'NIGHTMARE':(53,53),'HELL':(82,82)},
}
GATES=[
 {'id':'G-T-01','a':[5,3],'b':[5,4],'kind':'ROAD','unlock':'初始','description':'鲁·高因东门，直接进入碎石荒地'},
 {'id':'G-T-08','a':[5,2],'b':[4,2],'kind':'ROAD','unlock':'鲁·高因下水道支线批准','description':'下水道入口，初始可见需批准'},
 {'id':'G-01-02','a':[4,4],'b':[4,3],'kind':'ROAD','unlock':'碎石荒地 Castle 击败','description':'荒漠与高地关口'},
 {'id':'G-02-03','a':[2,4],'b':[2,5],'kind':'ROAD','unlock':'干燥高地 Castle 击败','description':'高地转入绿洲'},
 {'id':'G-03-04','a':[3,5],'b':[3,6],'kind':'ROAD','unlock':'遥远绿洲 Castle 击败','description':'绿洲转遗失城市'},
 {'id':'G-04-05','a':[5,6],'b':[5,7],'kind':'ROAD','unlock':'遗失城市 Castle 击败','description':'遗失城市进群蛇峡谷'},
 {'id':'G-T-06','a':[5,2],'b':[6,2],'kind':'PALACE_DOOR','unlock':'遗失城市 Castle 击败','description':'从鲁·高因皇宫入口进入第二主线'},
 {'id':'G-04-09','a':[3,8],'b':[3,9],'kind':'ROAD','unlock':'遗失城市 Castle 击败，且古代通道支线批准','description':'遗失城市东缘古代通道支线'},
 {'id':'G-06-07','a':[8,4],'b':[8,5],'kind':'ARCANE_PORTAL','unlock':'群蛇峡谷及皇宫神秘庇护所两个 Castle 均已征服','description':'神秘庇护所红色传送门 → 术士峡谷；并非普通道路'}
]

# POI rows and metadata
poilist=[]; coords=set(); counttypes=collections.Counter()
for k in sorted(POIS):
    records=POIS[k]
    assert sum(1 for x in records if x[0]==ZONE[k]['castleName'])==1,k
    for idx, (name,r,c,fc,source,boss,note,levelIndex) in enumerate(records,1):
        is_castle=(name==ZONE[k]['castleName'])
        poi_id=f'A{k:02d}-'+ ('C01' if is_castle else f'D{sum(1 for x in records[:idx] if x[0]!=ZONE[k]["castleName"]):02d}')
        assert GRID[r-1][c-1]==str(k),(k,name,r,c,GRID[r-1][c-1]);assert(r,c)not in coords,(r,c)
        coords.add((r,c));counttypes['Castle' if is_castle else 'Dungeon']+=1
        lev={dif: LEVELS[k][dif][levelIndex] for dif in ['NORMAL','NIGHTMARE','HELL']}
        recommended={dif: min(lev[dif]+1,{'NORMAL':21,'NIGHTMARE':56,'HELL':84}[dif]) for dif in lev}
        poilist.append({'id':poi_id,'name':name,'type':'Castle' if is_castle else 'Dungeon','regionId':k,'row':r,'col':c,'floors':fc,'source':source,'boss':boss,'notes':note,'challenge':lev,'recommended':recommended,'requiredForCastle':not is_castle,'branch':'SIDE' if ZONE[k]['optional'] else 'MAIN','autoOrder':k*100+idx,'fixedBossArena':bool(boss) or is_castle})
assert len(poilist)==49 and counttypes['Dungeon']==40 and counttypes['Castle']==9

# invariant: region connectivity (inside same region), town can be 2 cells
for rid,region in ZONE.items():
    tiles={(r+1,c+1) for r,row in enumerate(GRID) for c,ch in enumerate(row) if ch==str(rid)}
    assert 3<=len(tiles)<=8, (rid,len(tiles))
    stack=[next(iter(tiles))]; visited=set(stack)
    while stack:
        r,c=stack.pop()
        for xy in ((r+1,c),(r-1,c),(r,c+1),(r,c-1)):
            if xy in tiles and xy not in visited:visited.add(xy); stack.append(xy)
    assert visited==tiles, (rid,'disconnected',tiles-visited)
    region['tiles']=len(tiles)
    region['dungeonCount']=sum(p['regionId']==rid and p['type']=='Dungeon' for p in poilist)
    region['castlePoiId']=next(p['id'] for p in poilist if p['regionId']==rid and p['type']=='Castle')
assert sum(1 for s in ''.join(GRID) if s=='T')==2
assert len(''.join(GRID))==81
for g in GATES:
    a,b=g['a'],g['b'];assert abs(a[0]-b[0])+abs(a[1]-b[1])==1, g
assert len([p for p in poilist if p['regionId']==7])==7
assert len([p for p in poilist if p['regionId']==7 and p['type']=='Dungeon'])==6

# evaluate floors: deliberately planning budgets, not generator guaranteed results
floorplan=[]
for p in poilist:
    for f in range(1,p['floors']+1):
        is_last=f==p['floors']; is_boss=is_last and p['type']=='Castle'
        # escalate size based on chapter location and special nostalgic dungeons
        base=12 + min(p['regionId'],7)*2
        if p['regionId']==8: base=12
        if p['regionId']==9: base=17
        if p['regionId']==7: base=23
        if p['type']=='Castle': base+=7
        if '蛆虫巢穴' in p['name']: base=17
        if '死亡之殿' in p['name']: base=21
        if '皇宫地窖' in p['name']: base=24
        if '假墓' in p['name']: base=22 + (f-1)*2
        if '真墓' in p['name']:base=29
        active=base + (f-1)*3
        if is_boss:active+=5
        arena=(6 if is_boss else 0)
        entrance=1;exitcell=0 if is_boss else 1
        event=1 if active>=21 and f==1 and not is_boss else 0
        corridor=max(3, round(active*0.24))
        elite=1 + (1 if active>=30 else 0) + (1 if is_boss and active>=34 else 0)
        combat=active-entrance-exitcell-event-corridor-elite-arena
        assert combat>=2,(p['id'],f,combat)
        rows=max(4,math.ceil(math.sqrt(active/0.67)))
        cols=max(rows,math.ceil(active/(rows*0.67)))
        assert rows*cols>=active
        for diff in ['NORMAL','NIGHTMARE','HELL']:
            combatPer={'NORMAL':3,'NIGHTMARE':4,'HELL':5}[diff]
            escort={'NORMAL':2,'NIGHTMARE':3,'HELL':4}[diff]
            bossEscort=2 if is_boss else 0
            kills=combat*combatPer+elite*(1+escort)+(1+bossEscort if is_boss else 0)
            lv=p['challenge'][diff]
            floorplan.append({'ChapterId':'ACT02','DifficultyId':diff,'PoiId':p['id'],'FloorIndex':f,'FloorCount':p['floors'],'FloorDisplayName':f'{p["name"]}·第{f}层','LayoutRows':rows,'LayoutCols':cols,'ActiveCellTarget':active,'EntryCellCount':entrance,'CorridorCellCount':corridor,'CombatCellCount':combat,'EliteCellCount':elite,'EventCellCount':event,'ExitCellCount':exitcell,'BossArenaCellCount':arena,'FloorMonsterLevel':max(({'NORMAL':13,'NIGHTMARE':50,'HELL':79}[diff]),lv-1 if f==1 and p['floors']>=2 else lv),'PoiChallengeLevel':lv,'RecommendedPlayerLevel':p['recommended'][diff],'CombatMonstersPerCellTarget':combatPer,'EliteGroupMonsterTarget':1+escort,'BossActorCount':1 if is_boss else 0,'BossEscortTarget':bossEscort,'PlannedKillTarget':kills,'ContentStatus':'PROPOSAL_UNBALANCED'})
assert all(sum(row[key] for key in ['EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount'])==row['ActiveCellTarget'] for row in floorplan)

# expected kill summary by POI by difficulty and maps
killsByPoi={}
for p in poilist:
    killsByPoi[p['id']]={d:sum(f['PlannedKillTarget'] for f in floorplan if f['PoiId']==p['id'] and f['DifficultyId']==d) for d in ['NORMAL','NIGHTMARE','HELL']}
    p['plannedKills']=killsByPoi[p['id']]

# Auto-adventure state simulation: blocked without Castle auth; authorized Castle can be completed; side manual
class Sim:
 def __init__(self,diff='NORMAL'):
    self.diff=diff;self.completed=set();self.auth_castles=set();self.auth_side=set();self.log=[]
 def region_unlocked(self,rid):
    parents=ZONE[rid]['parents']
    return all(ZONE[i]['castlePoiId'] in self.completed for i in parents)
 def region_authorized(self,rid):return not ZONE[rid]['optional'] or rid in self.auth_side
 def castle_ready(self,rid):
    return all(p['id'] in self.completed for p in poilist if p['regionId']==rid and p['type']=='Dungeon')
 def candidates(self):
    return sorted([p for p in poilist if p['id'] not in self.completed and self.region_unlocked(p['regionId']) and self.region_authorized(p['regionId']) and (p['type']=='Dungeon' or (self.castle_ready(p['regionId']) and p['id'] in self.auth_castles))],key=lambda p:(p['challenge'][self.diff],1 if ZONE[p['regionId']]['optional'] else 0,p['autoOrder'],p['id']))
 def step(self):
    cs=self.candidates()
    if not cs:return None
    p=cs[0];self.completed.add(p['id']);self.log.append(p['id']);return p
 def approve_castle(self,rid):
    assert self.region_unlocked(rid) and self.region_authorized(rid) and self.castle_ready(rid)
    self.auth_castles.add(ZONE[rid]['castlePoiId'])
 def approve_side(self,rid):
    assert ZONE[rid]['optional'] and self.region_unlocked(rid)
    self.auth_side.add(rid)

for difficulty in ['NORMAL','NIGHTMARE','HELL']:
 s=Sim(difficulty)
 # no prior approval means stop when first Castle reached
 for _ in range(50):
    if not s.step(): break
 assert len(s.completed)==5,(difficulty,len(s.completed)) # Rocky five dungeons
 # auto approve castle when eligible to test final, never approve sides
 loops=0
 while len(s.completed)<sum(not ZONE[p['regionId']]['optional'] for p in poilist) and loops<100:
    loops+=1
    q=s.step()
    if not q:
        ready=[i for i in range(1,8) if s.region_unlocked(i) and s.castle_ready(i) and ZONE[i]['castlePoiId'] not in s.completed]
        assert ready,(difficulty,loops,len(s.completed),'deadlock')
        for i in ready:s.approve_castle(i)
 assert len(s.completed)==sum(not ZONE[p['regionId']]['optional'] for p in poilist), (difficulty,'not-cleared')
 assert ZONE[7]['castlePoiId'] in s.completed
 assert not any(p['regionId'] in (8,9) for p in poilist if p['id'] in s.completed)
 # verify returning to side after finishing main; approval needed once each
 for i in [8,9]:s.approve_side(i)
 while len(s.completed)<len(poilist) and loops<150:
    loops+=1
    q=s.step()
    if not q:
        ready=[i for i in [8,9] if s.region_unlocked(i) and s.castle_ready(i) and ZONE[i]['castlePoiId'] not in s.completed]
        assert ready
        for i in ready:s.approve_castle(i)
 assert len(s.completed)==len(poilist)

# outputs
json_dump=lambda path,obj: path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
json_dump(D/'ACT02_MAP_V0.1.json',{
 'schemaVersion':'ACT02_V0.1_PROPOSAL','chapterId':'ACT02','rows':9,'columns':9,'gridRows':GRID,'town':{'id':'TOWN_LUT_GHOLEIN','name':'鲁·高因','tiles':[[5,2],[5,3]],'portalNote':'皇宫入口由城市向南开放'},'regions':list(ZONE.values()),'gates':GATES,'poi':poilist,'planningDisclaimer':'仅内容配置方案，非当前 Godot 读取器已验证 schema','policy':{'selection':'CHALLENGE_ASC_MAIN_FIRST_STABLE','castleRequiresExplicitApproval':True,'sideRegionRequiresExplicitApproval':True,'sideDoesNotBlockMain':True,'reuseAcrossDifficulty':True}})

def write_csv(path,rows,fields):
 with path.open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader();w.writerows(rows)
write_csv(D/'ACT02_REGION_V0.1.csv',[{'ChapterId':'ACT02','RegionId':f'REGION_{r["id"]:02d}','RegionName':r['name'],'IsOptional':int(r['optional']),'UnlockParentCastleIds':';'.join(ZONE[i]['castlePoiId'] for i in r['parents']),'RequiresManualSideApproval':int(r['optional']),'CastlePoiId':r['castlePoiId'],'TileCount':r['tiles'],'DungeonCount':r['dungeonCount'],'Status':'PROPOSAL'} for r in ZONE.values()],['ChapterId','RegionId','RegionName','IsOptional','UnlockParentCastleIds','RequiresManualSideApproval','CastlePoiId','TileCount','DungeonCount','Status'])
poirows=[]
for p in poilist:
 row={'ChapterId':'ACT02','PoiId':p['id'],'RegionId':f'REGION_{p["regionId"]:02d}','Type':p['type'],'DisplayName':p['name'],'TileRow':p['row'],'TileCol':p['col'],'RequiredForCastle':int(p['requiredForCastle']),'IsOptionalRegion':int(ZONE[p['regionId']]['optional']),'ManualCastleApproval':int(p['type']=='Castle'),'FloorCount':p['floors'],'AutoOrder':p['autoOrder'],'ChallengeNormal':p['challenge']['NORMAL'],'ChallengeNightmare':p['challenge']['NIGHTMARE'],'ChallengeHell':p['challenge']['HELL'],'RecommendNormal':p['recommended']['NORMAL'],'RecommendNightmare':p['recommended']['NIGHTMARE'],'RecommendHell':p['recommended']['HELL'],'SourceType':p['source'],'FinalBossDisplayName':p['boss'],'KillBudgetNormal':p['plannedKills']['NORMAL'],'KillBudgetNightmare':p['plannedKills']['NIGHTMARE'],'KillBudgetHell':p['plannedKills']['HELL'],'ConfigStatus':'PROPOSAL_UNBALANCED'}
 poirows.append(row)
write_csv(D/'ACT02_POI_V0.1.csv',poirows,list(poirows[0]))
write_csv(D/'ACT02_FLOOR_PLAN_V0.1.csv',floorplan,list(floorplan[0]))
write_csv(D/'ACT02_GATES_V0.1.csv',[{'GateId':g['id'],'FromRow':g['a'][0],'FromCol':g['a'][1],'ToRow':g['b'][0],'ToCol':g['b'][1],'TravelKind':g['kind'],'UnlockDescription':g['unlock'],'Notes':g['description']} for g in GATES],['GateId','FromRow','FromCol','ToRow','ToCol','TravelKind','UnlockDescription','Notes'])
killtotals={diff:{'main':sum(p['plannedKills'][diff] for p in poilist if not ZONE[p['regionId']]['optional']),'side':sum(p['plannedKills'][diff] for p in poilist if ZONE[p['regionId']]['optional'])} for diff in ['NORMAL','NIGHTMARE','HELL']}
json_dump(D/'ACT02_KILL_ESTIMATE_V0.1.json',{'byDifficulty':killtotals,'definition':'规划击杀量=每层战斗格×每战斗格目标怪物数+精英格×(精英+随从)+Boss演员与随从，不包含回刷、动态刷新、阻路未清怪、召唤增援','perPoi':killsByPoi})
print('COUNTS',counttypes,'area',sum(v['tiles'] for v in ZONE.values()),'town',2,'void',sum(x=='x' for x in ''.join(GRID)),'floors',len(floorplan)//3, 'floor rows',len(floorplan),'kills',killtotals)
