using System;
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
        var victim=liveRoom.Monsters[0];victim.Hp=1;var shooter=retry.Heroes[0];shooter.SkillIds=Array.Empty<string>();shooter.Attack=100000;shooter.Range=100;shooter.Position=victim.Position;shooter.Timer=0;
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
    }
}
