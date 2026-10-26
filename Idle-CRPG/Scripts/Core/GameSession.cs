using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed class Actor
{
    public string SpecId = "";
    /// <summary>永久英雄实例身份，职业SpecId不能代替实例。</summary>
    public string HeroInstanceId="",SummonOwnerInstanceId="";
    /// <summary>后续装备/星图评级贡献预留；自然评级仍由等级确定，AR与DR独立消费。</summary>
    public double AttackRatingBonus,DefenseRatingBonus;
  /// <summary>当前装备已计入 Attack/MaxHp 的攻击与生命总和；用于换装差量计算与读档一致性校验。</summary>
  public float EquipmentAttack,EquipmentHp;
    public string Name = "";
    public Vector2 Position;
    public float Hp, MaxHp, Attack, Defense, Speed, Range, Cooldown, Timer;
    public int Level = 1;
    public int Experience,ExperienceFraction;
    public bool Boss;
    /// <summary>精英身份（C1-B B2）：仅由 TryApplyElite 在合法难度掷骰后标记，是"精英伤害/精英击杀收据"的唯一权威字段。</summary>
    public bool Elite;
    /// <summary>精英实例唯一令牌；用于一次性击杀收据，避免读档/重复结算二次领奖。</summary>
    public string EliteToken="";
    public bool Ranged;
    /// <summary>显式特殊索敌；默认沿用普通容量规则，仅隔离原型启用。</summary>
    public MonsterTargetPolicy TargetPolicy;
    public BattleAttributes Battle=new();
    public string[] SkillIds=Array.Empty<string>();
    /// <summary>已成功激活的持续光环；跨房保留，来源死亡或卸槽撤销，范围Buff仍属于短期战斗现场。</summary>
    public List<string> ActiveAuraSkills=new();
    public Dictionary<string,int> SkillLevels=new();
    public Dictionary<string,int> Talents=new();
    public List<RetiredTalent> RetiredTalents=new();
    public string[] LegacySkillIds=Array.Empty<string>();
    /// <summary>迁移前技能字段的只读JSON原件；包含旧投入、装配、加成、冷却和光环，不进入战斗消费者。</summary>
    public string LegacySkillData="";
    public bool StartingSkillGranted;
    /// <summary>仅旧档原本未学S1时免收补回基础攻击的一点，不挤占已投资权益；新英雄仍使用原初始点规则。</summary>
    public bool StarterPointWaived;
    public List<TalentBonus> TalentBonuses=new();
    public Dictionary<string,double> SkillSwapLocks=new();
    public int RewardXp;
    public float DropMultiplier=1, ValueMultiplier=1;
    /// <summary>非空表示独立召唤物；主人索引指向当前上阵英雄，召唤技能用于卸槽清理。</summary>
    public string SummonDefinitionId="",SummonSkill="";
    public int SummonOwner=-1;
    /// <summary>敌方预留槽来源为房内索引；激活一次后永不复用，死亡照常奖励。</summary>
    public int EnemySummonSource=-1;
    public string EnemySummonRule="";
    public bool EnemySummonActivated;
    /// <summary>Boss 阶段预留/激活实例的阶段标识（"poi:phase"）；空表示非阶段单位。</summary>
    public string BossPhaseId="";
    /// <summary>Boss 阶段预留槽位：未激活时为 true 且 0 血，激活一次后永不复用、死亡照常走奖励收据。</summary>
    public bool BossPhaseDormant;
    /// <summary>限时召唤剩余秒数，按固定20Hz逻辑步递减；0且定义为永续时表示不限期。属于召唤生命周期现场，随检查点保存。</summary>
    public float SummonRemaining;
    public bool Alive => Hp > 0;
    /// <summary>角色字段均为值类型或不可变字符串，复制后可独立用于检查点，不共享可变战斗状态。</summary>
    public Actor Copy(){var a=(Actor)MemberwiseClone();a.Battle=Battle.Copy();a.SkillIds=SkillIds.ToArray();a.ActiveAuraSkills=new(ActiveAuraSkills);a.SkillLevels=new(SkillLevels);a.Talents=new(Talents);a.RetiredTalents=new(RetiredTalents);a.LegacySkillIds=LegacySkillIds.ToArray();a.TalentBonuses=new(TalentBonuses);a.SkillSwapLocks=new(SkillSwapLocks);return a;}
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
    /// <summary>玩家侧独立召唤物；不并入 Heroes 或当前房间怪物列表，跨房保留数量与实际 HP。</summary>
    public List<Actor> Summons { get; } = new();
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
    /// <summary>非战斗跑图领队缓存；只在进入跑图、队伍或有效速度变化时重选，战斗不使用。</summary>
    private Actor? travelLeader;
    private readonly List<(Actor Hero,float Speed)> travelSpeedSnapshot=new();
    /// <summary>门前等待中的开门先锋（英雄或近战召唤物）；未等待或战斗时为 null。</summary>
    private Actor? doorOpener;
    /// <summary>门前等待累计秒数；只用于超时解除等待。</summary>
    private float doorWait;
    /// <summary>已改为领队直接开门（无近战先锋或等待超时），不再重新挑选先锋。</summary>
    private bool doorFallback;
    public bool MovingBetweenRooms => destination>=0;
    /// <summary>当前跑图领队；战斗期间沿用缓存但不再重新选举。只读诊断入口，不进入存档。</summary>
    public Actor? TravelLeader=>travelLeader;
    /// <summary>当前门前等待的开门先锋；没有等待时为 null。只读诊断入口，不进入存档。</summary>
    public Actor? DoorOpener=>doorOpener;
    /// <summary>行进时取沿路径最接近下一道门的存活成员，战斗镜头不使用此位置追踪。</summary>
    public Actor Leader => Heroes.Where(h=>h.Alive).OrderBy(h=>destination<0?0:Vector2.DistanceSquared(h.Position,phase==0?Floor.Rooms[CurrentRoom].DoorTowards(Floor.Rooms[destination]):Floor.Rooms[destination].DoorTowards(Floor.Rooms[CurrentRoom]))).FirstOrDefault()??Heroes[0];
    private float regroup;
    private List<PartyEntry> roomEntry=new();
    private List<Actor> roomEntrySummons=new();
    private uint roomEntryLootState;

    public GameSession(Config config, int seed, IEnumerable<string> ids)
    {
        Config = config; Seed = seed;
        Stars=new StarProgression(config.Stars);
        combatRandom=new PersistentRandom(unchecked((uint)seed^932701u));
        lootRandom=new PersistentRandom(unchecked((uint)seed^173831u));dungeonSeed=seed;
        var chosen = ids.ToArray();formationCapacityFloor=chosen.Length;
        if (chosen.Length < 1 || chosen.Length > 4) throw new ArgumentException("选择一至四名英雄，允许重复职业");
        Heroes.AddRange(chosen.Select(id => Actor.From(config.Heroes.Single(s => s.Id == id))));
        RunId=$"run-{seed}:0";
        for(int i=0;i<Heroes.Count;i++){var hero=Heroes[i];hero.HeroInstanceId=$"{RunId}:hero:{i+1}";config.ApplyCombatProfile(hero);if(config.Town!=null)GrantStarterSkill(hero);}
        Floors = Dungeon.Generate(seed,config);
        EnterFloor();
    }

    /// <summary>
    /// 世界远征入口。保留单地牢构造供独立模拟和检查使用。
    /// <c>chapter</c> 只供自动化/QA 指定起始章：会补齐"已走过章节"的空进度，使章节门禁与地图页自洽，
    /// 不代表玩家已通关；正式流程仍由 AdvanceChapter 逐章推进。
    /// </summary>
    public static GameSession CreateWorld(Config config,int seed,IEnumerable<string> ids,int chapter=0)
    {
        config=config.ChapterMaps[chapter];
        var game=new GameSession(config,seed,ids);game.Log.Clear();
        game.Floors.Clear();game.QaPrepareChapterBoot(chapter);
        game.World=new WorldCampaign(game.Config,game.DifficultyIndex);
        game.World.StartTownDeparture(game);
        game.Note("从初始村庄出发，自动远征已开始");return game;
    }
    /// <summary>预留科技系统命令，只有后续首次生成的房间受影响。</summary>
    public void SetSpawnModifiers(SpawnModifiers modifiers) {Config.Spawn.Validate(modifiers);if(Config.Dungeons.Any(d=>d.LevelMax+(long)Config.Spawn.Level-1+modifiers.LevelBonus>Config.MaxMonsterLevel))throw new ArgumentOutOfRangeException(nameof(modifiers));SpawnModifiers=modifiers;MarkChanged();}
    public bool RequestCastleChallenge(string castleId)
    {
        if(!CanChallengeCastle(castleId,out _)||World?.RequestCastle(castleId)!=true)return false;
        Note("已安排城堡挑战，完成当前地牢及返村交易后出发");return true;
    }
    internal void BeginDungeon(DungeonSpec dungeon,int visit)
    {
        dungeonEntrySerial=checked(dungeonEntrySerial+1);
        ResetBossPhases();
        // 每次开始新副本重置普攻及所有主动技能/换入冷却；跨房和换层不清换入等待。
        foreach(var hero in Heroes.Concat(reserveHeroes)){hero.Timer=0;hero.SkillSwapLocks.Clear();}
        ActiveDungeon=dungeon;Won=false;FloorIndex=0;regroup=0;ResetRescue();
        dungeonSeed=unchecked(Seed*397^visit*104729^dungeon.RegionId*7919);
        Floors=Dungeon.Generate(dungeonSeed,Config,Config.PoiProfiles.TryGetValue(dungeon.Id,out var profile)?profile.Floors:null);EnterFloor();
        Note($"进入{dungeon.Name} · 等级 {dungeon.LevelMin}–{dungeon.LevelMax}");
        ApplyGmEntryClear();
    }

    private void EnterFloor()
    {
        ClearVisualEvents();
        ResetTravelState();
        CurrentRoom = 0; destination = -1; phase = 0;
        var start = Floor.Rooms[0]; start.Revealed = true; start.Generated = true;
        var stairs=Floor.StairPosition(start);
        var inward=Vector2.Normalize(start.Center-stairs);
        var side=new Vector2(-inward.Y,inward.X);
        for (int i=0;i<Heroes.Count;i++) Heroes[i].Position = stairs+inward*.4f+side*(PartyFormation.SideOffset(i,Heroes.Count));
        PlaceSummonsNearParty(start);
        CaptureRoomEntry();
        Note($"抵达第 {FloorIndex+1} 层 · {Floor.Rooms.Count} 个房间");
    }

    /// <summary>固定步长推进；UI 倍速不得改变该步长。</summary>
    public void Tick(float dt)
    {
        if (dt <= 0 || dt > 0.1f) throw new ArgumentOutOfRangeException(nameof(dt));
        assetAuditTick++;
        // 瞬时位移标记按逻辑步重置：只剩异常恢复的脱队闪现使用，正常入场部署不再瞬移。
        summonBlinkTick.Clear();
        AdvanceSwapLocks(dt);AdvanceConsumables();World?.ObserveApprovalWait();
        if(World!=null && World.Stage!=CampaignStage.Dungeon){World.Tick(this,dt);if(World.InDungeonScene&&Floors.Count>0)FollowDungeonSummons(dt);return;}
        var tickFloor=Floor;bool fighting=!Floor.Rooms[CurrentRoom].Clear&&Floor.Rooms[CurrentRoom].Generated;
        var positions=Heroes.Select(h=>h.Position).ToArray();
        TickDungeon(dt);
        if(Floors.Count>0&&(World==null||World.InDungeonScene))FollowDungeonSummons(dt);
        if(fighting)tickFloor.BattleTicks++;else if(Heroes.Where((h,i)=>h.Position!=positions[i]).Any())tickFloor.WalkTicks++;
        // 通关后立即清空房间技能现场：返村途中不再属于地牢房间，残留动作会让正式快照校验失败。
        if(World!=null&&Won){World.CompleteDungeon(this);ResetSkillRoom();}
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
        // 脱战补员：只有永续召唤会在无敌人时自动施放；不影响救援、拾取和赶路的优先级。
        AutoSummonPermanent(dt);
        // 清房后救援优先于事件、探索、换层和最终通关。
        if (Rescue(dt)) return;
        if(CollectLoot(room,dt))return;
        if (room.Clear && !room.EventUsed)
        {
            if(room.Event!="")
            {
                Status="前往"+room.Event;
                var user=Heroes.Where(h=>h.Alive).OrderBy(h=>Vector2.DistanceSquared(h.Position,room.EventPosition)).First();
                if(Vector2.Distance(user.Position,room.EventPosition)>SpatialTolerance.InteractionRange){Move(user,room.EventPosition,dt);return;}
            }
            MarkChanged();
            room.EventUsed=true;
            if (room.Event == "营火") { foreach(var h in Heroes.Where(h=>h.Alive)) h.Hp=Math.Min(h.MaxHp,h.Hp+h.MaxHp*0.3f); Note("营火：存活队员恢复 30% 生命"); }
            if (room.Event == "宝箱") { GainExperience(8,"room_event"); Note("宝箱：公共XP +8"); }
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
            if (Heroes.Where(h=>h.Alive).Any(h=>Vector2.Distance(h.Position,stairs)>SpatialTolerance.InteractionRange)){regroup=0;return;}
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
            // 进入跑图状态时强制刷新领队并清空上一段门前等待，避免沿用旧队伍或旧先锋。
            ResetDoorWait();RefreshTravelLeader(true);
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
        ClearSummons();ResetSkillRoom();
        Retries++;
        int region=World?.CurrentRegion??ActiveDungeon?.RegionId??Config.StartRegionId;
        Floors.Clear();ActiveDungeon=null;Won=false;
        FloorIndex=0; CurrentRoom=0; regroup=0; ResetRescue();ResetTravelState();
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

    /// <summary>领队实际移动速度：装备/被动加成走统一属性聚合，不读取战斗中的临时寒缓。</summary>
    private float TravelWalkSpeed(Actor hero)=>Math.Max(hero.Speed,(float)EffectiveValue(hero,"move_speed",hero.Speed));
    /// <summary>召唤物开门先锋沿用非战斗跟随速度（锚点为当前领队）；入场加速只在房间开启后由部署逻辑接管。</summary>
    private float SummonTravelSpeed(Actor summon)
    {
        float catchup=Config.Talents.Rule("summon_catchup_speed_pct")/100f;
        var leader=travelLeader is {Alive:true}?travelLeader:Heroes.FirstOrDefault(h=>h.Alive);
        return leader!=null?Math.Max(summon.Speed,TravelWalkSpeed(leader)*catchup):summon.Speed;
    }
    /// <summary>只在进入跑图、队伍组成变化或有效速度变化（超过0.01）时重选领队，避免每步抖动。</summary>
    private void RefreshTravelLeader(bool force)
    {
        bool changed=force||travelLeader==null||!travelLeader.Alive||!Heroes.Contains(travelLeader)||travelSpeedSnapshot.Count!=Heroes.Count;
        if(!changed)
            for(int i=0;i<Heroes.Count;i++)
                if(travelSpeedSnapshot[i].Hero!=Heroes[i]||Math.Abs(travelSpeedSnapshot[i].Speed-TravelWalkSpeed(Heroes[i]))>.01f){changed=true;break;}
        if(!changed)return;
        travelSpeedSnapshot.Clear();
        foreach(var hero in Heroes)travelSpeedSnapshot.Add((hero,TravelWalkSpeed(hero)));
        travelLeader=Heroes.Where(h=>h.Alive).OrderByDescending(TravelWalkSpeed).ThenBy(h=>Heroes.IndexOf(h)).FirstOrDefault()??Heroes.FirstOrDefault();
    }
    private Actor[] TravelOrder(Actor leader)=>new[]{leader}.Concat(Heroes.Where(h=>h.Alive&&!ReferenceEquals(h,leader))).ToArray();
    private void ResetDoorWait(){doorOpener=null;doorWait=0;doorFallback=false;}
    private void ResetTravelState(){travelLeader=null;travelSpeedSnapshot.Clear();ResetDoorWait();}
    private bool IsTravelMember(Actor actor)=>Heroes.Contains(actor)||Summons.Contains(actor);
    /// <summary>从存活、可行动且近战定位的己方单位中选离门最近者；可包含近战召唤物，不按职业名硬编码。</summary>
    private Actor? SelectDoorOpener(Vector2 door)
    {
        Actor? best=null;float bestDistance=float.MaxValue;
        foreach(var hero in Heroes)
        {
            if(!hero.Alive||hero.Ranged||Stunned(hero))continue;
            float distance=Vector2.DistanceSquared(hero.Position,door);
            if(distance<bestDistance){best=hero;bestDistance=distance;}
        }
        foreach(var summon in Summons)
        {
            if(!summon.Alive||SummonRole(summon)!="melee"||Stunned(summon))continue;
            if(summon.SummonOwner<0||summon.SummonOwner>=Heroes.Count||!Heroes[summon.SummonOwner].Alive)continue;
            float distance=Vector2.DistanceSquared(summon.Position,door);
            if(distance<bestDistance){best=summon;bestDistance=distance;}
        }
        return best;
    }
    /// <summary>未到本房门时先走到门口，越过后才朝目标落点前进；领队、队员和先锋共用连续走门路径。</summary>
    private static Vector2 DoorStep(Vector2 position,Vector2 exit,Vector2 goal,Vector2 dir)=>Vector2.Dot(position-exit,dir)>-.05f?goal:exit;
    /// <summary>沿用既有队列间距；落后超过追赶距离的队员临时提速，但不改写 Actor.Speed。</summary>
    private void MoveTravelOrder(Actor[] order,Vector2 goal,Vector2 exit,Vector2 dir,float dt,float spacing,float catchupDistance,float catchupPct,Actor? hold,Actor? exclude)
    {
        for(int i=0;i<order.Length;i++)
        {
            var actor=order[i];
            if(!actor.Alive||ReferenceEquals(actor,hold)||ReferenceEquals(actor,exclude))continue;
            float previousDistance=i>0?Vector2.Distance(actor.Position,order[i-1].Position):float.MaxValue;
            if(i>0&&previousDistance<spacing)continue;
            float speed=TravelWalkSpeed(actor);
            if(i>0&&previousDistance>catchupDistance)speed*=catchupPct;
            Move(actor,DoorStep(actor.Position,exit,goal,dir),dt,speed);
        }
    }

    /// <summary>跑图：最快上阵英雄领队；抵达有内容房间门外后等待最近近战先锋开门，
    /// 无先锋或超时才由领队直接开门。战斗期间不走这里，因此不会重选领队或干预战斗 AI。</summary>
    private void Travel(float dt)
    {
        var a=Floor.Rooms[CurrentRoom]; var b=Floor.Rooms[destination];
        var dir=Vector2.Normalize(b.Center-a.Center);
        var exit=a.DoorTowards(b); var entry=b.DoorTowards(a);
        float spacing=Config.Talents.Rule("dungeon_queue_spacing_milli")/1000f;
        float catchupDistance=Config.Talents.Rule("party_catchup_distance_milli")/1000f;
        float catchupPct=Config.Talents.Rule("party_catchup_speed_pct")/100f;
        RefreshTravelLeader(false);
        var leader=travelLeader??Heroes.FirstOrDefault(h=>h.Alive);
        if(leader==null){destination=-1;phase=0;ResetTravelState();return;}
        var order=TravelOrder(leader);
        if(phase==0)
        {
            MoveTravelOrder(order,exit,exit,dir,dt,spacing,catchupDistance,catchupPct,null,null);
            if(Vector2.Distance(leader.Position,exit)<=SpatialTolerance.EntryTrigger)
            {
                var passage=Floor.Passages.Single(p=>(p.A==a.Id&&p.B==b.Id)||(p.A==b.Id&&p.B==a.Id));
                if(!passage.Revealed){passage.Revealed=true;MarkChanged();}
                phase=1;ResetDoorWait();
            }
            return;
        }
        if(b.Clear)
        {
            // 已清空房间没有开门剧情：全队直接连续通过，不等待先锋。
            MoveTravelOrder(order,entry,exit,dir,dt,spacing,catchupDistance,catchupPct,null,null);
            if(Vector2.Distance(leader.Position,entry)<=SpatialTolerance.EntryTrigger)EnterTargetRoom(b,entry);
            return;
        }
        float waitOffset=Config.Talents.Rule("dungeon_door_wait_offset_milli")/1000f;
        var waitPoint=entry-dir*waitOffset;
        if(doorOpener==null)
        {
            // 领队走到门外等待位，其余队员继续靠拢；先锋只会在领队到位后选择一次。
            MoveTravelOrder(order,waitPoint,exit,dir,dt,spacing,catchupDistance,catchupPct,null,null);
            // 已在等待线上或更靠近门时同样视为到位，避免读档/手动构造场景倒退回等待位。
            if(Vector2.Dot(leader.Position-waitPoint,dir)<-SpatialTolerance.EntryTrigger)return;
            doorOpener=SelectDoorOpener(entry)??leader;
            doorWait=0;doorFallback=ReferenceEquals(doorOpener,leader);
        }
        if(!doorOpener.Alive||!IsTravelMember(doorOpener))
        {
            doorOpener=SelectDoorOpener(entry)??leader;
            doorWait=0;doorFallback=ReferenceEquals(doorOpener,leader);
        }
        if(doorFallback)
        {
            // 无可用近战先锋或等待超时：领队直接开门，其余队员只靠拢不抢门。
            doorOpener=leader;
            MoveTravelOrder(order,waitPoint,exit,dir,dt,spacing,catchupDistance,catchupPct,null,leader);
            Move(leader,DoorStep(leader.Position,exit,entry,dir),dt,TravelWalkSpeed(leader));
        }
        else
        {
            // 领队停在门外等待位；先锋独自走到门口并率先进入，不等其他近战集合。
            MoveTravelOrder(order,waitPoint,exit,dir,dt,spacing,catchupDistance,catchupPct,leader,doorOpener);
            float speed=Summons.Contains(doorOpener)?SummonTravelSpeed(doorOpener):TravelWalkSpeed(doorOpener);
            Move(doorOpener,DoorStep(doorOpener.Position,exit,entry,dir),dt,speed);
        }
        if(Vector2.Distance(doorOpener.Position,entry)<=SpatialTolerance.EntryTrigger)
        {
            EnterTargetRoom(b,entry);
            return;
        }
        if(!doorFallback)
        {
            doorWait+=dt;
            if(doorWait*1000f>=Config.Talents.Rule("dungeon_door_wait_timeout_milli"))
            {doorFallback=true;doorOpener=leader;doorWait=0;}
        }
    }

    /// <summary>先锋/领队抵达门内后切换当前房间；保留连续移动，召唤物按实际坐标进入入场部署。</summary>
    private void EnterTargetRoom(Room room,Vector2 entry)
    {
        Reveal(room,entry);
        CurrentRoom=room.Id; destination=-1;
        // 召唤物不再随跨房瞬移：入场部署会让仍在旧房间或走廊的存活召唤物沿真实房门加速进入。
        // 进房检查点仍按当前存活召唤物记录实际坐标，读档后继续同一段连续移动。
        // 保留领队缓存：战斗期间不重新选举，清房后召唤物仍以同一领队为跟随锚点。
        ResetDoorWait();
        CaptureRoomEntry();
        StartCombatEntry(room,entry);
    }

    /// <summary>揭示并按快照生成房间内容。entry 为本次实际使用的房门位置，用于按真实进房方向
    /// 计算入口内侧避让区，不能固定排除房间某一侧。</summary>
    private void Reveal(Room room,Vector2 entry)
    {
        room.Revealed=true;
        if(room.Generated) return;
        room.Generated=true;
        var rule=Config.Spawn;var modifiers=SpawnModifiers;
        int levelMin=MonsterLevelMin,levelMax=MonsterLevelMax;
        var plan=ActiveDungeon==null?null:Config.FloorPlans.SingleOrDefault(p=>p.Poi==ActiveDungeon.Id&&p.Difficulty==DifficultyIndex&&p.Index==FloorIndex+1);
        // C2-A：逐层遭遇计划只提供"内容偏置"，数量仍夹在 spawn_rule 区间，等级仍取 Run.[Lmin,Lmax]。
        var encounter=ActiveDungeon==null?null:Config.EncounterFor(ChapterIndex+1,ActiveDungeon.Id,FloorIndex+1);
        room.SnapshotLevel=levelMin;
        var rng=new Random(unchecked(dungeonSeed*397 ^ FloorIndex*7919 ^ room.Id*104729));
        int count=rng.Next(rule.Min+modifiers.MinBonus,rule.Max+modifiers.MaxBonus+1);
        // 逐层节奏只在 spawn_rule 的合法区间内加减，且不覆盖 GM 临时数量输入。
        if(encounter!=null)count=Math.Clamp(count+encounter.CountBias,rule.Min+modifiers.MinBonus,rule.Max+modifiers.MaxBonus);
        // GM 数量是临时输入；已生成房间仍沿用原快照，BOSS 占总数中的一个名额。
        count=Math.Clamp(count+Gm.RoomCountBonus,1,1000);
        var inward=Vector2.Normalize(room.Center-entry);
        if(!float.IsFinite(inward.X)||inward==Vector2.Zero)inward=Vector2.UnitX;
        float passDepth=Config.Talents.Rule("combat_entry_pass_depth_milli")/1000f;
        // 普通刷怪保留四边0.8U留白，并额外避让入口内侧通行区；重试有界，失败时确定性回退，
        // 数量始终等于配置/快照值，不因避让改变怪物数量。
        Vector2 SpawnPoint()
        {
            for(int attempt=0;attempt<32;attempt++)
            {
                var p=room.Center+new Vector2(((float)rng.NextDouble()*2-1)*(room.HalfWidth-.8f),((float)rng.NextDouble()*2-1)*(room.HalfHeight-.8f));
                if(Vector2.Dot(p-entry,inward)>=passDepth-.0001f)return p;
            }
            var fallback=Vector2.Clamp(entry+inward*passDepth,room.Center-room.HalfSize+new Vector2(.8f),room.Center+room.HalfSize-new Vector2(.8f));
            return fallback;
        }
        // 城堡威胁组合：按"身份档位循环"逐格取身份，总数与等级仍由 spawn_rule 与 Run 区间决定。
        string areaId=ActiveDungeon==null?"":Config.AreaPart(ActiveDungeon.RegionId).Id;
        string[] composition=encounter==null||encounter.Composition==""?Array.Empty<string>():encounter.Composition.Split(';',StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<count;i++)
        {
            bool boss=room.Boss&&i==count-1&&(plan==null||plan.BossActors==1);
            // 世界远征按本次地牢所在统治区的投放池抽取身份；无池（单地牢模拟、legacy）仍用原全局怪物表。
            UnitSpec spec;
            if(ActiveDungeon==null)spec=Config.Monsters[rng.Next(Config.Monsters.Count)];
            else if(!boss&&composition.Length>0)
                spec=Config.PickSpawnMonsterOfArchetype(rng,areaId,composition[i%composition.Length] switch {"M"=>"melee","R"=>"ranged","H"=>"heavy",_=>"support"});
            else spec=Config.PickSpawnMonster(rng,areaId,encounter?.PoolBias??"mixed");
            var m=rule.Create(spec,rng.Next(levelMin,levelMax+1),boss,modifiers);
            m.Position=SpawnPoint();
            Config.ApplyCombatProfile(m);
            // 内容层：行为偏移（范围/猎手/支援）与 Boss 技能包；不改等级、奖励与房间预算。
            Config.ApplyMonsterContent(m,boss,ActiveDungeon?.Id??"");
            if(m.Boss&&ActiveDungeon!=null&&Config.PoiProfiles.TryGetValue(ActiveDungeon.Id,out var poi)&&poi.Boss!="")
                // 有真实阶段内容的章末 Boss 不再挂"（原型）"；其余占位名保持显式标注。
                m.Name=Config.BossPhasesOf(Config.ChapterIndex+1,ActiveDungeon.Id).Count>0?poi.Boss:poi.Boss+"（原型）";
            room.Monsters.Add(m);
            room.Origins.Add(new MonsterOrigin(m.SpecId,m.Level,m.Boss,m.Position,modifiers));
        }
        // 阶段预留槽位只在此刻（首次生成）与读档重建时创建，之后不再追加，因此复刷与读档都不会多刷 Boss。
        if(ActiveDungeon!=null)
            PrepareBossPhaseActors(Config,room,ActiveDungeon.Id,FloorIndex,room.Id,room.Monsters.Count==0?levelMin:room.Monsters.Max(m=>m.Level));
        // 精英词缀：只在世界远征投掷，Boss 房不参与，每房最多一个。
        if(ActiveDungeon!=null&&!room.Boss)TryApplyElite(room.Monsters,rng,DifficultyIndex,encounter?.EliteBias??0);
        if(!room.Boss && rng.NextDouble()<Config.EventChance) room.Event=rng.Next(2)==0?"营火":"宝箱";
        PrepareEnemySummons(Config,room);
        Floor.ActualSpawned+=room.Monsters.Count(m=>m.Alive);
        Note($"进入房间 {room.Id+1}：{room.Monsters.Count} 敌人 · 等级 {levelMin}–{levelMax}"+(room.Boss?" · BOSS":""));
    }

    private void Fight(Room room,float dt)
    {
        if(CombatEntryRemaining>0)
        {
            skillState.Targeting.EntryElapsed+=dt;
            // 警觉窗口只限制敌人 AI 启动；召唤物未到位不会被强制闪现，继续由入场部署按真实路径推进。
            Status=$"入场展开 · 敌人警觉 {CombatEntryRemaining:0.0}秒";
        }
        effectBudgetRemaining=SkillConfig.EffectBudget;
        PruneSummons();PruneEnemySummons();
        // Boss 阶段：每帧最多推进一段，先于技能推进，使新阶段技能当帧即可进入候选。
        AdvanceBossPhases(room);
        AdvanceSkills(dt);
        DrainEffects();
        threatIntents.Clear();
        if(CombatEntryRemaining<=0)PrepareMonsterTargets(dt);
        foreach(var h in Heroes.Where(h=>h.Alive)) Act(h,room.Monsters,dt,room);
        foreach(var s in Summons.Where(s=>s.Alive)) Act(s,room.Monsters,dt,room);
        if(CombatEntryRemaining<=0)foreach(var m in room.Monsters.Where(m=>m.Alive).ToArray()) Act(m,Heroes,dt,room);
        DrainEffects();
        SampleCombatStats(room,dt);
        if(room.Clear){if(!skillPreviewMode)ResetSkillRoom();}else SynchronizeAuras();
    }
    private void Act(Actor actor,List<Actor> opponents,float dt,Room room)
    {
        if(!InCombatRoom(actor)||entryMoved.Contains(actor)||Stunned(actor))return;
        // 入口通行未完成的己方单位不得停下攻击；推进与接战豁免由 AdvanceRoomEntry 统一安排。
        if(IsFriendlyActor(actor)&&EntryPassageBlocked(actor))return;
        actor.Timer=Math.Max(0,actor.Timer-dt*(float)(1+EffectiveAttributes(actor).AttackSpeed));
        if(RunRetreat(actor,dt,room))return;
        if(RunActiveSkill(actor))return;
        var target=IsFriendlyActor(actor)?FriendlyCombatTarget(actor,opponents):OrdinaryMonsterTarget(actor);
        if(target==null||!InCombatRoom(target)) return;
        double engagementRange=EffectiveValue(actor,"attack_range",actor.Range);
        // 已装配近战主动就绪时先接近其施放距离，避免远程普攻射程使至圣斩永远无法使用。
        foreach(string id in actor.SkillIds)if(SkillConfig.Skills.TryGetValue(id,out var equipped)&&equipped.Kind=="ActiveSkill"&&SkillCooldown(actor,id)<=0)engagementRange=Math.Min(engagementRange,EffectiveSkillRange(actor,id));
        if(Vector2.Distance(actor.Position,target.Position)>engagementRange)
        {
            Move(actor,target.Position,dt,(float)EffectiveValue(actor,"move_speed",actor.Speed)*MoveMultiplier(actor));
            // 进房门之后的战斗局限于当前房间，允许门口队员跨过边界。
            actor.Position=Vector2.Clamp(actor.Position,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
            return;
        }
        if(actor.Timer>0) return;
        if(IsFriendlyActor(actor))RecordFirstAttack(actor);
        actor.Timer=(float)EffectiveValue(actor,"basic_cooldown",actor.Cooldown);
        // 召唤物复用玩家侧普攻与伤害缩放；其战斗力由召唤定义快照决定，不读取怪物技能。
        string basic=IsFriendlyActor(actor)?"basic_player":"basic_monster";
        long originCast=++skillState.NextId;
        foreach(string effect in SkillConfig.Skills[basic].Effects)Enqueue(new(){Effect=effect,Skill=basic,Owner=CombatId(actor),Target=CombatId(target),Coefficient=SkillConfig.Skills[basic].Coefficient,OriginCast=originCast});
        DrainEffects();
    }
    internal static void Move(Actor a,Vector2 target,float dt,float? speed=null)
    {
        var delta=target-a.Position; float distance=delta.Length();
        // 到达阈值内直接吸附目标：否则“就地不动”与“房间归属容差”会互相矛盾，
        // 站在门线前 0.0001~0.001U 的单位既进不了房也走不动（AUD-001）。
        if(distance<=SpatialTolerance.MoveArrival){a.Position=target;return;}
        a.Position+=delta/distance*Math.Min(distance,(speed??a.Speed)*dt);
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
        // 进房检查点先淘汰上一房已死亡召唤，再按队伍落位并记录实际 HP；房内新召唤不进入检查点。
        RemoveDeadSummonsAtRoomEntry();
        ResetSkillRoom();
        roomEntryCombatState=combatRandom.State;
        roomEntry=Heroes.Select(h=>new PartyEntry(h.Position,h.Hp)).ToList();
        CaptureSummonEntry();roomEntryLootState=lootRandom.State;
    }
    /// <summary>清房后先救援，再逐份战利品走近领取，最后处理房间事件。掉落金币和物品在领取时入账。</summary>
    private bool CollectLoot(Room room,float dt)
    {
        var loot=room.Loot.FirstOrDefault(l=>!l.Collected);
        if(loot==null)return false;
        Status="前往拾取战利品";
        var user=Heroes.Where(h=>h.Alive).OrderBy(h=>Vector2.DistanceSquared(h.Position,loot.Position)).First();
        if(Vector2.Distance(user.Position,loot.Position)>SpatialTolerance.InteractionRange){Move(user,loot.Position,dt);return true;}
        // C1-B F01 / C2-B PO03：击杀金币乘率只在入账点应用一次（星图 + 药剂），账本以入账前后差额记账。
        double goldRate=StarKillGoldPct+PotionKillGoldPct;
        long gained=loot.Gold<=0?0:checked(loot.Gold+(long)Math.Ceiling(loot.Gold*goldRate));
        long goldBefore=Gold;loot.Collected=true;Gold=checked(Gold+gained);if(gained>0)AuditCurrency("loot_pickup","receive","gold",goldBefore,Gold);
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
        // 回城交易同时结算次优装备：保留更优/跨属性候选，只卖出同属性不优于已穿者（金币由本函数统一入账）。
        total=checked(total+SellInferiorEquipment());
        long before=Gold;Gold=checked(Gold+total);if(total>0)AuditCurrency("village_trade","sell","gold",before,Gold);return total;
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
