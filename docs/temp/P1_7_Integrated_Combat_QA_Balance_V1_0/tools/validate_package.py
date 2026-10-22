#!/usr/bin/env python3
"""Only validates authored TEST_ONLY CSV/JSON, never runs Godot or modifies source."""
from pathlib import Path
import csv, json, collections
ROOT=Path(__file__).resolve().parents[1]
D=ROOT/'data'
def read(name):
 with (D/name).open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
def check(ok,msg):
 if not ok:raise AssertionError(msg)
pre=read('prerequisites.csv');tests=read('test_catalog.csv');scenarios=read('scenario_matrix_TEST_ONLY.csv');parties=read('party_presets_TEST_ONLY.csv');seeds=read('seed_sets_TEST_ONLY.csv');refs=read('upstream_traceability.csv')
preids={x['capability_id'] for x in pre}
check(len(preids)==len(pre),'duplicate prerequisites')
check({x['status'] for x in pre}=={'UNKNOWN'},'unverified prerequisites must remain UNKNOWN')
check(len(tests)==50,'expected 50 authored QA cases')
check(len({x['case_id'] for x in tests})==len(tests),'duplicate case IDs')
check(all(x['planned_status']=='NOT_RUN' and not x['result_evidence'] for x in tests),'unrun test marked run or evidence fabricated')
check(all(x['truth_layer'] in {'Core','Godot','Core+Godot','Review'} for x in tests),'bad truth layer')
for t in tests:
 for id in t['prerequisites'].split(';'):check(id in preids,f"bad test prereq {t['case_id']}:{id}")
check(len(parties)==5,'must be five party templates')
check(all(x['party_size']=='4' for x in parties),'launch party cap must be four')
check(all(x['approval_status']=='TEST_ONLY' for x in seeds),'seed status must stay TEST_ONLY')
check(len({x['seed'] for x in seeds})==len(seeds),'duplicate seed')
check(len({x['scenario_id'] for x in scenarios})==len(scenarios),'duplicate scenario ID')
check(len(scenarios)==145,'expected 120 lab + 15 world + 10 special')
check(all(x['planned_status']=='NOT_RUN' for x in scenarios),'scenario claimed executed')
for s in scenarios:
 for id in s['dependencies'].split(';'):check(id in preids,f"bad scenario prereq {s['scenario_id']}:{id}")
 if s['party_preset_id']:check(s['party_preset_id'] in {p['party_preset_id'] for p in parties},'invalid party')
c=collections.Counter(s['scope'] for s in scenarios)
check(c=={'LAB_MATRIX':120,'WORLD_SMOKE':15,'SPECIAL':10},f'bad test layer distribution {c}')
for diff in ['NORMAL','NIGHTMARE','HELL']:
 lab=[s for s in scenarios if s['scope']=='LAB_MATRIX' and s['difficulty_id']==diff]
 check(len(lab)==40,f'missing {diff} lab')
 check({s['scene_id'] for s in lab}=={f'T{i:02d}' for i in range(1,9)},'missing lab room')
 check({s['party_preset_id'] for s in lab}=={p['party_preset_id'] for p in parties},'missing test party')
 world=[s for s in scenarios if s['scope']=='WORLD_SMOKE' and s['difficulty_id']==diff]
 check(len(world)==5 and {s['act_id'] for s in world}=={f'ACT{i:02d}' for i in range(1,6)},'missing act/difficulty pair')
check(len(refs)>=6,'traceability missing')
check(any(x['access_status']=='NOT_ACCESSED' for x in refs),'must not claim game repo access')
schema=json.loads((D/'telemetry_schema_TEST_ONLY.json').read_text(encoding='utf-8'))
check(schema['status']=='TEST_ONLY','telemetry example appears production')
log=[json.loads(x) for x in (D/'telemetry_run_example_TEST_ONLY.jsonl').read_text(encoding='utf-8').splitlines() if x.strip()]
check(all(set(schema['required_event_fields']).issubset(x.keys()) for x in log),'example log fields missing')
check(all(x['run_id'].startswith('TEST_') for x in log),'non TEST_ONLY run')
checks=['00_主Todo_交付收尾与开发验收.md','01_只读工程审计_准入与冲突审批.md','02_综合验收矩阵与用例追踪.md','03_采集协议与可复现对照.md','04_数值调平顺序与问题归因.md','05_五职业五章三难度测试计划.md','06_实施切片与证据签收.md','07_未决策冲突清单与回退策略.md','09_P0-1_护甲百分比迁移_独立补遗执行规范.md','CLAUDE_CODE_FIRST_PROMPT.md']
check(all((ROOT/x).is_file() for x in checks),'missing core docs')
print('PASS static package validation; 30 prerequisites (UNKNOWN), 50 planned QA cases (NOT_RUN), 145 planned scenarios (NOT_RUN): 120 lab + 15 world + 10 special; no real Godot/C# executed')
