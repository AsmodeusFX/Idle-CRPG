using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>有界链原型：从技能主目标起按距离确定性跳跃，目标不得重复，每跳都是独立的真实伤害实例。</summary>
    private void ChainEffect(SkillEffectWork work,EffectDefinition effect)
    {
        var owner=CombatActor(work.Owner);
        if(owner is not {Alive:true})return;
        if(!SkillConfig.Chains.TryGetValue(effect.MechanicId,out var chain))throw new InvalidDataException("链式定义引用无效："+effect.MechanicId);
        var current=CombatActor(work.Target);
        if(current is not {Alive:true}||!Enemies(work.Owner).Contains(current))return;
        var visited=new HashSet<int>();
        double coefficient=work.Coefficient*effect.Share;
        var fromPosition=owner.Position;
        for(int jump=0;jump<chain.MaxTargets;jump++)
        {
            if(jump>0)
            {
                // 上一跳死亡也继续从原坐标寻找最近的合法目标；找不到即自然终止。
                current=NextChainTarget(work.Owner,current!,visited,chain.JumpRange);
                if(current==null)break;
                coefficient*=chain.DamageDecay;
            }
            visited.Add(CombatId(current));
            PushVisual(CombatVisualKind.Chain,work.Skill,effect.MechanicId,work.Owner,CombatId(current),fromPosition,current.Position,coefficient,0,effect.DamageType,work.Depth);
            SettleDamage(owner,current,work.Skill,coefficient,effect.DamageType,owner.Ranged,work.Snapshot,depth:work.Depth,originCast:work.OriginCast);
            fromPosition=current.Position;
        }
    }
    /// <summary>链式选敌只按当前房间内存活、未在本链命中且位于跳跃距离内的敌方单位；距离同分用稳定战斗 ID。</summary>
    private Actor? NextChainTarget(int owner,Actor from,HashSet<int> visited,float range)
    {
        return Enemies(owner).Where(e=>e.Alive&&!visited.Contains(CombatId(e))&&Vector2.Distance(e.Position,from.Position)<=range)
            .OrderBy(e=>Vector2.DistanceSquared(e.Position,from.Position)).ThenBy(CombatId).FirstOrDefault();
    }
}
