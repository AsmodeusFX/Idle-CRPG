param([string]$OutDir="d:/Project-Asmo/Idle-CRPG/artifacts/qa/s15-c1-c3-recon-20261027-100854")
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
W "recon_facts.csv" @'
FactKey|Value|Source
star_nodes|39（含10退休/4旧族映射/17+3 C1B新节点）|config/star/star.csv
star_edges|38 边/39 节点/1 根(s_root)|config/star/star_layout.csv
star_rank_gate|引擎只有"点亮(Level>0)"；无@Rank阈值|Scripts/Core/StarConfiguration.cs:79
rhythm_rows|226（act1=47 act2=57 act3=67 act4=55）|config/chapters/act*/dungeon/floor_rhythm.csv
spawn_rule|count=1-5 xp_base=2 level=1|config/monster/spawn_rule.csv
hero_level_exp|L1->2=18 每级+18；Lv5累计=180 单英雄|config/hero/herolevel.csv
hero_base|warrior atk22/hp180；ranger19/105；mage30/85；priest17/130|config/hero/heroes.csv
mob_hp|a1 怪 hp35/55 range0.7-0.85|config/monster/act_monsters.csv
act1_castle_gate|A01=2 A02=5 A03=6 A04=7 A05=8 A06=10 A07=11 A08=12|config/chapters/act1/dungeon/dungeon.csv
castle_gate_rule|MonsterLevelMax>=poi_profile.challenge(空则LevelMin)且CastleUnlocked|Scripts/Core/KpProgression.cs:50
lmax_gate_rule|KP>=LmaxCost且全员AR/DR>=(Lmax+1)*10|Scripts/Core/KpProgression.cs:29
equipment_attrs|atk=3槽 hp=3槽；无AR/DR/抗性|config/item/equipment.csv
consumables|scroll=5 potion=4|config/item/scroll.csv; config/item/potion.csv
drop_table|31 行（按怪级）|config/item/drop_table.csv
boss_phase|BossPhases.cs + BossPhaseRuntime.cs 存在|Scripts/Core/BossPhaseRuntime.cs
'@
W "recon_runtime_chain.csv" @'
Chain|ReadPath|WritePath|FormulaOrRule|Evidence|Risk
公共XP入池|GameSession.PublicExperience|同字段|GainExperience(amount*XpGainMultiplier 含星图G01)|tests WP2_PASS|无等级差衰减
逐英雄升级|UpgradeHero(instanceId)|PublicExperience/hero.Level|每调用+1级 消耗 config/hero/herolevel.csv exp|tests WP2_PASS|4英雄到Lv5需16次调用
AR/DR|PrototypeProgression.AttackRating|Actor.AttackRatingBonus(星图R01/R02)|10+(Level-1)*10+Bonus|tests WP2_PASS|装备不提供AR/DR
KP入账|GainKillKp(每怪1 KP 收据去重)|Kp/KpEarned|Progression.KillKp|tests WP3_PASS|复刷同一出生实例不重复
Lmax购买|BuyKpUpgrade|MonsterLevelMax|+1级 需KP>=LmaxCost 且全员AR/DR>=(Lmax+1)*10|tests WP3_PASS|大门槛链
城堡准入|CanChallengeCastle|--|MonsterLevelMax>=challenge 且 CastleUnlocked|tests WP3_RECEIPT_CASTLE_PASS|A02-C01 需 Lmax5
刷怪|Reveal|room.Monsters|spawn_rule 1-5 + floor_rhythm count±1 + pool_bias×3|tests --c2a-checks|每房3-5只
TTK|SettleDamage|target.Hp|warrior22/mage30 对 hp35-55 约2击×0.85s|静态推算 + tests C1A|技能未放完即清房
装备|EquipmentValue|Actor.Attack/MaxHp(差分)|quality×base+per*level|tests WP5_PASS|仅ATK/HP
星图|Stars.StarValue/ApplyStarTotals|Actor(差分)/派生乘率|等级×每级值|tests --c1b-checks|@Rank缺失
存档|SessionSnapshot|--|Stars/EliteKillReceipts/MonsterOrigin|tests INT_01_08_PASS|Elite不随检查点保留
Boss阶段|BossPhaseRuntime|--|HP/时间阈值触发阶段随从与Buff|未取运行证据|NOT_VERIFIED
'@
W "recon_user_observations.csv" @'
ObsId|UserStatement|VerifiedCause|Evidence|Status|FixState
UX-01|UI整体完全不合格|当前UI为原型控件布局 无有效UI设计稿|历史截图 artifacts/qa/first-batch-ui-*|USER_OBSERVED|延期统一UI设计 不实施
PROG-01|第一章城堡要求怪物等级5 必须低级地牢重复刷|A02-C01 门槛 MonsterLevelMax>=5；需4笔KP升档 且到第4→5笔时全员AR/DR>=50(≈Lv5)；4英雄Lv5共需720公共XP≈360击杀(2XP/只)|config+静态推算|USER_OBSERVED|只做因果对账
PROG-02|四英雄逐次升级操作负担|UpgradeHero 每次仅+1级；到Lv5需16次点击 到Lv25需96次|HeroExperience.cs UpgradeHero|USER_OBSERVED|留待可用性裁决
COMBAT-01|房间怪太少太弱 技能没放完就清房|spawn_rule 1-5(常态3-5) + 节奏±1；怪HP35-55 vs 英雄攻22-30 约2击|tests --c2a-checks 固定种子样本|USER_OBSERVED|留待数值/怪群调优
EQ-UX-01|六部位只有ATK/HP 无法改变AR/DR准入|装备3atk+3hp；AR/DR仅由等级与星图产出|config/item/equipment.csv|USER_OBSERVED|留待产品取舍(单主属性为[C])
STAR-UX-01|星图连线混乱不可读|39节点38边 交叉多 无@Rank标注 根为s_root|config/star/star_layout.csv + 截图|USER_OBSERVED|延期统一UI改造
'@
W "recon_c3_assumptions.csv" @'
Item|Value|Source|Status
周期|10分钟/45分钟一次决策|C3报告 §1.3|E2
时长|600 游戏分钟|C3报告 §1.3|E2
Seed|3301/3302(P4) 其余1枚|C3报告 §1.3|E2
队伍|P4/P2R2P/P4SAME/P1|C3报告 §1.3|E2
每周期升级|1 名英雄 1 级|SCRIPT_POLICY_NOT_PRODUCT|IMPLEMENTED_DIFFERENT
每周期星图|仅根节点|同上|IMPLEMENTED_DIFFERENT
每周期Lmax|同周期内购买|同上|IMPLEMENTED_DIFFERENT
未建模|天赋加点/装备穿戴/卷轴/药剂/多笔升级/连续点击|C3报告 §1.4|NOT_MODELED
XP余额|P4 10min 剩103448 平均Lv15-16|C3报告|E2
章节|P4 480min首进第二章 600min未到第四章|C3报告|E2
击杀|P4 600min 11605次|C3报告|E2
静态代理|11189 四章主线静态击杀|C3报告 §重要红项|DESIGN_PROXY
上限|生产 MaxHeroLevel/MaxMonsterLevel=10000 未硬封25/31|config|MATCHED_EVIDENCED
'@
W "recon_evidence_index.csv" @'
EvidenceId|Path|Command|ExitCode|Reproducible|Note
E-01|artifacts/qa/s15-c1-c3-recon-20261027-100854/recon_facts.csv|powershell tools/S15-Recon-Matrix.ps1|0|YES|静态事实快照
E-02|artifacts/qa/s15-c1-c3-recon-20261027-100854/recon_design_id_crosswalk.csv|powershell tools/S15-Recon-Crosswalk.ps1|0|YES|264 行逐ID
E-03|artifacts/qa/s15-c1-c3-recon-20261027-100854/recon_master_matrix.csv|powershell tools/S15-Recon-Matrix.ps1|0|YES|41 行主状态
E-04|artifacts/qa/s15-c1-c3-recon-20261027-100854/recon_user_observations.csv|--|--|YES|用户观察与因果
E-05|artifacts/qa/s15-c1-c3-recon-20261027-100854/recon_c3_assumptions.csv|--|--|YES|C3 策略假设
E-06|artifacts/qa/s15-c1-c3-recon-20261027-100854/recon_runtime_chain.csv|--|--|YES|运行消费链
E-07|tests/IdleCrpg.Checks.csproj|dotnet run --project tests/IdleCrpg.Checks.csproj|0|YES|全量 Core
E-08|tests/IdleCrpg.Checks.csproj|--c1a-checks|0|YES|16技能+4T
E-09|tests/IdleCrpg.Checks.csproj|--c1a2-checks|0|YES|98节点 8构筑
E-10|tests/IdleCrpg.Checks.csproj|--c1b-checks|0|YES|星图B1/B2
E-11|tests/IdleCrpg.Checks.csproj|--c1b3-checks|0|YES|57/55 路线
E-12|tests/IdleCrpg.Checks.csproj|--c2a-checks|0|YES|393键/226层
E-13|artifacts/qa/first-batch-ui-20261026-112631-*/star-b3-rejections.png|Godot --first-batch-smoke|0|YES|真实窗口
E-14|docs/reviews/2026-10-25_p2_4_first_batch.md|--|--|YES|模块唯一Review §20-§27
'@
"written to $OutDir"
Get-ChildItem $OutDir | ForEach-Object { $_.Name + ' ' + $_.Length }
