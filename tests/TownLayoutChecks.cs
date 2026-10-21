using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class TownLayoutChecks
{
    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        void Need(bool ok,string why){if(!ok)throw new Exception("TOWN_LAYOUT: "+why);}
        GameSession Restore(SessionSnapshot snapshot)=>GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=snapshot})).Session!);
        var game=GameSession.CreateWorld(config,912,new[]{"warrior","ranger","mage"});
        var junk=config.Items.First(i=>config.ItemTypes.Single(t=>t.Id==i.TypeId).AutoSell);
        for(int chapter=1;chapter<5;chapter++)
        {
            game.CompleteGmChapter();
            for(int i=0;i<5000&&game.ChapterIndex<chapter;i++)game.Tick(.05f);
            Need(game.ChapterIndex==chapter,"自动跨章卡在主城");
            var map=game.Config;var town=map.Town!;
            if(chapter==1)Need(town.Width==48&&town.Height==40&&town.Width*town.Height>=34*28*2&&town.Props.Count>=24&&town.LayoutVersion==2,"鲁高因未扩建至两倍面积或缺少街区建筑");
            Need(town.Theme!="camp"&&town.Zones.Count>0,"后续章节仍复用营地");
            var anchors=town.Npcs.Select(n=>n.Position).Append(map.TownPortal).Append(map.TownWaypoint).Append(town.Exit).ToArray();
            foreach(var from in anchors)foreach(var to in anchors)
            {
                var path=town.FindPath(from,to);
                Need(path.All(town.Walkable),"NPC/传送/出发点不互通");
                for(int segment=1;segment<path.Count;segment++)
                    for(int sample=1;sample<5;sample++)Need(town.Walkable(path[segment-1]+(path[segment]-path[segment-1])*(sample/5f)), $"城镇路径线段穿越建筑或水域：{town.Id} {from}→{to}，{path[segment-1]}→{path[segment]}");
            }
            Need(!town.Walkable(new(float.NaN,0))&&!town.Walkable(new(0,float.PositiveInfinity)),"非法导航坐标未拒绝");
            Need(Vector2.Distance(map.TownPortal,map.TownWaypoint)>1,"回城与出发传送点重叠");
            if(chapter==2)Need(!town.Walkable(new(14,5))&&town.Walkable(new(18,10)),"第三章水道可走或栈桥不可走");
            if(chapter==3)Need(town.Npcs.Count==4&&ReferenceEquals(town.Npc("heal"),town.Npc("gamble"))&&!town.Npcs.Any(n=>n.HasRole("travel")),"第四章功能未合并或虚构旅行NPC");
            if(chapter==4)Need(town.Npcs.Any(n=>n.Id=="anya")&&!town.Npcs.Any(n=>n.Id=="nihlathak"),"第五章固定NPC策略不符");
            var snapshot=game.CaptureProgress();snapshot.World!.TownPhase=TownVisitPhase.Heal;
            foreach(var hero in snapshot.Heroes){hero.Hp=hero.MaxHp/2;hero.Position=map.TownPortal;}
            snapshot.Inventory.Add(new(junk.Id,5,3));
            var visit=Restore(snapshot);
            for(int i=0;i<3000&&visit.World!.TownPhase==TownVisitPhase.Heal;i++)
            {
                var before=visit.Heroes.Select(h=>h.Position).ToArray();visit.Tick(.05f);
                Need(visit.Heroes.All(h=>town.Walkable(h.Position)),"补给寻路穿越水面/建筑");
                Need(visit.Heroes.Select((h,index)=>Vector2.Distance(h.Position,before[index])).All(d=>d<=map.VillageSpeed*.05f+.0001f),"城镇移动超出固定步距离预算");
            }
            Need(visit.World!.TownPhase==TownVisitPhase.Sell&&visit.Heroes.All(h=>h.Hp==h.MaxHp)&&visit.Gold==game.Gold,"未在治疗后再进入售卖");
            visit=Restore(visit.CaptureProgress());
            for(int i=0;i<3000&&visit.World!.TownPhase!=TownVisitPhase.Exit;i++)visit.Tick(.05f);
            Need(visit.Inventory.Count==0&&visit.Gold==game.Gold+15,"售卖失败或重复到账");
            visit=Restore(visit.CaptureProgress());
            for(int i=0;i<3000&&visit.World!.Stage==CampaignStage.Village;i++)visit.Tick(.05f);
            Need(visit.World!.Stage==CampaignStage.TravelDungeon&&visit.Gold==game.Gold+15,"售卖读档后未出发或重复到账");
            // 旧版六NPC营地现场即使新布局仍可走，也必须重建目标路径；Sell/Exit 不能倒退导致重复服务。
            foreach(var phase in new[]{TownVisitPhase.Heal,TownVisitPhase.Sell,TownVisitPhase.Exit})
            {
                var old=game.CaptureProgress();old.World!.TownLayoutVersion=town.LayoutVersion-1;old.World.TownPhase=phase;
                old.World.VillagePaths[old.Heroes[0].SpecId]=new(){new(23,10),new(18,5)};
                foreach(var hero in old.Heroes)hero.Position=new(23,10);
                var migrated=Restore(old);var saved=migrated.CaptureProgress();
                Need(saved.World!.TownLayoutVersion==town.LayoutVersion&&saved.World.TownPhase==phase&&saved.World.VillagePaths.Count==0&&migrated.Heroes.All(h=>Vector2.Distance(h.Position,map.TownPortal)<.5f)&&migrated.Gold==game.Gold,"旧布局迁移丢失阶段/资源或没有清除旧路径");
                Need(Restore(saved).CaptureProgress().World!.TownLayoutVersion==town.LayoutVersion,"迁移读档不稳定");
                if(phase==TownVisitPhase.Exit)
                {
                    string path=Path.Combine(root,"artifacts/qa/town-layout",Guid.NewGuid().ToString("N"),"progress.json");
                    var store=new SaveStore(path);store.Save(migrated);store.Save(migrated);
                    File.WriteAllText(path,"broken");var backup=store.Load(config);
                    Need(backup?.Session?.World?.TownLayoutVersion==town.LayoutVersion&&backup.Session.World.TownPhase==phase&&backup.Session.Gold==game.Gold&&store.LastWarning!=null,"新布局备份恢复丢失阶段或资源");
                }
            }
            // 每章都实际完成一次往返传送，旧版出发门现场也必须迁移到本章出发阵。
            var portal=Restore(game.CaptureProgress());portal.SetGmBattle(new(InstantDungeonClear:true));
            for(int i=0;i<20000&&!(portal.World!.TownReturnPortalVisible&&portal.World.Stage==CampaignStage.Village&&portal.World.PortalPhase==PortalPhase.None);i++)portal.Tick(.05f);
            Need(portal.World!.Stage==CampaignStage.Village&&portal.World.TownReturnPortalVisible&&portal.Heroes.All(h=>town.Walkable(h.Position)),"回城传送未落在本章安全区");
            var outgoing=game.CaptureProgress();var waypoint=map.Waypoints[0];
            outgoing.World!.Cleared=map.Dungeons.Where(d=>d.RegionId==waypoint.Region).Select(d=>d.Id).ToList();
            // 解锁点须满足正常门禁；第一统治区点用于本章独立现场。
            outgoing.World.Stage=CampaignStage.TravelDungeon;outgoing.World.PortalPhase=PortalPhase.Opening;outgoing.World.PortalReturning=false;outgoing.World.PortalWaypoint=waypoint.Id;outgoing.World.PortalOrigin=new(14,7);outgoing.World.TownLayoutVersion=0;
            outgoing.World.Route.Add(outgoing.World.Position);
            foreach(var hero in outgoing.Heroes)hero.Position=new(14,7);
            var departing=Restore(outgoing);
            Need(departing.Heroes.All(h=>Vector2.Distance(h.Position,map.TownWaypoint)<.5f)&&departing.World!.PortalOrigin==map.TownWaypoint,"旧出发传送门未迁移到新传送阵");
        }
        Console.WriteLine("PASS: chapter 2–5 town geometry, multi-role NPCs, heal/sell/save/departure, return portals and old-layout migration");
    }
}
