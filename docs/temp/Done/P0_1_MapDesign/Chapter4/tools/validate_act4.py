"""Static content contract; DOES NOT assert Godot runtime integration."""
import csv,json,collections
from pathlib import Path
r=Path('/mnt/data/D2_Act4_Map_V0_1');d=r/'data'
m=json.loads((d/'ACT04_MAP_V0.1.json').read_text(encoding='utf-8'))
with (d/'ACT04_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as fh:floors=list(csv.DictReader(fh))
k=json.loads((d/'ACT04_KILL_ESTIMATE_V0.1.json').read_text(encoding='utf-8'))
assert len(m['gridRows'])==7 and all(len(x)==7 for x in m['gridRows'])
assert ''.join(m['gridRows']).count('x')==20
assert ''.join(m['gridRows']).count('T')==1
assert len(m['regions'])==5 and len(m['poi'])==24
assert collections.Counter(p['type'] for p in m['poi'])=={'Dungeon':19,'Castle':5}
assert len({p['id'] for p in m['poi']})==24
assert len({(p['row'],p['col']) for p in m['poi']})==24
for p in m['poi']:assert m['gridRows'][p['row']-1][p['col']-1]==str(p['regionId'])
assert len(floors)==231 and sum(p['floors'] for p in m['poi'])==77
for p in m['poi']:
 ff=[f for f in floors if f['PoiId']==p['id']]
 assert len(ff)==3*p['floors'],p['id']
 for dif in ('NORMAL','NIGHTMARE','HELL'):
  subset=sorted((f for f in ff if f['DifficultyId']==dif),key=lambda f:int(f['FloorIndex']))
  assert [int(f['FloorIndex']) for f in subset]==list(range(1,p['floors']+1))
  assert all(int(f['PoiChallengeLevel'])==p['challenge'][dif] for f in subset)
  for f in subset:
   active=sum(int(f[x]) for x in ['EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount'])
   assert active==int(f['ActiveCellTarget'])
   assert int(f['LayoutRows'])*int(f['LayoutCols'])>=active
   expected=int(f['CombatCellCount'])*int(f['CombatMonstersPerCellTarget'])+int(f['EliteCellCount'])*int(f['EliteGroupMonsterTarget'])+int(f['FixedUniqueActorCount'])+int(f['BossActorCount'])+int(f['BossEscortTarget'])
   assert expected==int(f['PlannedKillTarget'])
   assert int(f['BossActorCount'])==int(p['type']=='Castle' and int(f['FloorIndex'])==p['floors'])
for r in m['regions']:
 assert sum(row.count(str(r['id'])) for row in m['gridRows'])==r['tiles']
 assert r['dungeonCount']==sum(p['regionId']==r['id'] and p['type']=='Dungeon' for p in m['poi'])
 assert 3<=r['tiles']<=8
 for p in r['parents']:assert p<r['id']
 assert r['castlePoiId'] in {p['id'] for p in m['poi'] if p['regionId']==r['id'] and p['type']=='Castle'}
for gate in m['gates']:
 assert abs(gate['a'][0]-gate['b'][0])+abs(gate['a'][1]-gate['b'][1])==1
 assert all(1<=k<=7 for k in gate['a']+gate['b'])
 assert gate['a']!=gate['b']
assert len(m['gates'])==5
chaos=next(p for p in m['poi'] if p['name']=='混沌避难所')
assert chaos['type']=='Castle' and chaos['floors']==6
for dif in ('NORMAL','NIGHTMARE','HELL'):
 fs=sorted((f for f in floors if f['PoiId']==chaos['id'] and f['DifficultyId']==dif),key=lambda f:int(f['FloorIndex']))
 assert sum(int(f['SealActivationCount']) for f in fs)==5
 assert sum(int(f['FixedUniqueActorCount']) for f in fs)==3
 assert sum(int(f['BossActorCount']) for f in fs)==1
 for st in ('main','side'):
  optional=(st=='side')
  regmap={r['id']:r['optional'] for r in m['regions']}
  oid={p['id']:regmap[p['regionId']] for p in m['poi']}
  subtotal=sum(int(f['PlannedKillTarget']) for f in floors if f['DifficultyId']==dif and oid[f['PoiId']]==optional)
  assert k['byDifficulty'][dif][st]==subtotal,(dif,st)
# verify only pre-approved zone can be side and no mandatory quest in side
assert [r['id'] for r in m['regions'] if r['optional']]==[5]
assert all(not p['requiredForCastle'] for p in m['poi'] if p['type']=='Castle')
assert all(not p['questFacts'] for p in m['poi'] if p['regionId']==5 and p['type']=='Dungeon')
print('PASS: 49 world cells; 5 regions; 19+5 POIs; 5 legal gates; 77 floors * 3; 5 seals + 3 wardens; all budgets; branch-authorization scope')
