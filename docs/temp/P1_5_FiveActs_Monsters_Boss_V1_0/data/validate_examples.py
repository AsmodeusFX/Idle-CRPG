#!/usr/bin/env python3
"""Static reference/proposal consistency. NOT Godot project or production behavior tests."""
import csv,json,collections,sys
from pathlib import Path
p=Path(__file__).parent

def rows(name):
 with (p/name).open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))

def expect(condition,desc):
 if not condition:raise AssertionError(desc)
 print('[PASS]',desc)

region=rows('region_source_snapshot_REFERENCE.csv')
poi=rows('poi_source_snapshot_REFERENCE.csv')
monster=rows('monster_catalog_PROPOSAL.csv')
regionpool=rows('region_pools_PROPOSAL.csv')
bind=rows('poi_bindings_PROPOSAL.csv')
boss=rows('boss_anchors_PROPOSAL.csv')
minor=rows('named_dungeon_bosses_REFERENCE.csv')
stage=rows('special_stages_SOURCE.csv')
checks=rows('acceptance_matrix.csv')
source=json.loads((p/'source_manifest_REFERENCE.json').read_text(encoding='utf-8'))

expect(len(source)==5,'exact 5 upstream chapters')
expect(len(region)==42 and len({x['region_key'] for x in region})==42,'42 unique composite Region keys')
expect(len(poi)==231 and len({x['poi_key'] for x in poi})==231,'231 unique composite POI keys')
expect(sum(x['type']=='Dungeon' for x in poi)==189 and sum(x['type']=='Castle' for x in poi)==42,'189 Dungeon + 42 Castle')
expect(sum(x['floor_count_all_difficulties'] for x in source)==1515,'1515 source floor rows over 3 difficulties')
expect(all(x['floor_count_all_difficulties']%3==0 for x in source) and sum(x['floor_count_all_difficulties']//3 for x in source)==505,'505 floor records per difficulty')
expect([x['poi_count'] for x in source]==[41,49,53,24,64],'per Act POI count 41/49/53/24/64')
expect(len(monster)==49 and len({x['monster_content_id'] for x in monster})==49,'49 unique proposal monster IDs')
expect({x['archetype_id'] for x in monster}=={f'M{i:02d}' for i in range(1,9)},'8 P0-2 behavior prototypes fully covered')
expect({x['region_key'] for x in regionpool}=={x['region_key'] for x in region},'each of 42 regions has pool membership')
expect(all(x['monster_content_id'] in {m['monster_content_id'] for m in monster} for x in regionpool),'all pool members reference catalog monsters')
expect(all(x['region_key'].split(':')[0]==x['monster_content_id'].split('_')[0] for x in regionpool),'no cross-Act monster IDs')
pool_sum=collections.defaultdict(int)
for x in regionpool:pool_sum[x['region_key']]+=int(x['weight'])
expect(all(v==100 for v in pool_sum.values()),'each Region pool weights sum to 100')
expect(len(bind)==231 and {x['poi_key'] for x in bind}=={x['poi_key'] for x in poi},'all 231 POIs bound exactly once')
expect(all(x['region_key'] in {r['region_key'] for r in region} for x in bind),'each POI binding refers to valid region')
expect(len(boss)==42 and {x['boss_anchor_id'] for x in boss}=={x['poi_key'] for x in poi if x['type']=='Castle'},'exact one Boss anchor per existing Castle')
expect(all(x['boss_anchor_id'] in {b['boss_anchor_id'] for b in boss} for x in bind if x['existing_poi_type']=='Castle'),'all Castles have Boss anchors')
expect(all(x['source_normal_fixed_unique_total'].isdigit() for x in boss),'fixed unique actor counts typed')
expect(len(checks)==36 and len({x['test_id'] for x in checks})==36,'36 unique acceptance cases')
expect(set(x['difficulty'] for x in stage)=={'NORMAL','NIGHTMARE','HELL'},'3 difficulties represented in special phases')
phase_groups=collections.defaultdict(list)
for x in stage:phase_groups[(x['poi_key'],x['difficulty'])].append(x)
expect(len(phase_groups)==12,'4 special Castle profiles per difficulty')
for (key,diff),items in phase_groups.items():
 assert len({int(x['stage_index']) for x in items})==len(items),f'duplicate stage index {key} {diff}'
 if key=='ACT04:D04-C01':
  expect(sum(int(x['seal_count']) for x in items)==5 and sum(int(x['fixed_unique_actors']) for x in items)==3 and sum(int(x['boss_actor_count']) for x in items)==1,f'Act4 seals=5 uniques=3 boss=1 {diff}')
 if key=='ACT05:W08-C01':
  expect(sum(int(x['fixed_unique_actors']) for x in items)==3 and sum(int(x['boss_actor_count']) for x in items)==0,f'Ancients 3 fixed uniques 0 BossActor {diff}')
 if key=='ACT05:W09-C01':
  expect(sum(int(x['scripted_wave_count']) for x in items)==5 and sum(int(x['boss_actor_count']) for x in items)==1,f'Baal 5 waves 1 BossActor {diff}')
  expect(sum(int(x['scripted_wave_monster_target']) for x in items)>0,f'Baal wave target nonzero {diff}')
expect(all('PROPOSAL' in x['status'] for x in monster),'catalog IDs stay clearly PROPOSAL')
expect(all(x['binding_status']=='PROPOSAL_OVERLAY_ONLY' for x in bind),'POI bindings always additive PROPOSAL')
print(f'\nSTATIC DATA VALIDATED: {len(region)} regions, {len(poi)} POIs, {len(monster)} proposed monsters, {len(regionpool)} pool weights, {len(boss)} boss anchors, {len(stage)} special stage facts, {len(checks)} tests.')
print('DISCLAIMER: Project runtime, C# compile, Godot gameplay and actual source parity NOT checked.')
