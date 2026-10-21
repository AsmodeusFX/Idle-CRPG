using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    private SkillBattleState skillState=new();
    private readonly PersistentRandom combatRandom;
    private uint roomEntryCombatState;
    private int effectBudgetRemaining;
    private SkillConfiguration SkillConfig=>Config.Skills;
    public DamageBreakdown? LastDamage { get; private set; }
    /// <summary>当前房间技能状态只读诊断入口；正式存档不保留这个战斗现场。</summary>
    public int ActiveBuffCount=>skillState.Buffs.Count;
    public int ActiveFlightCount=>skillState.Flights.Count;

    /// <summary>显式装备技能的会话命令；默认职业没有主动技能，迁移不会自动投放这 15 项内容。</summary>
    public void EquipSkills(string heroId,IEnumerable<string> skillIds)
    {
        var hero=Heroes.Single(h=>h.SpecId==heroId);var ids=skillIds.ToArray();
        if(hero.SkillIds.Any(Config.Talents.Nodes.ContainsKey)||ids.Any(Config.Talents.Nodes.ContainsKey)){if(!SetTalentLoadout(heroId,ids,out var reason))throw new ArgumentException(reason);return;}
        if(ids.Distinct().Count()!=ids.Length||ids.Any(id=>!SkillConfig.Skills.TryGetValue(id,out var s)||s.Kind!="ActiveSkill"))throw new ArgumentException("技能引用重复或不是主动技能");
        hero.SkillIds=ids;ResetSkillRoom();MarkChanged();
    }
    private IEnumerable<Actor> RoomActors=>Heroes.Concat(Floor.Rooms[CurrentRoom].Monsters);
    private int CombatId(Actor actor)=>Heroes.Contains(actor)?Heroes.IndexOf(actor):Heroes.Count+Floor.Rooms[CurrentRoom].Monsters.IndexOf(actor);
    private Actor? CombatActor(int id)=>id<0?null:id<Heroes.Count?Heroes[id]:Floor.Rooms[CurrentRoom].Monsters.ElementAtOrDefault(id-Heroes.Count);
    private IEnumerable<Actor> Enemies(int owner)=>owner<Heroes.Count?Floor.Rooms[CurrentRoom].Monsters.Where(InCombatRoom):Heroes.Where(InCombatRoom);
    private IEnumerable<SkillBuffState> ActorBuffs(int id)=>skillState.Buffs.Where(b=>b.Target==id&&b.Remaining>0);
    private bool Stunned(Actor actor)=>ActorBuffs(CombatId(actor)).Any(b=>SkillConfig.Buffs[b.Id].Mechanic=="stun");
    private float MoveMultiplier(Actor actor)=>(float)ActorBuffs(CombatId(actor)).Where(b=>SkillConfig.Buffs[b.Id].Mechanic=="chill").Select(b=>1-b.Value).DefaultIfEmpty(1).Min();
    private double EffectiveValue(Actor actor,string attribute,double basis)
    {
        var declarations=ActorBuffs(CombatId(actor)).SelectMany(b=>SkillConfig.Buffs[b.Id].Attributes).Where(a=>a.Attribute==attribute);
        basis+=PassiveTalentValue(actor,attribute);
        foreach(var b in ActorBuffs(CombatId(actor)))if(attribute=="atk"&&(b.Id=="talent_battle_shout"||b.Id=="talent_weaken"))basis*=b.Id=="talent_weaken"?Math.Max(0,1-b.Value):1+b.Value;
        return Config.Attributes.Aggregate(attribute,basis,declarations);
    }
    private BattleAttributes EffectiveAttributes(Actor actor)
    {
        var result=actor.Battle.Copy();
        result.Dodge=Math.Clamp(EffectiveValue(actor,"dodge_rate",result.Dodge),0,1);result.CriticalChance=Math.Clamp(EffectiveValue(actor,"crit_rate",result.CriticalChance),0,1);
        result.BlockChance=Math.Clamp(EffectiveValue(actor,"block_rate",result.BlockChance),0,1);result.BlockReduction=Math.Clamp(EffectiveValue(actor,"block_reduction",result.BlockReduction),0,1);
        result.CriticalDamage=EffectiveValue(actor,"crit_damage",result.CriticalDamage);result.AttackSpeed=EffectiveValue(actor,"attack_speed_pct",result.AttackSpeed);result.CooldownReduction=EffectiveValue(actor,"cooldown_reduction_pct",result.CooldownReduction);
        foreach(var attr in ActorBuffs(CombatId(actor)).SelectMany(b=>SkillConfig.Buffs[b.Id].Attributes))
        {
            if(attr.Attribute=="damage_multiplier"){result.Modifiers.Add(new(DamageBucket.Multiplier,attr.Value));continue;}
            if(attr.Attribute=="skill_coefficient_bonus"){result.Modifiers.Add(new(DamageBucket.Skill,attr.Value));continue;}
            if(!attr.Attribute.EndsWith("_damage_pct")||attr.Attribute=="vulnerable_damage_pct")continue;
            DamageType? type=attr.Attribute switch {"physical_damage_pct"=>DamageType.Physical,"fire_damage_pct"=>DamageType.Fire,"cold_damage_pct"=>DamageType.Cold,"lightning_damage_pct"=>DamageType.Lightning,"poison_damage_pct"=>DamageType.Poison,_=>null};
            // 技能/普攻资格用稳定技能 ID 标记；本批源配置尚未投放此类属性。
            if(attr.Attribute=="basic_attack_damage_pct")result.Modifiers.Add(new(DamageBucket.General,attr.Value,Heroes.Contains(actor)?"basic_player":"basic_monster"));
            else if(attr.Attribute=="skill_damage_pct")foreach(string id in actor.SkillIds)result.Modifiers.Add(new(DamageBucket.General,attr.Value,id));
            else result.Modifiers.Add(new(DamageBucket.General,attr.Value,Type:type,RangedOnly:attr.Attribute=="ranged_damage_pct",BossOnly:attr.Attribute=="boss_damage_pct"));
        }
        return result;
    }
    private SkillAttackSnapshot AttackSnapshot(Actor actor)=>new(){Attack=EffectiveValue(actor,"atk",actor.Attack),Attributes=EffectiveAttributes(actor)};
    /// <summary>包含临时 Buff 的有效生命上限；临时增加上限不自动治疗。</summary>
    public float CombatMaximumHp(Actor actor)
    {
        if(Floors.Count==0||World!=null&&World.Stage!=CampaignStage.Dungeon)return actor.MaxHp;
        return (float)EffectiveValue(actor,"hp",actor.MaxHp);
    }
    private void ResetSkillRoom()
    {
        skillState=new();monsterLockIndex.Clear();foreach(var actor in Heroes)actor.Hp=Math.Min(actor.Hp,actor.MaxHp);
        if(FloorIndex<Floors.Count&&CurrentRoom<Floors[FloorIndex].Rooms.Count)foreach(var actor in Floor.Rooms[CurrentRoom].Monsters)actor.Hp=Math.Min(actor.Hp,actor.MaxHp);
    }

    /// <summary>所有普攻/技能/反击/持续伤害共用一次 HP 应用和奖励结算，死亡后不会重复结算。</summary>
    private DamageBreakdown? SettleDamage(Actor attacker,Actor target,string skill,double coefficient,DamageType type,bool ranged,SkillAttackSnapshot? snapshot=null,bool? critical=null,bool events=true,int depth=0,double multiplier=1)
    {
        if(!attacker.Alive||!target.Alive||!InCombatRoom(target))return null;
        bool hero=Heroes.Contains(attacker);snapshot??=AttackSnapshot(attacker);
        var debuffs=ActorBuffs(CombatId(target)).Select(b=>b.Id).ToHashSet();
        var attributes=snapshot.Attributes.Copy();
        foreach(var buff in ActorBuffs(CombatId(target)))foreach(var attr in SkillConfig.Buffs[buff.Id].Attributes.Where(a=>a.Attribute=="vulnerable_damage_pct"))attributes.Modifiers.Add(new(DamageBucket.Vulnerable,attr.Value,DebuffId:buff.Id));
        if(multiplier!=1)attributes.Modifiers.Add(new(DamageBucket.Multiplier,multiplier));
        var input=new DamageInput(skill,snapshot.Attack*(hero?1:Gm.MonsterAttack),EffectiveValue(target,"armor",target.Defense),coefficient,type,ranged,target.Boss,attributes,EffectiveAttributes(target),debuffs);
        var result=DamagePipeline.Resolve(input,Config.CombatRules,combatRandom,critical);LastDamage=result;HitCount++;
        double amount=result.FinalDamage/(hero?Gm.MonsterHp:1);
        if(!hero&&Gm.Invincible)amount=0;
        if(amount>0)foreach(var shield in ActorBuffs(CombatId(target)).Where(b=>SkillConfig.Buffs[b.Id].Mechanic=="shield").ToArray())
        {double absorbed=Math.Min(amount,shield.Value);shield.Value-=absorbed;amount-=absorbed;if(shield.Value<=0)skillState.Buffs.Remove(shield);}
        float before=target.Hp;target.Hp=(float)Math.Max(0,target.Hp-amount);bool killed=!target.Alive;
        if(killed&&Floor.Rooms[CurrentRoom].Monsters.Contains(target))
        {
            var room=Floor.Rooms[CurrentRoom];Kills++;Floor.ActualKillFacts++;GainMonsterExperience(target.RewardXp,target.Level);var loot=RollDrops(target);
            if(loot.Gold>0||loot.Items.Count>0)room.Loot.Add(loot);
            if(room.Clear){room.ContentCleared=true;Note("房间战斗结束，准备救援和拾取");}
        }
        if(killed){skillState.Buffs.RemoveAll(b=>b.Target==CombatId(target));ReleaseCombatTargets(CombatId(target));}
        if(events)
        {
            if(!result.Dmg2.Dodged)React("OnHit",CombatId(attacker),CombatId(target),skill,coefficient,depth);
            if(result.Dmg2.Critical)React("OnCritical",CombatId(attacker),CombatId(target),skill,coefficient,depth);
            if(result.Dmg2.Blocked)React("OnBlock",CombatId(target),CombatId(attacker),skill,coefficient,depth);
            if(target.Hp<before)React("OnDamageDealt",CombatId(attacker),CombatId(target),skill,coefficient,depth);
            if(killed)React("OnKill",CombatId(attacker),CombatId(target),skill,coefficient,depth);
        }
        return result;
    }
    private SkillActionState ActionOf(Actor actor)
    {
        int owner=CombatId(actor);var action=skillState.Actions.FirstOrDefault(a=>a.Owner==owner);
        if(action==null){action=new(){Owner=owner};skillState.Actions.Add(action);}return action;
    }
    private bool RunActiveSkill(Actor actor)
    {
        if(actor.SkillIds.Length==0)return false;
        var action=ActionOf(actor);if(action.Active!=""||action.Recovery>0)return true;
        foreach(string id in actor.SkillIds)
        {
            var skill=SkillConfig.Skills[id];if(SkillCooldown(actor,id)>0)continue;
            var target=Choose(actor,SkillConfig.Abilities.GetValueOrDefault(id),skill.Range);
            if(target==null)continue;
            action.Active=id;action.Target=CombatId(target);action.Windup=skill.Windup/(double)SkillConfig.TickRate;
            if(action.Windup<=0)Commit(action);return true;
        }
        return false;
    }
    private Actor? Choose(Actor owner,AbilityDefinition? ability,float range)
    {
        var enemies=Enemies(CombatId(owner)).Where(e=>ability?.Targeting=="highest_hp"||Vector2.Distance(owner.Position,e.Position)<=range).OrderBy(CombatId).ToArray();
        return ability?.Targeting switch {"lowest_hp"=>enemies.OrderBy(e=>e.Hp).FirstOrDefault(),"highest_hp"=>enemies.OrderByDescending(e=>e.Hp).FirstOrDefault(),"farthest"=>enemies.OrderByDescending(e=>Vector2.DistanceSquared(e.Position,owner.Position)).FirstOrDefault(),_=>enemies.OrderBy(e=>Vector2.DistanceSquared(e.Position,owner.Position)).FirstOrDefault()};
    }
    private void Commit(SkillActionState action)
    {
        action.Windup=0;
        var actor=CombatActor(action.Owner);var target=CombatActor(action.Target);string id=action.Active;var skill=SkillConfig.Skills[id];
        // 目标在前摇期间死亡则取消，不扣冷却；本项目保留单动作槽。
        if(actor is not {Alive:true}||target is not {Alive:true}||!InCombatRoom(actor)||!InCombatRoom(target)||Stunned(actor)){action.Active="";return;}
        double cooldown=skill.Cooldown/(double)SkillConfig.TickRate;
        if(SkillConfig.Abilities.TryGetValue(id,out var ability)&&ability.Form=="buff")
            cooldown=Math.Max(ability.Buffs.Max(b=>SkillConfig.Buffs[b].Duration)/(double)SkillConfig.TickRate*SkillConfig.Setting("buff_cooldown_floor_ratio"),cooldown*(1-SkillConfig.Setting("buff_cooldown_per_level")*(LevelOf(actor,id)-1)));
        action.Cooldowns[id]=cooldown;action.Recovery=skill.Recovery/(double)SkillConfig.TickRate;action.Active="";
        if(Config.Talents.Nodes.TryGetValue(id,out var talent)){CastTalent(actor,target,talent);return;}
        foreach(string effect in skill.Effects)Enqueue(new(){Effect=effect,Skill=id,Owner=action.Owner,Target=action.Target,Coefficient=skill.Coefficient});
    }
    private int LevelOf(Actor actor,string skill)=>Config.Talents.Nodes.ContainsKey(skill)?TalentLevel(actor,skill):actor.SkillLevels.GetValueOrDefault(skill,SkillConfig.Abilities.TryGetValue(skill,out var a)?a.Level:1);
    private void Enqueue(SkillEffectWork work)
    {
        if(work.Depth>SkillConfig.MaxDepth)throw new InvalidDataException("Effect 触发深度超过预算");
        if(skillState.Pending.Count>=SkillConfig.MaxPending)throw new InvalidDataException("Effect 待执行队列超出预算");skillState.Pending.Add(work);
    }
    private void React(string kind,int owner,int target,string skill,double coefficient,int depth,SkillFlightState? flight=null)
    {
        foreach(var buff in skillState.Buffs.Where(b=>b.Target==owner&&b.Remaining>0).ToArray())foreach(string id in SkillConfig.Buffs[buff.Id].Triggers)
        {
            var trigger=SkillConfig.Triggers[id];if(trigger.Event!=kind)continue;
            if(trigger.Filter=="primary_cast"&&(flight==null||flight.Mirrored||!flight.PrimaryFirst))continue;
            Enqueue(new(){Effect=trigger.Effect,Skill=skill,Owner=buff.Owner,Target=target,Coefficient=coefficient,BuffInstance=buff.Instance,Depth=depth+1,Flight=flight});
        }
    }
    private void ApplyBuff(string id,int owner,int target,string ability,double coefficient,SkillFlightState? cause)
    {
        var definition=SkillConfig.Buffs[id];var actor=CombatActor(target);if(actor is not {Alive:true})return;
        var old=skillState.Buffs.FirstOrDefault(b=>b.Id==id&&b.Target==target);
        if(old!=null&&definition.Stacking=="NonStacking")return;
        skillState.Buffs.RemoveAll(b=>b.Id==id&&b.Target==target);
        var buff=new SkillBuffState{Instance=++skillState.NextId,Id=id,Owner=owner,Target=target,Ability=ability,Remaining=definition.Duration/(double)SkillConfig.TickRate,Coefficient=coefficient,Cause=cause,Value=definition.Value};
        if(definition.Mechanic=="shield")buff.Value=AttackSnapshot(CombatActor(owner)!).Attack*definition.Value;
        if(definition.Mechanic=="mirror")buff.Value+=SkillConfig.Setting("mirror_level_bonus")*(LevelOf(CombatActor(owner)!,ability)-1);
        skillState.Buffs.Add(buff);
        actor.Hp=Math.Min(actor.Hp,CombatMaximumHp(actor));
    }
    private void AdvanceSkills(float dt)
    {
        foreach(var buff in skillState.Buffs.ToArray())
        {
            buff.Remaining-=dt;
            if(buff.Remaining<=.000001||CombatActor(buff.Target) is not {Alive:true}){skillState.Buffs.Remove(buff);continue;}
            var def=SkillConfig.Buffs[buff.Id];if(def.Periodic=="")continue;
            buff.Periodic+=dt;double interval=def.Interval/(double)SkillConfig.TickRate;
            while(buff.Periodic+.000001>=interval){buff.Periodic-=interval;Enqueue(new(){Effect=def.Periodic,Skill=buff.Ability,Owner=buff.Owner,Target=buff.Target,BuffInstance=buff.Instance,Coefficient=buff.Coefficient,Flight=buff.Cause});}
        }
        foreach(var action in skillState.Actions.ToArray())
        {
            var owner=CombatActor(action.Owner);if(owner is not {Alive:true}){action.Active="";continue;}
            foreach(string id in action.Cooldowns.Keys.ToArray())
            {double rate=SkillConfig.Abilities.TryGetValue(id,out var a)&&a.Form=="buff"?1:1+Math.Min(SkillConfig.Setting("cooldown_reduction_cap"),EffectiveAttributes(owner).CooldownReduction);action.Cooldowns[id]=Math.Max(0,action.Cooldowns[id]-dt*rate);}
            action.Recovery=Math.Max(0,action.Recovery-dt);
            if(action.Active!=""){
                if(Config.Talents.Nodes.TryGetValue(action.Active,out var node)&&node.Effect=="leap"&&CombatActor(action.Target) is {Alive:true} victim&&!Stunned(owner))
                {
                    Move(owner,victim.Position,dt,(float)(Vector2.Distance(owner.Position,victim.Position)/Math.Max(dt,action.Windup)));
                    var room=Floor.Rooms[CurrentRoom];owner.Position=Vector2.Clamp(owner.Position,room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
                }
                action.Windup-=dt;if(action.Windup<=.000001)Commit(action);}
        }
        foreach(var actor in RoomActors)actor.Hp=Math.Min(actor.Hp,CombatMaximumHp(actor));
        AdvanceFlights(dt);
    }
    private void DrainEffects()
    {
        while(effectBudgetRemaining>0&&skillState.Pending.Count>0)
        {
            effectBudgetRemaining--;
            var work=skillState.Pending[0];skillState.Pending.RemoveAt(0);var owner=CombatActor(work.Owner);if(owner is not {Alive:true})continue;
            var fx=SkillConfig.Effects[work.Effect];var target=CombatActor(work.Target);
            switch(fx.Kind)
            {
                case "Damage":if(target is {Alive:true})SettleDamage(owner,target,work.Skill,work.Coefficient*fx.Share,fx.DamageType,owner.Ranged,depth:work.Depth);break;
                case "ApplyBuff":ApplyBuff(fx.BuffId,work.Owner,SkillConfig.Buffs[fx.BuffId].Target=="enemy"?work.Target:work.Owner,work.Skill,work.Coefficient,work.Flight);break;
                case "AbilityCast":CastAbility(work,SkillConfig.Abilities[fx.MechanicId]);break;
                case "AbilityAttack":case "AbilityDot":case "AbilityCooldown":case "AbilityMirror":BuffEffect(work,fx);break;
                case "SpawnEntity":case "SpawnProjectile":SpawnGeneric(work,fx);break;
                default:throw new InvalidDataException("未知 Effect 类型");
            }
        }
    }
}
