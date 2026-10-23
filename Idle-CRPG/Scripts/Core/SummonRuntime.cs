using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>脱队计时只服务于非战斗跟随，不属于战斗现场；读档或重开房间从0重新累积。</summary>
    private readonly Dictionary<Actor,float> summonOffTimer=new();
    /// <summary>当前非战斗跟随锚点（跑图领队）；切换锚点时清空脱队计时，避免因换领队触发闪现。</summary>
    private Actor? summonFollowLeader;
    /// <summary>本次逻辑步发生瞬时位移（仅异常恢复的脱队闪现）的召唤物，仅供表现层跳过插值；不属于存档数据。</summary>
    private readonly HashSet<Actor> summonBlinkTick=new();
    /// <summary>本次入场窗口为每只召唤物分配的落点；只服务当前房间，换房、清房或重置技能现场即清空。</summary>
    private readonly Dictionary<Actor,Vector2> summonDeployGoals=new();
    /// <summary>已完成入场部署（到达落点或已进入有效战斗）的召唤物；同一入场窗口内不再由入场逻辑移动。</summary>
    private readonly HashSet<Actor> summonDeployed=new();
    internal bool SummonBlinkedThisTick(Actor actor)=>summonBlinkTick.Contains(actor);
    private bool IsFriendlyActor(Actor actor)=>Heroes.Contains(actor)||Summons.Contains(actor);
    /// <summary>索敌掩码与容量使用连续友方索引：英雄在前、召唤物在后，与 CombatId 的怪物区段区分。</summary>
    private int FriendlyCount=>Heroes.Count+Summons.Count;
    private Actor? FriendlyAt(int index)=>index<Heroes.Count?Heroes[index]:Summons.ElementAtOrDefault(index-Heroes.Count);
    private int FriendlyIndex(Actor actor)=>Heroes.Contains(actor)?Heroes.IndexOf(actor):Heroes.Count+Summons.IndexOf(actor);
    private int FriendlyIndexFromCombatId(int id)
    {
        if(id<0)return -1;
        if(id<Heroes.Count)return id;
        int monsters=Floors.Count>0&&CurrentRoom>=0&&CurrentRoom<Floor.Rooms.Count?Floor.Rooms[CurrentRoom].Monsters.Count:0;
        int index=id-Heroes.Count-monsters;
        return index>=0&&index<Summons.Count?Heroes.Count+index:-1;
    }
    /// <summary>英雄、当前房怪物、召唤物共用 ID 空间；召唤物追加在怪物之后，保持旧存档怪物 ID 不变。</summary>
    private bool IsFriendlyId(int id)
    {
        int monsters=Floors.Count>0&&CurrentRoom>=0&&CurrentRoom<Floor.Rooms.Count?Floor.Rooms[CurrentRoom].Monsters.Count:0;
        return id>=0&&(id<Heroes.Count||id>=Heroes.Count+monsters&&id<Heroes.Count+monsters+Summons.Count);
    }
    private bool InCurrentRoom(Actor actor)
    {
        if(Floors.Count==0||CurrentRoom<0||CurrentRoom>=Floor.Rooms.Count)return false;
        var room=Floor.Rooms[CurrentRoom];var delta=Vector2.Abs(actor.Position-room.Center);
        return actor.Alive&&delta.X<=room.HalfWidth+.0001f&&delta.Y<=room.HalfHeight+.0001f;
    }
    /// <summary>召唤物属性在施法时按主人基础值快照；跨房连续行进，换层重建位置但不补满 HP。</summary>
    private void SummonEffect(SkillEffectWork work,EffectDefinition effect)
    {
        if(Config.EnemySummons.TryGetValue(effect.MechanicId,out var enemyRule)&&!IsFriendlyId(work.Owner)){EnemySummonEffect(work,enemyRule);return;}
        var owner=CombatActor(work.Owner);
        if(owner is not {Alive:true}||!Heroes.Contains(owner)||!owner.SkillIds.Contains(work.Skill))return;
        if(!SkillConfig.Summons.TryGetValue(effect.MechanicId,out var definition))throw new InvalidDataException("召唤定义引用无效："+effect.MechanicId);
        if(Summons.Count(s=>s.Alive&&s.SummonOwner==work.Owner&&s.SummonDefinitionId==definition.Id)>=definition.MaxCount)return;
        var room=Floor.Rooms[CurrentRoom];
        float maxHp=owner.MaxHp*(float)definition.HpRatio;
        var summon=new Actor
        {
            SpecId="summon:"+definition.Id,Name=definition.Name,Level=owner.Level,
            MaxHp=maxHp,Hp=maxHp,Attack=owner.Attack*(float)definition.AttackRatio,Defense=owner.Defense*(float)definition.DefenseRatio,
            Speed=definition.Speed,Range=definition.Range,Cooldown=(float)definition.Cooldown,Timer=0,Ranged=definition.RangedCombat,
            SummonDefinitionId=definition.Id,SummonSkill=work.Skill,SummonOwner=work.Owner,SummonRemaining=(float)definition.Duration
        };
        int slot=Summons.Count(s=>s.Alive&&s.SummonOwner==work.Owner);
        var offset=new Vector2(.35f+.2f*slot,(slot%2==0?.3f:-.3f));
        // 脱战补员可能发生在走廊：主人不在房间时不夹取到房间内，避免召唤物被传送到墙里。
        var placed=owner.Position+offset;
        summon.Position=WithinRoom(room,owner.Position)?Vector2.Clamp(placed,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f)):placed;
        Summons.Add(summon);
        effectiveEffectVersion++;
        PushVisual(CombatVisualKind.Summon,work.Skill,definition.Id,work.Owner,CombatId(summon),owner.Position,summon.Position,0,0);
        MarkChanged();
    }
    /// <summary>新楼层按队伍位置重建召唤位置，不复制主人临时 Buff；普通跨房不调用此方法。</summary>
    private void PlaceSummonsNearParty(Room room)
    {
        summonOffTimer.Clear();summonDeployGoals.Clear();summonDeployed.Clear();
        int index=0;
        foreach(var summon in Summons.Where(s=>s.Alive).OrderBy(s=>s.SummonOwner).ThenBy(s=>summonOrder(s)))
        {
            var owner=summon.SummonOwner>=0&&summon.SummonOwner<Heroes.Count?Heroes[summon.SummonOwner]:null;
            var anchor=owner is {Alive:true}?owner.Position:Heroes.FirstOrDefault(h=>h.Alive)?.Position??room.Center;
            var offset=new Vector2(.35f+.2f*index,(index%2==0?.28f:-.28f));
            summon.Position=Vector2.Clamp(anchor+offset,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
            index++;
        }
        int summonOrder(Actor actor)=>Summons.IndexOf(actor);
    }
    /// <summary>主人死亡或卸槽后撤销召唤；保留列表位置直到下次进房，避免同一房间内 ID 位移。</summary>
    private void PruneSummons()
    {
        if(Summons.Count==0)return;
        foreach(var summon in Summons.Where(s=>s.Alive).ToArray())
        {
            bool ownerLegal=summon.SummonOwner>=0&&summon.SummonOwner<Heroes.Count&&Heroes[summon.SummonOwner] is {Alive:true} owner&&owner.SkillIds.Contains(summon.SummonSkill);
            if(ownerLegal&&SkillConfig.Summons.ContainsKey(summon.SummonDefinitionId))continue;
            KillSummon(summon);
        }
    }
    private void KillSummon(Actor summon)
    {
        if(!Summons.Contains(summon))return;
        summonOffTimer.Remove(summon);summonDeployGoals.Remove(summon);summonDeployed.Remove(summon);
        int id=CombatId(summon);
        summon.Hp=0;summon.Timer=0;
        skillState.Buffs.RemoveAll(b=>b.Target==id||b.Owner==id);
        ReleaseCombatTargets(id);
        MarkChanged();
    }
    private void RemoveSummonsOf(int owner)
    {
        foreach(var summon in Summons.Where(s=>s.Alive&&s.SummonOwner==owner).ToArray())KillSummon(summon);
    }
    /// <summary>离开地牢、全灭或换队时清除全部召唤记录；该路径不发放奖励或经验。</summary>
    private void ClearSummons()
    {
        if(Summons.Count==0&&roomEntrySummons.Count==0)return;
        if(Floors.Count>0&&CurrentRoom>=0&&CurrentRoom<Floor.Rooms.Count)
        {
            int first=Heroes.Count+Floor.Rooms[CurrentRoom].Monsters.Count;
            for(int i=0;i<Summons.Count;i++)
            {
                int id=first+i;
                skillState.Buffs.RemoveAll(b=>b.Target==id||b.Owner==id);
                ReleaseCombatTargets(id);
            }
        }
        foreach(var summon in Summons){summon.Hp=0;summon.Timer=0;}
        Summons.Clear();roomEntrySummons.Clear();summonOffTimer.Clear();summonBlinkTick.Clear();summonDeployGoals.Clear();summonDeployed.Clear();summonFollowLeader=null;MarkChanged();
    }
    /// <summary>所有回城入口统一撤销召唤及进房副本；死亡不产生经验、掉落或怪物击杀。</summary>
    internal void ClearTownSummons()=>ClearSummons();

    /// <summary>入场部署：复用英雄的展开纵深/间距配置，近战召唤物前压、远程靠后、支援贴近主人。
    /// 横向车道接在英雄序列之后，避免同纵深重叠。召唤物可能仍在上一房间或走廊，必须沿真实房门连续移动，
    /// 以基础 Speed 的配置倍率为上限推进；到达落点或进入有效战斗即结束部署，交回正式战斗 AI。
    /// 入场加速只作用于移动执行，不改写基础属性，也不延长敌人 AI 启动延迟。</summary>
    private void AdvanceEntrySummons(Room room,float dt)
    {
        var state=skillState.Targeting;
        if(!state.EntryPrepared)return;
        bool entryWindow=CombatEntryRemaining>0;
        float entrySpeedPct=Config.Talents.Rule("summon_entry_speed_pct")/100f;
        foreach(var summon in Summons.Where(s=>s.Alive).ToArray())
        {
            if(summonDeployed.Contains(summon))continue;
            // 新目标只在本房警觉窗口内认领；窗口结束后继续推进已认领的部署，不给战斗中途的新召唤补部署。
            if(!summonDeployGoals.ContainsKey(summon))
            {
                if(!entryWindow)continue;
                summonDeployGoals[summon]=SummonDeployGoal(summon,room,state);
            }
            // V1.2：召唤物同样独立完成入口通行，不因英雄或队友开始战斗而停在门口。
            // 近距离威胁、正在施法或被锁定才允许提前接战；接战后不再补做通行。
            if(!EntryGateCleared(summon))
            {
                int bit=1<<FriendlyIndex(summon);
                if(SummonEngaged(summon)||EntryThreatened(summon,room))
                {
                    state.EntryEngagedMask|=bit;summonDeployGoals.Remove(summon);summonDeployed.Add(summon);continue;
                }
                // 到达通行边界即结束入场部署并交回正式战斗AI：通行边界不是固定落点，本步不再改写位置。
                if(InCombatRoom(summon)&&EntryDepth(summon.Position)+.0001f>=EntryPassDepth)
                {state.EntryPassMask|=bit;summonDeployGoals.Remove(summon);summonDeployed.Add(summon);continue;}
                else
                {
                    Move(summon,PartyDoorWaypoint(summon,room,summonDeployGoals[summon]),dt,summon.Speed*entrySpeedPct);
                    entryMoved.Add(summon);continue;
                }
            }
            var goal=summonDeployGoals[summon];
            // 支援型始终贴近主人当前站位；其余角色沿用入场时分配的固定落点，避免每步重算产生抖动。
            if(SummonRole(summon)=="support")
            {
                var owner=OwnerOf(summon);
                var anchor=owner is {Alive:true}?owner.Position:state.EntryAnchor;
                var side=new Vector2(-state.EntryDirection.Y,state.EntryDirection.X);
                goal=ClampToRoom(room,anchor+side*(Summons.IndexOf(summon)%2==0?.25f:-.25f));
                summonDeployGoals[summon]=goal;
            }
            Move(summon,PartyDoorWaypoint(summon,room,goal),dt,summon.Speed*entrySpeedPct);
            entryMoved.Add(summon);
            // 走廊/旧房间的路径点不能当作落点：必须真正进入房间并站到目标附近才算部署完成。
            if(Vector2.DistanceSquared(summon.Position,goal)<.04f&&WithinRoom(room,summon.Position))
            {
                summonDeployGoals.Remove(summon);summonDeployed.Add(summon);
            }
        }
    }
    /// <summary>入场部署目标：近战前压、远程靠后、支援贴近主人；车道从英雄总数之后继续，保证多只召唤物不重叠。</summary>
    private Vector2 SummonDeployGoal(Actor summon,Room room,CombatTargetingState state)
    {
        var side=new Vector2(-state.EntryDirection.Y,state.EntryDirection.X);
        Vector2 goal;
        string role=SummonRole(summon);
        if(role=="support")
        {
            var owner=OwnerOf(summon);
            var anchor=owner is {Alive:true}?owner.Position:state.EntryAnchor;
            goal=anchor+side*(Summons.IndexOf(summon)%2==0?.25f:-.25f);
        }
        else
        {
            // 通行边界不小于原展开纵深：远程/支援型也必须真正入房通行后才停止推进。
            float depth=Math.Max(Config.Talents.Rule($"combat_entry_{role}_depth_milli")/1000f,EntryPassDepth);
            float spacing=Config.Talents.Rule($"combat_entry_{role}_spread_milli")/1000f;
            int laneIndex=Heroes.Count+Summons.IndexOf(summon);
            int lane=laneIndex==0?0:(laneIndex+1)/2*(laneIndex%2==1?-1:1);
            goal=state.EntryAnchor+state.EntryDirection*depth+side*(lane*spacing);
        }
        return ClampToRoom(room,goal);
    }
    private static Vector2 ClampToRoom(Room room,Vector2 point)=>Vector2.Clamp(point,room.Center-room.HalfSize+new Vector2(.4f),room.Center+room.HalfSize-new Vector2(.4f));
    /// <summary>英雄与召唤物共用的房门路径点：同房直接朝落点；在上一房间先走到通往目标房间的门；
    /// 在走廊时沿最近门段前进。禁止朝目标直线穿墙，也不做瞬移。</summary>
    private Vector2 PartyDoorWaypoint(Actor actor,Room targetRoom,Vector2 roomGoal)
    {
        var from=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,actor.Position));
        if(from==null)return CorridorWaypointTowards(actor.Position,targetRoom);
        if(from.Id!=targetRoom.Id)
        {
            var next=Floor.Rooms[FirstStep(from.Id,targetRoom.Id)];
            var exit=from.DoorTowards(next);
            return Vector2.Distance(actor.Position,exit)>.001f?exit:next.DoorTowards(from);
        }
        return roomGoal;
    }
    /// <summary>走廊内只沿最近门段前进；门段通向目标房间一侧时取该侧房门。</summary>
    private Vector2 CorridorWaypointTowards(Vector2 position,Room targetRoom)
    {
        var passage=Floor.Passages.OrderBy(p=>SegmentDistance(position,Floor.Rooms[p.A].DoorTowards(Floor.Rooms[p.B]),Floor.Rooms[p.B].DoorTowards(Floor.Rooms[p.A]))).First();
        var a=Floor.Rooms[passage.A];var b=Floor.Rooms[passage.B];
        bool towardA=targetRoom.Id==a.Id||targetRoom.Id!=b.Id&&FirstStep(a.Id,targetRoom.Id)!=b.Id;
        return towardA?a.DoorTowards(b):b.DoorTowards(a);
    }
    private Actor? OwnerOf(Actor summon)=>summon.SummonOwner>=0&&summon.SummonOwner<Heroes.Count?Heroes[summon.SummonOwner]:null;
    private string SummonRole(Actor summon)=>SkillConfig.Summons.TryGetValue(summon.SummonDefinitionId,out var definition)?definition.Role:"melee";
    private static bool WithinRoom(Room room,Vector2 p){var d=Vector2.Abs(p-room.Center);return d.X<=room.HalfWidth+.00001f&&d.Y<=room.HalfHeight+.00001f;}

    /// <summary>限时召唤按固定逻辑步递减剩余时间，归零即撤销；永续定义不受该计时影响。
    /// 计时不MarkChanged，避免每一步都触发存档；剩余时间随进房检查点保存。</summary>
    private void AdvanceSummonLifetimes(float dt)
    {
        foreach(var summon in Summons.Where(s=>s.Alive).ToArray())
        {
            if(!SkillConfig.Summons.TryGetValue(summon.SummonDefinitionId,out var definition)||definition.Permanent)continue;
            summon.SummonRemaining=Math.Max(0,summon.SummonRemaining-dt);
            if(summon.SummonRemaining<=0)KillSummon(summon);
        }
    }
    /// <summary>技能的全部效果指向同一定义且均为召唤时返回该定义（不限永续/限时）；
    /// 供AI名额资格与生命周期判断共用，混合技能不参与自动施放。</summary>
    private SummonDefinition? SoleSummonOf(string skillId)
    {
        if(!SkillConfig.Skills.TryGetValue(skillId,out var skill)||skill.Effects.Length==0)return null;
        SummonDefinition? found=null;
        foreach(string effectId in skill.Effects)
        {
            var effect=SkillConfig.Effects[effectId];
            if(effect.Kind!="Summon"||!SkillConfig.Summons.TryGetValue(effect.MechanicId,out var definition))return null;
            if(found!=null&&found.Id!=definition.Id)return null;
            found=definition;
        }
        return found;
    }
    /// <summary>技能只包含永续召唤效果时返回其定义，供脱战补员与生命周期判断共用；混合技能不参与脱战施放。</summary>
    private SummonDefinition? PermanentSummonOf(string skillId)=>SoleSummonOf(skillId) is {Permanent:true} definition?definition:null;
    /// <summary>该主人当前存活且同一召唤定义的单位数；与 SummonEffect 的名额判定使用同一口径。</summary>
    private int SummonCountOf(Actor owner,string definitionId)
    {
        int index=CombatId(owner);
        return Summons.Count(s=>s.Alive&&s.SummonOwner==index&&s.SummonDefinitionId==definitionId);
    }
    /// <summary>脱战补员只服务永续召唤；限时召唤仍由战斗AI施放，避免走廊里无限续时。
    /// 仍走原有前摇→提交→恢复时序与冷却，只是不要求敌人或有效攻击目标。</summary>
    private void AutoSummonPermanent(float dt)
    {
        var room=Floor.Rooms[CurrentRoom];
        if(!room.Generated||room.Monsters.Any(m=>m.Alive))return;
        // 先推进已经在途的脱战施放；战斗AI暂停时前摇仍按固定逻辑步完成。
        AdvanceActions(dt);
        foreach(var hero in Heroes.Where(h=>h.Alive))
        {
            int owner=Heroes.IndexOf(hero);
            // 先确认确实缺员且冷却完成，再触碰动作槽；否则会在非战斗场景留下空动作污染快照。
            string? cast=null;
            foreach(string id in hero.SkillIds)
            {
                var definition=PermanentSummonOf(id);
                if(definition==null||SkillCooldown(hero,id)>0)continue;
                if(Summons.Count(s=>s.Alive&&s.SummonOwner==owner&&s.SummonDefinitionId==definition.Id)>=definition.MaxCount)continue;
                cast=id;break;
            }
            if(cast==null)continue;
            var action=ActionOf(hero);
            // 不抢断已提交的前摇/恢复，也不在眩晕时开始新施放。
            if(action.Active!=""||action.Recovery>0||Stunned(hero))continue;
            action.Active=cast;action.Target=CombatId(hero);action.Windup=SkillConfig.Skills[cast].Windup/(double)SkillConfig.TickRate;
            if(action.Windup<=0)Commit(action);
        }
        effectBudgetRemaining=SkillConfig.EffectBudget;
        DrainEffects();
    }
    /// <summary>非战斗跟随锚点为当前跑图领队，不再跟随召唤者：近战在前、远程在后、支援略靠后，
    /// 横向车道按召唤物顺序错开。跟随距离内按原速，超出后按领队行走速度的配置倍率追赶；
    /// 入场部署本步已取得移动权的召唤物不参与跟随，战斗房间内不跟随、不闪现。</summary>
    private void FollowDungeonSummons(float dt)
    {
        AdvanceSummonLifetimes(dt);
        PruneSummons();
        if(CombatEntryRemaining>0)return;
        var current=Floor.Rooms[CurrentRoom];
        // 只在非战斗跑图或清房阶段刷新领队；战斗中沿用缓存，不重新选举。
        if(current.Clear||MovingBetweenRooms)RefreshTravelLeader(false);
        var leader=travelLeader is {Alive:true}?travelLeader:Heroes.FirstOrDefault(h=>h.Alive);
        if(leader==null)return;
        // 领队切换时清空脱队计时，让召唤物平滑改追新领队，不因换锚点触发闪现。
        if(!ReferenceEquals(summonFollowLeader,leader)){summonFollowLeader=leader;summonOffTimer.Clear();}
        var leaderRoom=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,leader.Position));
        Vector2 forward=Vector2.Zero;
        if(destination>=0&&phase==0)
        {
            var target=Floor.Rooms[destination];
            forward=Vector2.Normalize(target.Center-current.Center);
            if(!float.IsFinite(forward.X))forward=Vector2.Zero;
        }
        float follow=Config.Talents.Rule("summon_follow_distance_milli")/1000f;
        float blink=Config.Talents.Rule("summon_blink_distance_milli")/1000f;
        float blinkDelay=Config.Talents.Rule("summon_blink_delay_milli")/1000f;
        float catchup=Config.Talents.Rule("summon_catchup_speed_pct")/100f;
        foreach(var summon in Summons.Where(s=>s.Alive))
        {
            // 门前开门先锋由跑图逻辑独占移动，跟随不得把它拉回领队身边。
            if(destination>=0&&phase==1&&ReferenceEquals(summon,doorOpener))continue;
            // 入场部署在本步已取得移动权（可能仍在走廊或上一房间），跟随不得再改写其位置。
            if(entryMoved.Contains(summon))continue;
            if(!MovingBetweenRooms&&!current.Clear&&InCombatRoom(summon))continue;
            var summonRoom=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,summon.Position));
            float distance=Vector2.Distance(summon.Position,leader.Position);
            // 闪现只在当前房间已清理、领队位于合法房间且召唤物未参与接敌/施法时累积。
            if(distance>blink&&current.Clear&&leaderRoom!=null&&!SummonEngaged(summon))
            {
                summonOffTimer[summon]=summonOffTimer.GetValueOrDefault(summon)+dt;
                if(summonOffTimer[summon]+.00001f>=blinkDelay)
                {
                    BlinkSummon(summon,leader,leaderRoom);
                    summonOffTimer.Remove(summon);
                    continue;
                }
            }
            else summonOffTimer.Remove(summon);
            var goal=SummonFollowGoal(summon,leader,leaderRoom,forward);
            // 已站在领队附近自己的角色位置时不移动，避免每步微调造成抖动。
            if(leaderRoom!=null&&ReferenceEquals(summonRoom,leaderRoom)&&Vector2.Distance(summon.Position,goal)<=.12f)continue;
            float speed=distance>follow?Math.Max(summon.Speed,TravelWalkSpeed(leader)*catchup):summon.Speed;
            float budget=speed*dt;
            for(int step=0;step<8&&budget>.00001f;step++)
            {
                var targetRoom=leaderRoom??Floor.Rooms.OrderBy(r=>Vector2.DistanceSquared(r.Center,leader.Position)).First();
                var from=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,summon.Position));
                Vector2 waypoint=goal;
                if(from!=null&&from.Id!=targetRoom.Id)
                {
                    var next=Floor.Rooms[FirstStep(from.Id,targetRoom.Id)];
                    var exit=from.DoorTowards(next);
                    waypoint=Vector2.Distance(summon.Position,exit)>.001f?exit:next.DoorTowards(from);
                }
                else if(from==null)
                {
                    // 走廊内只沿所属门段前进；禁止朝领队直线切穿房墙。
                    waypoint=CorridorWaypointTowards(summon.Position,targetRoom);
                }
                else if(leaderRoom==null)
                {
                    var next=from!.Neighbors.Select(n=>Floor.Rooms[n]).OrderBy(r=>SegmentDistance(leader.Position,from.DoorTowards(r),r.DoorTowards(from))).First();
                    var exit=from.DoorTowards(next);
                    waypoint=Vector2.Distance(summon.Position,exit)>.001f?exit:leader.Position;
                }
                else if(Vector2.Distance(summon.Position,waypoint)<=.00001f)break;
                float distanceToGoal=Vector2.Distance(summon.Position,waypoint);
                if(distanceToGoal<.00001f)break;
                Move(summon,waypoint,1,Math.Min(budget,distanceToGoal));budget-=Math.Min(budget,distanceToGoal);
            }
        }
    }
    /// <summary>领队附近的角色站位：近战在前、远程在后；横向车道沿用既有奇偶公式，多只召唤物不重叠。</summary>
    private Vector2 SummonFollowGoal(Actor summon,Actor leader,Room? leaderRoom,Vector2 forward)
    {
        // 领队在走廊等无房间坐标的位置时只沿门段追赶，不套用房间内前后站位，避免把召唤物挤进墙里。
        if(leaderRoom==null)return leader.Position;
        var side=forward.LengthSquared()>.0001f?new Vector2(-forward.Y,forward.X):new Vector2(0,1);
        (float ahead,float lateral)=SummonRole(summon) switch
        {
            "ranged"=>(-.65f,.38f),
            "support"=>(-.35f,.28f),
            _=>(.55f,.32f)
        };
        int index=Summons.IndexOf(summon);
        int lane=index==0?0:(index+1)/2*(index%2==1?-1:1);
        var goal=leader.Position+forward*ahead+side*(lane*lateral);
        return leaderRoom!=null?ClampToRoom(leaderRoom,goal):goal;
    }
    /// <summary>正在施法、接敌或被怪物锁定的召唤物视为战斗中，不能通过跟随闪现脱离。</summary>
    private bool SummonEngaged(Actor summon)
    {
        int id=CombatId(summon);
        if(skillState.Actions.Any(a=>a.Owner==id&&(a.Active!=""||a.Recovery>0)))return true;
        int index=FriendlyIndex(summon);
        return skillState.Targeting.Locks.Any(l=>l.Owner==id||l.Target==index);
    }
    /// <summary>严重脱队的异常恢复：只同步到当前领队附近并释放旧接敌占用；保留HP/Buff/数量，
    /// 不重新召唤、不产生伤害或奖励事件，也不改变召唤归属。</summary>
    private void BlinkSummon(Actor summon,Actor leader,Room room)
    {
        int slot=Summons.IndexOf(summon);
        // 黄金角螺旋给跟随同一领队的多只召唤物分配不同虚拟站位，避免全部叠在一点。
        float angle=slot*2.399963f,radius=.35f+.15f*(slot%3);
        var offset=new Vector2(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius);
        ReleaseCombatTargets(CombatId(summon));
        summon.Position=Vector2.Clamp(leader.Position+offset,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
        summonBlinkTick.Add(summon);
    }
    /// <summary>进房检查点只保留进房时已存在的召唤；房内新召唤随战斗重开消失。</summary>
    private void CaptureSummonEntry()=>roomEntrySummons=Summons.Where(s=>s.Alive).Select(s=>s.Copy()).ToList();
    private void RemoveDeadSummonsAtRoomEntry()
    {
        bool removed=Summons.RemoveAll(s=>!s.Alive)>0;
        if(removed)MarkChanged();
    }
}
