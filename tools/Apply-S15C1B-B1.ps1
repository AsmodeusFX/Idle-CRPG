# S15-C1-B / B1：17个NEEDS_EXTENSION节点（46阶）落地；价格取[H]仅作受控测试值。
# 幂等：按节点ID追加，已存在不重复。旧9可买节点与10退休节点保持不变。
param(
    [string]$Root="d:/Project-Asmo/Idle-CRPG/Idle-CRPG",
    [string]$Package="d:/Project-Asmo/Idle-CRPG/docs/temp/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5"
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$star=Join-Path $Root "config/star"

# 节点定义：设计ID|attr_id|每级值|icon|target|名称|布局col|布局row|布局前置（分号=任一）
$defs=@(
 "O02|attack_speed_pct|0.02|attack||连斩节奏|4|0|s_atk",
 "O03|crit_rate|0.01|attack||暴击潜力|5|0|O02",
 "O04|element_damage_pct|0.02|attack||元素驾驭|4|1|s_atk",
 "D02|block_rate|0.01|defense||格挡练习|4|2|s_hp",
 "D03|element_resistance|0.02|defense||异常抵抗|4|3|s_hp",
 "R01|attack_rating|2|attack||攻击评级研习|5|4|s_root",
 "R02|defense_rating|2|defense||防御评级研习|5|5|s_root",
 "C-RAN|class_trait_unlock|1|utility|ranger|游侠特性资格|7|0|s_atk;s_xp_01",
 "C-SOR|class_trait_unlock|1|utility|mage|法师特性资格|7|1|s_atk;s_xp_01",
 "C-PAL|class_trait_unlock|1|utility|priest|圣骑士特性资格|7|2|s_hp;s_atk",
 "S01|scroll_damage_pct|0.05|utility||卷轴威力|6|4|s_atk;s_xp_01",
 "P01|potion_duration_pct|0.10|utility||药剂时效|6|5|s_hp",
 "P02|potion_effect_pct|0.05|utility||药剂效力|6|6|P01",
 "A01|travel_pct|0.03|utility||行进熟练|5|6|s_root",
 "F01|kill_gold_pct|0.03|utility||财富积累|4|5|s_root",
 "F02|sell_gold_pct|0.03|utility||售卖收益|4|6|F01",
 "F03|offline_pct|0.05|utility||离线修行|4|8|F01"
)
$rankRows=Import-Csv (Join-Path $Package "C1B_78阶逐级配置与开售控制.csv") -Encoding UTF8
$costs=@{}
foreach($r in $rankRows){ if(-not $costs.ContainsKey($r.NodeId)){$costs[$r.NodeId]=@()}; $costs[$r.NodeId]+=@([int]$r.Rank,[int]$r.PriceGold_H) }
function CostList([string]$id){
    $flat=$costs[$id]; $pairs=@()
    for($i=0;$i -lt $flat.Count;$i+=2){ $pairs+=$flat[$i+1] }
    return ($pairs | Sort-Object) -join ';'
}
function MaxRank([string]$id){ return [int](($costs[$id] | Where-Object { $_ -gt 0 }).Count/2) }

$contentPath=Join-Path $star "star.csv"; $layoutPath=Join-Path $star "star_layout.csv"
$content=Import-Csv $contentPath -Encoding UTF8
$layout=Import-Csv $layoutPath -Encoding UTF8
$haveContent=@($content | ForEach-Object { $_.id }); $haveLayout=@($layout | ForEach-Object { $_.id })
$newContent=New-Object System.Collections.Generic.List[string]
$newLayout=New-Object System.Collections.Generic.List[string]
foreach($d in $defs){
    $f=$d -split '\|'; $id=$f[0]
    if($haveContent -notcontains $id){
        $max=MaxRank $id; $cost=CostList $id
        $newContent.Add(($id,$f[5],$max,"gold",$cost,$f[1],"flat",$f[2],$f[3],$f[4]) -join ',')
    }
    if($haveLayout -notcontains $id){ $newLayout.Add(($id,$f[6],$f[7],$f[8],$f[5]) -join ',') }
}
function Append([string]$path,[string[]]$lines){
    if($lines.Count -eq 0){return 0}
    $text=[IO.File]::ReadAllText($path,$utf8); if(-not $text.EndsWith("`n")){$text+="`r`n"}
    $text+=($lines -join "`r`n")+"`r`n"; [IO.File]::WriteAllText($path,$text,$utf8); return $lines.Count
}
$c=Append $contentPath $newContent; $l=Append $layoutPath $newLayout

# 星图意向表（编辑器用）同步追加，保持三表对齐。
$planPath=Join-Path $star "star_plan.csv"
if(Test-Path $planPath){
    $plan=Import-Csv $planPath -Encoding UTF8; $havePlan=@($plan | ForEach-Object { $_.id })
    $newPlan=New-Object System.Collections.Generic.List[string]
    foreach($d in $defs){ $f=$d -split '\|'; if($havePlan -notcontains $f[0]){ $newPlan.Add(($f[0],$f[5]) -join ',') } }
    Append $planPath $newPlan | Out-Null
}
"added star=$c layout=$l"
"nodes_now=$((Import-Csv $contentPath -Encoding UTF8).Count) layout_now=$((Import-Csv $layoutPath -Encoding UTF8).Count)"
