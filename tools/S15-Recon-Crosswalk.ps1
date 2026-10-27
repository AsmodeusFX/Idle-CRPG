param([string]$Workspace="d:/Project-Asmo/Idle-CRPG",[string]$OutDir="")
$ErrorActionPreference="Stop"
$cfg=Join-Path $Workspace "Idle-CRPG/config"
if($OutDir -eq ""){ $OutDir=Join-Path $Workspace ("artifacts/qa/s15-c1-c3-recon-" + (Get-Date -Format "yyyyMMdd-HHmmss")) }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
function T($p){ return @(Import-Csv -LiteralPath $p -Encoding UTF8) }
$rows=@()
foreach($s in (T (Join-Path $cfg "skill/skill_slot.csv"))){
    if($s.skill_id -eq ''){continue}
    $rows += [pscustomobject]@{Family='SKILL';DesignId=$s.skill_id;DesignVersion='S06_V0.8';Authority='C';ProductionId=$s.skill_id;ConfigRow=('skill/skill_slot.csv:' + $s.hero + ':S' + $s.slot);RuntimeConsumer='SkillRuntime.RunActiveSkill';ActualBehavior='fixed slot auto cast';EvidenceStrength='E1_STATIC';MainState='MATCHED_EVIDENCED'}
}
foreach($n in (T (Join-Path $cfg "skill/talent.csv"))){
    $rows += [pscustomobject]@{Family='TALENT';DesignId=$n.id;DesignVersion='S07_V0.8';Authority='P';ProductionId=$n.id;ConfigRow='skill/talent.csv';RuntimeConsumer=('kind=' + $n.kind + '/effect=' + $n.effect);ActualBehavior=$n.skill_id;EvidenceStrength='E1_STATIC';MainState='NOT_VERIFIED'}
}
foreach($n in (T (Join-Path $cfg "star/star.csv"))){
    $ret= if($n.attr_id -in @('skill_slots','party_slots','talent_reset','talent_reset_discount')){'retired'}else{'active'}
    $rows += [pscustomobject]@{Family='STAR';DesignId=$n.id;DesignVersion='C1B_V0.5';Authority='P';ProductionId=$n.id;ConfigRow=('star/star.csv:max=' + $n.max_level);RuntimeConsumer='Stars.StarValue';ActualBehavior=($n.attr_id + '/' + $ret);EvidenceStrength='E1_STATIC';MainState='MATCHED_EVIDENCED'}
}
foreach($e in (T (Join-Path $cfg "item/equipment.csv"))){
    $rows += [pscustomobject]@{Family='EQUIP_SLOT';DesignId=$e.slot;DesignVersion='S11_V1.2';Authority='C';ProductionId=$e.id;ConfigRow='item/equipment.csv';RuntimeConsumer='EquipmentValue';ActualBehavior=($e.attr + ' base=' + $e.base_value + ' per=' + $e.per_level);EvidenceStrength='E1_STATIC';MainState='MATCHED_EVIDENCED'}
}
foreach($q in (T (Join-Path $cfg "item/equipment_quality.csv"))){
    $rows += [pscustomobject]@{Family='EQUIP_QUALITY';DesignId=$q.id;DesignVersion='S11_V1.2';Authority='P';ProductionId=$q.id;ConfigRow='item/equipment_quality.csv';RuntimeConsumer='EquipmentValue';ActualBehavior=('mult=' + $q.multiplier + ' w=' + $q.weight);EvidenceStrength='E1_STATIC';MainState='MATCHED_EVIDENCED'}
}
foreach($s in (T (Join-Path $cfg "item/scroll.csv"))){
    $rows += [pscustomobject]@{Family='SCROLL';DesignId=$s.id;DesignVersion='C2B';Authority='C';ProductionId=$s.id;ConfigRow='item/scroll.csv';RuntimeConsumer='Consumables.UseScroll';ActualBehavior=('damage_pct=' + $s.damage_pct);EvidenceStrength='E1_STATIC';MainState='MATCHED_EVIDENCED'}
}
foreach($p in (T (Join-Path $cfg "item/potion.csv"))){
    $rows += [pscustomobject]@{Family='POTION';DesignId=$p.id;DesignVersion='C2B';Authority='C';ProductionId=$p.id;ConfigRow='item/potion.csv';RuntimeConsumer='AdvanceConsumables';ActualBehavior=('ticks=' + $p.duration_ticks + ' attack=' + $p.attack_pct);EvidenceStrength='E1_STATIC';MainState='NOT_VERIFIED'}
}
foreach($d in (T (Join-Path $cfg "item/drop_table.csv"))){
    $rows += [pscustomobject]@{Family='DROP_LEVEL';DesignId=('LEVEL_' + $d.monster_level);DesignVersion='C2B';Authority='H';ProductionId=('level=' + $d.monster_level);ConfigRow='item/drop_table.csv';RuntimeConsumer='DropRoll';ActualBehavior='probabilistic drop';EvidenceStrength='E1_STATIC';MainState='NOT_VERIFIED'}
}
$rows | Export-Csv -LiteralPath (Join-Path $OutDir "recon_design_id_crosswalk.csv") -NoTypeInformation -Encoding UTF8
"rows=$($rows.Count) out=$OutDir"
