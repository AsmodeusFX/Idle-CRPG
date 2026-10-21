using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

public static class HeroExperienceChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("HERO_XP: "+why);}
        var c=Config.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        Need(c.MonsterExperiencePercent(1,6)==300&&c.MonsterExperiencePercent(1,100)==300&&c.MonsterExperiencePercent(6,1)==0&&c.MonsterExperiencePercent(100,1)==0&&c.MonsterExperiencePercent(1,1)==100,"等级差倍率及边界");
        var g=new GameSession(c,785,new[]{"warrior","ranger","mage","priest"});
        for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
        var snapshot=g.Capture();var reserve=snapshot.Heroes.Last();snapshot.Heroes.RemoveAt(3);snapshot.ReserveHeroes.Add(reserve);
        snapshot.RoomEntry.Clear();snapshot.SkillState=new();
        g=GameSession.Restore(c,snapshot);
        var high=g.Heroes[0];var mid=g.Heroes[1];var dead=g.Heroes[2];high.Level=6;mid.Level=2;
        foreach(var h in g.Heroes){h.Experience=0;h.ExperienceFraction=0;h.Timer=999;h.SkillIds=Array.Empty<string>();}
        dead.Hp=0;
        var room=g.Floor.Rooms[g.CurrentRoom];var target=room.Monsters.First();
        foreach(var m in room.Monsters){m.Hp=0;m.Timer=999;}
        target.Hp=1;target.Level=1;target.RewardXp=1;high.Position=target.Position;high.Attack=100000;high.Range=100;high.Timer=0;
        int kills=g.Kills;
        for(int i=0;i<20&&g.Kills==kills;i++)g.Tick(.05f);
        Need(g.Kills==kills+1,"未通过真实击杀入口结算");
        Need(high.Experience==0&&mid.Experience==0&&mid.ExperienceFraction==80&&dead.Experience==1&&dead.Hp==0,"个人倍率/小数经验/倒地经验");
        Need(g.ReserveHeroes[0].Experience==reserve.Experience&&g.ReserveHeroes[0].Level==reserve.Level,"候补获得击杀经验");
        g.GrantGmResources(experience:17);
        Need(dead.Level==2&&dead.Experience==0&&dead.Hp==0&&mid.Level==2&&mid.Experience==17&&mid.ExperienceFraction==80,"独立升级或倒地升级复活");
        var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
        Need(restored.Heroes[1].ExperienceFraction==80&&restored.Heroes[2].Level==2&&restored.ReserveHeroes[0].Experience==reserve.Experience,"个人成长存档");
        var old=g.Capture();old.HeroExperienceVersion=0;old.Experience=7;
        var migrated=GameSession.Restore(c,old);
        Need(migrated.Heroes.Concat(migrated.ReserveHeroes).All(h=>h.Experience==7&&h.ExperienceFraction==0),"旧共享经验迁移");
        Console.WriteLine("PASS: personal XP, signed level multipliers, fractional retention, dead deployed eligibility, reserve exclusion, independent level-up and old-save migration");
    }
}
