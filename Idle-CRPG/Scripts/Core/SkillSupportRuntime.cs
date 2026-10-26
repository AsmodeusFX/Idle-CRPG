using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    private IEnumerable<Actor> Allies(int owner)=>IsFriendlyId(owner)?Heroes.Concat(Summons).Where(InCombatRoom):Floor.Rooms[CurrentRoom].Monsters.Where(InCombatRoom);
    private Actor? ChooseSkillTarget(Actor actor,SkillDefinition skill)
    {
        if(skill.Condition=="nearby_melee"&&!Enemies(CombatId(actor)).Any(e=>!e.Ranged&&Vector2.Distance(e.Position,actor.Position)<=skill.Range))return null;
        // 尸体技能只在本房存在未消费尸体且进入施放距离时有目标；无尸体时跳过且不进入冷却。
        if(skill.TargetPolicy=="corpse")return ChooseCorpseTarget(actor,skill.Range);
        if(skill.TargetPolicy=="self")return actor;
        if(skill.TargetPolicy=="ally_lowest_hp")return Allies(CombatId(actor)).Where(a=>a.Hp<CombatMaximumHp(a)&&Vector2.Distance(a.Position,actor.Position)<=skill.Range).OrderBy(a=>a.Hp/CombatMaximumHp(a)).ThenBy(CombatId).FirstOrDefault();
        if(skill.Condition=="missing_buff")
        {
            var buffs=skill.Effects.Select(id=>SkillConfig.Effects[id]).Where(f=>f.Kind=="ApplyBuff").Select(f=>f.BuffId).ToArray();
            return Enemies(CombatId(actor)).Where(e=>Vector2.Distance(e.Position,actor.Position)<=skill.Range&&buffs.Any(id=>!ActorBuffs(CombatId(e)).Any(b=>b.Id==id))).OrderBy(e=>Vector2.DistanceSquared(actor.Position,e.Position)).ThenBy(CombatId).FirstOrDefault();
        }
        return Choose(actor,SkillConfig.Abilities.GetValueOrDefault(skill.Id),skill.Range);
    }
    private Actor[] BuffEffectTargets(SkillEffectWork work,EffectDefinition effect,Actor center)
    {
        var buff=SkillConfig.Buffs[effect.BuffId];
        if(buff.Target=="self")return new[]{CombatActor(work.Owner)!};
        if(effect.Extent<=0)return new[]{center};
        return (buff.Target=="ally"?Allies(work.Owner):Enemies(work.Owner)).Where(a=>Vector2.Distance(a.Position,center.Position)<=effect.Extent).ToArray();
    }
    /// <summary>显式治疗仅恢复存活合法单位至有效上限，不复活、不触发第二条伤害或奖励路径。</summary>
    private void HealTarget(Actor? target,double fraction,string skill="")
    {
        if(target is not {Alive:true}||!InCombatRoom(target))return;
        float maximum=CombatMaximumHp(target);RecordHeal(target,maximum);float before=target.Hp;target.Hp=(float)Math.Min(maximum,target.Hp+maximum*fraction);
        if(target.Hp>before){effectiveEffectVersion++;PushVisual(CombatVisualKind.Heal,skill,"",CombatId(target),CombatId(target),target.Position,target.Position,target.Hp-before,0);}
    }
    private bool IsAuraSkill(string id)=>SkillConfig.Skills.TryGetValue(id,out var skill)&&skill.Effects.Any(e=>SkillConfig.Effects[e].Kind=="MaintainAura");
    /// <summary>激活状态随玩家跨房保留；卸槽和死亡立刻撤销，范围内贡献只存在于现有Buff列表。</summary>
    private void PruneAuraActivations()
    {
        var actors=Heroes.Concat(reserveHeroes);
        if(Floors.Count>0)actors=actors.Concat(Floor.Rooms[CurrentRoom].Monsters);
        foreach(var actor in actors)
        {
            // 职业特性T光环不进入SkillIds（不占S1-S4、不自动施放），按已学T节点维持；
            // 其它光环仍要求已装配且技能含MaintainAura。
            var talentAuras=Heroes.Contains(actor)||reserveHeroes.Contains(actor)?TalentAuraSkills(actor):Array.Empty<string>();
            bool changed=actor.ActiveAuraSkills.RemoveAll(id=>!actor.Alive||(!actor.SkillIds.Contains(id)&&!talentAuras.Contains(id))||!IsAuraSkill(id))>0;
            if(changed&&Heroes.Contains(actor))MarkChanged();
        }
        skillState.Buffs.RemoveAll(b=>b.AuraSkill!=""&&(CombatActor(b.Owner) is not {Alive:true} owner||!owner.ActiveAuraSkills.Contains(b.AuraSkill)));
    }
    private void SynchronizeAuras(int activationOwner=-1,string activationSkill="",long activationCast=0)
    {
        PruneAuraActivations();
        var desired=new HashSet<(int Owner,int Target,string Buff,string Skill)>();
        foreach(var source in RoomActors.Where(InCombatRoom))foreach(string id in source.ActiveAuraSkills)
        {
            int owner=CombatId(source);
            foreach(var effect in SkillConfig.Skills[id].Effects.Select(e=>SkillConfig.Effects[e]).Where(e=>e.Kind=="MaintainAura"))
            {
                var definition=SkillConfig.Buffs[effect.BuffId];
                var targets=(definition.Target=="ally"?Allies(owner):Enemies(owner)).Where(a=>Vector2.Distance(a.Position,source.Position)<=effect.Extent).ToArray();
                if(targets.Length==0)continue;
                long root=owner==activationOwner&&id==activationSkill?activationCast:skillState.Buffs.FirstOrDefault(b=>b.Owner==owner&&b.AuraSkill==id)?.OriginCast??0;
                if(root==0)root=++skillState.NextId;
                foreach(var target in targets)
                {
                    int targetId=CombatId(target);desired.Add((owner,targetId,effect.BuffId,id));
                    if(!skillState.Buffs.Any(b=>b.Owner==owner&&b.Target==targetId&&b.Id==effect.BuffId&&b.AuraSkill==id))ApplyBuff(effect.BuffId,owner,targetId,id,1,null,id,root);
                }
            }
        }
        skillState.Buffs.RemoveAll(b=>b.AuraSkill!=""&&!desired.Contains((b.Owner,b.Target,b.Id,b.AuraSkill)));
    }
}
