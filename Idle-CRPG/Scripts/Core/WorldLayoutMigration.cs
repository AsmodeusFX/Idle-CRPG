using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>从旧 16 城堡方块布局迁移：保留养成资源、有效普通地牢通关记录，新统治区完全落入旧占领范围才继承占领。</summary>
    private static void MigrateWorldLayout(Config config,SessionSnapshot s)
    {
        if(s.World==null||s.World.LayoutVersion!=0)return;
        var w=s.World;var oldCleared=w.Cleared.ToHashSet();
        var occupied=config.Regions.Where(r=>oldCleared.Contains("c"+(1+r.X/4+(r.Y/4)*4))).Select(r=>r.Id).ToHashSet();
        w.Cleared=config.Dungeons.Where(d=>!d.Castle&&oldCleared.Contains(d.Id)).Select(d=>d.Id).ToList();
        foreach(var part in config.AreaParts)
            if(config.Rulers.Where(p=>p.Value==part.CastleId).All(p=>occupied.Contains(p.Key)))
            {
                w.Cleared.Add(part.CastleId);
                // 原先已占领区域内的新增/调整地牢继承完成，不把旧占领区倒退成新一轮必清任务。
                w.Cleared.AddRange(config.Dungeons.Where(d=>!d.Castle&&config.Rulers[d.RegionId]==part.CastleId&&!w.Cleared.Contains(d.Id)).Select(d=>d.Id));
            }
        var progress=new WorldProgress();progress.Cleared.UnionWith(w.Cleared);
        var current=config.Regions.Single(r=>r.X==(int)w.Position.X&&r.Y==(int)w.Position.Y);
        int region=progress.AreaPartUnlocked(config,config.AreaPart(current.Id).Id)?current.Id:config.StartRegionId;
        var village=config.Villages.Single(v=>v.RegionId==region);var anchor=config.Regions.Single(r=>r.Id==region);
        w.Position=new Vector2(anchor.X+village.X,anchor.Y+village.Y);w.Stage=CampaignStage.Village;w.VillageId=village.Id;
        w.TradeElapsed=0;w.Route.Clear();w.VillagePaths.Clear();w.PendingCastle="";w.PendingCastles.Clear();
        var nextTarget=ExpeditionPlanner.Select(config,progress,region,false);w.TargetId=nextTarget.Dungeon.Id;w.Farming=nextTarget.Farming;
        if(!w.Explored.Contains(region))w.Explored.Add(region);
        w.LayoutVersion=1;w.AreaPartAccessVersion=1;w.NavigationVersion=1;w.Status="地图布局更新 · 前往交易 NPC";
        s.Floors.Clear();s.ActiveDungeonId="";s.Won=false;s.FloorIndex=s.CurrentRoom=0;s.Destination=-1;s.Phase=0;s.Regroup=0;s.ReviveTarget=s.Rescuer=-1;s.ReviveElapsed=0;s.RoomEntry.Clear();
        for(int i=0;i<s.Heroes.Count;i++){s.Heroes[i].Position=new Vector2(0,i*.2f);s.Heroes[i].Hp=s.Heroes[i].MaxHp;s.Heroes[i].Timer=0;}
        s.Revision++;s.Log.Add("地图更新：保留成长与资源，继承有效占领和通关记录，回村补给后继续冒险");
        while(s.Log.Count>40)s.Log.RemoveAt(0);
    }
}
