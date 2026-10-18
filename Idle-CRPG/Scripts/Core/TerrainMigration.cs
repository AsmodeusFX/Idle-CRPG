using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>旧地图新增阻断格后保留有效通关与资源；失效现场返初始村庄交易，不强行穿越地形。</summary>
    private static void MigrateTerrainRules(Config c,SessionSnapshot s)
    {
        var w=s.World;if(w==null)return;
        if(w.TerrainVersion is <0 or >1)throw new InvalidDataException("不支持的地图地形版本");
        if(w.TerrainVersion==1)return;
        foreach(var entry in s.CompletedMaps)
        {
            if(entry.Chapter<0||entry.Chapter>=c.ChapterMaps.Count)throw new InvalidDataException("历史章节越界");
            var map=c.ChapterMaps[entry.Chapter];entry.Explored.RemoveAll(id=>map.Regions.Any(r=>r.Id==id&&!r.Enterable));
            entry.Cleared.RemoveAll(id=>!map.Dungeons.Any(d=>d.Id==id));
        }
        w.Explored.RemoveAll(id=>c.Regions.Any(r=>r.Id==id&&!r.Enterable));
        w.Cleared.RemoveAll(id=>!c.Dungeons.Any(d=>d.Id==id));
        var progress=new WorldProgress();progress.Cleared.UnionWith(w.Cleared);var allowed=progress.AccessibleRegions(c);
        bool Legal(Vector2 point)=>float.IsFinite(point.X)&&float.IsFinite(point.Y)&&WorldNavigation.Walkable(c,point)&&c.Regions.Any(r=>r.Enterable&&r.X==(int)point.X&&r.Y==(int)point.Y&&allowed.Contains(r.Id));
        bool target=c.Dungeons.Any(d=>d.Id==w.TargetId&&allowed.Contains(d.RegionId));
        w.TerrainVersion=1;
        if(Legal(w.Position)&&w.Route.All(Legal)&&target)return;
        var village=c.Villages.Single(v=>v.RegionId==c.StartRegionId);var region=c.Regions.Single(r=>r.Id==village.RegionId);
        w.Position=new Vector2(region.X+village.X,region.Y+village.Y);w.Stage=CampaignStage.Village;w.VillageId=village.Id;w.TradeElapsed=0;w.Route.Clear();w.VillagePaths.Clear();w.PendingCastles.Clear();w.PendingCastle="";w.Explored.Add(region.Id);
        var next=ExpeditionPlanner.Select(c,progress,region.Id,false);w.TargetId=next.Dungeon.Id;w.Farming=next.Farming;
        s.Floors.Clear();s.ActiveDungeonId="";s.Won=false;s.FloorIndex=s.CurrentRoom=0;s.Destination=-1;s.Phase=0;s.Regroup=0;s.ReviveTarget=s.Rescuer=-1;s.ReviveElapsed=0;s.RoomEntry.Clear();s.SkillState=new();
        for(int i=0;i<s.Heroes.Count;i++){var hero=s.Heroes[i];hero.Position=new Vector2(0,i*.2f);hero.Hp=hero.MaxHp;hero.Timer=0;}
        w.Status="地形规则更新 · 返回村庄补给";s.Log.Add(w.Status);if(s.Log.Count>40)s.Log.RemoveAt(0);
    }
}
