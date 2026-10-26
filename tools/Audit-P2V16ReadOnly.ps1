param([string]$Output='artifacts/qa/p2-4-v16/s15')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$out=[IO.Path]::GetFullPath((Join-Path $repo $Output))
$qa=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/qa'))+[IO.Path]::DirectorySeparatorChar
if(-not $out.StartsWith($qa,[StringComparison]::OrdinalIgnoreCase)){throw 'Only artifacts/qa output is allowed'}
$config=Join-Path $repo 'Idle-CRPG/config'
$inputs=@(Get-ChildItem $config -Recurse -File -Filter '*.csv')+@(Get-ChildItem (Join-Path $repo 'Idle-CRPG/Scripts') -Recurse -File -Filter '*.cs')+@(Get-ChildItem (Join-Path $repo 'docs/temp') -Recurse -File)
function Fingerprints { $inputs | ForEach-Object {[pscustomobject]@{path=$_.FullName.Substring($repo.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}} }
$before=@(Fingerprints)
New-Item -ItemType Directory -Force $out | Out-Null
$before | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $out 'source_hashes.csv')
$tables=@('skill/skill_slot.csv','skill/talent.csv','skill/skill.csv','skill/effect.csv','skill/buff.csv','star/star.csv','combat/prototype_progression.csv','hero/hero_level.csv','item/equipment.csv','item/equipment_quality.csv','item/scroll.csv','item/potion.csv')
$schema=[Collections.Generic.List[string]]::new()
$schema.Add('# S15 read-only schema audit (2026-10-26)')
$schema.Add('PARTIAL_INPUT: S06-S13 original row-level candidate assets are absent from current temp/suggest. S15 contains references and aggregate counts only. Do not infer design IDs or behavioral equivalence from existing similar skills. No production files are changed.')
$schema.Add('')
$schema.Add('| Design column | Current table/column | Type/consumer | Status and required test |')
$schema.Add('|---|---|---|---|')
foreach($table in $tables){
    $file=Join-Path $config $table
    if(-not(Test-Path -LiteralPath $file)){continue}
    $columns=(Get-Content -Encoding UTF8 -LiteralPath $file -First 1).Split(',')
    foreach($column in $columns){$schema.Add("| unavailable: candidate source missing | $table / $column | CSV loader + technical field dictionary | CURRENT_SCHEMA_ONLY; requires design-ID, unit and real-effect validation |")}
}
$schema.Add('')
$schema.Add('Fixed skills: skill_slot.csv has 16 nonempty definitions across FIVE classes; the first four classes have 12. This is not proof of S06 first-run 16 behavior definitions. Five prototype T nodes exist; only warrior qualification is deployed. Mutual exclusion, rank, per-instance investment and free regional reset are implemented; candidate effects still require individual matching.')
$schema.Add('AR/DR/XP/KP: all nine prototype_progression.csv rows are H. HeroRatingBase+(Lv-1)*HeroRatingPerLevel and monster m*MonsterRatingPerLevel are current consumers, separate from missing H-B1 31-level candidate tables. Lmax requires every deployed hero AR/DR; Lmin remains closed.')
$schema.Add('Equipment: six templates/six slots/four qualities/one ATK-or-HP attribute. Current IDs: common=普通, uncommon=优秀, rare=稀有, legendary=传奇. Candidate 普通/稀有/罕见/传奇 cannot be implicitly mapped by display name: DESIGN_DECISION.')
$schema.Add('Consumables: one single-target physical scroll, one attack-percent potion. Five scroll UI positions and four occupied potion slots are implemented. Missing control/movement/team-buff definitions and capacity-node effects require original S12 rows before REUSE/NEEDS_RUNTIME_SUPPORT classification.')
$schema | Set-Content -Encoding UTF8 (Join-Path $out 'schema_diff.md')
$cross=@(Import-Csv -Encoding UTF8 (Join-Path $config 'skill/skill_slot.csv') | ForEach-Object {[pscustomobject]@{design_id='unavailable';production_id=$_.skill_id;kind=('fixed_S'+$_.slot);class_id=$_.hero;source='skill/skill_slot.csv';match_status=$(if($_.skill_id){'UNKNOWN_DESIGN_ID'}else{'NOT_DEPLOYED'});risk='S06 original ID/behavior missing; no implicit display-name mapping'}})
$cross | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $out 'id_crosswalk.csv')
$runtime=Get-Content -Raw -Encoding UTF8 (Join-Path $repo 'Idle-CRPG/Scripts/Core/SkillRuntime.cs')
$coverage=@(Import-Csv -Encoding UTF8 (Join-Path $config 'skill/effect.csv') | ForEach-Object {
    $consumed=$runtime.Contains('case "'+$_.kind+'"')
    [pscustomobject]@{design_id='unavailable';production_id=$_.id;effectKey=$_.kind;consumer=$(if($consumed){'SkillRuntime.DrainEffects -> shared runtime'}else{'unavailable'});status=$(if($consumed){'STATIC_CONSUMER'}else{'RED'});evidence='Case label is static evidence, not candidate behavior acceptance'}
})
$coverage+=@(Import-Csv -Encoding UTF8 (Join-Path $config 'star/star.csv') | ForEach-Object {
    $retired=$_.attr_id -in @('party_slots','skill_slots','talent_reset','talent_reset_discount')
    $supported=$_.attr_id -in @('attack','hp','xp_gain_pct','class_trait_unlock')
    [pscustomobject]@{design_id='unavailable';production_id=$_.id;effectKey=$_.attr_id;consumer=$(if($retired){'retired / historical record only'}elseif($supported){'Stars.ApplyStarTotals / XpGainMultiplier / ClassTraitUnlocked'}else{'unavailable'});status=$(if($retired){'DEFERRED_RETIRED'}elseif($supported){'STATIC_CONSUMER'}else{'RED'});evidence='31 definitions/78 ranks/57 and 56 purchase routes are candidate references; original rows missing'}
})
$coverage+=[pscustomobject]@{design_id='S06-S13 original effects unavailable';production_id='unavailable';effectKey='unavailable';consumer='unavailable';status='RED';evidence='INPUT_MISSING: individual candidate effects cannot be judged'}
$coverage | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $out 'effect_coverage.csv')
@'
# Source precedence

Current explicit user decisions > approved baseline and patches > authorized V1.6 repairs > engineering facts > unapproved S06-S13 candidates/P/H. Existing code does not override approved objectives; candidate counts do not authorize overwriting P0 topology.

Production maps load config/chapters/actN. P0 IDs and approved AND/OR changes remain governed by campaign_five_acts.md, act1.md, map_access.md and campaign review. S15's 31 castles/136 dungeons/226 Normal floors are candidate summary references, not independently verified loaded counts. Missing row-level S09/S10/P0 crosswalk is INPUT_MISSING, not permission to alter gates.

S1-S4+T is approved mechanism. The current 16 historical definitions include necromancer and are not the first-four-class S06 candidate set. CastTalent exists only in historical QA/isolated preview; retired slot and paid full-tree-reset purchases retain history without automatic gold refunds. Old points are returned only on player command under the user's 2026-10-26 decision.

H-B1/H-S08/H-EQ1/H-C1 and current prototype_progression.csv remain separate H sources. Lmin level gating is P and stays closed. Equipment display names do not define quality-ID mappings. S14/AP/fifth seat remain deferred.
'@ | Set-Content -Encoding UTF8 (Join-Path $out 'source_precedence.md')
@'
# Proposed implementation packages (NOT EXECUTED)

1. Obtain the original S04-S13 row-level candidate tables and stable design IDs; complete schema_diff/id_crosswalk first. A full migration estimate is unavailable until INPUT_MISSING is resolved.
2. Review every skill/talent effect using the existing DamagePipeline/Buff/Summon/Chain authority. Split NEEDS_RUNTIME_SUPPORT into behavior tests and save compatibility work. Existing skill names are insufficient to mark REUSE.
3. Validate all 31 star effect keys and prices before production. Missing capacity effects and retired slots cannot pad purchase counts. Resolve quality-ID naming and H-parameter source versions.
4. Connect content to real P0 IDs and verify AND/OR, optional branches, Boss/event cells and pools in QA copies. Do not replace topology.
5. Split five-scroll/four-potion work by real damage/control/movement/team effect. Verify inventory, occupied active slot, source ownership and reload; no silent substitute damage behavior.
6. Runtime asset_ledger.csv now records actual XP/KP/gold/equipment/scroll/potion transactions. Existing snapshots expose levels/chapter/retries. TTK, Boss attempts/win rates, actual monster-level histograms and per-hero talent sampling still need dedicated metric verification. Fixed offline H quotas are not foreground earning estimates. Ten-hour balance is not validated.

This is a proposal/dependency list only. No new content, effects or numerical tuning is implemented by this audit.
'@ | Set-Content -Encoding UTF8 (Join-Path $out 'implementation_packages_proposal.md')
$after=@(Fingerprints)
$changed=Compare-Object ($before | ForEach-Object {$_.path+':'+$_.sha256}) ($after | ForEach-Object {$_.path+':'+$_.sha256})
if($changed){throw 'Input hashes changed during read-only audit'}
@"
# Read-only audit evidence

Command: powershell -File tools/Audit-P2V16ReadOnly.ps1
Output: artifacts/qa/p2-4-v16/s15
Input files: $($before.Count); before/after SHA256 identical; source_hashes.csv lists each input.
Scope: production CSV, Core/UI source, all docs/temp inputs. Only artifacts/qa reports are written; no editor export, player save or production configuration write. This statement covers this script only, not earlier authorized R-A edits.
Discovery: rg --files docs/temp docs/suggest found S15 references but no original S06-S13 row-level candidate tables.
Status: PARTIAL_INPUT. Six requested artifacts are generated; full 31-star/98-talent/design-column mapping remains unavailable without original sources. Missing IDs and effects are not invented or marked passed.
"@ | Set-Content -Encoding UTF8 (Join-Path $out 'read_only_audit_evidence.md')
Write-Output "S15_READ_ONLY_PARTIAL_INPUT inputs=$($before.Count) hashes_unchanged=true output=$out"
