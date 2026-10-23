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
}
public sealed partial class GameSession
{
    // 索引仅指向本步权威Locks中的对象，不保存第二份目标或预约账本。
    private readonly Dictionary<int,MonsterTargetLock> monsterLockIndex=new();
    private readonly HashSet<Actor> entryMoved=new();
    public IEnumerable<(int Owner,int Target,MonsterLockKind Kind)> MonsterTargets=>skillState.Targeting.Locks.Select(l=>(l.Owner,l.Target,l.Kind));
    /// <summary>仅有怪新房的短暂警觉窗口；已清理房间和走廊始终为0。</summary>
    public double CombatEntryRemaining=>skillState.Targeting.EntryPrepared?Math.Max(0,Config.Talents.Rule("combat_entry_delay_milli")/1000d-skillState.Targeting.EntryElapsed):0;
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
    /// <summary>按实际坐标判断本房空间资格；调用者的英雄/怪物列表另限定当前房间成员。</summary>
    public bool InCombatRoom(Actor actor)
    {
        var room=Floor.Rooms[CurrentRoom];var delta=Vector2.Abs(actor.Position-room.Center);
        return actor.Alive&&delta.X<=room.HalfWidth+.0001f&&delta.Y<=room.HalfHeight+.0001f;
    }
    /// <summary>队尾继续连续跨门，不夹取或瞬移进房；进房检查点的位置足以恢复这一过程。</summary>
    private void AdvanceRoomEntry(Room room,float dt)
    {
        entryMoved.Clear();bool spread=!room.Clear&&skillState.Targeting.EntryPrepared;
        foreach(var hero in Heroes)
        {
            int heroIndex=Heroes.IndexOf(hero);
            bool inside=InCombatRoom(hero);
            if(inside&&!room.Clear&&room.Monsters.Any(InCombatRoom)){skillState.Targeting.DeployedMask|=1<<heroIndex;continue;}
            if(!hero.Alive||inside&&(!spread||(skillState.Targeting.DeployedMask&(1<<heroIndex))!=0))continue;
            if(!spread)
            {
                // 非入场窗口：房内成员已跳过，房外成员仍由跑图/跟随逻辑处理。
                entryMoved.Add(hero);continue;
            }
            // 即使仍在走廊或上一房间，也先计算房间内落点，再沿真实房门连续进入，不直线穿墙。
            var state=skillState.Targeting;var side=new Vector2(-state.EntryDirection.Y,state.EntryDirection.X);
            // 入场纵深/横向间距按攻击类型分组；接敌容量只决定怪物预约，不能把远程法系推入前排。
            string role=hero.Ranged?"ranged":"melee";
            float depth=Config.Talents.Rule($"combat_entry_{role}_depth_milli")/1000f;
            float spacing=Config.Talents.Rule($"combat_entry_{role}_spread_milli")/1000f;
            int lane=heroIndex==0?0:(heroIndex+1)/2*(heroIndex%2==1?-1:1);
            var goal=state.EntryAnchor+state.EntryDirection*depth+side*(lane*spacing);
            goal=Vector2.Clamp(goal,room.Center-room.HalfSize+new Vector2(.4f),room.Center+room.HalfSize-new Vector2(.4f));
            Move(hero,inside?goal:PartyDoorWaypoint(hero,room,goal),dt);
            if(inside&&Vector2.DistanceSquared(hero.Position,goal)<.04f)skillState.Targeting.DeployedMask|=1<<heroIndex;
            entryMoved.Add(hero);
        }
        // 召唤物与英雄共用同一入场窗口；入场逻辑独占其移动权，完成后交回正式战斗 AI。
        if(spread)AdvanceEntrySummons(room,dt);
    }
    private readonly Queue<string> engagementEvents=new();
    /// <summary>GM只读诊断，直接读取唯一预约账本。</summary>
    public string EngagementDiagnostics()
    {
        if(Floors.Count==0)return "无战斗现场";
        var lines=new List<string>();
        for(int i=0;i<FriendlyCount;i++)if(FriendlyAt(i) is {Alive:true} a&&EngagementCapacity(a)>0)
            lines.Add($"{a.Name} {skillState.Targeting.Locks.Count(l=>l.Target==i&&l.Kind==MonsterLockKind.Normal)}/{EngagementCapacity(a)}："+string.Join("，",skillState.Targeting.Locks.Where(l=>l.Target==i).Select(l=>$"#{l.Owner} {l.Kind}")));
        return string.Join("\n",lines.Concat(engagementEvents));
    }
    private void EngagementEvent(string message)
    {
        engagementEvents.Enqueue(message);while(engagementEvents.Count>24)engagementEvents.Dequeue();
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
