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
    // 仅标记本次效果是否真正改变战斗状态，不参与伤害数值或存档。
    private long effectiveEffectVersion;
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
        hero.SkillIds=ids;PruneSummons();PruneAuraActivations();ResetSkillRoom();MarkChanged();
    }
    private IEnumerable<Actor> RoomActors=>Heroes.Concat(Summons).Concat(Floor.Rooms[CurrentRoom].Monsters);
    /// <summary>ID 顺序固定为英雄、当前房怪物、召唤物；召唤物追加在末尾以免旧存档怪物 ID 位移。</summary>
    private int CombatId(Actor actor)
    {
        if(Heroes.Contains(actor))return Heroes.IndexOf(actor);
        int monsters=Floor.Rooms[CurrentRoom].Monsters.IndexOf(actor);
        if(monsters>=0)return Heroes.Count+monsters;
        return Heroes.Count+Floor.Rooms[CurrentRoom].Monsters.Count+Summons.IndexOf(actor);
    }
    private Actor? CombatActor(int id)
    {
        if(id<0)return null;
        if(id<Heroes.Count)return Heroes[id];
        int monster=id-Heroes.Count;var room=Floor.Rooms[CurrentRoom];
        return monster<room.Monsters.Count?room.Monsters[monster]:Summons.ElementAtOrDefault(monster-room.Monsters.Count);
    }
    /// <summary>敌对判定按 ID 区段：英雄与召唤物为友方，当前房怪物为敌方。</summary>
    private IEnumerable<Actor> Enemies(int owner)=>IsFriendlyId(owner)?Floor.Rooms[CurrentRoom].Monsters.Where(InCombatRoom):Heroes.Concat(Summons).Where(InCombatRoom);
    private IEnumerable<SkillBuffState> ActorBuffs(int id)=>skillState.Buffs.Where(b=>b.Target==id&&b.Remaining>0)
        .GroupBy(b=>BuffGroup(b.Id)).SelectMany(group=>SkillConfig.Buffs[group.First().Id].Stacking=="Strongest"
            ?group.OrderByDescending(b=>SkillConfig.Buffs[b.Id].StackStrength).ThenBy(b=>b.Owner).ThenBy(b=>b.Instance).Take(1):group);
    private string BuffGroup(string id)=>SkillConfig.Buffs[id].StackGroup==""?id:SkillConfig.Buffs[id].StackGroup;
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
        result.FireResistance=EffectiveValue(actor,"fire_resistance",result.FireResistance);result.ColdResistance=EffectiveValue(actor,"cold_resistance",result.ColdResistance);
        result.LightningResistance=EffectiveValue(actor,"lightning_resistance",result.LightningResistance);result.PoisonResistance=EffectiveValue(actor,"poison_resistance",result.PoisonResistance);
        foreach(var attr in ActorBuffs(CombatId(actor)).SelectMany(b=>SkillConfig.Buffs[b.Id].Attributes))
        {
            if(attr.Attribute=="power_damage_pct"){result.Modifiers.Add(new(DamageBucket.Power,attr.Value));continue;}
            if(attr.Attribute=="skill_coefficient_bonus"){result.Modifiers.Add(new(DamageBucket.Skill,attr.Value));continue;}
            if(!attr.Attribute.EndsWith("_damage_pct")||attr.Attribute.EndsWith("vulnerable_damage_pct"))continue;
            DamageType? type=attr.Attribute switch {"physical_damage_pct"=>DamageType.Physical,"fire_damage_pct"=>DamageType.Fire,"cold_damage_pct"=>DamageType.Cold,"lightning_damage_pct"=>DamageType.Lightning,"poison_damage_pct"=>DamageType.Poison,_=>null};
            // 技能/普攻资格用稳定技能 ID 标记；本批源配置尚未投放此类属性。
            if(attr.Attribute=="basic_attack_damage_pct")result.Modifiers.Add(new(DamageBucket.General,attr.Value,IsFriendlyActor(actor)?"basic_player":"basic_monster"));
            else if(attr.Attribute=="skill_damage_pct")foreach(string id in actor.SkillIds)result.Modifiers.Add(new(DamageBucket.General,attr.Value,id));
            else result.Modifiers.Add(new(DamageBucket.General,attr.Value,Type:type,RangedOnly:attr.Attribute=="ranged_damage_pct",BossOnly:attr.Attribute=="boss_damage_pct"));
        }
        return result;
    }
    private SkillAttackSnapshot AttackSnapshot(Actor actor)=>new(){Attack=EffectiveValue(actor,"atk",actor.Attack),Level=actor.Level,Attributes=EffectiveAttributes(actor)};
    /// <summary>包含临时 Buff 的有效生命上限；临时增加上限不自动治疗。</summary>
    public float CombatMaximumHp(Actor actor)
    {
        if(Floors.Count==0||World!=null&&World.Stage!=CampaignStage.Dungeon)return actor.MaxHp;
        return (float)EffectiveValue(actor,"hp",actor.MaxHp);
    }
    private void ResetSkillRoom()
    {
        skillState=new();ClearVisualEvents();monsterLockIndex.Clear();foreach(var actor in Heroes.Concat(Summons))actor.Hp=Math.Min(actor.Hp,actor.MaxHp);
        if(FloorIndex<Floors.Count&&CurrentRoom<Floors[FloorIndex].Rooms.Count)foreach(var actor in Floor.Rooms[CurrentRoom].Monsters)actor.Hp=Math.Min(actor.Hp,actor.MaxHp);
    }

    /// <summary>所有普攻/技能/反击/持续伤害共用一次 HP 应用和奖励结算，死亡后不会重复结算。</summary>
    private DamageBreakdown? SettleDamage(Actor attacker,Actor target,string skill,double coefficient,DamageType type,bool ranged,SkillAttackSnapshot? snapshot=null,bool? critical=null,bool events=true,int depth=0,double powerBonus=0,DamagePortion[]? portions=null,long originCast=0,double baseScale=1)
    {
        if(!attacker.Alive||!target.Alive||!InCombatRoom(target))return null;
        // 召唤物与英雄共用玩家侧伤害缩放；只有英雄受 GM 无敌保护。
        bool friendly=IsFriendlyActor(attacker),hero=Heroes.Contains(attacker);snapshot??=AttackSnapshot(attacker);
        var debuffs=ActorBuffs(CombatId(target)).Select(b=>b.Id).ToHashSet();
        var attributes=snapshot.Attributes.Copy();
        foreach(var buff in ActorBuffs(CombatId(target)))foreach(var attr in SkillConfig.Buffs[buff.Id].Attributes.Where(a=>a.Attribute.EndsWith("vulnerable_damage_pct")))
        {
            DamageType? vulnerability=attr.Attribute switch {"physical_vulnerable_damage_pct"=>DamageType.Physical,"fire_vulnerable_damage_pct"=>DamageType.Fire,"cold_vulnerable_damage_pct"=>DamageType.Cold,"lightning_vulnerable_damage_pct"=>DamageType.Lightning,"poison_vulnerable_damage_pct"=>DamageType.Poison,_=>null};
            attributes.Modifiers.Add(new(DamageBucket.Vulnerable,attr.Value,Type:vulnerability,DebuffId:buff.Id));
        }
        if(powerBonus!=0)attributes.Modifiers.Add(new(DamageBucket.Power,powerBonus));
        if(!double.IsFinite(baseScale)||baseScale<0)throw new InvalidDataException("伤害本体比例无效");
        var input=new DamageInput(skill,snapshot.Attack*(friendly?1:Gm.MonsterAttack)*baseScale,EffectiveValue(target,"armor",target.Defense),coefficient,type,ranged,target.Boss,attributes,EffectiveAttributes(target),debuffs,snapshot.Level,portions);
        var result=DamagePipeline.Resolve(input,Config.CombatRules,combatRandom,critical) with {OriginCast=originCast};LastDamage=result;HitCount++;
        double amount=result.FinalDamage/(friendly?Gm.MonsterHp:1);
        if(!friendly&&Gm.Invincible)amount=0;
        if(amount>0)foreach(var shield in ActorBuffs(CombatId(target)).Where(b=>SkillConfig.Buffs[b.Id].Mechanic=="shield").ToArray())
        {double absorbed=Math.Min(amount,shield.Value);shield.Value-=absorbed;amount-=absorbed;if(absorbed>0)effectiveEffectVersion++;if(shield.Value<=0)skillState.Buffs.Remove(shield);}
        float before=target.Hp;target.Hp=(float)Math.Max(0,target.Hp-amount);bool killed=!target.Alive;
        if(target.Hp<before)effectiveEffectVersion++;
        // 表现事件只记录本次真实 HP 变化；不参与后续触发、奖励或死亡判定。
        PushVisual(CombatVisualKind.Hit,skill,"",CombatId(attacker),CombatId(target),attacker.Position,target.Position,
            Math.Max(0,before-target.Hp),0,type,depth,result.Dmg2.Critical,result.Dmg2.Blocked,result.Dmg2.Dodged,killed);
        if(target.Hp<before&&!killed&&!ranged&&depth==0&&target.Ranged&&!Stunned(target)&&!entryMoved.Contains(target)&&CombatEntryRemaining<=0)
        {
            var retreat=ActionOf(target);
            if(retreat.RetreatCooldown<=0&&!retreat.RetreatMoving)retreat.RetreatThreat=CombatId(attacker);
        }
        if(killed&&Floor.Rooms[CurrentRoom].Monsters.Contains(target))
        {
            var room=Floor.Rooms[CurrentRoom];Kills++;Floor.ActualKillFacts++;GainMonsterExperience(target.RewardXp,target.Level);var loot=RollDrops(target);
            if(loot.Gold>0||loot.Items.Count>0)room.Loot.Add(loot);
            RecordCorpse(target);
            if(room.Clear){room.ContentCleared=true;Note("房间战斗结束，准备救援和拾取");}
        }
        if(killed){if(target.ActiveAuraSkills.Count>0&&Heroes.Contains(target))MarkChanged();target.ActiveAuraSkills.Clear();skillState.Buffs.RemoveAll(b=>b.Target==CombatId(target)||b.Owner==CombatId(target)&&b.AuraSkill!="");ReleaseCombatTargets(CombatId(target));}
        // 召唤物死亡只移除自身，不产尸、不进入怪物奖励或经验路径；主人死亡立即撤销其召唤。
        if(killed&&Summons.Contains(target))MarkChanged();
        if(killed&&Heroes.Contains(target))RemoveSummonsOf(Heroes.IndexOf(target));
        if(events)
        {
            if(!result.Dmg2.Dodged)React("OnHit",CombatId(attacker),CombatId(target),skill,coefficient,depth,originCast:originCast);
            if(result.Dmg2.Critical)React("OnCritical",CombatId(attacker),CombatId(target),skill,coefficient,depth,originCast:originCast);
            if(result.Dmg2.Blocked)React("OnBlock",CombatId(target),CombatId(attacker),skill,coefficient,depth,originCast:originCast);
            if(target.Hp<before)React("OnDamageDealt",CombatId(attacker),CombatId(target),skill,coefficient,depth,originCast:originCast);
            if(killed)React("OnKill",CombatId(attacker),CombatId(target),skill,coefficient,depth,originCast:originCast);
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
            if(actor.ActiveAuraSkills.Contains(id))continue;
            var skill=SkillConfig.Skills[id];if(SkillCooldown(actor,id)>0)continue;
            var target=ChooseSkillTarget(actor,skill);
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
        // 目标在前摇期间死亡则取消，不扣冷却；尸体目标例外，但尸体已被竞争消费时同样取消。
        bool corpseTarget=skill.TargetPolicy=="corpse";
        bool validCorpse=corpseTarget&&target is {Alive:false}&&CorpseInRoom(target)&&CorpseExists(action.Target);
        if(actor is not {Alive:true}||target==null||!InCombatRoom(actor)||(!InCombatRoom(target)&&!validCorpse)||Stunned(actor)){action.Active="";return;}
        double cooldown=skill.Cooldown/(double)SkillConfig.TickRate;
        if(SkillConfig.Abilities.TryGetValue(id,out var ability)&&ability.Form=="buff")
            cooldown=Math.Max(ability.Buffs.Max(b=>SkillConfig.Buffs[b].Duration)/(double)SkillConfig.TickRate*SkillConfig.Setting("buff_cooldown_floor_ratio"),cooldown*(1-SkillConfig.Setting("buff_cooldown_per_level")*(LevelOf(actor,id)-1)));
        action.Cooldowns[id]=cooldown;action.Recovery=skill.Recovery/(double)SkillConfig.TickRate;action.Active="";
        long originCast=++skillState.NextId;
        DamageType castType=DamageType.Physical;
        foreach(string effect in skill.Effects){var definition=SkillConfig.Effects[effect];if(definition.Kind is "Damage" or "Chain" or "CorpseExplosion" or "AreaPulse" or "Heal"){castType=definition.DamageType;break;}}
        PushVisual(CombatVisualKind.Cast,id,"",action.Owner,action.Target,actor.Position,target.Position,0,skill.Range,castType);
        if(Config.Talents.Nodes.TryGetValue(id,out var talent))
        {
            // 绑定节点按 skill.csv/effect.csv 执行，倍率取当前生效等级；未绑定节点保持旧 CastTalent 分派。
            if(talent.SkillId==""){CastTalent(actor,target,talent,originCast);return;}
            double value=TalentValue(actor,talent);
            foreach(string effect in skill.Effects)Enqueue(new(){Effect=effect,Skill=id,Owner=action.Owner,Target=action.Target,Coefficient=value,OriginCast=originCast});
            return;
        }
        foreach(string effect in skill.Effects)Enqueue(new(){Effect=effect,Skill=id,Owner=action.Owner,Target=action.Target,Coefficient=skill.Coefficient,OriginCast=originCast});
    }
    private int LevelOf(Actor actor,string skill)=>Config.Talents.Nodes.ContainsKey(skill)?TalentLevel(actor,skill):actor.SkillLevels.GetValueOrDefault(skill,SkillConfig.Abilities.TryGetValue(skill,out var a)?a.Level:1);
    private void Enqueue(SkillEffectWork work)
    {
        if(work.Depth>SkillConfig.MaxDepth)throw new InvalidDataException("Effect 触发深度超过预算");
        if(work.OriginCast==0)work.OriginCast=++skillState.NextId;
        if(skillState.Pending.Count>=SkillConfig.MaxPending)throw new InvalidDataException("Effect 待执行队列超出预算");skillState.Pending.Add(work);
    }
    private void React(string kind,int owner,int target,string skill,double coefficient,int depth,SkillFlightState? flight=null,long originCast=0)
    {
        long cast=flight?.OriginCast??originCast;
        foreach(var buff in skillState.Buffs.Where(b=>b.Target==owner&&b.Remaining>0).ToArray())foreach(string id in SkillConfig.Buffs[buff.Id].Triggers)
        {
            var trigger=SkillConfig.Triggers[id];if(trigger.Event!=kind)continue;
            if(trigger.Filter=="primary_cast"&&(flight==null||flight.Mirrored||!flight.PrimaryFirst))continue;
            // direct只允许根源命中触发，避免Proc伤害再次触发自身形成无界递归。
            if(trigger.Filter=="direct"&&depth!=0)continue;
            Enqueue(new(){Trigger=id,Effect=trigger.Effect,Skill=skill,Owner=buff.Owner,Target=target,Coefficient=coefficient,BuffInstance=buff.Instance,Depth=depth+1,Flight=flight,OriginCast=cast});
        }
    }
    /// <summary>按proc_limit.csv限次；0表示不限，未声明的触发保持原有无限次行为。计数上限有界，超出时丢弃最旧施法。</summary>
    private bool ProcAllowed(string triggerId,int target,long originCast)
    {
        if(!SkillConfig.ProcLimits.TryGetValue(triggerId,out var limit))return true;
        if(originCast<=0)return true;
        int total=0;int own=0;
        foreach(var row in skillState.ProcFires.Where(r=>r.OriginCast==originCast&&r.Trigger==triggerId))
        {
            total+=row.Count;if(row.Target==target)own+=row.Count;
        }
        if(limit.MaxPerCast>0&&total>=limit.MaxPerCast)return false;
        if(limit.MaxPerTarget>0&&own>=limit.MaxPerTarget)return false;
        return true;
    }
    private void RecordProcSuccess(SkillEffectWork work)
    {
        string triggerId=work.Trigger;int target=work.Target;long originCast=work.OriginCast;
        if(SkillConfig.ProcIcdSeconds.TryGetValue(triggerId,out double seconds)&&seconds>0)
        {
            var icd=skillState.ProcIcds.FirstOrDefault(p=>p.Owner==work.Owner&&p.Trigger==triggerId);
            if(icd==null){icd=new(){Owner=work.Owner,Trigger=triggerId};skillState.ProcIcds.Add(icd);}
            icd.ReadyTick=checked(skillState.ProcTick+(long)Math.Ceiling(seconds*SkillConfig.TickRate));
        }
        if(!SkillConfig.ProcLimits.ContainsKey(triggerId)||originCast<=0)return;
        var fire=skillState.ProcFires.FirstOrDefault(r=>r.OriginCast==originCast&&r.Trigger==triggerId&&r.Target==target);
        if(fire!=null){fire.Count++;return;}
        if(skillState.ProcFires.Count>=4096)foreach(var old in skillState.ProcFires.OrderBy(r=>r.OriginCast).Take(1024).ToArray())skillState.ProcFires.Remove(old);
        skillState.ProcFires.Add(new(){OriginCast=originCast,Trigger=triggerId,Target=target,Count=1});
    }
    private void ApplyBuff(string id,int owner,int target,string ability,double coefficient,SkillFlightState? cause,string auraSkill="",long originCast=0)
    {
        var definition=SkillConfig.Buffs[id];var actor=CombatActor(target);if(actor is not {Alive:true})return;
        bool sourced=definition.Stacking=="Strongest";
        var old=skillState.Buffs.FirstOrDefault(b=>b.Id==id&&b.Target==target&&(!sourced||b.Owner==owner&&b.Ability==ability&&b.AuraSkill==auraSkill));
        if(old!=null&&definition.Stacking=="NonStacking")return;
        if(old!=null)skillState.Buffs.Remove(old);
        var buff=new SkillBuffState{Instance=++skillState.NextId,Id=id,Owner=owner,Target=target,Ability=ability,Remaining=definition.Duration/(double)SkillConfig.TickRate,Coefficient=coefficient,Cause=cause,Value=definition.Value,AuraSkill=auraSkill,OriginCast=originCast};
        if(definition.Mechanic=="shield")buff.Value=AttackSnapshot(CombatActor(owner)!).Attack*definition.Value;
        if(definition.Mechanic=="mirror")buff.Value+=SkillConfig.Setting("mirror_level_bonus")*(LevelOf(CombatActor(owner)!,ability)-1);
        skillState.Buffs.Add(buff);
        effectiveEffectVersion++;
        actor.Hp=Math.Min(actor.Hp,CombatMaximumHp(actor));
        // Buff表现按目标落点记录；光环/旋风/诅咒的持续状态由UI读取 PresentationBuffs 绘制。
        PushVisual(CombatVisualKind.Buff,auraSkill!=""?auraSkill:ability,id,owner,target,
            CombatActor(owner)?.Position??actor.Position,actor.Position,0,0);
    }
    private void AdvanceSkills(float dt)
    {
        skillState.ProcTick=checked(skillState.ProcTick+(long)Math.Ceiling(dt*SkillConfig.TickRate-.000001));
        SynchronizeAuras();
        foreach(var buff in skillState.Buffs.ToArray())
        {
            if(buff.AuraSkill=="")buff.Remaining-=dt;
            if(buff.Remaining<=.000001||CombatActor(buff.Target) is not {Alive:true}){skillState.Buffs.Remove(buff);continue;}
        }
        var activeBuffs=skillState.Buffs.Select(b=>b.Target).Distinct().SelectMany(ActorBuffs).ToArray();
        foreach(var buff in activeBuffs)
        {
            var def=SkillConfig.Buffs[buff.Id];if(def.Periodic=="")continue;
            buff.Periodic+=dt;double interval=def.Interval/(double)SkillConfig.TickRate;
            while(buff.Periodic+.000001>=interval){buff.Periodic-=interval;Enqueue(new(){Effect=def.Periodic,Skill=buff.Ability,Owner=buff.Owner,Target=buff.Target,BuffInstance=buff.Instance,Coefficient=buff.Coefficient,Flight=buff.Cause,OriginCast=buff.Cause?.OriginCast??buff.OriginCast});}
        }
        foreach(var action in skillState.Actions.ToArray())
        {
            var owner=CombatActor(action.Owner);if(owner is not {Alive:true}){action.Active="";continue;}
            foreach(string id in action.Cooldowns.Keys.ToArray())
            {double rate=SkillConfig.Abilities.TryGetValue(id,out var a)&&a.Form=="buff"?1:1+Math.Min(SkillConfig.Setting("cooldown_reduction_cap"),EffectiveAttributes(owner).CooldownReduction);action.Cooldowns[id]=Math.Max(0,action.Cooldowns[id]-dt*rate);}
            action.Recovery=Math.Max(0,action.Recovery-dt);
            action.RetreatCooldown=Math.Max(0,action.RetreatCooldown-dt);
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
            bool proc=work.Trigger!="";
            if(proc&&(!InCombatRoom(owner)||!SkillConfig.Triggers.TryGetValue(work.Trigger,out var trigger)||trigger.Effect!=work.Effect||!skillState.Buffs.Any(b=>b.Instance==work.BuffInstance&&b.Remaining>0&&b.Owner==work.Owner&&b.Id==trigger.OwnerBuff)||
                skillState.ProcIcds.Any(p=>p.Owner==work.Owner&&p.Trigger==work.Trigger&&p.ReadyTick>skillState.ProcTick)||!ProcAllowed(work.Trigger,work.Target,work.OriginCast)||!ProcHasFlightCapacity(work,fx)))continue;
            if(proc)
            {
                var chance=SkillConfig.Triggers[work.Trigger].Chance;
                if((fx.Kind is "Damage" or "ApplyBuff" or "Heal" or "SpawnEntity" or "SpawnProjectile")&&(target==null||!InCombatRoom(target)))continue;
                if(chance<=0||chance<1&&combatRandom.NextDouble()>=chance)continue;
            }
            long successBefore=effectiveEffectVersion;
            switch(fx.Kind)
            {
                case "Damage":
                    if(target is {Alive:true})foreach(var hit in fx.Extent>0?Enemies(work.Owner).Where(e=>Vector2.Distance(e.Position,target.Position)<=fx.Extent).ToArray():new[]{target})
                        SettleDamage(owner,hit,work.Skill,work.Coefficient*fx.Share,fx.DamageType,owner.Ranged,work.Snapshot,depth:work.Depth,portions:fx.Portions,originCast:work.OriginCast);
                    break;
                case "ApplyBuff":
                    if(target is {Alive:true})foreach(var hit in BuffEffectTargets(work,fx,target))ApplyBuff(fx.BuffId,work.Owner,CombatId(hit),work.Skill,work.Coefficient,work.Flight,originCast:work.OriginCast);
                    break;
                case "Heal":if(work.BuffInstance==0||ActorBuffs(work.Target).Any(b=>b.Instance==work.BuffInstance))HealTarget(target,fx.Share,work.Skill);break;
                case "MaintainAura":if(!owner.ActiveAuraSkills.Contains(work.Skill)){owner.ActiveAuraSkills.Add(work.Skill);MarkChanged();effectiveEffectVersion++;}SynchronizeAuras(work.Owner,work.Skill,work.OriginCast);break;
                case "AbilityCast":CastAbility(work,SkillConfig.Abilities[fx.MechanicId]);break;
                case "AbilityAttack":case "AbilityDot":case "AbilityCooldown":case "AbilityMirror":BuffEffect(work,fx);break;
                case "Summon":SummonEffect(work,fx);break;
                case "Chain":ChainEffect(work,fx);break;
                case "CorpseExplosion":CorpseExplosionEffect(work,fx);break;
                case "AreaPulse":AreaPulseEffect(work,fx);break;
                case "SpawnEntity":case "SpawnProjectile":SpawnGeneric(work,fx);break;
                default:throw new InvalidDataException("未知 Effect 类型");
            }
            if(proc&&effectiveEffectVersion!=successBefore)RecordProcSuccess(work);
        }
    }
}
