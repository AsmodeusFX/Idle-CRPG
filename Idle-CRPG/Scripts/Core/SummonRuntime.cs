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
    /// <summary>本次逻辑步发生瞬时位移（脱队闪现、跨房同步或入场兜底）的召唤物，仅供表现层跳过插值；不属于存档数据。</summary>
    private readonly HashSet<Actor> summonBlinkTick=new();
    /// <summary>本次入场窗口为每只召唤物分配的落点；只服务当前房间，换房、清房或重置技能现场即清空。</summary>
    private readonly Dictionary<Actor,Vector2> summonDeployGoals=new();
    /// <summary>已完成入场落位（含超时兜底同步）的召唤物；同一入场窗口内不再由入场逻辑移动。</summary>
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
        Summons.Clear();roomEntrySummons.Clear();summonOffTimer.Clear();summonBlinkTick.Clear();summonDeployGoals.Clear();summonDeployed.Clear();MarkChanged();
    }
    /// <summary>所有回城入口统一撤销召唤及进房副本；死亡不产生经验、掉落或怪物击杀。</summary>
    internal void ClearTownSummons()=>ClearSummons();

    /// <summary>正式跨房切换：仍留在上一房间或走廊的存活召唤物直接并入新房间入口内侧的合法落点。
    /// 只同步一次坐标并释放旧接敌占用；保留 HP、Buff、数量与既有寿命，不重新召唤、不产生伤害或奖励。</summary>
    private void SyncSummonsToRoom(Room room,Vector2 entry)
    {
        if(Summons.Count==0)return;
        var inward=Vector2.Normalize(room.Center-entry);
        if(!float.IsFinite(inward.X)||inward.LengthSquared()<.0001f)inward=Vector2.UnitY;
        var side=new Vector2(-inward.Y,inward.X);
        int slot=0;
        foreach(var summon in Summons.Where(s=>s.Alive))
        {
            if(!WithinRoom(room,summon.Position))
            {
                ReleaseCombatTargets(CombatId(summon));
                summonOffTimer.Remove(summon);
                // 入口内侧横向错位，避免多只召唤物完全重叠；落点仍夹取在房间战斗边界内。
                float lane=slot==0?0:(slot+1)/2*(slot%2==1?-1:1);
                var goal=entry+inward*.45f+side*(lane*.3f);
                summon.Position=Vector2.Clamp(goal,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
                summonBlinkTick.Add(summon);
            }
            slot++;
        }
    }
    /// <summary>入场部署：复用英雄的展开纵深/间距配置，近战召唤物前压、远程靠后、支援贴近主人。
    /// 横向车道接在英雄序列之后，避免同纵深重叠；只在敌人警觉窗口内执行，并用独立临时加速。
    /// 入场结束或到达后立即停止，不改写基础 Speed，也不参与正式战斗 AI 的移动权。</summary>
    private void AdvanceEntrySummons(Room room,float dt)
    {
        var state=skillState.Targeting;
        if(!state.EntryPrepared||CombatEntryRemaining<=0)return;
        float basePct=Config.Talents.Rule("summon_entry_speed_pct")/100f;
        float ownerPct=Config.Talents.Rule("summon_entry_owner_pct")/100f;
        var side=new Vector2(-state.EntryDirection.Y,state.EntryDirection.X);
        foreach(var summon in Summons.Where(s=>s.Alive))
        {
            if(!InCombatRoom(summon)||summonDeployed.Contains(summon))continue;
            var owner=summon.SummonOwner>=0&&summon.SummonOwner<Heroes.Count?Heroes[summon.SummonOwner]:null;
            string role=SummonRole(summon);
            Vector2 goal;
            if(role=="support")
            {
                // 支援型不承担前排展开，直接贴近主人当前站位；侧向小幅错位避免与主人重叠。
                var anchor=owner is {Alive:true}?owner.Position:state.EntryAnchor;
                goal=anchor+side*(Summons.IndexOf(summon)%2==0?.25f:-.25f);
            }
            else
            {
                float depth=Config.Talents.Rule($"combat_entry_{role}_depth_milli")/1000f;
                float spacing=Config.Talents.Rule($"combat_entry_{role}_spread_milli")/1000f;
                // 车道序号沿用英雄的奇偶公式，但从英雄总数之后继续，保证与同纵深英雄错开。
                int laneIndex=Heroes.Count+Summons.IndexOf(summon);
                int lane=laneIndex==0?0:(laneIndex+1)/2*(laneIndex%2==1?-1:1);
                goal=state.EntryAnchor+state.EntryDirection*depth+side*(lane*spacing);
            }
            goal=Vector2.Clamp(goal,room.Center-room.HalfSize+new Vector2(.4f),room.Center+room.HalfSize-new Vector2(.4f));
            summonDeployGoals[summon]=goal;
            float speed=Math.Max(summon.Speed*basePct,OwnerWalkSpeed(owner??Heroes[0])*ownerPct);
            Move(summon,goal,dt,speed);
            entryMoved.Add(summon);
            if(Vector2.DistanceSquared(summon.Position,goal)<.04f)summonDeployed.Add(summon);
        }
    }
    /// <summary>敌人 AI 启动时，仍未到位且未接敌的召唤物兜底同步到已分配站位；不延长警觉时间，
    /// 不改变怪物目标/接敌容量，也不给未参与入场的房内新召唤补瞬移。</summary>
    private void SnapEntrySummons()
    {
        if(summonDeployGoals.Count==0)return;
        foreach(var (summon,goal) in summonDeployGoals.ToArray())
        {
            summonDeployed.Add(summon);
            if(!summon.Alive||SummonEngaged(summon))continue;
            ReleaseCombatTargets(CombatId(summon));
            summon.Position=goal;
            summonBlinkTick.Add(summon);
        }
        summonDeployGoals.Clear();
    }
    private string SummonRole(Actor summon)=>SkillConfig.Summons.TryGetValue(summon.SummonDefinitionId,out var definition)?definition.Role:"melee";
    private static bool WithinRoom(Room room,Vector2 p){var d=Vector2.Abs(p-room.Center);return d.X<=room.HalfWidth+.00001f&&d.Y<=room.HalfHeight+.00001f;}

    /// <summary>清房后跟随主人：正常跟随距离内按原速、超出则按主人当前行走速度的配置倍率追赶；持续脱队且落点合法时闪现。
    /// 入场部署窗口由入场逻辑独占移动权，战斗房间内不跟随、不闪现。</summary>
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
    /// <summary>技能只包含永续召唤效果时返回其定义，供脱战补员与生命周期判断共用；混合技能不参与脱战施放。</summary>
    private SummonDefinition? PermanentSummonOf(string skillId)
    {
        if(!SkillConfig.Skills.TryGetValue(skillId,out var skill)||skill.Effects.Length==0)return null;
        SummonDefinition? found=null;
        foreach(string effectId in skill.Effects)
        {
            var effect=SkillConfig.Effects[effectId];
            if(effect.Kind!="Summon"||!SkillConfig.Summons.TryGetValue(effect.MechanicId,out var definition)||!definition.Permanent)return null;
            if(found!=null&&found.Id!=definition.Id)return null;
            found=definition;
        }
        return found;
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
    private void FollowDungeonSummons(float dt)
    {
        AdvanceSummonLifetimes(dt);
        PruneSummons();
        if(CombatEntryRemaining>0)return;
        var current=Floor.Rooms[CurrentRoom];
        float follow=Config.Talents.Rule("summon_follow_distance_milli")/1000f;
        float blink=Config.Talents.Rule("summon_blink_distance_milli")/1000f;
        float blinkDelay=Config.Talents.Rule("summon_blink_delay_milli")/1000f;
        float catchup=Config.Talents.Rule("summon_catchup_speed_pct")/100f;
        foreach(var summon in Summons.Where(s=>s.Alive))
        {
            if(!MovingBetweenRooms&&!current.Clear&&InCombatRoom(summon))continue;
            var owner=Heroes[summon.SummonOwner];
            if(!owner.Alive)continue;
            var ownerRoom=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,owner.Position));
            float distance=Vector2.Distance(summon.Position,owner.Position);
            // 闪现只在当前房间已清理、主人位于合法房间且召唤物未参与接敌/施法时累积。
            if(distance>blink&&current.Clear&&ownerRoom!=null&&!SummonEngaged(summon))
            {
                summonOffTimer[summon]=summonOffTimer.GetValueOrDefault(summon)+dt;
                if(summonOffTimer[summon]+.00001f>=blinkDelay)
                {
                    BlinkSummon(summon,owner,ownerRoom);
                    summonOffTimer.Remove(summon);
                    continue;
                }
            }
            else summonOffTimer.Remove(summon);
            var summonRoom=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,summon.Position));
            if(ownerRoom!=null&&ReferenceEquals(summonRoom,ownerRoom)&&distance<=follow)continue;
            float speed=distance>follow?Math.Max(summon.Speed,OwnerWalkSpeed(owner)*catchup):summon.Speed;
            float budget=speed*dt;
            for(int step=0;step<8&&budget>.00001f;step++)
            {
                var targetRoom=ownerRoom??Floor.Rooms.OrderBy(r=>Vector2.DistanceSquared(r.Center,owner.Position)).First();
                var from=Floor.Rooms.FirstOrDefault(r=>WithinRoom(r,summon.Position));
                // 同房时追到主人身后 follow 距离的落点：等距尾随，避免每步在3单位边界启停造成顿挫。
                Vector2 goal=owner.Position;
                if(from!=null&&from.Id==targetRoom.Id&&distance>follow)
                    goal=owner.Position-Vector2.Normalize(owner.Position-summon.Position)*follow;
                if(from!=null&&from.Id!=targetRoom.Id)
                {
                    var next=Floor.Rooms[FirstStep(from.Id,targetRoom.Id)];
                    var exit=from.DoorTowards(next);
                    goal=Vector2.Distance(summon.Position,exit)>.001f?exit:next.DoorTowards(from);
                }
                else if(from==null)
                {
                    // 走廊内只沿所属门段前进；禁止朝主人直线切穿房墙。
                    var passage=Floor.Passages.OrderBy(p=>SegmentDistance(summon.Position,Floor.Rooms[p.A].DoorTowards(Floor.Rooms[p.B]),Floor.Rooms[p.B].DoorTowards(Floor.Rooms[p.A]))).First();
                    var a=Floor.Rooms[passage.A];var b=Floor.Rooms[passage.B];
                    bool towardA=targetRoom.Id==a.Id||targetRoom.Id!=b.Id&&FirstStep(a.Id,targetRoom.Id)!=b.Id;
                    goal=towardA?a.DoorTowards(b):b.DoorTowards(a);
                }
                else if(ownerRoom==null)
                {
                    var next=from!.Neighbors.Select(n=>Floor.Rooms[n]).OrderBy(r=>SegmentDistance(owner.Position,from.DoorTowards(r),r.DoorTowards(from))).First();
                    var exit=from.DoorTowards(next);
                    goal=Vector2.Distance(summon.Position,exit)>.001f?exit:owner.Position;
                }
                else if(Vector2.Distance(summon.Position,goal)<=.00001f)break;
                float distanceToGoal=Vector2.Distance(summon.Position,goal);
                if(distanceToGoal<.00001f)break;
                Move(summon,goal,1,Math.Min(budget,distanceToGoal));budget-=Math.Min(budget,distanceToGoal);
            }
        }
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.DistanceSquared(p,a+d*Math.Clamp(Vector2.Dot(p-a,d)/d.LengthSquared(),0,1));}
    }
    /// <summary>主人当前行走速度；装备/被动加成走统一属性聚合，不吃寒缓以外的临时加速，也不改写基础Speed。</summary>
    private float OwnerWalkSpeed(Actor owner)=>Math.Max(owner.Speed,(float)EffectiveValue(owner,"move_speed",owner.Speed));
    /// <summary>正在施法、接敌或被怪物锁定的召唤物视为战斗中，不能通过跟随闪现脱离。</summary>
    private bool SummonEngaged(Actor summon)
    {
        int id=CombatId(summon);
        if(skillState.Actions.Any(a=>a.Owner==id&&(a.Active!=""||a.Recovery>0)))return true;
        int index=FriendlyIndex(summon);
        return skillState.Targeting.Locks.Any(l=>l.Owner==id||l.Target==index);
    }
    /// <summary>脱队闪现只同步坐标并释放旧接敌占用；保留HP/Buff/数量，不重新召唤、不产生伤害或奖励事件。</summary>
    private void BlinkSummon(Actor summon,Actor owner,Room room)
    {
        int slot=0;
        for(int i=0;i<Summons.IndexOf(summon);i++)if(Summons[i].Alive&&Summons[i].SummonOwner==summon.SummonOwner)slot++;
        // 黄金角螺旋给同主人的多只召唤物分配不同虚拟站位，避免全部叠在一点。
        float angle=slot*2.399963f,radius=.35f+.15f*(slot%3);
        var offset=new Vector2(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius);
        ReleaseCombatTargets(CombatId(summon));
        summon.Position=Vector2.Clamp(owner.Position+offset,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
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
