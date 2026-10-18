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
    public bool Ranged;
    public BattleAttributes Battle=new();
    public string[] SkillIds=Array.Empty<string>();
    public Dictionary<string,int> SkillLevels=new();
    public int RewardXp;
    public float DropMultiplier=1, ValueMultiplier=1;
    public bool Alive => Hp > 0;
    /// <summary>角色字段均为值类型或不可变字符串，复制后可独立用于检查点，不共享可变战斗状态。</summary>
    public Actor Copy(){var a=(Actor)MemberwiseClone();a.Battle=Battle.Copy();a.SkillIds=SkillIds.ToArray();a.SkillLevels=new(SkillLevels);return a;}
    public static Actor From(UnitSpec s) => new() { SpecId=s.Id, Name=s.Name, Hp=s.Hp, MaxHp=s.Hp, Attack=s.Attack, Defense=s.Defense, Speed=s.Speed, Range=s.Range, Cooldown=s.Cooldown };
}

public sealed partial class GameSession
{
    public Config Config { get; private set; }
    public int Seed { get; }
    public List<Floor> Floors { get; private set; }
    public WorldCampaign? World { get; private set; }
    public DungeonSpec? ActiveDungeon { get; private set; }
    public long Gold { get; private set; }
    private readonly Dictionary<(string ItemId,long Price),long> inventory=new();
    public IReadOnlyDictionary<(string ItemId,long Price),long> Inventory => inventory;
    public SpawnModifiers SpawnModifiers { get; private set; } = new();
    private readonly PersistentRandom lootRandom;
    /// <summary>重要状态变更版本；表现层在完整逻辑步结束后保存，避免奖励和击杀被拆开。</summary>
    public long Revision { get; private set; }
    public long HitCount { get; private set; }
    internal void MarkChanged() => Revision++;
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
    public bool MovingBetweenRooms => destination>=0;
    /// <summary>行进时取沿路径最接近下一道门的存活成员，战斗镜头不使用此位置追踪。</summary>
    public Actor Leader => Heroes.Where(h=>h.Alive).OrderBy(h=>destination<0?0:Vector2.DistanceSquared(h.Position,phase==0?Floor.Rooms[CurrentRoom].DoorTowards(Floor.Rooms[destination]):Floor.Rooms[destination].DoorTowards(Floor.Rooms[CurrentRoom]))).FirstOrDefault()??Heroes[0];
    private float regroup;
    private List<PartyEntry> roomEntry=new();
    private uint roomEntryLootState;

    public GameSession(Config config, int seed, IEnumerable<string> ids)
    {
        Config = config; Seed = seed;
        Stars=new StarProgression(config.Stars);
        combatRandom=new PersistentRandom(unchecked((uint)seed^932701u));
        lootRandom=new PersistentRandom(unchecked((uint)seed^173831u));dungeonSeed=seed;
        var chosen = ids.ToArray();
        if (chosen.Length != 3 || chosen.Distinct().Count() != 3) throw new ArgumentException("选择三个不同职业");
        Heroes.AddRange(chosen.Select(id => Actor.From(config.Heroes.Single(s => s.Id == id))));
        foreach(var hero in Heroes)config.ApplyCombatProfile(hero);
        Floors = Dungeon.Generate(seed,config);
        EnterFloor();
    }

    /// <summary>世界远征入口。保留单地牢构造供独立模拟和检查使用。</summary>
    public static GameSession CreateWorld(Config config,int seed,IEnumerable<string> ids)
    {
        config=config.ChapterMaps[0];
        var game=new GameSession(config,seed,ids);game.Log.Clear();
        game.Floors.Clear();game.World=new WorldCampaign(config);
        game.Note("从初始村庄出发，自动远征已开始");return game;
    }
    /// <summary>预留科技系统命令，只有后续首次生成的房间受影响。</summary>
    public void SetSpawnModifiers(SpawnModifiers modifiers) {Config.Spawn.Validate(modifiers);if(Config.Dungeons.Any(d=>d.LevelMax+(long)Config.Spawn.Level-1+modifiers.LevelBonus>Config.MaxMonsterLevel))throw new ArgumentOutOfRangeException(nameof(modifiers));SpawnModifiers=modifiers;MarkChanged();}
    public bool RequestCastleChallenge(string castleId)
    {
        if(World?.RequestCastle(castleId)!=true)return false;
        Note("已安排城堡挑战，完成当前地牢及返村交易后出发");return true;
    }
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
        var stairs=Floor.StairPosition(start);
        var inward=Vector2.Normalize(start.Center-stairs);
        var side=new Vector2(-inward.Y,inward.X);
        for (int i=0;i<Heroes.Count;i++) Heroes[i].Position = stairs+inward*.4f+side*((i-1)*.35f);
        CaptureRoomEntry();
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
        if(CollectLoot(room,dt))return;
        if (room.Clear && !room.EventUsed)
        {
            if(room.Event!="")
            {
                Status="前往"+room.Event;
                var user=Heroes.Where(h=>h.Alive).OrderBy(h=>Vector2.DistanceSquared(h.Position,room.EventPosition)).First();
                if(Vector2.Distance(user.Position,room.EventPosition)>.35f){Move(user,room.EventPosition,dt);return;}
            }
            MarkChanged();
            room.EventUsed=true;
            if (room.Event == "营火") { foreach(var h in Heroes.Where(h=>h.Alive)) h.Hp=Math.Min(h.MaxHp,h.Hp+h.MaxHp*0.3f); Note("营火：存活队员恢复 30% 生命"); }
            if (room.Event == "宝箱") { GainExperience(8); Note("宝箱：全队获得 8 经验"); }
        }
        if (Floor.Clear && CurrentRoom == Floor.BossRoom.Id && destination < 0)
        {
            Status="楼梯集合";
            var stairs=Floor.StairPosition(room);
            foreach(var h in Heroes.Where(h=>h.Alive)) Move(h,stairs,dt);
            if (Heroes.Where(h=>h.Alive).Any(h=>Vector2.Distance(h.Position,stairs)>0.35f)){regroup=0;return;}
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
            MarkChanged();
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
        ResetSkillRoom();
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
        var exit=a.DoorTowards(b); var entry=b.DoorTowards(a);
        var goal=phase==0 ? exit : phase==1 ? entry : b.Center;
        foreach(var h in Heroes.Where(h=>h.Alive)) Move(h,goal,dt);
        // 任意队员实际触门才揭示。先等全队到入口，避免队尾留在未激活房间。
        if(phase==0 && Heroes.Any(h=>h.Alive && Vector2.Distance(h.Position,exit)<=0.32f))
        {
            var passage=Floor.Passages.Single(p=>(p.A==a.Id&&p.B==b.Id)||(p.A==b.Id&&p.B==a.Id));
            if(!passage.Revealed){passage.Revealed=true;MarkChanged();}
            if(Heroes.Where(h=>h.Alive).All(h=>Vector2.Distance(h.Position,exit)<=0.32f)) phase=1;
        }
        else if(phase==1 && Heroes.Any(h=>h.Alive && Vector2.Distance(h.Position,entry)<=0.32f))
        {
            Reveal(b);
            CurrentRoom=b.Id; destination=-1;
            CaptureRoomEntry();
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
        // GM 数量是临时输入；已生成房间仍沿用原快照，BOSS 占总数中的一个名额。
        count=Math.Clamp(count+Gm.RoomCountBonus,1,1000);
        for(int i=0;i<count;i++)
        {
            var m=rule.Create(Config.Monsters[rng.Next(Config.Monsters.Count)],rng.Next(levelMin,levelMax+1),room.Boss&&i==count-1,modifiers);
            m.Position=room.Center+new Vector2(((float)rng.NextDouble()*2-1)*(room.HalfWidth-.8f),((float)rng.NextDouble()*2-1)*(room.HalfHeight-.8f));
            Config.ApplyCombatProfile(m);
            room.Monsters.Add(m);
            room.Origins.Add(new MonsterOrigin(m.SpecId,m.Level,m.Boss,m.Position,modifiers));
        }
        if(!room.Boss && rng.NextDouble()<Config.EventChance) room.Event=rng.Next(2)==0?"营火":"宝箱";
        Note($"进入房间 {room.Id+1}：{room.Monsters.Count} 敌人 · 等级 {levelMin}–{levelMax}"+(room.Boss?" · BOSS":""));
    }

    private void Fight(Room room,float dt)
    {
        effectBudgetRemaining=SkillConfig.EffectBudget;
        AdvanceSkills(dt);
        DrainEffects();
        foreach(var h in Heroes.Where(h=>h.Alive)) Act(h,room.Monsters,dt,room);
        foreach(var m in room.Monsters.Where(m=>m.Alive)) Act(m,Heroes,dt,room);
        DrainEffects();
        Separate(Heroes,room,dt);Separate(room.Monsters,room,dt);
        if(room.Clear)ResetSkillRoom();
    }
    private static void Separate(List<Actor> actors,Room room,float dt)
    {
        for(int i=0;i<actors.Count;i++)for(int j=i+1;j<actors.Count;j++)
        {
            var a=actors[i];var b=actors[j];if(!a.Alive||!b.Alive)continue;
            var delta=b.Position-a.Position;float distance=delta.Length();if(distance>=.62f)continue;
            var push=(distance<.001f?new Vector2(1,0):delta/distance)*Math.Min((.62f-distance)*.5f,dt*2);
            a.Position=Vector2.Clamp(a.Position-push,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
            b.Position=Vector2.Clamp(b.Position+push,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
        }
    }
    private void Act(Actor actor,List<Actor> opponents,float dt,Room room)
    {
        if(Stunned(actor))return;
        actor.Timer=Math.Max(0,actor.Timer-dt*(float)(1+EffectiveAttributes(actor).AttackSpeed));
        if(RunActiveSkill(actor))return;
        var target=opponents.Where(o=>o.Alive).OrderBy(o=>Vector2.DistanceSquared(o.Position,actor.Position)).FirstOrDefault();
        if(target==null) return;
        if(Vector2.Distance(actor.Position,target.Position)>EffectiveValue(actor,"attack_range",actor.Range))
        {
            Move(actor,target.Position,dt,(float)EffectiveValue(actor,"move_speed",actor.Speed)*MoveMultiplier(actor));
            // 进房门之后的战斗局限于当前房间，允许门口队员跨过边界。
            actor.Position=Vector2.Clamp(actor.Position,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
            return;
        }
        if(actor.Timer>0) return;
        actor.Timer=(float)EffectiveValue(actor,"basic_cooldown",actor.Cooldown);
        string basic=Heroes.Contains(actor)?"basic_player":"basic_monster";
        foreach(string effect in SkillConfig.Skills[basic].Effects)Enqueue(new(){Effect=effect,Skill=basic,Owner=CombatId(actor),Target=CombatId(target),Coefficient=SkillConfig.Skills[basic].Coefficient});
        DrainEffects();
    }
    internal static void Move(Actor a,Vector2 target,float dt,float? speed=null)
    {
        var delta=target-a.Position; float distance=delta.Length();
        if(distance>0.001f) a.Position+=delta/distance*Math.Min(distance,(speed??a.Speed)*dt);
    }
    private RoomLoot RollDrops(Actor monster)
    {
        var loot=new RoomLoot {Position=monster.Position};
        foreach(var drop in Config.Drops.Where(d=>d.MonsterId==monster.SpecId))
        {
            if(lootRandom.NextDouble()>=Math.Min(1,drop.Chance*monster.DropMultiplier))continue;
            var item=Config.Items.Single(i=>i.Id==drop.ItemId);var type=Config.ItemTypes.Single(t=>t.Id==item.TypeId);
            int count=lootRandom.Next(drop.Min,drop.Max+1);
            if(type.Kind=="currency")loot.Gold=checked(loot.Gold+(long)Math.Ceiling(count*monster.ValueMultiplier));
            else
            {
                long price=checked((long)Math.Ceiling(item.SellPrice*monster.ValueMultiplier));
                loot.Items.Add(new InventoryEntry(item.Id,price,count));
            }
        }
        return loot;
    }
    private void CaptureRoomEntry()
    {
        ResetSkillRoom();roomEntryCombatState=combatRandom.State;
        roomEntry=Heroes.Select(h=>new PartyEntry(h.Position,h.Hp)).ToList();roomEntryLootState=lootRandom.State;
    }
    /// <summary>清房后先救援，再逐份战利品走近领取，最后处理房间事件。掉落金币和物品在领取时入账。</summary>
    private bool CollectLoot(Room room,float dt)
    {
        var loot=room.Loot.FirstOrDefault(l=>!l.Collected);
        if(loot==null)return false;
        Status="前往拾取战利品";
        var user=Heroes.Where(h=>h.Alive).OrderBy(h=>Vector2.DistanceSquared(h.Position,loot.Position)).First();
        if(Vector2.Distance(user.Position,loot.Position)>.35f){Move(user,loot.Position,dt);return true;}
        loot.Collected=true;Gold=checked(Gold+loot.Gold);
        foreach(var item in loot.Items)
        {
            var key=(item.ItemId,item.Price);inventory[key]=checked(inventory.GetValueOrDefault(key)+item.Count);
        }
        Note($"拾取 {loot.Gold} 金币 · {loot.Items.Sum(i=>i.Count)} 件物品，已入库");return true;
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
        MarkChanged();
        Experience=(int)Math.Min(int.MaxValue,Experience+(long)amount);
        while(Heroes[0].Level<Config.MaxHeroLevel&&Experience>=Config.HeroLevels[Heroes[0].Level].Experience)
        {
            var before=Config.HeroLevels[Heroes[0].Level];var after=Config.HeroLevels[Heroes[0].Level+1];
            Experience-=before.Experience;
            foreach(var h in Heroes) { h.Level++;h.MaxHp+=after.HpBonus-before.HpBonus;h.Attack+=after.AttackBonus-before.AttackBonus;if(h.Alive)h.Hp=Math.Min(CombatMaximumHp(h),h.Hp+after.HpBonus-before.HpBonus); }
            Note($"全队成长至等级 {Heroes[0].Level}");
        }
    }
    internal void Note(string message) { MarkChanged();Log.Add(message); if(Log.Count>40) Log.RemoveAt(0); }
}
