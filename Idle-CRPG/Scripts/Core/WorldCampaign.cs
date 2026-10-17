using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public enum RegionStatus { Unexplored, Sweeping, WaitingCastle, Occupied }
public enum CampaignStage { TravelDungeon, Dungeon, TravelVillage, Village }
public record ExpeditionTarget(DungeonSpec Dungeon,bool Farming);

/// <summary>世界永久进度，地牢全灭与复刷不重置。</summary>
public sealed class WorldProgress
{
    public HashSet<int> Explored { get; } = new();
    public HashSet<string> Cleared { get; } = new();
    public RegionStatus Status(Config c,int region)
    {
        if(Cleared.Contains(c.Rulers[region]))return RegionStatus.Occupied;
        if(CastleUnlocked(c,c.Rulers[region]))return RegionStatus.WaitingCastle;
        return Explored.Contains(region)?RegionStatus.Sweeping:RegionStatus.Unexplored;
    }
    public bool CastleUnlocked(Config c,string castle) => c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]==castle).All(d=>Cleared.Contains(d.Id));
}

public static class ExpeditionPlanner
{
    public static IEnumerable<RegionSpec> Neighbors(Config c,int region)
    {
        var a=c.Regions.Single(r=>r.Id==region);
        return c.Regions.Where(r=>Math.Abs(r.X-a.X)+Math.Abs(r.Y-a.Y)==1);
    }
    /// <summary>等级优先、稳定 ID 决胜。边界全橙时复刷一次，然后允许回溯探索边界。</summary>
    public static ExpeditionTarget Select(Config c,WorldProgress p,int current,bool previousWasFarm)
    {
        var normal=c.Dungeons.Where(d=>!d.Castle).ToArray();
        var local=normal.Where(d=>d.RegionId==current&&!p.Cleared.Contains(d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(local!=null)return new(local,false);
        var neighbors=Neighbors(c,current).ToArray();
        var next=normal.Where(d=>neighbors.Any(n=>n.Id==d.RegionId)&&!p.Cleared.Contains(d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(next!=null)return new(next,false);
        var orange=normal.Where(d=>p.Status(c,d.RegionId)==RegionStatus.WaitingCastle).OrderByDescending(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(!previousWasFarm&&neighbors.All(r=>p.Status(c,r.Id)==RegionStatus.WaitingCastle)&&orange!=null)return new(orange,true);
        // 可沿已探索路径回溯到任一边界，不把橙色区域当成不可通行墙。
        var frontier=c.Regions.Where(r=>p.Explored.Contains(r.Id)||Neighbors(c,r.Id).Any(n=>p.Explored.Contains(n.Id))).Select(r=>r.Id).ToHashSet();
        var pending=normal.Where(d=>frontier.Contains(d.RegionId)&&!p.Cleared.Contains(d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(pending!=null)return new(pending,false);
        return new(orange??normal.OrderByDescending(d=>d.LevelMin).ThenBy(d=>d.Id).First(),true);
    }
}

public sealed partial class WorldCampaign
{
    private readonly Config config;
    public WorldProgress Progress { get; } = new();
    public CampaignStage Stage { get; private set; }
    public Vector2 Position { get; private set; }
    public int CurrentRegion => config.Regions.Single(r=>r.X==(int)Position.X&&r.Y==(int)Position.Y).Id;
    public ExpeditionTarget Target { get; private set; } = null!;
    public VillageSpec Village { get; private set; }
    public float TradeElapsed { get; private set; }
    public int Trades { get; private set; }
    public int DungeonVisits { get; private set; }
    public string PendingCastle { get; private set; }="";
    public string Status { get; private set; } = "初始村庄出发";
    public IReadOnlyCollection<Vector2> Route => route.ToArray();
    private readonly Queue<Vector2> route = new();
    private readonly Dictionary<string,Queue<Vector2>> villagePaths=new();

    public WorldCampaign(Config config)
    {
        this.config=config;
        Village=config.Villages.Single(v=>v.RegionId==config.StartRegionId);
        Position=WorldPoint(Village.RegionId,Village.X,Village.Y);
        Progress.Explored.Add(CurrentRegion);
        Plan(false);
    }
    private Vector2 WorldPoint(int region,float x,float y) {var r=config.Regions.Single(r=>r.Id==region);return new Vector2(r.X+x,r.Y+y);}
    private void Plan(bool previousWasFarm)
    {
        if(PendingCastle!=""&&!Progress.Cleared.Contains(PendingCastle)&&Progress.CastleUnlocked(config,PendingCastle))
            Target=new(config.Dungeons.Single(d=>d.Id==PendingCastle),false);
        else Target=ExpeditionPlanner.Select(config,Progress,CurrentRegion,previousWasFarm);
        PendingCastle="";
        Stage=CampaignStage.TravelDungeon;
        BuildRoute(Target.Dungeon.RegionId,WorldPoint(Target.Dungeon.RegionId,Target.Dungeon.X,Target.Dungeon.Y));
        Status=(Target.Farming?"复刷 · ":"前往 · ")+Target.Dungeon.Name;
    }
    private void BuildRoute(int region,Vector2 target)
    {
        route.Clear();foreach(var point in WorldNavigation.FindPath(config,Position,target,Progress.Explored))route.Enqueue(point);
    }
    /// <summary>玩家请求城堡；地牢/返村/交易中只排队，确保交易流程不会被跳过。</summary>
    internal bool RequestCastle(string id)
    {
        if(!config.Dungeons.Any(d=>d.Id==id&&d.Castle)||Progress.Cleared.Contains(id)||!Progress.CastleUnlocked(config,id))return false;
        PendingCastle=id;
        if(Stage==CampaignStage.TravelDungeon)Plan(false);
        return true;
    }
    /// <summary>世界与村庄没有战斗；交易完成后才产生下一次地牢选择。</summary>
    internal void Tick(GameSession game,float dt)
    {
        if(Stage==CampaignStage.Dungeon)return;
        if(Stage==CampaignStage.Village)
        {
            var npc=config.Npcs.Single(n=>n.Id==Village.NpcId);
            bool arrived=true;
            foreach(var h in game.Heroes)
            {
                if(Vector2.Distance(h.Position,npc.Position)>config.TradeRange)
                {
                    var anchor=WorldPoint(Village.RegionId,Village.X,Village.Y);
                    var from=anchor+h.Position/config.WorldUnits;var target=anchor+npc.Position/config.WorldUnits;
                    if(!villagePaths.TryGetValue(h.SpecId,out var path))
                    {
                        path=new Queue<Vector2>(WorldNavigation.FindPath(config,from,target,Progress.Explored));villagePaths[h.SpecId]=path;
                    }
                    while(path.Count>0&&Vector2.DistanceSquared(path.Peek(),from)<.00000001f)path.Dequeue();
                    var waypoint=path.Count>0?path.Peek():target;
                    GameSession.Move(h,(waypoint-anchor)*config.WorldUnits,dt,config.VillageSpeed);arrived=false;
                }
            }
            Position=WorldPoint(Village.RegionId,Village.X,Village.Y)+game.Heroes[0].Position/config.WorldUnits;
            if(!arrived) {TradeElapsed=0;Status="前往 "+npc.Name;return;}
            TradeElapsed=Math.Min(config.TradeSeconds,TradeElapsed+dt);
            Status=$"自动交易 {TradeElapsed:0.0}/{config.TradeSeconds:0.0}秒";
            if(TradeElapsed+0.00001f>=config.TradeSeconds)
            {
                long sold=game.SellJunk();Trades++;
                foreach(var h in game.Heroes){h.Hp=h.MaxHp;h.Timer=0;}
                game.Note($"{Village.Name}：杂物售出 {sold} 金币，补给完成");
                Plan(Target.Farming);
            }
            return;
        }
        float remaining=config.TravelSpeed*dt;
        while(route.Count>0&&remaining>0)
        {
            var target=route.Peek();float distance=Vector2.Distance(Position,target);
            if(distance<=remaining){Position=target;route.Dequeue();remaining-=distance;}
            else {Position+=Vector2.Normalize(target-Position)*remaining;remaining=0;}
            if(Progress.Explored.Add(CurrentRegion))game.MarkChanged();
        }
        if(route.Count>0)return;
        if(Stage==CampaignStage.TravelDungeon)
        {
            if(Target.Dungeon.Castle&&!Progress.CastleUnlocked(config,Target.Dungeon.Id))throw new InvalidOperationException("城堡仍未解锁");
            Stage=CampaignStage.Dungeon;DungeonVisits++;
            game.BeginDungeon(Target.Dungeon,DungeonVisits);
        }
        else
        {
            EnterVillage(game);
        }
    }
    private void EnterVillage(GameSession game)
    {
        game.MarkChanged();
        Stage=CampaignStage.Village;TradeElapsed=0;route.Clear();villagePaths.Clear();
        for(int i=0;i<game.Heroes.Count;i++)game.Heroes[i].Position=new Vector2(0,i*.2f);
        Status=Village.Name+" · 前往交易 NPC";
    }
    /// <summary>全灭直接在当前区域村庄复活；不记地牢通关，必须完成正常交易后才选目标。</summary>
    internal void RespawnInVillage(GameSession game,int regionId)
    {
        Village=config.Villages.Single(v=>v.RegionId==regionId);
        Position=WorldPoint(regionId,Village.X,Village.Y);
        Progress.Explored.Add(regionId);
        EnterVillage(game);
        Status="全灭复活 · "+Village.Name+" · 前往交易 NPC";
    }
    internal void CompleteDungeon(GameSession game)
    {
        Progress.Cleared.Add(Target.Dungeon.Id);
        game.Note(Target.Dungeon.Name+" 已通关"+(Target.Dungeon.Castle?"，统治区域已占领":""));
        Village=config.Villages.Single(v=>v.RegionId==Target.Dungeon.RegionId);
        Stage=CampaignStage.TravelVillage;
        BuildRoute(Village.RegionId,WorldPoint(Village.RegionId,Village.X,Village.Y));
        Status="通关返村 · "+Village.Name;
    }
}
