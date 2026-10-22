using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>持续旋风窗口：周期Buff每拍以自身当前位置为圆心，对范围内敌人走唯一SettleDamage；Buff到期或卸槽即停止。</summary>
    private void AreaPulseEffect(SkillEffectWork work,EffectDefinition effect)
    {
        var owner=CombatActor(work.Owner);
        if(owner is not {Alive:true})return;
        var center=CombatActor(work.Target);
        if(center is not {Alive:true})return;
        PushVisual(CombatVisualKind.Pulse,work.Skill,"",work.Owner,work.Target,center.Position,center.Position,0,effect.Extent,effect.DamageType,work.Depth);
        foreach(var hit in Enemies(work.Owner).Where(e=>e.Alive&&InCombatRoom(e)&&Vector2.Distance(e.Position,center.Position)<=effect.Extent).ToArray())
            SettleDamage(owner,hit,work.Skill,work.Coefficient*effect.Share,effect.DamageType,owner.Ranged,work.Snapshot,depth:work.Depth,originCast:work.OriginCast);
    }
}
