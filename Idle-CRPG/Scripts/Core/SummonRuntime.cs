using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
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
            Speed=definition.Speed,Range=definition.Range,Cooldown=(float)definition.Cooldown,Timer=0,Ranged=false,
            SummonDefinitionId=definition.Id,SummonSkill=work.Skill,SummonOwner=work.Owner
        };
        int slot=Summons.Count(s=>s.Alive&&s.SummonOwner==work.Owner);
        var offset=new Vector2(.35f+.2f*slot,(slot%2==0?.3f:-.3f));
        summon.Position=Vector2.Clamp(owner.Position+offset,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
        Summons.Add(summon);
        effectiveEffectVersion++;
        PushVisual(CombatVisualKind.Summon,work.Skill,definition.Id,work.Owner,CombatId(summon),owner.Position,summon.Position,0,0);
        MarkChanged();
    }
    /// <summary>新楼层按队伍位置重建召唤位置，不复制主人临时 Buff；普通跨房不调用此方法。</summary>
    private void PlaceSummonsNearParty(Room room)
    {
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
        Summons.Clear();roomEntrySummons.Clear();MarkChanged();
    }
    /// <summary>所有回城入口统一撤销召唤及进房副本；死亡不产生经验、掉落或怪物击杀。</summary>
    internal void ClearTownSummons()=>ClearSummons();

    /// <summary>清房后跟随主人；赶路沿真实房门与走廊连续移动，落后者进房后才参与正式战斗。</summary>
    private void FollowDungeonSummons(float dt)
    {
        PruneSummons();
        var current=Floor.Rooms[CurrentRoom];
        foreach(var summon in Summons.Where(s=>s.Alive))
        {
            if(!MovingBetweenRooms&&!current.Clear&&InCombatRoom(summon))continue;
            var owner=Heroes[summon.SummonOwner];
            float budget=summon.Speed*dt;
            for(int step=0;step<8&&budget>.00001f;step++)
            {
                var ownerRoom=Floor.Rooms.FirstOrDefault(r=>Inside(r,owner.Position));
                var targetRoom=ownerRoom??Floor.Rooms.OrderBy(r=>Vector2.DistanceSquared(r.Center,owner.Position)).First();
                var from=Floor.Rooms.FirstOrDefault(r=>Inside(r,summon.Position));
                Vector2 goal=owner.Position;
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
                else if(Vector2.Distance(summon.Position,goal)<=Config.Talents.Rule("dungeon_queue_spacing_milli")/1000f)break;
                float distance=Vector2.Distance(summon.Position,goal);
                if(distance<.00001f)break;
                Move(summon,goal,1,Math.Min(budget,distance));budget-=Math.Min(budget,distance);
            }
        }
        static bool Inside(Room room,Vector2 p){var d=Vector2.Abs(p-room.Center);return d.X<=room.HalfWidth+.00001f&&d.Y<=room.HalfHeight+.00001f;}
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.DistanceSquared(p,a+d*Math.Clamp(Vector2.Dot(p-a,d)/d.LengthSquared(),0,1));}
    }
    /// <summary>进房检查点只保留进房时已存在的召唤；房内新召唤随战斗重开消失。</summary>
    private void CaptureSummonEntry()=>roomEntrySummons=Summons.Where(s=>s.Alive).Select(s=>s.Copy()).ToList();
    private void RemoveDeadSummonsAtRoomEntry()
    {
        bool removed=Summons.RemoveAll(s=>!s.Alive)>0;
        if(removed)MarkChanged();
    }
}
