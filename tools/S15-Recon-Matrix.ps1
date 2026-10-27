param([string]$OutDir="d:/Project-Asmo/Idle-CRPG/artifacts/qa/s15-c1-c3-recon-20261027-100854")
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$raw=@'
CORE-01|CORE|公共XP单池/无等级差衰减/手动升级|P2-3-M2 §3|HIGH|C|PublicExperience|HeroExperience.cs:22|tests WP2_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|经验共享但仍需玩家逐次点击
CORE-02|CORE|固定四席/1-4人/同职实例隔离|P2-3-M2 §1.1|HIGH|C|HeroInstanceId/TeamLocked|SessionSnapshot 校验|tests WP1_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
CORE-03|CORE|自然ARDR为主要准入/旧Armor退出伤害|P2-3-M2 §2.1|HIGH|C|prototype_progression.csv|PrototypeProgression.cs:27|tests WP2_PASS|DamagePipeline 仍接收 Armor|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_ENGINEERING_FIX_PROPOSAL|YES|需确认退出伤害是否落实
CORE-04|CORE|KP逐笔Lmax/逐英雄ARDR检查/Lmin禁售|P2-3-M2 §4|HIGH|C|Progression.LmaxCost|KpProgression.cs:29|tests WP3_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
CORE-05|CORE|Account-Run/旧档价值/重生冻结|P2-4 Review §19|HIGH|C|Account/Run 快照|LegacyQuarantine|tests WP1/LEGACY_SELECTION_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
CORE-06|CORE|离线结算/药剂冻结/不后台成长|P2-4 Review §24|HIGH|C|Rebirth.SettleOffline|SaveStore.cs:100|tests WP7_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C1A-01|C1A|16固定技能映射与自动施放|S06_V0.8|HIGH|C|skill_slot.csv 20槽|SkillRuntime.RunActiveSkill|tests C1A_SKILL_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C1A-02|C1A|四职业T资格与实例投点|S06/S07|HIGH|C|class_trait 节点|FixedSkills.cs:103|tests C1A/C1A2_PASS|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C1A-03|C1A|98天赋逐节点与八套构筑|S07_V0.8|HIGH|P|talent.csv 155行|tests --c1a2-checks|8 builds|部分节点语义近似|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_DESIGN_SPEC|YES|Review §23.4
C1A-04|C1A|目标优先/精英特化/下一次攻击|S07|HIGH|P|modifier/trigger 节点|tests --c1a2-checks|--|优先→射程；下一击→命中后Buff|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_DESIGN_SPEC|YES|--
C1A-05|C1A|护阵类天赋是否仍消费Armor|S07|HIGH|P|talent armor 被动|Talents.cs PassiveTalentValue|--|Armor 仍参与伤害公式|IMPLEMENTED_DIFFERENT|E1_STATIC|NEEDS_ENGINEERING_FIX_PROPOSAL|YES|与 CORE-03 同根因
C1A-06|C1A|旋风/陨星/祝福锤可观察行为|S06|HIGH|P|barb_whirlwind_window 等|AreaPulse 原地脉冲|tests C1A|--|无位移/无固定落点/绕体近似|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_DESIGN_SPEC|YES|Review §22.4
C1B-01|C1B|31设计星图/78阶/禁售映射|C1B_V0.5|HIGH|P|star.csv 39节点|Stars.cs|tests --c1b-checks|10旧节点退休且O01/D01/G01/C-BAR映射旧族|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|YES|价格口径需确认
C1B-02|C1B|57/55路线与s_root入口成本|C1B_V0.5|HIGH|P|star_layout 唯一根|StarConfiguration.cs:38|tests --c1b3-checks|路线多1笔s_root不计入57/55|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|YES|--
C1B-03|C1B|@Rank前置 vs 点亮即可|C1B_V0.5|HIGH|P|layout prereq 仅点亮|StarConfiguration.cs:79|--|@Rank门槛未实现|IMPLEMENTED_DIFFERENT|E1_STATIC|NEEDS_ENGINEERING_FIX_PROPOSAL|YES|与 UI-02 相关
C1B-04|C1B|T资格ClassId共享|C1B_V0.5|HIGH|C|class_trait_unlock target_id|Talents.cs:135|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C1B-05|C1B|精英身份读档与G02收据|C1B_V0.5|HIGH|P|Actor.Elite/EliteToken|MonsterContent.cs:105|--|检查点用MonsterOrigin重建不保留Elite|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_ENGINEERING_FIX_PROPOSAL|YES|--
C2A-01|C2A|393键/136地牢/226层/31城堡|C2A|HIGH|C|chapters/act1-4|tests --c2a-checks|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C2A-02|C2A|226层节拍影响真实生成|C2A|HIGH|P|floor_rhythm.csv 226行|EncounterPlans.cs + Reveal|--|数量±1/身份偏好/精英±10已消费|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|YES|--
C2A-03|C2A|31城堡威胁组合与楼层继承|S09|HIGH|P|boss_skill.csv 34行|--|--|城堡楼层沿用统治区池无逐城堡组合|NOT_VERIFIED|E1_STATIC|NEEDS_RUNTIME_EVIDENCE|YES|--
C2A-04|C2A|四章终Boss阶段与预警|C2A|HIGH|C|BossPhases 配置|BossPhaseRuntime.cs|--|机制存在但本轮未取运行证据|NOT_VERIFIED|E1_STATIC|NEEDS_RUNTIME_EVIDENCE|YES|需补固定种子阶段证据
C2A-05|C2A|第一章首个城堡Lv5门槛|S09|HIGH|C|act1 A02-C01 level_min=5|KpProgression.cs:50|--|门槛=Lmax≥5且A01-C01已通|MATCHED_EVIDENCED|E1_STATIC|KEEP_MATCH|YES|体验卡点见OBS-01
C2A-06|C2A|怪物密度与TTK|C2A|HIGH|P|spawn_rule 1-5 + ±1|GameSession.cs:488|--|每房3-5只/怪HP35-55/约2击即死|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_DESIGN_SPEC|YES|密度未由设计给定
C2B-01|C2B|六槽单主属性与24实投|EQ-01|HIGH|C|equipment.csv 3atk+3hp|EquipmentValue|--|24组合=6槽×4品质|MATCHED_EVIDENCED|E1_STATIC|KEEP_MATCH|NO|--
C2B-02|C2B|装备ATKHP与ARDR门槛冲突|EQ-01|HIGH|C|equipment.csv|EquipmentValue/PrototypeProgression|--|装备不提供AR/DR|IMPLEMENTED_DIFFERENT|E1_STATIC|NEEDS_USER_RULING|YES|单调属新体验反馈
C2B-03|C2B|31怪级掉落与品质|C2B|HIGH|H|drop_table.csv 31行|DropRoll|--|MATCHED_EVIDENCED|E1_STATIC|KEEP_MATCH|NO|--
C2B-04|C2B|手动换装与五件合批|C2B|HIGH|C|GearSlots|Equipment.cs:191|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C2B-05|C2B|五类卷轴消费者|C2B|HIGH|C|scroll.csv 5行|Consumables.UseScroll|--|连锁跳距与保护效果未取证|NOT_VERIFIED|E1_STATIC|NEEDS_RUNTIME_EVIDENCE|YES|--
C2B-06|C2B|四药剂槽与坚韧减伤|POT-01|HIGH|C|potion.csv 4行|AdvanceConsumables|--|坚韧为reduce_damage 15%[H]|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_USER_RULING|YES|数值未批准
C2B-07|C2B|金币流水与幂等|C2B|HIGH|C|AuditCurrency|Equipment.cs:201|tests WP5/WP6|--|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C3-01|C3|21参数真实消费者|C3_V0.7|HIGH|H|Progression/star/drop|--|--|本轮未逐参数复核|NOT_VERIFIED|E0_UNVERIFIED|NEEDS_RUNTIME_EVIDENCE|YES|--
C3-02|C3|XP/KP/金币逐笔守恒与复现|C3_V0.7|HIGH|C|AuditCurrency|tests INT_01_08_PASS|--|非600min场景|MATCHED_EVIDENCED|E2_RUNTIME|KEEP_MATCH|NO|--
C3-03|C3|600min内只能单次升级的策略差异|C3_04|CRITICAL|P|--|SimulationChecks 策略|--|每周期1笔升级 vs 96笔需求|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_ENGINEERING_FIX_PROPOSAL|YES|脚本限制非产品规则
C3-04|C3|真实章节首次进入 vs 通关|C3_04|CRITICAL|P|--|--|--|报告仅到未到第四章|NOT_VERIFIED|E2_RUNTIME|NEEDS_RUNTIME_EVIDENCE|YES|--
C3-05|C3|复刷击杀与11189代理不同|C3|HIGH|P|--|--|--|复刷击杀未分解|NOT_VERIFIED|E1_STATIC|NEEDS_RUNTIME_EVIDENCE|YES|--
C3-06|C3|Lv25/Lmax31里程碑 vs 硬上限|C3|HIGH|P|MaxHeroLevel/MaxMonsterLevel|--|--|生产上限10000未硬封|MATCHED_EVIDENCED|E1_STATIC|KEEP_MATCH|NO|--
C3-07|C3|主动操作模型是否缺失|C3_04|CRITICAL|P|--|--|--|星图根/天赋/装备/药剂未建模|IMPLEMENTED_DIFFERENT|E2_RUNTIME|NEEDS_ENGINEERING_FIX_PROPOSAL|YES|--
C3-08|C3|BossTTK/真实10h未验|C3|HIGH|P|--|--|--|无真实10h与Godot证据|NOT_VERIFIED|E0_UNVERIFIED|NEEDS_RUNTIME_EVIDENCE|YES|--
UI-01|UI|用户反馈UI完全不合格|UX-01|USER|O|--|--|--|用户第一手观察|USER_REVIEW_OPEN|USER_OBSERVED|DEFER_EXPERIENCE|YES|不得用Smoke PASS抵消
UI-02|UI|星图连线混乱不可读|STAR-UX-01|USER|O|star_layout 39节点38边|StarUi/StarCanvas|截图 star-class-trait-purchased.png|边多交叉且无@Rank标注|USER_REVIEW_OPEN|USER_OBSERVED|DEFER_EXPERIENCE|YES|--
UI-03|UI|四英雄逐级点击负担|PROG-02|USER|O|UpgradeHero 单级|HeroExperience.cs UpgradeHero|--|16笔到Lv5、96笔到Lv25|USER_REVIEW_OPEN|E1_STATIC|DEFER_EXPERIENCE|YES|--
'@
$header=@('AuditId','Domain','RequirementOrObservation','DesignSourceHint','RiskForAudit','Authority','ProductionConfigOrId','CodeConsumerPathAndLine','RuntimeEvidenceFileAndSeed','ObservedDifference','UniqueMainState','EvidenceStrength','ProposedDisposition','OwnerDecisionRequired','ReviewComment')
$lines=New-Object System.Collections.Generic.List[string]
function Q([string]$v){ return '"' + ($v -replace '"','""') + '"' }
$lines.Add((($header | ForEach-Object { Q $_ }) -join ','))
foreach($row in ($raw -split "`r?`n")){
    if($row.Trim() -eq ''){continue}
    $f=$row -split '\|'
    if($f.Count -ne 15){
        # 容忍多余分隔：把多余的文本列并入"运行时证据"列，保证 15 列对齐。
        $extra=$f.Count-15
        $head=@($f[0..8]); $mid=@($f[9..(9+$extra)]) -join " "; $tail=@($f[(10+$extra)..($f.Count-1)])
        $f=@($head + $mid) + $tail
    }
    $lines.Add((($f | ForEach-Object { Q $_ }) -join ','))
}
[IO.File]::WriteAllText((Join-Path $OutDir "recon_master_matrix.csv"),(($lines -join "`r`n")+"`r`n"),$utf8)
$m=Import-Csv (Join-Path $OutDir "recon_master_matrix.csv") -Encoding UTF8
$m | Group-Object UniqueMainState | ForEach-Object { $_.Name + '=' + $_.Count }
