param([string]$Out="d:/Project-Asmo/Idle-CRPG/artifacts/qa/dump98.csv")
$ErrorActionPreference="Stop"
$root="d:/Project-Asmo/Idle-CRPG"
$design=@(Import-Csv -LiteralPath (Join-Path $root "docs/temp/Steam_D2_设计工程一致性只读对账执行包_V1.0/refs_仅对账证据非执行授权/design_S07_98nodes_v0.8.csv") -Encoding UTF8)
$talent=@(Import-Csv -LiteralPath (Join-Path $root "Idle-CRPG/config/skill/talent.csv") -Encoding UTF8)
$ti=@{}; foreach($x in $talent){ $ti[$x.id]=$x }
$alias=@{
 'BAR-S201'='barb_whirlwind_window';'BAR-S301'='barb_taunt';'BAR-S401'='barb_ancestor';'BAR-T01'='warrior_trait';
 'RAN-S201'='ran_multishot';'RAN-S301'='ran_guided_arrow';'RAN-S401'='ran_valkyrie';'RAN-T01'='ranger_trait';
 'SOR-S201'='sor_frost_nova';'SOR-S301'='sor_chain_lightning';'SOR-S401'='sor_meteor';'SOR-T01'='mage_trait';
 'PAL-S201'='pal_shield_bash';'PAL-S301'='pal_prayer';'PAL-S401'='pal_hammer';'PAL-T01'='priest_trait'
}
$rows=New-Object System.Collections.Generic.List[object]
foreach($n in $design){
    $prodId = if($alias.ContainsKey($n.NodeId)){ $alias[$n.NodeId] } else { $n.NodeId }
    $p=$ti[$prodId]
    $rows.Add([pscustomobject]@{
        design= $n.NodeId; class= $n.ClassId; region= $n.Region; prodId= $prodId
        kind= if($p){$p.kind}else{'MISSING'}; effect= if($p){$p.effect}else{''}; skill= if($p){$p.skill_id}else{''}; trigger= if($p){$p.trigger_id}else{''}
        behavior= $n.Behavior; prereq= $n.Prerequisite; excl= $n.ExclusiveGroup
    })
}
$rows | Export-Csv -LiteralPath $Out -NoTypeInformation -Encoding UTF8
"rows=$($rows.Count) out=$Out"
$rows | Group-Object kind | ForEach-Object { "$($_.Name)=$($_.Count)" }
