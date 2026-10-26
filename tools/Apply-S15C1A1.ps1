# S15-C1-A-1 配置落地：四职业 16 固定技能 + 4 职业特性T（可重复执行，已存在的行不重复追加）
# 只改 Idle-CRPG/config/skill/*.csv 与技能槽、初始技能；不触碰星图/C2/C3 内容。
param([string]$Workspace = "d:/Project-Asmo/Idle-CRPG")
$ErrorActionPreference = "Stop"
$dir = Join-Path $Workspace "Idle-CRPG/config/skill"
$utf8 = New-Object System.Text.UTF8Encoding($false)

function ReadRows([string]$name) { return @(Import-Csv -LiteralPath (Join-Path $dir $name) -Encoding UTF8) }
function WriteRows([string]$name, $rows, [string[]]$columns) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append(($columns -join ','))
    foreach ($r in $rows) { [void]$sb.Append("`r`n"); [void]$sb.Append(($columns | ForEach-Object { [string]$r.$_ }) -join ',') }
    [IO.File]::WriteAllText((Join-Path $dir $name), $sb.ToString() + "`r`n", $utf8)
}
function AppendMissing([string]$name, [string[]]$lines) {
    $path = Join-Path $dir $name
    $text = [IO.File]::ReadAllText($path)
    $existing = @{}
    foreach ($row in @(Import-Csv -LiteralPath $path -Encoding UTF8)) { $existing[$row.id] = $true }
    $add = @()
    foreach ($line in $lines) {
        $id = ($line -split ',')[0]
        if (-not $existing.ContainsKey($id)) { $add += $line; $existing[$id] = $true }
    }
    if ($add.Count -eq 0) { return 0 }
    if (-not $text.EndsWith("`n")) { $text += "`r`n" }
    [IO.File]::WriteAllText($path, $text + (($add -join "`r`n")) + "`r`n", $utf8)
    return $add.Count
}

# 1) 技能槽：四职业固定顺位（S1保底 / S2-S4 投点后自动施放）
$slotColumns = "id", "hero", "slot", "skill_id"
$slots = @(
    @{ id = "warrior-1"; hero = "warrior"; slot = "1"; skill_id = "barb_bash" },
    @{ id = "warrior-2"; hero = "warrior"; slot = "2"; skill_id = "barb_whirlwind_window" },
    @{ id = "warrior-3"; hero = "warrior"; slot = "3"; skill_id = "barb_taunt" },
    @{ id = "warrior-4"; hero = "warrior"; slot = "4"; skill_id = "barb_ancestor" },
    @{ id = "ranger-1"; hero = "ranger"; slot = "1"; skill_id = "ran_shot" },
    @{ id = "ranger-2"; hero = "ranger"; slot = "2"; skill_id = "ran_multishot" },
    @{ id = "ranger-3"; hero = "ranger"; slot = "3"; skill_id = "ran_guided_arrow" },
    @{ id = "ranger-4"; hero = "ranger"; slot = "4"; skill_id = "ran_valkyrie" },
    @{ id = "mage-1"; hero = "mage"; slot = "1"; skill_id = "mage_fireball" },
    @{ id = "mage-2"; hero = "mage"; slot = "2"; skill_id = "sor_frost_nova" },
    @{ id = "mage-3"; hero = "mage"; slot = "3"; skill_id = "sor_chain_lightning" },
    @{ id = "mage-4"; hero = "mage"; slot = "4"; skill_id = "sor_meteor" },
    @{ id = "priest-1"; hero = "priest"; slot = "1"; skill_id = "paladin_smite" },
    @{ id = "priest-2"; hero = "priest"; slot = "2"; skill_id = "pal_shield_bash" },
    @{ id = "priest-3"; hero = "priest"; slot = "3"; skill_id = "pal_prayer" },
    @{ id = "priest-4"; hero = "priest"; slot = "4"; skill_id = "pal_hammer" },
    @{ id = "necromancer-1"; hero = "necromancer"; slot = "1"; skill_id = "nec_raise_skeleton" },
    @{ id = "necromancer-2"; hero = "necromancer"; slot = "2"; skill_id = "nec_amplify_damage" },
    @{ id = "necromancer-3"; hero = "necromancer"; slot = "3"; skill_id = "nec_lower_resist" },
    @{ id = "necromancer-4"; hero = "necromancer"; slot = "4"; skill_id = "nec_corpse_explosion" }
)
WriteRows "skill_slot.csv" $slots $slotColumns

# 2) 初始技能：S1 保底（游侠改为单箭）
$startRows = @(ReadRows "hero_starting_skill.csv")
foreach ($r in $startRows) { if ($r.id -eq "ranger") { $r.skill_id = "ran_shot" } }
WriteRows "hero_starting_skill.csv" $startRows @("id", "skill_id")

# 3) 新增技能定义（pal_prayer 改为主动治疗脉冲）
$skillText = [IO.File]::ReadAllText((Join-Path $dir "skill.csv"))
$skillText = $skillText.Replace(
    "pal_prayer,prototype,ActiveSkill,200,3,3,1,3,pal_prayer_aura,祈祷光环,,,self,",
    "pal_prayer,prototype,ActiveSkill,200,3,3,1,3,pal_prayer_heal,祈祷,,sfx_hit,ally_lowest_hp,")
[IO.File]::WriteAllText((Join-Path $dir "skill.csv"), $skillText, $utf8)
$skillLines = @(
    "ran_shot,prototype,ActiveSkill,40,3,3,1,6,ran_shot_projectile,弓箭射击,,sfx_hit,enemy,",
    "barb_ancestor,prototype,ActiveSkill,600,3,3,1,4,barb_ancestor_summon,先祖援军,,,self,",
    "sor_frost_nova,prototype,ActiveSkill,100,3,3,1,2.6,sor_frost_nova_burst;sor_frost_nova_chill,冰霜新星,,sfx_hit,self,",
    "sor_meteor,prototype,ActiveSkill,200,40,6,1,6.5,sor_meteor_fall,陨星术,,sfx_hit,enemy,",
    "pal_shield_bash,prototype,ActiveSkill,80,3,3,1,1.6,pal_shield_bash_hit;pal_shield_bash_stun,盾击,,sfx_hit,enemy,",
    "pal_hammer,prototype,ActiveSkill,120,3,3,1,2.8,pal_hammer_spin,祝福之锤,,sfx_hit,self,"
)
$added = AppendMissing "skill.csv" $skillLines

# 4) 新增效果
$fxLines = @(
    "ran_shot_projectile,prototype,SpawnProjectile,,,0,9,ran_shot_hit,HitAllOnPathThenExpire,HoldOnSpawnTick,,,,,,,1,0",
    "ran_shot_hit,prototype,Damage,Physical,1,,,,,,,,,,,,,",
    "barb_ancestor_summon,prototype,Summon,,,,,,,,,,,,barb_ancestor,,,",
    "sor_frost_nova_burst,prototype,AreaPulse,Cold,0.7,2.6,,,,,,,,,,,,",
    "sor_frost_nova_chill,prototype,ApplyBuff,,,2.6,,,,,sor_frost_nova_buff,,,,,,,",
    "sor_meteor_fall,prototype,AreaPulse,Fire,1.4,2.8,,,,,,,,,,,,",
    "pal_shield_bash_hit,prototype,Damage,Physical,1,,,,,,,,,,,,,",
    "pal_shield_bash_stun,prototype,ApplyBuff,,,1.6,,,,,pal_shield_bash_stun_buff,,,,,,,",
    "pal_hammer_spin,prototype,AreaPulse,Physical,0.9,2.8,,,,,,,,,,,,"
)
$addedFx = AppendMissing "effect.csv" $fxLines

# 5) 新增 Buff（祈祷治疗比例上调为原型值，仅[H]）
$buffText = [IO.File]::ReadAllText((Join-Path $dir "buff.csv"))
$buffText = $buffText.Replace("pal_prayer_buff,121,Strongest,,pal_prayer_heal,20,,,0,0,ally,pal_prayer_group,1",
    "pal_prayer_buff,121,Strongest,,pal_prayer_heal,20,,,0,0,ally,pal_prayer_group,1")
[IO.File]::WriteAllText((Join-Path $dir "buff.csv"), $buffText, $utf8)
$buffLines = @(
    "sor_frost_nova_buff,60,RefreshDuration,,,,,chill,0.35,0,enemy,,",
    "pal_shield_bash_stun_buff,30,RefreshDuration,,,,,stun,0,0,enemy,,",
    "pal_guard_buff,121,Strongest,,pal_guard_heal,20,,,0,0,ally,pal_guard_group,1",
    "pal_zeal_buff,121,Strongest,attack_speed_pct:add:0.2,,,,,0,0,ally,pal_zeal_group,1"
)
$addedBuff = AppendMissing "buff.csv" $buffLines

# 6) 召唤原型（先祖：上限1、限时8秒、按主人归属）
$addedSummon = AppendMissing "summon.csv" @("barb_ancestor,先祖援军,1,0.6,0.5,0.5,2.5,0.9,3,1,melee,8")

# 7) 新技能对应的可学习主动节点（id 必须等于 skill_id；tree 3 追加，避免与旧三系位置冲突）
$talentPath = Join-Path $dir "talent.csv"
$talents = @(Import-Csv -LiteralPath $talentPath -Encoding UTF8)
$talentColumns = @("id", "hero", "tree", "tree_name", "row", "col", "name", "description", "max_rank", "bonus_allowed", "kind", "prerequisite", "required_rank", "required_points", "effect", "value", "per_rank", "cooldown", "range", "radius", "damage_type", "projectile_speed", "skill_id", "exclusive_group", "class_trait")
function NextFree($hero, $tree) {
    $used = @($talents | Where-Object { $_.hero -eq $hero -and [int]$_.tree -eq $tree })
    $row = 0; while ($used | Where-Object { [int]$_.row -eq $row }) { $row++ }
    return $row
}
$newNodes = @(
    @{ id = "ran_shot"; hero = "ranger"; tree = 3; tree_name = "固定技能"; name = "弓箭射击"; desc = "单箭物理远程基础攻"; cd = 2; range = 6; type = "Physical"; speed = 9 },
    @{ id = "barb_ancestor"; hero = "warrior"; tree = 3; tree_name = "固定技能"; name = "先祖援军"; desc = "召唤一名限时祖先（上限1，8秒）"; cd = 30; range = 4; type = "Physical"; speed = 0 },
    @{ id = "sor_frost_nova"; hero = "mage"; tree = 3; tree_name = "固定技能"; name = "冰霜新星"; desc = "近身冰霜爆发并减速"; cd = 5; range = 2.6; type = "Cold"; speed = 0 },
    @{ id = "sor_meteor"; hero = "mage"; tree = 3; tree_name = "固定技能"; name = "陨星术"; desc = "长前摇后范围火焰爆发"; cd = 10; range = 6.5; type = "Fire"; speed = 0 },
    @{ id = "pal_shield_bash"; hero = "priest"; tree = 3; tree_name = "固定技能"; name = "盾击"; desc = "近战盾击并短暂眩晕"; cd = 4; range = 1.6; type = "Physical"; speed = 0 },
    @{ id = "pal_hammer"; hero = "priest"; tree = 3; tree_name = "固定技能"; name = "祝福之锤"; desc = "绕体范围物理伤害"; cd = 6; range = 2.8; type = "Physical"; speed = 0 }
)
$addedNodes = 0
foreach ($n in $newNodes) {
    if (@($talents | Where-Object { $_.id -eq $n.id }).Count -gt 0) { continue }
    $row = NextFree $n.hero $n.tree
    $talents += [pscustomobject]@{
        id = $n.id; hero = $n.hero; tree = $n.tree; tree_name = $n.tree_name; row = $row; col = 0
        name = $n.name; description = $n.desc; max_rank = 10; bonus_allowed = 1; kind = "active"
        prerequisite = ""; required_rank = 0; required_points = 0; effect = "data"; value = 1; per_rank = 0
        cooldown = $n.cd; range = $n.range; radius = 0; damage_type = $n.type; projectile_speed = $n.speed
        skill_id = $n.id; exclusive_group = ""; class_trait = 0
    }
    $addedNodes++
}
# 8) 职业特性T：圣骑士拆为守护/进攻两条互斥光环；其余三职业T保留被动但改为可观察属性
foreach ($t in $talents) {
    if ($t.id -eq "priest_trait") {
        $t.tree = 3; $t.tree_name = "固定技能"; $t.name = "守护光环"; $t.description = "职业特性：持续为附近队友恢复生命"
        $t.kind = "active"; $t.effect = "data"; $t.skill_id = "pal_guard_aura"; $t.cooldown = 6; $t.range = 3
        $t.prerequisite = ""; $t.required_rank = 0; $t.required_points = 0; $t.class_trait = 1; $t.exclusive_group = "pal_trait_branch"
        $t.max_rank = 1; $t.value = 1; $t.per_rank = 0
    }
    if ($t.id -eq "ranger_trait") { $t.effect = "crit_rate"; $t.value = 0.01; $t.per_rank = 0.01; $t.description = "职业特性：提高暴击率（原型）" }
    if ($t.id -eq "mage_trait") { $t.effect = "crit_rate"; $t.value = 0.01; $t.per_rank = 0.01; $t.description = "职业特性：提高暴击率（原型）" }
    if ($t.id -eq "warrior_trait") { $t.description = "职业特性：提高攻击（原型）" }
}
if (@($talents | Where-Object { $_.id -eq "pal_trait_zeal" }).Count -eq 0) {
    $row = NextFree "priest" 3
    $talents += [pscustomobject]@{
        id = "pal_trait_zeal"; hero = "priest"; tree = 3; tree_name = "固定技能"; row = $row; col = 0
        name = "狂热光环"; description = "职业特性：持续提高附近队友攻击速度"; max_rank = 1; bonus_allowed = 0; kind = "active"
        prerequisite = ""; required_rank = 0; required_points = 0; effect = "data"; value = 1; per_rank = 0
        cooldown = 6; range = 3; radius = 0; damage_type = "Physical"; projectile_speed = 0
        skill_id = "pal_zeal_aura"; exclusive_group = "pal_trait_branch"; class_trait = 1
    }
    $addedNodes++
}
WriteRows "talent.csv" $talents $talentColumns

# 9) 两条 T 光环技能（不占 S1-S4 槽，由职业特性学习后持续维持）
$auraAdded = AppendMissing "skill.csv" @(
    "pal_guard_aura,prototype,ActiveSkill,120,3,3,1,3,pal_guard_aura_fx,守护光环,,,self,",
    "pal_zeal_aura,prototype,ActiveSkill,120,3,3,1,3,pal_zeal_aura_fx,狂热光环,,,self,"
)
$auraFx = AppendMissing "effect.csv" @(
    "pal_guard_aura_fx,prototype,MaintainAura,,,3,,,,,pal_guard_buff,,,,,,,",
    "pal_guard_heal,prototype,Heal,,0.02,,,,,,,,,,,,,",
    "pal_zeal_aura_fx,prototype,MaintainAura,,,3,,,,,pal_zeal_buff,,,,,,,"
)

Write-Host "C1A1_CONFIG_OK skills=+$added fx=+$addedFx buff=+$addedBuff summon=+$addedSummon nodes=+$addedNodes auraSkills=+$auraAdded auraFx=+$auraFx"
