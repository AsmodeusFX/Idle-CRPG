from pathlib import Path
import json,csv
ROOT=Path(__file__).resolve().parents[1]
DATA=ROOT/'data'
M=json.loads((DATA/'ACT05_MAP_V0.1.json').read_text(encoding='utf-8'))
int_fields={'FloorIndex','FloorCount','LayoutRows','LayoutCols','ActiveCellTarget','EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount','FloorMonsterLevel','PoiChallengeLevel','RecommendedPlayerLevel','CombatMonstersPerCellTarget','EliteGroupMonsterTarget','BossActorCount','BossEscortTarget','PlannedKillTarget','ScriptedWaveCount','ScriptedWaveMonsterTarget','FixedUniqueActorCount'}
with (DATA/'ACT05_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as f:
    M['floorPlans']=[{k:int(v) if k in int_fields else v for k,v in row.items()} for row in csv.DictReader(f)]
T=(ROOT/'tools/map_ui_template.html').read_text(encoding='utf-8')
assert T.count('__ACT05_MAP_DATA__')==1
T=T.replace('__ACT05_MAP_DATA__',json.dumps(M,ensure_ascii=False,separators=(',',':')).replace('</','<\\/'))
out=ROOT/'Chapter5_Act5_Interactive_Map_V0.1.html'
out.write_text(T,encoding='utf-8')
print('Built',out,'bytes',out.stat().st_size)
