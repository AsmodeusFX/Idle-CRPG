using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed class Actor
{
    public string Name = "";
    public Vector2 Position;
    public float Hp, MaxHp, Attack, Defense, Speed, Range, Cooldown, Timer;
    public int Level = 1;
    public bool Boss;
    public bool Alive => Hp > 0;
    public static Actor From(UnitSpec s) => new() { Name=s.Name, Hp=s.Hp, MaxHp=s.Hp, Attack=s.Attack, Defense=s.Defense, Speed=s.Speed, Range=s.Range, Cooldown=s.Cooldown };
}

public sealed class GameSession
{
    public Config Config { get; }
    public int Seed { get; }
    public List<Floor> Floors { get; }
    public List<Actor> Heroes { get; } = new();
    public List<string> Log { get; } = new();
    public int FloorIndex { get; private set; }
    public int CurrentRoom { get; private set; }
    public int Experience { get; private set; }
    public int Kills { get; private set; }
    public bool Won { get; private set; }
    /// <summary>全灭重挑战次数；不影响累计成长和击杀资源。</summary>
    public int Retries { get; private set; }
    public Actor? ReviveTarget { get; private set; }
    public Actor? Rescuer { get; private set; }
    /// <summary>只在救援者进入交互距离后累积，单位为秒。</summary>
    public float ReviveElapsed { get; private set; }
    public Floor Floor => Floors[FloorIndex];
    public string Status { get; private set; } = "探索";
    private int destination = -1, phase;
    private float regroup;
    public float AverageLevel => (float)Heroes.Average(h => h.Level);

    public GameSession(Config config, int seed, IEnumerable<string> ids)
    {
        Config = config; Seed = seed;
        var chosen = ids.ToArray();
        if (chosen.Length != 3 || chosen.Distinct().Count() != 3) throw new ArgumentException("选择三个不同职业");
        Heroes.AddRange(chosen.Select(id => Actor.From(config.Heroes.Single(s => s.Id == id))));
        Floors = Dungeon.Generate(seed,config);
        EnterFloor();
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
        if (Won) return;
        if (!Heroes.Any(h=>h.Alive)) { RetryDungeon(); return; }
        var room = Floor.Rooms[CurrentRoom];
        if (!room.Clear && room.Generated)
        {
            Status = "交战中";
            ResetRescue();
            Fight(room,dt);
            if (!Heroes.Any(h=>h.Alive)) RetryDungeon();
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

    /// <summary>重置所有楼层的本轮内容，保留同一拓扑、队员实例和所有累计成长。</summary>
    private void RetryDungeon()
    {
        Retries++;
        foreach (var floor in Floors)
        {
            foreach (var room in floor.Rooms)
            {
                room.Revealed=false; room.Generated=false; room.EventUsed=false;
                room.SnapshotLevel=0; room.Event=""; room.Monsters.Clear();
            }
            foreach (var passage in floor.Passages) passage.Revealed=false;
        }
        FloorIndex=0; regroup=0; ResetRescue();
        foreach (var h in Heroes) { h.Hp=h.MaxHp; h.Timer=0; }
        EnterFloor();
        Status="全灭后重新挑战";
        Note($"小队全灭，返回一层入口 · 第 {Retries+1} 次挑战，成长与资源保留");
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
        room.SnapshotLevel=Math.Max(1,FloorIndex+(int)AverageLevel);
        var rng=new Random(unchecked(Seed*397 ^ FloorIndex*7919 ^ room.Id*104729));
        int count=rng.Next(Config.MonstersMin,Config.MonstersMax+1)+(int)(AverageLevel/3);
        for(int i=0;i<count+(room.Boss?1:0);i++)
        {
            var m=Actor.From(Config.Monsters[rng.Next(Config.Monsters.Count)]);
            m.Level=room.SnapshotLevel; m.Boss=room.Boss&&i==count;
            m.MaxHp*= (1+0.18f*(m.Level-1))*(m.Boss?4:1); m.Hp=m.MaxHp;
            m.Attack*= (1+0.12f*(m.Level-1))*(m.Boss?1.4f:1); m.Defense+=m.Level-1;
            if(m.Boss) m.Name="守门者";
            m.Position=room.Center+new Vector2((float)rng.NextDouble()*4-2,(float)rng.NextDouble()*4-2);
            room.Monsters.Add(m);
        }
        if(!room.Boss && rng.NextDouble()<Config.EventChance) room.Event=rng.Next(2)==0?"营火":"宝箱";
        Note($"进入房间 {room.Id+1}：{room.Monsters.Count} 敌人 · 等级 {room.SnapshotLevel}"+(room.Boss?" · BOSS":""));
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
        if(!target.Alive && room.Monsters.Contains(target)) { Kills++; GainExperience(target.Level*2*(target.Boss?4:1)); if(target.Boss) Note("守门者已击败"); }
    }
    private static void Move(Actor a,Vector2 target,float dt)
    {
        var delta=target-a.Position; float distance=delta.Length();
        if(distance>0.001f) a.Position+=delta/distance*Math.Min(distance,a.Speed*dt);
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
    private void Note(string message) { Log.Add(message); if(Log.Count>40) Log.RemoveAt(0); }
}
