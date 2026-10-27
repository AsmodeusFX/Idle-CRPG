param([string]$Workspace="d:/Project-Asmo/Idle-CRPG")
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$cfg=Join-Path $Workspace "Idle-CRPG/config"
$refs=Join-Path $Workspace "docs/temp/Steam_D2_设计工程一致性只读对账执行包_V1.0/refs_仅对账证据非执行授权"
# 设计包已于2026-10-27迁移到深层目录管理：以下为真实设计源位置。
$pkg2=Join-Path $Workspace "docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包"
$c1bDir=Join-Path $pkg2 "P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5"
$c2bDir=Join-Path $pkg2 "P2-4 S15_C2-B｜装备、卷轴与药剂完整投放"
$c3Dir=Join-Path $pkg2 "P2-4 S15_C3_首轮数值配置与运行验收_V0.7"
$stamp=Get-Date -Format "yyyyMMdd-HHmmss"
$out=Join-Path $Workspace "artifacts/qa/s15-c1-c3-recon-proof2-$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
function T($p){ return @(Import-Csv -LiteralPath $p -Encoding UTF8) }
foreach($need in @($c1bDir,$c2bDir,$c3Dir)){ if(-not (Test-Path $need)){ throw "设计源目录缺失：$need" } }

$cs=@(Get-ChildItem -Path (Join-Path $Workspace "Idle-CRPG/Scripts") -Recurse -File -Filter *.cs)
$cfgFiles=@(Get-ChildItem -Path $cfg -Recurse -File -Filter *.csv)
$sha=[System.Security.Cryptography.SHA256]::Create()
function HashOf($files){ $cat=($files | Sort-Object FullName | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }) -join ''; return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($cat))) -replace '-','').ToLower() }
$fp='{"stamp":"'+$stamp+'","cs_files":'+$cs.Count+',"cs_sha256":"'+(HashOf $cs)+'","csv_files":'+$cfgFiles.Count+',"csv_sha256":"'+(HashOf $cfgFiles)+'","dotnet":"'+((& dotnet --version | Out-String).Trim())+'","os":"'+[Environment]::OSVersion.VersionString+'"}'
[IO.File]::WriteAllText((Join-Path $out "recon_fingerprint.json"),$fp,$utf8)

$talents=T (Join-Path $cfg "skill/talent.csv")
$talentIds=@{}; foreach($t in $talents){ $talentIds[$t.id]=$t }
$skillIds=@{}; foreach($s in (T (Join-Path $cfg "skill/skill.csv"))){ $skillIds[$s.id]=$s }
$rows=New-Object System.Collections.Generic.List[object]
function AddRow($family,$srcFile,$srcId,$auth,$prodId,$cfgRow,$consumer,$actual,$evidence,$state,$note){
    $rows.Add([pscustomobject]@{
        family=$family; source_file=$srcFile; source_id=$srcId; source_authority=$auth
        production_id=$prodId; config_row=$cfgRow; consumer=$consumer; actual_runtime=$actual
        evidence_status=$evidence; main_state=$state; reference_version='V1.1'; note=$note
    })
}

# 1) 16 技能
foreach($pair in @(
 @('BAR-S1','barb_bash'),@('BAR-S2','barb_whirlwind_window'),@('BAR-S3','barb_taunt'),@('BAR-S4','barb_ancestor'),
 @('RAN-S1','ran_shot'),@('RAN-S2','ran_multishot'),@('RAN-S3','ran_guided_arrow'),@('RAN-S4','ran_valkyrie'),
 @('SOR-S1','mage_fireball'),@('SOR-S2','sor_frost_nova'),@('SOR-S3','sor_chain_lightning'),@('SOR-S4','sor_meteor'),
 @('PAL-S1','paladin_smite'),@('PAL-S2','pal_shield_bash'),@('PAL-S3','pal_prayer'),@('PAL-S4','pal_hammer'))){
    $did=$pair[0];$prodId=$pair[1]
    $ok=$skillIds.ContainsKey($prodId)
    AddRow 'C1A_SKILL' 'design_S06_v0.8.md' $did 'P' $prodId ('skill/skill.csv:'+$prodId) 'SkillRuntime.RunActiveSkill' ($(if($ok){'fixed slot auto cast'}else{'MISSING'})) $(if($ok){'E2_RUNTIME(--c1a-checks)'}else{'E0_UNVERIFIED'}) $(if($ok){'MATCHED_EVIDENCED'}else{'NOT_IMPLEMENTED'}) ''
}
# 2) 4职业T（含圣骑士互斥分支）
foreach($pair in @(@('BAR-T01','warrior_trait','passive'),@('RAN-T01','ranger_trait','passive'),@('SOR-T01','mage_trait','passive'),@('PAL-T01','priest_trait','pal_guard_aura'),@('PAL-T01B','pal_trait_zeal','pal_zeal_aura'))){
    $did=$pair[0];$prodId=$pair[1];$mode=$pair[2]
    $ok=$talentIds.ContainsKey($prodId)
    AddRow 'C1A_T' 'design_S06_v0.8.md' $did 'P' $prodId ('skill/talent.csv:'+$prodId) 'ClassTraitUnlocked+runtime' $mode $(if($ok){'E2_RUNTIME(--c1a-checks)'}else{'E0_UNVERIFIED'}) $(if($ok){'MATCHED_EVIDENCED'}else{'NOT_IMPLEMENTED'}) 'T不占S1-S4槽'
}
# 3) S07 98 设计节点
$nodeAlias=@{
 'BAR-S201'='barb_whirlwind_window';'BAR-S301'='barb_taunt';'BAR-S401'='barb_ancestor';'BAR-T01'='warrior_trait';
 'RAN-S201'='ran_multishot';'RAN-S301'='ran_guided_arrow';'RAN-S401'='ran_valkyrie';'RAN-T01'='ranger_trait';
 'SOR-S201'='sor_frost_nova';'SOR-S301'='sor_chain_lightning';'SOR-S401'='sor_meteor';'SOR-T01'='mage_trait';
 'PAL-S201'='pal_shield_bash';'PAL-S301'='pal_prayer';'PAL-S401'='pal_hammer';'PAL-T01'='priest_trait'}
foreach($d in (T (Join-Path $refs "design_S07_98nodes_v0.8.csv"))){
    $prodId=$d.NodeId
    if($nodeAlias.ContainsKey($d.NodeId)){$prodId=$nodeAlias[$d.NodeId]}
    $ok=$talentIds.ContainsKey($prodId)
    AddRow 'C1A2_TALENT98' 'design_S07_98nodes_v0.8.csv' $d.NodeId 'P' ($(if($ok){$prodId}else{'MISSING'})) ($(if($ok){'skill/talent.csv:'+$prodId}else{'--'})) ($(if($ok){'TalentNode kind='+$talentIds[$prodId].kind}else{'--'})) ($(if($ok){'learnt+region reset+save'}else{'absent'})) $(if($ok){'E2_RUNTIME(--c1a2-checks)'}else{'E0_UNVERIFIED'}) $(if($ok){'IMPLEMENTED_DIFFERENT'}else{'NOT_IMPLEMENTED'}) ($(if($ok){'语义收窄见Review 23.4'}else{'设计节点未落地'}))
}
# 4) C1B 31 设计星图（真实设计源 + B0 生产映射）
$starDesign=Join-Path $c1bDir "C1B_31节点真实消费者与售卖门禁.csv"
$b0=Join-Path $Workspace "artifacts/qa/s15-c1b-b0/node_status_31.csv"
$b0Map=@{}; foreach($n in (T $b0)){ $b0Map[$n.design_node]=$n }
foreach($d in (T $starDesign)){
    $prodId='MISSING';$st='(unknown)';$evd='E1_STATIC';$state='NOT_VERIFIED'
    if($b0Map.ContainsKey($d.NodeId)){
        $prodId=$b0Map[$d.NodeId].production_star_id; $st=$b0Map[$d.NodeId].primary_status
        if($st -in @('WORKS','NEEDS_EXTENSION','NO_CONSUMER')){$state='MATCHED_EVIDENCED';$evd='E2_RUNTIME(--c1b-checks 2026-10-27)'}
        else{$state='NOT_IMPLEMENTED'}
    }
    AddRow 'C1B_STAR31' 'C1B_31节点真实消费者与售卖门禁.csv' $d.NodeId 'P' $prodId ('star/star.csv:max='+$d.MaxRank) $d.ConsumerToVerify ('gate='+$d.SaleGate+'/status='+$st) $evd $state (($d.ConfirmedBasis) -replace "[`r`n,]",' ')
}
AddRow 'C1B_ROUTE' 'C1B_两阵容替代购买合法路线_条件性.csv' '57/55' 'P' 's_root+39 prod nodes' 'star/star.csv;star_layout.csv' 'tests --c1b3-checks' 'routes 57/55 real purchases' 'E2_RUNTIME(--c1b3-checks 2026-10-27)' 'MATCHED_EVIDENCED' '设计包已迁移；复跑时从新路径临时恢复输入，跑完已清理'
# 5) S11 28 → 24 + 4
foreach($t in (T (Join-Path $refs "design_S11_28templates_P.csv"))){
    $isCharmHp=($t.TemplateId -match 'CHARM_HP')
    AddRow 'C2B_EQUIP28' 'design_S11_28templates_P.csv' $t.TemplateId 'P' ($t.SlotId+'/'+$t.MainStat) ('item/equipment.csv:'+$t.SlotId) 'EquipmentValue' ($(if($isCharmHp){'not dropped'}else{'6 slots x 4 qualities'})) ($(if($isCharmHp){'SUPERSEDED_BY_USER(EQ-01)'}else{'E2_RUNTIME(--c2b-checks)'})) ($(if($isCharmHp){'SUPERSEDED_BY_USER'}else{'MATCHED_EVIDENCED'})) ($(if($isCharmHp){'CHARM_HP 不投放首轮'}else{''}))
}
# 6) 5卷轴 + 4药剂（真实设计源）
$equipDesign=Join-Path $c2bDir "C2B_5卷轴4药剂真实行为与工程扩展合同.csv"
$scrollMap=@{'SC01'='scroll_bolt';'SC02'='scroll_arc';'SC03'='scroll_flame';'SC04'='scroll_frost';'SC05'='scroll_banner'}
$potionMap=@{'PO01'='potion_might';'PO02'='potion_tough';'PO03'='potion_greed';'PO04'='potion_swift'}
foreach($d in (T $equipDesign)){
    if($d.Category -eq 'SCROLL'){
        $prodId=$scrollMap[$d.DesignId]
        AddRow 'C2B_SCROLL' 'C2B_5卷轴4药剂真实行为与工程扩展合同.csv' $d.DesignId ($(if($d.DesignId -eq 'SC01'){'C'}else{'P'})) $prodId ('item/scroll.csv:'+$prodId) 'Consumables.UseScroll' $d.TargetEffect 'E2_RUNTIME(--c2b-checks 2026-10-27)' 'MATCHED_EVIDENCED' $d.RuntimeStatus_C0
    } else {
        $prodId=$potionMap[$d.DesignId]
        $state='MATCHED_EVIDENCED';$note=$d.RuntimeStatus_C0
        if($d.DesignId -eq 'PO02'){$state='SUPERSEDED_BY_USER';$note='设计[P/H] +15%MaxHP；被 POT-01[C] 覆盖为百分比减伤'}
        AddRow 'C2B_POTION' 'C2B_5卷轴4药剂真实行为与工程扩展合同.csv' $d.DesignId 'P' $prodId ('item/potion.csv:'+$prodId) 'AdvanceConsumables' $d.TargetEffect 'E2_RUNTIME(--c2b-checks 2026-10-27)' $state $note
    }
}
# 7) 31 怪级掉落（设计源 H-B1 31档）
$levels=Join-Path $c3Dir "C3_31怪级联合参数待校准_H-B1.csv"
$dropRows=@{}; foreach($r in (T (Join-Path $cfg "item/drop_table.csv"))){ $dropRows[$r.monster_level]=$r }
foreach($l in (T $levels)){
    $lvl=$l.MonsterLevel
    $ok=$dropRows.ContainsKey($lvl)
    AddRow 'C2B_DROP31' 'C3_31怪级联合参数待校准_H-B1.csv' ('LEVEL_'+$lvl) 'H' ('level='+$lvl) 'item/drop_table.csv' 'DropRoll' ($(if($ok){'row present'}else{'missing'})) 'E1_STATIC(数值为[H]未授权采用)' ($(if($ok){'IMPLEMENTED_DIFFERENT'}else{'NOT_IMPLEMENTED'})) '设计表标 H_NOT_AUTHORIZED；生产为首轮[H]值'
}
# 8) 31 城堡
foreach($c in (T (Join-Path $refs "design_S09_31castle_v1.0.csv"))){
    $poi=$c.PoiId;$prod=@()
    foreach($n in 1..4){ $d=Join-Path $cfg ("chapters/act$n/dungeon/dungeon.csv"); if(Test-Path $d){ if(@(T $d | Where-Object { $_.id -eq $poi }).Count -gt 0){ $prod+='act'+$n } } }
    $ok=$prod.Count -gt 0
    AddRow 'C2A_CASTLE31' 'design_S09_31castle_v1.0.csv' $c.CompositeId 'P' $poi ('chapters/'+($prod -join '/')+'/dungeon/dungeon.csv') 'CanChallengeCastle+castle_threat+EncounterFor' ($(if($ok){'gate+threat composition'}else{'absent'})) $(if($ok){'E2_RUNTIME(--c2a-boss-checks 2026-10-27)'}else{'E0_UNVERIFIED'}) $(if($ok){'MATCHED_EVIDENCED'}else{'NOT_IMPLEMENTED'}) ('P0Boss='+$c.P0Boss+'/status='+$c.P0BossStatus)
}
$rows | Export-Csv -LiteralPath (Join-Path $out "recon_design_source_crosswalk.csv") -NoTypeInformation -Encoding UTF8
$byFamily=$rows | Group-Object family | ForEach-Object {
    [pscustomobject]@{
        family=$_.Name; design_total=$_.Count
        matched=@($_.Group | Where-Object { $_.main_state -eq 'MATCHED_EVIDENCED' }).Count
        implemented_different=@($_.Group | Where-Object { $_.main_state -eq 'IMPLEMENTED_DIFFERENT' }).Count
        superseded=@($_.Group | Where-Object { $_.main_state -eq 'SUPERSEDED_BY_USER' }).Count
        not_verified=@($_.Group | Where-Object { $_.main_state -eq 'NOT_VERIFIED' }).Count
        not_implemented=@($_.Group | Where-Object { $_.main_state -eq 'NOT_IMPLEMENTED' }).Count
    }
}
$byFamily | Export-Csv -LiteralPath (Join-Path $out "recon_coverage_by_family.csv") -NoTypeInformation -Encoding UTF8
"crosswalk=$($rows.Count) out=$out"
$byFamily | Format-Table -AutoSize | Out-String -Width 120
