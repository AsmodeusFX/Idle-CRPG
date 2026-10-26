using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IdleCrpg.Core;

public static class FirstBatchChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("FIRST_BATCH: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));string hero=c.Heroes.First(h=>c.HeroUnlockKills[h.Id]==0).Id;
        foreach(int count in new[]{1,3,4})
        {
            var game=GameSession.CreateNewWorld(c,42,Enumerable.Repeat(hero,count));
            Check(game.PartyCapacity==4&&game.Heroes.Count==count&&game.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==count,"同职实例/四席");
            game.Heroes[0].Level=3;game.Account.AchievementFacts.Add("qa-fact");
            string text=SaveStore.Encode(new SaveDocument {Session=game.CaptureProgress()});
            using var json=JsonDocument.Parse(text);
            Check(json.RootElement.TryGetProperty("Account",out _)&&json.RootElement.TryGetProperty("Run",out _)&&!json.RootElement.TryGetProperty("Session",out _),"单文件双层权威");
            var doc=SaveStore.Decode(c,text);var restored=GameSession.Restore(c,doc.Session!);
            Check(restored.Heroes.Select(h=>h.HeroInstanceId).SequenceEqual(game.Heroes.Select(h=>h.HeroInstanceId))&&restored.Heroes[0].Level==3&&restored.Heroes.Skip(1).All(h=>h.Level==1),"实例、等级重载串号");
            Check(restored.Account.AchievementFacts.SequenceEqual(new[]{"qa-fact"})&&!restored.RequestFormation(new[]{hero},out _)&&!restored.DeployHero(hero,out _),"锁队/账号重载");
            Check(!restored.TryRebirth(out _)&&restored.AbandonRun().AchievementFacts.Contains("qa-fact"),"放弃不清账号，重生禁用");
            for(int version=1;version<=4;version++)
            {
                var legacy=game.CaptureProgress();legacy.RunVersion=0;legacy.RunId="";foreach(var h in legacy.Heroes)h.HeroInstanceId="";
                var migrated=SaveStore.Decode(c,SaveStore.Encode(new SaveDocument {Version=version,Session=legacy}));
                Check(migrated.Version==5&&migrated.Session!.Heroes[0].Level==3,"旧外层版本等级不降");
            }
        }
        bool rejected=false;try{GameSession.CreateNewWorld(c,1,Array.Empty<string>());}catch(ArgumentException){rejected=true;}Check(rejected,"零人未拒绝");
        var directory=QaOutput.NewDirectory(root,"wp1-save-checks");var path=Path.Combine(directory,"progress.json");var store=new SaveStore(path);
        var one=GameSession.CreateNewWorld(c,17,new[]{hero});store.Save(one);store.Save(one);
        File.WriteAllText(path,"invalid");Check(store.Load(c)?.Session!=null&&store.LastWarning!=null,"损坏主档未恢复备份");
        File.WriteAllText(path+".bak","invalid");string original=File.ReadAllText(path);rejected=false;
        try{store.Load(c);}catch(MigrationRequiredException){rejected=true;}Check(rejected&&File.ReadAllText(path)==original&&File.ReadAllText(path+".bak")=="invalid","错误静默清档");
        Console.WriteLine("WP1_PASS: 1/3/4 duplicate classes, stable IDs, lock, Account/Run, V1-V4, account-preserving abandonment, rebirth disabled, backup recovery and no silent reset");
        var shared=GameSession.CreateNewWorld(c,99,Enumerable.Repeat(hero,4));shared.GrantGmResources(experience:10000);
        Check(shared.PublicExperience==10000&&shared.Heroes.All(h=>h.Level==1&&h.Experience==0),"公共入池自动升级或按人数复制");
        string first=shared.Heroes[0].HeroInstanceId;int cost=shared.ExperienceRequired(shared.Heroes[0]);
        Check(shared.UpgradeHero(first,out _)&&shared.Heroes[0].Level==2&&shared.Heroes.Skip(1).All(h=>h.Level==1)&&shared.PublicExperience==10000-cost,"指定升级恰一次");
        var poolCopy=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=shared.CaptureProgress()})).Session!);
        Check(poolCopy.PublicExperience==shared.PublicExperience&&poolCopy.Heroes[0].Level==2&&poolCopy.AttackRating(poolCopy.Heroes[0])==shared.AttackRating(shared.Heroes[0]),"升级读档对账");
        var input=new DamageInput("qa",100,10000,1,DamageType.Physical,false,false,new(),new(),new System.Collections.Generic.HashSet<string>(),AttackRating:20,DefenseRating:10);
        var damage=DamagePipeline.Resolve(input,c.CombatRules,new(123));
        Check(damage.FinalDamage==150&&DamagePipeline.Resolve(input with {Armor=0},c.CombatRules,new(123)).FinalDamage==150,"AR增伤或Armor双活");
        Check(DamagePipeline.Resolve(input with {AttackRating=10,DefenseRating=20},c.CombatRules,new(123)).FinalDamage==40,"DR惩罚");
        // QA-2A：1/2/4人击杀同一只怪，入池XP等于该怪原始奖励，不按人数复制、不按等级差衰减、不自动升级。
        int xpParties=0;
        foreach(int size in new[]{1,2,4})
        {
            var solo=GameSession.CreateWorld(c,300+size,Enumerable.Repeat(hero,size));
            for(int i=0;i<20000&&(solo.World!.Stage!=CampaignStage.Dungeon||solo.CurrentRoom==0);i++)solo.Tick(.05f);
            Check(solo.World!.Stage==CampaignStage.Dungeon&&solo.CurrentRoom!=0,$"{size}人组队未进入战斗房间");
            while(solo.CombatEntryRemaining>0)solo.Tick(.05f);
            var live=solo.Floor.Rooms[solo.CurrentRoom];var victimMonster=live.Monsters.FirstOrDefault(m=>m.Alive);
            if(victimMonster==null)continue;
            long poolBefore=solo.PublicExperience;int reward=victimMonster.RewardXp;
            solo.QaResolveHit(victimMonster);
            Check(solo.PublicExperience-poolBefore==reward,$"{size}人组队入池XP不等于怪物原始XP：{solo.PublicExperience-poolBefore}/{reward}");
            Check(solo.Heroes.All(h=>h.Level==1&&h.Experience==0),$"{size}人组队击杀后出现自动升级或私有经验");
            xpParties++;
        }
        Check(xpParties==3,$"QA-2A未覆盖1/2/4人组队：{xpParties}/3");
        Console.WriteLine("WP2_PASS: shared XP once, no automatic spend, targeted single level, reload, natural ratings, AR/DR multiplier and Armor retired");

        var funded=shared.CaptureProgress();funded.Kp=100;funded.KpEarned=100;funded.KpSpent=0;
        var kp=GameSession.Restore(c,funded);string unchanged=SaveStore.Encode(new(){Session=kp.Capture()});
        Check(!kp.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out string why)&&why.Contains("AR不足")&&SaveStore.Encode(new(){Session=kp.Capture()})==unchanged,"低AR拒绝不扣款");
        foreach(var h in kp.Heroes)h.Level=2;kp.Heroes[1].DefenseRatingBonus=-1;
        unchanged=SaveStore.Encode(new(){Session=kp.Capture()});
        Check(!kp.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out why)&&why.Contains("DR不足")&&SaveStore.Encode(new(){Session=kp.Capture()})==unchanged,"逐人DR拒绝无变化");
        kp.Heroes[1].DefenseRatingBonus=0;
        Check(kp.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out _)&&kp.MonsterLevelMax==2&&kp.Kp==90&&kp.KpSpent==10,"逐笔Lmax交易");
        Check(!kp.BuyKpUpgrade(KpPurchase.MonsterLevelMinimum,out _)&&!kp.BuyKpUpgrade((KpPurchase)999,out _)&&kp.MonsterLevelMin==1,"Lmin非法请求未封禁");
        var savedKp=SaveStore.Decode(c,SaveStore.Encode(new(){Session=kp.CaptureProgress()}));var reloadKp=GameSession.Restore(c,savedKp.Session!);
        Check(reloadKp.Kp==90&&reloadKp.MonsterLevelMax==2&&reloadKp.KpEarned-reloadKp.KpSpent==reloadKp.Kp,"KP存档账本");
        savedKp.Session!.MonsterLevelMin=2;rejected=false;try{GameSession.Restore(c,savedKp.Session);}catch(InvalidDataException){rejected=true;}Check(rejected,"篡改Lmin存档未拒绝");
        var poor=GameSession.CreateNewWorld(c,1,new[]{hero});Check(!poor.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out why)&&why.Contains("KP不足")&&poor.Kp==0&&poor.MonsterLevelMax==1,"KP不足交易");
        foreach(var h in reloadKp.Heroes){h.Hp=h.MaxHp=1000000;h.Attack=100000;}
        for(int i=0;i<30000&&reloadKp.Kills<10;i++)reloadKp.Tick(.05f);
        Check(reloadKp.Kills>0&&reloadKp.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters).All(m=>m.Level is >=1 and <=2),$"真实刷怪违反Run区间 kills={reloadKp.Kills} status={reloadKp.Status} world={reloadKp.World?.Stage} room={reloadKp.CurrentRoom} levels={string.Join(',',reloadKp.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters).Select(m=>m.Level))}");
        Check(reloadKp.KpEarned>100&&reloadKp.KpEarned-reloadKp.KpSpent==reloadKp.Kp,"真实KP击杀账本");
        Console.WriteLine("WP3_PASS: KP/AR/DR failure atomicity, per-click Lmax, Lmin UI/Core/save rejection, range spawning and reload ledger");
        var retry=new GameSession(c,785,new[]{hero});retry.Heroes[0].Hp=retry.Heroes[0].MaxHp=100000;retry.Heroes[0].Timer=999;
        for(int i=0;i<10000&&retry.CurrentRoom==0;i++)retry.Tick(.05f);
        while(retry.CombatEntryRemaining>0)retry.Tick(.05f);
        var liveRoom=retry.Floor.Rooms[retry.CurrentRoom];Check(liveRoom.Monsters.Count>1,"重复领取场景须为未清房");
        foreach(var m in liveRoom.Monsters){m.Timer=999;m.Hp=m.MaxHp=100000000;}
        var victim=liveRoom.Monsters[0];victim.Hp=1;var shooter=retry.Heroes[0];shooter.Attack=100000;shooter.Range=100;shooter.Position=victim.Position;shooter.Timer=0;
        var isolated=retry.Capture().SkillState;isolated.Actions.Clear();isolated.Flights.Clear();isolated.Pending.Clear();isolated.Buffs.Clear();
        int killCount=retry.Kills;retry.QaResolveHit(victim);
        Check(retry.Kills==killCount+1&&!liveRoom.Clear,"首次击杀现场");long earned=retry.KpEarned;
        var replay=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=retry.CaptureProgress()})).Session!);
        var same=replay.Floor.Rooms[replay.CurrentRoom].Monsters[0];foreach(var m in replay.Floor.Rooms[replay.CurrentRoom].Monsters)m.Timer=999;
        replay.Heroes[0].Position=same.Position;replay.Heroes[0].Timer=0;killCount=replay.Kills;
        replay.QaResolveHit(same);
        Check(replay.Kills==killCount+1&&replay.KpEarned==earned,$"房间重开重复领取同一出生KP kills={replay.Kills}/{killCount} earned={replay.KpEarned}/{earned}");
        // 出生地块可能是营地（营地不参与统治归属），城堡须从默认开放的统治区取，不能对出生地块求 areapart。
        var castleGame=GameSession.CreateWorld(c,42,new[]{hero});var castle=c.Dungeons.Single(d=>d.Id==c.AreaParts.First(p=>p.InitiallyUnlocked).CastleId);
        castleGame.World!.Progress.Cleared.UnionWith(c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]==castle.Id).Select(d=>d.Id));
        int required=castleGame.CastleRequiredMonsterLevel(castle.Id);castleGame.QaMonsterRange(required-1);
        Check(!castleGame.RequestCastleChallenge(castle.Id),"城堡差一级未拒绝");castleGame.QaMonsterRange(required);
        Check(castleGame.Kills==0&&castleGame.RequestCastleChallenge(castle.Id),"城堡满Lmax但无同档实杀应可安排");
        Console.WriteLine("WP3_RECEIPT_CASTLE_PASS: repeated checkpoint kill no duplicate KP; exact Lmax castle gate without kill proof");

        // R-03：升级必须带来可用天赋点，且"升级＋投点"读档后一致（WP4 依赖该来源）。
        var talentGame=GameSession.CreateNewWorld(c,61,new[]{hero});var talentHero=talentGame.Heroes[0];
        int pointsBefore=talentGame.TalentPoints(talentHero);
        talentGame.GrantGmResources(experience:100000);
        Check(talentGame.UpgradeHero(talentHero.HeroInstanceId,out _),"升级失败");
        int gained=c.Talents.Rule("talent_points_per_level");
        Check(gained>0&&talentGame.TalentPoints(talentHero)==pointsBefore+gained,$"升级未带来天赋点：{pointsBefore}->{talentGame.TalentPoints(talentHero)}");
        var firstNode=c.Talents.Nodes.Values.First(n=>n.Hero==talentHero.SpecId&&n.Row==0&&!n.ClassTrait);
        Check(talentGame.LearnTalent(talentHero.HeroInstanceId,firstNode.Id,out string learnWhy),$"首排天赋不可学习：{learnWhy}");
        int pointsAfterLearn=talentGame.TalentPoints(talentHero);
        var talentReload=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=talentGame.CaptureProgress()})).Session!);
        Check(talentReload.TalentPoints(talentReload.Heroes[0])==pointsAfterLearn&&talentReload.Heroes[0].Talents.GetValueOrDefault(firstNode.Id)==1,"升级＋投点读档后天赋点或投入不一致");
        Console.WriteLine("WP2_TALENT_PASS: level-up grants talent points and upgrade+investment survives save/reload");

        // R-04：同一遭遇重复触发不得重复入账；新的一次合法遭遇（同模板/同场地/同Seed）必须正常获得KP。
        c.Spawn=c.Spawn with {Min=2,Max=2};
        var revisit=GameSession.CreateWorld(c,321,new[]{hero});
        // 出生地块是营地，普通地牢须从默认开放统治区内取。
        var farmCastle=c.AreaParts.First(p=>p.InitiallyUnlocked).CastleId;
        var farm=c.Dungeons.First(d=>!d.Castle&&c.Rulers.TryGetValue(d.RegionId,out var owner)&&owner==farmCastle);
        for(int i=0;i<20000&&revisit.World!.Stage!=CampaignStage.Dungeon;i++)revisit.Tick(.05f);
        Check(revisit.World!.Stage==CampaignStage.Dungeon,"重访场景未进入地牢阶段");
        long KillFirstMonster(GameSession game,int visit)
        {
            game.BeginDungeon(farm,visit);
            Actor? target=null;
            for(int i=0;i<3000&&target==null;i++)
            {
                game.Tick(.05f);
                target=game.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters).FirstOrDefault(m=>m.Alive&&m.Hp>0);
            }
            if(target==null)throw new Exception("重访场景没有可击杀怪物");
            long before=game.Kp;game.QaResolveHit(target);
            Check(game.Kp==before+1,$"合法遭遇击杀没有获得KP：{before}->{game.Kp}");
            return game.Kp;
        }
        long firstVisitKp=KillFirstMonster(revisit,1);
        long secondVisitKp=KillFirstMonster(revisit,2);
        Check(secondVisitKp==firstVisitKp+1,$"同一地牢再次进入的同位怪物被收据误杀：{firstVisitKp}->{secondVisitKp}");
        Console.WriteLine("WP3_RECEIPT_REVISIT_PASS: same encounter deduplicated while a new legal revisit still earns KP");

        // C-LMAX-SNAPSHOT：升档不改写已实例化房间；此后第一次生成怪物的房间使用新区间。
        var snapshotGame=GameSession.CreateWorld(c,606,new[]{hero});
        for(int i=0;i<20000&&(snapshotGame.World!.Stage!=CampaignStage.Dungeon||snapshotGame.CurrentRoom==0);i++)snapshotGame.Tick(.05f);
        Check(snapshotGame.World!.Stage==CampaignStage.Dungeon&&snapshotGame.CurrentRoom!=0,"升档快照场景未进入战斗房间");
        while(snapshotGame.CombatEntryRemaining>0)snapshotGame.Tick(.05f);
        Check(snapshotGame.Floor.Rooms[snapshotGame.CurrentRoom].Monsters.Count>0,"升档快照场景房间没有怪物");
        snapshotGame.GrantGmResources(experience:200000);
        foreach(var heroInstance in snapshotGame.Heroes)Check(snapshotGame.UpgradeHero(heroInstance.HeroInstanceId,out string upWhy),$"升档前升级失败：{upWhy}");
        var fundedSnapshot=snapshotGame.CaptureProgress();fundedSnapshot.Kp=10;fundedSnapshot.KpEarned=10;snapshotGame=GameSession.Restore(c,fundedSnapshot);
        var beforeSnapshot=snapshotGame.Floor.Rooms[snapshotGame.CurrentRoom].Monsters.Select(m=>(m.SpecId,m.Level,m.Hp,m.MaxHp)).ToArray();
        Check(snapshotGame.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out string lmaxWhy),$"升档失败：{lmaxWhy}");
        var afterSnapshot=snapshotGame.Floor.Rooms[snapshotGame.CurrentRoom].Monsters.Select(m=>(m.SpecId,m.Level,m.Hp,m.MaxHp)).ToArray();
        Check(beforeSnapshot.SequenceEqual(afterSnapshot),"升档改写了已生成房间的怪物/奖励快照");
        int previousRoom=snapshotGame.CurrentRoom;
        foreach(var fighter in snapshotGame.Heroes)fighter.Attack=100000;
        for(int i=0;i<20000&&snapshotGame.CurrentRoom==previousRoom;i++)snapshotGame.Tick(.05f);
        var nextRoom=snapshotGame.Floor.Rooms[snapshotGame.CurrentRoom];
        Check(snapshotGame.CurrentRoom!=previousRoom&&nextRoom.Monsters.Count>0,"升档后未生成新的房间怪物");
        Check(nextRoom.Monsters.All(m=>m.Level>=1&&m.Level<=2),$"新房间怪物等级越出新区间：{string.Join(',',nextRoom.Monsters.Select(m=>m.Level))}");
        Console.WriteLine("WP3_LMAX_SNAPSHOT_PASS: generated rooms keep their snapshot while newly instantiated rooms use the new [Lmin,Lmax]");

        // R-06：Lmin 对玩家仍禁用，但保留隔离的极端区间反例能力；该现场不得写入正式存档。
        var risk=GameSession.CreateNewWorld(c,881,new[]{hero});
        risk.QaRiskRange(3,3);
        for(int i=0;i<20000&&risk.CurrentRoom==0;i++)risk.Tick(.05f);
        while(risk.CombatEntryRemaining>0)risk.Tick(.05f);
        var riskMonsters=risk.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters).ToArray();
        Check(riskMonsters.Length>0&&riskMonsters.All(m=>m.Level==3),$"QA极端区间未生效：{string.Join(',',riskMonsters.Select(m=>m.Level))}");
        rejected=false;
        try{SaveStore.Decode(c,SaveStore.Encode(new(){Session=risk.CaptureProgress()}));}catch(InvalidDataException){rejected=true;}
        Check(rejected,"含 Lmin≠1 的隔离现场被写进正式存档");
        Check(!risk.BuyKpUpgrade(KpPurchase.MonsterLevelMinimum,out _),"玩家命令仍可购买 Lmin");
        Console.WriteLine("WP3_LMIN_ISOLATION_PASS: extreme QA range spawns as designed and can neither persist nor be player-purchased");
    }
}
