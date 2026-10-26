using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using IdleCrpg.Core;

/// <summary>一条命令驱动真实生产命令链；地图终点以明示QA前置加速，不声称完成四章长跑或10小时平衡。</summary>
public static class IntegrationClosureChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("INT_CLOSURE: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        // 单卷单药机制切片：生产已扩到 5 卷轴/4 药剂，随机掉落会破坏本用例的同种收支守恒断言。
        TestPlayerActions.SingleConsumableFixture(c);
        c.Scrolls[0]=c.Scrolls[0] with {DropChance=1};c.Potions[0]=c.Potions[0] with {DropChance=1};
        var output=Path.Combine(root,"artifacts/qa/p2-4-v16/integration");Directory.CreateDirectory(output);
        var recorder=new SimulationRecorder();var all=new List<AssetEvent>();
        var report=new List<string>{"INT-01~08 | seed=1660 | QA balance/drop/terminal prerequisites explicitly enabled | no formal balance claim"};
        var game=GameSession.CreateNewWorld(c,1660,new[]{"warrior","warrior","mage"});
        var initial=game.CaptureProgress();initial.Kp=50;initial.KpEarned=50;game=GameSession.Restore(c,initial);
        void Attach(GameSession current){current.AssetRecorded+=entry=>{recorder.Asset(entry);all.Add(entry);};}
        Attach(game);game.SetGmBattle(new(Invincible:true));
        Need(game.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==3&&game.PartyCapacity==4,"INT01实例/四席");
        report.Add("INT-01 PASS: duplicate class Run; formal encode/decode; fixed seats and IDs");
        game.GrantGmResources(gold:1000,experience:10000);
        for(int tick=0;tick<20000&&game.CurrentRoom==0;tick++)game.Tick(.05f);
        while(game.CombatEntryRemaining>0)game.Tick(.05f);
        var room=game.Floor.Rooms[game.CurrentRoom];var target=room.Monsters.First(m=>m.Alive);target.Hp=1;
        long xp=game.PublicExperience,kp=game.Kp;game.QaResolveHit(target);
        Need(game.PublicExperience>xp&&game.Kp>kp,"INT02击杀没有即时XP/KP");
        var hero=game.Heroes[0];int otherLevel=game.Heroes[1].Level;
        Need(game.UpgradeHero(hero.HeroInstanceId,out _)&&game.Heroes[1].Level==otherLevel,"INT02指定升级串实例");
        report.Add("INT-02 PASS: real damage kill/rewards; targeted upgrade; event source/sink conservation");
        foreach(var h in game.Heroes.Where(h=>h.Level<2))Need(game.UpgradeHero(h.HeroInstanceId,out _),"INT03全员评级升级前置");
        var snapshots=game.Floor.Rooms.Where(r=>r.Generated).Select(r=>(r.Id,r.SnapshotLevel)).ToArray();
        Need(game.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out _)&&game.MonsterLevelMax==2,"INT03 Lmax合法购买");
        Need(snapshots.All(old=>game.Floor.Rooms[old.Id].SnapshotLevel==old.SnapshotLevel)&&!game.BuyKpUpgrade(KpPurchase.MonsterLevelMinimum,out _),"INT03快照/禁购Lmin");
        report.Add("INT-03 PASS: generated room snapshots immutable; legal Lmax transaction; Lmin remains rejected; detailed revisit/castle gates in FirstBatchChecks");
        Need(game.BuyStar("s_root",out _)&&game.BuyStar("s_trait_warrior",out _),"INT04星图资格");
        Need(game.LearnTalent(hero.HeroInstanceId,"barb_taunt",out _),"INT04 S2学习");
        Need(game.UpgradeHero(hero.HeroInstanceId,out _)&&game.LearnTalent(hero.HeroInstanceId,"warrior_trait",out _),"INT04 T学习");
        Need(!game.Heroes[1].Talents.ContainsKey("warrior_trait")&&!game.Heroes[1].SkillIds.Contains("barb_taunt")&&!game.BuyStar("s_skill_1",out _),"INT04新旧双权威/实例串改");
        report.Add("INT-04 PASS: Run gold/class qualification; per-instance S2/T; no retired-slot purchase; real effect evidence in FixedSkillClosureChecks");
        foreach(var spec in c.EquipmentBases)game.QaOfferEquipment(hero.HeroInstanceId,spec.Id,10,"rare");
        int ready=game.BatchEquipReadyCount();Need(ready>=6&&game.EquipAllReplaceable()==ready,"INT05批穿未执行全部N项");
        Need(c.EquipmentBases.All(spec=>game.WornEquipment(hero,spec.Slot)!=null),"INT05六部位不齐");
        report.Add("INT-05 PASS: six-slot batch, all N candidates consumed; HP/death/revival/unequip/cross-attribute boundaries in Wp5EquipmentChecks");
        string scroll=c.Scrolls[0].Id;game.QaGrantConsumable(scroll);
        var live=room.Monsters.First(m=>m.Alive);live.Hp=1;game.Heroes[0].Position=live.Position;
        int stock=game.ScrollStock[scroll];Need(game.UseScrollOnNearest(scroll,out _),"INT06卷轴失败");
        // 100%同种卷轴掉落：成功消费一张且击杀又入一张，最终库存守恒，不得覆盖新收入。
        Need(game.ScrollStock[scroll]==stock,"INT06卷轴击杀覆盖同种掉落或重复扣减");
        int slot=game.PotionSlots.ToList().FindIndex(s=>s.ItemId!=""&&!s.Active);Need(slot>=0&&game.UsePotion(slot,out _),"INT06药剂未占槽激活");
        double remaining=game.PotionSlots[slot].RemainingSeconds;
        Need(!game.UsePotion(slot,out _)&&!game.DiscardPotion(slot,out _),"INT06生效药剂重用/丢弃");
        report.Add("INT-06 PASS: scroll lethal consumption+same-kind drop conservation; potion state transition; no empty-effect charge");
        long expectedGold=all.Where(e=>e.AssetType=="gold").Sum(e=>e.Delta);
        long expectedXp=all.Where(e=>e.AssetType=="xp").Sum(e=>e.Delta);
        long expectedKp=50+all.Where(e=>e.AssetType=="kp").Sum(e=>e.Delta);
        Need(game.Gold==expectedGold&&game.PublicExperience==expectedXp&&game.Kp==expectedKp,"三币逐笔总账不守恒");
        Need(all.All(e=>e.AssetType is not ("gold" or "xp" or "kp")||long.Parse(e.After)-long.Parse(e.Before)==e.Delta),"事件行不守恒");
        var store=new SaveStore(Path.Combine(output,"progress.json"));store.Save(game);
        game=GameSession.Restore(c,store.Load(c)!.Run!);Attach(game);
        Need(game.PotionSlots[slot].RemainingSeconds==remaining,"正式重读推进药剂时间");
        Need(game.UpgradeHero(game.Heroes[0].HeroInstanceId,out _),"重读后实际消费命令失败");
        game.World!.Progress.Cleared.Add(c.ChapterMaps[3].MainClearPoi); // 明示终点前置，不声称实战逐章完成。
        Need(game.Rebirth(out AccountState account,out _)&&account.AchievementPoints==0,"INT07重生/擅自AP");
        var next=GameSession.StartRun(c,1661,new[]{"ranger"},account);Attach(next);
        Need(next.PublicExperience==0&&next.Kp==0&&next.Gold==0&&next.ScrollStock.Count==0&&next.PotionSlots.All(s=>s.ItemId=="")&&!next.Heroes.Any(h=>h.Talents.ContainsKey("warrior_trait")),"INT07跨Run串资产");
        Need(!next.AbandonRun().AchievementFacts.Except(account.AchievementFacts).Any(),"INT07放弃伪造重生事实");
        report.Add("INT-07 PASS: explicit QA chapter4 terminal -> real Rebirth -> new Run; Account facts retained, AP zero, Run assets reset; legacy quarantine in dedicated migration checks");
        var offline=next.CaptureProgress();offline.SavedAtUnix=1_000_000;
        var settlement=GameSession.SettleOffline(c,offline,1_003_600);var duplicate=GameSession.SettleOffline(c,offline,1_003_600);
        Need(settlement.Seconds==3600&&duplicate.Seconds==0&&offline.Heroes[0].Level==1,"INT08离线幂等/自动升级");
        recorder.Asset(new(offline.RunId+":offline:1000000:xp",offline.RunId,0,"offline_settlement","receive","","xp","",settlement.Experience,"0",offline.PublicExperience.ToString(),"1000000..1003600"));
        recorder.Asset(new(offline.RunId+":offline:1000000:kp",offline.RunId,0,"offline_settlement","receive","","kp","",settlement.Kp,"0",offline.Kp.ToString(),"1000000..1003600"));
        recorder.Asset(new(offline.RunId+":offline:1000000:gold",offline.RunId,0,"offline_settlement","receive","","gold","",settlement.Gold,"0",offline.Gold.ToString(),"1000000..1003600"));
        var loaded=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=offline})).Run!);
        Need(loaded.PublicExperience==settlement.Experience&&loaded.Kp==settlement.Kp&&loaded.Gold==settlement.Gold,"INT08结算重读丢收益");
        store.Reset();Need(store.Load(c)?.Run==null&&store.Legacy.Skills.Count==0,"清全部未清隔离/本轮");
        report.Add("INT-08 PASS: explicit offline interval, duplicate zero, full save loop; no offline Tick or auto spend; live background uses ordinary Tick");
        recorder.WriteAll(output);File.WriteAllLines(Path.Combine(output,"int-results.txt"),report);
        Need(all.Select(e=>e.EventId).Distinct().Count()==all.Count,"事件ID跨读取重复");
        Console.WriteLine("INT_01_08_PASS: production command chain, fixed skills, typed asset ledger, three-currency conservation, same-kind scroll-drop atomicity, explicit terminal fixture, Run reset and offline idempotence; output="+output);
    }
}
