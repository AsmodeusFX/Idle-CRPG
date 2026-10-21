using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public enum RegionStatus { Unexplored, Sweeping, WaitingCastle, Occupied, PlannedAttack }
public enum CampaignStage { TravelDungeon, Dungeon, TravelVillage, Village }
public record ExpeditionTarget(DungeonSpec Dungeon,bool Farming);

/// <summary>世界永久进度，地牢全灭与复刷不重置。</summary>
public sealed partial class WorldProgress
{
    public bool ReturnPortalVisible {get;set;}
    public HashSet<int> Explored { get; } = new();
    public HashSet<string> Cleared { get; } = new();
    public RegionStatus Status(Config c,int region)
    {
        if(c.IsTown(region)||!c.Regions.Single(r=>r.Id==region).Enterable)return RegionStatus.Unexplored;
        if(Cleared.Contains(c.Rulers[region]))return RegionStatus.Occupied;
        if(CastleUnlocked(c,c.Rulers[region]))return RegionStatus.WaitingCastle;
        return Explored.Contains(region)?RegionStatus.Sweeping:RegionStatus.Unexplored;
    }
    public bool CastleUnlocked(Config c,string castle) => AreaPartUnlocked(c,c.AreaParts.Single(p=>p.CastleId==castle).Id)&&c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]==castle&&(!c.PoiProfiles.TryGetValue(d.Id,out var profile)||profile.RequiredForCastle)).All(d=>Cleared.Contains(d.Id));
}

public static class ExpeditionPlanner
{
    public static IEnumerable<RegionSpec> Neighbors(Config c,int region)
    {
        var a=c.Regions.Single(r=>r.Id==region);
        return c.Regions.Where(r=>r.Enterable&&Math.Abs(r.X-a.X)+Math.Abs(r.Y-a.Y)==1&&c.CanCross(region,r.Id));
    }
    /// <summary>只在已解锁统治区开拓；全部扫荡完后持续复刷最高等级橙区，等待玩家主动攻城。</summary>
    public static ExpeditionTarget Select(Config c,WorldProgress p,int current,bool previousWasFarm,int difficulty=0,PersistentRandom? random=null)
    {
        if(c.PoiProfiles.Count>0||c.Town!=null)return SelectConfigured(c,p,current,difficulty,random);
        var accessible=p.AccessibleRegions(c);
        var normal=c.Dungeons.Where(d=>!d.Castle&&accessible.Contains(d.RegionId)).ToArray();
        var local=normal.Where(d=>d.RegionId==current&&!p.Cleared.Contains(d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(local!=null)return new(local,false);
        var neighbors=Neighbors(c,current).ToArray();
        var next=normal.Where(d=>neighbors.Any(n=>n.Id==d.RegionId)&&!p.Cleared.Contains(d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(next!=null)return new(next,false);
        var orange=normal.Where(d=>p.Status(c,d.RegionId)==RegionStatus.WaitingCastle).OrderByDescending(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        var pending=normal.Where(d=>!p.Cleared.Contains(d.Id)).OrderBy(d=>d.LevelMin).ThenBy(d=>d.Id).FirstOrDefault();
        if(pending!=null)return new(pending,false);
        return new(orange??normal.OrderByDescending(d=>d.LevelMin).ThenBy(d=>d.Id).First(),true);
    }
    /// <summary>主线普通地牢全局稳定排序；要塞及未授权支线绝不自动进入候选。</summary>
    private static ExpeditionTarget SelectConfigured(Config c,WorldProgress p,int current,int difficulty,PersistentRandom? random)
    {
        var accessible=p.AccessibleRegions(c);
        var normal=c.Dungeons.Where(d=>!d.Castle&&accessible.Contains(d.RegionId)&&(!c.PoiProfiles.TryGetValue(d.Id,out var profile)||profile.Status!="blocked")).OrderBy(d=>c.Challenge(d.Id,difficulty).Challenge).ThenBy(d=>d.Id).ToArray();
        bool Reachable(DungeonSpec d)
        {
            var a=c.Regions.Single(r=>r.Id==current);var b=c.Regions.Single(r=>r.Id==d.RegionId);
            try{WorldNavigation.FindPath(c,new(a.X+.5f,a.Y+.5f),new(b.X+d.X,b.Y+d.Y),allowedRegions:accessible);return true;}catch(InvalidOperationException){return false;}
        }
        var pending=normal.Where(d=>!p.Cleared.Contains(d.Id)&&Reachable(d)).ToArray();
        if(pending.Length>0){int lowest=c.Challenge(pending[0].Id,difficulty).Challenge;var same=pending.Where(d=>c.Challenge(d.Id,difficulty).Challenge==lowest).ToArray();return new(same[(random??new PersistentRandom(731913u)).Next(0,same.Length)],false);}
        var farming=normal.OrderByDescending(d=>c.Challenge(d.Id,difficulty).Challenge).FirstOrDefault(Reachable)??throw new InvalidOperationException("没有合法可达的普通地牢，配置阻塞");
        return new(farming,true);
    }
}

public sealed partial class WorldCampaign
{
    private readonly Config config;
    private readonly int difficulty;
    public string BranchCastle {get;private set;}="";
    public WorldProgress Progress { get; } = new();
    public CampaignStage Stage { get; private set; }
    public Vector2 Position { get; private set; }
    public int CurrentRegion => config.RegionAt((int)Position.X,(int)Position.Y).Id;
    public ExpeditionTarget Target { get; private set; } = null!;
    public VillageSpec Village { get; private set; }
    public float TradeElapsed { get; private set; }
    public int Trades { get; private set; }
    public int DungeonVisits { get; private set; }
    public bool WaitingApproval {get;private set;}
    public long WaitApprovalTicks {get;private set;}
    internal void ObserveApprovalWait(){if(WaitingApproval)WaitApprovalTicks++;}
    private readonly List<string> pendingCastles=new();
    /// <summary>按玩家点击顺序保存待派遣城堡，重复请求不入队。</summary>
    public IReadOnlyList<string> PendingCastles => pendingCastles.AsReadOnly();
    public string PendingCastle => pendingCastles.FirstOrDefault()??"";
    /// <summary>已占领优先；排队、前往及挑战中的统治区显示计划攻击。</summary>
    public RegionStatus RegionState(int region)
    {
        if(config.IsTown(region)||!config.Regions.Single(r=>r.Id==region).Enterable)return RegionStatus.Unexplored;
        string id=config.Rulers[region];
        if(Progress.Cleared.Contains(id))return RegionStatus.Occupied;
        return pendingCastles.Contains(id)||(Target.Dungeon.Id==id&&Stage is CampaignStage.TravelDungeon or CampaignStage.Dungeon)?RegionStatus.PlannedAttack:Progress.Status(config,region);
    }
    public string Status { get; private set; } = "初始村庄出发";
    public IReadOnlyCollection<Vector2> Route => route.ToArray();
    private readonly Queue<Vector2> route = new();
    private readonly Dictionary<string,Queue<Vector2>> villagePaths=new();

    public WorldCampaign(Config config,int difficulty=0,WorldProgress? progress=null,string branchCastle="")
    {
        this.config=config;this.difficulty=difficulty;Progress=progress??new();BranchCastle=branchCastle;
        Village=config.Villages.Single(v=>v.RegionId==config.StartRegionId);
        Position=WorldPoint(Village.RegionId,Village.X,Village.Y);
        Progress.Explored.Add(CurrentRegion);
        Plan(false);
    }
    private Vector2 WorldPoint(int region,float x,float y) {var r=config.Regions.Single(r=>r.Id==region);return new Vector2(r.X+x,r.Y+y);}
    private void Plan(bool previousWasFarm)
    {
        WaitingApproval=false;
        pendingCastles.RemoveAll(id=>Progress.Cleared.Contains(id));
        bool headReady=pendingCastles.Count>0&&Progress.CastleUnlocked(config,pendingCastles[0]);
        if(!headReady&&TryRecovery(out var recovery))
        {
            Target=recovery;Stage=CampaignStage.TravelDungeon;BuildRoute(Target.Dungeon.RegionId,WorldPoint(Target.Dungeon.RegionId,Target.Dungeon.X,Target.Dungeon.Y));Status="失败回刷 · "+Target.Dungeon.Name;return;
        }
        if(BranchCastle!="")
        {
            var candidates=config.Dungeons.Where(d=>config.Rulers[d.RegionId]==BranchCastle&&!d.Castle&&!Progress.Cleared.Contains(d.Id)).OrderBy(d=>config.Challenge(d.Id,difficulty).Challenge).ThenBy(d=>d.Id).ToArray();
            var same=candidates.Where(d=>config.Challenge(d.Id,difficulty).Challenge==config.Challenge(candidates[0].Id,difficulty).Challenge).ToArray();
            var next=same.Length==0?null:same[recoveryRandom.Next(0,same.Length)];
            Target=new(next??config.Dungeons.Single(d=>d.Id==BranchCastle),false);
            Stage=CampaignStage.TravelDungeon;BuildRoute(Target.Dungeon.RegionId,WorldPoint(Target.Dungeon.RegionId,Target.Dungeon.X,Target.Dungeon.Y));Status="支线计划 · "+Target.Dungeon.Name;return;
        }
        // 新拓扑可能使旧队首暂时不满足前置；保留批准与顺序，继续合法普通地牢，禁止静默删掉玩家指令。
        if(headReady)
        {
            Target=new(config.Dungeons.Single(d=>d.Id==pendingCastles[0]),false);
            pendingCastles.RemoveAt(0);
        }
        else Target=ExpeditionPlanner.Select(config,Progress,CurrentRegion,previousWasFarm,difficulty,recoveryRandom);
        WaitingApproval=Target.Farming&&config.Dungeons.Any(d=>d.Castle&&!Progress.Cleared.Contains(d.Id)&&Progress.CastleUnlocked(config,d.Id)&&!pendingCastles.Contains(d.Id));
        Stage=CampaignStage.TravelDungeon;
        BuildRoute(Target.Dungeon.RegionId,WorldPoint(Target.Dungeon.RegionId,Target.Dungeon.X,Target.Dungeon.Y));
        Status=(Target.Farming?"复刷 · ":"前往 · ")+Target.Dungeon.Name+(WaitingApproval?" · 待批要塞期间继续积累":"");
    }
    internal void PrepareResume()=>Plan(Target.Farming);
    private void BuildRoute(int region,Vector2 target)
    {
        route.Clear();foreach(var point in WorldNavigation.FindPath(config,Position,target,allowedRegions:Progress.AccessibleRegions(config)))route.Enqueue(point);
    }
    /// <summary>玩家请求城堡；地牢/返村/交易中只排队，确保交易流程不会被跳过。</summary>
    internal bool RequestCastle(string id)
    {
        if(!config.Dungeons.Any(d=>d.Id==id&&d.Castle)||Progress.Cleared.Contains(id)||!Progress.CastleUnlocked(config,id))return false;
        if(pendingCastles.Contains(id)||(Target.Dungeon.Id==id&&Stage is CampaignStage.TravelDungeon or CampaignStage.Dungeon))return false;
        pendingCastles.Add(id);
        WaitingApproval=false;
        ClearFailure(id);
        if(Stage==CampaignStage.TravelDungeon&&!Target.Dungeon.Castle)Plan(false);
        return true;
    }
    /// <summary>世界与村庄没有战斗；交易完成后才产生下一次地牢选择。</summary>
    internal void Tick(GameSession game,float dt)
    {
        teamLevel=game.TeamChallengeLevel;
        if(TickPortal(game,dt))return;
        if(Stage==CampaignStage.Dungeon)return;if(game.PendingFormation.Count>0&&Stage==CampaignStage.TravelDungeon)ReturnForFormation(game,false);
        if(Stage==CampaignStage.Village)
        {
            if(config.Town!=null){TickTown(game,dt);return;}
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
                if(!game.AfterVillageTrade())Plan(Target.Farming);
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
            if(!Progress.AreaPartUnlocked(config,config.AreaPart(Target.Dungeon.RegionId).Id))throw new InvalidOperationException("目标统治区尚未解锁");
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
        if(config.Town!=null)SpawnTown(game,TownVisitPhase.Heal);
        Status=Village.Name+" · 前往交易 NPC";
    }
    /// <summary>全灭直接在当前区域村庄复活；不记地牢通关，必须完成正常交易后才选目标。</summary>
    internal void RespawnInVillage(GameSession game,int regionId)
    {
        PortalPhase=PortalPhase.None;PortalElapsed=0;PortalWaypoint="";
        Village=config.ReturnVillage(regionId);
        Position=WorldPoint(Village.RegionId,Village.X,Village.Y);
        Progress.Explored.Add(Village.RegionId);
        EnterVillage(game);
        Status="全灭复活 · "+Village.Name+" · 前往交易 NPC";
    }
    internal void CompleteDungeon(GameSession game)
    {
        ClearFailure(Target.Dungeon.Id);
        Progress.Cleared.Add(Target.Dungeon.Id);
        game.Note(Target.Dungeon.Name+" 已通关"+(Target.Dungeon.Castle?"，统治区域已占领":""));
        Village=config.ReturnVillage(Target.Dungeon.RegionId);
        Stage=CampaignStage.TravelVillage;
        BuildRoute(Village.RegionId,WorldPoint(Village.RegionId,Village.X,Village.Y));
        Status="通关返村 · "+Village.Name;
        if(config.Town!=null)BeginPortal(true,origin:game.Heroes[0].Position);
    }
    /// <summary>旧档可能位于新规则锁定范围；保留资源，返初始村庄交易，再按已解锁范围出发。</summary>
    internal bool ReconcileAreaPartAccess(GameSession game)
    {
        if(!Progress.AccessibleRegions(config).Contains(CurrentRegion))
        {RespawnInVillage(game,config.StartRegionId);return false;}
        if(Stage==CampaignStage.TravelDungeon)
        {
            if(Progress.AreaPartUnlocked(config,config.AreaPart(Target.Dungeon.RegionId).Id))BuildRoute(Target.Dungeon.RegionId,WorldPoint(Target.Dungeon.RegionId,Target.Dungeon.X,Target.Dungeon.Y));
            else Plan(Target.Farming);
        }
        else if(Stage==CampaignStage.TravelVillage)BuildRoute(Village.RegionId,WorldPoint(Village.RegionId,Village.X,Village.Y));
        return true;
    }
}
