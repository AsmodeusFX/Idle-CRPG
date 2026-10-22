#!/usr/bin/env python3
"""Static TEST_ONLY example consistency checker. Does not build or run Godot/C#."""
from pathlib import Path
import csv, json, sys

BASE = Path(__file__).resolve().parent.parent
DATA = BASE / 'data'
def load_csv(name):
    with (DATA / name).open(encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))
def load_json(name):
    return json.loads((DATA / name).read_text(encoding='utf-8'))

errors=[]
items=load_csv('item_definitions_TEST_ONLY.csv')
affixes=load_csv('affix_definitions_TEST_ONLY.csv')
instances=load_json('item_instance_rolls_TEST_ONLY.json')
cases=load_json('bd_test_scenarios_TEST_ONLY.json')
item_ids={x['item_def_id'] for x in items}
affix_defs={x['affix_id']:x for x in affixes}
instance_ids=set()
if len(item_ids)!=len(items): errors.append('duplicate item_def_id')
if len(affix_defs)!=len(affixes): errors.append('duplicate affix_id')
for item in items:
    if not item['item_def_id'].startswith('TEST_'): errors.append('non-test item_def')
for aff in affixes:
    if not aff['affix_id'].startswith('TEST_') or aff['approval_state']!='TEST_ONLY': errors.append('non-test affix')
    if aff['allowed_item_def'] not in item_ids: errors.append('affix missing item definition:'+aff['affix_id'])
    if aff['kind'] not in ('Attribute','SkillModifier','Trigger'): errors.append('invalid affix kind:'+aff['affix_id'])
for inst in instances['instances']:
    iid=inst['instance_id']
    if iid in instance_ids: errors.append('duplicate instance id:'+iid)
    instance_ids.add(iid)
    if inst['item_def_id'] not in item_ids: errors.append('instance missing item ref:'+iid)
    occ=set()
    for roll in inst['affixes']:
        d=affix_defs.get(roll['affix_id'])
        if not d: errors.append('unknown roll affix:'+str(roll['affix_id'])); continue
        if d['allowed_item_def']!=inst['item_def_id']: errors.append('roll-affix illegal item pairing:'+iid)
        if roll['occurrence_index'] in occ: errors.append('duplicate occurrence index:'+iid)
        occ.add(roll['occurrence_index'])
        if not isinstance(roll['rolled_value'],(int,float)): errors.append('non numeric rolled_value:'+iid)
for case in cases['test_cases']:
    if not case['id'].startswith('TEST_'): errors.append('non-test case id')
    for iid in case['item_instances']:
        if iid not in instance_ids: errors.append('unknown test instance:'+iid)
if not instances.get('test_only') or not cases.get('test_only'): errors.append('missing TEST_ONLY flag')
if errors:
    print('STATIC EXAMPLE VALIDATION FAIL:')
    for e in errors: print(' -',e)
    sys.exit(1)
print('STATIC TEST_ONLY VALIDATION PASS')
print('ItemDefinitions:',len(items),'AffixDefinitions:',len(affixes),'ItemInstances:',len(instances['instances']),'TestScenarios:',len(cases['test_cases']))
print('NOTE: This is not a C#/Godot build, save/load test, or user approval of production affix data.')
