using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>触发效果预检既有弹体总预算；满额不执行、不掷随机、不消费ICD或限次，不新增镜像专属数值。</summary>
    private bool ProcHasFlightCapacity(SkillEffectWork work,EffectDefinition effect)
    {
        long count=effect.Kind switch {"SpawnProjectile"=>effect.ProjectileCount,"SpawnEntity"=>1,_=>0};
        AbilityDefinition? ability=null;bool mirror=false;
        if(effect.Kind=="AbilityCast")ability=SkillConfig.Abilities[effect.MechanicId];
        if(effect.Kind is "AbilityMirror" or "AbilityAttack")
        {
            var buff=skillState.Buffs.FirstOrDefault(b=>b.Instance==work.BuffInstance&&b.Remaining>0);
            if(buff==null)return false;
            if(effect.Kind=="AbilityMirror")
            {if(work.Flight==null||!SkillConfig.Abilities.TryGetValue(work.Flight.Skill,out ability)||ability.Form=="buff")return false;mirror=true;}
            else {if(!SkillConfig.AbilityEffects.TryGetValue(effect.MechanicId,out var attack))return false;count=attack.Count;}
        }
        if(ability!=null&&ability.Form!="buff")count=(mirror?ability.Effects.Take(1):ability.Effects).Sum(id=>(long)SkillConfig.AbilityEffects[id].Count);
        return skillState.Flights.Count+count<=SkillConfig.Setting("max_flights");
    }
    private void CastAbility(SkillEffectWork work,AbilityDefinition ability)
    {
        var owner=CombatActor(work.Owner)!;var target=Choose(owner,ability,SkillConfig.Skills[ability.Id].Range);if(target==null)return;
        if(ability.Form=="buff")
        {
            foreach(string id in ability.Buffs)ApplyBuff(id,work.Owner,work.Owner,ability.Id,work.Coefficient,null);
            float maximum=CombatMaximumHp(owner),before=owner.Hp;owner.Hp=(float)Math.Min(maximum,owner.Hp+maximum*SkillConfig.Setting("self_buff_heal_fraction"));if(owner.Hp>before)effectiveEffectVersion++;return;
        }
        var first=EmitAbility(work,ability,target,false,1);
        if(first!=null)React("OnSkillCommit",work.Owner,CombatId(target),ability.Id,work.Coefficient,work.Depth,first);
        if(ability.Root>0)foreach(var enemy in Enemies(work.Owner).ToArray())
        {ApplyBuff("buff_stun",work.Owner,CombatId(enemy),ability.Id,work.Coefficient,null);skillState.Buffs.Last(b=>b.Id=="buff_stun"&&b.Target==CombatId(enemy)).Remaining=Quantize(ability.Root);}
    }
    private double Quantize(double seconds)=>Math.Ceiling(seconds*SkillConfig.TickRate)/SkillConfig.TickRate;
    private SkillFlightState? EmitAbility(SkillEffectWork work,AbilityDefinition ability,Actor selected,bool mirror,double scale)
    {
        var owner=CombatActor(work.Owner)!;var candidates=Enemies(work.Owner).Where(e=>ability.Targeting=="highest_hp"||Vector2.Distance(owner.Position,e.Position)<=SkillConfig.Skills[ability.Id].Range).OrderBy(CombatId).ToArray();
        if(candidates.Length==0)return null;
        candidates=ability.Targeting switch {"lowest_hp"=>candidates.OrderBy(e=>e.Hp).ToArray(),"highest_hp"=>candidates.OrderByDescending(e=>e.Hp).ToArray(),"farthest"=>candidates.OrderByDescending(e=>Vector2.DistanceSquared(owner.Position,e.Position)).ToArray(),_=>candidates.OrderBy(e=>Vector2.DistanceSquared(owner.Position,e.Position)).ToArray()};
        candidates=candidates.Where(e=>e!=selected).Prepend(selected).ToArray();SkillFlightState? first=null;
        foreach(string id in mirror?ability.Effects.Take(1):ability.Effects)
        {
            var fx=SkillConfig.AbilityEffects[id];
            for(int i=0;i<fx.Count;i++)
            {
                if(skillState.Flights.Count>=SkillConfig.Setting("max_flights"))throw new InvalidDataException("技能投射物超过预算");
                var target=candidates[i%candidates.Length];var direction=Unit(target.Position-owner.Position);var side=new Vector2(-direction.Y,direction.X);
                float jitter=(float)(combatRandom.NextDouble()*2-1)*fx.SpawnJitter;
                Vector2 start=owner.Position+(mirror?direction*(float)(SkillConfig.Setting("mirror_offset")/SkillConfig.Setting("pixels_per_unit")):Vector2.Zero)+side*jitter;
                var f=new SkillFlightState{Id=++skillState.NextId,OriginCast=work.OriginCast,Skill=ability.Id,Effect=id,Form=ability.Form,Attribution=work.Skill,Owner=work.Owner,Target=CombatId(target),Depth=work.Depth,Start=start,Position=start,Previous=start,
                    TargetPoint=fx.Band>0?target.Position:selected.Position+side*((i-(fx.Count-1)/2f)*fx.Spread),Direction=direction,
                    Life=Quantize(fx.Type=="auto_attack"?SkillConfig.Setting("counter_lifetime"):fx.Duration),MaxLife=Quantize(fx.Type=="auto_attack"?SkillConfig.Setting("counter_lifetime"):fx.Duration),Hold=Quantize(fx.Hold),
                    Delay=Quantize((mirror?SkillConfig.Setting("mirror_delay"):0)+i*fx.VolleyInterval+combatRandom.NextDouble()*fx.VolleyJitter),
                    Coefficient=work.Coefficient*fx.Power*(fx.Type=="auto_attack"?1:1+SkillConfig.Setting("skill_level_bonus")*(LevelOf(owner,ability.Id)-1)),
                    Arc=fx.ArcMin+(float)combatRandom.NextDouble()*(fx.ArcMax-fx.ArcMin),Snapshot=AttackSnapshot(owner),Mirrored=mirror,PowerScale=scale,PrimaryFirst=i==0&&!mirror&&fx.Type!="auto_attack"};
                if(ability.Form=="target")f.Life=f.MaxLife=Quantize(fx.Delay);
                if(ability.Form=="ground"){f.Position=f.TargetPoint;f.Pulse=fx.Interval>0?Quantize(fx.Interval):1d/SkillConfig.TickRate;}
                skillState.Flights.Add(f);first??=f;
                effectiveEffectVersion++;
            }
        }
        return first;
    }
    private static Vector2 Unit(Vector2 v)=>v.LengthSquared()<.0000001f?Vector2.UnitX:Vector2.Normalize(v);
    private void BuffEffect(SkillEffectWork work,EffectDefinition effect)
    {
        var buff=skillState.Buffs.FirstOrDefault(b=>b.Instance==work.BuffInstance&&b.Remaining>0);if(buff==null)return;
        var owner=CombatActor(buff.Owner);if(owner is not {Alive:true})return;
        switch(effect.Kind)
        {
            case "AbilityDot":
                if(buff.Cause!=null&&CombatActor(buff.Target) is {Alive:true} victim)HitAbility(buff.Cause,victim,SkillConfig.Buffs[buff.Id].Value*SkillConfig.Buffs[buff.Id].Interval/SkillConfig.TickRate,false);
                break;
            case "AbilityAttack":
            {
                var fx=SkillConfig.AbilityEffects[effect.MechanicId];var a=SkillConfig.Abilities[buff.Ability];var target=Choose(owner,a,fx.Range);
                if(target!=null)EmitAbility(withValues(buff.Ability),a with {Effects=new[]{fx.Id},Form="projectile"},target,false,1);
                break;
            }
            case "AbilityCooldown":
            {
                var action=ActionOf(owner);var cooling=action.Cooldowns.Where(p=>p.Value>0&&SkillConfig.Abilities.TryGetValue(p.Key,out var a)&&a.Form!="buff").ToArray();
                if(cooling.Length>0){var pick=cooling[combatRandom.Next(0,cooling.Length)];double floor=SkillConfig.Skills[pick.Key].Cooldown/(double)SkillConfig.TickRate/(1+SkillConfig.Setting("cooldown_reduction_cap"));double next=Math.Min(pick.Value,Math.Max(floor,pick.Value-SkillConfig.Buffs[buff.Id].Extra));action.Cooldowns[pick.Key]=next;if(next<pick.Value)effectiveEffectVersion++;}
                break;
            }
            case "AbilityMirror":
                if(work.Flight!=null&&SkillConfig.Abilities.TryGetValue(work.Flight.Skill,out var original)&&original.Form!="buff")
                {var target=Choose(owner,original,SkillConfig.Skills[original.Id].Range);if(target!=null)EmitAbility(withValues(original.Id),original,target,true,buff.Value);}
                break;
        }
        SkillEffectWork withValues(string id)=>new(){Owner=buff.Owner,Target=work.Target,Skill=id,Coefficient=SkillConfig.Skills[id].Coefficient,Depth=work.Depth,OriginCast=work.OriginCast};
    }
    private void SpawnGeneric(SkillEffectWork work,EffectDefinition effect)
    {
        var owner=CombatActor(work.Owner)!;var target=CombatActor(work.Target);if(target==null)return;
        int count=effect.Kind=="SpawnEntity"?1:effect.ProjectileCount;
        if(skillState.Flights.Count+count>SkillConfig.Setting("max_flights"))throw new InvalidDataException("投射物预算超限");
        var forward=Unit(target.Position-owner.Position);var snapshot=work.Snapshot??AttackSnapshot(owner);
        if(work.OriginCast==0)work.OriginCast=++skillState.NextId;
        for(int i=0;i<count;i++)
        {
            // 固定角度扇形，权威二维扫掠；每箭自己的Hit列表允许同一次施法多箭命中同一目标。
            float angle=count==1?0:(i/(float)(count-1)-.5f)*effect.SpreadDegrees*MathF.PI/180;
            var direction=new Vector2(forward.X*MathF.Cos(angle)-forward.Y*MathF.Sin(angle),forward.X*MathF.Sin(angle)+forward.Y*MathF.Cos(angle));
            skillState.Flights.Add(new(){Id=++skillState.NextId,OriginCast=work.OriginCast,Owner=work.Owner,Target=work.Target,Skill=work.Skill,Depth=work.Depth,Coefficient=work.Coefficient,Position=owner.Position,Start=owner.Position,Previous=owner.Position,TargetPoint=target.Position,Direction=direction,Form=effect.Kind=="SpawnEntity"?"entity":"generic",GenericEffect=effect.Id,GenericRadius=effect.Extent,GenericSpeed=effect.Speed,Snapshot=snapshot,Life=effect.Kind=="SpawnEntity"?effect.Lifetime/(double)SkillConfig.TickRate:Math.Max(1d/SkillConfig.TickRate,SkillConfig.Skills[work.Skill].Range/effect.Speed)});
            effectiveEffectVersion++;
        }
    }
    private void AdvanceFlights(float dt)
    {
        foreach(var f in skillState.Flights.ToArray())
        {
            if(!f.Active||CombatActor(f.Owner) is not {Alive:true}){f.Active=false;continue;}
            if(f.Delay>0){f.Delay=Math.Max(0,f.Delay-dt);continue;}
            if(f.Hold>0)
            {
                f.Hold=Math.Max(0,f.Hold-dt);
                if(f.GenericEffect==""&&SkillConfig.AbilityEffects[f.Effect].Trajectory is "line_shot" or "line_pierce")
                {f.Position=CombatActor(f.Owner)!.Position+(f.Mirrored?f.Direction*(float)(SkillConfig.Setting("mirror_offset")/SkillConfig.Setting("pixels_per_unit")):Vector2.Zero);}
                continue;
            }
            if(f.Form is "generic" or "entity")
            {
                var def=SkillConfig.Effects[f.GenericEffect];f.Life-=dt;
                if(f.Form=="entity")
                {if(f.Life<=.000001){foreach(var next in def.ResolveEffects)Enqueue(new(){Effect=next,Skill=f.Skill,Owner=f.Owner,Target=f.Target,Coefficient=f.Coefficient,Depth=f.Depth+1,OriginCast=f.OriginCast,Snapshot=f.Snapshot});f.Active=false;}continue;}
                f.Previous=f.Position;f.Position+=f.Direction*f.GenericSpeed*dt;
                if(Config.Talents.Nodes.TryGetValue(f.Skill,out var talent)&&talent.Effect=="shot")
                {
                    var victim=CombatActor(f.Target);
                    if(victim is {Alive:true}&&SegmentDistance(victim.Position,f.Previous,f.Position)<=f.GenericRadius+SkillConfig.CollisionRadius)
                    {
                        var victims=talent.Radius>0?Enemies(f.Owner).Where(e=>Vector2.Distance(e.Position,victim.Position)<=talent.Radius).ToArray():new[]{victim};
                        foreach(var hit in victims)Enqueue(new(){Effect=def.Impact,Skill=f.Skill,Owner=f.Owner,Target=CombatId(hit),Coefficient=f.Coefficient,Depth=f.Depth+1,OriginCast=f.OriginCast,Snapshot=f.Snapshot});
                        f.Active=false;
                    }
                    if(f.Life<=0)f.Active=false;continue;
                }
                foreach(var target in Enemies(f.Owner).Where(e=>!f.Hit.Contains(CombatId(e))&&SegmentDistance(e.Position,f.Previous,f.Position)<=f.GenericRadius+SkillConfig.CollisionRadius).OrderBy(e=>Vector2.DistanceSquared(e.Position,f.Previous)).ToArray())
                {f.Hit.Add(CombatId(target));Enqueue(new(){Effect=def.Impact,Skill=f.Skill,Owner=f.Owner,Target=CombatId(target),Coefficient=f.Coefficient,Depth=f.Depth+1,OriginCast=f.OriginCast,Snapshot=f.Snapshot});}
                if(f.Life<=0)f.Active=false;continue;
            }
            var fx=SkillConfig.AbilityEffects[f.Effect];
            f.Life-=dt;f.Elapsed+=dt;
            if(f.Form=="target")
            {if(f.Life<=.000001){if(CombatActor(f.Target) is {Alive:true} target)HitAbility(f,target);f.Active=false;}continue;}
            if(f.Form=="ground")
            {
                f.Pulse+=dt;double interval=fx.Interval>0?Quantize(fx.Interval):1d/SkillConfig.TickRate;
                while(interval>0&&f.Pulse+.000001>=interval){f.Pulse-=interval;AreaHit(f);}
                if(f.Life<=.000001)f.Active=false;continue;
            }
            if(fx.Trajectory=="sky_drop")
            {
                if(f.Life<=.000001)
                {
                    f.Position=f.TargetPoint;
                    if(fx.Interval>0&&fx.BuffId!="")
                    {f.Form="ground";f.Life=f.MaxLife=SkillConfig.Buffs[fx.BuffId].Duration/(double)SkillConfig.TickRate;f.Pulse=Quantize(fx.Interval);}
                    else {AreaHit(f);f.Active=false;}
                }
                continue;
            }
            float speed=fx.Speed>0?fx.Speed:(float)SkillConfig.Setting("default_projectile_speed");
            f.Previous=f.Position;
            if(fx.Trajectory is "line_shot" or "line_pierce")
            {
                f.Position+=f.Direction*speed*dt;
                foreach(var target in Enemies(f.Owner).Where(e=>!f.Hit.Contains(CombatId(e))&&SegmentDistance(e.Position,f.Previous,f.Position)<=SkillConfig.CollisionRadius).OrderBy(e=>Vector2.DistanceSquared(e.Position,f.Previous)).ToArray())
                {
                    HitAbility(f,target);f.Hit.Add(CombatId(target));
                    if(fx.Trajectory=="line_shot"){if(!f.Pierced&&combatRandom.NextDouble()<fx.Pierce)f.Pierced=true;else{f.Active=false;break;}}
                }
            }
            else
            {
                var target=CombatActor(f.Target);
                if(target is not {Alive:true})
                {
                    target=f.Reacquired<SkillConfig.Setting("max_reacquire")?Enemies(f.Owner).Where(e=>Vector2.Distance(e.Position,f.TargetPoint)<=SkillConfig.Setting("fallback_radius")/SkillConfig.Setting("pixels_per_unit")).OrderBy(e=>e.Hp).ThenBy(CombatId).FirstOrDefault():null;
                    if(target==null){f.Active=false;continue;}f.Target=CombatId(target);f.Reacquired++;
                }
                f.TargetPoint=target.Position;f.Start+=Unit(target.Position-f.Start)*Math.Min(Vector2.Distance(target.Position,f.Start),speed*dt);
                var side=new Vector2(-f.Direction.Y,f.Direction.X);double t=Math.Clamp(f.Elapsed/Math.Max(f.MaxLife,dt),0,1);
                f.Position=f.Start+side*(float)(Math.Sin(Math.PI*t)*f.Arc);
                if(SegmentDistance(target.Position,f.Previous,f.Position)<=SkillConfig.CollisionRadius||Vector2.Distance(f.Start,target.Position)<.001f){HitAbility(f,target);f.Active=false;}
            }
            if(f.Life<=.000001)f.Active=false;
        }
        skillState.Flights.RemoveAll(f=>!f.Active);
    }
    private static float SegmentDistance(Vector2 point,Vector2 a,Vector2 b)
    {var delta=b-a;float t=delta.LengthSquared()<.0000001f?0:Math.Clamp(Vector2.Dot(point-a,delta)/delta.LengthSquared(),0,1);return Vector2.Distance(point,a+delta*t);}
    private void AreaHit(SkillFlightState f)
    {
        var fx=SkillConfig.AbilityEffects[f.Effect];
        foreach(var target in Enemies(f.Owner).Where(e=>fx.All||Vector2.Distance(e.Position,f.TargetPoint)<=fx.Radius).ToArray())HitAbility(f,target);
    }
    private void HitAbility(SkillFlightState flight,Actor target,double ratio=1,bool applyState=true)
    {
        var owner=CombatActor(flight.Owner);if(owner is not {Alive:true}||!target.Alive)return;
        var fx=SkillConfig.AbilityEffects[flight.Effect];double extra=0;
        if(applyState)
        {
            if(fx.Condition=="target_hp_below"&&target.Hp/CombatMaximumHp(target)<fx.ConditionValue)extra+=SkillConfig.Setting("execute_multiplier")-1;
            if(fx.Condition=="target_has_state"&&ActorBuffs(CombatId(target)).Any())extra+=fx.ConditionValue;
        }
        // 分身强度与周期比例缩放本体；斩杀/状态条件是威能贡献，不能将二者混成独立乘区。
        var result=SettleDamage(owner,target,flight.Skill,flight.Coefficient,DamageType.Physical,true,flight.Snapshot,flight.Critical,false,flight.Depth,extra,originCast:flight.OriginCast,baseScale:flight.PowerScale*ratio);
        if(result==null)return;
        if(!result.Dmg2.Dodged)flight.Critical??=result.Dmg2.Critical;
        if(applyState&&flight.PrimaryFirst&&!flight.CriticalNotified&&result.Dmg2.Critical){flight.CriticalNotified=true;React("OnCritical",flight.Owner,CombatId(target),flight.Skill,flight.Coefficient,flight.Depth,flight);}
        if(!applyState||!target.Alive||result.Dmg2.Dodged)return;
        if(fx.BuffId!="")ApplyBuff(fx.BuffId,flight.Owner,CombatId(target),flight.Skill,flight.Coefficient,flight);
        var room=Floor.Rooms[CurrentRoom];
        if(fx.Knockback>0)target.Position+=Unit(target.Position-owner.Position)*fx.Knockback;
        if(fx.Gather>0)target.Position+=Unit(flight.TargetPoint-target.Position)*Math.Min(Vector2.Distance(target.Position,flight.TargetPoint),fx.Gather);
        target.Position=Vector2.Clamp(target.Position,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
    }
}
