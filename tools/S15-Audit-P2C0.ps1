# S15-C0 只读完整对账（READ_ONLY_RECON）
# 只读取 docs/temp 输入与 Idle-CRPG/config 生产配置，只在 artifacts/qa 下写报告；不改生产配置、docs、存档。
# 用法: pwsh -File tools/S15-Audit-P2C0.ps1 [-Workspace d:/Project-Asmo/Idle-CRPG]
param(
    [string]$Workspace = "d:/Project-Asmo/Idle-CRPG",
    [string]$Package = "docs/temp/P2-4 S15_C0完整输入与独立Review_V0.3"
)
$ErrorActionPreference = "Stop"
$game = Join-Path $Workspace "Idle-CRPG"
$cfg = Join-Path $game "config"
$pkg = Join-Path $Workspace $Package
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $Workspace "artifacts/qa/s15-c0-$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

function WriteText([string]$name, [string]$text) { [IO.File]::WriteAllText((Join-Path $out $name), $text, $utf8) }
function WriteCsv([string]$name, $rows, [string[]]$columns) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine(($columns | ForEach-Object { '"' + ($_ -replace '"', '""') + '"' }) -join ',')
    foreach ($r in $rows) {
        [void]$sb.AppendLine(($columns | ForEach-Object {
            $v = if ($null -eq $r.$_) { "" } else { [string]$r.$_ }
            '"' + ($v -replace '"', '""') + '"'
        }) -join ',')
    }
    [IO.File]::WriteAllText((Join-Path $out $name), $sb.ToString(), $utf8)
}
function ReadCsv([string]$path) { if (-not (Test-Path $path)) { return @() }; return @(Import-Csv -LiteralPath $path -Encoding UTF8) }

$lineMaps = @{}
function LineOf([string]$path, [string]$id) {
    if (-not (Test-Path $path)) { return 0 }
    if (-not $lineMaps.ContainsKey($path)) {
        $map = @{}; $i = 0
        foreach ($line in [IO.File]::ReadAllLines($path)) {
            $i++
            $first = ($line -split ',')[0].Trim('"').Trim()
            if (-not $map.ContainsKey($first)) { $map[$first] = $i }
        }
        $lineMaps[$path] = $map
    }
    $m = $lineMaps[$path]
    if ($m.ContainsKey($id)) { return $m[$id] }
    return 0
}
function Rel([string]$full) { return ($full.Substring($Workspace.Length).TrimStart('\', '/') -replace '\\', '/') }
function Ev([string]$path, [string]$id) { $n = LineOf $path $id; if ($n -gt 0) { return "$(Rel $path):$n" } else { return "$(Rel $path):NOT_FOUND" } }

# ---------------- 生产事实：配置装载 ----------------
$heroes = ReadCsv (Join-Path $cfg "hero/heroes.csv")
$slots = ReadCsv (Join-Path $cfg "skill/skill_slot.csv")
$skills = ReadCsv (Join-Path $cfg "skill/skill.csv")
$effects = ReadCsv (Join-Path $cfg "skill/effect.csv")
$buffs = ReadCsv (Join-Path $cfg "skill/buff.csv")
$talents = ReadCsv (Join-Path $cfg "skill/talent.csv")
$summons = ReadCsv (Join-Path $cfg "skill/summon.csv")
$chains = ReadCsv (Join-Path $cfg "skill/chain.csv")
$triggers = ReadCsv (Join-Path $cfg "skill/trigger.csv")
$procs = ReadCsv (Join-Path $cfg "skill/proc_limit.csv")
$procIcd = ReadCsv (Join-Path $cfg "skill/proc_icd.csv")
$abilities = ReadCsv (Join-Path $cfg "skill/ability.csv")
$stars = ReadCsv (Join-Path $cfg "star/star.csv")
$starLayout = ReadCsv (Join-Path $cfg "star/star_layout.csv")
$equipBases = ReadCsv (Join-Path $cfg "item/equipment.csv")
$equipQual = ReadCsv (Join-Path $cfg "item/equipment_quality.csv")
$scrolls = ReadCsv (Join-Path $cfg "item/scroll.csv")
$potions = ReadCsv (Join-Path $cfg "item/potion.csv")
$monsterLevels = ReadCsv (Join-Path $cfg "monster/monsterlevel.csv")
$actMonsters = ReadCsv (Join-Path $cfg "monster/act_monsters.csv")
$fightAttrs = ReadCsv (Join-Path $cfg "combat/fightattr.csv")
$spawnRule = ReadCsv (Join-Path $cfg "monster/spawn_rule.csv")

$chapters = @{}
foreach ($n in 1..5) {
    $tag = "act$n"
    $chapters[$n] = [pscustomobject]@{
        Tag   = $tag
        Dir   = (Join-Path $cfg "chapters/$tag")
        Dun   = (ReadCsv (Join-Path $cfg "chapters/$tag/dungeon/dungeon.csv"))
        Prof  = (ReadCsv (Join-Path $cfg "chapters/$tag/dungeon/poi_profile.csv"))
        Floor = (ReadCsv (Join-Path $cfg "chapters/$tag/dungeon/floor_plan.csv"))
        Pool  = (ReadCsv (Join-Path $cfg "chapters/$tag/monster/region_pool.csv"))
        Area  = (ReadCsv (Join-Path $cfg "chapters/$tag/world/areapart.csv"))
    }
}
$skillSlotMap = @{}      # "hero:slot" -> skill_id
foreach ($s in $slots) { $skillSlotMap["$($s.hero):$($s.slot)"] = $s.skill_id }
$classOfDesign = @{ BAR = "warrior"; RAN = "ranger"; SOR = "mage"; PAL = "priest"; NEC = "necromancer" }
$classNameCn = @{ warrior = "野蛮人"; ranger = "游侠"; mage = "法师"; priest = "圣骑士"; necromancer = "死灵法师" }
$attrIds = @($fightAttrs | ForEach-Object { $_.attr_id })
$starAttrs = @($stars | ForEach-Object { $_.attr_id } | Sort-Object -Unique)
$talentKinds = @($talents | Group-Object kind | ForEach-Object { "$($_.Name):$($_.Count)" })

# 生产事实摘要
$capability = [ordered]@{
    effect_kinds_configured   = (@($effects | ForEach-Object { $_.kind } | Sort-Object -Unique) -join "/")
    trigger_rows              = $triggers.Count
    proc_limit_rows           = $procs.Count
    proc_icd_rows             = $procIcd.Count
    buff_rows                 = $buffs.Count
    summon_rows               = $summons.Count
    chain_rows                = $chains.Count
    ability_rows              = $abilities.Count
    talent_rows               = $talents.Count
    talent_kinds              = ($talentKinds -join ",")
    star_nodes                = $stars.Count
    star_attrs                = ($starAttrs -join "/")
    equipment_bases           = $equipBases.Count
    equipment_qualities       = (@($equipQual | ForEach-Object { "$($_.id)=$($_.name)" }) -join ",")
    scroll_rows               = $scrolls.Count
    potion_rows               = $potions.Count
    monster_levels            = $monsterLevels.Count
    act_monster_rows          = $actMonsters.Count
    attribute_catalog         = ($attrIds -join "/")
    combat_skill_slots_filled = @($slots | Where-Object { $_.skill_id -ne "" }).Count
    combat_skill_slots_empty  = @($slots | Where-Object { $_.skill_id -eq "" }).Count
}
$dungeonTotal = 0; $castleTotal = 0; $floorNormal = 0
foreach ($n in 1..5) { $dungeonTotal += $chapters[$n].Dun.Count; $castleTotal += @($chapters[$n].Dun | Where-Object { $_.kind -eq "castle" }).Count; $floorNormal += @($chapters[$n].Floor | Where-Object { $_.DifficultyId -eq "NORMAL" }).Count }
$capability["dungeons_chapter_scoped"] = $dungeonTotal
$capability["castles_chapter_scoped"] = $castleTotal
$capability["floor_plan_normal_rows"] = $floorNormal

# ---------------- 1. 输入完整性（SHA/记录数） ----------------
$manifest = ReadCsv (Join-Path $pkg "matrices/source_manifest_verified.csv")
$sourceRows = @()
foreach ($m in $manifest) {
    $path = Join-Path $pkg ($m.included_path -replace '/', '\')
    if (-not (Test-Path $path)) { $path = Join-Path $pkg ("design_sources/" + $m.file_name) }
    $records = -1; $sha = "MISSING"
    if (Test-Path $path) {
        $records = @(Import-Csv -LiteralPath $path -Encoding UTF8).Count
        $sha = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower()
    }
    $sourceRows += [pscustomobject]@{
        source_key   = $m.source_key
        file_name    = $m.file_name
        declared_rec = $m.record_count
        actual_rec   = $records
        rec_match    = if ($records -eq [int]$m.record_count) { "YES" } else { "NO" }
        sha_match    = if ($sha -eq $m.sha256.ToLower()) { "YES" } else { "NO" }
        actual_sha   = $sha
        declared_sha = $m.sha256
        path         = (Rel $path)
    }
}
WriteCsv "source_verification.csv" $sourceRows @("source_key", "file_name", "declared_rec", "actual_rec", "rec_match", "sha_match", "actual_sha", "declared_sha", "path")

# ---------------- 2. 675 项 crosswalk ----------------
$map675 = ReadCsv (Join-Path $pkg "matrices/S15_675项设计工程联合映射_待核对.csv")
$slotTable = ReadCsv (Join-Path $pkg "matrices/S15_四职业16技能槽位与行为差异.csv")
$slotRows = @{}
foreach ($s in $slotTable) { $slotRows[$s.design_id] = $s }

# 技能设计→生产判定（逐槽人工核定，基于 skill/effect/buff 现状）
$skillVerdict = @{
    "BAR-S1" = @{ cap = "单体近战伤害（Damage + 普攻/技能系数）"; occupant = "barb_bash"; match = "STRONG"; direct = "YES"; status = "REAL_REUSE"; gap = "行为一致；系数/硬直为[P]待C3，不改变AR/DR门槛" }
    "BAR-S2" = @{ cap = "有界移动中多段范围物理伤害（AreaPulse + Damage）"; occupant = "barb_taunt"; match = "WEAK"; direct = "NO"; status = "MODIFY"; gap = "生产S2为嘲讽增益、S3才是barb_whirlwind_window；需按角色重绑槽位，并存档仅按skill_id不按槽位" }
    "BAR-S3" = @{ cap = "范围友军增益或有限嘲讽（ApplyBuff + forced）"; occupant = "barb_whirlwind_window"; match = "STRONG"; direct = "YES"; status = "MODIFY"; gap = "barb_taunt在S2；能力齐备，仅槽位重绑S2↔S3" }
    "BAR-S4" = @{ cap = "召唤有限上限实体（Summon + summon.csv）"; occupant = ""; match = "NONE"; direct = "NO"; status = "MODIFY"; gap = "槽位空；summon.csv仅2原型，需新增召唤定义与技能行，无新运行时系统" }
    "RAN-S1" = @{ cap = "单体远程基础射击（普攻 Damage）"; occupant = "ran_multishot"; match = "NONE"; direct = "NO"; status = "MODIFY"; gap = "S1现为多重箭；单箭行为由普攻承担，不能直接当S1技能，需新增单箭技能或改语义" }
    "RAN-S2" = @{ cap = "扇面多弹道分配（SpawnProjectile + damage_parts）"; occupant = "ran_guided_arrow"; match = "STRONG"; direct = "YES"; status = "MODIFY"; gap = "ran_multishot在S1；需槽位重绑" }
    "RAN-S3" = @{ cap = "追踪投射物（AbilityCast arc homing）"; occupant = "ran_valkyrie"; match = "STRONG"; direct = "YES"; status = "REAL_REUSE"; gap = "ran_guided_arrow已有arc_homing原型；槽位S3现为女武神，需重绑" }
    "RAN-S4" = @{ cap = "近战召唤物（Summon，ran_valkyrie原型）"; occupant = ""; match = "NONE"; direct = "NO"; status = "MODIFY"; gap = "槽位空；ran_valkyrie现占S3，需重绑并确认召唤上限1/5s是否满足设计" }
    "SOR-S1" = @{ cap = "范围火伤（Damage+Area/Projectile，fire）"; occupant = "mage_fireball"; match = "STRONG"; direct = "YES"; status = "REAL_REUSE"; gap = "行为一致；半径/系数为[P]" }
    "SOR-S2" = @{ cap = "近身范围冰伤+减速（AreaPulse + chill）"; occupant = "sor_chain_lightning"; match = "NONE"; direct = "NO"; status = "MODIFY"; gap = "S2现为连锁闪电；冰霜新星需新增技能/效果行，chill机制已有消费者" }
    "SOR-S3" = @{ cap = "有限跳跃连锁（Chain + chain.csv）"; occupant = ""; match = "STRONG"; direct = "YES"; status = "MODIFY"; gap = "sor_chain_lightning现占S2；需重绑到S3" }
    "SOR-S4" = @{ cap = "延时落地范围火伤（SpawnProjectile/AreaPulse + windup）"; occupant = ""; match = "NONE"; direct = "NO"; status = "MODIFY"; gap = "槽位空；有范围/延迟参数但无地面预警系统，需新增技能行并确认前摇表现" }
    "PAL-S1" = @{ cap = "单体近战物理伤害"; occupant = "paladin_smite"; match = "STRONG"; direct = "YES"; status = "REAL_REUSE"; gap = "行为一致；系数[P]" }
    "PAL-S2" = @{ cap = "近战命中+短控制（Damage + stun Buff）"; occupant = "pal_prayer"; match = "NONE"; direct = "NO"; status = "MODIFY"; gap = "S2现为祈祷；盾击需新增技能行，stun机制已有消费者，Boss免疫需单列" }
    "PAL-S3" = @{ cap = "受损友军治疗脉冲（Heal + target_policy ally_lowest_hp）"; occupant = "pal_fanaticism"; match = "STRONG"; direct = "YES"; status = "MODIFY"; gap = "pal_prayer现占S2；治疗目标策略已有ally_lowest_hp，需重绑" }
    "PAL-S4" = @{ cap = "有界环绕范围伤害（MaintainAura/AreaPulse）"; occupant = "pal_conviction"; match = "WEAK"; direct = "NO"; status = "MODIFY"; gap = "pal_conviction是增益光环；祝福之锤需新增技能行；环绕判定受1D合法目标限制" }
}

$crossRows = @()
foreach ($r in $map675) {
    $kind = $r.kind
    $productionId = ""; $verifiedKey = ""; $context = ""; $cap = ""; $consumer = ""; $saveImpact = ""; $match = "NONE"; $conflict = "NO"; $direct = "NO"; $migration = "NONE"; $evidence = ""; $note = $r.issue

    switch ($kind) {
        "HERO_SKILL" {
            $v = $skillVerdict[$r.design_id]
            $cls = $classOfDesign[$r.class_id]
            $slotNo = $r.design_id.Substring($r.design_id.Length - 1)
            $occupant = $skillSlotMap["$cls`:fixed_$slotNo"]
            $productionId = if ($occupant) { $occupant } else { "" }
            $verifiedKey = if ($occupant) { $(if ($occupant -in @($skills | ForEach-Object { $_.id })) { "skill.csv 存在且挂载于 $cls/fixed_$slotNo" } else { "skill_slot.csv 引用但 skill.csv 缺失" }) } else { "空槽（skill_slot.csv 空 skill_id）" }
            $context = "$cls/fixed_$slotNo"
            $cap = $v.cap; $consumer = "SkillRuntime.DrainEffects / ApplyBuff / SummonRuntime / ChainRuntime（见 skill/effect.csv）"
            $saveImpact = "存档按 SkillIds（id）与 Talents 记录，不按槽位序号；槽位重绑不触发迁移；新增技能需 FixedSkillsVersion 不变时补默认装配"
            $match = $v.match; $direct = $v.direct; $migration = if ($v.direct -eq "YES") { "NONE" } else { "SKILL_SLOT_REBIND_OR_NEW_ROW" }
            $evidence = (Ev (Join-Path $cfg "skill/skill_slot.csv") $("fixed_" + $slotNo)) + "; " + (Ev (Join-Path $cfg "skill/skill.csv") $occupant)
            $note = $v.gap
        }
        "HERO_TALENT" {
            $beh = $r.design_name
            $cap = "见 effect_coverage（逐节点所需能力）"
            $consumer = "TalentConfiguration + CanLearnFixedTalent + PassiveTalentValue / active talent coefficient"
            $saveImpact = "Talents 字典按节点 id；新节点 id 可增量写入，旧档缺省为空无需迁移"
            $match = "WEAK"; $direct = "NO"; $migration = "NEW_TALENT_ROWS"
            $evidence = (Rel (Join-Path $cfg "skill/talent.csv"))
            $productionId = ""
            $verifiedKey = "生产 talent.csv 无同名节点（设计节点为新增）"
        }
        "STAR" {
            $capAttr = @{ "O01" = "attack"; "D01" = "hp"; "G01" = "xp_gain_pct" }
            $attr = $capAttr[$r.design_id]
            $node = @(if ($attr) { @($stars | Where-Object { $_.attr_id -eq $attr }) } else { @() })
            $verifiedKey = if ($attr) { "star.csv 已有 attr_id=$attr（$($node.Count) 节点）" } else { "star.csv 无该意图的属性（白名单8项）" }
            $productionId = if ($node.Count -gt 0) { ($node | ForEach-Object { $_.id }) -join ";" } else { "" }
            $context = "star/star.csv attr_id 白名单：$($starAttrs -join '/')"
            $cap = switch ($r.design_id) {
                "O02" { "attack_speed_pct 属性（已消费，星图白名单未开放）" }
                "O03" { "crit_rate 属性（已消费，星图白名单未开放）" }
                "O04" { "fire/cold/lightning/poison_damage_pct 乘区（已消费，星图白名单未开放）" }
                "O05" { "无精英伤害乘区（仅 boss_damage_pct）" }
                "D02" { "block_rate 属性（已消费，星图白名单未开放）" }
                "D03" { "fire/cold/lightning/poison_resistance 属性（已消费，星图白名单未开放）" }
                "D04" { "无受治疗效果属性" }
                "R01" { "Actor.AttackRatingBonus 已预留，无星图入口" }
                "R02" { "Actor.DefenseRatingBonus 已预留，无星图入口" }
                "G02" { "无精英额外XP独立收据" }
                "G03" { "无额外天赋点属性（影响S07预算）" }
                { $_ -like "C-*" } { "class_trait_unlock 已消费，仅s_trait_warrior" }
                "S01" { "卷轴伤害倍率无入口（scroll.damage_pct 为配置常量）" }
                "S02" { "卷轴库存无容量上限（Dictionary 无 cap）" }
                "S03" { "无自动使用卷轴策略系统" }
                "P01" { "药剂 duration_ticks 无倍率入口" }
                "P02" { "PotionAttackPct 已消费，无星图倍率" }
                "P03" { "PotionSlotCount=4 常量 + 存档槽数校验 + UI 4槽" }
                "A01" { "行程速度是全局 world.travel_speed，无玩家级倍率" }
                "A02" { "无拾取周转参数" }
                "A03" { "交易/整备读条为全局配置，无玩家级倍率" }
                "F01" { "击杀金币无倍率入口" }
                "F02" { "出售价 ceil(value*0.35) 固定，无倍率" }
                "F03" { "SettleOffline 有结算但无星图参数入口" }
                default { "未在星图属性白名单" }
            }
            $consumer = switch ($r.design_id) {
                "O01" { "Stars.ApplyStarTotals → Actor.Attack（Stars.cs）" }
                "D01" { "Stars.ApplyStarTotals → Actor.MaxHp（Stars.cs）" }
                "G01" { "GameSession.XpGainMultiplier → HeroExperience.GainExperience" }
                { $_ -like "C-*" } { "GameSession.ClassTraitUnlocked / FixedSkills.CanLearnFixedTalent" }
                { $_ -in @("O02", "O03", "O04", "D02", "D03") } { "SkillRuntime.EffectiveValue(actor, attr) 已消费同名词表属性，但星图不产出" }
                { $_ -in @("R01", "R02") } { "AttackRating/DefenseRating 公式读取 *Bonus 字段（当前恒0）" }
                default { "无（仅UI文案 StarEffect）" }
            }
            $attr = if ($r.design_id -eq "O01") { "attack" } elseif ($r.design_id -eq "D01") { "hp" } elseif ($r.design_id -eq "G01") { "xp_gain_pct" } elseif ($r.design_id -like "C-*") { "class_trait_unlock" } else { "" }
            $match = if ($attr) { "STRONG" } elseif ($r.design_id -in @("O02", "O03", "O04", "D02", "D03", "R01", "R02", "P02", "F03", "A01", "A03")) { "WEAK" } else { "NONE" }
            $direct = if ($attr) { "YES" } else { "NO" }
            $migration = if ($attr) { "NONE" } else { "NEW_STAR_ATTR_AND_SAVE" }
            $evidence = (Ev (Join-Path $cfg "star/star.csv") $productionId.Split(';')[0]) + "; " + (Rel (Join-Path $cfg "star/star.csv"))
            $note = switch ($r.design_id) {
                "O05" { "需新增精英伤害乘区或复用 elite 标记；当前仅 boss_damage_pct" }
                "D04" { "需新增受治疗加成属性并接入 Heal" }
                "G02" { "需精英击杀独立XP收据，防止与倍率叠加" }
                "G03" { "影响S07 24点预算，须C3正式跑数后再定" }
                "S02" { "容量数值未定（设计标注待核定）→ INPUT_MISSING" }
                "S03" { "属未来策略系统，首轮不投放 → DESIGN_DECISION" }
                "P03" { "4→6→8 需改常量、UI与存档槽数校验，旧档按4槽迁移" }
                { $_ -like "C-*" } { "机制已有，仅战士有节点；其余4职业需新增节点并避免解锁第5席" }
                default { "需扩展 StarConfiguration 白名单+应用路径与存档校验" }
            }
        }
        "MONSTER_LEVEL" {
            $lv = [int]$r.design_id
            $exists = $lv -le $monsterLevels.Count
            $productionId = if ($exists) { "$lv" } else { "" }
            $verifiedKey = if ($exists) { "monsterlevel.csv 存在该等级（hp/attack 倍率）" } else { "越界" }
            $context = "monsterlevel.csv 1..$($monsterLevels.Count)"
            $cap = "等级倍率存在；设计XP/KP/AR-DR列无生产表"
            $consumer = "LevelConfiguration → Spawn.Create；XP 来自 spawn_rule.xp_base，KP 来自 KpProgression"
            $saveImpact = "MonsterLevelMin/Max 与房间 SnapshotLevel 已持久化"
            $match = if ($exists) { "WEAK" } else { "NONE" }
            $direct = "NO"; $migration = "NUMERIC_H_NEEDS_C3"
            $evidence = (Ev (Join-Path $cfg "monster/monsterlevel.csv") "$lv")
            $note = "H-B1 的 31 档 XP/KP/评级需求无同形生产列，须 C3 授权后才可落库"
        }
        "MONSTER_POOL" {
            $actNo = [int](($r.design_id -split ':')[0] -replace 'ACT0?', '')
            $regionNo = [int](($r.design_id -split 'REGION_')[1])
            $area = @(if ($chapters.Contains($actNo)) { $chapters[$actNo].Area } else { @() })
            $areaId = if ($area.Count -ge $regionNo) { $area[$regionNo - 1].id } else { "" }
            $pool = @(if ($chapters.Contains($actNo)) { @($chapters[$actNo].Pool | Where-Object { $_.areapart_id -eq $areaId }) } else { @() })
            $productionId = if ($areaId) { $areaId } else { "" }
            $verifiedKey = if ($pool.Count -gt 0) { "chapters/$($chapters[$actNo].Tag)/monster/region_pool.csv 有 $($pool.Count) 条权重" } else { "该areapart无池记录" }
            $context = "act$actNo areapart=$areaId"
            $cap = "按统治区抽取身份（Config.PickSpawnMonster）"
            $consumer = "GameSession.Reveal → Config.PickSpawnMonster(rng, areapart)"
            $saveImpact = "不保存池权重；房间 Origins 保存实际 SpecId/Level"
            $match = if ($pool.Count -gt 0) { "STRONG" } else { "NONE" }
            $direct = if ($pool.Count -gt 0) { "YES" } else { "NO" }
            $migration = "POOL_COMPOSITION_REPLACE"
            $evidence = (Ev (Join-Path $cfg "chapters/$($chapters[$actNo].Tag)/monster/region_pool.csv") $areaId)
            $note = "键可对；设计候选怪物ID为旧P1身份，需按C2-A逐条决定复用/新增，当前权重为工程原型组合"
        }
        "MONSTER_IDENTITY" {
            $name = $r.design_name
            $hit = @($actMonsters | Where-Object { $_.name -eq $name -or $_.id -eq ($r.design_id.ToLower()) })
            $productionId = if ($hit.Count -gt 0) { ($hit | ForEach-Object { $_.id }) -join ";" } else { "" }
            $verifiedKey = if ($hit.Count -gt 0) { "act_monsters.csv 同名/同id命中 $($hit.Count)" } else { "act_monsters.csv 无同名身份" }
            $context = "act_monsters.csv ($($actMonsters.Count) 条)"
            $cap = "怪物身份=属性行+行为包（act_behaviors/boss_skill）"
            $consumer = "MonsterPools/region_pool → Actor + ApplyMonsterContent"
            $saveImpact = "Origins 保存 SpecId；身份替换不改存档字段"
            $match = if ($hit.Count -gt 0) { "STRONG" } else { "NONE" }
            $direct = if ($hit.Count -gt 0) { "YES" } else { "NO" }
            $migration = if ($hit.Count -gt 0) { "NONE" } else { "NEW_MONSTER_ROW" }
            $evidence = if ($hit.Count -gt 0) { (Ev (Join-Path $cfg "monster/act_monsters.csv") $hit[0].id) } else { (Rel (Join-Path $cfg "monster/act_monsters.csv")) }
            $note = if ($hit.Count -gt 0) { "名称命中仅证明身份存在，行为差异需C2-A逐条核对" } else { "旧P1身份在生产无对应，需新增行或明确不复用（产品取舍）" }
        }
        "DUNGEON" {
            $chapterNo = [int]$r.chapter
            $poi = ($r.design_id -split ':', 2)[1]
            $rows = @(if ($chapters.Contains($chapterNo)) { @($chapters[$chapterNo].Dun | Where-Object { $_.id -eq $poi }) } else { @() })
            $prof = @(if ($chapters.Contains($chapterNo)) { @($chapters[$chapterNo].Prof | Where-Object { $_.id -eq $poi }) } else { @() })
            $productionId = if ($rows.Count -gt 0) { $rows[0].id } else { "" }
            $verifiedKey = if ($rows.Count -gt 0) { "act$chapterNo dungeon.csv kind=$($rows[0].kind) region=$($rows[0].region_id) Lv$($rows[0].level_min)-$($rows[0].level_max)" } else { "该章 dungeon.csv 无此 POI" }
            $context = "act$chapterNo/$poi"
            $cap = "POI 键+kind+等级区间+poi_profile(optional/required_for_castle/floors)"
            $consumer = "WorldConfig/POI 配置 → BeginDungeon → Dungeon.Generate；解锁由 Cleared 集合推导"
            $saveImpact = "WorldSnapshot.Cleared/ActiveDungeonId 用 POI 字面量，新增不影响旧档"
            $match = if ($rows.Count -gt 0) { "STRONG" } else { "NONE" }
            $direct = if ($rows.Count -gt 0) { "YES" } else { "NO" }
            $migration = "NONE"
            $evidence = (Ev (Join-Path $cfg "chapters/act$chapterNo/dungeon/dungeon.csv") $poi) + "; " + (Ev (Join-Path $cfg "chapters/act$chapterNo/dungeon/poi_profile.csv") $poi)
            $note = "P0 键与拓扑只读确认，不写回；战斗内容按 C2-A 另授权"
        }
        "DUNGEON_NORMAL_FLOOR" {
            $chapterNo = [int]$r.chapter
            $parts = $r.design_id -split ':'
            $poi = $parts[1]; $floor = [int]($parts[2] -replace 'F', '')
            $plan = @(if ($chapters.Contains($chapterNo)) { @($chapters[$chapterNo].Floor | Where-Object { $_.PoiId -eq $poi -and $_.FloorIndex -eq "$floor" -and $_.DifficultyId -eq "NORMAL" }) } else { @() })
            $prof = @(if ($chapters.Contains($chapterNo)) { @($chapters[$chapterNo].Prof | Where-Object { $_.id -eq $poi }) } else { @() })
            $productionId = if ($plan.Count -gt 0) { $plan[0].id } else { "" }
            $verifiedKey = if ($plan.Count -gt 0) { "floor_plan NORMAL 行存在（FloorCount=$($plan[0].FloorCount) ConfigStatus=$($plan[0].ConfigStatus)）" } else { "无该 POI/层 NORMAL 规划行" }
            $context = "act$chapterNo/$poi/F$floor poiprofile.floors=$(if($prof.Count){$prof[0].floors}else{'?'})"
            $cap = "楼层规划行仅用于名称显示与 FloorRunReport；刷怪数量/精英/事件预算来自 spawn_rule/elite/EventChance"
            $consumer = "Config.FloorPlans → CurrentFloorDisplayName / FloorReports；Reveal 仅读 plan.BossActors"
            $saveImpact = "楼层规划不参与存档；房间内容按 SnapshotLevel/Origins 重建"
            $match = if ($plan.Count -gt 0) { "STRONG" } else { "NONE" }
            $direct = if ($plan.Count -gt 0) { "NO" } else { "NO" }
            $migration = "NONE"
            $evidence = (Ev (Join-Path $cfg "chapters/act$chapterNo/dungeon/floor_plan.csv") $productionId)
            $note = "键可对账；但设计节拍（CombatCellCount/EliteCellCount 等）在生成时未被消费，属 RED：需 C2-A 新增消费或明确降级为文案"
        }
        "CASTLE" {
            $chapterNo = [int]$r.chapter
            $poi = ($r.design_id -split ':', 2)[1]
            $rows = @(if ($chapters.Contains($chapterNo)) { @($chapters[$chapterNo].Dun | Where-Object { $_.id -eq $poi -and $_.kind -eq "castle" }) } else { @() })
            $productionId = if ($rows.Count -gt 0) { $rows[0].id } else { "" }
            $verifiedKey = if ($rows.Count -gt 0) { "castle POI 存在；areapart 与 gate 同章配置" } else { "该章无此城堡" }
            $context = "act$chapterNo/$poi"
            $cap = "城堡=POI(kind=castle)+areapart+解锁门禁"
            $consumer = "AreaParts.AreaPartUnlocked → WorldCampaign 攻城入口"
            $saveImpact = "Cleared 集合与 PendingCastles 持久化"
            $match = if ($rows.Count -gt 0) { "STRONG" } else { "NONE" }
            $direct = if ($rows.Count -gt 0) { "YES" } else { "NO" }
            $migration = "NONE"
            $evidence = (Ev (Join-Path $cfg "chapters/act$chapterNo/dungeon/dungeon.csv") $poi)
            $note = "Boss/威胁/反击设计内容需 C2-A；门禁与拓扑保持现网"
        }
        "EQUIPMENT_TEMPLATE" {
            $slot = ($r.design_id -split '_')[0]
            $slotMap = @{ WEAPON = "weapon"; HELM = "helm"; CHEST = "chest"; GLOVES = "gloves"; BOOTS = "boots"; CHARM = "ring" }
            $engineSlot = $slotMap[$slot]
            $attr = if ($r.design_id -match '_HP_') { "hp" } else { "atk" }
            $base = @($equipBases | Where-Object { $_.slot -eq $engineSlot -and $_.attr -eq $attr })
            $qid = ($r.design_id -split '_')[-1].ToLower()
            $quality = @($equipQual | Where-Object { $_.id -eq $qid })
            $productionId = if ($base.Count -gt 0) { $base[0].id + "+" + ($(if ($quality.Count) { $quality[0].id } else { "" })) } else { "" }
            $verifiedKey = if ($base.Count -gt 0) { "equipment.csv 有 $engineSlot/$attr 基础行" } else { "无同槽同属性基础行" }
            $context = "slot=$engineSlot attr=$attr quality=$qid"
            $cap = "六部位×(atk|hp)×4品质，数值=round((base+per_level*(lv-1))*quality.multiplier,2)"
            $consumer = "Equipment.EquipmentValue / TryDropEquipment / SellInferiorEquipment"
            $saveImpact = "EquipmentSave(BaseId,Level,Quality,Value)；品质改ID需迁移映射表"
            $match = if ($base.Count -gt 0 -and $quality.Count -gt 0) { "STRONG" } else { "NONE" }
            $direct = "NO"
            $migration = "QUALITY_ID_MIGRATION"
            $evidence = (Ev (Join-Path $cfg "item/equipment.csv") $base[0].id) + "; " + (Ev (Join-Path $cfg "item/equipment_quality.csv") $qid)
            $note = "设计=普通/稀有/罕见/传奇；工程=common/uncommon/rare/legendary（epic缺失、uncommon多余），中文名不可隐式重映射"
        }
        "SCROLL" {
            $cap = switch ($r.design_id) { "SC01" { "单目标伤害（统一DMG管线）" } "SC02" { "最多3目标雷伤（需Chain或逐目标结算）" } "SC03" { "目标点固定半径范围伤害" } "SC04" { "有限时减速+少量冰伤" } "SC05" { "短期承伤减免/HP保护" } }
            $existing = if ($r.design_id -eq "SC01") { "scroll_bolt" } else { "" }
            $productionId = $existing
            $verifiedKey = if ($existing) { "scroll.csv 有 scroll_bolt（damage_pct=1.8）" } else { "scroll.csv 仅1行，无该行为" }
            $context = "item/scroll.csv（1 行）"
            $consumer = if ($existing) { "Consumables.UseScroll → SettleDamage" } else { "需扩展 UseScroll（当前仅单目标伤害）" }
            $saveImpact = "ScrollStock 按 id 计数写入快照；新增 id 不需迁移"
            $match = if ($existing) { "STRONG" } else { "NONE" }
            $direct = if ($existing) { "YES" } else { "NO" }
            $migration = if ($existing) { "NONE" } else { "SCROLL_RUNTIME_EXTENSION" }
            $evidence = (Rel (Join-Path $cfg "item/scroll.csv"))
        }
        "POTION" {
            $cap = switch ($r.design_id) { "PO01" { "固定时长队伍ATK%" } "PO02" { "承伤/生存型效果" } "PO03" { "击杀金币增益" } "PO04" { "非战斗移速" } }
            $existing = if ($r.design_id -eq "PO01") { "potion_might" } else { "" }
            $productionId = $existing
            $verifiedKey = if ($existing) { "potion.csv 有 potion_might（attack_pct=0.25, 600tick）" } else { "potion.csv 仅1行，无该效果列" }
            $context = "item/potion.csv（1 行，列固定 attack_pct/duration_ticks/drop_chance）"
            $consumer = if ($existing) { "PotionAttackPct → SkillRuntime 伤害乘区" } else { "需扩展 potion 列与消费点" }
            $saveImpact = "PotionSlotSave(ItemId,Active,Remaining)；新增列不改变槽结构"
            $match = if ($existing) { "STRONG" } else { "NONE" }
            $direct = if ($existing) { "YES" } else { "NO" }
            $migration = if ($existing) { "NONE" } else { "POTION_SCHEMA_EXTENSION" }
            $evidence = (Rel (Join-Path $cfg "item/potion.csv"))
        }
        default {
            $verifiedKey = "未分类"
        }
    }

    $crossRows += [pscustomobject]@{
        kind               = $kind
        design_id          = $r.design_id
        design_name        = $r.design_name
        chapter            = $r.chapter
        class_id           = $r.class_id
        source_key         = $r.source_key
        design_status      = $r.design_status
        design_prefill     = $r.crosswalk_status
        production_id      = $productionId
        engine_verified    = $verifiedKey
        engine_context     = $context
        required_capability = $cap
        consumer_entry     = $consumer
        save_migration     = $saveImpact
        match_strength     = $match
        name_conflict      = $conflict
        direct_mappable    = $direct
        migration_required = $migration
        evidence           = $evidence
        audit_note         = $note
    }
}
WriteCsv "id_crosswalk_engine_complete.csv" $crossRows @("kind", "design_id", "design_name", "chapter", "class_id", "source_key", "design_status", "design_prefill", "production_id", "engine_verified", "engine_context", "required_capability", "consumer_entry", "save_migration", "match_strength", "name_conflict", "direct_mappable", "migration_required", "evidence", "audit_note")

# ---------------- 3. 154 项效果覆盖 ----------------
$eff154 = ReadCsv (Join-Path $pkg "matrices/S15_154项效果消费者联合核查_待核对.csv")

# 能力表：存在的运行时能力（已核实消费者）与缺口
$caps = @{
    damage      = @{ exists = $true; entry = "SkillRuntime.SettleDamage/DamagePipeline"; note = "单体/范围伤害统一管线" }
    projectile  = @{ exists = $true; entry = "SkillRuntime.SpawnProjectile → AbilityRuntime"; note = "投射物/弹道" }
    chain       = @{ exists = $true; entry = "ChainRuntime + chain.csv(1行)"; note = "有限跳跃+衰减，每目标一次" }
    summon      = @{ exists = $true; entry = "SummonRuntime + summon.csv(2行)"; note = "上限/生命周期/主人" }
    aura        = @{ exists = $true; entry = "SkillSupportRuntime.MaintainAura"; note = "范围 Buff 维持" }
    heal        = @{ exists = $true; entry = "SkillSupportRuntime Heal"; note = "按有效上限回血" }
    shield      = @{ exists = $true; entry = "SkillRuntime shield mechanic"; note = "护盾吸收" }
    chill       = @{ exists = $true; entry = "SkillRuntime chill mechanic"; note = "减速" }
    stun        = @{ exists = $true; entry = "SkillRuntime stun mechanic"; note = "硬直/不可行动" }
    forced      = @{ exists = $true; entry = "CombatTargeting forced"; note = "强制嘲讽目标" }
    pulse       = @{ exists = $true; entry = "PulseRuntime AreaPulse"; note = "周期范围脉冲" }
    corpse      = @{ exists = $true; entry = "CorpseRuntime"; note = "尸体爆炸" }
    ability_arc = @{ exists = $true; entry = "AbilityRuntime(arc_homing, 1行)"; note = "弧线追踪" }
    attr_passive = @{ exists = $true; entry = "TalentConfiguration kind=passive + PassiveTalentValue"; note = "仅 atk/armor/crit_rate/engagement_capacity" }
    attr_generic = @{ exists = $true; entry = "SkillRuntime.EffectiveValue + buff attributes + fightattr 36项"; note = "攻速/伤害乘区/抗性等可由 Buff 或天赋扩展" }
    trigger     = @{ exists = $false; entry = "SkillRuntime.React + trigger.csv(0行)"; note = "触发器表为空，React 永不命中" }
    proc        = @{ exists = $false; entry = "ProcAllowed/proc_limit.csv(0)+proc_icd.csv(0)"; note = "无同次施放/同目标限额与ICD" }
    target_score = @{ exists = $false; entry = "SkillSupportRuntime.Choose + ability.Targeting"; note = "仅 lowest/highest/farthest/nearest，无精英/威胁评分" }
    elite_reward = @{ exists = $false; entry = "无精英独立收据"; note = "精英仅词缀，XP/KP 与普通怪同一路径" }
    dot         = @{ exists = $false; entry = "buff.csv mechanic=dot 无消费者"; note = "AbilityDot 无配置" }
    haste       = @{ exists = $false; entry = "buff mechanic=haste 无消费者（仅legacy天赋）"; note = "攻速需走 attack_speed_pct" }
    crit_reduce = @{ exists = $false; entry = "buff mechanic=crit_reduce 无消费者"; note = "仅 legacy CastTalent" }
    mirror      = @{ exists = $false; entry = "AbilityMirror 无配置"; note = "镜像无实现入口" }
}

function TalentVerdict([string]$id, [string]$behavior) {
    # 逐条规则：先判缺口系统，再判可复用能力
    $b = $behavior
    if ($id -match '-T0[1-7]$' -and $b -match '姿态|路线|终点|开启') { return @{ cap = "class_trait/分支姿态"; status = "DESIGN_DECISION"; entry = "TalentConfiguration class_trait + star class_trait_unlock"; gap = "T分支语义与T节点数量需产品定稿；工程每职业仅1个T节点且仅战士可解锁" } }
    if ($b -match '学会') { return @{ cap = "skill_slot_bind"; status = "MODIFY"; entry = "skill_slot.csv + talent kind=active + skill_id"; gap = "需先按C1-A确定该槽技能身份；空槽职业无法落地" } }
    if ($b -match '召唤|先祖|女武神') { return @{ cap = "summon"; status = "MODIFY"; entry = "SummonRuntime + summon.csv"; gap = "需新增召唤定义（上限/继承比例）；『优先保护/优先牵制』需目标评分(缺口)" } }
    if ($b -match '下一次|限一次|之后|结束后|命中后|击杀后|触发|条件刷新|每阶|独立一次') { return @{ cap = "trigger/proc"; status = "NEW_EFFECT"; entry = "trigger.csv(0)/proc_limit(0)"; gap = "工程无触发器与同次限额，『下一次/限一次』类条件无法在线执行" } }
    if ($b -match '优先|目标评分|重新选择|转火|最危险|精英.*优先|覆盖上限') { return @{ cap = "target_score"; status = "NEW_EFFECT"; entry = "Choose 仅 support 4 策略"; gap = "需新增精英/威胁/覆盖上限评分，且不得全图扫描" } }
    if ($b -match '光环|范围内友军|范围友军') { return @{ cap = "aura"; status = "MODIFY"; entry = "MaintainAura"; gap = "T03/T04 需与T01 同源改造，不能新增第二个常驻光环" } }
    if ($b -match '治疗|回复|恢复|祈祷') { return @{ cap = "heal"; status = "MODIFY"; entry = "Heal + ally_lowest_hp"; gap = "『护阵反馈』需 Buff；周期恢复需光环周期效果" } }
    if ($b -match '护盾|减伤|承压|保护') { return @{ cap = "shield"; status = "MODIFY"; entry = "buff mechanic=shield"; gap = "需绑定到技能/天赋触发点" } }
    if ($b -match '嘲讽|硬直|眩晕|减速|牵制|控制|制止') { return @{ cap = "stun/chill/forced"; status = "MODIFY"; entry = "buff mechanic stun/chill/forced"; gap = "Boss 免疫与叠加上限需逐技能声明" } }
    if ($b -match '攻速|出手节奏|攻击节奏') { return @{ cap = "attr_generic(attack_speed_pct)"; status = "MODIFY"; entry = "EffectiveValue attack_speed_pct"; gap = "被动白名单不含攻速，需扩展 PassiveTalentValue 或改用 Buff" } }
    if ($b -match '暴击') { return @{ cap = "attr_passive(crit_rate)"; status = "REAL_REUSE"; entry = "PassiveTalentValue crit_rate"; gap = "数值[P]" } }
    if ($b -match '元素|火|冰|雷|毒|物理|伤害提高|强化|加成|系数|输出') { return @{ cap = "attr_generic/damage"; status = "MODIFY"; entry = "EffectiveValue 伤害乘区 + skill coefficient"; gap = "被动白名单仅4项；扩乘区需改白名单（配置+小改代码）" } }
    return @{ cap = "未知"; status = "DESIGN_DECISION"; entry = "无"; gap = "行为描述无法自动归并，需设计Owner确认所需机制" }
}

# 98 天赋逐条核定（设计Owner输入→工程能力→状态）；规则化会误判的行在此显式固定。
$talentVerdictAll = @{}
@(
 "BAR-B01|NEW_EFFECT|下一次近战加成的条件态|无触发/下一击状态机|trigger.csv 0 行，React 永不命中",
 "BAR-B02|NEW_EFFECT|命中附加硬直+免疫+内置间隔|stun 已有，内置间隔无|proc_icd.csv 0 行，无 ICD 执行",
 "BAR-B03|NEW_EFFECT|有限下一击优势|无触发/条件态|同 B01",
 "BAR-S201|MODIFY|技能入队|改槽位/新技能行|S2 现为 barb_taunt，需按 C1-A 重绑",
 "BAR-S202|REAL_REUSE|绑定技能的系数加成|active talent skill_id+value|SkillRuntime.Commit 取 TalentValue 作为系数",
 "BAR-S203|NEW_EFFECT|施放中沿路径持续命中|无通道/移动结算|AreaPulse 仅原地周期",
 "BAR-S204|NEW_EFFECT|每目标命中上限|无 proc 限额|proc_limit.csv 0 行",
 "BAR-S205|NEW_EFFECT|集中精英的目标评分|无精英评分|Choose 仅 4 策略",
 "BAR-S206|NEW_EFFECT|旋风后下一击强化|无条件态|同 B01",
 "BAR-S301|REAL_REUSE|战吼入队|barb_taunt 已挂 S2|槽位与设计 S3 不同，重绑即可",
 "BAR-S302|REAL_REUSE|Buff 持续时间|buff.duration_ticks|按来源取效，不重复注册由 ApplyBuff 处理",
 "BAR-S303|NEW_EFFECT|按敌群接近时机施放|AI 条件仅 nearby_melee/missing_buff|需新增施放条件",
 "BAR-S304|MODIFY|有限攻势强化|buff attributes（伤害乘区）|需新增 buff 行与属性绑定",
 "BAR-S305|REAL_REUSE|短时嘲讽|forced mechanic|CombatTargeting forced 已消费",
 "BAR-S401|MODIFY|召唤先祖|Summon+summon.csv|需新增召唤定义；上限/主人已支持",
 "BAR-S402|MODIFY|召唤继承比例|summon.hp_ratio/attack_ratio|参数齐备",
 "BAR-S403|NEW_EFFECT|召唤偏向精英|无目标评分|同 S205",
 "BAR-S404|NEW_EFFECT|召唤优先受威胁友军|无评分/护卫逻辑|需新增接敌评分",
 "RAN-B01|MODIFY|基础射击强化|basic_attack_damage_pct 属性|被动白名单不含该属性，需扩展或走 Buff",
 "RAN-B02|NEW_EFFECT|击杀后重新评估目标|无击杀触发|trigger.csv 0 行",
 "RAN-B03|NEW_EFFECT|精英锁矢稳定|无精英标记评分|需新增",
 "RAN-S201|REAL_REUSE|多重箭入队|ran_multishot 已存在（S1）|需重绑到 S2",
 "RAN-S202|MODIFY|多弹道有效清理|effect.projectile_count/spread|参数在，需新技能行",
 "RAN-S203|NEW_EFFECT|弹道分配去重|无每目标限额|proc_limit 0 行",
 "RAN-S204|MODIFY|增加覆盖|projectile_count/spread_degrees|参数齐备",
 "RAN-S205|MODIFY|缩窄角度集中|spread_degrees|参数齐备",
 "RAN-S206|NEW_EFFECT|第二名贯穿|effect 无 pierce 列|ability_effect 有 pierce 但不适用普通弹道",
 "RAN-S207|NEW_EFFECT|齐射后再定位|无条件链|同 B01",
 "RAN-S301|REAL_REUSE|导引箭入队|ran_guided_arrow + arc_homing|能力齐备",
 "RAN-S302|MODIFY|追踪成功改善|ability_effect arc/speed|参数可调",
 "RAN-S303|NEW_EFFECT|猎首评分|无评分|同 S205",
 "RAN-S304|NEW_EFFECT|对精英更强|无精英伤害乘区|仅 boss_damage_pct",
 "RAN-S305|MODIFY|命中后减速|ApplyBuff chill|需把 chill 绑定到命中效果",
 "RAN-S401|MODIFY|女武神召唤|summon ran_valkyrie|现占 S3，需重绑 S4",
 "RAN-S402|MODIFY|召唤 HP/贡献|summon.hp_ratio|参数齐备",
 "RAN-S403|NEW_EFFECT|召唤护阵|无护卫评分|需新增",
 "RAN-S404|NEW_EFFECT|召唤猎首|无评分|需新增",
 "SOR-B01|REAL_REUSE|火球范围伤害提高|active talent 系数|skill 已存在",
 "SOR-B02|REAL_REUSE|爆裂范围扩大|effect.extent|参数齐备",
 "SOR-B03|REAL_REUSE|爆裂范围缩小|effect.extent|参数齐备",
 "SOR-B04|MODIFY|可视残效（不赋DoT）|无地面残效表现|表现为新增，不新增DoT",
 "SOR-S201|MODIFY|冰霜新星|AreaPulse+chill|需新技能/效果行",
 "SOR-S202|MODIFY|新星强化|extent/buff 值|参数齐备",
 "SOR-S203|NEW_EFFECT|近身受威胁时施放|AI 条件不足|需新增条件",
 "SOR-S204|MODIFY|扩大影响范围|effect.extent|参数齐备",
 "SOR-S205|MODIFY|缩小范围延长减速|extent/buff.duration|参数齐备",
 "SOR-S301|REAL_REUSE|连锁闪电入队|sor_chain_lightning|需重绑 S2→S3",
 "SOR-S302|REAL_REUSE|跳跃效力/目标数|chain.max_targets/jump_range/decay|参数齐备",
 "SOR-S303|NEW_EFFECT|优先经过减速目标|无目标评分|需新增",
 "SOR-S304|REAL_REUSE|减少跳跃提高单体|chain 参数|参数齐备",
 "SOR-S401|MODIFY|陨星术|范围伤+前摇|需新技能行；无地面预警系统",
 "SOR-S402|MODIFY|中心火伤提高|系数/area|参数齐备",
 "SOR-S403|MODIFY|扩大群体覆盖|extent|参数齐备",
 "SOR-S404|MODIFY|精英中心爆发|extent+系数|参数齐备（无精英乘区）",
 "SOR-S405|NEW_EFFECT|火球定位下一次陨星|无触发/评分|同 B01",
 "PAL-B01|REAL_REUSE|圣击强化|active talent 系数|skill 已存在",
 "PAL-B02|NEW_EFFECT|维持防守位置|无站位意图系统|AI 无位置保持",
 "PAL-B03|NEW_EFFECT|对近身精英压制|无精英乘区|需新增",
 "PAL-S201|MODIFY|盾击|Damage+stun|需新技能行",
 "PAL-S202|MODIFY|盾击伤害/稳健|系数|参数齐备",
 "PAL-S203|NEW_EFFECT|优先威胁友军|无评分|需新增",
 "PAL-S204|MODIFY|短硬直/节奏中断|stun|stun 已消费；Boss 免疫需声明",
 "PAL-S205|NEW_EFFECT|偏精英伤害|无精英乘区|需新增",
 "PAL-S301|REAL_REUSE|祈祷入队|pal_prayer(Heal)|现占 S2，需重绑 S3",
 "PAL-S302|REAL_REUSE|治疗对象与幅度|Heal+ally_lowest_hp|参数齐备",
 "PAL-S303|NEW_EFFECT|优先多名受损友军|无多目标评分|需新增",
 "PAL-S304|NEW_EFFECT|优先最危险友军|无评分|需新增",
 "PAL-S305|MODIFY|祈祷后有限护阵|shield buff|需绑定一次性 Buff（无触发则用时长限制）",
 "PAL-S401|MODIFY|祝福之锤|环绕范围伤|需新技能行",
 "PAL-S402|MODIFY|飞锤覆盖|extent|参数齐备",
 "PAL-S403|MODIFY|靠近自身保护|extent/radius|参数齐备",
 "PAL-S404|MODIFY|扩展群伤|extent|参数齐备",
 "PAL-S405|NEW_EFFECT|盾击后下一次锤|无条件链|同 B01"
) | ForEach-Object {
    $parts = $_ -split '\|'
    $talentVerdictAll[$parts[0]] = @{ status = $parts[1]; cap = $parts[2]; entry = $parts[3]; gap = $parts[4] }
}
# T（职业特性/姿态分支）24 条统一按设计裁决处理
foreach ($id in @("BAR-T01","BAR-T02","BAR-T03","BAR-T04","BAR-T05","BAR-T06","BAR-T07","RAN-T01","RAN-T02","RAN-T03","RAN-T04","RAN-T05","RAN-T06","SOR-T01","SOR-T02","SOR-T03","SOR-T04","SOR-T05","SOR-T06","PAL-T01","PAL-T02","PAL-T03","PAL-T04","PAL-T05","PAL-T06")) {
    $talentVerdictAll[$id] = @{ status = "DESIGN_DECISION"; cap = "职业特性/姿态分支"; entry = "TalentConfiguration class_trait + star class_trait_unlock"; gap = "每职业仅1个T节点且仅战士可解锁；T01–T07 的分支/互斥语义需设计定稿后才可拆分" }
}
$effRows = @()
foreach ($e in $eff154) {
    $verdict = $null; $required = ""; $entry = ""; $gap = ""; $test = ""
    switch ($e.kind) {
        "HERO_SKILL" {
            $v = $skillVerdict[$e.design_id]
            $required = $v.cap
            $entry = "SkillRuntime.DrainEffects / ApplyBuff / SummonRuntime / ChainRuntime"
            $gap = $v.gap
            $test = "固定seed战斗回放：施放→命中/增益→存档重开后状态一致；伤害仅经 DamagePipeline"
        }
        "HERO_TALENT" {
            $t = if ($talentVerdictAll.ContainsKey($e.design_id)) { $talentVerdictAll[$e.design_id] } else { TalentVerdict $e.design_id $e.intended_behavior }
            $required = $t.cap; $entry = $t.entry; $gap = $t.gap; $status = $t.status
            $test = "投点→战斗行为可观察→读档一致；条件类需触发器回放与同次施放限额"
        }
        "STAR" {
            $row = $crossRows | Where-Object { $_.kind -eq "STAR" -and $_.design_id -eq $e.design_id }
            $required = $row.required_capability; $entry = $row.consumer_entry; $gap = $row.audit_note
            $test = "真实购买→属性/收益变化→重读档不回滚、不重复叠加"
        }
        "SCROLL" {
            $row = $crossRows | Where-Object { $_.kind -eq "SCROLL" -and $_.design_id -eq $e.design_id }
            $required = $row.required_capability; $entry = $row.consumer_entry; $gap = $row.audit_note
            $test = "库存扣1→统一伤害/控制结算→击杀掉落不覆盖；读档库存一致"
        }
        "POTION" {
            $row = $crossRows | Where-Object { $_.kind -eq "POTION" -and $_.design_id -eq $e.design_id }
            $required = $row.required_capability; $entry = $row.consumer_entry; $gap = $row.audit_note
            $test = "4槽占位→生效→计时→读档剩余时间；不写回永久属性"
        }
    }
    $status = if ($e.kind -in @("HERO_SKILL", "HERO_TALENT", "STAR", "SCROLL", "POTION") -and $e.kind -ne "HERO_TALENT") {
        if ($e.kind -eq "HERO_SKILL") { $skillVerdict[$e.design_id].status }
        elseif ($e.kind -eq "STAR") { $crossRows | Where-Object { $_.kind -eq "STAR" -and $_.design_id -eq $e.design_id } | ForEach-Object { if ($_.match_strength -eq "STRONG") { "REAL_REUSE" } elseif ($_.match_strength -eq "WEAK") { "MODIFY" } else { "NEW_EFFECT" } } }
        elseif ($e.kind -eq "SCROLL") { if ($e.design_id -eq "SC01") { "REAL_REUSE" } else { "MODIFY" } }
        elseif ($e.kind -eq "POTION") { if ($e.design_id -eq "PO01") { "REAL_REUSE" } elseif ($e.design_id -eq "PO02") { "MODIFY" } else { "MODIFY" } }
    } else { $status }
    # 设计侧明确未定/未来项覆盖
    if ($e.design_id -eq "S02") { $status = "INPUT_MISSING"; $gap = "卷轴容量每阶数值未定（设计标注待核定）" }
    if ($e.design_id -eq "S03") { $status = "DESIGN_DECISION"; $gap = "自动使用卷轴属未来策略系统，首轮不投放" }
    if ($e.design_id -eq "G03") { $status = "DESIGN_DECISION"; $gap = "额外天赋点影响S07 24点预算，须C3跑数后定" }
    if ($e.design_id -eq "P03") { $status = "MODIFY"; $gap = "4→6→8：需改常量、UI四槽、存档槽数校验并迁移旧档；当前UI已预留8槽布局" }
    if ($status -eq $null -or $status -eq "") { $status = "DESIGN_DECISION" }

    $effRows += [pscustomobject]@{
        kind                = $e.kind
        design_id           = $e.design_id
        design_name         = $e.design_name
        intended_behavior   = $e.intended_behavior
        source_key          = $e.source_key
        required_capability = $required
        consumer_entry      = $entry
        gap_vs_design       = $gap
        test_required       = $test
        status              = $status
        implementation_gate = "READ_ONLY_ID_SCHEMA_EFFECT_FIRST"
        design_prefill      = $e.coverage_status
        evidence            = "见 id_crosswalk_engine_complete.csv 同 design_id 行 + config/skill|item|star 对应行"
    }
}
WriteCsv "effect_coverage_engine_complete.csv" $effRows @("kind", "design_id", "design_name", "intended_behavior", "source_key", "required_capability", "consumer_entry", "gap_vs_design", "test_required", "status", "implementation_gate", "design_prefill", "evidence")

# ---------------- 4. 16 技能槽位差异 ----------------
$slotOut = @()
foreach ($s in $slotTable) {
    $v = $skillVerdict[$s.design_id]
    $cls = $classOfDesign[$s.design_class_id]
    $slotNo = $s.design_slot
    $occupant = $skillSlotMap["$cls`:fixed_$slotNo"]
    $slotOut += [pscustomobject]@{
        design_id            = $s.design_id
        design_class         = $s.design_class_id
        engine_class         = $cls
        engine_slot          = "fixed_$slotNo"
        design_name          = $s.design_skill_name
        occupancy_verified   = if ($occupant) { $occupant } else { "EMPTY" }
        candidate_review_only = $s.candidate_existing_id_REVIEW_ONLY
        behavior_capability  = $v.cap
        verdict              = $v.status
        rebind_allowed       = if ($v.status -eq "REAL_REUSE") { "REBIND_ONLY" } else { "REBIND_PLUS_NEW_ROW" }
        talent_impact        = "天赋节点按 skill_id 绑定（TalentConfiguration 校验同CD同射程）；改槽位不影响已学节点，改 skill_id 需同步节点"
        save_impact          = "SkillIds 存 id 不存槽位；重绑不改存档；新增技能需补默认装配"
        engine_effect_test   = "未执行（只读）"
        proposed_stage       = "S15-C1_AFTER_AUTHORIZATION"
    }
}
WriteCsv "skill_slot_engine_diff.csv" $slotOut @("design_id", "design_class", "engine_class", "engine_slot", "design_name", "occupancy_verified", "candidate_review_only", "behavior_capability", "verdict", "rebind_allowed", "talent_impact", "save_impact", "engine_effect_test", "proposed_stage")

# ---------------- 5. 31 星图真实消费者与活跃可购买次数 ----------------
$retiredAttrs = @("skill_slots", "party_slots", "talent_reset", "talent_reset_discount")
$starOut = @()
$activePurchases = 0
foreach ($st in $stars) {
    $retired = $st.attr_id -in $retiredAttrs
    $maxLevel = [int]$st.max_level
    if (-not $retired -and $st.attr_id -ne "") { $activePurchases += $maxLevel }
    $consumer = switch ($st.attr_id) {
        "attack" { "Stars.ApplyStarTotals → Actor.Attack" }
        "hp" { "Stars.ApplyStarTotals → Actor.MaxHp" }
        "xp_gain_pct" { "XpGainMultiplier → GainExperience" }
        "class_trait_unlock" { "ClassTraitUnlocked + CanLearnFixedTalent" }
        "skill_slots" { "无实际消费（固定技能模式下 ActiveSkillSlots 恒4）" }
        "party_slots" { "无任何消费（FormationCapacity 恒4）" }
        "talent_reset" { "无（固定技能模式下恒可/恒否，StarValue 不参与）" }
        "talent_reset_discount" { "无（同上）" }
        default { "UI 文案 only" }
    }
    $starOut += [pscustomobject]@{
        node_id                = $st.id
        name                   = $st.name
        attr_id                = $st.attr_id
        max_level              = $maxLevel
        value_per_level        = $st.value_per_level
        cost                   = $st.cost
        retired                = if ($retired) { "YES" } else { "NO" }
        buyable_in_production  = if ($retired -or $st.attr_id -eq "") { "NO" } else { "YES" }
        real_consumer          = $consumer
        counts_toward_purchase_budget = if ($retired -or $st.attr_id -eq "") { "NO" } else { "YES" }
        layout_prereq          = (@($starLayout | Where-Object { $_.id -eq $st.id }) | ForEach-Object { $_.prereq })
        evidence               = (Ev (Join-Path $cfg "star/star.csv") $st.id)
    }
}
WriteCsv "star_consumer_engine.csv" $starOut @("node_id", "name", "attr_id", "max_level", "value_per_level", "cost", "retired", "buyable_in_production", "real_consumer", "counts_toward_purchase_budget", "layout_prereq", "evidence")

# 楼层数一致性：设计层数 vs poi_profile.floors
$floorMismatch = @()
foreach ($g in ($map675 | Where-Object { $_.kind -eq "DUNGEON_NORMAL_FLOOR" } | Group-Object { ($_.design_id -split ":")[0] + ":" + ($_.design_id -split ":")[1] })) {
    $chNo = [int](($g.Group[0].design_id -split ":")[0]); $poi = ($g.Group[0].design_id -split ":")[1]
    $prof = @($chapters[$chNo].Prof | Where-Object { $_.id -eq $poi })
    if ($prof.Count -gt 0 -and [int]$prof[0].floors -ne $g.Count) {
        $floorMismatch += [pscustomobject]@{ chapter = $chNo; poi = $poi; design_floors = $g.Count; engine_floors = $prof[0].floors }
    }
}
WriteCsv "floor_count_mismatch.csv" $floorMismatch @("chapter", "poi", "design_floors", "engine_floors")
# ---------------- 5b. 258 条设计字段 schema 对账 ----------------
$fields = ReadCsv (Join-Path $pkg "matrices/S15_设计字段与工程列名初步对账.csv")
# 生产列事实字典（key=工程表|列名）；type/unit/range/default/rounding 来自加载器校验与表头
$colFacts = @{
    "skill/skill.csv|id" = "string|键|唯一|必填|—|SkillConfiguration.cs:75"
    "skill/skill.csv|cooldown_ticks" = "int|tick(20/s)|>=0|0|整数|SkillRuntime.RunActiveSkill CD"
    "skill/skill.csv|coefficient" = "double|倍率|>=0|必填|不取整|SettleDamage basis*coefficient"
    "skill/skill.csv|range" = "double|世界单位|>0|必填|—|SkillSupportRuntime 目标筛选"
    "skill/skill.csv|effect_ids" = "list<effect.id>|—|图可达|必填|—|DrainEffects 队列"
    "skill/skill.csv|target_policy" = "enum|—|enemy/self/ally_lowest_hp/corpse|enemy|—|SkillSupportRuntime.Choose"
    "skill/skill.csv|cast_condition" = "enum|—|空/nearby_melee/missing_buff|空|—|RunActiveSkill 前置"
    "skill/effect.csv|kind" = "enum15|—|Damage/ApplyBuff/Heal/MaintainAura/AbilityCast/Summon/Chain/CorpseExplosion/AreaPulse/SpawnProjectile(+5空转)|必填|—|DrainEffects switch"
    "skill/effect.csv|share" = "double|比例|0..1|1|—|伤害分量权重"
    "skill/effect.csv|extent" = "double|世界单位|>=0|0|—|范围/半径"
    "skill/effect.csv|speed" = "double|单位/秒|>0|必填(弹道)|—|AbilityRuntime 飞行"
    "skill/effect.csv|spawn_tick" = "int|tick|>=0|0|整数|延迟生成"
    "skill/effect.csv|lifetime_ticks" = "int|tick|>=1|必填|整数|弹道/实体寿命"
    "skill/effect.csv|buff_id" = "ref<buff.id>|—|存在|空|—|ApplyBuff"
    "skill/effect.csv|projectile_count" = "int|个|>=1|1|整数|多弹道"
    "skill/effect.csv|spread_degrees" = "double|度|>=0|0|—|扇面"
    "skill/buff.csv|duration_ticks" = "int|tick(20/s)|>=1|必填|整数|Buff 倒计时"
    "skill/buff.csv|stacking_policy" = "enum|—|见SkillConfiguration|—|—|ApplyBuff 合并"
    "skill/buff.csv|attributes" = "list<attr:value>|词表单位|见fightattr|空|—|EffectiveValue/EffectiveAttributes"
    "skill/buff.csv|mechanic" = "enum|—|空/haste/shield/dot/chill/stun/crit_reduce/mirror/forced|空|—|SkillRuntime 分支"
    "skill/buff.csv|value" = "double|机制相关|—|0|—|mechanic 参数"
    "skill/talent.csv|max_rank" = "int|级|>=1|必填|整数|CanLearnFixedTalent"
    "skill/talent.csv|kind" = "enum|—|passive/active|必填|—|被动/主动分派"
    "skill/talent.csv|value" = "double|属性或倍率|见effect|0|—|TalentValue"
    "skill/talent.csv|per_rank" = "double|每级增量|—|0|—|TalentValue"
    "skill/talent.csv|skill_id" = "ref<skill.id>|—|同CD同射程|空|—|active 节点绑定"
    "skill/talent.csv|class_trait" = "bool|—|0/1|0|—|FixedSkills 门槛"
    "star/star.csv|max_level" = "int|阶|>=1|必填|整数|StarProgression"
    "star/star.csv|cost" = "int|金币/阶|>=0|必填|整数|BuyStar 扣费"
    "star/star.csv|attr_id" = "enum8|—|attack/hp/party_slots/skill_slots/talent_reset/talent_reset_discount/xp_gain_pct/class_trait_unlock|空=占位|—|StarTotals/StarValue"
    "star/star.csv|value_per_level" = "double|属性单位|—|必填|—|StarTotals"
    "item/equipment.csv|slot" = "enum6|—|weapon/helm/chest/gloves/boots/ring|必填|—|GearSlots"
    "item/equipment.csv|base_value" = "double|属性点|>=0|必填|round2|EquipmentValue"
    "item/equipment.csv|per_level" = "double|属性点/级|>=0|必填|round2|EquipmentValue"
    "item/equipment_quality.csv|multiplier" = "double|倍率|>0|必填|round2|EquipmentValue"
    "item/equipment_quality.csv|weight" = "int|权重|>=0 合计100|必填|整数|掉落掷骰"
    "item/scroll.csv|damage_pct" = "double|攻击百分比|>0|必填|—|UseScroll→SettleDamage"
    "item/scroll.csv|drop_chance" = "double|概率|0..1|必填|—|击杀掉落"
    "item/potion.csv|duration_ticks" = "int|tick(20/s)|>=1|必填|整数|AdvanceConsumables"
    "item/potion.csv|attack_pct" = "double|队伍ATK%|>=0|必填|—|PotionAttackPct"
    "monster/monsterlevel.csv|hp_multiplier" = "double|倍率|>0|必填|—|Spawn.Create"
    "monster/monsterlevel.csv|attack_multiplier" = "double|倍率|>0|必填|—|Spawn.Create"
    "dungeon/dungeon.csv|kind" = "enum|—|normal/castle|必填|—|BeginDungeon/WorldProgress"
    "dungeon/dungeon.csv|level_min" = "int|怪级|1..MaxMonsterLevel|必填|整数|难度带/刷怪"
    "dungeon/poi_profile.csv|floors" = "int|层|1..20|必填|整数|Dungeon.Generate"
    "dungeon/floor_plan.csv|CombatCellCount" = "int|格|>=0|必填|整数|仅报告/名称，不驱动刷怪"
    "world/areapart.csv|unlock_mode" = "enum|—|any/all|any|—|AreaPartUnlocked"
}
$genericFacts = @{
    "id" = "string|键|唯一|必填|—|加载器主键"; "nodeid" = "string|键|唯一|必填|—|StarConfiguration/编辑器"
    "name" = "string|显示名|非空|必填|—|UI/日志"; "status" = "enum|—|prototype/blocked|必填|—|加载校验"
    "kind" = "enum|—|各表自定|必填|—|分派"; "chapter" = "int|章|1..5|必填|整数|ChapterConfiguration"
    "chapterid" = "string|章键|ACT01..ACT05|必填|—|ChapterConfiguration"; "regionid" = "int|region.id|存在|必填|整数|WorldConfig.Rulers"
    "regionkey" = "int|region.id|存在|必填|整数|AreaParts.AreaPart"; "poiid" = "string|POI字面量|存在|必填|—|PoiConfiguration"
    "dungeonkey" = "string|POI字面量|存在|必填|—|PoiConfiguration"; "compositeid" = "string|章:POI|存在|必填|—|C2-A 只读对账"
    "classid" = "enum|—|warrior/ranger/mage/priest/necromancer|必填|—|TalentConfiguration.hero"
    "职业" = "enum|—|同上（中文）|必填|—|仅设计侧列"; "skillid" = "string|skill.id|存在|必填|—|skill_slot/talent.skill_id"
    "技能槽位" = "enum|—|S1..S4|必填|—|skill_slot.slot"; "maxrank" = "int|级|>=1|必填|整数|talent.max_rank"
    "firstchapter" = "int|章|>=1|必填|整数|设计侧章节窗口"; "templateid" = "string|模板键|唯一|必填|—|装备内容模板（生产用 equipment.id+quality）"
    "slotid" = "enum|—|weapon/helm/chest/gloves/boots/ring|必填|—|GearSlots"; "qualityid" = "enum|—|common/uncommon/rare/legendary|必填|—|equipment_quality.id"
    "monsterlevel" = "int|怪级|1..10000|必填|整数|Spawn.Create"; "monsteractuallevel" = "int|怪级|1..10000|必填|整数|房间 SnapshotLevel"
    "itemlevel" = "int|装备等级|1..MaxMonsterLevel|必填|整数|EquipmentValue"
    "prerequisite" = "list<id>|—|同系更早行/存在|空|—|TalentConfiguration 校验"
    "prerequisiteor" = "list<id>|—|存在/无环|空|—|StarConfiguration.PrerequisitesMet"
    "exclusivegroup" = "string|组|—|空|—|CanLearnFixedTalent 互斥"
    "本档评级要求ar" = "double|AR|>=1|—|—|KpProgression 门槛（当前由等级推导，不读表）"
    "本档评级要求dr" = "double|DR|>=1|—|—|KpProgression 门槛（同上）"
    "该档怪击杀公共xp" = "long|XP/击杀|>=0|—|整数|spawn_rule.xp_base（不是按等级表）"
    "购买该档kp费用" = "int|KP|>=0|—|整数|KpProgression 定价（当前按等级固定）"
}
$schemaRows = @()
foreach ($f in $fields) {
    $designField = $f.design_field
    $suffix = ""
    foreach ($s in @("_REFERENCE", "_Historical", "_P0", "_H", "_P", "_C")) { if ($designField -like "*$s") { $suffix = $s; break } }
    $engineCol = ""; $engineTable = ""; $status = "MULTI_TABLE_OR_P0_ONLY"; $facts = "UNKNOWN"
    $suggested = $f.suggested_engine_table_REVIEW_ONLY
    $sugPath = Join-Path $cfg ($suggested -replace '/', '\')
    if (Test-Path $sugPath) {
        $head = (Get-Content -LiteralPath $sugPath -TotalCount 1 -Encoding UTF8) -split ','
        $norm = ($designField -replace '_P$|_H$|_C$|_P0$|_REFERENCE$|_Historical$', '')
        $hit = @($head | Where-Object { $_ -eq $designField })
        if ($hit.Count -eq 0) { $hit = @($head | Where-Object { $_ -eq $norm }) }
        if ($hit.Count -gt 0) {
            $engineCol = $hit[0]; $engineTable = $suggested
            $facts = $colFacts["$suggested|$engineCol"]; if (-not $facts) { $g = $genericFacts[($engineCol -replace "_", "").ToLower()]; if (-not $g) { $g = $genericFacts[$engineCol.ToLower()] }; $facts = if ($g) { $g } else { "type=UNKNOWN|unit=UNKNOWN|range=UNKNOWN|default=UNKNOWN|rounding=UNKNOWN|consumer=见加载器" } }
            $status = "EXACT_COLUMN_VERIFIED"
            if ($designField -ne $norm) { $status = "SUFFIXED_DESIGN_FIELD_MAPS_TO_ENGINE_COLUMN" }
        } elseif ($suffix -eq "") {
            $status = "NO_EXACT_LABEL_SEMANTIC_UNVERIFIED"
        }
    }
    if ($suffix -in @("_H", "_REFERENCE", "_Historical")) { $status = "DESIGN_ONLY_" + $suffix.TrimStart('_') + "_NO_PRODUCTION_COLUMN" }
    if ($suffix -eq "_P" -or $suffix -eq "_C") { $status = "DESIGN_INTENT_" + $suffix.TrimStart('_') + "_NEEDS_C3_OR_REVIEW" }
    $schemaRows += [pscustomobject]@{
        source_key = $f.source_key; design_table = $f.design_table; design_field = $designField
        suffix_class = if ($suffix) { $suffix.TrimStart('_') } else { "" }
        suggested_engine_table = $suggested; engine_table_verified = $engineTable; engine_column = $engineCol
        facts = $facts; match_status = $status
        runtime_consumer = if ($engineCol) { "见 facts consumer 字段" } else { "UNKNOWN" }
        design_prefill = $f.match_status
    }
}
WriteCsv "schema_diff_complete.csv" $schemaRows @("source_key", "design_table", "design_field", "suffix_class", "suggested_engine_table", "engine_table_verified", "engine_column", "facts", "match_status", "runtime_consumer", "design_prefill")
# ---------------- 6. 汇总 ----------------
$crossByMatch = $crossRows | Group-Object kind, match_strength | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }
$effByStatus = $effRows | Group-Object status | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }
$effByKind = $effRows | Group-Object kind, status | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }
$missingKeys = $crossRows | Where-Object { $_.kind -in @("DUNGEON", "DUNGEON_NORMAL_FLOOR", "CASTLE") -and $_.match_strength -ne "STRONG" }
$redMap = $crossRows | Where-Object { $_.kind -eq "DUNGEON_NORMAL_FLOOR" -and $_.direct_mappable -eq "NO" }

$summary = [ordered]@{
    generated_at     = (Get-Date).ToString("s")
    workspace        = $Workspace
    package          = $Package
    output           = (Rel $out)
    read_only        = $true
    counts           = [ordered]@{
        crosswalk_rows = $crossRows.Count
        effect_rows    = $effRows.Count
        skill_slots    = $slotOut.Count
        stars          = $starOut.Count
        sources        = $sourceRows.Count
    }
    cross_by_kind_match = $crossByMatch
    effect_by_status    = $effByStatus
    effect_by_kind      = $effByKind
    p0_key_missing      = $missingKeys.Count
    floor_plan_not_consumed = $redMap.Count
    star_active_purchase_ranks = $activePurchases
    star_active_nodes = @($starOut | Where-Object { $_.retired -eq "NO" -and $_.attr_id -ne "" }).Count
    floor_count_mismatch = $floorMismatch.Count
    star_nodes_total    = $stars.Count
    star_nodes_retired  = @($starOut | Where-Object { $_.retired -eq "YES" }).Count
    capability          = $capability
    source_sha_all_match = (@($sourceRows | Where-Object { $_.sha_match -ne "YES" }).Count -eq 0)
}
WriteText "audit_summary.json" ($summary | ConvertTo-Json -Depth 6)

# 输入未被改写的证据：对包内 21 源 + 4 矩阵重新取哈希
$after = @()
foreach ($r in $sourceRows) { $p = Join-Path $Workspace $r.path; $after += [pscustomobject]@{ path = $r.path; sha_before = $r.actual_sha; sha_after = if (Test-Path $p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower() } else { "MISSING" } } }
WriteCsv "source_hashes_before_after.csv" $after @("path", "sha_before", "sha_after")

Write-Host "OUTPUT=$out"
Write-Host ("CROSSWALK={0} EFFECT={1} SLOTS={2} STARS={3}" -f $crossRows.Count, $effRows.Count, $slotOut.Count, $starOut.Count)
Write-Host ("EFFECT_STATUS: " + ($effByStatus -join " "))
Write-Host ("CROSSWALK: " + ($crossByMatch -join " "))
Write-Host ("STAR active purchase ranks={0} retired_nodes={1}/{2}" -f $activePurchases, $summary.star_nodes_retired, $stars.Count)
Write-Host ("P0 key missing={0}; floor_plan_not_consumed={1}" -f $missingKeys.Count, $redMap.Count)
Write-Host ("SHA all match={0}" -f $summary.source_sha_all_match)
