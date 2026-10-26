# S15-C1-A-2：98天赋落地（可重复执行；已存在的行不重复追加）
# 设计输入：docs/temp/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/C1A核心输入/02_C1A_98天赋逐节点补规表.csv
# 每个节点都必须落到真实消费者：modifier/trait 由 Commit/Effect/Buff/Summon/Chain 消费；trigger 由 React+proc_limit+proc_icd 消费。
param([string]$Root="d:/Project-Asmo/Idle-CRPG/Idle-CRPG")
$ErrorActionPreference="Stop"
$skill=Join-Path $Root "config/skill"
$utf8=New-Object System.Text.UTF8Encoding($false)
function Append-Rows([string]$file,[string[]]$headers,[string[][]]$rows){
    $path=Join-Path $skill $file
    $existing=@{}
    if(Test-Path $path){ foreach($r in (Import-Csv -LiteralPath $path -Encoding UTF8)){ $existing[$r.($headers[0])]=$true } }
    $lines=New-Object System.Collections.Generic.List[string]
    foreach($row in $rows){
        if($existing.ContainsKey($row[0])){continue}
        $lines.Add(($row | ForEach-Object { if($_ -match '[,"]'){ '"'+($_ -replace '"','""')+'"' } else { $_ } }) -join ',')
        $existing[$row[0]]=$true
    }
    if($lines.Count -eq 0){return 0}
    $text=""
    if(Test-Path $path){ $text=[IO.File]::ReadAllText($path,$utf8); if(-not $text.EndsWith("`n")){$text+="`r`n"} } else { $text=($headers -join ',')+"`r`n" }
    $text+= (($lines -join "`r`n")+"`r`n")
    [IO.File]::WriteAllText($path,$text,$utf8)
    return $lines.Count
}

# ---------- 1. 共享 Buff ----------
$buffCols="id,duration_ticks,stacking_policy,attributes,periodic_effect,periodic_interval_ticks,trigger_ids,mechanic,value,extra,target,stack_group,stack_strength".Split(',')
$buffs=@(
 @("talent_stun_short","20","RefreshDuration","","","","","stun","0","0","enemy","",""),
 @("talent_next_hit_atk","40","Strongest","atk:pct:0.25","","","","","0","0","self","talent_next_hit","1"),
 @("talent_team_atk","120","Strongest","atk:pct:0.10","","","","","0","0","ally","talent_team_atk","1"),
 @("talent_team_guard","120","Strongest","armor:flat:8","","","","","0","0","ally","talent_team_guard","1"),
 @("talent_haste","80","RefreshDuration","attack_speed_pct:add:0.15","","","","","0","0","self","",""),
 @("talent_guard","120","RefreshDuration","armor:flat:12","","","","","0","0","self","",""),
 @("talent_focus","80","RefreshDuration","crit_rate:add:0.10","","","","","0","0","self","",""),
 @("talent_slow","40","RefreshDuration","","","","","chill","0.25","0","enemy","","")
)
Append-Rows "buff.csv" $buffCols $buffs | Out-Null

# ---------- 2. 共享 Effect（只落Buff，不新增伤害路径） ----------
$effectCols="id,role,kind,damage_type,share,extent,speed,impact_effect,behavior,spawn_tick,buff_id,entity_kind,lifetime_ticks,resolve_effect_ids,mechanic_id,damage_parts,projectile_count,spread_degrees".Split(',')
function EffRow([string]$id,[string]$buff,[string]$extent){ return @($id,"prototype","ApplyBuff","","",$extent,"","","","",$buff,"","","","","","","") }
$effects=@(
 (EffRow "eff_talent_stun" "talent_stun_short" "0"),
 (EffRow "eff_talent_next_atk" "talent_next_hit_atk" "0"),
 (EffRow "eff_talent_team_atk" "talent_team_atk" "5"),
 (EffRow "eff_talent_team_guard" "talent_team_guard" "5"),
 (EffRow "eff_talent_haste" "talent_haste" "0"),
 (EffRow "eff_talent_guard" "talent_guard" "0"),
 (EffRow "eff_talent_focus" "talent_focus" "0"),
 (EffRow "eff_talent_slow" "talent_slow" "0")
)
Append-Rows "effect.csv" $effectCols $effects | Out-Null

# ---------- 3. 节点表 ----------
# 列：本地ID|区域|MaxRank|前置|互斥|kind|effect|value|per_rank|技能槽|名称|描述|触发事件|触发Effect|每施放限额|每目标限额|ICD秒
$nodes=@(
 # ---------------- 野蛮人 21 新节点 ----------------
 "B01|RB|3|ROOT||modifier|coefficient|0.12|0.12|S1|沉重打击|重击系数随投入提高，不改变AR/DR门槛",
 "B02|RB|1|B01@1|BAR_B|trigger|trigger|0|0|S1|震慑重击|重击命中后短硬直，内置间隔限制|OnHit|eff_talent_stun|1|1|3",
 "B03|RB|1|B01@1|BAR_B|trigger|trigger|0|0|S1|破阵猛击|重击命中后获得一次下一击攻击强化|OnHit|eff_talent_next_atk|1|1|4",
 "S202|RS2|3|S201@1||modifier|coefficient|0.15|0.15|S2|旋风刀势|提高旋风合法物理命中强度",
 "S203|RS2|1|S201@1||modifier|extent|0.15|0|S2|回旋步伐|扩大旋风合法命中范围，不新增接敌容量",
 "S204|RS2|1|S202@1|BAR_S2|modifier|extent|0.25|0|S2|群阵疾扫|扩大合法目标覆盖，单目标命中仍有界",
 "S205|RS2|1|S202@1|BAR_S2|modifier|coefficient|0.40|0|S2|凶兽斩击|集中到少数已接敌目标，提高单体强度",
 "S206|RS2|1|S203@1||trigger|trigger|0|0|S2|旋风余势|旋风命中后获得一次下一击强化|OnHit|eff_talent_next_atk|1|1|5",
 "S302|RS3|2|S301@1||modifier|duration|0.30|0.15|S3|号角传递|延长战吼效果持续，不重复注册Buff",
 "S303|RS3|1|S301@1||modifier|range|0.30|0|S3|吼声时机|扩大战吼作用范围以覆盖接近中的敌群",
 "S304|RS3|1|S302@1|BAR_S3|trigger|trigger|0|0|S3|战斗号令|战吼生效时给队伍有限攻势强化|OnSkillCommit|eff_talent_team_atk|1|1|6",
 "S305|RS3|1|S302@1|BAR_S3|modifier|duration|0.25|0|S3|挑战怒吼|延长合法范围内短时嘲讽",
 "S402|RS4|3|S401@1||modifier|summon_hp|0.20|0.10|S4|先祖之力|提高先祖存活与近战贡献",
 "S403|RS4|1|S402@1|BAR_S4|modifier|summon_attack|0.20|0|S4|先祖破阵|援军偏向协助打击重点目标",
 "S404|RS4|1|S402@1|BAR_S4|modifier|summon_hp|0.25|0|S4|先祖守阵|援军更耐打以建立合法接敌",
 "T02|RT|2|T01@1||trait|coefficient|0.10|0.05|S1|战场本能|近身战持续性：强化重击出手",
 "T03|RT|1|T01@1&S301@1||trigger|trigger|0|0|S3|狂战连携|战吼生效时自身攻速提升|OnSkillCommit|eff_talent_haste|1|1|6",
 "T04|RT|1|T02@1|BAR_T|trait|coefficient|0.20|0|S2|狂怒阵线|攻势姿态：强化旋风持续输出",
 "T05|RT|1|T02@1|BAR_T|trait|duration|0.25|0|S3|护队战线|护阵姿态：延长战吼与嘲讽",
 "T06|RT|1|T04@1&S204@1||trigger|trigger|0|0|S2|旋风压境|旋风命中后获得一次攻势强化|OnHit|eff_talent_next_atk|1|1|6",
 "T07|RT|1|T05@1&S305@1||trigger|trigger|0|0|S3|钢铁救援|嘲讽生效后自身获得护甲反馈|OnSkillCommit|eff_talent_guard|1|1|6",
 # ---------------- 游侠 21 新节点 ----------------
 "B01|RB|3|ROOT||modifier|coefficient|0.12|0.12|S1|稳弓基础|基础箭系数提高，默认无追踪",
 "B02|RB|1|B01@1|RAN_B|trigger|trigger|0|0|S1|转火快箭|击杀后获得短暂攻速以重新评估目标|OnKill|eff_talent_haste|1|1|4",
 "B03|RB|1|B01@1|RAN_B|trigger|trigger|0|0|S1|精英锁矢|基础射击命中后获得一次下一击强化|OnHit|eff_talent_next_atk|1|1|4",
 "S202|RS2|3|S201@1||modifier|coefficient|0.15|0.15|S2|箭幕齐放|提高多重箭对合法不同敌人的有效清理",
 "S203|RS2|1|S201@1||modifier|targets|1|0|S2|弹道分配|增加一支弹道，优先分配给不同目标",
 "S204|RS2|1|S202@1|RAN_S2|modifier|targets|2|0|S2|猎群齐射|进一步增加覆盖目标数",
 "S205|RS2|1|S202@1|RAN_S2|modifier|coefficient|0.40|0|S2|定点箭幕|缩窄表现角度并集中单体伤害",
 "S206|RS2|1|S203@1||modifier|extent|0.30|0|S2|穿行利箭|扩大箭矢命中体积以形成有限贯穿",
 "S207|RS2|1|S204@1||trigger|trigger|0|0|S2|齐射再定位|多重箭击杀后获得短暂攻速|OnKill|eff_talent_haste|1|1|6",
 "S302|RS3|2|S301@1||modifier|coefficient|0.15|0.10|S3|回旋追矢|改善追踪箭的飞行与命中效率",
 "S303|RS3|1|S301@1||modifier|range|0.30|0|S3|猎首判定|扩大合法锁定距离以优先覆盖重点目标",
 "S304|RS3|1|S302@1|RAN_S3|modifier|coefficient|0.45|0|S3|猎首终结|对当前重点目标更强，保留单体身份",
 "S305|RS3|1|S302@1|RAN_S3|trigger|trigger|0|0|S3|游猎牵制|导引箭命中后施加有限减速|OnHit|eff_talent_slow|1|1|5",
 "S402|RS4|3|S401@1||modifier|summon_hp|0.20|0.10|S4|战场化身|提高女武神真实HP与贡献",
 "S403|RS4|1|S402@1|RAN_S4|modifier|summon_attack|0.20|0|S4|女武神护阵|女武神更偏牵制威胁游侠的敌人",
 "S404|RS4|1|S402@1|RAN_S4|modifier|summon_hp|0.25|0|S4|女武神猎首|女武神更耐打以配合集中目标",
 "T02|RT|2|T01@1||trait|coefficient|0.10|0.05|S1|预判射线|优化基础箭向合法敌人的分配稳定性",
 "T03|RT|1|T02@1|RAN_T|trait|targets|1|0|S2|猎群法则|群猎路线：多重箭多覆盖一个目标",
 "T04|RT|1|T02@1|RAN_T|trait|coefficient|0.20|0|S3|猎首法则|猎首路线：导引箭更集中单体",
 "T05|RT|1|T03@1&S204@1||trigger|trigger|0|0|S2|风暴箭路|多重箭命中后获得短暂攻速|OnHit|eff_talent_haste|1|1|6",
 "T06|RT|1|T04@1&S304@1||trigger|trigger|0|0|S3|锁定猎场|导引箭命中后获得下一击强化|OnHit|eff_talent_next_atk|1|1|6",
 # ---------------- 法师 20 新节点 ----------------
 "B01|RB|3|ROOT||modifier|coefficient|0.12|0.12|S1|火球基础|火球范围火伤系数提高",
 "B02|RB|1|B01@1|SOR_B|modifier|extent|0.20|0|S1|扩散火球|扩大有界爆裂范围，重心清群",
 "B03|RB|1|B01@1|SOR_B|modifier|extent|-0.15|0|S1|聚焦火球|减小爆裂范围提高主目标效率",
 "B04|RB|1|B01@1||trigger|trigger|0|0|S1|爆裂余温|火球命中后自身获得一次法术强化|OnHit|eff_talent_focus|1|1|5",
 "S202|RS2|3|S201@1||modifier|coefficient|0.15|0.15|S2|冰霜扩环|提高新星伤害与减速效用",
 "S203|RS2|1|S201@1||modifier|cooldown|-0.15|0|S2|自卫时机|缩短新星冷却以便近身受威胁时可用",
 "S204|RS2|1|S202@1|SOR_S2|modifier|extent|0.25|0|S2|寒冰屏障|扩大有界影响范围，协助退让",
 "S205|RS2|1|S202@1|SOR_S2|modifier|duration|0.35|0|S2|寒凝压制|减小范围但延长减速持续",
 "S302|RS3|3|S301@1||modifier|targets|1|0|S3|雷链延伸|增加合法跳跃目标数，不重复命中",
 "S303|RS3|1|S302@1|SOR_S3|modifier|coefficient|0.30|0|S3|冷雷传导|优先经过减速目标并强化传导效率",
 "S304|RS3|1|S302@1|SOR_S3|modifier|targets|-1|0|S3|闪雷集束|减少跳跃数提高单体效率",
 "S402|RS4|3|S401@1||modifier|coefficient|0.15|0.15|S4|星火积压|提高陨星中心火伤",
 "S403|RS4|1|S402@1|SOR_S4|modifier|extent|0.25|0|S4|裂焰扩散|扩大群体覆盖，降低单体集中",
 "S404|RS4|1|S402@1|SOR_S4|modifier|coefficient|0.40|0|S4|陨星聚核|精英中心爆发，降低覆盖",
 "S405|RS4|1|S401@1&B01@1||trigger|trigger|0|0|S4|火球定位|火球命中后改善下一次陨星的动作效率|OnHit|eff_talent_focus|1|1|6",
 "T02|RT|2|T01@1||trait|coefficient|0.10|0.05|S1|施法节制|改善本英雄后续一次法术动作稳定性",
 "T03|RT|1|T02@1|SOR_T|trait|coefficient|0.20|0|S4|炎术专精|火焰路线：强化陨星",
 "T04|RT|1|T02@1|SOR_T|trait|duration|0.25|0|S2|霜雷织网|霜雷路线：延长减速以衔接雷链",
 "T05|RT|1|T03@1&S401@1||trigger|trigger|0|0|S4|炎星引爆|陨星命中后获得法术强化|OnHit|eff_talent_focus|1|1|6",
 "T06|RT|1|T04@1&S301@1||trigger|trigger|0|0|S3|电霜连锁|雷链命中后获得法术强化|OnHit|eff_talent_focus|1|1|6",
 # ---------------- 圣骑士 20 新节点 ----------------
 "B01|RB|3|ROOT||modifier|coefficient|0.12|0.12|S1|盾手基础|圣击物理伤害强化，保持近身射程",
 "B02|RB|1|B01@1|PAL_B|modifier|duration|0.30|0|S1|守势圣击|圣击后维持防守位置：延长自身防护",
 "B03|RB|1|B01@1|PAL_B|modifier|coefficient|0.30|0|S1|进击圣击|对近身目标更稳定压制",
 "S202|RS2|3|S201@1||modifier|coefficient|0.15|0.15|S2|坚固盾面|提高盾击伤害与稳健性",
 "S203|RS2|1|S201@1||modifier|range|0.30|0|S2|盾击优先|扩大合法选择范围以覆盖威胁友军的敌人",
 "S204|RS2|1|S202@1|PAL_S2|modifier|duration|0.40|0|S2|盾墙制止|延长短硬直，Boss免疫单独处理",
 "S205|RS2|1|S202@1|PAL_S2|modifier|coefficient|0.40|0|S2|惩戒猛击|盾击偏重伤害，不附更长控制",
 "S302|RS3|3|S301@1||modifier|extent|0.20|0.10|S3|援助祷词|扩大祈祷治疗覆盖对象",
 "S303|RS3|1|S302@1|PAL_S3|modifier|extent|0.25|0|S3|群体祈祷|优先覆盖多名受损友军",
 "S304|RS3|1|S302@1|PAL_S3|modifier|range|0.25|0|S3|紧急护佑|扩大最危险友军的可治疗距离",
 "S305|RS3|1|S301@1||trigger|trigger|0|0|S3|祈祷余韵|祈祷生效后给友军有限护阵反馈|OnSkillCommit|eff_talent_team_guard|1|1|6",
 "S402|RS4|3|S401@1||modifier|coefficient|0.15|0.15|S4|圣锤轨迹|提高飞锤对近身敌群的覆盖效率",
 "S403|RS4|1|S402@1|PAL_S4|modifier|extent|-0.15|0|S4|锤阵护体|缩小范围靠近自身保护",
 "S404|RS4|1|S402@1|PAL_S4|modifier|extent|0.25|0|S4|锤阵扫荡|扩展合法群伤范围，保持上限",
 "S405|RS4|1|S401@1&S201@1||trigger|trigger|0|0|S4|锤盾连携|盾击成功后下一次飞锤更强|OnHit|eff_talent_next_atk|1|1|6",
 "T02|RT|2|T01@1||trait|duration|0.15|0.10|S3|光环秩序|规范光环覆盖与刷新时长",
 "T03|RT|1|T02@1|PAL_T|trigger|trigger|0|0|S3|祈祷光环|守护路线：祈祷后队伍获得护甲反馈|OnSkillCommit|eff_talent_team_guard|1|1|6",
 "T04|RT|1|T02@1|PAL_T|trigger|trigger|0|0|S3|狂热光环|进攻路线：祈祷后队伍获得攻势节奏|OnSkillCommit|eff_talent_team_atk|1|1|6",
 "T05|RT|1|T03@1&S201@1||trigger|trigger|0|0|S2|圣盾守望|盾击命中后队伍获得护甲反馈|OnHit|eff_talent_team_guard|1|1|6",
 "T06|RT|1|T04@1&S401@1||trigger|trigger|0|0|S4|圣锤狂信|飞锤命中后队伍获得攻势强化|OnHit|eff_talent_team_atk|1|1|6"
)
$class=@{
 BAR=@{hero="warrior";slot=@{S1="barb_bash";S2="barb_whirlwind_window";S3="barb_taunt";S4="barb_ancestor"};trait="warrior_trait"};
 RAN=@{hero="ranger";slot=@{S1="ran_shot";S2="ran_multishot";S3="ran_guided_arrow";S4="ran_valkyrie"};trait="ranger_trait"};
 SOR=@{hero="mage";slot=@{S1="mage_fireball";S2="sor_frost_nova";S3="sor_chain_lightning";S4="sor_meteor"};trait="mage_trait"};
 PAL=@{hero="priest";slot=@{S1="paladin_smite";S2="pal_shield_bash";S3="pal_prayer";S4="pal_hammer"};trait="priest_trait"}
}
$talentCols="id,hero,tree,tree_name,row,col,name,description,max_rank,bonus_allowed,kind,prerequisite,required_rank,required_points,effect,value,per_rank,cooldown,range,radius,damage_type,projectile_speed,skill_id,exclusive_group,class_trait,trigger_id".Split(',')
$triggers=@(); $limits=@(); $icds=@(); $talents=@(); $ledger=@()
$counts=@(21,21,20,20); $clsOrder=@("BAR","RAN","SOR","PAL"); $idx=0
for($ci=0;$ci -lt 4;$ci++){
    $cls=$clsOrder[$ci]; $info=$class[$cls]; $rowIndex=($ci*40)
    $segment=@($nodes[$idx..($idx+$counts[$ci]-1)]); $idx+=$counts[$ci]
    foreach($line in $segment){
        $f=$line -split '\|'
        $local=$f[0]; $maxRank=$f[2]; $preRaw=$f[3]; $excl=$f[4]; $kind=$f[5]; $effect=$f[6]
        $value=$f[7]; $perRank=$f[8]; $slotName=$f[9]; $name=$f[10]; $desc=$f[11]
        $trigEvent=$f[12]; $trigEffect=$f[13]; $limitCast=$f[14]; $limitTarget=$f[15]; $icd=$f[16]
        $id="$cls-$local"; $skillId=$info.slot[$slotName]
        $pre=""
        if($preRaw -and $preRaw -ne "ROOT"){
            $tokens=@()
            foreach($token in $preRaw.Split('&')){
                $parts=$token.Split('@'); $t=$parts[0]
                $mapped=switch($t){ "S201"{$info.slot["S2"]} "S301"{$info.slot["S3"]} "S401"{$info.slot["S4"]} "T01"{$info.trait} default{"$cls-$t"} }
                $tokens+= if($parts.Length -gt 1){ "$mapped@$($parts[1])" } else { $mapped }
            }
            $pre=($tokens -join '&')
        }
        $groupId=if($excl){ $excl } else { "" }
        $rowIndex++
        $triggerId=""
        if($kind -eq "trigger"){
            $triggerId="trg_"+($id.ToLower() -replace '[^a-z0-9]','_')
            $triggers+=,@($triggerId,$trigEvent,$trigEffect,"",$(if($trigEvent -eq "OnHit"){"direct"}else{""}))
            $limits+=,@($triggerId,$limitCast,$limitTarget)
            $icds+=,@($triggerId,$icd)
            $ledger+=,"$id|trigger|$trigEvent→$trigEffect|限额$limitCast/$limitTarget ICD${icd}s 作用于$skillId"
        } else {
            $ledger+=,"$id|$kind|$effect=$value(+$perRank/级)|作用于$skillId"
        }
        $talents+=,@($id,$info.hero,"3","固定技能","$rowIndex","0",$name,$desc,"$maxRank","0",$kind,$pre,"0","0",$effect,"$value","$perRank","0.05","1","0","Physical","0",$skillId,$groupId,"0",$triggerId)
    }
}
$triggerCols="id,event_kind,effect_id,owner_buff,origin_filter".Split(',')
$limitCols="id,max_per_cast,max_per_target".Split(',')
$icdCols="id,icd_seconds".Split(',')
$c1=Append-Rows "trigger.csv" $triggerCols $triggers
$c2=Append-Rows "proc_limit.csv" $limitCols $limits
$c3=Append-Rows "proc_icd.csv" $icdCols $icds
$c4=Append-Rows "talent.csv" $talentCols $talents
$ledgerPath=Join-Path $Root "../artifacts/qa/s15-c1a2-node-ledger.csv"
New-Item -ItemType Directory -Force -Path (Split-Path $ledgerPath) | Out-Null
[IO.File]::WriteAllText($ledgerPath,(("node|mechanism|detail" + "`r`n")+($ledger -join "`r`n")+"`r`n"),$utf8)
"added trigger=$c1 proc_limit=$c2 proc_icd=$c3 talent=$c4"
"ledger=$ledgerPath"
