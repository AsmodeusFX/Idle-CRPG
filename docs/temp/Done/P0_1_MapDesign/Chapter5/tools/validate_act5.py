from pathlib import Path
from collections import Counter, defaultdict
import csv,json
R=Path(__file__).resolve().parents[1];D=R/'data'
M=json.loads((D/'ACT05_MAP_V0.1.json').read_text(encoding='utf-8'))
K=json.loads((D/'ACT05_KILL_ESTIMATE_V0.1.json').read_text(encoding='utf-8'))
S=json.loads((D/'ACT05_SIMULATION_V0.1.json').read_text(encoding='utf-8'))
with (D/'ACT05_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as f: floors=list(csv.DictReader(f))
assert M['rows']==11 and M['columns']==11 and len(M['gridRows'])==11
assert all(len(row)==11 for row in M['gridRows'])
assert ''.join(M['gridRows']).count('x')==45
assert ''.join(M['gridRows']).count('T')==1
regions={r['id']:r for r in M['regions']};assert len(regions)==11
assert sum(r['tiles'] for r in regions.values())==75
assert sum(r['optional'] for r in regions.values())==2
pmap={p['id']:p for p in M['poi']}
assert Counter(p['type'] for p in M['poi'])=={'Dungeon':53,'Castle':11}
assert len(pmap)==64
occupied=set()
for p in M['poi']:
 point=(p['row'],p['col']);assert point not in occupied,point
 occupied.add(point)
 exp=str(p['regionId']) if p['regionId']<=9 else ('A' if p['regionId']==10 else 'B')
 assert M['gridRows'][point[0]-1][point[1]-1]==exp
for region_id,r in regions.items():
 cells={(i+1,j+1) for i,line in enumerate(M['gridRows']) for j,ch in enumerate(line) if ch==(str(region_id) if region_id<=9 else ('A' if region_id==10 else 'B'))}
 assert len(cells)==r['tiles'] and 3<=len(cells)<=8
 seen={next(iter(cells))};queue=list(seen)
 while queue:
  row,col=queue.pop()
  for next_ in [(row+1,col),(row-1,col),(row,col+1),(row,col-1)]:
   if next_ in cells and next_ not in seen:seen.add(next_);queue.append(next_)
 assert seen==cells
 assert sum(p['regionId']==region_id and p['type']=='Castle' for p in M['poi'])==1
 assert sum(p['regionId']==region_id and p['type']=='Dungeon' for p in M['poi'])==r['dungeonCount']
 for parent in r['parents']:assert parent!=region_id and parent in regions and parent<region_id
assert len(M['gates'])==11
for g in M['gates']:
 a,b=g['a'],g['b'];assert abs(a[0]-b[0])+abs(a[1]-b[1])==1
 assert M['gridRows'][a[0]-1][a[1]-1] == ('T' if g['fromRegionId']==0 else str(g['fromRegionId']) if g['fromRegionId']<=9 else 'A' if g['fromRegionId']==10 else 'B')
 assert M['gridRows'][b[0]-1][b[1]-1] == (str(g['toRegionId']) if g['toRegionId']<=9 else 'A' if g['toRegionId']==10 else 'B')
assert len(floors)==573
floor_count=Counter((f['PoiId'],f['DifficultyId']) for f in floors)
for p in M['poi']:
 for d in ('NORMAL','NIGHTMARE','HELL'):assert floor_count[p['id'],d]==p['floors']
for f in floors:
 intfield=lambda x:int(f[x]);active=intfield('ActiveCellTarget')
 assert active==sum(intfield(k) for k in ('EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount'))
 assert intfield('LayoutRows')*intfield('LayoutCols')>=active
 expected=(intfield('CombatCellCount')*intfield('CombatMonstersPerCellTarget')+intfield('EliteCellCount')*intfield('EliteGroupMonsterTarget')+intfield('FixedUniqueActorCount')+intfield('BossActorCount')+intfield('BossEscortTarget')+intfield('ScriptedWaveMonsterTarget'))
 assert intfield('PlannedKillTarget')==expected
 wave=(f['PoiId']=='W09-C01' and intfield('FloorIndex')==5)
 if wave:assert intfield('ScriptedWaveCount')==5 and intfield('ScriptedWaveMonsterTarget')==5*(5+('NORMAL','NIGHTMARE','HELL').index(f['DifficultyId']))
 anc=(f['PoiId']=='W08-C01' and intfield('FloorIndex')==4)
 if anc:assert intfield('FixedUniqueActorCount')==3 and intfield('BossActorCount')==0
for d in ('NORMAL','NIGHTMARE','HELL'):
 for side in ('main','side'):
  ids={p['id'] for p in M['poi'] if (not regions[p['regionId']]['optional']) == (side=='main')}
  planned=sum(int(f['PlannedKillTarget']) for f in floors if f['DifficultyId']==d and f['PoiId'] in ids)
  assert K['byDifficulty'][d][side]==planned
 assert len(S[d]['mainSequence'])==54 and len(S[d]['sideSequenceAfterMain'])==10
 assert len(S[d]['stopsAtFirstCastleAfterDungeons'])==5
 assert S[d]['mainSequence'][-1]=='W09-C01'
 assert S[d]['sideSequenceAfterMain'][-1]=='W11-C01'
print('PASS: 121 tiles, 11 connected regions, 64 unique POIs, 11 valid gates, 191 floors × 3 difficulty, scripted waves/ancients, exact budgets and full simulator')
