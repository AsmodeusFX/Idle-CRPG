<#
.SYNOPSIS
把外部 P1-5 交接包的候选怪物身份与区域投放池，转成工程内可加载的原型内容表。

.DESCRIPTION
只做“内容身份层”：
- 身份属性沿用工程现有怪物模板（archetype.template → rat/guard），不引入外部数值；
- 远程/近战与掉落跟随模板与 archetype；
- 区域池按统治区 areapart 落地为 chapters/actN/monster/region_pool.csv。

外部包（docs/temp/...）只读；本脚本可重复执行，已生成的 a1_/a2_… 行会被替换而不是重复追加。
-Idempotent 由“先剔除 ^a[1-5]_ 行再整体重写”保证。
-Check 只比较差异，不写文件。

用法：
  pwsh tools/import_act_monsters.ps1 -Check
  pwsh tools/import_act_monsters.ps1
#>
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'docs/temp/P1_5_FiveActs_Monsters_Boss_V1_0/data'
$cfg = Join-Path $root 'Idle-CRPG/config'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$changed = New-Object System.Collections.Generic.List[string]

function Read-Csv([string]$path) { Import-Csv -LiteralPath $path -Encoding UTF8 }
function Read-Text([string]$path) { [IO.File]::ReadAllText($path) }
function Write-Csv([string]$path, [string[]]$header, $rows, [string]$why) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append(($header -join ',')).Append("`n")
    foreach ($row in $rows) {
        $cells = foreach ($h in $header) { $v = [string]$row.$h; if ($v -match '[",\r\n]') { '"' + $v.Replace('"', '""') + '"' } else { $v } }
        [void]$sb.Append(($cells -join ',')).Append("`n")
    }
    $text = $sb.ToString()
    $old = if (Test-Path -LiteralPath $path) { Read-Text $path } else { '' }
    if ($old -ne $text) {
        $changed.Add($why)
        if (-not $Check) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
            [IO.File]::WriteAllText($path, $text, $utf8)
        }
    }
}
function Write-ImportMeta([string]$path) {
    $meta = $path + '.import'
    $text = "[remap]`n`n" + 'importer="keep"' + "`n"
    if (-not (Test-Path -LiteralPath $meta) -or (Read-Text $meta) -ne $text) {
        $changed.Add("meta: " + (Split-Path -Leaf $path))
        if (-not $Check) { [IO.File]::WriteAllText($meta, $text, $utf8) }
    }
}
function Is-Generated([string]$id) { return $id -match '^a[1-5]_' }

if (-not (Test-Path -LiteralPath $src)) { throw "缺少外部交接包：$src（该目录由用户维护，不做复制）" }

$catalog = Read-Csv (Join-Path $src 'monster_catalog_PROPOSAL.csv')
$pools   = Read-Csv (Join-Path $src 'region_pools_PROPOSAL.csv')
$poiSnap = Read-Csv (Join-Path $src 'poi_source_snapshot_REFERENCE.csv')
$arch    = Read-Csv (Join-Path $cfg 'monster/preview_archetypes.csv')
$monsters = Read-Csv (Join-Path $cfg 'monster/monsters.csv')
$attrs    = Read-Csv (Join-Path $cfg 'combat/unit_attributes.csv')
$drops    = Read-Csv (Join-Path $cfg 'monster/monster_drop.csv')

$archById = @{}; foreach ($a in $arch) { $archById[$a.id] = $a }
$monById  = @{}; foreach ($m in $monsters) { $monById[$m.id] = $m }

# 1) 身份 ID：ACT01_FALLEN -> a1_fallen；跨章前缀避免与现有 rat/guard 冲突。
$identities = New-Object System.Collections.Generic.List[object]
foreach ($c in $catalog) {
    if ($c.act -notmatch '^ACT0([1-5])$') { throw "未知章节：$($c.act)" }
    $actNo = [int]$Matches[1]
    $slug = $c.monster_content_id -replace '^ACT0[1-5]_', ''
    $slug = $slug.ToLowerInvariant()
    $id = "a${actNo}_$slug"
    $a = $archById[$c.archetype_id]
    if ($null -eq $a) { throw "archetype 未定义：$($c.archetype_id)（$($c.monster_content_id)）" }
    $tpl = $monById[$a.template]
    if ($null -eq $tpl) { throw "怪物模板未定义：$($a.template)" }
    $identities.Add([pscustomobject]@{
        Id = $id; Name = $c.display_name_candidate; Act = $actNo
        Archetype = $c.archetype_id; Template = $a.template; Ranged = $a.ranged
        Hp = $tpl.hp; Attack = $tpl.attack; Defense = $tpl.defense
        Speed = $tpl.speed; Range = $tpl.range; Cooldown = $tpl.cooldown
    })
}
$dup = $identities | Group-Object Id | Where-Object Count -gt 1
if ($dup) { throw "身份 ID 重复：$($dup.Name -join ',')" }

# 2) 既有 monsters.csv 必须保持原样：新增身份写独立表，避免改变旧单地牢与 legacy 回归的随机怪物集合。
$monHeader = @('id','name','hp','attack','defense','speed','range','cooldown')
$originRows = @($monsters | Where-Object { -not (Is-Generated $_.id) } | ForEach-Object { [pscustomobject]@{ id=$_.id; name=$_.name; hp=$_.hp; attack=$_.attack; defense=$_.defense; speed=$_.speed; range=$_.range; cooldown=$_.cooldown } })
Write-Csv (Join-Path $cfg 'monster/monsters.csv') $monHeader $originRows 'monster/monsters.csv'

$actRows = @($identities | Sort-Object Id | ForEach-Object { [pscustomobject]@{ id=$_.Id; name=$_.Name; hp=$_.Hp; attack=$_.Attack; defense=$_.Defense; speed=$_.Speed; range=$_.Range; cooldown=$_.Cooldown } })
Write-Csv (Join-Path $cfg 'monster/act_monsters.csv') $monHeader $actRows 'monster/act_monsters.csv'
Write-ImportMeta (Join-Path $cfg 'monster/act_monsters.csv')

# 3) unit_attributes.csv：新身份必须有一行，否则接敌容量读取会报错。
$attrHeader = @('id','side','dodge_rate','crit_rate','crit_damage','block_rate','block_reduction','ranged','engagement_capacity','fire_resistance','cold_resistance','lightning_resistance','poison_resistance')
$attrRows = @($attrs | Where-Object { -not (Is-Generated $_.id) } | ForEach-Object { $r=[ordered]@{}; foreach($h in $attrHeader){ $r[$h]=$_.$h }; [pscustomobject]$r })
$attrRows += @($identities | Sort-Object Id | ForEach-Object {
    [pscustomobject]@{ id=$_.Id; side='monster'; dodge_rate=0; crit_rate=0; crit_damage=0; block_rate=0; block_reduction=0; ranged=$_.Ranged; engagement_capacity=0; fire_resistance=0; cold_resistance=0; lightning_resistance=0; poison_resistance=0 }
})
Write-Csv (Join-Path $cfg 'combat/unit_attributes.csv') $attrHeader $attrRows 'combat/unit_attributes.csv'

# 4) monster_drop.csv：掉落沿用模板；不新造经济数值。
$dropHeader = @('monster_id','item_id','chance','quantity_min','quantity_max')
$dropRows = @($drops | Where-Object { -not (Is-Generated $_.monster_id) } | ForEach-Object { [pscustomobject]@{ monster_id=$_.monster_id; item_id=$_.item_id; chance=$_.chance; quantity_min=$_.quantity_min; quantity_max=$_.quantity_max } })
foreach ($identity in ($identities | Sort-Object Id)) {
    $rows = @($drops | Where-Object { $_.monster_id -eq $identity.Template })
    foreach ($d in $rows) { $dropRows += [pscustomobject]@{ monster_id=$identity.Id; item_id=$d.item_id; chance=$d.chance; quantity_min=$d.quantity_min; quantity_max=$d.quantity_max } }
}
Write-Csv (Join-Path $cfg 'monster/monster_drop.csv') $dropHeader $dropRows 'monster/monster_drop.csv'

# 5) 区域池：REGION_NN -> apNN（与章节 areapart 一一对应，已在交接时核对）。
$idByContent = @{}; foreach ($i in $identities) { $idByContent[$i.Id] = $i.Id }
$byActRegion = @{}
foreach ($p in $pools) {
    if ($p.region_key -notmatch '^ACT0([1-5]):REGION_(\d+)$') { throw "区域键非法：$($p.region_key)" }
    $actNo = [int]$Matches[1]; $regionNo = [int]$Matches[2]
    $slug = $p.monster_content_id -replace '^ACT0[1-5]_', ''
    $id = "a${actNo}_$($slug.ToLowerInvariant())"
    if (-not $idByContent.ContainsKey($id)) { throw "区域池引用了未定义身份：$($p.monster_content_id)" }
    $key = "$actNo|ap$regionNo"
    if (-not $byActRegion.ContainsKey($key)) { $byActRegion[$key] = New-Object System.Collections.Generic.List[object] }
    $byActRegion[$key].Add([pscustomobject]@{ areapart_id = "ap$regionNo"; monster_id = $id; weight = [int]$p.weight })
}
$poolHeader = @('areapart_id','monster_id','weight')
foreach ($actNo in 1..5) {
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($key in ($byActRegion.Keys | Where-Object { $_ -like "$actNo|*" } | Sort-Object {
        [int](($_ -split '\|')[1] -replace 'ap','') })) {
        $group = $byActRegion[$key]
        $sum = ($group | Measure-Object -Property weight -Sum).Sum
        if ($sum -ne 100) { throw "$key 权重合计 $sum ≠ 100" }
        $ordered = $group | Sort-Object @{Expression='weight';Descending=$true}, monster_id
        foreach ($g in $ordered) { $rows.Add($g) }
    }
    $path = Join-Path $cfg "chapters/act$actNo/monster/region_pool.csv"
    Write-Csv $path $poolHeader $rows "chapters/act$actNo/monster/region_pool.csv"
    Write-ImportMeta $path
}

if ($changed.Count -eq 0) { Write-Output 'ACT_MONSTER_IMPORT: no changes' }
else {
    Write-Output ("ACT_MONSTER_IMPORT: " + $(if ($Check) { 'changes pending' } else { 'written' }))
    $changed | ForEach-Object { Write-Output "  $_" }
}
if ($Check -and $changed.Count -gt 0) { exit 1 }
