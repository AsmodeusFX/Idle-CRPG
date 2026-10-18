using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public record WaypointSpec(string Id,string Castle,int Region,Vector2 Position);
public enum PortalPhase { None, Opening, Using, Arriving }
public sealed partial class Config
{
    public List<WaypointSpec> Waypoints=new();
    public Vector2 TownPortal {get;private set;}
    public Vector2 TownWaypoint {get;private set;}
    public float PortalSeconds {get;private set;}=.65f;
    public float PortalRange {get;private set;}=.4f;
    private void LoadWaypoints(string mapRoot)
    {
        Waypoints=Read(Path.Combine(mapRoot,"world/waypoint.csv")).Select(r=>new WaypointSpec(r["id"],r["castle_id"],Integer(r,"region_id"),new(Number(r,"x"),Number(r,"y")))).ToList();
        if(Waypoints.Count!=AreaParts.Count||Waypoints.Select(w=>w.Id).Distinct().Count()!=Waypoints.Count||Waypoints.Select(w=>w.Castle).Distinct().Count()!=Waypoints.Count||Waypoints.Any(w=>!Dungeons.Any(d=>d.Castle&&d.Id==w.Castle)||!Rulers.TryGetValue(w.Region,out var ruler)||ruler!=w.Castle||!Dungeons.Any(d=>d.RegionId==w.Region)||w.Position.X<=0||w.Position.X>=1||w.Position.Y<=0||w.Position.Y>=1||!WorldNavigation.Walkable(this,WaypointPoint(w))))throw new InvalidDataException("区域传送点必须一一对应且位于本区域有地牢的可走格");
        Town!.FindPath(Town.Entry,TownPortal);
        Town.FindPath(Town.Entry,TownWaypoint);
        if(Waypoints.Any(w=>Dungeons.Any(d=>d.RegionId==w.Region&&Vector2.Distance(w.Position,new(d.X,d.Y))<.18f)))throw new InvalidDataException("传送点与地牢/要塞入口距离过近");
    }
    public Vector2 WaypointPoint(WaypointSpec w){var r=Regions.Single(r=>r.Id==w.Region);return new(r.X+w.Position.X,r.Y+w.Position.Y);}
}
public sealed partial class WorldProgress
{
    /// <summary>解锁由当前难度/章节真实通关集合推导；所在地块全部地牢（含要塞）必须通关，且区域门禁仍满足。</summary>
    public bool WaypointUnlocked(Config c,WaypointSpec w)=>AreaPartUnlocked(c,c.AreaParts.Single(a=>a.CastleId==w.Castle).Id)&&c.Dungeons.Where(d=>d.RegionId==w.Region).All(d=>Cleared.Contains(d.Id));
}
public sealed partial class WorldCampaign
{
    public PortalPhase PortalPhase {get;private set;}
    public bool PortalReturning {get;private set;}
    public float PortalElapsed {get;private set;}
    public string PortalWaypoint {get;private set;}="";
    public Vector2 PortalOrigin {get;private set;}
    public bool TownReturnPortalVisible=>Progress.ReturnPortalVisible;
    public float PortalDuration=>config.PortalSeconds;
    public bool InTownScene=>Stage==CampaignStage.Village||PortalPhase!=PortalPhase.None&&!PortalReturning&&PortalPhase!=PortalPhase.Arriving;
    public bool InDungeonScene=>Stage==CampaignStage.Dungeon||PortalPhase!=PortalPhase.None&&PortalReturning&&PortalPhase!=PortalPhase.Arriving;
    public bool HasTownPortal=>config.Waypoints.Any(w=>Progress.WaypointUnlocked(config,w));
    /// <summary>比较可达传送点到目标的实际寻路长度；不能绕门禁，也不借用旧章节解锁。</summary>
    public WaypointSpec? ClosestWaypoint()
    {
        var region=config.Regions.Single(r=>r.Id==Target.Dungeon.RegionId);var target=new Vector2(region.X+Target.Dungeon.X,region.Y+Target.Dungeon.Y);
        var allowed=Progress.AccessibleRegions(config);var candidates=new List<(WaypointSpec Waypoint,float Distance)>();
        foreach(var w in config.Waypoints.Where(w=>Progress.WaypointUnlocked(config,w)))
        {
            try
            {
                var from=config.WaypointPoint(w);float distance=0;
                foreach(var point in WorldNavigation.FindPath(config,from,target,allowedRegions:allowed)){distance+=Vector2.Distance(from,point);from=point;}
                candidates.Add((w,distance));
            }
            catch(InvalidOperationException){}
        }
        return candidates.OrderBy(c=>c.Distance).ThenBy(c=>c.Waypoint.Id).Select(c=>c.Waypoint).FirstOrDefault();
    }
    private void BeginPortal(bool returning,string waypoint="",Vector2? origin=null)
    {
        PortalOrigin=origin??config.TownWaypoint;
        PortalReturning=returning;PortalWaypoint=waypoint;PortalPhase=PortalPhase.Opening;PortalElapsed=0;Status="打开传送门";
    }
    private bool TickPortal(GameSession game,float dt)
    {
        if(PortalPhase==PortalPhase.None)return false;
        if(PortalPhase==PortalPhase.Using)
        {
            bool arrived=true;
            foreach(var hero in game.Heroes)
            {
                if(Vector2.Distance(hero.Position,PortalOrigin)<=config.PortalRange)continue;
                GameSession.Move(hero,PortalOrigin,dt,config.VillageSpeed);arrived=false;
            }
            if(!arrived){Status="小队进入传送门";return true;}
        }
        PortalElapsed+=dt;
        if(PortalElapsed+.00001f<PortalDuration)return true;
        PortalElapsed=0;
        if(PortalPhase==PortalPhase.Opening){PortalPhase=PortalPhase.Using;Status="进入传送门";}
        else if(PortalPhase==PortalPhase.Using)
        {
            if(PortalReturning)
            {
                Village=config.ReturnVillage(config.StartRegionId);Position=WorldPoint(Village.RegionId,Village.X,Village.Y);Progress.Explored.Add(Village.RegionId);
                EnterVillage(game);
                Progress.ReturnPortalVisible=true;
                for(int i=0;i<game.Heroes.Count;i++)game.Heroes[i].Position=config.TownPortal+new Vector2(0,(i-1)*.35f);
            }
            else
            {
                var waypoint=config.Waypoints.Single(w=>w.Id==PortalWaypoint);Position=config.WaypointPoint(waypoint);Progress.Explored.Add(waypoint.Region);
                var target=config.Regions.Single(r=>r.Id==Target.Dungeon.RegionId);BuildRoute(target.Id,new(target.X+Target.Dungeon.X,target.Y+Target.Dungeon.Y));
            }
            PortalPhase=PortalPhase.Arriving;Status="传送抵达";
        }
        else {PortalPhase=PortalPhase.None;PortalWaypoint="";Status=Stage==CampaignStage.Village?"前往治疗":"前往 · "+Target.Dungeon.Name;}
        game.MarkChanged();return true;
    }
}
