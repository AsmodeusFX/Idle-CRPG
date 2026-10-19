using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;
public sealed partial class GameSession
{
    /// <summary>第一章v2→v3仅增通路及AND约束：保留真实完成、资源、授权队列，旧非法现场安全回城，不捏造前置通关。</summary>
    private static void MigrateCampaignPatch(Config c,SessionSnapshot s)
    {
        var w=s.World;if(w==null||c.MapVersion!=3||w.MapVersion!=2||s.ChapterIndex!=0)return;
        if(w.Cleared.Any(id=>!c.Dungeons.Any(d=>d.Id==id)))throw new InvalidDataException("旧地图存在无法映射的POI");
        w.MapVersion=3;
        var progress=new WorldProgress();progress.Cleared.UnionWith(w.Cleared);var allowed=progress.AccessibleRegions(c);
        bool Legal(Vector2 p)=>WorldNavigation.Walkable(c,p)&&allowed.Contains(c.RegionAt((int)p.X,(int)p.Y).Id);
        bool target=c.Dungeons.Any(d=>d.Id==w.TargetId&&allowed.Contains(d.RegionId));
        if(!Legal(w.Position)||!target||w.Route.Any(p=>!Legal(p)))
        {
            var village=c.ReturnVillage(c.StartRegionId);var r=c.RegionById(village.RegionId)!;
            w.Position=new(r.X+village.X,r.Y+village.Y);w.VillageId=village.Id;w.Stage=CampaignStage.Village;w.TownVersion=0;w.TradeElapsed=0;w.Route.Clear();w.VillagePaths.Clear();w.PortalPhase=PortalPhase.None;w.PortalElapsed=0;w.PortalWaypoint="";
            var next=ExpeditionPlanner.Select(c,progress,c.StartRegionId,false,s.DifficultyIndex);w.TargetId=next.Dungeon.Id;w.Farming=next.Farming;
            w.Explored.Add(c.StartRegionId);s.Floors.Clear();s.ActiveDungeonId="";s.Won=false;s.FloorIndex=s.CurrentRoom=0;s.Destination=-1;s.Phase=0;s.Regroup=0;s.ReviveTarget=s.Rescuer=-1;s.ReviveElapsed=0;s.RoomEntry.Clear();s.SkillState=new();
        }
        s.Log.Add("章节拓扑补丁：保留已通关及资源；后续目标遵循新的AND门禁");while(s.Log.Count>40)s.Log.RemoveAt(0);s.Revision++;
    }
}
