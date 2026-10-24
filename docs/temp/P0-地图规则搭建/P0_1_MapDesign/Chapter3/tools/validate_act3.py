"""Independent content checks (not Godot integration tests)."""
from pathlib import Path
import csv,json,collections
root=Path('/mnt/data/D2_Act3_Map_V0_1');dat=root/'data'
M=json.loads((dat/'ACT03_MAP_V0.1.json').read_text(encoding='utf-8'))
with (dat/'ACT03_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as f:F=list(csv.DictReader(f))
assert len(M['gridRows'])==10 and all(len(x)==10 for x in M['gridRows'])
assert len(M['regions'])==9 and len(M['poi'])==53
assert collections.Counter(p['type'] for p in M['poi'])=={'Dungeon':44,'Castle':9}
assert len({p['id'] for p in M['poi']})==53
assert len({(p['row'],p['col']) for p in M['poi']})==53
for p in M['poi']:assert M['gridRows'][p['row']-1][p['col']-1]==str(p['regionId'])
assert sum(s.count('x') for s in M['gridRows'])==38
assert sum(s.count('T') for s in M['gridRows'])==2
assert len(F)==273
for p in M['poi']:
 ff=[f for f in F if f['PoiId']==p['id']]
 assert len(ff)==3*p['floors'],p['id']
 for d in ('NORMAL','NIGHTMARE','HELL'):
  fx=sorted((f for f in ff if f['DifficultyId']==d),key=lambda x:int(x['FloorIndex']))
  assert len(fx)==p['floors']
  assert [int(x['FloorIndex']) for x in fx]==list(range(1,p['floors']+1))
  assert all(int(x['PoiChallengeLevel'])==p['challenge'][d] for x in fx)
  for f in fx:
   assert int(f['ActiveCellTarget']) == sum(int(f[key]) for key in ['EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount'])
   assert int(f['LayoutRows'])*int(f['LayoutCols'])>=int(f['ActiveCellTarget'])
   expected=int(f['CombatCellCount'])*int(f['CombatMonstersPerCellTarget'])+int(f['EliteCellCount'])*int(f['EliteGroupMonsterTarget'])+int(f['BossActorCount'])+int(f['BossEscortTarget'])
   assert int(f['PlannedKillTarget'])==expected,(p['id'],d,f['FloorIndex'])
   assert int(f['BossActorCount'])==int(p['type']=='Castle' and int(f['FloorIndex'])==p['floors'])
for gate in M['gates']:
 assert abs(gate['a'][0]-gate['b'][0])+abs(gate['a'][1]-gate['b'][1])==1
 assert all(1<=a<=10 for a in gate['a']+gate['b'])
by_id={z['id']:z for z in M['regions']}
for z in M['regions']:
 assert z['dungeonCount']==sum(p['regionId']==z['id'] and p['type']=='Dungeon' for p in M['poi'])
 assert 3<=z['tiles']<=8
 assert sum(M['gridRows'][r].count(str(z['id'])) for r in range(10))==z['tiles']
 assert z['castlePoiId'] in {p['id'] for p in M['poi'] if p['regionId']==z['id'] and p['type']=='Castle'}
 assert all(parent<z['id'] for parent in z['parents'])
assert {p['name'] for p in M['poi'] if p['mandatoryQuestFacts']}=={'蜘蛛洞窟','剥皮地牢','库拉斯下水道'}
# boss dungeon internal split and no self dependency
final=next(p for p in M['poi'] if p['regionId']==7 and p['type']=='Castle')
assert final['floors']==5 and final['name']=='崔凡克·议会神殿'
assert [x['FloorDisplayName'] for x in F if x['PoiId']==final['id'] and x['DifficultyId']=='NORMAL'][2:]==['憎恨囚牢·第1层','憎恨囚牢·第2层','憎恨囚牢·第3层']
kill=json.loads((dat/'ACT03_KILL_ESTIMATE_V0.1.json').read_text(encoding='utf-8'))
for d in ('NORMAL','NIGHTMARE','HELL'):
 for typ,expected in [('main',False),('side',True)]:
  total=sum(int(f['PlannedKillTarget']) for f in F if f['DifficultyId']==d and by_id[next(p['regionId'] for p in M['poi'] if p['id']==f['PoiId'])]['optional']==expected)
  assert total==kill['byDifficulty'][d][typ]
print('VALIDATE_PASS: 100 cells, 9 connected-region plans, 53 POIs, 9 gates, 91 floors x3, split budget, quest facts, internal Durance, kill aggregation')
