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
    public int Experience,ExperienceFraction;
    public bool Boss;
    public bool Ranged;
    public BattleAttributes Battle=new();
    public string[] SkillIds=Array.Empty<string>();
    /// <summary>已成功激活的持续光环；跨房保留，来源死亡或卸槽撤销，范围Buff仍属于短期战斗现场。</summary>
    public List<string> ActiveAuraSkills=new();
    public Dictionary<string,int> SkillLevels=new();
    public Dictionary<string,int> Talents=new();
    public bool StartingSkillGranted;
    public List<TalentBonus> TalentBonuses=new();
    public Dictionary<string,double> SkillSwapLocks=new();
    public int RewardXp;
    public float DropMultiplier=1, ValueMultiplier=1;
    public bool Alive => Hp > 0;
    /// <summary>角色字段均为值类型或不可变字符串，复制后可独立用于检查点，不共享可变战斗状态。</summary>
    public Actor Copy(){var a=(Actor)MemberwiseClone();a.Battle=Battle.Copy();a.SkillIds=SkillIds.ToArray();a.ActiveAuraSkills=new(ActiveAuraSkills);a.SkillLevels=new(SkillLevels);a.Talents=new(Talents);a.TalentBonuses=new(TalentBonuses);a.SkillSwapLocks=new(SkillSwapLocks);return a;}
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
    public int Kills { get; private set; }
    public bool Won { get; private set; }
    /// <summary>全灭返村次数；不影响累计成长和击杀资源。</summary>
    public int Retries { get; private set; }
    public Actor? ReviveTarget { get; private set; }
    public Actor? Rescuer { get; private set; }
    /// <summary>只在救援者进入交互距离后累积，单位为秒。</summary>
    public float ReviveElapsed { get; private set; }
    public Floor Floor => Floors[FloorIndex];
    public string CurrentFloorDisplayName=>ActiveDungeon==null?"":Config.FloorPlans.FirstOrDefault(p=>p.Poi==ActiveDungeon.Id&&p.Difficulty==DifficultyIndex&&p.Index==FloorIndex+1)?.Name??ActiveDungeon.Name;
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
        var chosen = ids.ToArray();formationCapacityFloor=chosen.Length;
        if (chosen.Length < 1 || chosen.Length > 5 || chosen.Distinct().Count() != chosen.Length) throw new ArgumentException("选择一至五个不同职业");
        Heroes.AddRange(chosen.Select(id => Actor.From(config.Heroes.Single(s => s.Id == id))));
        foreach(var hero in Heroes){config.ApplyCombatProfile(hero);if(config.Town!=null)GrantStarterSkill(hero);}
        Floors = Dungeon.Generate(seed,config);
        EnterFloor();
    }

    /// <summary>世界远征入口。保留单地牢构造供独立模拟和检查使用。</summary>
    public static GameSession CreateWorld(Config config,int seed,IEnumerable<string> ids)
    {
        config=config.ChapterMaps[0];
        var game=new GameSession(config,seed,ids);game.Log.Clear();
        game.Floors.Clear();game.World=new WorldCampaign(config);
        game.World.StartTownDeparture(game);
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
        Floors=Dungeon.Generate(dungeonSeed,Config,Config.PoiProfiles.TryGetValue(dungeon.Id,out var profile)?profile.Floors:null);EnterFloor();
        Note($"进入{dungeon.Name} · 等级 {dungeon.LevelMin}–{dungeon.LevelMax}");
        ApplyGmEntryClear();
    }

    private void EnterFloor()
    {
        CurrentRoom = 0; destination = -1; phase = 0;
        var start = Floor.Rooms[0]; start.Revealed = true; start.Generated = true;
        var stairs=Floor.StairPosition(start);
        var inward=Vector2.Normalize(start.Center-stairs);
        var side=new Vector2(-inward.Y,inward.X);
        for (int i=0;i<Heroes.Count;i++) Heroes[i].Position = stairs+inward*.4f+side*(PartyFormation.SideOffset(i,Heroes.Count));
        CaptureRoomEntry();
        Note($"抵达第 {FloorIndex+1} 层 · {Floor.Rooms.Count} 个房间");
    }

    /// <summary>固定步长推进；UI 倍速不得改变该步长。</summary>
    public void Tick(float dt)
    {
        if (dt <= 0 || dt > 0.1f) throw new ArgumentOutOfRangeException(nameof(dt));
        AdvanceSwapLocks(dt);World?.ObserveApprovalWait();
        if(World!=null && World.Stage!=CampaignStage.Dungeon){World.Tick(this,dt);return;}
        var tickFloor=Floor;bool fighting=!Floor.Rooms[CurrentRoom].Clear&&Floor.Rooms[CurrentRoom].Generated;
        var positions=Heroes.Select(h=>h.Position).ToArray();
        TickDungeon(dt);
        if(fighting)tickFloor.BattleTicks++;else if(Heroes.Where((h,i)=>h.Position!=positions[i]).Any())tickFloor.WalkTicks++;
        if(World!=null&&Won)World.CompleteDungeon(this);
    }
    private void TickDungeon(float dt)
    {
        if (Won) return;
        Floor.ElapsedTicks++;
        if (!Heroes.Any(h=>h.Alive)) { RespawnParty(); return; }
        var room = Floor.Rooms[CurrentRoom];
        if(room.Clear&&(skillState.Targeting.Locks.Count>0||skillState.Targeting.EntryPrepared))ResetSkillRoom();
        if(destination<0&&room.Generated)
        {
            if(!room.Clear)AdvanceRoomEntry(room,dt);else entryMoved.Clear();
        }
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
            if (room.Event == "宝箱") { GainMonsterExperience(8,Math.Max(1,room.SnapshotLevel)); Note("宝箱：每名上阵英雄按房间等级结算经验（基础8）"); }
        }
        if(pendingFormation.Length>0&&World!=null&&!(Floor.Clear&&FloorIndex==Floors.Count-1))
        {ResetSkillRoom();World.ReturnForFormation(this,true);return;}
        if (Floor.Clear && destination < 0 && (CurrentRoom == Floor.BossRoom.Id||Config.Town!=null&&FloorIndex==Floors.Count-1))
        {
            if(Config.Town!=null&&FloorIndex==Floors.Count-1)
            {
                // 最后一层不需要地面出口：完成救援、拾取与事件后，小队就地施放回城门。
                Won=true;Status="施放回城传送门";Note("所有楼层已清空，小队打开回城传送门");return;
            }
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
        World?.RecordFailure(this);
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
        var party=Heroes.Where(h=>h.Alive).ToArray();
        for(int i=0;i<party.Length;i++)
        {
            var actor=party[i];var waypoint=phase==1&&Vector2.Dot(actor.Position-exit,dir)<-.05f?exit:goal;
            if(i>0&&Vector2.Distance(actor.Position,party[i-1].Position)<Config.Talents.Rule("dungeon_queue_spacing_milli")/1000f)continue;
            Move(actor,waypoint,dt);
        }
        // 玩家编队首位先穿门，队尾持续移动；不等全员集合再激活敌人。
        if(phase==0 && Vector2.Distance(party[0].Position,exit)<=0.32f)
        {
            var passage=Floor.Passages.Single(p=>(p.A==a.Id&&p.B==b.Id)||(p.A==b.Id&&p.B==a.Id));
            if(!passage.Revealed){passage.Revealed=true;MarkChanged();}
            phase=1;
        }
        else if(phase==1 && Vector2.Distance(party[0].Position,entry)<=0.32f)
        {
            Reveal(b);
            CurrentRoom=b.Id; destination=-1;
            CaptureRoomEntry();
            StartCombatEntry(b,entry);
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
        if(ActiveDungeon!=null&&Config.PoiProfiles.ContainsKey(ActiveDungeon.Id))levelMin=levelMax=Config.Challenge(ActiveDungeon.Id,DifficultyIndex).Challenge+rule.Level-1+modifiers.LevelBonus;
        var plan=ActiveDungeon==null?null:Config.FloorPlans.SingleOrDefault(p=>p.Poi==ActiveDungeon.Id&&p.Difficulty==DifficultyIndex&&p.Index==FloorIndex+1);
        if(plan!=null)levelMin=levelMax=plan.MonsterLevel+rule.Level-1+modifiers.LevelBonus;
        room.SnapshotLevel=levelMin;
        var rng=new Random(unchecked(dungeonSeed*397 ^ FloorIndex*7919 ^ room.Id*104729));
        int count=rng.Next(rule.Min+modifiers.MinBonus,rule.Max+modifiers.MaxBonus+1);
        // GM 数量是临时输入；已生成房间仍沿用原快照，BOSS 占总数中的一个名额。
        count=Math.Clamp(count+Gm.RoomCountBonus,1,1000);
        for(int i=0;i<count;i++)
        {
            bool boss=room.Boss&&i==count-1&&(plan==null||plan.BossActors==1);
            var m=rule.Create(Config.Monsters[rng.Next(Config.Monsters.Count)],rng.Next(levelMin,levelMax+1),boss,modifiers);
            m.Position=room.Center+new Vector2(((float)rng.NextDouble()*2-1)*(room.HalfWidth-.8f),((float)rng.NextDouble()*2-1)*(room.HalfHeight-.8f));
            Config.ApplyCombatProfile(m);
            if(m.Boss&&ActiveDungeon!=null&&Config.PoiProfiles.TryGetValue(ActiveDungeon.Id,out var poi)&&poi.Boss!="")m.Name=poi.Boss+"（原型）";
            room.Monsters.Add(m);
            room.Origins.Add(new MonsterOrigin(m.SpecId,m.Level,m.Boss,m.Position,modifiers));
        }
        if(!room.Boss && rng.NextDouble()<Config.EventChance) room.Event=rng.Next(2)==0?"营火":"宝箱";
        Floor.ActualSpawned+=room.Monsters.Count;
        Note($"进入房间 {room.Id+1}：{room.Monsters.Count} 敌人 · 等级 {levelMin}–{levelMax}"+(room.Boss?" · BOSS":""));
    }

    private void Fight(Room room,float dt)
    {
        if(CombatEntryRemaining>0)
        {
            skillState.Targeting.EntryElapsed+=dt;Status=$"入场展开 · 敌人警觉 {CombatEntryRemaining:0.0}秒";
            return;
        }
        effectBudgetRemaining=SkillConfig.EffectBudget;
        AdvanceSkills(dt);
        DrainEffects();
        foreach(var h in Heroes.Where(h=>h.Alive)) Act(h,room.Monsters,dt,room);
        PrepareMonsterTargets(dt);
        foreach(var m in room.Monsters.Where(m=>m.Alive)) Act(m,Heroes,dt,room);
        DrainEffects();
        if(room.Clear)ResetSkillRoom();else SynchronizeAuras();
    }
    private void Act(Actor actor,List<Actor> opponents,float dt,Room room)
    {
        if(!InCombatRoom(actor)||entryMoved.Contains(actor)||Stunned(actor))return;
        actor.Timer=Math.Max(0,actor.Timer-dt*(float)(1+EffectiveAttributes(actor).AttackSpeed));
        if(RunRetreat(actor,dt,room))return;
        if(RunActiveSkill(actor))return;
        var target=Heroes.Contains(actor)?opponents.Where(InCombatRoom).OrderBy(o=>Vector2.DistanceSquared(o.Position,actor.Position)).FirstOrDefault():OrdinaryMonsterTarget(actor);
        if(target==null||!InCombatRoom(target)) return;
        double engagementRange=EffectiveValue(actor,"attack_range",actor.Range);
        // 已装配近战主动就绪时先接近其施放距离，避免远程普攻射程使至圣斩永远无法使用。
        foreach(string id in actor.SkillIds)if(SkillCooldown(actor,id)<=0)engagementRange=Math.Min(engagementRange,SkillConfig.Skills[id].Range);
        if(Vector2.Distance(actor.Position,target.Position)>engagementRange)
        {
            Move(actor,target.Position,dt,(float)EffectiveValue(actor,"move_speed",actor.Speed)*MoveMultiplier(actor));
            // 进房门之后的战斗局限于当前房间，允许门口队员跨过边界。
            actor.Position=Vector2.Clamp(actor.Position,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
            return;
        }
        if(actor.Timer>0) return;
        actor.Timer=(float)EffectiveValue(actor,"basic_cooldown",actor.Cooldown);
        string basic=Heroes.Contains(actor)?"basic_player":"basic_monster";
        long originCast=++skillState.NextId;
        foreach(string effect in SkillConfig.Skills[basic].Effects)Enqueue(new(){Effect=effect,Skill=basic,Owner=CombatId(actor),Target=CombatId(target),Coefficient=SkillConfig.Skills[basic].Coefficient,OriginCast=originCast});
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
    /// <summary>只检查实际自动售卖的背包物品，与售卖结算共用配置条件。</summary>
    internal bool HasAutoSellItems=>inventory.Any(e=>e.Value>0&&Config.ItemTypes.Single(t=>t.Id==Config.Items.Single(i=>i.Id==e.Key.ItemId).TypeId).AutoSell);

    /// <summary>当前仅实现 HP；MP 接入后须同时检查和恢复。负面效果按现有技能目标声明识别。</summary>
    internal bool NeedsTownHealing=>Heroes.Any(h=>h.Hp<h.MaxHp)||skillState.Buffs.Any(b=>b.Target>=0&&b.Target<Heroes.Count&&b.Remaining>0&&Config.Skills.Buffs[b.Id].Target=="enemy");

    internal void RestoreTownHealth()
    {
        skillState.Buffs.RemoveAll(b=>b.Target>=0&&b.Target<Heroes.Count&&Config.Skills.Buffs[b.Id].Target=="enemy");
        foreach(var h in Heroes){h.Hp=h.MaxHp;h.Timer=0;}
    }
    internal void Note(string message) { MarkChanged();Log.Add(message); if(Log.Count>40) Log.RemoveAt(0); }
}
