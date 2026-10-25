using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;
public static class FormationChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("LOCKED_FORMATION: "+why);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));var g=GameSession.CreateNewWorld(c,42,new[]{"warrior","warrior","mage"});
        var ids=g.Heroes.Select(h=>h.HeroInstanceId).ToArray();g.Account.AchievementFacts.Add("keep");
        Need(!g.RequestFormation(new[]{"ranger"},out _)&&!g.DeployHero("ranger",out _)&&g.PendingFormation.Count==0,"城镇绕过锁队");
        g.SetGmBattle(new(Invincible:true));for(int i=0;i<20000&&g.World!.Stage!=CampaignStage.Dungeon;i++)g.Tick(.05f);
        Need(!g.RequestFormation(new[]{"ranger"},out _)&&g.Heroes.Select(h=>h.HeroInstanceId).SequenceEqual(ids),"战斗中更改阵容");
        foreach(var h in g.Heroes)h.Hp=0;g.Tick(.05f);
        Need(!g.RequestFormation(new[]{"ranger"},out _)&&g.Heroes.Select(h=>h.HeroInstanceId).SequenceEqual(ids),"全灭复活绕过锁队");
        var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
        Need(restored.Heroes.Select(h=>h.HeroInstanceId).SequenceEqual(ids)&&restored.TeamLocked,"重载锁队/实例");
        var next=GameSession.StartRun(c,43,new[]{"ranger"},restored.AbandonRun());
        Need(next.Heroes.Single().SpecId=="ranger"&&next.Account.AchievementFacts.Contains("keep")&&next.PublicExperience==0&&next.Kp==0,"弃轮未重选或损失账号");
        Console.WriteLine("PASS: immutable team in town/combat/wipe/reload, duplicate instance identity, account-preserving abandonment and fresh roster");
    }
}
