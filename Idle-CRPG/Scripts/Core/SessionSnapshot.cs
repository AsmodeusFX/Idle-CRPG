using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;

namespace IdleCrpg.Core;

public sealed class SessionSnapshot
{
    public int Seed, DungeonSeed, FloorIndex, CurrentRoom, Experience, Kills, Retries, Destination=-1, Phase;
    public long Gold, Revision, HitCount;
    public uint LootRandomState;
    public uint RoomEntryLootState;
    public bool Won;
    public float Regroup, ReviveElapsed;
    public int ReviveTarget=-1, Rescuer=-1;
    public string Status="", ActiveDungeonId="";
    public List<Actor> Heroes=new();
    public List<Floor> Floors=new();
    public List<InventoryEntry> Inventory=new();
    public List<string> Log=new();
    public SpawnModifiers Modifiers=new();
    public bool RoomCheckpoint;
    public List<PartyEntry> RoomEntry=new();
    public WorldSnapshot? World;
}
public record InventoryEntry(string ItemId,long Price,long Count);
public record PartyEntry(Vector2 Position,float Hp);
public sealed class WorldSnapshot
{
    public CampaignStage Stage;
    public Vector2 Position;
    public string TargetId="",VillageId="",Status="";
    public string PendingCastle="";
    public int NavigationVersion;
    public bool Farming;
    public float TradeElapsed;
    public int Trades,DungeonVisits;
    public List<int> Explored=new();
    public List<string> Cleared=new();
    public List<Vector2> Route=new();
    public Dictionary<string,List<Vector2>> VillagePaths=new();
    public int AreaPartAccessVersion;
    public int LayoutVersion;
}

public sealed partial class GameSession
{
    /// <summary>快照包含房间生成结果、尸体、计时与随机源。序列化必须紧随快照，期间不推进模拟。</summary>
    public SessionSnapshot Capture() => new() {
        Seed=Seed,DungeonSeed=dungeonSeed,FloorIndex=FloorIndex,CurrentRoom=CurrentRoom,Experience=Experience,Kills=Kills,Retries=Retries,RoomEntryLootState=roomEntryLootState,
        Gold=Gold,Revision=Revision,HitCount=HitCount,LootRandomState=lootRandom.State,Won=Won,Destination=destination,Phase=phase,Regroup=regroup,
        ReviveElapsed=ReviveElapsed,ReviveTarget=ReviveTarget==null?-1:Heroes.IndexOf(ReviveTarget),Rescuer=Rescuer==null?-1:Heroes.IndexOf(Rescuer),
        Status=Status,ActiveDungeonId=ActiveDungeon?.Id??"",Heroes=Heroes.ToList(),Floors=Floors,
        Inventory=inventory.Select(e=>new InventoryEntry(e.Key.ItemId,e.Key.Price,e.Value)).ToList(),Log=Log.ToList(),Modifiers=SpawnModifiers,World=World?.Capture(),RoomEntry=roomEntry.ToList()
    };
    /// <summary>正式存档只保留房间检查点。未完成战斗从进房位置重开，已领取资源与奖励标志一并保留。</summary>
    public SessionSnapshot CaptureProgress()
    {
        // 直接复制持久字段，不先序列化整场战斗；后台不访问活跃模型。
        var s=Capture();s.Heroes=Heroes.Select(h=>h.Copy()).ToList();
        s.Floors=Floors.Select(f=>new Floor {
            Passages=f.Passages.Select(p=>new Passage {A=p.A,B=p.B,Revealed=p.Revealed}).ToList(),
            Rooms=f.Rooms.Select(r=>new Room {
                Id=r.Id,X=r.X,Y=r.Y,Depth=r.Depth,HalfWidth=r.HalfWidth,HalfHeight=r.HalfHeight,
                Boss=r.Boss,Revealed=r.Revealed,Generated=r.Generated,EventUsed=r.EventUsed,Event=r.Event,SnapshotLevel=r.SnapshotLevel,
                Neighbors=r.Neighbors.ToList(),ContentCleared=r.Clear,
                Origins=r.Clear?new():r.Origins.Count>0?r.Origins.ToList():r.Monsters.Where(m=>m.Alive).Select(m=>new MonsterOrigin(m.SpecId,m.Level,m.Boss,m.Position,s.Modifiers)).ToList(),
                Loot=!r.Clear?new():r.Loot.Where(l=>!l.Collected).Select(l=>new RoomLoot {Position=l.Position,Gold=l.Gold,Items=l.Items.ToList()}).ToList()
            }).ToList()
        }).ToList();
        s.RoomCheckpoint=true;
        bool inDungeon=s.World==null||s.World.Stage==CampaignStage.Dungeon;
        if(inDungeon&&!s.Floors[s.FloorIndex].Rooms[s.CurrentRoom].ContentCleared)
        {
            for(int i=0;i<s.Heroes.Count;i++)
            {
                var entry=s.RoomEntry.Count==3?s.RoomEntry[i]:new PartyEntry(s.Floors[s.FloorIndex].Rooms[s.CurrentRoom].Center,s.Heroes[i].MaxHp);
                s.Heroes[i].Position=entry.Position;s.Heroes[i].Hp=entry.Hp;s.Heroes[i].Timer=0;
            }
            s.Destination=-1;s.Phase=0;s.Regroup=0;s.ReviveTarget=s.Rescuer=-1;s.ReviveElapsed=0;
            if(s.RoomEntryLootState!=0)s.LootRandomState=s.RoomEntryLootState;
        }
        s.HitCount=0;
        return s;
    }
    /// <summary>校验后恢复；正式检查点重建未清房战斗，保留到账成长和待拾取奖励，不结算离线时间。</summary>
    public static GameSession Restore(Config config,SessionSnapshot s)
    {
        MigrateWorldLayout(config,s);
        if(s.RoomCheckpoint)
        {
            foreach(var floor in s.Floors)foreach(var r in floor.Rooms)
            {
                r.Monsters.Clear();
                if(r.Generated&&!r.ContentCleared)
                    foreach(var o in r.Origins)
                    {
                        var spec=config.Monsters.SingleOrDefault(m=>m.Id==o.SpecId)??throw new InvalidDataException("出生模板怪物引用无效");
                        config.Spawn.Validate(o.Modifiers);
                        if(o.Level<1||o.Level>10000)throw new InvalidDataException("出生模板等级无效");
                        var monster=config.Spawn.Create(spec,o.Level,o.Boss,o.Modifiers);monster.Position=o.Position;r.Monsters.Add(monster);
                    }
            }
        }
        ValidateSnapshot(config,s);
        var game=new GameSession(config,s.Seed,s.Heroes.Select(h=>h.SpecId));
        game.Heroes.Clear();game.Heroes.AddRange(s.Heroes);
        game.Floors=s.Floors;game.dungeonSeed=s.DungeonSeed;game.FloorIndex=s.FloorIndex;game.CurrentRoom=s.CurrentRoom;
        game.Experience=s.Experience;game.Kills=s.Kills;game.Retries=s.Retries;game.Gold=s.Gold;game.Revision=s.Revision;game.HitCount=s.HitCount;
        game.lootRandom.State=s.LootRandomState;game.Won=s.Won;game.destination=s.Destination;game.phase=s.Phase;game.regroup=s.Regroup;
        game.ReviveElapsed=s.ReviveElapsed;game.ReviveTarget=s.ReviveTarget<0?null:game.Heroes[s.ReviveTarget];game.Rescuer=s.Rescuer<0?null:game.Heroes[s.Rescuer];
        game.Status=s.Status;game.ActiveDungeon=s.ActiveDungeonId==""?null:config.Dungeons.Single(d=>d.Id==s.ActiveDungeonId);
        game.SpawnModifiers=s.Modifiers;game.Log.Clear();game.Log.AddRange(s.Log);
        game.roomEntry=s.RoomEntry.ToList();
        game.roomEntryLootState=s.RoomEntryLootState;
        foreach(var i in s.Inventory)game.inventory.Add((i.ItemId,i.Price),i.Count);
        game.World=s.World==null?null:WorldCampaign.Restore(config,s.World);
        if(game.World!=null&&s.World!.AreaPartAccessVersion==0&&!game.World.ReconcileAreaPartAccess(game))
        {
            game.Floors.Clear();game.ActiveDungeon=null;game.Won=false;game.destination=-1;game.phase=0;game.FloorIndex=game.CurrentRoom=0;game.regroup=0;game.ResetRescue();
            foreach(var h in game.Heroes){h.Hp=h.MaxHp;h.Timer=0;}
            game.Note("统治区前置规则更新：保留成长与资源，回初始村庄补给后继续冒险");
        }
        return game;
    }
    private static void ValidateSnapshot(Config c,SessionSnapshot s)
    {
        static void Need([DoesNotReturnIf(false)] bool value,string reason) {if(!value)throw new InvalidDataException("存档无效："+reason);}
        static bool Finite(float n)=>float.IsFinite(n);
        static bool Point(Vector2 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y);
        Need(s.Heroes!=null&&s.Floors!=null&&s.Inventory!=null&&s.Log!=null&&s.Modifiers!=null,"缺少状态字段");
        Need(s.Heroes!.Count==3&&s.Heroes.Select(h=>h.SpecId).Distinct().Count()==3&&s.Heroes.All(h=>c.Heroes.Any(t=>t.Id==h.SpecId)),"队伍引用");
        Need(s.Gold>=0&&s.Experience>=0&&s.Kills>=0&&s.Retries>=0&&s.Revision>=0&&s.HitCount>=0&&s.LootRandomState!=0,"资源/随机源");
        Need(s.Log!.Count<=40&&s.ReviveTarget>=-1&&s.ReviveTarget<3&&s.Rescuer>=-1&&s.Rescuer<3&&Finite(s.ReviveElapsed)&&s.ReviveElapsed>=0&&s.ReviveElapsed<=c.ReviveSeconds&&Finite(s.Regroup)&&s.Regroup>=0,"行动计时");
        Need((s.ReviveTarget<0)==(s.Rescuer<0)&&(s.ReviveTarget<0||(!s.Heroes[s.ReviveTarget].Alive&&s.Heroes[s.Rescuer].Alive)),"救援引用");
        c.Spawn.Validate(s.Modifiers!);
        Need(s.Inventory!.All(i=>c.Items.Any(t=>t.Id==i.ItemId)&&i.Price>=0&&i.Count>0)&&s.Inventory.Select(i=>(i.ItemId,i.Price)).Distinct().Count()==s.Inventory.Count,"背包引用/数量");
        Need(s.RoomEntry.Count is 0 or 3&&s.RoomEntry.All(e=>Point(e.Position)&&Finite(e.Hp)&&e.Hp>=0),"进房节点");
        Need(s.ActiveDungeonId==""||c.Dungeons.Any(d=>d.Id==s.ActiveDungeonId),"地牢引用");
        foreach(var a in s.Heroes.Concat(s.Floors!.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters)))
            Need(Point(a.Position)&&Finite(a.Hp)&&Finite(a.MaxHp)&&Finite(a.Attack)&&Finite(a.Defense)&&Finite(a.Speed)&&Finite(a.Range)&&Finite(a.Cooldown)&&Finite(a.Timer)&&Finite(a.DropMultiplier)&&Finite(a.ValueMultiplier)&&a.MaxHp>0&&a.Hp>=0&&a.Hp<=a.MaxHp&&a.Level>0&&a.Attack>=0&&a.Defense>=0&&a.Speed>0&&a.Range>0&&a.Cooldown>0&&a.Timer>=0&&a.DropMultiplier>=0&&a.ValueMultiplier>0,"角色属性");
        Need(s.Floors.Count<=20,"楼层数量");
        foreach(var f in s.Floors)
        {
            Need(f.Rooms.Count>=3&&f.Rooms.Count<=20&&f.Rooms.Select((r,i)=>r.Id==i).All(v=>v)&&f.Rooms.Count(r=>r.Boss)==1,"房间编号/BOSS");
            Need(f.Rooms.Select(r=>(r.X,r.Y)).Distinct().Count()==f.Rooms.Count&&f.Passages.Count==f.Rooms.Count-1,"房间拓扑");
            Need(f.Passages.Select(p=>(Math.Min(p.A,p.B),Math.Max(p.A,p.B))).Distinct().Count()==f.Passages.Count&&f.Rooms.Sum(r=>r.Neighbors.Count)==f.Passages.Count*2,"重复走廊/连接数量");
            foreach(var r in f.Rooms)
            {
                Need(r.Neighbors.Distinct().Count()==r.Neighbors.Count&&r.Neighbors.All(n=>n>=0&&n<f.Rooms.Count&&n!=r.Id&&f.Rooms[n].Neighbors.Contains(r.Id)&&Math.Abs(r.X-f.Rooms[n].X)+Math.Abs(r.Y-f.Rooms[n].Y)==1),"房间连接");
                Need(float.IsFinite(r.HalfWidth)&&float.IsFinite(r.HalfHeight)&&r.HalfWidth>=2&&r.HalfWidth<=5&&r.HalfHeight>=2&&r.HalfHeight<=5,"房间尺寸");
                Need((!r.Generated||r.Revealed)&&(!r.EventUsed||r.Generated)&&new[]{"","营火","宝箱"}.Contains(r.Event)&&r.Monsters.Count<=1000&&(!r.Generated?r.Monsters.Count==0:true),"房间生成状态");
                Need(r.Monsters.All(m=>c.Monsters.Any(t=>t.Id==m.SpecId)&&m.RewardXp>=0),"怪物引用");
                Need(r.Origins.Count<=1000&&r.Loot.Count<=1000&&(!r.ContentCleared||r.Generated),"房间检查点");
                Need(!s.RoomCheckpoint||!r.Generated||r.ContentCleared||r.Origins.Count>0,"未清房缺少出生模板");
                foreach(var loot in r.Loot)
                    Need(Point(loot.Position)&&loot.Gold>=0&&loot.Items.All(i=>c.Items.Any(t=>t.Id==i.ItemId)&&i.Price>=0&&i.Count>0),"战利品数据");
            }
            Need(f.Passages.All(p=>p.A>=0&&p.B>=0&&p.A<f.Rooms.Count&&p.B<f.Rooms.Count&&f.Rooms[p.A].Neighbors.Contains(p.B)),"走廊引用");
            var connected=new HashSet<int>();var pending=new Queue<int>();pending.Enqueue(0);
            while(pending.Count>0){int id=pending.Dequeue();if(!connected.Add(id))continue;foreach(int n in f.Rooms[id].Neighbors)pending.Enqueue(n);}
            Need(connected.Count==f.Rooms.Count,"房间不连通");
        }
        bool inDungeon=s.World==null||s.World.Stage==CampaignStage.Dungeon;
        if(inDungeon)
        {
            Need(s.FloorIndex>=0&&s.FloorIndex<s.Floors.Count,"当前楼层");var f=s.Floors[s.FloorIndex];
            Need(s.CurrentRoom>=0&&s.CurrentRoom<f.Rooms.Count&&f.Rooms[s.CurrentRoom].Revealed&&s.Destination>=-1&&s.Destination<f.Rooms.Count&&s.Phase>=0&&s.Phase<=1,"当前房间/移动");
            Need(s.Destination<0||f.Rooms[s.CurrentRoom].Neighbors.Contains(s.Destination),"移动目标不相邻");
        }
        if(s.World!=null)
        {
            WorldCampaign.Validate(c,s.World);
            Need(!inDungeon||s.ActiveDungeonId==s.World.TargetId,"世界/地牢目标不一致");
        }
    }
}

public sealed partial class WorldCampaign
{
    internal WorldSnapshot Capture() => new() {LayoutVersion=1,AreaPartAccessVersion=1,Stage=Stage,Position=Position,TargetId=Target.Dungeon.Id,Farming=Target.Farming,VillageId=Village.Id,TradeElapsed=TradeElapsed,Trades=Trades,DungeonVisits=DungeonVisits,Status=Status,PendingCastle=PendingCastle,NavigationVersion=1,Explored=Progress.Explored.OrderBy(i=>i).ToList(),Cleared=Progress.Cleared.OrderBy(i=>i).ToList(),Route=route.ToList(),VillagePaths=villagePaths.ToDictionary(p=>p.Key,p=>p.Value.ToList())};
    internal static WorldCampaign Restore(Config c,WorldSnapshot s)
    {
        var w=new WorldCampaign(c) {Stage=s.Stage,Position=s.Position,Target=new(c.Dungeons.Single(d=>d.Id==s.TargetId),s.Farming),Village=c.Villages.Single(v=>v.Id==s.VillageId),TradeElapsed=s.TradeElapsed,Trades=s.Trades,DungeonVisits=s.DungeonVisits,Status=s.Status};
        w.Progress.Explored.Clear();w.Progress.Explored.UnionWith(s.Explored);w.Progress.Cleared.UnionWith(s.Cleared);w.PendingCastle=s.PendingCastle;w.route.Clear();foreach(var p in s.Route)w.route.Enqueue(p);
        foreach(var p in s.VillagePaths)w.villagePaths[p.Key]=new Queue<Vector2>(p.Value);
        if(s.NavigationVersion==0&&s.Stage is CampaignStage.TravelDungeon or CampaignStage.TravelVillage)
        {
            var last=s.Route.Last();w.BuildRoute(w.CurrentRegion,last);
        }
        return w;
    }
    internal static void Validate(Config c,WorldSnapshot s)
    {
        if(s.LayoutVersion is not (0 or 1))throw new InvalidDataException("世界布局存档版本无效");
        if(s.AreaPartAccessVersion is not (0 or 1))throw new InvalidDataException("统治区前置存档版本无效");
        if(s.NavigationVersion is not (0 or 1))throw new InvalidDataException("不支持的世界寻路版本");
        bool Point(Vector2 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&p.X>=0&&p.Y>=0&&p.X<c.WorldWidth&&p.Y<c.WorldHeight;
        if(s.VillagePaths==null||s.VillagePaths.Count>3||s.VillagePaths.Any(p=>!c.Heroes.Any(h=>h.Id==p.Key)||p.Value.Count>20000||p.Value.Any(v=>!Point(v))))throw new InvalidDataException("村庄路径无效");
        if(!Enum.IsDefined(s.Stage)||!Point(s.Position)||!c.Dungeons.Any(d=>d.Id==s.TargetId)||!c.Villages.Any(v=>v.Id==s.VillageId)||s.Explored==null||s.Cleared==null||s.Route==null||s.Explored.Any(id=>!c.Regions.Any(r=>r.Id==id))||s.Cleared.Any(id=>!c.Dungeons.Any(d=>d.Id==id))||s.Route.Count>c.Regions.Count*WorldNavigation.CellsPerRegion*WorldNavigation.CellsPerRegion+2||s.Route.Any(p=>!Point(p))||s.Trades<0||s.DungeonVisits<0||!float.IsFinite(s.TradeElapsed)||s.TradeElapsed<0||s.TradeElapsed>c.TradeSeconds||s.PendingCastle!=""&&!c.Dungeons.Any(d=>d.Id==s.PendingCastle&&d.Castle))
            throw new InvalidDataException("存档世界状态/引用无效");
        int current=c.Regions.Single(r=>r.X==(int)s.Position.X&&r.Y==(int)s.Position.Y).Id;
        if(s.AreaPartAccessVersion==1)
        {
            var progress=new WorldProgress();progress.Cleared.UnionWith(s.Cleared);
            var allowed=progress.AccessibleRegions(c);
            int RegionAt(Vector2 point)=>c.Regions.Single(r=>r.X==(int)point.X&&r.Y==(int)point.Y).Id;
            if(!allowed.Contains(current)||s.Stage is CampaignStage.TravelDungeon or CampaignStage.Dungeon&&!allowed.Contains(c.Dungeons.Single(d=>d.Id==s.TargetId).RegionId)||s.Route.Any(p=>!allowed.Contains(RegionAt(p))))throw new InvalidDataException("存档路线或目标进入未解锁统治区");
        }
        if(!s.Explored.Contains(current)||((s.Stage==CampaignStage.TravelDungeon||s.Stage==CampaignStage.TravelVillage)&&s.Route.Count==0)||((s.Stage==CampaignStage.Dungeon||s.Stage==CampaignStage.Village)&&s.Route.Count!=0))throw new InvalidDataException("存档探索/路径状态无效");
    }
}
