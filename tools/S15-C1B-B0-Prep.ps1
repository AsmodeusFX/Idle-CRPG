# S15-C1-B / B0 只读对账：把31节点状态投影到78阶，重算可购买容量。
# 只读 docs/temp 输入与 artifacts 输出，不改配置、代码、存档。
param(
    [string]$Workspace="d:/Project-Asmo/Idle-CRPG",
    [string]$Package="docs/temp/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5"
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$pkg=Join-Path $Workspace $Package
$out=Join-Path $Workspace "artifacts/qa/s15-c1b-b0"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$nodes=@{}
foreach($n in Import-Csv (Join-Path $out "node_status_31.csv") -Encoding UTF8){$nodes[$n.design_node]=$n}
$ranks=Import-Csv (Join-Path $pkg "C1B_78阶逐级配置与开售控制.csv") -Encoding UTF8
if($ranks.Count -ne 78){throw "78阶表行数异常：$($ranks.Count)"}
if($nodes.Count -ne 31){throw "节点状态表行数异常：$($nodes.Count)"}

# 每个节点一票主状态；不重复计数。
$rows=@()
foreach($r in $ranks){
    $n=$nodes[$r.NodeId]; if($null -eq $n){throw "阶表引用了未定义节点：$($r.NodeId)"}
    $sellable=switch($n.primary_status){
        "WORKS"{"NOW"}
        "NEEDS_EXTENSION"{"AFTER_B1_B2"}
        "QA_ONLY"{"QA_ONLY"}
        "DEFERRED"{"NEVER_THIS_ROUND"}
        "BLOCKED"{"NEVER_UNTIL_EVIDENCE"}
        "NO_CONSUMER"{"NEVER_UNTIL_CONSUMER"}
        default{throw "未知状态：$($n.primary_status)"}
    }
    $rows+=[pscustomobject]@{
        design_node=$r.NodeId; rank=[int]$r.Rank; max_rank=[int]$r.MaxRank
        price_gold_H=[int]$r.PriceGold_H; price_approved=$r.PriceApproved
        effect_type=$n.effect_type; primary_status=$n.primary_status
        sellable_class=$sellable; consumer_ref=$n.consumer_ref; prerequisite_or=$r.PrerequisiteOR
    }
}
$header=($rows[0].PSObject.Properties.Name | ForEach-Object {'"'+$_+'"'}) -join ','
$body=$rows | ForEach-Object { ($_.PSObject.Properties.Value | ForEach-Object { '"'+([string]$_ -replace '"','""')+'"' }) -join ',' }
[IO.File]::WriteAllText((Join-Path $out "rank_status_78.csv"),($header+"`r`n")+(($body -join "`r`n")+"`r`n"),$utf8)

# 容量重算：按阶数（不是节点数），并区分职业资格节点在多职业队伍里的可用性。
$byStatus=$rows | Group-Object primary_status | ForEach-Object { "$($_.Name)=$($_.Count)" }
$rankSumByNode=$rows | Group-Object design_node | ForEach-Object { $_.Count }
$classNodes=@("C-BAR","C-RAN","C-SOR","C-PAL","C-NEC")
$usableForTeam={
    param([int]$classes)
    ($rows | Where-Object {
        $_.sellable_class -in @("NOW","AFTER_B1_B2") -and
        ($_.design_node -notin $classNodes -or [array]::IndexOf($classNodes,$_.design_node) -lt $classes)
    }).Count
}
$capacity=[ordered]@{
    total_ranks=$rows.Count
    now_sellable=($rows | Where-Object sellable_class -eq "NOW").Count
    after_b1_b2=($rows | Where-Object sellable_class -eq "AFTER_B1_B2").Count
    never_this_round=($rows | Where-Object sellable_class -eq "NEVER_THIS_ROUND").Count
    never_until_consumer=($rows | Where-Object sellable_class -eq "NEVER_UNTIL_CONSUMER").Count
    never_until_evidence=($rows | Where-Object sellable_class -eq "NEVER_UNTIL_EVIDENCE").Count
    qa_only=($rows | Where-Object sellable_class -eq "QA_ONLY").Count
    reachable_4class=(& $usableForTeam 4)
    reachable_2class=(& $usableForTeam 2)
    reachable_1class=(& $usableForTeam 1)
    package_claimed=@{four_class=68;two_class=66;one_class=65;after_no_consumer=@(57,55,54)}
    by_status=$byStatus
    ranks_per_node=$rankSumByNode
}
$capacity | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out "capacity_recompute.json") -Encoding UTF8

# 与包的容量口径核对（包用"节点数"当"阶数"扣减，这里如实列出差异）
$packageSensitivity=Import-Csv (Join-Path $pkg "C1B_可购买容量敏感性.csv") -Encoding UTF8
$packageSensitivity | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $out "package_capacity_sensitivity.json") -Encoding UTF8

"OUT=$out"
"RANKS=$($rows.Count) byStatus: $($byStatus -join ' ')"
"NOW=$($capacity.now_sellable) AFTER_B1B2=$($capacity.after_b1_b2) QA=$($capacity.qa_only) DEFER=$($capacity.never_this_round) NOCONSUMER=$($capacity.never_until_consumer) BLOCKED=$($capacity.never_until_evidence)"
"REACH 4/2/1 = $($capacity.reachable_4class)/$($capacity.reachable_2class)/$($capacity.reachable_1class)"
