param([string]$OutDir=(Get-ChildItem "d:/Project-Asmo/Idle-CRPG/artifacts/qa" -Directory -Filter 's15-c1-c3-recon-proof-*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
function W([string]$n,[string]$raw){
    $lines=New-Object System.Collections.Generic.List[string]
    foreach($row in ($raw -split "`r?`n")){
        if($row.Trim() -eq ''){continue}
        $f=$row -split '\|'
        $lines.Add((($f | ForEach-Object { '"' + ($_ -replace '"','""') + '"' }) -join ','))
    }
    [IO.File]::WriteAllText((Join-Path $OutDir $n),(($lines -join "`r`n")+"`r`n"),$utf8)
}
W "recon_status_corrections.csv" @'
AuditId|original_status|new_status|authority|evidence|reason|owner
CORE-03|IMPLEMENTED_DIFFERENT|MATCHED_EVIDENCED|C|Scripts/Core/DamagePipeline.cs:72 仅做有限性/非负校验；74-98 公式只含 Attack/AR-DR/技能/威能/易伤/暴击/抗性/格挡，无 input.Armor|字段存在≠参与伤害，Armor 为 INPUT_ONLY，旧结论把入参误判为伤害乘区|工程
C1A-05|IMPLEMENTED_DIFFERENT|NOT_IMPLEMENTED|P|7 个 passive（barb_courage/barb_defense/ranger_hide/mage_frost_armor/paladin_armor/paladin_fortitude/necro_bone_armor）与 3 个 buff（elite_hardened/talent_team_guard/talent_guard）产 armor；armor 只进 DamageInput.Armor，无任何伤害/承伤读取|护甲类天赋 TALENT_NO_EFFECT，属真实缺消费者；如需保留护阵身份需改效果或恢复 armor 公式（待授权）|工程+设计
C2A-03|NOT_VERIFIED|MATCHED_EVIDENCED|P|config/chapters/act1-4/dungeon/castle_threat.csv；EncounterPlans.cs:67 LoadCastleThreats→CastleThreats；EncounterFor(GameSession.cs:497)；--c2a-boss-checks 2026-10-27 通过（31 castle threat combos）|原审计只查 boss_skill.csv 未查到 castle_threat 消费者，属取证遗漏|工程
C2A-04|NOT_VERIFIED|MATCHED_EVIDENCED|C|config/monster/boss_phase.csv；BossPhases.cs/BossPhaseRuntime.cs；--c2a-boss-checks 2026-10-27 输出 andariel_phases/council_to_mephisto/chaos_sanctuary_stages|机制与阶段数据均已存在且可复现（4 个章末 Boss）；仅机制级，UI 可读性另评|工程
C2B-02|IMPLEMENTED_DIFFERENT|SUPERSEDED_BY_USER|C|equipment.csv 3×atk+3×hp；EQ-01[C] 六槽单主属性；AR/DR 只由等级+星图|符合 EQ-01[C]；“装备不能替代评级成长”是用户体验新反馈 USER_OBSERVED，不是设计违约|用户+设计
C2B-06|IMPLEMENTED_DIFFERENT|SUPERSEDED_BY_USER|C|potion.csv + POT-01[C] 百分比减伤；15% 为 [H]|玩法身份已按裁决更换；数值未批准，数值层记 NOT_VERIFIED|用户
C1B-03|IMPLEMENTED_DIFFERENT|DESIGN_UNDECIDED|P|design_S07_98nodes_v0.8.csv：98/98 Status=P，78 节点使用 A@Rank 前置；引擎 PrerequisitesMet 只判点亮|@Rank 属 [P] 候选，不是已确认[C]；若升[C] 会改变 57/55 顺序与可售性，需用户裁决|用户+设计
C3-03|IMPLEMENTED_DIFFERENT|EXPERIMENT_LIMITATION|P|refs/c3_data_report.md §1.3：每 10/45 分钟仅 1 人 1 级|属实验策略覆盖缺口，不是生产只能单次升级（生产逐次点击为[C]）|工程
C3-07|IMPLEMENTED_DIFFERENT|EXPERIMENT_LIMITATION|P|refs/c3_data_report.md §1.4 NOT_MODELED 列表|同上：星图根/天赋/装备/耗材未建模是实验限制，不是生产缺失|工程
C2A-06|IMPLEMENTED_DIFFERENT|DESIGN_UNDECIDED|O|spawn_rule 1-5 + 节奏±1；act_monsters hp35-55；无[C]级密度/TTK目标|密度/TTK 未确认为[C]，工程未违反已确认数值；用户观感保留 USER_OBSERVED|用户+设计
C2A-05|MATCHED_EVIDENCED|MATCHED_EVIDENCED|C|A02-C01 level_min=5；KpProgression.cs:50；AR=10+(L-1)*10|机制链成立；体验卡点保留 USER_OBSERVED，不因机制一致而判体验通过|用户
UI-01|USER_REVIEW_OPEN|USER_REVIEW_OPEN|O|用户第一手体验|UI 不合格不由 smoke PASS 覆盖；统一 UI/UE 后置|用户
UI-02|USER_REVIEW_OPEN|USER_REVIEW_OPEN|O|star_layout 39 节点 38 边；无 @Rank 标注|同上，星图可读性后置|用户
UI-03|USER_REVIEW_OPEN|USER_REVIEW_OPEN|O|UpgradeHero 单级；Lv5=16 次|升级点击繁琐属体验，不得自动升级替代[C]|用户
'@
W "recon_evidence_verification.csv" @'
EvidenceId|Claim|Path|Check|Result|Strength|Reproduce
EV-01|Armor 不参与伤害公式|Idle-CRPG/Scripts/Core/DamagePipeline.cs:69-99|逐行读 Resolve 表达式|input.Armor 仅第72行校验，未出现在任何乘区|E1_STATIC（源码级定论）|静态可复核
EV-02|无动态 Armor A/B|tests/*|搜索既有 armor A/B 用例|无；任务禁止新增测试源码|E0_UNVERIFIED（动态差值）|NOT_AVAILABLE
EV-03|护甲天赋产出 armor|config/skill/talent.csv 7 行 + buff.csv 3 行|grep armor|7 passive + 3 buff|E1_STATIC|静态可复核
EV-04|城堡威胁组合消费者|config/chapters/act1-4/dungeon/castle_threat.csv；EncounterPlans.cs:22/67/97；GameSession.cs:497|代码 + 配置|CastleThreats 载入并在 EncounterFor 消费|E2_RUNTIME|--c2a-boss-checks 退出0
EV-05|4 章末 Boss 阶段|config/monster/boss_phase.csv；BossPhaseRuntime.cs|运行 --c2a-boss-checks|C2A_A2_BOSS_PASS；产物 andariel_phases.csv 等|E2_RUNTIME|退出0（2026-10-27）
EV-06|98 设计节点全落地|refs/design_S07_98nodes_v0.8.csv；config/skill/talent.csv|ID 映射（16 别名为技能/特性节点）|98/98 命中|E2_RUNTIME(--c1a2-checks)|退出0
EV-07|C1B 31 设计节点|artifacts/qa/s15-c1b-b0/node_status_31.csv；config/star/star.csv|逐节点状态|24 已实现 / 7 禁售（4 延期+1 QA+2 阻塞）|E2_RUNTIME(--c1b-checks 今日)|退出0
EV-08|57/55 路线复现|docs/temp/.../C1B_两阵容...csv|运行 --c1b3-checks|DirectoryNotFoundException：设计路线 CSV 已从 temp 删除|NOT_VERIFIED(SOURCE_MISSING)|历史产物 s15-c1b3-9da00b57(2026-10-26)
EV-09|装备 28→24+4|refs/design_S11_28templates_P.csv；config/item/equipment.csv|逐模板映射|24 realized + 4 SUPERSEDED_BY_USER|E1_STATIC|静态可复核
EV-10|城堡 31/31|refs/design_S09_31castle_v1.0.csv；config/chapters/act1-4|逐 CompositeId 命中|31/31|E1_STATIC|静态可复核
'@
"written to $OutDir"
