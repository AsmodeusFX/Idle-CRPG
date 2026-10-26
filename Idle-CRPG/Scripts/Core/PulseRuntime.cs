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
        // 旋风/新星/陨星/飞锤的覆盖范围受 extent 修饰器影响（S204/S202/S404/S402等），上限仍由技能配置决定。
        float pulseExtent=EffectiveExtent(work,effect);
        PushVisual(CombatVisualKind.Pulse,work.Skill,"",work.Owner,work.Target,center.Position,center.Position,0,pulseExtent,effect.DamageType,work.Depth);
        var hits=Enemies(work.Owner).Where(e=>e.Alive&&InCombatRoom(e)&&Vector2.Distance(e.Position,center.Position)<=pulseExtent).ToArray();
        RecordAoe(hits.Length);
        foreach(var hit in hits)
            SettleDamage(owner,hit,work.Skill,work.Coefficient*effect.Share,effect.DamageType,owner.Ranged,work.Snapshot,depth:work.Depth,originCast:work.OriginCast);
    }
}
