#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Steam D2 C3 deterministic design-side static stress budgets.
NOT a simulation of the C# runtime or Godot. H values only, no production writes.
Inputs: immutable local snapshots under refs/. Outputs: same dir as this script.
"""
import csv, os, json, math, hashlib
from collections import defaultdict
from pathlib import Path
ROOT=Path(__file__).resolve().parent
REF=ROOT/'refs'
def records(path):
    with open(path,encoding='utf-8-sig',newline='') as f:
        return list(csv.DictReader(f))
def write_csv(name, header, rows):
    path=ROOT/name
    with open(path,'w',encoding='utf-8-sig',newline='') as f:
        wr=csv.DictWriter(f,fieldnames=header)
        wr.writeheader()
        wr.writerows(rows)
    return path
def rnd(n): return round(n,3)

old = {int(x['Chapter']):x for x in records(REF/'S13_四章统一输入表_H-V1.4.csv')}
corrected = {int(x['Chapter']):x for x in records(REF/'C2_C3_主线与可选内容预算纠偏_关键红项.csv')}
sale_rows = records(REF/'S11_全额与80pct压力账本_H-EQ1.csv')
sales = {int(x['Chapter']):float(x['AvgSaleGold']) for x in sale_rows if x['Party']=='四职业各一' and x['Scenario']=='100%击杀但无额外首通装备'}
assert len(old)==len(corrected)==len(sales)==4
buy = records(REF/'C1B_两阵容替代购买合法路线_条件性.csv')
by_party=defaultdict(list)
for r in buy:
    by_party[r['Scenario']].append(r)
assert sorted(len(v) for v in by_party.values())==[55,57]
assert sum(float(r['PriceGold_H']) for r in buy)==21650+20750
for party, purchases in by_party.items():
    assert [int(p['PurchaseSequence']) for p in purchases]==list(range(1,len(purchases)+1))
    assert all(p['PrereqPass']=='YES' and p['RuntimeValidity']=='NOT_TESTED' for p in purchases)
    assert not ({'G03','S02','S03','P03','A02','A03','G02','C-NEC','F03'} & {p['NodeId'] for p in purchases})

updated=[]
for ch in range(1,5):
    o,c=old[ch],corrected[ch]
    old_kills=int(o['P0PlannedLegalKills_Historical'])
    new_kills=int(c['CorrectedMainlineStaticBudget'])
    assert int(c['S11OldAllContentBudget'])==old_kills
    assert old_kills-new_kills==int(c['OvercountOptionalKills'])
    ratio=new_kills/old_kills
    xp=float(o['XPIncomeAt100_H'])*ratio
    kill_gold=float(o['EquipmentGoldKillAt100_H'])*ratio
    sale100=sales[ch]*ratio
    updated.append(dict(
      Chapter=ch,Minutes_P=int(o['TimeMin_P']),OriginalAllDungeonPlusMainCastleKills_H=old_kills,
      MainDungeonKills_H=int(c['MainDungeonStaticKills']),OptionalDungeonKills_EXCLUDED=int(c['OptionalDungeonStaticKills']),
      CastleKillsHistoricalAssumption_H=int(c['S11MainCastleStaticKillAssumption']),
      MainlineStaticKillsCorrected_H=new_kills,OptionalOvercountRemoved=int(c['OvercountOptionalKills']),
      XPPerKillProxy_H=rnd(float(o['XPIncomeAt100_H'])/old_kills),
      XPIncomeAt100Main_H=rnd(xp),HeroXPUpgradeSpend_H=int(o['XPUpgradeNeed_H']),
      KPIncomeAt100Main_H=new_kills,KPLmaxSpend_H=int(o['KPLmaxNeed_H']),
      KillGoldPerKill_H=rnd(float(o['EquipmentGoldKillAt100_H'])/old_kills),
      KillGoldAt100Main_H=rnd(kill_gold),
      S11SaleMeanNoBonusUncorrected_H=rnd(sales[ch]),
      SaleGoldAt100LinearProxy_H=rnd(sale100),
      StaticKillPerActiveMinIfTimingMet_H=rnd(new_kills/int(o['TimeMin_P'])),
      LmaxEndCandidate_H=int(o['LmaxEnd_H']),PartyEachHeroMinLevel_H=int(o['PartyEachMinHeroLevel_H']),
      GoldModelWarning='SaleGold是S11 no-first-clear 120种子均值乘主线比例的线性代理；非重新蒙特卡洛',
      XPModelWarning='XP按S13每章单位击杀期望同比缩放；非31实际怪级权重或星图XP时序'
    ))
cols=list(updated[0].keys())
write_csv('C3_四章必经静态内容与数值输入修正_H.csv',cols,updated)

scenario_rows=[]; thresholds=[]; summary=[]
for party,purchases in sorted(by_party.items()):
    per_ch=defaultdict(list)
    for p in purchases: per_ch[int(p['Chapter'])].append(p)
    assert sum(len(v) for v in per_ch.values()) in (55,57)
    for chapter in range(1,5):
        spend=sum(float(p['PriceGold_H']) for p in per_ch[chapter]); n=len(per_ch[chapter]);
        assert abs(spend - sum(float(p['PriceGold_H']) for p in purchases if int(p['Chapter'])==chapter))<1e-8
    cumxpneed=cumkpneed=cumgoldneed=cumxp100=cumkp100=cumkillgold=cumsale=0
    for row in updated:
        ch=row['Chapter']; cumxp100+=row['XPIncomeAt100Main_H']; cumkp100+=row['KPIncomeAt100Main_H'];cumxpneed+=row['HeroXPUpgradeSpend_H']; cumkpneed+=row['KPLmaxSpend_H']
        cumgoldneed+=sum(float(p['PriceGold_H']) for p in per_ch[ch]); cumkillgold+=row['KillGoldAt100Main_H'];cumsale+=row['SaleGoldAt100LinearProxy_H']
        thresholds.append(dict(ScenarioParty=party,Chapter=ch,CumXPNeed_H=cumxpneed,CumXPIncomeIf100_H=rnd(cumxp100),XP_MinFactor_H=rnd(cumxpneed/cumxp100),CumKPSpend_H=cumkpneed,CumKPIncomeIf100_H=cumkp100,KP_MinFactor_H=rnd(cumkpneed/cumkp100),CumStarSpend_H=rnd(cumgoldneed),CumGoldKillOnlyIf100_H=rnd(cumkillgold),GoldKillOnly_MinFactor_H=rnd(cumgoldneed/cumkillgold),CumGoldWithLinearSaleIf100_H=rnd(cumkillgold+cumsale),GoldKillPlusSaleLinear_MinFactor_H=rnd(cumgoldneed/(cumkillgold+cumsale)),GoldRule='星图Rank全真实生效仅条件成立；收入未计星图增益/离线/耗材；价格未经批准'))
    for factor in [1.,.9,.8,.7]:
      for sale_mode in ('KILL_GOLD_ONLY_LOWER_BOUND','KILL_PLUS_S11_LINEAR_SALE_PROXY'):
        cxp=ckp=cgold=spentxp=spentkp=spentgold=0.
        min_xp=1e100; min_gold=1e100; min_kp=1e100
        for row in updated:
          ch=row['Chapter']; kills=row['MainlineStaticKillsCorrected_H']*factor
          incxp=row['XPIncomeAt100Main_H']*factor
          inckp=kills
          salegold=row['SaleGoldAt100LinearProxy_H']*factor if sale_mode!='KILL_GOLD_ONLY_LOWER_BOUND' else 0.
          incgold=row['KillGoldAt100Main_H']*factor+salegold
          starspend=sum(float(p['PriceGold_H']) for p in per_ch[ch]); np=len(per_ch[ch])
          cxp+=incxp;ckp+=inckp;cgold+=incgold
          spentxp+=row['HeroXPUpgradeSpend_H'];spentkp+=row['KPLmaxSpend_H'];spentgold+=starspend
          xpgap=cxp-spentxp;kpgap=ckp-spentkp;goldgap=cgold-spentgold
          min_xp=min(min_xp,xpgap);min_kp=min(min_kp,kpgap);min_gold=min(min_gold,goldgap)
          scenario_rows.append(dict(Party=party,EffectiveMainlineKillFactor_H=factor,SaleModel_H=sale_mode,Chapter=ch,MinutesCumulative_P=sum(x['Minutes_P'] for x in updated if x['Chapter']<=ch),MainlineKillsInChapter_H=rnd(kills),CumulativeXPIncomeProxy_H=rnd(cxp),CumulativeXPRequired_H=rnd(spentxp),XP_CumulativeMargin_H=rnd(xpgap),XPCanMeetChapterTarget_Proxy=xpgap>=-0.001,CumulativeKPIncome_H=rnd(ckp),CumulativeKPRequired_H=rnd(spentkp),KP_CumulativeMargin_H=rnd(kpgap),KPBudgetSufficient_Proxy=kpgap>=-0.001,CumulativeGoldIncome_H=rnd(cgold),CumulativeStarSpend_H=rnd(spentgold),Gold_CumulativeMargin_H=rnd(goldgap),GoldSufficientIfAllRanksValid_Proxy=goldgap>=-0.001,ChapterStarPurchases_Conditional=np,ChapterStarSpend_H=rnd(starspend),Proof='独立静态策划账本/非Godot；全章末结算偏乐观，不能证明购买时间点及真实进度'))
        summary.append(dict(Party=party,Factor_H=factor,SaleModel_H=sale_mode,TotalPurchases_Conditional=len(purchases),TotalStarGold_H=rnd(spentgold),FinalXPProxyMargin_H=rnd(cxp-spentxp),MinChapterXPProxyMargin_H=rnd(min_xp),FinalKPProxyMargin_H=rnd(ckp-spentkp),FinalGoldProxyMargin_H=rnd(cgold-spentgold),MinChapterGoldProxyMargin_H=rnd(min_gold),GlobalChapterEndBudgetPASS_Proxy=(min_xp>=-.001 and min_kp>=-.001 and min_gold>=-.001),Actual10HourValidated='NO'))
assert abs(sum(r['MainlineStaticKillsCorrected_H'] for r in updated)-11189)<1e-8
assert abs(sum(r['OptionalOvercountRemoved'] for r in updated)-1291)<1e-8
assert len(scenario_rows)==64 and len(thresholds)==8 and len(summary)==16
write_csv('C3_四章双阵容跨账压力_静态代理_H.csv',list(scenario_rows[0]),scenario_rows)
write_csv('C3_三资源分章最低覆盖阈值_静态代理_H.csv',list(thresholds[0]),thresholds)
write_csv('C3_八类压力情景双阵容总览_H.csv',list(summary[0]),summary)

level=records(REF/'S04-S05_31档联立候选数值_H-B1.csv')
assert len(level)==31 and [int(r['怪级g']) for r in level]==list(range(1,32))
level_rows=[]
for r in level:
    level_rows.append({'MonsterLevel':int(r['怪级g']), 'ChapterLabel_H':r['章节'],
        'ARRequirement_H':r['本档评级要求AR'],'DRRequirement_H':r['本档评级要求DR'],
        'MinimumFourHeroLevelProxy_H':r['四职自然最低英雄Lv'],
        'XPPerKillAtActualMonsterLevel_H':r['该档怪击杀公共XP'],
        'UniformL1ToLmaxXPMean_H':r['[1,g]均匀抽样期望XP/击杀'],
        'KPCostToUnlockLmaxThisLevel_H':r['购买该档KP费用'],
        'CumLmaxKPCost_H':r['至本档累计KP费用'],
        'ProductionValidation':'H_NOT_AUTHORIZED | current WP3 H lmax_cost=10; require config consumer migration',
        'MonsterActualLevelAuthority':'Run [Lmin,Lmax], Lmin currently 1 and disabled for player'})
write_csv('C3_31怪级联合参数待校准_H-B1.csv',list(level_rows[0]),level_rows)

# Research-only; no roleplay of actual engine events.
result={
 'model_kind':'DESIGN_SIDE_DETERMINISTIC_STATIC_SURROGATE_NOT_ENGINE',
 'validations':{'corrected_mainline_kills':11189,'optional_kills_excluded':1291,'level_rows':31,'star_purchase_rows':len(buy),'chapter_cases':len(scenario_rows),'summaries':len(summary),'threshold_rows':len(thresholds)},
 'baseline_flags':{'offline_included':False,'star_effects_applied':False,'scroll_potion_combat_effects_applied':False,'castle_first_clear_bonus_items':False,'optional_dungeons':False,'actual_monster_level_histogram':False,'time_pass':False,'gold_sales_earnings_guaranteed':False},
 'sample_cases':[r for r in summary if r['Party'].startswith('甲') and r['Factor_H'] in [1.0,0.8]],
 'source_hashes':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(REF.iterdir()) if p.is_file()},
}
(ROOT/'C3_模型复算输出.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps(result['validations'],ensure_ascii=False))
for x in result['sample_cases']: print(x)
