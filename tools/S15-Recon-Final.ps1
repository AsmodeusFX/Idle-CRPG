param([string]$Workspace="d:/Project-Asmo/Idle-CRPG")
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$cfg=Join-Path $Workspace "Idle-CRPG/config"
$refs=Join-Path $Workspace "docs/temp/Steam_D2_设计工程一致性只读对账执行包_V1.0/refs_仅对账证据非执行授权"
$pkg2=Join-Path $Workspace "docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包"
$c1bDir=Join-Path $pkg2 "P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5"
$c2bDir=Join-Path $pkg2 "P2-4 S15_C2-B｜装备、卷轴与药剂完整投放"
$c3Dir=Join-Path $pkg2 "P2-4 S15_C3_首轮数值配置与运行验收_V0.7"
$stamp=Get-Date -Format "yyyyMMdd-HHmmss"
$out=Join-Path $Workspace "artifacts/qa/s15-c1-c3-recon-final-$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
function T($p){ return @(Import-Csv -LiteralPath $p -Encoding UTF8) }

$talents=T (Join-Path $cfg "skill/talent.csv")
$ti=@{}; foreach($x in $talents){ $ti[$x.id]=$x }
$skillIds=@{}; foreach($s in (T (Join-Path $cfg "skill/skill.csv"))){ $skillIds[$s.id]=$s }

$rows=New-Object System.Collections.Generic.List[object]
function Add($family,$srcFile,$srcId,$auth,$prodId,$cfgRow,$consumer,$idMatch,$consumerPresent,$behaviorEq,$runtimeProof,$state,$openItem,$note){
    $rows.Add([pscustomobject]@{
        family=$family; source_file=$srcFile; source_id=$srcId; source_authority=$auth
        production_id=$prodId; config_row=$cfgRow; consumer=$consumer
        identity_match=$idMatch; consumer_present=$consumerPresent; behavior_equivalence=$behaviorEq
        runtime_proof=$runtimeProof; main_state=$state; open_item=$openItem; note=$note
    })
}

# ---------- 1) 16 技能：ID匹配 + 行为等价逐项 ----------
$skillBehavior=@{
 'BAR-S1'=@('barb_bash','EQUIVALENT','单体近战伤害');'BAR-S2'=@('barb_whirlwind_window','APPROX','原地周期脉冲，未实现施放中位移');
 'BAR-S3'=@('barb_taunt','EQUIVALENT','范围嘲讽 forced');'BAR-S4'=@('barb_ancestor','EQUIVALENT','召唤上限1/限时');
 'RAN-S1'=@('ran_shot','EQUIVALENT','单发弹道');'RAN-S2'=@('ran_multishot','EQUIVALENT','3弹道扇面');
 'RAN-S3'=@('ran_guided_arrow','EQUIVALENT','追踪弹道');'RAN-S4'=@('ran_valkyrie','EQUIVALENT','召唤');
 'SOR-S1'=@('mage_fireball','EQUIVALENT','投射物+火伤');'SOR-S2'=@('sor_frost_nova','EQUIVALENT','范围冰伤+减速');
 'SOR-S3'=@('sor_chain_lightning','EQUIVALENT','连锁4目标');'SOR-S4'=@('sor_meteor','APPROX','前摇+命中点爆发，无固定落点/预警');
 'PAL-S1'=@('paladin_smite','EQUIVALENT','单体近战');'PAL-S2'=@('pal_shield_bash','EQUIVALENT','伤害+眩晕');
 'PAL-S3'=@('pal_prayer','EQUIVALENT','主动治疗脉冲');'PAL-S4'=@('pal_hammer','APPROX','绕体径向脉冲，未实现飞锤轨迹')
}
foreach($k in $skillBehavior.Keys){
    $v=$skillBehavior[$k];$prodId=$v[0];$eq=$v[1];$note=$v[2]
    $ok=$skillIds.ContainsKey($prodId)
    Add 'C1A_SKILL' 'design_S06_v0.8.md' $k 'P' $prodId ('skill/skill.csv:'+$prodId) 'SkillRuntime.RunActiveSkill' $(if($ok){'YES'}else{'NO'}) $(if($ok){'YES'}else{'NO'}) $eq $(if($ok){'E2_RUNTIME(--c1a-checks)'}else{'E0_UNVERIFIED'}) $(if(-not $ok){'NOT_IMPLEMENTED'}elseif($eq -eq 'EQUIVALENT'){'MATCHED_EVIDENCED'}else{'IMPLEMENTED_DIFFERENT'}) $(if($eq -eq 'EQUIVALENT'){'NO'}else{'YES'}) $note
}
# ---------- 2) 4职业T ----------
$tBehavior=@(
 @('BAR-T01','warrior_trait','APPROX','当前仅单节点被动，设计T路线分支未拆分'),
 @('RAN-T01','ranger_trait','APPROX','同上'),@('SOR-T01','mage_trait','APPROX','同上'),
 @('PAL-T01','priest_trait','APPROX','T拆为守护/进攻两条互斥光环，与设计单T身份为近似'),
 @('PAL-T01B','pal_trait_zeal','APPROX','进攻光环为互斥分支，设计未逐条确认'))
foreach($x in $tBehavior){
    $k=$x[0];$prodId=$x[1];$eq=$x[2];$note=$x[3]
    $ok=$ti.ContainsKey($prodId)
    Add 'C1A_T' 'design_S06_v0.8.md' $k 'P' $prodId ('skill/talent.csv:'+$prodId) 'ClassTraitUnlocked+runtime' $(if($ok){'YES'}else{'NO'}) $(if($ok){'YES'}else{'NO'}) $eq $(if($ok){'E2_RUNTIME(--c1a-checks)'}else{'E0_UNVERIFIED'}) $(if($ok){'IMPLEMENTED_DIFFERENT'}else{'NOT_IMPLEMENTED'}) 'YES' $note
}

# ---------- 3) S07 98 节点逐DesignId定性 ----------
$alias=@{
 'BAR-S201'='barb_whirlwind_window';'BAR-S301'='barb_taunt';'BAR-S401'='barb_ancestor';'BAR-T01'='warrior_trait';
 'RAN-S201'='ran_multishot';'RAN-S301'='ran_guided_arrow';'RAN-S401'='ran_valkyrie';'RAN-T01'='ranger_trait';
 'SOR-S201'='sor_frost_nova';'SOR-S301'='sor_chain_lightning';'SOR-S401'='sor_meteor';'SOR-T01'='mage_trait';
 'PAL-S201'='pal_shield_bash';'PAL-S301'='pal_prayer';'PAL-S401'='pal_hammer';'PAL-T01'='priest_trait'}
function Classify98($kind,$effect,$beh){
    if($kind -eq 'MISSING'){ return @('UNKNOWN','生产节点缺失') }
    if($kind -eq 'active'){ return @('EQUIVALENT','槽位绑定与自动施法一致') }
    if($kind -eq 'passive'){ return @('APPROX','单节点被动承载职业特性，T路线未拆分') }
    # 条件/一次性/评分/站桩类语义：一律视为近似，交由设计Owner确认
    if($beh -match '下一次|下一击|限一次|每场一次|结束后'){ return @('APPROX','设计为下一次/一次性语义，实现为常量修饰或命中后短Buff') }
    if($beh -match '优先|评分|集中|选择|威胁|转火'){ return @('APPROX','设计为目标评分/优先，实现为范围/射程/系数修饰') }
    if($beh -match '路径|位移|过程中|持续命中'){ return @('APPROX','设计为移动路径命中，实现为范围修饰') }
    if($beh -match '位置|站位|护卫|护阵|掩护'){ return @('APPROX','设计为站位/护卫语义，实现为属性或范围修饰') }
    if($beh -match '精英|首领'){ return @('APPROX','设计含精英指向，实现无精英乘区/评分') }
    if($kind -eq 'trigger'){
        if($beh -match '硬直|眩晕|减速|免疫|内置间隔'){ return @('EQUIVALENT','控制/减益+ICD 与设计一致') }
        return @('APPROX','设计为条件触发/一次性语义，实现为命中后短Buff+限额')
    }
    # 维度一致性：设计文本维度和生产 effect 维度必须同向，否则记近似
    $dim=$null
    if($beh -match '伤害|强度|系数'){ $dim='coefficient' }
    elseif($beh -match '范围|半径|覆盖'){ $dim='extent' }
    elseif($beh -match '持续|时长'){ $dim='duration' }
    elseif($beh -match '目标数|数量|弹道|跳跃'){ $dim='targets' }
    elseif($beh -match '冷却|频率'){ $dim='cooldown' }
    elseif($beh -match '射程|距离'){ $dim='range' }
    elseif($beh -match '召唤|继承'){ $dim='summon' }
    if($null -eq $dim){ return @('APPROX','设计意图非纯数值维度，无法确认等价') }
    $effectDim = if($effect -like 'summon_*'){'summon'}else{$effect}
    if($effectDim -ne $dim){ return @('APPROX',('设计维度='+$dim+'，实现维度='+$effect)) }
    return @('EQUIVALENT',('同为'+$dim+'维度且数值方向一致'))
}
foreach($d in (T (Join-Path $refs "design_S07_98nodes_v0.8.csv"))){
    $prodId = if($alias.ContainsKey($d.NodeId)){ $alias[$d.NodeId] } else { $d.NodeId }
    $p=$ti[$prodId]
    $kind = if($p){$p.kind}else{'MISSING'}
    $effect = if($p){$p.effect}else{''}
    $r=Classify98 $kind $effect $d.Behavior
    $eq=$r[0];$why=$r[1]
    $state = if($eq -eq 'EQUIVALENT'){'MATCHED_EVIDENCED'} elseif($eq -eq 'APPROX'){'IMPLEMENTED_DIFFERENT'} else {'NOT_IMPLEMENTED'}
    $open = if($eq -eq 'EQUIVALENT'){'NO'}else{'YES'}
    Add 'C1A2_TALENT98' 'design_S07_98nodes_v0.8.csv' $d.NodeId 'P' ($(if($p){$prodId}else{'MISSING'})) ($(if($p){'skill/talent.csv:'+$prodId}else{'--'})) ($(if($p){'kind='+$kind+'/effect='+$effect}else{'--'})) $(if($p){'YES'}else{'NO'}) $(if($p){'YES'}else{'NO'}) $eq $(if($p){'E2_RUNTIME(--c1a2-checks)'}else{'E0_UNVERIFIED'}) $state $open ($why+' | 设计行为：'+$d.Behavior)
}

# ---------- 4) C1B 31 星图 + 路线 ----------
$b0=@{}; foreach($n in (T (Join-Path $Workspace "artifacts/qa/s15-c1b-b0/node_status_31.csv"))){ $b0[$n.design_node]=$n }
foreach($d in (T (Join-Path $c1bDir "C1B_31节点真实消费者与售卖门禁.csv"))){
    $prodId='MISSING';$st='(unknown)';$state='NOT_VERIFIED';$proof='E1_STATIC'
    if($b0.ContainsKey($d.NodeId)){
        $prodId=$b0[$d.NodeId].production_star_id;$st=$b0[$d.NodeId].primary_status
        if($st -in @('WORKS','NEEDS_EXTENSION','NO_CONSUMER')){$state='MATCHED_EVIDENCED';$proof='E2_RUNTIME(--c1b-checks)'}
        elseif($st -in @('DEFERRED','QA_ONLY','BLOCKED')){$state='NOT_IMPLEMENTED'}
    }
    Add 'C1B_STAR31' 'C1B_31节点真实消费者与售卖门禁.csv' $d.NodeId 'P' $prodId ('star/star.csv:max='+$d.MaxRank) $d.ConsumerToVerify $(if($prodId -ne 'MISSING'){'YES'}else{'NO'}) $(if($prodId -ne 'MISSING'){'YES'}else{'NO'}) $(if($state -eq 'MATCHED_EVIDENCED'){'EQUIVALENT'}else{'UNKNOWN'}) $proof $state $(if($state -eq 'MATCHED_EVIDENCED'){'NO'}else{'YES'}) ('SaleGate='+$d.SaleGate+'/status='+$st)
}
Add 'C1B_ROUTE' 'C1B_两阵容替代购买合法路线_条件性.csv' '57/55' 'P' 's_root+39 nodes' 'star/star.csv;star_layout.csv' 'tests --c1b3-checks' 'YES' 'YES' 'EQUIVALENT' 'E2_RUNTIME(--c1b3-checks 2026-10-27)' 'MATCHED_EVIDENCED' 'NO' '设计包已迁移到P2-Part2深层目录；复跑时临时恢复输入'

# ---------- 5) 装备 28 / 卷轴 5 / 药剂 4 / 怪级 31 / 城堡 31 ----------
foreach($t in (T (Join-Path $refs "design_S11_28templates_P.csv"))){
    $charm=($t.TemplateId -match 'CHARM_HP')
    Add 'C2B_EQUIP28' 'design_S11_28templates_P.csv' $t.TemplateId 'P' ($t.SlotId+'/'+$t.MainStat) ('item/equipment.csv:'+$t.SlotId) 'EquipmentValue' 'YES' 'YES' $(if($charm){'N/A'}else{'EQUIVALENT'}) $(if($charm){'SUPERSEDED(EQ-01)'}else{'E2_RUNTIME(--c2b-checks)'}) $(if($charm){'SUPERSEDED_BY_USER'}else{'MATCHED_EVIDENCED'}) 'NO' $(if($charm){'CHARM_HP 首轮不投放'}else{''})
}
$scrollMap=@{'SC01'='scroll_bolt';'SC02'='scroll_arc';'SC03'='scroll_flame';'SC04'='scroll_frost';'SC05'='scroll_banner'}
$potionMap=@{'PO01'='potion_might';'PO02'='potion_tough';'PO03'='potion_greed';'PO04'='potion_swift'}
foreach($d in (T (Join-Path $c2bDir "C2B_5卷轴4药剂真实行为与工程扩展合同.csv"))){
    $auth= if($d.DesignId -eq 'SC01' -or $d.DesignId -eq 'PO01'){'C'}else{'P'}
    if($d.Category -eq 'SCROLL'){
        Add 'C2B_SCROLL' 'C2B_5卷轴4药剂真实行为与工程扩展合同.csv' $d.DesignId $auth $scrollMap[$d.DesignId] ('item/scroll.csv:'+$scrollMap[$d.DesignId]) 'Consumables.UseScroll' 'YES' 'YES' 'EQUIVALENT' 'E2_RUNTIME(--c2b-checks 2026-10-27)' 'MATCHED_EVIDENCED' 'NO' $d.TargetEffect
    } else {
        $sup = $d.DesignId -eq 'PO02'
        Add 'C2B_POTION' 'C2B_5卷轴4药剂真实行为与工程扩展合同.csv' $d.DesignId $auth $potionMap[$d.DesignId] ('item/potion.csv:'+$potionMap[$d.DesignId]) 'AdvanceConsumables' 'YES' 'YES' $(if($sup){'N/A'}else{'EQUIVALENT'}) 'E2_RUNTIME(--c2b-checks 2026-10-27)' $(if($sup){'SUPERSEDED_BY_USER'}else{'MATCHED_EVIDENCED'}) $(if($sup){'YES'}else{'NO'}) $(if($sup){'设计[P/H] +15%MaxHP 被 POT-01[C] 覆盖为百分比减伤，15%仍[H]'}else{$d.TargetEffect})
    }
}
$drop=@{}; foreach($r in (T (Join-Path $cfg "item/drop_table.csv"))){ $drop[$r.monster_level]=$r }
foreach($l in (T (Join-Path $c3Dir "C3_31怪级联合参数待校准_H-B1.csv"))){
    $lvl=$l.MonsterLevel;$ok=$drop.ContainsKey($lvl)
    Add 'C2B_DROP31' 'C3_31怪级联合参数待校准_H-B1.csv' ('LEVEL_'+$lvl) 'H' ('level='+$lvl) 'item/drop_table.csv' 'DropRoll' 'YES' $(if($ok){'YES'}else{'NO'}) 'UNKNOWN' 'E1_STATIC(数值为[H])' $(if($ok){'IMPLEMENTED_DIFFERENT'}else{'NOT_IMPLEMENTED'}) 'YES' '设计表标注 H_NOT_AUTHORIZED；生产为工程首轮[H]值，数值等价未验证'
}
foreach($c in (T (Join-Path $refs "design_S09_31castle_v1.0.csv"))){
    $poi=$c.PoiId;$found=$false
    foreach($n in 1..4){ $d=Join-Path $cfg ("chapters/act$n/dungeon/dungeon.csv"); if(Test-Path $d){ if(@(T $d | Where-Object { $_.id -eq $poi }).Count -gt 0){ $found=$true } } }
    Add 'C2A_CASTLE31' 'design_S09_31castle_v1.0.csv' $c.CompositeId 'P' $poi 'chapters/act*/dungeon/dungeon.csv' 'CanChallengeCastle+castle_threat+EncounterFor' $(if($found){'YES'}else{'NO'}) $(if($found){'YES'}else{'NO'}) $(if($found){'EQUIVALENT'}else{'UNKNOWN'}) $(if($found){'E2_RUNTIME(--c2a-boss-checks)'}else{'E0_UNVERIFIED'}) $(if($found){'MATCHED_EVIDENCED'}else{'NOT_IMPLEMENTED'}) 'NO' ('P0Boss='+$c.P0Boss)
}

$rows | Export-Csv -LiteralPath (Join-Path $out "recon_final_design_master_250.csv") -NoTypeInformation -Encoding UTF8
$rows | Group-Object family | ForEach-Object {
    [pscustomobject]@{
        family=$_.Name; total=$_.Count
        identity_match=@($_.Group | Where-Object { $_.identity_match -eq 'YES' }).Count
        consumer_present=@($_.Group | Where-Object { $_.consumer_present -eq 'YES' }).Count
        behavior_equivalent=@($_.Group | Where-Object { $_.behavior_equivalence -eq 'EQUIVALENT' }).Count
        behavior_approx=@($_.Group | Where-Object { $_.behavior_equivalence -eq 'APPROX' }).Count
        behavior_unknown=@($_.Group | Where-Object { $_.behavior_equivalence -eq 'UNKNOWN' }).Count
        matched=@($_.Group | Where-Object { $_.main_state -eq 'MATCHED_EVIDENCED' }).Count
        impl_diff=@($_.Group | Where-Object { $_.main_state -eq 'IMPLEMENTED_DIFFERENT' }).Count
        superseded=@($_.Group | Where-Object { $_.main_state -eq 'SUPERSEDED_BY_USER' }).Count
        not_impl=@($_.Group | Where-Object { $_.main_state -eq 'NOT_IMPLEMENTED' }).Count
        open=@($_.Group | Where-Object { $_.open_item -eq 'YES' }).Count
    }
} | Export-Csv -LiteralPath (Join-Path $out "recon_final_stats_by_family.csv") -NoTypeInformation -Encoding UTF8
$rows | Where-Object { $_.open_item -eq 'YES' } | Select-Object family,source_id,production_id,behavior_equivalence,main_state,note |
    Export-Csv -LiteralPath (Join-Path $out "recon_final_open_items.csv") -NoTypeInformation -Encoding UTF8
"rows=$($rows.Count) out=$out"
($rows | Group-Object main_state | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ' '
($rows | Group-Object behavior_equivalence | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ' '
