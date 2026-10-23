using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>普通近战的优先预约、可攻击溢出与远程主锁；不表示受击人数上限。</summary>
public enum MonsterLockKind { Normal, Overflow, Ranged, Forced, Special }
/// <summary>后排优先只改变普通主锁选择，不占Normal容量；Forced仍优先。</summary>
public enum MonsterTargetPolicy { Ordinary, BacklinePriority }
public sealed class MonsterTargetLock
{
    public int Owner,Target;
    public MonsterLockKind Kind;
    public bool NeedsReevaluation;
    public double NextEvaluation;
}
/// <summary>只属于当前房间的临时态；正式存档清空，完整测试快照保存以复现决策。</summary>
public sealed class CombatTargetingState
{
    public List<MonsterTargetLock> Locks=new();
    public Dictionary<int,int> FriendlyTargets=new();
    public int CandidateMask,Cursor;
    public int FloorIndex=-1,RoomIndex=-1;
    public int[] Capacities=Array.Empty<int>();
    public double Clock;
    public bool EntryPrepared;
    public int DeployedMask;
    public double EntryElapsed;
    public Vector2 EntryAnchor,EntryDirection;
    /// <summary>入口通行按友方连续索引（英雄在前、召唤物在后）独立记录：已完成入房通行、以及提前接战豁免。
    /// 两者都属于本房短期现场，正式检查点清空，完整快照保存以复现决策。</summary>
    public int EntryPassMask,EntryEngagedMask;
}
public sealed partial class GameSession
{
    // 索引仅指向本步权威Locks中的对象，不保存第二份目标或预约账本。
    private readonly Dictionary<int,MonsterTargetLock> monsterLockIndex=new();
    private readonly HashSet<Actor> entryMoved=new();
    public IEnumerable<(int Owner,int Target,MonsterLockKind Kind)> MonsterTargets=>skillState.Targeting.Locks.Select(l=>(l.Owner,l.Target,l.Kind));
    /// <summary>入口通行深度、近距离威胁距离、敌方基础警觉与兜底上限；数值均为原型参数。</summary>
    private float EntryPassDepth=>Config.Talents.Rule("combat_entry_pass_depth_milli")/1000f;
    private float EntryThreatRange=>Config.Talents.Rule("combat_entry_threat_range_milli")/1000f;
    private double EntryAlertBase=>Config.Talents.Rule("combat_entry_delay_milli")/1000d;
    private double EntryAlertCap=>Config.Talents.Rule("combat_entry_alert_cap_milli")/1000d;
    /// <summary>仅有怪新房的短暂警觉窗口；已清理房间和走廊始终为0。
    /// 基础警觉无条件保留；之后还要等小队前方真正入房通行，但没有超过兜底上限的等待。</summary>
    public double CombatEntryRemaining
    {
        get
        {
            var s=skillState.Targeting;if(!s.EntryPrepared)return 0;
            if(s.EntryElapsed<EntryAlertBase)return EntryAlertBase-s.EntryElapsed;
            if(EntryAlertReleased)return 0;
            return Math.Max(0,EntryAlertCap-s.EntryElapsed);
        }
    }
    /// <summary>敌方启动条件：基础警觉已过，且前方已入房约通行深度、已发生近距离遭遇，或已到兜底上限。
    /// 只参考小队前方进度，不等待全部队友和召唤物。</summary>
    private bool EntryAlertReleased
    {
        get
        {
            var s=skillState.Targeting;
            if(!s.EntryPrepared)return true;
            if(s.EntryElapsed+.000001<EntryAlertBase)return false;
            if(s.EntryElapsed+.000001>=EntryAlertCap)return true;
            var room=Floor.Rooms[CurrentRoom];
            foreach(var hero in Heroes)
            {
                if(!hero.Alive||!InCombatRoom(hero))continue;
                if(EntryDepth(hero.Position)+.0001f>=EntryPassDepth)return true;
                if(room.Monsters.Any(m=>m.Alive&&InCombatRoom(m)&&Vector2.Distance(m.Position,hero.Position)<=EntryThreatRange))return true;
            }
            return false;
        }
    }
    /// <summary>单位沿当前入口门线朝内方向的入房深度（世界单位）；门外为负，适配上下左右四种入口。</summary>
    public float EntryDepth(Vector2 position)
    {
        var s=skillState.Targeting;
        return Vector2.Dot(position-s.EntryAnchor,s.EntryDirection);
    }
    /// <summary>单个单位是否已不再受入口通行限制：完成通行、提前接战豁免，或沿用旧的展开完成掩码。
    /// 按友方连续索引独立判定，队友开始战斗不会顺带放行队尾。</summary>
    private bool EntryGateCleared(Actor actor)
    {
        int index=FriendlyIndex(actor);if(index<0)return true;
        var s=skillState.Targeting;int bit=1<<index;
        return ((s.DeployedMask|s.EntryPassMask|s.EntryEngagedMask)&bit)!=0;
    }
    /// <summary>是否已按提前接战豁免结束通行，接战后不再补做通行。</summary>
    private bool EntryEngagedExempt(Actor actor)
    {
        int index=FriendlyIndex(actor);
        return index>=0&&(skillState.Targeting.EntryEngagedMask&(1<<index))!=0;
    }
    /// <summary>入口浅区近距离威胁：只统计本房存活怪物的真实间距，屏幕错位不参与判定。</summary>
    private bool EntryThreatened(Actor actor,Room room)=>room.Monsters.Any(m=>m.Alive&&InCombatRoom(m)&&Vector2.Distance(m.Position,actor.Position)<=EntryThreatRange);
    /// <summary>正在发生的攻击/施法、已在飞来的打击或已被怪物锁定同样允许立即接战，不只依赖距离阈值。</summary>
    private bool EntryEngagedByCombat(Actor actor)
    {
        int id=CombatId(actor);
        if(skillState.Actions.Any(a=>a.Owner==id&&(a.Active!=""||a.Windup>0||a.Recovery>0)))return true;
        if(skillState.Flights.Any(f=>f.Target==id))return true;
        int index=FriendlyIndex(actor);
        return index>=0&&skillState.Targeting.Locks.Any(l=>l.Target==index||l.Owner==id);
    }
    /// <summary>入场通行未完成的己方单位不得停下战斗；由入场推进继续前进，或按豁免转入正式战斗。
    /// 解锁条件只看“通行深度/提前接战”，与房间归属容差无关；房间归属统一由 SpatialTolerance 判定，
    /// 因此不会出现“已到达门线（在房内）但仍被通行门禁拦住且无法再移动”的组合。</summary>
    private bool EntryPassageBlocked(Actor actor)
    {
        var s=skillState.Targeting;
        if(!s.EntryPrepared||Floors.Count==0)return false;
        var room=Floor.Rooms[CurrentRoom];
        if(room.Clear||!InCombatRoom(actor))return false;
        // 战斗中临时召唤不参与已认领的入场部署，直接按正式战斗AI行动。
        if(actor.SummonDefinitionId!=""&&!summonDeployGoals.ContainsKey(actor))return false;
        return !EntryGateCleared(actor);
    }
    private void StartCombatEntry(Room room,Vector2 anchor)
    {
        if(room.Clear)return;
        var state=skillState.Targeting;state.EntryPrepared=true;state.EntryElapsed=0;state.EntryAnchor=anchor;
        state.FloorIndex=FloorIndex;state.RoomIndex=CurrentRoom;
        state.EntryDirection=Vector2.Normalize(room.Center-anchor);
        if(!float.IsFinite(state.EntryDirection.X))state.EntryDirection=Vector2.UnitX;
    }
    /// <summary>容量按统一战斗属性聚合，向下取整；0容量目标仍完全合法可攻击。</summary>
    public int EngagementCapacity(Actor actor)
    {
        // 召唤物不属于职业配置，接敌容量直接取召唤定义；仍允许战斗 Buff 修正。
        double basis=actor.SummonDefinitionId!=""&&SkillConfig.Summons.TryGetValue(actor.SummonDefinitionId,out var summon)?summon.EngagementCapacity:Config.BaseEngagementCapacity(actor);
        double value=Floors.Count>0&&(World==null||World.Stage==CampaignStage.Dungeon)?EffectiveValue(actor,"engagement_capacity",basis):Config.Attributes.Aggregate("engagement_capacity",basis+PassiveTalentValue(actor,"engagement_capacity"),Array.Empty<BuffAttributeSpec>());
        return checked((int)Math.Floor(value));
    }
    /// <summary>按实际坐标判断本房空间资格；调用者的英雄/怪物列表另限定当前房间成员。
    /// 房间归属容差与位移到达阈值配套（见 SpatialTolerance），站在门线上的单位视为已入房。</summary>
    public bool InCombatRoom(Actor actor)=>actor.Alive&&SpatialTolerance.InsideRoom(Floor.Rooms[CurrentRoom],actor.Position);
    /// <summary>队尾继续连续跨门，不夹取或瞬移进房；进房检查点的位置足以恢复这一过程。
    /// 入口通行按单位独立判定：未完成约通行深度的单位继续沿门轴朝内推进；
    /// 只有近距离威胁、正在发生的攻击或被锁定才允许提前接战，接战后不再补做通行。</summary>
    private void AdvanceRoomEntry(Room room,float dt)
    {
        entryMoved.Clear();bool spread=!room.Clear&&skillState.Targeting.EntryPrepared;
        if(!spread)
        {
            // 非入场窗口：房内成员交回正式战斗AI，走廊/上一房间成员仍由跑图与跟随逻辑处理。
            foreach(var hero in Heroes)if(!InCombatRoom(hero))entryMoved.Add(hero);
            return;
        }
        var state=skillState.Targeting;
        for(int heroIndex=0;heroIndex<Heroes.Count;heroIndex++)
        {
            var hero=Heroes[heroIndex];
            if(!hero.Alive)continue;
            bool inside=InCombatRoom(hero);int bit=1<<heroIndex;
            if(EntryGateCleared(hero))
            {
                // 已通行或已提前接战：进入房间后交回正式战斗AI，不再补做落位。
                if(inside&&room.Monsters.Any(InCombatRoom))state.DeployedMask|=bit;
                continue;
            }
            if(inside)
            {
                bool byCombat=EntryEngagedByCombat(hero),byThreat=!byCombat&&EntryThreatened(hero,room);
                if(byCombat||byThreat)
                {
                    state.EntryEngagedMask|=bit;state.DeployedMask|=bit;
                    RecordEarlyEngage(byCombat?"战斗锁定":"近距威胁");
                    EngagementEvent($"#{heroIndex} {hero.Name}：入口浅区提前接战（{(byCombat?"施法/被锁定":"近距威胁")}），通行豁免");
                    continue;
                }
            }
            if(inside&&EntryDepth(hero.Position)+.0001f>=EntryPassDepth){state.EntryPassMask|=bit;state.DeployedMask|=bit;continue;}
            // 即使仍在走廊或上一房间，也先计算房内推进点，再沿真实房门连续进入，不直线穿墙。
            // 门线只是路径点，不能作为终点，否则位移系统会判定“已到达”而不入房（AUD-001）。
            var goal=EntryAdvancePoint(hero,room,heroIndex);
            Move(hero,inside?goal:EntryMoveTarget(hero.Position,room,goal),dt);
            entryMoved.Add(hero);
        }
        // 召唤物与英雄共用同一入场窗口；入场逻辑独占其移动权，完成后交回正式战斗 AI。
        AdvanceEntrySummons(room,dt);
    }
    /// <summary>入口通行推进点：沿门轴朝内取“通行深度”与原展开纵深的较大值，横向仍按远近程车道错开。
    /// 这是通行限制而非固定部署坐标，允许在到达前因真实威胁提前接战。</summary>
    private Vector2 EntryAdvancePoint(Actor actor,Room room,int laneIndex)
    {
        var state=skillState.Targeting;var side=new Vector2(-state.EntryDirection.Y,state.EntryDirection.X);
        // 纵深/间距按攻击类型分组；接敌容量只决定怪物预约，不能把远程法系推入前排。
        string role=actor.Ranged?"ranged":"melee";
        float depth=Math.Max(Config.Talents.Rule($"combat_entry_{role}_depth_milli")/1000f,EntryPassDepth);
        float spacing=Config.Talents.Rule($"combat_entry_{role}_spread_milli")/1000f;
        int lane=laneIndex==0?0:(laneIndex+1)/2*(laneIndex%2==1?-1:1);
        var goal=state.EntryAnchor+state.EntryDirection*depth+side*(lane*spacing);
        return Vector2.Clamp(goal,room.Center-room.HalfSize+new Vector2(.4f),room.Center+room.HalfSize-new Vector2(.4f));
    }
    private readonly Queue<string> engagementEvents=new();
    /// <summary>GM只读诊断：入口门线、朝内方向、通行边界、单位深度/停止原因与敌方警觉，再附唯一预约账本。</summary>
    public string EngagementDiagnostics()
    {
        if(Floors.Count==0)return "无战斗现场";
        var lines=EntryDiagnostics().ToList();
        lines.AddRange(CombatStatsDiagnostics());
        if(skillDecisions.Count>0)
        {
            lines.Add("技能AI决策（本房最近记录）：");
            lines.AddRange(skillDecisions);
        }
        for(int i=0;i<FriendlyCount;i++)if(FriendlyAt(i) is {Alive:true} a&&EngagementCapacity(a)>0)
            lines.Add($"{a.Name} {skillState.Targeting.Locks.Count(l=>l.Target==i&&l.Kind==MonsterLockKind.Normal)}/{EngagementCapacity(a)}："+string.Join("，",skillState.Targeting.Locks.Where(l=>l.Target==i).Select(l=>$"#{l.Owner} {l.Kind}")));
        return string.Join("\n",lines.Concat(engagementEvents));
    }
    /// <summary>入场通行与敌方警觉的只读诊断；不写入正式存档，也不参与任何判定。</summary>
    public IEnumerable<string> EntryDiagnostics()
    {
        if(Floors.Count==0)return Array.Empty<string>();
        var room=Floor.Rooms[CurrentRoom];var s=skillState.Targeting;
        if(!s.EntryPrepared)
            return new[]{room.Clear?"入场：当前房间已清理，无通行限制":"入场：尚未进入有活怪房间"};
        var lines=new List<string>
        {
            $"入场：门线({s.EntryAnchor.X:0.00},{s.EntryAnchor.Y:0.00}) 朝内({s.EntryDirection.X:0.00},{s.EntryDirection.Y:0.00}) 通行边界 {EntryPassDepth:0.00}U 近距威胁 {EntryThreatRange:0.00}U",
            $"敌方警觉：{(CombatEntryRemaining>0?$"进行中 剩余{CombatEntryRemaining:0.00}s":"已启动")} 基础{EntryAlertBase:0.00}s 兜底{EntryAlertCap:0.00}s",
            $"本房怪物 {room.Monsters.Count(m=>m.Alive)} 只：{(CombatEntryRemaining>0?"待机（警觉中）":"常规索敌")}"
        };
        for(int i=0;i<FriendlyCount;i++)
        {
            if(FriendlyAt(i) is not {Alive:true} actor)continue;
            string gate=EntryEngagedExempt(actor)?"已接战豁免":EntryGateCleared(actor)?"已通行":"通行中";
            string target=TargetText(actor);
            lines.Add($"{actor.Name} 坐标({actor.Position.X:0.00},{actor.Position.Y:0.00}) 深度{EntryDepth(actor.Position):0.00} {gate} 目标{target} 停止原因={EntryStopReason(actor,room,i)}");
        }
        return lines;
    }
    /// <summary>GM 诊断用的当前目标文本：友方读稳定目标，怪物读唯一预约账本。</summary>
    private string TargetText(Actor actor)
    {
        if(IsFriendlyActor(actor))
            return skillState.Targeting.FriendlyTargets.TryGetValue(CombatId(actor),out int id)?$"#{id}": "无";
        return monsterLockIndex.TryGetValue(CombatId(actor),out var locked)?$"#{locked.Target} {locked.Kind}":"无";
    }
    /// <summary>GM 用的移动/行动停止原因推断；只读现有状态，不改变任何决策。</summary>
    private string EntryStopReason(Actor actor,Room room,int index)
    {
        if(!InCombatRoom(actor))return "穿门或走廊";
        if(EntryPassageBlocked(actor))return "入口通行";
        bool acting=skillState.Actions.Any(a=>a.Owner==CombatId(actor)&&(a.Active!=""||a.Windup>0||a.Recovery>0));
        if(acting)return "技能动作";
        if(!IsFriendlyActor(actor))return ordinaryReason(actor);
        if(!skillState.Targeting.FriendlyTargets.TryGetValue(CombatId(actor),out int target))return "重新索敌";
        var enemy=CombatActor(target);
        if(enemy==null||!enemy.Alive)return "目标失效";
        double range=EffectiveValue(actor,"attack_range",actor.Range);
        return Vector2.Distance(actor.Position,enemy.Position)<=range+.0001?"攻击目标":"接近目标";

        string ordinaryReason(Actor monster)
        {
            if(!monsterLockIndex.TryGetValue(CombatId(monster),out var locked))return "无合法目标";
            var victim=FriendlyAt(locked.Target);
            if(victim==null||!victim.Alive)return "目标失效";
            double reach=EffectiveValue(monster,"attack_range",monster.Range);
            return Vector2.Distance(monster.Position,victim.Position)<=reach+.0001?$"攻击{locked.Kind}目标":"接近目标";
        }
    }
    private void EngagementEvent(string message)
    {
        engagementEvents.Enqueue(message);while(engagementEvents.Count>24)engagementEvents.Dequeue();
    }
    /// <summary>入场推进的最终目标：门线只是路径点，不能成为终点。
    /// 若位移系统已判定“到达门线”（剩余距离不超过到达阈值），改用房内推进点，
    /// 保证几何目标一定落在房间归属判定之内，而不是停在边界死区（AUD-001/AUD-002）。
    /// 只在贴边带内这样走，直线不会切墙；其余情况仍交给走廊/上一房间的路径点。</summary>
    private Vector2 EntryMoveTarget(Vector2 position,Room room,Vector2 roomGoal)
    {
        var waypoint=PartyDoorWaypoint(position,room,roomGoal);
        if(Vector2.Distance(position,waypoint)>SpatialTolerance.MoveArrival)return waypoint;
        return SpatialTolerance.NearRoomEdge(room,position)?roomGoal:waypoint;
    }
    private readonly HashSet<int> threatIntents=new();
    /// <summary>正常重新索敌时优先处理本房四单位内后排威胁；意图不占容量。</summary>
    private Actor? FriendlyCombatTarget(Actor actor,List<Actor> opponents)
    {
        var state=skillState.Targeting;
        if(state.FriendlyTargets.TryGetValue(CombatId(actor),out int old)&&CombatActor(old) is { } current&&opponents.Contains(current)&&InCombatRoom(current))return current;
        float range=Config.Talents.Rule("targeting_local_range_milli")/1000f;
        bool Threat(Actor enemy)=>!enemy.Ranged&&!enemy.Boss&&enemy.TargetPolicy==MonsterTargetPolicy.Ordinary&&monsterLockIndex.TryGetValue(CombatId(enemy),out var l)&&l.Kind==MonsterLockKind.Overflow&&FriendlyAt(l.Target) is {Ranged:true};
        var candidates=opponents.Where(InCombatRoom);
        Actor? chosen=null;
        int index=FriendlyIndexFromCombatId(CombatId(actor));
        if(!actor.Ranged&&EngagementCapacity(actor)>state.Locks.Count(l=>l.Kind==MonsterLockKind.Normal&&l.Target==index))
            chosen=candidates.Where(e=>Threat(e)&&Vector2.DistanceSquared(e.Position,actor.Position)<=range*range).OrderBy(e=>threatIntents.Contains(CombatId(e))).ThenBy(e=>Vector2.DistanceSquared(e.Position,actor.Position)).ThenBy(CombatId).FirstOrDefault();
        chosen??=candidates.OrderBy(e=>Vector2.DistanceSquared(e.Position,actor.Position)).ThenBy(CombatId).FirstOrDefault();
        if(chosen!=null){state.FriendlyTargets[CombatId(actor)]=CombatId(chosen);if(Threat(chosen))threatIntents.Add(CombatId(chosen));}
        return chosen;
    }
    /// <summary>直接近战命中后在唯一锁上原子承接，不改变其他交战者。</summary>
    private void TryMeleeEngagement(Actor front,Actor enemy)
    {
        int owner=CombatId(enemy),target=FriendlyIndexFromCombatId(CombatId(front));
        if(target<0||front.Ranged||enemy.Ranged||enemy.Boss||enemy.TargetPolicy!=MonsterTargetPolicy.Ordinary||!enemy.Alive||!InCombatRoom(front))return;
        if(!monsterLockIndex.TryGetValue(owner,out var l)||l.Kind!=MonsterLockKind.Overflow||FriendlyAt(l.Target) is not {Ranged:true})return;
        if(ActorBuffs(owner).Any(b=>SkillConfig.Buffs[b.Id].Mechanic=="forced"))return;
        if(Vector2.Distance(front.Position,enemy.Position)>EffectiveValue(front,"attack_range",front.Range)+.0001)return;
        if(skillState.Targeting.Locks.Count(x=>x.Kind==MonsterLockKind.Normal&&x.Target==target)>=EngagementCapacity(front))return;
        EngagementEvent($"切换 #{owner} {l.Target}→{target}：直接近战承接");
        l.Target=target;l.Kind=MonsterLockKind.Normal;l.NeedsReevaluation=false;
    }
    private void ReleaseCombatTargets(int id)
    {
        var locks=skillState.Targeting.Locks;
        int friendlyIndex=FriendlyIndexFromCombatId(id);
        skillState.Targeting.FriendlyTargets.Remove(id);
        foreach(int key in skillState.Targeting.FriendlyTargets.Where(p=>p.Value==id).Select(p=>p.Key).ToArray())skillState.Targeting.FriendlyTargets.Remove(key);
        foreach(var l in locks.Where(l=>l.Owner==id||friendlyIndex>=0&&l.Target==friendlyIndex))EngagementEvent($"解除 #{l.Owner}→{l.Target}：死亡或离场");
        if(locks.RemoveAll(l=>l.Owner==id||friendlyIndex>=0&&l.Target==friendlyIndex)==0)return;
        foreach(var l in locks)if(l.Kind==MonsterLockKind.Overflow)l.NeedsReevaluation=true;
        foreach(int owner in monsterLockIndex.Where(p=>p.Value.Owner==id||friendlyIndex>=0&&p.Value.Target==friendlyIndex).Select(p=>p.Key).ToArray())monsterLockIndex.Remove(owner);
    }
    private Actor? OrdinaryMonsterTarget(Actor actor)=>monsterLockIndex.TryGetValue(CombatId(actor),out var locked)?FriendlyAt(locked.Target):null;
    /// <summary>本步只对缺锁者初选；已正常接敌者不洗牌，事件触发的溢出重评受间隔及公平预算限制。</summary>
    private void PrepareMonsterTargets(float dt)
    {
        var state=skillState.Targeting;state.Clock+=dt;var room=Floor.Rooms[CurrentRoom];
        // 怪物 Owner ID 仍从英雄数开始；友方目标索引额外包含召唤物，两者不能混用。
        int monsterBase=Heroes.Count,count=FriendlyCount;
        if(state.RoomIndex>=0&&(state.RoomIndex!=CurrentRoom||state.FloorIndex!=FloorIndex))throw new InvalidOperationException("普通索敌现场不能跨房间复用");
        state.FloorIndex=FloorIndex;state.RoomIndex=CurrentRoom;
        var busy=skillState.Actions.Where(a=>a.Active!=""||a.Recovery>0).Select(a=>a.Owner).ToHashSet();
        var capacities=new int[count];int mask=0;
        for(int i=0;i<count;i++)if(FriendlyAt(i) is { } friendly&&InCombatRoom(friendly)){mask|=1<<i;capacities[i]=EngagementCapacity(friendly);}
        bool changed=state.CandidateMask!=mask||!state.Capacities.SequenceEqual(capacities);
        state.CandidateMask=mask;state.Capacities=capacities;
        int before=state.Locks.Count;
        bool Invalid(MonsterTargetLock l)=>l.Owner<monsterBase||l.Owner>=monsterBase+room.Monsters.Count||!room.Monsters[l.Owner-monsterBase].Alive||l.Target<0||l.Target>=count||(mask&(1<<l.Target))==0||l.Kind!=MonsterLockKind.Forced&&room.Monsters[l.Owner-monsterBase].Ranged!=(l.Kind==MonsterLockKind.Ranged);
        foreach(var l in state.Locks.Where(Invalid))EngagementEvent($"解除 #{l.Owner}→{l.Target}：死亡、离房或攻击类型失效");
        state.Locks.RemoveAll(Invalid);
        changed|=before!=state.Locks.Count;
        int ForcedTarget(int owner)
        {
            foreach(var buff in ActorBuffs(owner).Where(b=>SkillConfig.Buffs[b.Id].Mechanic=="forced").OrderByDescending(b=>b.Instance))
            {
                int target=FriendlyIndexFromCombatId(buff.Owner);
                if(target>=0&&(mask&(1<<target))!=0)return target;
            }
            return -1;
        }
        foreach(var locked in state.Locks.ToArray())
        {
            if(busy.Contains(locked.Owner))continue;
            int forced=ForcedTarget(locked.Owner);
            if(forced>=0)
            {
                if(locked.Kind!=MonsterLockKind.Forced||locked.Target!=forced){changed=true;EngagementEvent($"切换 #{locked.Owner} {locked.Target}→{forced}：强制索敌");}
                locked.Target=forced;locked.Kind=MonsterLockKind.Forced;locked.NeedsReevaluation=false;
            }
            else if(locked.Kind==MonsterLockKind.Forced){EngagementEvent($"解除 #{locked.Owner}→{locked.Target}：强制索敌结束");state.Locks.Remove(locked);changed=true;}
        }
        var occupied=new int[count];
        foreach(var locked in state.Locks.OrderBy(l=>l.Owner))
        {
            if(locked.Kind!=MonsterLockKind.Normal)continue;
            if(occupied[locked.Target]<capacities[locked.Target])occupied[locked.Target]++;
            else {EngagementEvent($"释放普通容量 #{locked.Owner}→{locked.Target}：容量降低，保留攻击目标");locked.Kind=MonsterLockKind.Overflow;locked.NeedsReevaluation=true;changed=true;}
        }
        if(changed)foreach(var l in state.Locks)if(l.Kind==MonsterLockKind.Overflow)l.NeedsReevaluation=true;
        monsterLockIndex.Clear();foreach(var l in state.Locks)monsterLockIndex.Add(l.Owner,l);
        float range=Config.Talents.Rule("targeting_local_range_milli")/1000f;
        double interval=Config.Talents.Rule("targeting_reevaluate_milli")/1000d;
        MonsterTargetLock? Select(int owner)
        {
            int forced=ForcedTarget(owner);if(forced>=0)return new(){Owner=owner,Target=forced,Kind=MonsterLockKind.Forced};
            var actor=room.Monsters[owner-monsterBase];int target=-1;float distance=float.MaxValue;
            bool ranged=actor.Ranged;
            if(!ranged&&actor.TargetPolicy==MonsterTargetPolicy.BacklinePriority)
            {
                // 候选限本房存活友方；远程职责优先，无后排则最近合法对象。
                for(int pass=0;pass<2&&target<0;pass++)
                    for(int i=0;i<count;i++)if((mask&(1<<i))!=0&&FriendlyAt(i) is { } friendly&&(pass==1||friendly.Ranged))
                    {float d=Vector2.DistanceSquared(actor.Position,friendly.Position);if(d<distance){target=i;distance=d;}}
                return target<0?null:new(){Owner=owner,Target=target,Kind=MonsterLockKind.Special};
            }
            if(!ranged)
                for(int i=0;i<count;i++)if((mask&(1<<i))!=0&&occupied[i]<capacities[i]&&FriendlyAt(i) is { } friendly)
                {float d=Vector2.DistanceSquared(actor.Position,friendly.Position);if(d<=range*range&&d<distance){target=i;distance=d;}}
            var kind=target>=0?MonsterLockKind.Normal:ranged?MonsterLockKind.Ranged:MonsterLockKind.Overflow;
            if(target<0)
            {
                // 优先其他合法对象；仅有满载前排时仍按最近目标攻击，不因无名额停机。
                for(int pass=0;pass<2&&target<0;pass++)
                    for(int i=0;i<count;i++)if((mask&(1<<i))!=0&&FriendlyAt(i) is { } friendly)
                    {
                        float d=Vector2.DistanceSquared(actor.Position,friendly.Position);
                        bool fullLocal=capacities[i]>0&&occupied[i]>=capacities[i]&&d<=range*range;
                        if(!ranged&&pass==0&&fullLocal)continue;
                        if(d<distance){target=i;distance=d;}
                    }
            }
            if(target<0)return null;
            if(kind==MonsterLockKind.Normal)occupied[target]++;
            return new(){Owner=owner,Target=target,Kind=kind,NextEvaluation=state.Clock+interval};
        }
        for(int i=0;i<room.Monsters.Count;i++)
        {
            int owner=monsterBase+i;
            if(!room.Monsters[i].Alive||monsterLockIndex.ContainsKey(owner)||busy.Contains(owner))continue;
            var selected=Select(owner);if(selected==null)continue;
            state.Locks.Add(selected);monsterLockIndex.Add(owner,selected);
            EngagementEvent($"建立 #{owner}→{selected.Target} {selected.Kind}：首次索敌或原目标失效");
        }
        int budget=Config.Talents.Rule("targeting_budget"),total=room.Monsters.Count;
        int start=total==0?0:state.Cursor%total;
        for(int step=0;step<total&&budget>0;step++)
        {
            int index=(start+step)%total,owner=monsterBase+index;
            if(!monsterLockIndex.TryGetValue(owner,out var l)||l.Kind!=MonsterLockKind.Overflow||!l.NeedsReevaluation||l.NextEvaluation>state.Clock||busy.Contains(owner))continue;
            // 有效主锁保持稳定；目标失效仍由上方清理并重新索敌。
            if(FriendlyAt(l.Target) is { } valid&&InCombatRoom(valid))
            {
                // 原目标自身恢复容量可就地恢复预约，绝不吸走其他目标的敌人。
                if(!valid.Ranged&&occupied[l.Target]<capacities[l.Target]&&Vector2.DistanceSquared(room.Monsters[index].Position,valid.Position)<=range*range)
                {l.Kind=MonsterLockKind.Normal;occupied[l.Target]++;EngagementEvent($"建立普通容量 #{owner}→{l.Target}：原目标容量恢复");}
                l.NeedsReevaluation=false;l.NextEvaluation=state.Clock+interval;budget--;state.Cursor=(index+1)%Math.Max(1,total);continue;
            }
            var selected=Select(owner);if(selected==null)continue;
            l.Target=selected.Target;l.Kind=selected.Kind;l.NeedsReevaluation=false;l.NextEvaluation=selected.NextEvaluation;
            budget--;state.Cursor=(index+1)%Math.Max(1,total);
        }
        // 刚释放的正常名额也可能由本步死亡效果产生，下一步清理后触发统一重评。
        state.Locks.Sort((a,b)=>a.Owner.CompareTo(b.Owner));
    }
}
