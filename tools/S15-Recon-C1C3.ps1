# S15 C1-C3 只读对账：从真实仓库导出机器可读的交叉表与事实快照。只写 artifacts/qa。
param([string]$Workspace="d:/Project-Asmo/Idle-CRPG")
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$cfg=Join-Path $Workspace "Idle-CRPG/config"
$stamp=Get-Date -Format "yyyyMMdd-HHmmss"
$out=Join-Path $Workspace "artifacts/qa/s15-c1-c3-recon-$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
function W([string]$name,[string[]]$lines){ [IO.File]::WriteAllText((Join-Path $out $name),(($lines -join "`r`n")+"`r`n"),$utf8) }
function T([string]$p){ return @(Import-Csv -LiteralPath $p -Encoding UTF8) }

$files=@(Get-ChildItem -Path (Join-Path $Workspace "Idle-CRPG/Scripts") -Recurse -File -Filter *.cs)
$cfgFiles=@(Get-ChildItem -Path $cfg -Recurse -File -Filter *.csv)
$codeCat=($files | Sort-Object FullName | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }) -join ''
$cfgCat=($cfgFiles | Sort-Object FullName | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }) -join ''
$sha=[System.Security.Cryptography.SHA256]::Create()
$codeHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($codeCat))) -replace '-','').ToLower()
$cfgHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($cfgCat))) -replace '-','').ToLower()
$dotnet=(& dotnet --version | Out-String).Trim()
$fp='{'
$fp+='"stamp":"'+$stamp+'","cs_files":'+$files.Count+',"cs_sha256":"'+$codeHash+'",'
$fp+='"csv_files":'+$cfgFiles.Count+',"csv_sha256":"'+$cfgHash+'",'
$fp+='"dotnet":"'+$dotnet+'","os":"'+[Environment]::OSVersion.VersionString+'"'
$fp+='}'
W "recon_fingerprint.json" @($fp)

$rows=New-Object System.Collections.Generic.List[string]
$rows.Add("Family,DesignId,DesignVersion,Authority,ProductionId,ConfigFileRow,RuntimeConsumer,ActualRuntimeBehavior,EvidenceStrength,MainState")
foreach($s in (T (Join-Path $cfg "skill/skill_slot.csv"))){
    if($s.skill_id -eq ''){continue}
    $rows.Add("SKILL,$($s.skill_id),S06_V0.8,C,$($s.skill_id),skill/skill_slot.csv:$($s.hero):S$($s.slot),SkillRuntime.RunActiveSkill,固定槽位自动施放,E1_STATIC,MATCHED_EVIDENCED")
}
foreach($n in (T (Join-Path $cfg "skill/talent.csv"))){
    $rows.Add("TALENT,$($n.id),S07_V0.8,P,$($n.id),skill/talent.csv,kind=$($n.kind);effect=$($n.effect);skill=$($n.skill_id),FixedSkills/TalentRuntime,E1_STATIC,NOT_VERIFIED")
}
foreach($n in (T (Join-Path $cfg "star/star.csv"))){
    $ret='NO'; if($n.attr_id -in @('skill_slots','party_slots','talent_reset','talent_reset_discount')){$ret='YES'}
    $rows.Add("STAR,$($n.id),C1B_V0.5,P,$($n.id),star/star.csv:max=$($n.max_level),Stars.StarValue,attr=$($n.attr_id);retired=$ret,E1_STATIC,MATCHED_EVIDENCED")
}
foreach($e in (T (Join-Path $cfg "item/equipment.csv"))){
    $rows.Add("EQUIP_SLOT,$($e.slot),S11_V1.2,C,$($e.id),item/equipment.csv,EquipmentValue,attr=$($e.attr);base=$($e.base_value);per=$($e.per_level),E1_STATIC,MATCHED_EVIDENCED")
}
foreach($q in (T (Join-Path $cfg "item/equipment_quality.csv"))){
    $rows.Add("EQUIP_QUALITY,$($q.id),S11_V1.2,P,$($q.id),item/equipment_quality.csv,EquipmentValue,mult=$($q.multiplier);weight=$($q.weight),E1_STATIC,MATCHED_EVIDENCED")
}
foreach($s in (T (Join-Path $cfg "item/scroll.csv"))){
    $rows.Add("SCROLL,$($s.id),C2B,C,$($s.id),item/scroll.csv,Consumables.UseScroll,damage_pct=$($s.damage_pct),E1_STATIC,MATCHED_EVIDENCED")
}
foreach($p in (T (Join-Path $cfg "item/potion.csv"))){
    $rows.Add("POTION,$($p.id),C2B,C,$($p.id),item/potion.csv,AdvanceConsumables,duration=$($p.duration_ticks);attack_pct=$($p.attack_pct),E1_STATIC,NOT_VERIFIED")
}
foreach($d in (T (Join-Path $cfg "item/drop_table.csv"))){
    $rows.Add("DROP_LEVEL,$($d.monster_level),C2B,H,level=$($d.monster_level),item/drop_table.csv,DropRoll,E1_STATIC,NOT_VERIFIED")
}
W "recon_design_id_crosswalk.csv" $rows.ToArray()

$facts=New-Object System.Collections.Generic.List[string]
$facts.Add("FactKey,Value,Source")
$heroes=T (Join-Path $cfg "hero/heroes.csv")
$heroLine=($heroes | ForEach-Object { $_.id + ':atk=' + $_.attack + ',hp=' + $_.hp }) -join ';'
$facts.Add("hero_base,$heroLine,config/hero/heroes.csv")
$lv=T (Join-Path $cfg "hero/herolevel.csv")
$lvLine=($lv | Select-Object -First 8 | ForEach-Object { 'L' + $_.id + '=' + $_.exp }) -join ';'
$facts.Add("hero_level_exp,$lvLine,config/hero/herolevel.csv")
$sr=(T (Join-Path $cfg "monster/spawn_rule.csv"))[0]
$facts.Add("spawn_rule," + ('count=' + $sr.count_min + '-' + $sr.count_max + ',xp_base=' + $sr.xp_base + ',level=' + $sr.level) + '.' + "config/monster/spawn_rule.csv")
foreach($n in 1..4){
    $d=T (Join-Path $cfg ("chapters/act$n/dungeon/dungeon.csv"))
    $c=@($d | Where-Object { $_.kind -eq 'castle' })
    $line=($c | ForEach-Object { $_.id + ':Lmin=' + $_.level_min + ':Lmax=' + $_.level_max }) -join ';'
    $facts.Add("act${n}_castles,$line,config/chapters/act$n/dungeon/dungeon.csv")
    $fr=Join-Path $cfg ("chapters/act$n/dungeon/floor_rhythm.csv")
    if(Test-Path $fr){
        $f=T $fr
        $h=@($f | Where-Object { $_.pool_bias -eq 'heavy' }).Count
        $r=@($f | Where-Object { $_.pool_bias -eq 'ranged' }).Count
        $m=@($f | Where-Object { $_.pool_bias -eq 'melee' }).Count
        $e=@($f | Where-Object { $_.elite_bias -eq '-10' }).Count
        $facts.Add("act${n}_rhythm," + ('rows=' + $f.Count + ';heavy=' + $h + ';ranged=' + $r + ';melee=' + $m + ';elite-10=' + $e) + '.' + "config/chapters/act$n/dungeon/floor_rhythm.csv")
    }
}
$layout=T (Join-Path $cfg "star/star_layout.csv")
$edges=@($layout | Where-Object { $_.prereq -ne '' }).Count
$multi=@($layout | Where-Object { $_.prereq -like '*;*' }).Count
$roots=@($layout | Where-Object { $_.prereq -eq '' }).Count
$facts.Add("star_layout," + ('node=' + $layout.Count + ';edges=' + $edges + ';multiPrereq=' + $multi + ';root=' + $roots) + '.' + "config/star/star_layout.csv")
$facts.Add("star_rank_gate,engine only checks lit (Level>0); no @Rank threshold in layout or loader","Scripts/Core/StarConfiguration.cs:79")
$eqg=(T (Join-Path $cfg "item/equipment.csv")) | Group-Object attr | ForEach-Object { $_.Name + '=' + $_.Count }
$facts.Add("equipment_attrs," + ($eqg -join ';') + '.' + "config/item/equipment.csv")
$mob=(T (Join-Path $cfg "monster/act_monsters.csv")) | Select-Object -First 6 | ForEach-Object { $_.id + ':hp=' + $_.hp + ',range=' + $_.range }
$facts.Add("mob_sample," + ($mob -join ';') + '.' + "config/monster/act_monsters.csv")
$facts.Add("kp_gate,Lmax cost = Config.Progression.LmaxCost KP and every hero AR/DR >= (Lmax+1)*10","Scripts/Core/KpProgression.cs:29")
$facts.Add("castle_gate,MonsterLevelMax >= poi_profile.challenge (fallback LevelMin) and CastleUnlocked","Scripts/Core/KpProgression.cs:50")
$facts.Add("boss_phase,runtime BossPhaseRuntime.cs + config BossPhases; phase trigger by HP/时间阈值","Scripts/Core/BossPhaseRuntime.cs")
W "recon_facts.csv" $facts.ToArray()
"OUT=$out"
Get-ChildItem $out | ForEach-Object { $_.Name + ' ' + $_.Length }
