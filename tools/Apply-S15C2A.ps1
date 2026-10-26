# S15-C2-A / A1：把 226 条普通层遭遇合同落成逐章 floor_rhythm.csv
# 只做内容偏置：数量仍在 spawn_rule 区间、等级仍取 Run.[Lmin,Lmax]、精英概率仍受 elite.csv 约束。
param(
    [string]$Workspace="d:/Project-Asmo/Idle-CRPG",
    [string]$Package="docs/temp/P2-4 S15_C2-A｜地牢、怪物、城堡内容投放"
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$rows=Import-Csv (Join-Path $Workspace (Join-Path $Package "C2A_226层节拍与唯一生成消费者合同.csv")) -Encoding UTF8
if($rows.Count -ne 226){throw "合同表应为226行，实际$($rows.Count)"}

function PoolBias([string]$pattern){
    if($pattern -match '重型近战'){return 'heavy'}
    if($pattern -match '范围法师|辅助施术'){return 'ranged'}
    if($pattern -match '普通射手|后排猎手'){return 'ranged'}
    if($pattern -match '快速集群|基础近战'){return 'melee'}
    return 'mixed'
}
function CountBias([string]$pattern,[string]$pool){
    if($pool -eq 'heavy'){return -1}
    if($pattern -match '快速集群'){return 1}
    return 0
}
function EliteBias([string]$purpose){
    if($purpose -match '集中|提前|前置'){return 10}
    if($purpose -match '普通群|收敛|无精英'){return -10}
    return 0
}

$byChapter=@{}
foreach($r in $rows){
    $chapter=[int]$r.Chapter
    $poi=($r.DungeonKey -split ':',2)[1]
    $floor=[int]$r.FloorIndex
    $pattern=$r.EncounterPattern_P
    $pool=PoolBias $pattern
    $cb=CountBias $pattern $pool
    $eb=EliteBias $r.ElitePurpose_P
    if(-not $byChapter.ContainsKey($chapter)){$byChapter[$chapter]=New-Object System.Collections.Generic.List[string]}
    $byChapter[$chapter].Add((@($chapter,$poi,$floor,$r.RoomRhythm_P,$pattern,$r.ElitePurpose_P,$cb,$eb,$pool) -join ','))
}
$total=0
foreach($n in ($byChapter.Keys | Sort-Object)){
    $path=Join-Path $Workspace "Idle-CRPG/config/chapters/act$n/dungeon/floor_rhythm.csv"
    $header="chapter,poi,floor,rhythm,pattern,elite_purpose,count_bias,elite_bias,pool_bias"
    $text=$header+"`r`n"+(($byChapter[$n] | Sort-Object { [int](($_ -split ',')[2]) }) -join "`r`n")+"`r`n"
    [IO.File]::WriteAllText($path,$text,$utf8)
    $total+=$byChapter[$n].Count
    "act$n rows=$($byChapter[$n].Count)"
}
"total=$total"
