param([string]$OutDir=(Get-ChildItem "d:/Project-Asmo/Idle-CRPG/artifacts/qa" -Directory -Filter 's15-c1-c3-recon-proof2-*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName)
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
CORE-03|IMPLEMENTED_DIFFERENT|MATCHED_EVIDENCED|C|DamagePipeline.cs:72 仅校验；74-98 公式无 input.Armor|Armor=INPUT_ONLY，旧结论误判|工程
C1A-05|IMPLEMENTED_DIFFERENT|NOT_IMPLEMENTED|P|7 passive + 3 buff 产 armor；无伤害/承伤读取|TALENT_NO_EFFECT|工程+设计
C2A-03|NOT_VERIFIED|MATCHED_EVIDENCED|P|castle_threat.csv×4章；EncounterPlans.cs:22/67/97；GameSession.cs:497；--c2a-boss-checks 退出0|取证遗漏|工程
C2A-04|NOT_VERIFIED|MATCHED_EVIDENCED|C|boss_phase.csv；BossPhaseRuntime；--c2a-boss-checks 产物 4 章末阶段|机制与证据可复现|工程
C2B-02|IMPLEMENTED_DIFFERENT|SUPERSEDED_BY_USER|C|3atk+3hp 符合 EQ-01[C]|体验反馈另记 USER_OBSERVED|用户+设计
C2B-06|IMPLEMENTED_DIFFERENT|SUPERSEDED_BY_USER|C|POT-01[C] 已实施；15% 为[H]|数值层 NOT_VERIFIED|用户
C1B-03|IMPLEMENTED_DIFFERENT|DESIGN_UNDECIDED|P|design_S07_98nodes 98/98 Status=P；78 节点用 A@Rank|@Rank 为[P]，需用户裁决是否升[C]|用户+设计
C1B_ROUTE|NOT_VERIFIED|MATCHED_EVIDENCED|P|设计包迁移至 P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包；临时恢复输入后 --c1b3-checks 退出0（57/55）|SOURCE_MISSING 已解除，非实现变化|工程
C2B_SCROLL|NOT_VERIFIED|MATCHED_EVIDENCED|P|C2B_5卷轴4药剂合同.csv；SC01-05→scroll_bolt/arc/flame/frost/banner；--c2b-checks 退出0|设计源已定位|工程
C2B_POTION|NOT_VERIFIED|MATCHED_EVIDENCED(3)+SUPERSEDED(1)|P|PO01/03/04 已实施；PO02 被 POT-01 覆盖|设计源已定位|工程+用户
C2B_DROP31|NOT_VERIFIED|IMPLEMENTED_DIFFERENT|H|C3_31怪级联合参数待校准_H-B1.csv；drop_table 31 行；表内标注 H_NOT_AUTHORIZED|覆盖31档但数值为工程[H]首轮值，非逐项采用|工程
C3-03|IMPLEMENTED_DIFFERENT|EXPERIMENT_LIMITATION|P|c3_data_report §1.3|实验策略限制|工程
C3-07|IMPLEMENTED_DIFFERENT|EXPERIMENT_LIMITATION|P|c3_data_report §1.4|同上|工程
C2A-06|IMPLEMENTED_DIFFERENT|DESIGN_UNDECIDED|O|spawn_rule 1-5 + ±1；无[C]密度/TTK目标|观感 USER_OBSERVED|用户+设计
C2A-05|MATCHED_EVIDENCED|MATCHED_EVIDENCED|C|A02-C01 level_min=5；KpProgression.cs:50|机制成立；体验另评|用户
UI-01|USER_REVIEW_OPEN|USER_REVIEW_OPEN|O|用户第一手体验|不由 smoke 覆盖|用户
UI-02|USER_REVIEW_OPEN|USER_REVIEW_OPEN|O|39节点38边无@Rank标注|后置 UI 改造|用户
UI-03|USER_REVIEW_OPEN|USER_REVIEW_OPEN|O|UpgradeHero 单级；Lv5=16次|体验待规格|用户
'@
W "recon_evidence_verification.csv" @'
EvidenceId|Claim|Path|Check|Result|Strength|Reproduce
EV-01|Armor 不参与伤害|Idle-CRPG/Scripts/Core/DamagePipeline.cs:69-99|逐行读 Resolve|input.Armor 未进入任何乘区|E1_STATIC（源码定论）|静态可复核
EV-02|动态 Armor A/B|tests|搜索既有用例|无；禁止新增测试源码|E0_UNVERIFIED|NOT_AVAILABLE
EV-03|护甲天赋产 armor|config/skill/talent.csv(7行)+buff.csv(3行)|grep|TALENT_NO_EFFECT|E1_STATIC|静态
EV-04|城堡威胁组合|config/chapters/act1-4/dungeon/castle_threat.csv；EncounterPlans.cs:22/67/97|代码+配置|CastleThreats 被 EncounterFor 消费|E2_RUNTIME|--c2a-boss-checks 退出0(2026-10-27)
EV-05|4章末Boss阶段|config/monster/boss_phase.csv；BossPhaseRuntime.cs|运行|C2A_A2_BOSS_PASS；4阶段产物|E2_RUNTIME|退出0
EV-06|98设计节点|refs/design_S07_98nodes_v0.8.csv；talent.csv|ID映射(16别名)|98/98|E2_RUNTIME(--c1a2-checks)|退出0
EV-07|C1B 31节点|C1B_31节点…csv(迁移后)；B0 映射；star.csv|逐ID|24实现/7禁售|E2_RUNTIME(--c1b-checks 2026-10-27)|退出0
EV-08|57/55路线|C1B_两阵容…csv(迁移后)|--c1b3-checks|C1B3_PASS 57/55 real purchases|E2_RUNTIME|退出0（临时恢复输入后）
EV-09|装备28→24+4|S11_28templates；equipment.csv|逐模板|24+4|E2_RUNTIME(--c2b-checks)|退出0
EV-10|31城堡|S09_31castle；act1-4|逐CompositeId|31/31|E1_STATIC+E2(阶段)|静态+运行
EV-11|5卷轴|C2B_5卷轴4药剂合同.csv；scroll.csv|逐ID→生产|SC01-05全命中|E2_RUNTIME(--c2b-checks)|退出0
EV-12|4药剂|同上|逐ID→生产|PO01/03/04命中；PO02被POT-01覆盖|E2_RUNTIME|退出0
EV-13|31怪级|C3_31怪级联合参数待校准_H-B1.csv；drop_table.csv|逐级|31/31行存在，数值[H]|E1_STATIC|静态
EV-14|设计源迁移|docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包|目录枚举|C1B/C2B/C3 包均在|E1_STATIC|静态
'@
"written $OutDir"
