using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>每次本房怪物死亡只登记一条尸体权利；召唤物、英雄和跨房尸体不进入该列表。</summary>
    private void RecordCorpse(Actor target)
    {
        int id=CombatId(target);
        if(skillState.Corpses.Any(c=>c.ActorId==id))return;
        skillState.Corpses.Add(new(){ActorId=id,Position=target.Position});
    }
    private bool CorpseExists(int actorId)=>skillState.Corpses.Any(c=>c.ActorId==actorId);
    /// <summary>尸体已死亡，不能用存活判定；空间资格只按本房边界和记录坐标判断。</summary>
    private bool CorpseInRoom(Actor actor)
    {
        var room=Floor.Rooms[CurrentRoom];var delta=Vector2.Abs(actor.Position-room.Center);
        return delta.X<=room.HalfWidth+.0001f&&delta.Y<=room.HalfHeight+.0001f;
    }
    /// <summary>尸体目标只从本房未被消费的死亡事实中选择；同距用稳定战斗 ID 打破，已被消费则立即失去资格。</summary>
    private Actor? ChooseCorpseTarget(Actor actor,float range)
    {
        return skillState.Corpses.Select(c=>CombatActor(c.ActorId))
            .Where(c=>c is {Alive:false}&&CorpseInRoom(c)&&Vector2.Distance(actor.Position,c.Position)<=range)
            .OrderBy(c=>Vector2.DistanceSquared(actor.Position,c!.Position)).ThenBy(c=>CombatId(c!)).FirstOrDefault();
    }
    /// <summary>尸爆在 DrainEffects 内原子移除一具尸体，再以尸体坐标为圆心走唯一 SettleDamage；竞争失败不产生第二条伤害路径。</summary>
    private void CorpseExplosionEffect(SkillEffectWork work,EffectDefinition effect)
    {
        var owner=CombatActor(work.Owner);
        if(owner is not {Alive:true})return;
        var corpse=skillState.Corpses.FirstOrDefault(c=>c.ActorId==work.Target);
        if(corpse==null)return;
        skillState.Corpses.Remove(corpse);
        PushVisual(CombatVisualKind.CorpseExplosion,work.Skill,"",work.Owner,work.Target,corpse.Position,corpse.Position,0,effect.Extent,effect.DamageType,work.Depth);
        foreach(var hit in Enemies(work.Owner).Where(e=>e.Alive&&Vector2.Distance(e.Position,corpse.Position)<=effect.Extent).ToArray())
            SettleDamage(owner,hit,work.Skill,work.Coefficient*effect.Share,effect.DamageType,owner.Ranged,work.Snapshot,depth:work.Depth,originCast:work.OriginCast);
    }
}
