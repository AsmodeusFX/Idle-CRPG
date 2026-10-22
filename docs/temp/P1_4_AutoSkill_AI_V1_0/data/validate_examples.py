#!/usr/bin/env python3
"""Offline static validation of TEST_ONLY example data. NOT a Godot test."""
from __future__ import annotations
import csv
import json
from pathlib import Path
from collections import defaultdict

ROOT = Path(__file__).resolve().parent

def rows(filename: str) -> list[dict[str, str]]:
    with (ROOT / filename).open(encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))

profiles = rows("skill_ai_profiles_TEST_ONLY.csv")
loadouts = rows("hero_loadouts_TEST_ONLY.csv")
tests = rows("ai_test_cases_TEST_ONLY.csv")
semantics = json.loads((ROOT / "cast_condition_semantics_TEST_ONLY.json").read_text(encoding="utf-8"))
fixtures = json.loads((ROOT / "scenario_fixtures_TEST_ONLY.json").read_text(encoding="utf-8"))["fixtures"]

conditions = set(semantics["condition_ids"])
targets = set(semantics["target_policy_ids"])
actions = set(semantics["allowed_action_kinds_in_fixture"])
profile_by_id = {}
for row in profiles:
    sid = row["skill_id"].strip()
    assert sid and sid not in profile_by_id, f"Duplicate/empty skill ID: {sid}"
    assert row["condition_id"] in conditions, f"Unknown condition in {sid}"
    assert row["target_policy_id"] in targets, f"Unknown target policy in {sid}"
    assert row["role"] in {"Barbarian", "Paladin", "Sorceress", "Necromancer", "Ranger"}
    assert int(row["priority"]) >= 0
    assert row["approval_state"] == "R", f"Expected recommendation marker in {sid}"
    profile_by_id[sid] = row
assert len(profile_by_id) == 17, f"Expected 17 skill examples, got {len(profile_by_id)}"

by_loadout = defaultdict(list)
for row in loadouts:
    lid = row["loadout_id"]
    sid = row["skill_id"]
    assert sid in profile_by_id, f"Loadout {lid} references unknown skill {sid}"
    assert row["hero_class"] == profile_by_id[sid]["role"]
    slots = int(row["unlocked_slots"])
    index = int(row["slot_index"])
    assert 1 <= slots <= 5 and 1 <= index <= slots, f"Invalid slot {lid}:{index}/{slots}"
    by_loadout[lid].append(row)
for lid, entries in by_loadout.items():
    assert len(set(x["slot_index"] for x in entries)) == len(entries), f"Duplicate slot in {lid}"
    assert len({x["unlocked_slots"] for x in entries}) == 1, f"Inconsistent unlocked_slots in {lid}"
    assert len({x["hero_class"] for x in entries}) == 1, f"Mixed classes in {lid}"
    assert len(entries) <= int(entries[0]["unlocked_slots"])

ids = set()
for row in tests:
    tid = row["test_id"]
    assert tid not in ids, f"Duplicate test {tid}"
    assert row["loadout_id"] in by_loadout, f"Unknown loadout in {tid}"
    assert row["expected_action_kind"] in actions, f"Unknown action in {tid}"
    if row["expected_skill_id"]:
        assert row["expected_skill_id"] in profile_by_id, f"Unknown expected skill in {tid}"
        assert any(x["skill_id"] == row["expected_skill_id"] for x in by_loadout[row["loadout_id"]]), f"Expected skill is not in loadout: {tid}"
    assert row["approval_dependency"] in {"NONE"} | {f"C{i:02d}" for i in range(1, 13)}
    ids.add(tid)
assert ids == {f"AI{i:02d}" for i in range(1, 31)}, "Expected AI01-AI30 exactly"
fixture_ids = set()
for fix in fixtures:
    assert fix["id"] not in fixture_ids, f"Duplicate fixture id: {fix['id']}"
    assert fix["test"] in ids and fix["loadout"] in by_loadout
    assert fix["loadout"] == next(t["loadout_id"] for t in tests if t["test_id"] == fix["test"])
    fixture_ids.add(fix["id"])
assert len(fixtures) >= 8
assert semantics["status_rules"]["active_auras"].startswith("MULTIPLE")
assert semantics["status_rules"]["different_curses"].startswith("CAN_COEXIST")
assert semantics["status_rules"]["ranger_weapon"].startswith("BOW_ONLY")
assert semantics["status_rules"]["CD_start"].startswith("COMMIT_ONLY")
print(f"PASS: TEST_ONLY static consistency: {len(profiles)} skills / {len(by_loadout)} loadouts / {len(tests)} cases / {len(fixtures)} snapshot examples")
print("NOT TESTED: Godot/C# compile, actual Runtime, Core.Tests, project config schema, Mini visuals")
