param([int]$Seed=20261018)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$configRoot=Join-Path $repoRoot 'Idle-CRPG/config'
$regions=Import-Csv (Join-Path $configRoot 'world/region.csv')
# 参照用户截图的 35 个错位城堡锚点；初始城堡固定在 (0,0)。
$anchors=@('0,0','7,0','14,0','6,1','12,1','14,1','4,2','5,2','7,2','0,3','5,4','9,4','0,5','10,5','13,5','1,6','12,6','0,7','6,7','15,7','15,8','2,9','4,9','5,9','8,10','6,11','9,12','0,14','2,14','8,14','9,14','1,15','2,15','6,15','14,15')
$points=@($anchors | ForEach-Object { $xy=$_.Split(','); [pscustomobject]@{X=[int]$xy[0];Y=[int]$xy[1]} })
function Neighbors([int]$cell) {
    $x=$cell%16;$y=[int][Math]::Floor($cell/16)
    if($x -gt 0){$cell-1};if($x -lt 15){$cell+1};if($y -gt 0){$cell-16};if($y -lt 15){$cell+16}
}
$complete=$false
for($attempt=0;$attempt -lt 100 -and !$complete;$attempt++) {
    $random=[Random]::new($Seed+$attempt)
    $owner=[int[]]::new(256);for($i=0;$i -lt 256;$i++){$owner[$i]=-1}
    $counts=[int[]]::new($points.Count)
    $frontiers=@(foreach($point in $points){,[Collections.Generic.HashSet[int]]::new()})
    for($i=0;$i -lt $points.Count;$i++){$owner[$points[$i].Y*16+$points[$i].X]=$i;$counts[$i]=1}
    for($i=0;$i -lt $points.Count;$i++){foreach($n in (Neighbors ($points[$i].Y*16+$points[$i].X))){if($owner[$n] -lt 0){[void]$frontiers[$i].Add($n)}}}
    for($remaining=256-$points.Count;$remaining -gt 0;$remaining--) {
        $bestScore=[double]::PositiveInfinity;$bestPart=-1;$bestCell=-1
        for($i=0;$i -lt $points.Count;$i++) {
            if($counts[$i] -ge 24){continue}
            foreach($cell in $frontiers[$i]) {
                $distance=[Math]::Abs($cell%16-$points[$i].X)+[Math]::Abs([int][Math]::Floor($cell/16)-$points[$i].Y)
                $score=$counts[$i]*2+$distance*.8+$random.NextDouble()*1.8
                if($counts[$i] -lt 3){$score-=100}
                if($score -lt $bestScore){$bestScore=$score;$bestPart=$i;$bestCell=$cell}
            }
        }
        if($bestPart -lt 0){break}
        $owner[$bestCell]=$bestPart;$counts[$bestPart]++
        foreach($frontier in $frontiers){[void]$frontier.Remove($bestCell)}
        foreach($n in (Neighbors $bestCell)){if($owner[$n] -lt 0){[void]$frontiers[$bestPart].Add($n)}}
    }
    $complete=($owner -notcontains -1)
}
if(!$complete){throw '无法生成连续的统治区布局'}
# 按统治区邻接图广度优先编号，初始区的三个邻区自然编号为 2、3、4。
$order=[Collections.Generic.List[int]]::new();$order.Add(0)
$seen=[Collections.Generic.HashSet[int]]::new();[void]$seen.Add(0)
for($cursor=0;$cursor -lt $order.Count;$cursor++) {
    $partIndex=$order[$cursor];$adjacent=[Collections.Generic.HashSet[int]]::new()
    for($cell=0;$cell -lt 256;$cell++) {
        if($owner[$cell] -ne $partIndex){continue}
        foreach($n in (Neighbors $cell)){if($owner[$n] -ne $partIndex){[void]$adjacent.Add($owner[$n])}}
    }
    foreach($next in ($adjacent | Sort-Object {$points[$_].Y},{$points[$_].X})) {if($seen.Add($next)){$order.Add($next)}}
}
$renumber=@{};for($i=0;$i -lt $order.Count;$i++){$renumber[$order[$i]]=$i}
$points=@($order | ForEach-Object {$points[$_]});$counts=@($order | ForEach-Object {$counts[$_]})
for($cell=0;$cell -lt 256;$cell++){$owner[$cell]=$renumber[$owner[$cell]]}
$utf8=[Text.UTF8Encoding]::new($false)
function WriteConfig([string]$path,[string[]]$lines){[IO.File]::WriteAllLines((Join-Path $configRoot $path),$lines,$utf8)}
$rulers=[Collections.Generic.List[string]]::new();$rulers.Add('region_id,castle_id')
$dungeons=[Collections.Generic.List[string]]::new();$dungeons.Add('id,name,region_id,kind,level_min,level_max,x,y')
$parts=[Collections.Generic.List[string]]::new();$parts.Add('id,castle_id,initially_unlocked')
$edges=[Collections.Generic.HashSet[string]]::new()
$normalCounts=[int[]]::new(256)
for($i=0;$i -lt $points.Count;$i++) {
    $point=$points[$i]
    $cells=@(0..255 | Where-Object {$owner[$_] -eq $i} | Sort-Object { [Math]::Abs($_%16-$point.X)+[Math]::Abs([int][Math]::Floor($_/16)-$point.Y) },{$_})
    $total=[Math]::Max(3,[Math]::Min(10,$cells.Count))
    for($j=0;$j -lt $total;$j++){$normalCounts[$cells[$j%$cells.Count]]++}
}
foreach($region in $regions) {
    $cell=[int]$region.y*16+[int]$region.x;$part=$owner[$cell]+1;$regionId=[int]$region.id
    $rulers.Add("$regionId,c$part")
    # 按整个统治区分配 3–10 个普通地牢；边缘可只有村庄，小统治区可在同格放两个。
    $level=1+[int][Math]::Floor(([int]$region.x+[int]$region.y)/3)
    for($j=1;$j -le $normalCounts[$cell];$j++) {
        $entryY=if($j -eq 1){'.25'}else{'.75'};$entryLevel=$level+$j-1
        $dungeons.Add("d${regionId}_$j,$($region.name)洞窟$j,$regionId,normal,$entryLevel,$entryLevel,.7,$entryY")
    }
    foreach($n in (Neighbors $cell)) {if($owner[$n] -ne $owner[$cell] -and $part -ne 1){[void]$edges.Add("ap$part,ap$($owner[$n]+1)")}}
}
for($i=0;$i -lt $points.Count;$i++) {
    $number=$i+1;$point=$points[$i];$region=$regions | Where-Object {[int]$_.x -eq $point.X -and [int]$_.y -eq $point.Y}
    $regionId=[int]$region.id;$level=3+[int][Math]::Floor(($point.X+$point.Y)/3)
    $dungeons.Add("c$number,$($region.name)城堡,$regionId,castle,$level,$level,.45,.2")
    $flag=if($i -eq 0){'1'}else{'0'};$parts.Add("ap$number,c$number,$flag")
}
WriteConfig 'world/castle_region.csv' $rulers.ToArray()
WriteConfig 'world/areapart.csv' $parts.ToArray()
WriteConfig 'world/areapart_prerequisite.csv' (@('areapart_id,required_areapart_id')+@($edges | Sort-Object))
WriteConfig 'dungeon/dungeon.csv' $dungeons.ToArray()
Write-Output "Generated: $($points.Count) castles, $(($normalCounts|Measure-Object -Sum).Sum) normal dungeons; dominion cells min/max: $(($counts|Measure-Object -Minimum).Minimum)/$(($counts|Measure-Object -Maximum).Maximum); each dominion has 3-10 normal dungeons; seed $Seed, attempt $attempt"
