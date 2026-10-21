using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;
public static class HeroChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("HERO_CHECK: "+message);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        Check(c.Heroes.Count==5&&c.HeroUnlockKills.Values.Count(n=>n==0)==3,"五职业及初始三职业");
        bool rejected=false;try{GameSession.CreateNewWorld(c,1,"priest");}catch(ArgumentException){rejected=true;}Check(rejected,"锁定角色禁止创角");
        for(int count=1;count<=5;count++)
        {
            var party=GameSession.CreateWorld(c,20261017,c.Heroes.Take(count).Select(h=>h.Id));
            foreach(var map in c.ChapterMaps)
                foreach(var anchor in new[]{map.Town!.Entry,map.TownPortal,map.TownWaypoint})
                    for(int i=0;i<count;i++)Check(map.Town.Walkable(anchor+new System.Numerics.Vector2(0,PartyFormation.SideOffset(i,count))),$"{count}人出生/传送点越界，第{map.ChapterIndex+1}章");
            for(int tick=0;tick<1000&&party.World!.Stage==CampaignStage.Village;tick++)party.Tick(.05f);
            Check(party.World!.Stage!=CampaignStage.Village,$"{count}人队伍无法离城");
        }
        var g=GameSession.CreateNewWorld(c,7,"warrior");Check(g.Heroes.Count==1&&g.PartyCapacity==1,"单人开局");
        Check(!g.DeployHero("mage",out _),"未解锁席位禁止增员");
        var s=g.CaptureProgress();var copy=GameSession.Restore(c,s);for(int i=0;i<800;i++)copy.Tick(.05f);
        Check(copy.Heroes.Count==1,"单人存读档和移动");
        var saved=g.CaptureProgress();saved.Kills=250;saved.Gold=1000;g=GameSession.Restore(c,saved);
        for(int i=1;i<=4;i++)Check(g.BuyStar("s_party_"+i,out _)||i==1&&g.BuyStar("s_root",out _)&&g.BuyStar("s_party_1",out _),"扩编购买");
        Check(g.PartyCapacity==5&&g.HeroUnlocked("priest")&&g.HeroUnlocked("necromancer"),"扩编与条件解锁");
        for(int i=0;i<200000&&!(g.World!.Stage==CampaignStage.Village&&g.World.PortalPhase==PortalPhase.None);i++)g.Tick(.05f);
        foreach(var id in new[]{"ranger","mage","priest","necromancer"}){Check(g.DeployHero(id,out _),"加入编队计划 "+id);for(int tick=0;tick<30000&&g.PendingFormation.Count>0;tick++)g.Tick(.05f);Check(g.PendingFormation.Count==0,"凯恩处执行计划 "+id);}
        Check(g.Heroes.Count==5&&!g.DeployHero("mage",out _),"五人上限与防重复");
        var doc=new SaveDocument{Session=g.CaptureProgress(),ViewMode=4};var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(doc)).Session!);
        Check(restored.Heroes.Count==5&&restored.PartyCapacity==5,"五人正式存档恢复");
        for(int i=0;i<2000;i++)restored.Tick(.05f);
        Console.WriteLine("PASS: single hero creation, configurable unlocks, four star expansions, town deployment, five-member save/load and movement");
    }
}
