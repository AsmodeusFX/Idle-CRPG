using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;
public static class HeroExperienceChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("PUBLIC_XP: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        foreach(int count in new[]{1,2,4})
        {
            var g=new GameSession(c,785,Enumerable.Repeat("warrior",count));
            foreach(var h in g.Heroes){h.Hp=h.MaxHp=100000;h.Timer=999;h.SkillIds=Array.Empty<string>();}
            for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];while(g.CombatEntryRemaining>0)g.Tick(.05f);
            foreach(var m in room.Monsters){m.Hp=0;m.Timer=999;}
            var target=room.Monsters[0];target.Hp=1;target.RewardXp=7;target.Level=10;
            var attacker=g.Heroes[0];attacker.Level=6;attacker.Position=target.Position;attacker.Attack=100000;attacker.Range=100;attacker.Timer=0;
            if(count>1)g.Heroes[1].Hp=0;
            int kills=g.Kills;long before=g.PublicExperience;
            for(int i=0;i<30&&g.Kills==kills;i++)g.Tick(.05f);
            Need(g.Kills==kills+1&&g.PublicExperience-before==7&&g.Heroes.All(h=>h.Experience==0),"真实击杀不按人数复制、不衰减、不自动消费");
            g.GrantGmResources(experience:1000);int[] levels=g.Heroes.Select(h=>h.Level).ToArray();long pool=g.PublicExperience;
            var selected=g.Heroes.Last();int cost=g.ExperienceRequired(selected);
            Need(g.UpgradeHero(selected.HeroInstanceId,out _)&&selected.Level==levels.Last()+1&&g.PublicExperience==pool-cost,"指定实例一次升级");
            if(count>1)Need(selected.Hp>0||!selected.Alive,"倒地状态非法");
            var saved=g.CaptureProgress();var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=saved})).Session!);
            Need(restored.PublicExperience==g.PublicExperience&&restored.Heroes.Select(h=>h.Level).SequenceEqual(g.Heroes.Select(h=>h.Level)),"经验与等级重载");
            var old=g.CaptureProgress();old.PublicExperienceVersion=0;old.HeroExperienceVersion=1;
            foreach(var h in old.Heroes){h.Experience=7;h.ExperienceFraction=50;}
            var migrated=GameSession.Restore(c,old);Need(migrated.PublicExperience==count*7+count/2&&migrated.PublicExperienceFraction==(count%2)*50,"未花余额与小数迁移");
            var once=GameSession.Restore(c,migrated.CaptureProgress());Need(once.PublicExperience==migrated.PublicExperience,"二次迁移重复发XP");
            old=g.CaptureProgress();old.PublicExperienceVersion=0;old.HeroExperienceVersion=0;old.Experience=7;
            Need(GameSession.Restore(c,old).PublicExperience==7,"旧共享余额按人数重复发放");
        }
        Need(c.MonsterExperiencePercent(100,1)==100&&c.MonsterExperiencePercent(1,100)==100,"旧倍率接口未退役");
        Console.WriteLine("PASS: public kill XP once for 1/2/4 duplicate heroes, no decay/automatic spend, targeted upgrades, unspent/fraction migration and idempotence");
    }
}
