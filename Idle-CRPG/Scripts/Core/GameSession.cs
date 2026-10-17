using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed class Actor
{
    public string SpecId = "";
    public string Name = "";
    public Vector2 Position;
    public float Hp, MaxHp, Attack, Defense, Speed, Range, Cooldown, Timer;
    public int Level = 1;
    public bool Boss;
    public int RewardXp;
    public float DropMultiplier=1, ValueMultiplier=1;
    public bool Alive => Hp > 0;
    public static Actor From(UnitSpec s) => new() { SpecId=s.Id, Name=s.Name, Hp=s.Hp, MaxHp=s.Hp, Attack=s.Attack, Defense=s.Defense, Speed=s.Speed, Range=s.Range, Cooldown=s.Cooldown };
}

public sealed class GameSession
{
    public Config Config { get; }
    public int Seed { get; }
    public List<Floor> Floors { get; private set; }
    public WorldCampaign? World { get; private set; }
    public DungeonSpec? ActiveDungeon { get; private set; }
    public long Gold { get; private set; }
    private readonly Dictionary<(string ItemId,long Price),long> inventory=new();
    public IReadOnlyDictionary<(string ItemId,long Price),long> Inventory => inventory;
    public SpawnModifiers SpawnModifiers { get; private set; } = new();
    private readonly Random lootRandom;
    private int dungeonSeed;
    public List<Actor> Heroes { get; } = new();
    public List<string> Log { get; } = new();
    public int FloorIndex { get; private set; }
    public int CurrentRoom { get; private set; }
    public int Experience { get; private set; }
    public int Kills { get; private set; }
    public bool Won { get; private set; }
    /// <summary>全灭返村次数；不影响累计成长和击杀资源。</summary>
    public int Retries { get; private set; }
    public Actor? ReviveTarget { get; private set; }
    public Actor? Rescuer { get; private set; }
    /// <summary>只在救援者进入交互距离后累积，单位为秒。</summary>
    public float ReviveElapsed { get; private set; }
    public Floor Floor => Floors[FloorIndex];
    public string Status { get; private set; } = "探索";
    private int destination = -1, phase;
    private float regroup;

    public GameSession(Config config, int seed, IEnumerable<string> ids)
    {
        Config = config; Seed = seed;
        lootRandom=new Random(unchecked(seed^173831));dungeonSeed=seed;
        var chosen = ids.ToArray();
        if (chosen.Length != 3 || chosen.Distinct().Count() != 3) throw new ArgumentException("选择三个不同职业");
        Heroes.AddRange(chosen.Select(id => Actor.From(config.Heroes.Single(s => s.Id == id))));
        Floors = Dungeon.Generate(seed,config);
        EnterFloor();
    }

    /// <summary>世界远征入口。保留单地牢构造供独立模拟和检查使用。</summary>
    public static GameSession CreateWorld(Config config,int seed,IEnumerable<string> ids)
    {
        var game=new GameSession(config,seed,ids);game.Log.Clear();
        game.Floors.Clear();game.World=new WorldCampaign(config);
        game.Note("从初始村庄出发，自动远征已开始");return game;
    }
    /// <summary>预留科技系统命令，只有后续首次生成的房间受影响。</summary>
    public void SetSpawnModifiers(SpawnModifiers modifiers) {Config.Spawn.Validate(modifiers);SpawnModifiers=modifiers;}
    internal void BeginDungeon(DungeonSpec dungeon,int visit)
    {
        ActiveDungeon=dungeon;Won=false;FloorIndex=0;regroup=0;ResetRescue();
        dungeonSeed=unchecked(Seed*397^visit*104729^dungeon.RegionId*7919);
        Floors=Dungeon.Generate(dungeonSeed,Config);EnterFloor();
        Note($"进入{dungeon.Name} · 等级 {dungeon.LevelMin}–{dungeon.LevelMax}");
    }

    private void EnterFloor()
    {
        CurrentRoom = 0; destination = -1; phase = 0;
        var start = Floor.Rooms[0]; start.Revealed = true; start.Generated = true;
        for (int i=0;i<Heroes.Count;i++) Heroes[i].Position = start.Center + new Vector2(0, (i-1)*0.5f);
        Note($"抵达第 {FloorIndex+1} 层 · {Floor.Rooms.Count} 个房间");
    }

    /// <summary>固定步长推进；UI 倍速不得改变该步长。</summary>
    public void Tick(float dt)
    {
        if (dt <= 0 || dt > 0.1f) throw new ArgumentOutOfRangeException(nameof(dt));
        if(World!=null && World.Stage!=CampaignStage.Dungeon){World.Tick(this,dt);return;}
        TickDungeon(dt);
        if(World!=null&&Won)World.CompleteDungeon(this);
    }
    private void TickDungeon(float dt)
    {
        if (Won) return;
        if (!Heroes.Any(h=>h.Alive)) { RespawnParty(); return; }
        var room = Floor.Rooms[CurrentRoom];
        if (!room.Clear && room.Generated)
        {
            Status = "交战中";
            ResetRescue();
            Fight(room,dt);
            if (!Heroes.Any(h=>h.Alive)) RespawnParty();
            return;
        }
        // 清房后救援优先于事件、探索、换层和最终通关。
        if (Rescue(dt)) return;
        if (room.Clear && !room.EventUsed)
        {
            room.EventUsed=true;
            if (room.Event == "营火") { foreach(var h in Heroes.Where(h=>h.Alive)) h.Hp=Math.Min(h.MaxHp,h.Hp+h.MaxHp*0.3f); Note("营火：存活队员恢复 30% 生命"); }
            if (room.Event == "宝箱") { GainExperience(8); Note("宝箱：全队获得 8 经验"); }
        }
        if (Floor.Clear && CurrentRoom == Floor.BossRoom.Id && destination < 0)
        {
            Status="楼梯集合";
            foreach(var h in Heroes.Where(h=>h.Alive)) Move(h,room.Center,dt);
            if (Heroes.Where(h=>h.Alive).Any(h=>Vector2.Distance(h.Position,room.Center)>0.35f)) return;
            regroup += dt;
            if(regroup < 0.6f) return;
            regroup=0;
            if(FloorIndex == Floors.Count-1) { Won=true; Status="地牢通关"; Note("所有楼层已清空，地牢通关！"); }
            else { FloorIndex++; EnterFloor(); }
            return;
        }
        Status="探索中";
        if(destination < 0)
        {
            var target = Floor.Clear ? Floor.BossRoom.Id : FindTarget();
            destination=FirstStep(CurrentRoom,target); phase=0;
        }
        Travel(dt);
    }

    private void ResetRescue()
    {
        ReviveTarget = null; Rescuer = null; ReviveElapsed = 0;
    }

    /// <summary>单名存活队员就近救援，逐个复活；多人不会叠加读条速度。</summary>
    private bool Rescue(float dt)
    {
        var dead = Heroes.FirstOrDefault(h => !h.Alive);
        if (dead == null) { ResetRescue(); return false; }
        if (ReviveTarget != dead || Rescuer == null || !Rescuer.Alive)
        {
            ResetRescue(); ReviveTarget = dead;
            Rescuer = Heroes.Where(h => h.Alive).OrderBy(h => Vector2.DistanceSquared(h.Position,dead.Position)).First();
        }
        if (Vector2.Distance(Rescuer!.Position,dead.Position) > Config.ReviveRange)
        {
            Status = $"前往救援 {dead.Name}";
            ReviveElapsed = 0;
            Move(Rescuer,dead.Position,dt);
            return true;
        }
        ReviveElapsed = Math.Min(Config.ReviveSeconds,ReviveElapsed + dt);
        Status = $"复活 {dead.Name} {ReviveElapsed:0.0}/{Config.ReviveSeconds:0.0}秒";
        if (ReviveElapsed + 0.00001f >= Config.ReviveSeconds)
        {
            dead.Hp = dead.MaxHp * Config.ReviveHpRatio;
            dead.Timer = 0;
            Note($"{Rescuer.Name} 复活了 {dead.Name}，恢复 {Config.ReviveHpRatio:P0} 生命");
            ResetRescue();
        }
        return true;
    }

    /// <summary>放弃本轮地牢并在所在区域村庄满血复活，资源保留，交易完成后重新决策。</summary>
    private void RespawnParty()
    {
        Retries++;
        int region=World?.CurrentRegion??ActiveDungeon?.RegionId??Config.StartRegionId;
        Floors.Clear();ActiveDungeon=null;Won=false;
        FloorIndex=0; CurrentRoom=0; regroup=0; ResetRescue();
        destination=-1;phase=0;
        foreach (var h in Heroes) { h.Hp=h.MaxHp; h.Timer=0; }
        World??=new WorldCampaign(Config);
        World.RespawnInVillage(this,region);
        Status="全灭后在村庄复活";
        Note($"小队全灭，在{World.Village.Name}复活，交易后继续冒险；成长与资源保留");
    }

    private int FindTarget()
    {
        var seen=new HashSet<int>{CurrentRoom}; var q=new Queue<int>(); q.Enqueue(CurrentRoom);
        while(q.Count>0) { int id=q.Dequeue(); if(!Floor.Rooms[id].Clear) return id; foreach(int n in Floor.Rooms[id].Neighbors) if(seen.Add(n)) q.Enqueue(n); }
        throw new InvalidOperationException("没有可达的未清理房间");
    }
    private int FirstStep(int from,int to)
    {
        var parents=new Dictionary<int,int>{{from,-1}}; var q=new Queue<int>(); q.Enqueue(from);
        while(q.Count>0) { int id=q.Dequeue(); if(id==to) break; foreach(int n in Floor.Rooms[id].Neighbors) if(parents.TryAdd(n,id)) q.Enqueue(n); }
        while(parents[to]!=from && parents[to]>=0) to=parents[to];
        return to;
    }

    private void Travel(float dt)
    {
        var a=Floor.Rooms[CurrentRoom]; var b=Floor.Rooms[destination];
        var dir=Vector2.Normalize(b.Center-a.Center);
        var exit=a.Center+dir*3.5f; var entry=b.Center-dir*3.5f;
        var goal=phase==0 ? exit : phase==1 ? entry : b.Center;
        foreach(var h in Heroes.Where(h=>h.Alive)) Move(h,goal,dt);
        // 任意队员实际触门才揭示。先等全队到入口，避免队尾留在未激活房间。
        if(phase==0 && Heroes.Any(h=>h.Alive && Vector2.Distance(h.Position,exit)<=0.32f))
        {
            Floor.Passages.Single(p=>(p.A==a.Id&&p.B==b.Id)||(p.A==b.Id&&p.B==a.Id)).Revealed=true;
            if(Heroes.Where(h=>h.Alive).All(h=>Vector2.Distance(h.Position,exit)<=0.32f)) phase=1;
        }
        else if(phase==1 && Heroes.Any(h=>h.Alive && Vector2.Distance(h.Position,entry)<=0.32f))
        {
            Reveal(b);
            CurrentRoom=b.Id; destination=-1;
        }
    }

    private void Reveal(Room room)
    {
        room.Revealed=true;
        if(room.Generated) return;
        room.Generated=true;
        var rule=Config.Spawn;var modifiers=SpawnModifiers;
        int levelMin=(ActiveDungeon?.LevelMin??1)+rule.Level-1+modifiers.LevelBonus;
        int levelMax=(ActiveDungeon?.LevelMax??1)+rule.Level-1+modifiers.LevelBonus;
        room.SnapshotLevel=levelMin;
        var rng=new Random(unchecked(dungeonSeed*397 ^ FloorIndex*7919 ^ room.Id*104729));
        int count=rng.Next(rule.Min+modifiers.MinBonus,rule.Max+modifiers.MaxBonus+1);
        for(int i=0;i<count;i++)
        {
            var m=rule.Create(Config.Monsters[rng.Next(Config.Monsters.Count)],rng.Next(levelMin,levelMax+1),room.Boss&&i==count-1,modifiers);
            m.Position=room.Center+new Vector2((float)rng.NextDouble()*4-2,(float)rng.NextDouble()*4-2);
            room.Monsters.Add(m);
        }
        if(!room.Boss && rng.NextDouble()<Config.EventChance) room.Event=rng.Next(2)==0?"营火":"宝箱";
        Note($"进入房间 {room.Id+1}：{room.Monsters.Count} 敌人 · 等级 {levelMin}–{levelMax}"+(room.Boss?" · BOSS":""));
    }

    private void Fight(Room room,float dt)
    {
        foreach(var h in Heroes.Where(h=>h.Alive)) Act(h,room.Monsters,dt,room);
        foreach(var m in room.Monsters.Where(m=>m.Alive)) Act(m,Heroes,dt,room);
    }
    private void Act(Actor actor,List<Actor> opponents,float dt,Room room)
    {
        actor.Timer=Math.Max(0,actor.Timer-dt);
        var target=opponents.Where(o=>o.Alive).OrderBy(o=>Vector2.DistanceSquared(o.Position,actor.Position)).FirstOrDefault();
        if(target==null) return;
        if(Vector2.Distance(actor.Position,target.Position)>actor.Range)
        {
            Move(actor,target.Position,dt);
            // 进房门之后的战斗局限于当前房间，允许门口队员跨过边界。
            actor.Position=Vector2.Clamp(actor.Position,room.Center-new Vector2(3.2f),room.Center+new Vector2(3.2f));
            return;
        }
        if(actor.Timer>0) return;
        actor.Timer=actor.Cooldown;
        target.Hp=Math.Max(0,target.Hp-Math.Max(1,actor.Attack-target.Defense));
        if(!target.Alive && room.Monsters.Contains(target)) { Kills++; GainExperience(target.RewardXp); RollDrops(target); if(target.Boss) Note("守门者已击败"); }
    }
    internal static void Move(Actor a,Vector2 target,float dt,float? speed=null)
    {
        var delta=target-a.Position; float distance=delta.Length();
        if(distance>0.001f) a.Position+=delta/distance*Math.Min(distance,(speed??a.Speed)*dt);
    }
    private void RollDrops(Actor monster)
    {
        foreach(var drop in Config.Drops.Where(d=>d.MonsterId==monster.SpecId))
        {
            if(lootRandom.NextDouble()>=Math.Min(1,drop.Chance*monster.DropMultiplier))continue;
            var item=Config.Items.Single(i=>i.Id==drop.ItemId);var type=Config.ItemTypes.Single(t=>t.Id==item.TypeId);
            int count=lootRandom.Next(drop.Min,drop.Max+1);
            if(type.Kind=="currency")Gold=checked(Gold+(long)Math.Ceiling(count*monster.ValueMultiplier));
            else
            {
                long price=checked((long)Math.Ceiling(item.SellPrice*monster.ValueMultiplier));
                var key=(item.Id,price);inventory[key]=checked(inventory.GetValueOrDefault(key)+count);
            }
        }
    }
    internal long SellJunk()
    {
        long total=0;
        foreach(var entry in inventory.ToArray())
        {
            var item=Config.Items.Single(i=>i.Id==entry.Key.ItemId);
            if(!Config.ItemTypes.Single(t=>t.Id==item.TypeId).AutoSell)continue;
            total=checked(total+entry.Key.Price*entry.Value);inventory.Remove(entry.Key);
        }
        Gold=checked(Gold+total);return total;
    }
    private void GainExperience(int amount)
    {
        Experience+=amount;
        while(Experience>=Heroes[0].Level*18)
        {
            Experience-=Heroes[0].Level*18;
            foreach(var h in Heroes) { h.Level++; h.MaxHp+=18; h.Attack+=4; if(h.Alive) h.Hp=Math.Min(h.MaxHp,h.Hp+18); }
            Note($"全队成长至等级 {Heroes[0].Level}");
        }
    }
    internal void Note(string message) { Log.Add(message); if(Log.Count>40) Log.RemoveAt(0); }
}
