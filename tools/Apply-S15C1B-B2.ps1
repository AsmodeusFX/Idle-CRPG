# S15-C1-B / B2：O05 精英伤害、D04 受治疗增强、G02 精英击杀XP（各2/2/3阶）。
# 价格取[H]受控测试值；幂等追加；布局沿用C1-B网格，避免与已有节点重叠。
param(
    [string]$Root="d:/Project-Asmo/Idle-CRPG/Idle-CRPG",
    [string]$Package="d:/Project-Asmo/Idle-CRPG/docs/temp/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5"
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$star=Join-Path $Root "config/star"
$defs=@(
 "O05|elite_damage_pct|0.03|attack||精英猎手|8|0|O03;O04",
 "D04|healing_received_pct|0.05|defense||恢复强化|8|1|D02;D03",
 "G02|elite_xp_pct|0.10|utility||悟性增进|8|4|s_xp_01"
)
$rankRows=Import-Csv (Join-Path $Package "C1B_78阶逐级配置与开售控制.csv") -Encoding UTF8
$costs=@{}
foreach($r in $rankRows){ if(-not $costs.ContainsKey($r.NodeId)){$costs[$r.NodeId]=@()}; $costs[$r.NodeId]+=@([int]$r.PriceGold_H) }
function CostList([string]$id){ $rows=$costs[$id]; return ($rows -join ';') }
function MaxRank([string]$id){ return [int]$costs[$id].Count }

$contentPath=Join-Path $star "star.csv"; $layoutPath=Join-Path $star "star_layout.csv"
$haveContent=@((Import-Csv $contentPath -Encoding UTF8) | ForEach-Object { $_.id })
$haveLayout=@((Import-Csv $layoutPath -Encoding UTF8) | ForEach-Object { $_.id })
$newContent=New-Object System.Collections.Generic.List[string]
$newLayout=New-Object System.Collections.Generic.List[string]
foreach($d in $defs){
    $f=$d -split '\|'; $id=$f[0]
    if($haveContent -notcontains $id){ $newContent.Add(($id,$f[5],(MaxRank $id),"gold",(CostList $id),$f[1],"flat",$f[2],$f[3],$f[4]) -join ',') }
    if($haveLayout -notcontains $id){ $newLayout.Add(($id,$f[6],$f[7],$f[8],$f[5]) -join ',') }
}
function Append([string]$path,[string[]]$lines){
    if($lines.Count -eq 0){return 0}
    $text=[IO.File]::ReadAllText($path,$utf8); if(-not $text.EndsWith("`n")){$text+="`r`n"}
    [IO.File]::WriteAllText($path,($text+($lines -join "`r`n")+"`r`n"),$utf8); return $lines.Count
}
$c=Append $contentPath $newContent; $l=Append $layoutPath $newLayout
$planPath=Join-Path $star "star_plan.csv"
if(Test-Path $planPath){
    $havePlan=@((Import-Csv $planPath -Encoding UTF8) | ForEach-Object { $_.id })
    $newPlan=New-Object System.Collections.Generic.List[string]
    foreach($d in $defs){ $f=$d -split '\|'; if($havePlan -notcontains $f[0]){ $newPlan.Add(($f[0],$f[5]) -join ',') } }
    Append $planPath $newPlan | Out-Null
}
"added star=$c layout=$l nodes=$((Import-Csv $contentPath -Encoding UTF8).Count)"
