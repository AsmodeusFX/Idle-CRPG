using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;
public static class HeroChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("HERO_CHECK: "+why);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        Need(c.Heroes.Count==5&&c.HeroUnlockKills.Values.Count(n=>n==0)==3,"职业配置");
        bool rejected=false;try{GameSession.CreateNewWorld(c,1,"priest");}catch(ArgumentException){rejected=true;}Need(rejected,"未解锁职业新建");
        for(int count=1;count<=4;count++)
        {
            var g=GameSession.CreateNewWorld(c,20261017,Enumerable.Repeat("warrior",count));
            foreach(var map in c.ChapterMaps)foreach(var anchor in new[]{map.Town!.Entry,map.TownPortal,map.TownWaypoint})
                for(int i=0;i<count;i++)Need(map.Town.Walkable(anchor+new System.Numerics.Vector2(0,PartyFormation.SideOffset(i,count))),"出生/传送点越界");
            for(int i=0;i<1000&&g.World!.Stage==CampaignStage.Village;i++)g.Tick(.05f);
            Need(g.World!.Stage!=CampaignStage.Village&&g.PartyCapacity==4,"重复职业不能独立离城");
            var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.CaptureProgress(),ViewMode=4})).Session!);
            Need(restored.Heroes.Count==count&&restored.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==count,"同职恢复串号");
            Need(!restored.DeployHero("mage",out _),"旧加入命令绕过锁队");
        }
        rejected=false;try{GameSession.CreateNewWorld(c,1,Enumerable.Repeat("warrior",5));}catch(ArgumentException){rejected=true;}Need(rejected,"新轮第五人未拒绝");
        Console.WriteLine("PASS: initial unlocks, 1-4 duplicate class creation, independent town navigation, save/load, fixed four slots and no fifth-seat bypass");
    }
}
