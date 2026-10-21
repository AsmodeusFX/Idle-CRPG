using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;
public static class FormationChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string reason){if(!ok)throw new Exception("FORMATION: "+reason);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        GameSession Reopen(GameSession g)=>GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
        void Finish(GameSession g){for(int i=0;i<30000&&g.PendingFormation.Count>0;i++)g.Tick(.05f);Need(g.PendingFormation.Count==0,"计划执行停滞");}
        var g=GameSession.CreateNewWorld(c,20261017,"warrior");
        Need(!g.RequestFormation(Array.Empty<string>(),out _)&&!g.RequestFormation(new[]{"warrior","warrior"},out _)&&!g.RequestFormation(new[]{"priest"},out _)&&!g.RequestFormation(new[]{"warrior","mage"},out _),"人数、重复、解锁或容量门禁");
        g.SetGmBattle(new(Invincible:true));
        for(int i=0;i<20000&&!(g.World!.Stage==CampaignStage.Dungeon&&g.Floor.Rooms[g.CurrentRoom].Monsters.Any(m=>m.Alive));i++)g.Tick(.05f);
        Need(g.World!.Stage==CampaignStage.Dungeon&&g.Floor.Rooms[g.CurrentRoom].Monsters.Any(m=>m.Alive),"未构造交战房间");
        string dungeon=g.ActiveDungeon!.Id;int room=g.CurrentRoom;int kills=g.Kills;int retries=g.Retries;
        Need(g.RequestFormation(new[]{"mage"},out _),"战斗中不能安排编队");
        Need(g.Heroes.Single().SpecId=="warrior"&&g.World.Stage==CampaignStage.Dungeon,"确认即改变战斗阵容");
        var copy=Reopen(g);Need(copy.PendingFormation.SequenceEqual(new[]{"mage"})&&copy.Heroes.Single().SpecId=="warrior","战斗检查点丢失编队计划");
        g=copy;g.SetGmBattle(new(Invincible:true));bool portal=false;
        for(int i=0;i<30000&&g.World!.Stage==CampaignStage.Dungeon;i++)g.Tick(.05f);
        Need(g.World!.Stage==CampaignStage.TravelVillage&&g.World.PortalPhase==PortalPhase.Opening,"清房后未开门回城");
        Need(g.Floor.Rooms[room].Clear&&!g.Floor.Rooms[room].Loot.Any(l=>!l.Collected)&&g.ReviveTarget==null,"未完成清房救援拾取即回城");
        Need(!g.World.Progress.Cleared.Contains(dungeon)&&g.Retries==retries&&g.Kills>=kills,"未清地牢误记通关/失败或累计资源丢失");
        g=Reopen(g);Need(g.PendingFormation.Count==1&&g.World!.PortalPhase==PortalPhase.Opening,"回城开门阶段计划未恢复");
        for(int i=0;i<30000&&g.PendingFormation.Count>0;i++)
        {
            portal|=g.World!.PortalPhase!=PortalPhase.None;
            if(g.World.Stage==CampaignStage.Village&&g.World.PortalPhase==PortalPhase.None&&g.World.TownPhase==TownVisitPhase.Formation&&g.World.TradeElapsed>0)
            {g=Reopen(g);break;}
            g.Tick(.05f);
        }
        Need(g.PendingFormation.Count>0&&g.Heroes.Single().SpecId=="warrior","未完成凯恩交互即变阵");Finish(g);
        Need(portal&&g.Heroes.Single().SpecId=="mage"&&g.ReserveHeroes.Single().SpecId=="warrior","执行编队或候补保留失败");
        Need(Vector2.Distance(g.Heroes[0].Position,c.Town!.Npc("formation").Position)<=c.Town.InteractionRange,"没有实际到达凯恩");
        float attack=g.ReserveHeroes.Single().Attack;int level=g.ReserveHeroes.Single().Level;
        g=Reopen(g);Need(g.ReserveHeroes.Single().Attack==attack&&g.ReserveHeroes.Single().Level==level,"候补存档成长丢失");
        Need(g.RequestFormation(new[]{"warrior"},out _),"无法重新上阵候补");Finish(g);
        Need(g.Heroes[0].SpecId=="warrior"&&g.Heroes[0].Attack==attack&&g.Heroes[0].Level==level,"重新上阵重置或重复成长");
        int reserveLevel=g.ReserveHeroes.Single().Level;g.GrantGmResources(0,c.HeroLevels[g.Heroes[0].Level].Experience);Need(g.ReserveHeroes.Single().Level==reserveLevel,"候补错误获得了上阵英雄经验");
        Need(g.RequestFormation(new[]{"ranger"},out _)&&g.RequestFormation(new[]{"mage"},out _)&&g.PendingFormation.Single()=="mage","新计划没有替换旧编队计划");g.CancelFormation();Need(g.PendingFormation.Count==0,"取消计划失败");
        var bad=g.CaptureProgress();bad.PendingFormation=new[]{"missing"};bool rejected=false;try{GameSession.Restore(c,bad);}catch(InvalidDataException){rejected=true;}Need(rejected,"坏计划未拒绝");
        for(int i=0;i<2000;i++)g.Tick(.05f);Need(g.World!.Stage!=CampaignStage.Village,"编队后自动循环停滞");
        Console.WriteLine("PASS: formation validation, deferred room return, portal/NPC service, pending/portal/service saves, reserves, replacement/cancel and automatic departure");
    }
}
