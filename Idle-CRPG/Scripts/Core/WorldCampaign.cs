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
        if(c.Dungeons.Where(d=>d.RegionId==region&&!d.Castle).All(d=>Cleared.Contains(d.Id)))return RegionStatus.WaitingCastle;
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
        var castle=c.Dungeons.Where(d=>d.Castle&&!p.Cleared.Contains(d.Id)&&p.Explored.Contains(d.RegionId)&&p.CastleUnlocked(c,d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(castle!=null)return new(castle,false);
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

public sealed class WorldCampaign
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
    public string Status { get; private set; } = "初始村庄出发";
    public IReadOnlyCollection<Vector2> Route => route.ToArray();
    private readonly Queue<Vector2> route = new();

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
        Target=ExpeditionPlanner.Select(config,Progress,CurrentRegion,previousWasFarm);
        Stage=CampaignStage.TravelDungeon;
        BuildRoute(Target.Dungeon.RegionId,WorldPoint(Target.Dungeon.RegionId,Target.Dungeon.X,Target.Dungeon.Y));
        Status=(Target.Farming?"复刷 · ":"前往 · ")+Target.Dungeon.Name;
    }
    private void BuildRoute(int region,Vector2 target)
    {
        route.Clear();
        int from=CurrentRegion;
        var parents=new Dictionary<int,int>{{from,-1}};var q=new Queue<int>();q.Enqueue(from);
        while(q.Count>0)
        {
            int id=q.Dequeue();if(id==region)break;
            foreach(var n in ExpeditionPlanner.Neighbors(config,id))
                if((n.Id==region||Progress.Explored.Contains(n.Id))&&parents.TryAdd(n.Id,id))q.Enqueue(n.Id);
        }
        if(!parents.ContainsKey(region))throw new InvalidOperationException("目标没有经过探索边界的可达路线");
        var path=new List<int>(); for(int id=region;id!=from;id=parents[id])path.Add(id);path.Reverse();
        if(path.Count>0)route.Enqueue(WorldPoint(from,0.5f,0.5f));
        foreach(int id in path)route.Enqueue(WorldPoint(id,0.5f,0.5f));
        route.Enqueue(target);
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
                if(Vector2.Distance(h.Position,npc.Position)>config.TradeRange) {GameSession.Move(h,npc.Position,dt,config.VillageSpeed);arrived=false;}
            }
            if(!arrived) {Status="前往 "+npc.Name;return;}
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
            Progress.Explored.Add(CurrentRegion);
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
            Stage=CampaignStage.Village;TradeElapsed=0;
            for(int i=0;i<game.Heroes.Count;i++)game.Heroes[i].Position=new Vector2(-4,-1+i*0.6f);
            Status=Village.Name+" · 前往交易 NPC";
        }
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
