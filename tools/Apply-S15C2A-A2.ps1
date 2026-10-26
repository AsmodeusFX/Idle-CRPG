# S15-C2-A / A2：Boss 阶段表 + 城堡威胁组合 + 身份职能标签 + 误导入修正
# 幂等：重复执行结果一致。只写内容选择层，不改数量/等级/奖励/准入权威。
param(
    [string]$Workspace=(Split-Path -Parent $PSScriptRoot),
    [switch]$Check
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$config=Join-Path $Workspace "Idle-CRPG/config"
$pending=New-Object System.Collections.Generic.List[string]

function Emit([string]$path,[string]$text){
    $full=Join-Path $Workspace $path
    $dir=Split-Path -Parent $full
    if(-not (Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force | Out-Null}
    $normalized=$text -replace "`r`n","`n"
    $current=if(Test-Path $full){[IO.File]::ReadAllText($full) -replace "`r`n","`n"}else{$null}
    if($current -ne $normalized){
        $pending.Add($path)
        if(-not $Check){[IO.File]::WriteAllText($full,$normalized.Replace("`n","`r`n"),$utf8)}
    }
}

# ---------------------------------------------------------------- 1. 身份职能标签
# 只描述"池内身份属于哪一档"，与等级/数量/奖励无关；供区域偏好与城堡威胁组合消费。
$archetypes=[ordered]@{
    rat='melee';guard='melee'
    a1_dark_hunter='ranged';a1_dark_priest='support';a1_fallen='melee';a1_fallen_shaman='support'
    a1_goatman='melee';a1_rogue_archer='ranged';a1_skel_archer='ranged';a1_skeleton='melee'
    a1_vampire_mage='ranged';a1_zombie='heavy'
    a2_arcane_mage='ranged';a2_desert_raider='melee';a2_desert_stalker='ranged';a2_fanatic='support'
    a2_sand_caster='ranged';a2_scarab='heavy';a2_skeleton='melee';a2_skeleton_archer='ranged'
    a2_unraveler='support';a2_viper_guard='melee'
    a3_bat='melee';a3_bog_guard='heavy';a3_council='ranged';a3_fetish_archer='ranged'
    a3_flayer='melee';a3_flayer_shaman='support';a3_jungle_stalker='ranged';a3_spider='melee'
    a3_temple_priest='support';a3_zealot='melee'
    a4_abyss_stalker='ranged';a4_demon_archer='ranged';a4_doom_knight='melee';a4_fire_wraith='ranged'
    a4_hell_imp='melee';a4_oblivion_knight='support';a4_pit_lord='heavy';a4_venom_lord='heavy'
    a5_baal_minion='heavy';a5_blood_witch='ranged';a5_cold_guard='melee';a5_fiend_shaman='support'
    a5_frost_archer='ranged';a5_frost_wraith='ranged';a5_imp='melee';a5_minotaur='heavy'
    a5_siege_demon='heavy';a5_slayer='ranged';a5_succubus='ranged'
}
$text="monster_id,archetype"
foreach($key in $archetypes.Keys){$text+="`n$key,$($archetypes[$key])"}
Emit "Idle-CRPG/config/monster/archetypes.csv" ($text+"`n")

# ---------------------------------------------------------------- 2. 四章末 Boss 阶段
# trigger：fight_start 战斗开始 / hp_pct 血量阈值 / sequence 遭遇序列 / floor_stage 地图阶段
# scope：boss 需要 Boss 本体 / room 只按楼层推进
# role：add 阶段随从 / successor 阶段接替的新 Boss
# POI ID 只在章内唯一（A08-C01 同时存在于第1/2章），因此主键必须是 chapter+poi_id。
$phaseHeader="id,chapter,poi_id,phase,trigger,threshold,scope,name,skill_id,buff_id,escort_template,escort_count,role,telegraph"
# 空单元格统一写成 ~（脚本内占位），避免连续竖线在命令行/编辑器间被折叠。
$phases=@(
 'A08-C01-P1|1|A08-C01|1|fight_start|0|boss|毒雾领域|boss_poison_burst|~|a1_fallen|2|add|安达利尔抬手，毒雾自脚下扩散，亡者爬出地面'
 'A08-C01-P2|1|A08-C01|2|hp_pct|50|boss|毒压狂怒|boss_venom_press|boss_enrage|~|0|~|安达利尔嘶鸣，攻速暴涨并喷吐毒液'
 'A07-C01-P1|2|A07-C01|1|fight_start|0|boss|寒冰冲锋|boss_charge|~|a2_scarab|2|add|都瑞尔站定蓄力，甲虫自沙中爬出'
 'A07-C01-P2|2|A07-C01|2|hp_pct|50|boss|冰霜重压|sor_frost_nova|boss_enrage|~|0|~|都瑞尔重击抬手，寒霜沿地面扩散'
 'B07-C01-P1|3|B07-C01|1|fight_start|0|boss|议会结阵|boss_council_bless|~|a3_council|2|add|议会成员逐个入场并结成法阵'
 'B07-C01-P2|3|B07-C01|2|sequence|0|boss|墨菲斯托|boss_chain_bolt|boss_enrage|boss_mephisto|1|successor|议会成员尽数倒下，墨菲斯托自祭坛升起'
 'D04-C01-P1|4|D04-C01|1|floor_stage|3|room|封印西翼|~|~|a4_doom_knight|2|add|西侧封印亮起，末日骑士守卫封印'
 'D04-C01-P2|4|D04-C01|2|floor_stage|4|room|封印东翼|~|~|a4_oblivion_knight|2|add|东侧封印亮起，遗忘骑士展开诅咒'
 'D04-C01-P3|4|D04-C01|3|floor_stage|5|room|封印中央|~|~|a4_pit_lord|1|add|中央封印亮起，地狱领主踏出封印'
 'D04-C01-P4|4|D04-C01|4|floor_stage|6|boss|恐惧之王|boss_fire_ring|boss_enrage|~|0|~|迪亚波罗张开双臂，火焰环在脚下扩散'
 'D04-C01-P5|4|D04-C01|5|hp_pct|50|boss|终末雷暴|boss_chain_bolt|~|a4_hell_imp|2|add|迪亚波罗举掌蓄势，雷电沿锁链跳跃'
)
$phaseLines=@()
foreach($row in $phases){
    $cells=$row -split '\|'
    $mapped=@()
    foreach($cell in $cells){if($cell -eq '~'){$mapped+=''}else{$mapped+=$cell}}
    $phaseLines+=($mapped -join ',')
}
Emit "Idle-CRPG/config/monster/boss_phase.csv" ($phaseHeader+"`n"+($phaseLines -join "`n")+"`n")
# ---------------------------------------------------------------- 3. 31 城堡威胁组合
# composition 是"身份档位循环"（M近战/R远程/H重型/S辅助），总数仍由 spawn_rule 决定。
$castleHeader="id,poi,floor,threat,composition,count_bias,elite_bias"
$castles=@{
 1=@(
  'A01-C01|守门混编|M;M;H|0|0'
  'A02-C01|突袭先锋|M;M;R|-1|0'
  'A03-C01|远程召援|R;M;R|0|0'
  'A04-C01|元素先锋|R;M;M|0|0'
  'A05-C01|召唤支援|M;S;M|0|0'
  'A06-C01|法术阵地|R;R;M|0|0'
  'A07-C01|护阵攻坚|M;M;R;H|1|0'
  'A08-C01|章节首领|M;R;R;S|0|5'
 )
 2=@(
  'A01-C01|沙漠阵地|M;R;M|0|0'
  'A02-C01|重卫护阵|H;H;R|0|0'
  'A03-C01|术士支援|M;R;H|0|0'
  'A04-C01|亡灵压迫|M;S;R|0|0'
  'A05-C01|高速精英|M;H;M|0|10'
  'A06-C01|法术召唤|R;S;R|0|0'
  'A07-C01|章节首领|M;R;M;S|0|5'
  'A08-C01|复活亡者|M;S;M|0|0'
  'A09-C01|法术阵地|R;R;M|0|0'
 )
 3=@(
  'B01-C01|持续状态|M;M;R|0|0'
  'B02-C01|群潮召援|M;M;M;R|1|0'
  'B03-C01|守卫施术|M;M;S;R|0|0'
  'B04-C01|祭坛精英|M;R;S;R|0|10'
  'B05-C01|狂热近战|M;M;M|0|0'
  'B06-C01|远程封锁|R;R;M;H|0|0'
  'B07-C01|章节首领|M;R;M;R;S|0|5'
  'B08-C01|持续瘴气|M;H;M;S|0|0'
  'B09-C01|神庙审判|R;M;H;S|0|0'
 )
 4=@(
  'D01-C01|连续阶段|M;R;M;R|0|0'
  'D02-C01|经典强敌|H;M;H|0|5'
  'D03-C01|精英军团|H;S;R;R|0|10'
  'D04-C01|终章首领|M;R;M;R|0|5'
  'D05-C01|熔炉强敌|H;R;H;S|0|0'
  'D04-C01@3|封印西翼|H;M;M|0|0'
  'D04-C01@4|封印东翼|H;S;M|0|0'
  'D04-C01@5|封印中央|H;H;M|0|0'
  'D04-C01@6|王座|M;R;H;R|0|10'
 )
}
foreach($chapter in @(1,2,3,4)){
    $lines=@()
    foreach($entry in $castles[$chapter]){
        $cells=$entry -split '\|'
        $poi=$cells[0];$floor=0
        if($poi -match '@'){$parts=$poi -split '@';$poi=$parts[0];$floor=[int]$parts[1]}
        $lines+=(@("$poi-$floor",$poi,$floor,$cells[1],$cells[2],$cells[3],$cells[4]) -join ',')
    }
    Emit "Idle-CRPG/config/chapters/act$chapter/dungeon/castle_threat.csv" ($castleHeader+"`n"+($lines -join "`n")+"`n")
}
# ---------------------------------------------------------------- 4. Boss 展示名
# 议会→墨菲斯托是遭遇序列：首领本体显示议会成员，墨菲斯托由 boss_phase 的 successor 身份带入。
$poiPath=Join-Path $Workspace "Idle-CRPG/config/chapters/act3/dungeon/poi_profile.csv"
$poiText=([IO.File]::ReadAllText($poiPath) -replace "`r`n","`n")
$poiNext=$poiText.Replace(',议会成员 → 墨菲斯托,',',议会成员,')
if($poiNext -ne $poiText){$pending.Add("$poiPath (B07 boss_name)");if(-not $Check){[IO.File]::WriteAllText($poiPath,$poiNext.Replace("`n","`r`n"),$utf8)}}

# ---------------------------------------------------------------- 5. 复用既有技能的 Boss 阶段技能包
# 只新增 skill/effect/buff 行并复用既有 Effect 类型；伤害与冷却均为 [H] 原型，留待 C3 调整。
function AppendMissing([string]$path,[string[]]$rows){
    $full=Join-Path $Workspace $path
    $text=([IO.File]::ReadAllText($full) -replace "`r`n","`n")
    $lines=$text -split "`n"
    $missing=@()
    foreach($row in $rows){if(-not ($lines -contains $row)){$missing+=$row}}
    if($missing.Count -eq 0){return}
    $pending.Add("$path (+$($missing.Count) rows)")
    if($Check){return}
    [IO.File]::WriteAllText($full,($text.TrimEnd("`n")+"`n"+($missing -join "`n")+"`n").Replace("`n","`r`n"),$utf8)
}
function Row([string[]]$cells,[int]$width){
    $copy=@($cells)
    while($copy.Count -lt $width){$copy+=''}
    return ($copy -join ',')
}
AppendMissing "Idle-CRPG/config/skill/skill.csv" @(
  (Row @('boss_poison_burst','boss','ActiveSkill','200','20','8','1','2.8','boss_poison_burst_fx;boss_poison_cloud_apply','毒雾爆发','','sfx_hit','self','') 14)
  (Row @('boss_venom_press','boss','ActiveSkill','150','12','6','1.4','1.8','boss_venom_strike_fx','毒液重压','','sfx_hit','enemy','') 14)
  (Row @('boss_charge','boss','ActiveSkill','180','16','8','1.4','2.2','mob_heavy_fx','寒冰冲锋','','sfx_hit','enemy','') 14)
  (Row @('boss_fire_ring','boss','ActiveSkill','220','24','10','1','3.2','boss_fire_ring_fx','火焰环','','sfx_hit','self','') 14)
  (Row @('boss_council_bless','boss','ActiveSkill','300','10','6','1','3','boss_council_bless_apply','议会祝祷','','sfx_hit','self','') 14)
  (Row @('boss_chain_bolt','boss','ActiveSkill','90','8','4','1','6','sor_chain_lightning_chain','连锁雷击','','sfx_hit','enemy','') 14)
)
AppendMissing "Idle-CRPG/config/skill/effect.csv" @(
  (Row @('boss_poison_burst_fx','prototype','AreaPulse','Poison','0.6','2.8') 18)
  (Row @('boss_poison_cloud_apply','prototype','ApplyBuff','','','2.8','','','','','boss_poison_cloud') 18)
  (Row @('boss_venom_strike_fx','prototype','Damage','Poison','1.8') 18)
  (Row @('boss_fire_ring_fx','prototype','AreaPulse','Fire','0.8','3.2') 18)
  (Row @('boss_council_bless_apply','prototype','ApplyBuff','','','3','','','','','boss_council_bless') 18)
)
AppendMissing "Idle-CRPG/config/skill/buff.csv" @(
  (Row @('boss_poison_cloud','200','RefreshDuration','poison_resistance:add:-0.2','','','','','','','enemy') 13)
  (Row @('boss_council_bless','160','Strongest','attack_speed_pct:add:0.15','','','','','','','ally') 13)
  (Row @('boss_enrage','400','RefreshDuration','attack_speed_pct:add:0.25','','','','','','','self') 13)
)
# 阶段接替的独立 Boss 身份（不是把普通怪改名）：需要属性与掉落行。
AppendMissing "Idle-CRPG/config/monster/act_monsters.csv" @(
  'boss_mephisto,墨菲斯托,140,14,4,1.4,2.2,1.6'
)
AppendMissing "Idle-CRPG/config/combat/unit_attributes.csv" @(
  'boss_mephisto,monster,0,0.05,1.5,0,0,true,2,0.1,0.25,0.25,0.5'
)
AppendMissing "Idle-CRPG/config/monster/monster_drop.csv" @(
  'boss_mephisto,gold,1,2,6'
  'boss_mephisto,fang,0.5,1,2'
  'boss_mephisto,relic,0.15,1,1'
)

# ---------------------------------------------------------------- 6. 配置导入元数据
# CSV 一律 keep；禁止被 Godot 当作翻译表导入。
$keep="[remap]`n`nimporter=`"keep`"`n"
foreach($path in @(
    "Idle-CRPG/config/monster/archetypes.csv.import",
    "Idle-CRPG/config/monster/boss_phase.csv.import",
    "Idle-CRPG/config/chapters/act1/dungeon/castle_threat.csv.import",
    "Idle-CRPG/config/chapters/act2/dungeon/castle_threat.csv.import",
    "Idle-CRPG/config/chapters/act3/dungeon/castle_threat.csv.import",
    "Idle-CRPG/config/chapters/act4/dungeon/castle_threat.csv.import"
)){
    $full=Join-Path $Workspace $path
    $current=if(Test-Path $full){[IO.File]::ReadAllText($full)}else{$null}
    if($current -ne $keep.Replace("`n","`r`n")){
        $pending.Add($path)
        if(-not $Check){[IO.File]::WriteAllText($full,$keep.Replace("`n","`r`n"),$utf8)}
    }
}
# A1 遗留：floor_rhythm/hero_unlocks 曾被自动导入为翻译表，改回 keep 并移走误生成资源。
foreach($chapter in @(1,2,3,4)){
    $path="Idle-CRPG/config/chapters/act$chapter/dungeon/floor_rhythm.csv.import"
    $full=Join-Path $Workspace $path
    if((Test-Path $full) -and ([IO.File]::ReadAllText($full) -match 'csv_translation')){
        $pending.Add($path+" (retranslate→keep)")
        if(-not $Check){[IO.File]::WriteAllText($full,$keep.Replace("`n","`r`n"),$utf8)}
    }
}
$heroImport="Idle-CRPG/config/hero/hero_unlocks.csv.import"
$heroFull=Join-Path $Workspace $heroImport
if((Test-Path $heroFull) -and ([IO.File]::ReadAllText($heroFull) -match 'csv_translation')){
    $pending.Add("$heroImport (retranslate→keep)")
    if(-not $Check){[IO.File]::WriteAllText($heroFull,$keep.Replace("`n","`r`n"),$utf8)}
}
$legacyRoot=Join-Path $Workspace "artifacts/legacy-csv-translations"
foreach($file in (Get-ChildItem (Join-Path $Workspace "Idle-CRPG/config") -Recurse -Filter "*.translation" -ErrorAction SilentlyContinue)){
    $pending.Add(($file.FullName.Substring($Workspace.Length+1)+" (move→artifacts)"))
    if(-not $Check){
        if(-not (Test-Path $legacyRoot)){New-Item -ItemType Directory -Path $legacyRoot -Force | Out-Null}
        Move-Item -LiteralPath $file.FullName -Destination (Join-Path $legacyRoot $file.Name) -Force
    }
}

if($Check){
    if($pending.Count -eq 0){"S15C2A-A2 config: up to date"}
    else{"S15C2A-A2 config: "+$pending.Count+" pending";$pending | ForEach-Object {$_}}
}else{
    "S15C2A-A2 config written; changed="+$pending.Count
    $pending | ForEach-Object {$_}
}
