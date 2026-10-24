#!/usr/bin/env python3
"""Static consistency checks for this hand-off's TEST_ONLY CSV files.
This is NOT a Game.Core test and must not be mistaken for a successful Godot run.
"""
import csv
from pathlib import Path
from collections import Counter
ROOT=Path(__file__).resolve().parent

def records(n):
    with (ROOT/n).open(encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))

mons=records('monster_archetypes_TEST_ONLY.csv')
rooms=records('encounters_TEST_ONLY.csv')
items=records('encounter_members_TEST_ONLY.csv')
checks=records('test_assertions_TEST_ONLY.csv')
assert len(mons)==8 and {x['monster_id'] for x in mons}=={f'M{i:02d}' for i in range(1,9)}
assert len(set(x['monster_id'] for x in mons))==8
assert len([x for x in rooms if x['is_primary_room']=='true'])==8
assert {x['scene_id'] for x in rooms if x['is_primary_room']=='true'}=={f'T{i:02d}' for i in range(1,9)}
assert len(rooms)==9 and next(x for x in rooms if x['scene_id']=='T04C')['variant_of']=='T04'
assert len(set(x['scene_id'] for x in rooms))==len(rooms)
assert all(x['scene_id'] in {r['scene_id'] for r in rooms} for x in items)
assert all(x['monster_id'] in {m['monster_id'] for m in mons}|{'EXISTING_BOSS_OR_TEST_PROXY'} for x in items)
assert all(int(x['initial_count'])>0 for x in items)
counts=Counter()
for x in items: counts[x['scene_id']]+=int(x['initial_count'])
assert counts=={'T01':8,'T02':12,'T03':3,'T04':8,'T04C':8,'T05':8,'T06':5,'T07':5,'T08':5},counts
assert len(checks)==30 and len({x['test_id'] for x in checks})==30
assert {x['test_id'] for x in checks}=={f'AI{i:02d}' for i in range(1,21)}|{f'R{i:02d}' for i in range(1,11)}
assert all(x['status']=='TEST_ONLY' for x in mons+rooms+items+checks)
assert next(x for x in mons if x['monster_id']=='M02')['target_policy_semantic']=='EngagementCapacityThenOthers'
for x in mons:
    if x['attack_mode_semantic']=='Ranged': assert x['target_policy_semantic']=='NearestLegalTarget'
assert all(x['ai_activation_semantic']=='PROJECT_EXISTING_DELAY' for x in mons)
print('PASS: TEST_ONLY package schema checks (8 archetypes; 8 primary rooms + T04C; 30 assertions)')
print('NOTE: no Godot/C# compilation, gameplay or actual balance tests were performed.')
