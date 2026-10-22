using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public enum DamageType { Physical,Fire,Cold,Lightning,Poison }
/// <summary>动作定义与运行状态分离；所有时间轴均为本项目 20Hz 逻辑 Tick。</summary>
public record SkillDefinition(string Id,string Kind,int Cooldown,int Windup,int Recovery,double Coefficient,float Range,string[] Effects,string Name,string TargetPolicy="enemy",string Condition="");
public record EffectDefinition(string Id,string Kind,DamageType DamageType,double Share,string BuffId,string MechanicId,string Impact,float Speed,float Extent,string[] ResolveEffects,int Lifetime,DamagePortion[]? Portions=null,int ProjectileCount=1,float SpreadDegrees=0);
public record BuffAttributeSpec(string Attribute,string Operation,double Value);
public record BuffDefinition(string Id,int Duration,string Stacking,BuffAttributeSpec[] Attributes,string Periodic,int Interval,string[] Triggers,string Mechanic,double Value,double Extra,string Target,string StackGroup="",double StackStrength=0);
public record TriggerDefinition(string Id,string Event,string Effect,string OwnerBuff,string Filter);
public record AbilityDefinition(string Id,string Form,string Targeting,string Hits,double Root,int Level,int MaxLevel,string[] Effects,string[] Buffs);
/// <summary>迁移 Idle-Diablo AbilityEffect 的机制参数；距离与速度在加载时从原像素统一换算为世界单位。</summary>
public record AbilityEffectDefinition(string Id,string Type,string Condition,double ConditionValue,double Power,double Duration,double Delay,double Interval,float Range,string Trajectory,int Count,double Hold,float ArcMin,float ArcMax,float Speed,float Spread,double VolleyInterval,double VolleyJitter,float SpawnJitter,double Pierce,float Radius,bool All,float Band,float Knockback,float Gather,string BuffId);

/// <summary>迁移源项目七张 Skill/Effect/Buff/Trigger/Ability 表；保留稳定 ID，加载时检查全部引用及执行环。</summary>
public sealed class SkillConfiguration
{
    public Dictionary<string,SkillDefinition> Skills { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,EffectDefinition> Effects { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,BuffDefinition> Buffs { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,TriggerDefinition> Triggers { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,AbilityDefinition> Abilities { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,AbilityEffectDefinition> AbilityEffects { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,double> Settings { get; }=new(StringComparer.Ordinal);
    public int TickRate { get; private set; }
    public int EffectBudget { get; private set; }
    public int MaxDepth { get; private set; }
    public int MaxPending { get; private set; }
    public float CollisionRadius { get; private set; }
    public double Setting(string id)=>Settings[id];
    private static readonly string[] SkillColumns="id,role,kind,cooldown_ticks,windup_ticks,recovery_ticks,coefficient,range,effect_ids,name,cast_sound_id,hit_sound_id,target_policy,cast_condition".Split(',');
    private static readonly string[] EffectColumns="id,role,kind,damage_type,share,extent,speed,impact_effect,behavior,spawn_tick,buff_id,entity_kind,lifetime_ticks,resolve_effect_ids,mechanic_id,damage_parts,projectile_count,spread_degrees".Split(',');
    private static readonly string[] BuffColumns="id,duration_ticks,stacking_policy,attributes,periodic_effect,periodic_interval_ticks,trigger_ids,mechanic,value,extra,target,stack_group,stack_strength".Split(',');
    private static readonly string[] AbilityEffectColumns="id,name,effect_type,condition,condition_value,power,skill_flat,duration,delay,tick_interval,range,trajectory,projectile_count,hover_time,arc_min,arc_max,speed,spread,volley_interval,volley_jitter,spawn_jitter,pierce_chance,aoe_radius,aoe_all,band,knockback,gather,buff_id".Split(',');
    public static SkillConfiguration Load(string directory,FightAttributeCatalog? attributes=null)
    {
        CsvTable Table(string name,params string[] columns){string path=Path.Combine(directory,"skill",name+".csv");return CsvTable.Load(path,File.ReadAllText(path),columns);}
        var c=new SkillConfiguration();
        var runtime=Table("runtime","id","value");
        c.TickRate=runtime.Require("ticks_per_second").Integer("value");c.EffectBudget=runtime.Require("effect_budget").Integer("value");c.MaxDepth=runtime.Require("max_depth").Integer("value");c.MaxPending=runtime.Require("max_pending").Integer("value");c.CollisionRadius=(float)runtime.Require("collision_radius").Number("value");
        if(c.TickRate!=20||c.EffectBudget<1||c.MaxDepth<1||c.MaxPending<c.EffectBudget||c.CollisionRadius<=0)throw new InvalidDataException("技能运行预算或固定 Tick 配置无效");
        foreach(var row in Table("ability_settings","id","value").Rows)c.Settings.Add(row.Id,row.Number("value"));
        foreach(string key in new[]{"pixels_per_unit","skill_level_bonus","self_buff_heal_fraction","buff_cooldown_per_level","buff_cooldown_floor_ratio","mirror_offset","mirror_delay","fallback_radius","max_reacquire","max_flights","cooldown_reduction_cap","default_projectile_speed","mirror_level_bonus","counter_lifetime","execute_multiplier"})
            if(!c.Settings.ContainsKey(key))throw new InvalidDataException("技能设置缺少 "+key);
        double pixels=c.Setting("pixels_per_unit");
        if(c.Setting("default_projectile_speed")<=0||c.Setting("mirror_level_bonus")<0||c.Setting("counter_lifetime")<=0||c.Setting("execute_multiplier")<1)throw new InvalidDataException("技能补充机制参数无效");
        if(pixels<=0||c.Setting("skill_level_bonus")<0||c.Setting("self_buff_heal_fraction") is <0 or >1||c.Setting("buff_cooldown_per_level")<0||c.Setting("buff_cooldown_floor_ratio")<1||c.Setting("mirror_delay")<0||c.Setting("fallback_radius")<0||c.Setting("cooldown_reduction_cap")<0||c.Setting("max_flights")<1||c.Setting("max_flights")%1!=0||c.Setting("max_reacquire")<0||c.Setting("max_reacquire")%1!=0)throw new InvalidDataException("Ability 设置无效");
        foreach(var r in Table("skill",SkillColumns).Rows)
        {
            var def=new SkillDefinition(r.Id,r.Required("kind"),r.Integer("cooldown_ticks"),r.Integer("windup_ticks"),r.Integer("recovery_ticks"),r.Number("coefficient"),(float)r.Number("range"),r.List("effect_ids"),r.Required("name"),r.Required("target_policy"),r["cast_condition"]);
            if(def.TargetPolicy is not ("enemy" or "self" or "ally_lowest_hp")||def.Condition is not ("" or "nearby_melee" or "missing_buff"))throw r.Error("target_policy","技能目标或释放条件无效");
            if(def.Kind is not ("BasicAttack" or "ActiveSkill")||def.Cooldown<1||def.Windup<0||def.Recovery<0||def.Coefficient<0||def.Range<=0||def.Effects.Length==0)throw r.Error("kind","技能时间轴或效果无效");
            c.Skills.Add(def.Id,def);
        }
        foreach(var r in Table("effect",EffectColumns).Rows)
        {
            string kind=r.Required("kind");
            string[] allowed=kind switch {
                "Damage"=>new[]{"damage_type","share","damage_parts","extent"},"ApplyBuff"=>new[]{"buff_id","extent"},
                "Heal"=>new[]{"share"},"MaintainAura"=>new[]{"buff_id","extent"},
                "SpawnProjectile"=>new[]{"extent","speed","impact_effect","behavior","spawn_tick","projectile_count","spread_degrees"},
                "SpawnEntity"=>new[]{"entity_kind","lifetime_ticks","resolve_effect_ids"},
                "AbilityCast" or "AbilityAttack" or "AbilityDot" or "AbilityCooldown" or "AbilityMirror"=>new[]{"mechanic_id"},
                _=>throw r.Error("kind","未实现的效果类型")};
            foreach(string column in EffectColumns.Skip(3))if(!allowed.Contains(column)&&r[column]!="")throw r.Error(column,"字段不适用于此效果类型");
            if(kind=="SpawnProjectile"&&(r["behavior"]!="HitAllOnPathThenExpire"||r["spawn_tick"]!="HoldOnSpawnTick"))throw r.Error("behavior","未实现的弹道策略");
            DamageType damage=kind=="Damage"?r.Enum<DamageType>("damage_type"):DamageType.Physical;
            var def=new EffectDefinition(r.Id,kind,damage,kind is "Damage" or "Heal"?r.Number("share"):0,r["buff_id"],r["mechanic_id"],r["impact_effect"],kind=="SpawnProjectile"?(float)r.Number("speed"):0,r["extent"]!=""?(float)r.Number("extent"):0,r.List("resolve_effect_ids"),kind=="SpawnEntity"?r.Integer("lifetime_ticks"):0);
            if(kind=="Damage"&&r["damage_parts"]!="")
            {
                var parts=r.List("damage_parts").Select(value=>
                {
                    var pair=value.Split(':');
                    if(pair.Length!=2||!Enum.TryParse<DamageType>(pair[0],out var type)||!Enum.IsDefined(type)||!double.TryParse(pair[1],NumberStyles.Float,CultureInfo.InvariantCulture,out double weight)||!double.IsFinite(weight)||weight<0)throw r.Error("damage_parts","分量须为合法伤害类型:非负权重");
                    return new DamagePortion(type,weight);
                }).ToArray();
                if(parts.Length is <1 or >5||parts.Select(p=>p.Type).Distinct().Count()!=parts.Length||Math.Abs(parts.Sum(p=>p.Weight)-1)>.000001||parts[0].Type!=damage)throw r.Error("damage_parts","分量须类型唯一、合计1，首项与damage_type一致");
                def=def with {Portions=parts};
            }
            if(def.Share<0||kind=="SpawnProjectile"&&(def.Speed<=0||def.Extent<0)||kind=="SpawnEntity"&&def.Lifetime<1)throw r.Error("kind","效果参数无效");
            if(!float.IsFinite(def.Extent)||def.Extent<0||kind=="Heal"&&def.Share>1||kind=="MaintainAura"&&def.Extent<=0)throw r.Error("extent","效果范围/治疗比例无效");
            if(kind=="SpawnProjectile")
            {
                int count=r.Integer("projectile_count");float spread=(float)r.Number("spread_degrees");
                if(count is <1 or >64||!float.IsFinite(spread)||spread<0||spread>180||count==1&&spread!=0)throw r.Error("projectile_count","扇形弹数1～64、夹角0～180度；单发夹角须为0");
                def=def with {ProjectileCount=count,SpreadDegrees=spread};
            }
            c.Effects.Add(def.Id,def);
        }
        foreach(var r in Table("buff",BuffColumns).Rows)
        {
            var attrs=r.List("attributes").Select(text=>
            {
                var parts=text.Split(':');
                if(parts.Length!=3||!double.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!double.IsFinite(value))throw r.Error("attributes","Invalid attribute declaration");
                attributes?.ValidateContribution(new(parts[0],parts[1],value));
                return new BuffAttributeSpec(parts[0],parts[1],value);
            }).ToArray();
            var def=new BuffDefinition(r.Id,r.Integer("duration_ticks"),r.Required("stacking_policy"),attrs,r["periodic_effect"],r["periodic_interval_ticks"]==""?0:r.Integer("periodic_interval_ticks"),r.List("trigger_ids"),r["mechanic"],r["value"]==""?0:r.Number("value"),r["extra"]==""?0:r.Number("extra"),r["target"],r["stack_group"],r["stack_strength"]==""?0:r.Number("stack_strength"));
            if(def.Duration<1||def.Stacking is not ("NonStacking" or "RefreshDuration" or "Strongest")||def.Periodic!=""&&def.Interval<1||def.Periodic==""&&def.Interval!=0||!new[]{"","haste","shield","dot","chill","stun","crit_reduce","mirror","forced"}.Contains(def.Mechanic)||def.Target is not ("" or "self" or "enemy" or "ally")||def.Value<0||def.Extra<0||def.StackStrength<0||def.Mechanic=="chill"&&def.Value>1||def.Stacking!="Strongest"&&(def.StackGroup!=""||r["stack_strength"]!=""))throw r.Error("mechanic","Buff 生命周期或机制无效");
            c.Buffs.Add(def.Id,def);
        }
        foreach(var r in Table("trigger","id","event_kind","effect_id","owner_buff","origin_filter").Rows)
        {
            if(!new[]{"OnHit","OnCritical","OnBlock","OnDamageDealt","OnKill","OnSkillCommit"}.Contains(r["event_kind"])||r["origin_filter"] is not ("" or "primary_cast"))throw r.Error("event_kind","未实现的事件或来源过滤");
            c.Triggers.Add(r.Id,new(r.Id,r["event_kind"],r.Required("effect_id"),r.Required("owner_buff"),r["origin_filter"]));
        }
        foreach(var r in Table("ability","id","source_id","form","targeting","hits","cast_root","level","max_level","effect_ids","buff_ids").Rows)
        {
            var def=new AbilityDefinition(r.Id,r.Required("form"),r["targeting"],r["hits"],r.Number("cast_root"),r.Integer("level"),r.Integer("max_level"),r.List("effect_ids"),r.List("buff_ids"));
            if(r.Required("source_id")!=def.Id||def.Form is not ("projectile" or "target" or "ground" or "buff")||def.Targeting is not ("" or "nearest" or "lowest_hp" or "highest_hp" or "farthest")||def.Hits is not ("" or "ground")||def.Root<0||def.Level<1||def.MaxLevel<def.Level||def.Form!="buff"&&def.Effects.Length==0||def.Form=="buff"&&def.Buffs.Length==0)throw r.Error("form","Ability 形态或等级无效");
            c.Abilities.Add(def.Id,def);
        }
        foreach(var r in Table("ability_effect",AbilityEffectColumns).Rows)
        {
            double N(string key)=>r.Number(key);float P(string key)=>(float)(N(key)/pixels);
            var def=new AbilityEffectDefinition(r.Id,r.Required("effect_type"),r["condition"],N("condition_value"),N("power"),N("duration"),N("delay"),N("tick_interval"),P("range"),r["trajectory"],r.Integer("projectile_count"),N("hover_time"),P("arc_min"),P("arc_max"),P("speed"),P("spread"),N("volley_interval"),N("volley_jitter"),P("spawn_jitter"),N("pierce_chance"),P("aoe_radius"),r.Integer("aoe_all")==1,P("band"),P("knockback"),P("gather"),r["buff_id"]);
            if(def.Type is not ("attack" or "auto_attack" or "shorten_cooldown" or "mirror_cast")||def.Condition is not ("" or "target_hp_below" or "target_has_state")||def.Trajectory is not ("" or "line_shot" or "line_pierce" or "sky_drop" or "arc_homing")||def.Power<0||N("skill_flat")!=0||def.Duration<0||def.Delay<0||def.Interval<0||def.Count is <1 or >64||def.Pierce is <0 or >1||def.Hold<0||def.ArcMin>def.ArcMax||def.Speed<0||def.Radius<0||def.Knockback<0||def.Gather<0||def.VolleyInterval<0||def.VolleyJitter<0||def.SpawnJitter<0||r.Integer("aoe_all") is not (0 or 1))throw r.Error("effect_type","Ability 效果参数无效");
            c.AbilityEffects.Add(def.Id,def);
        }
        c.ValidateGraph();return c;
    }
    private void ValidateGraph()
    {
        void Need(bool ok,string value){if(!ok)throw new InvalidDataException("技能配置引用或机制不匹配："+value);}
        Need(Skills.ContainsKey("basic_player")&&Skills.ContainsKey("basic_monster"),"缺少普攻定义");
        foreach(var s in Skills.Values)foreach(var id in s.Effects)Need(Effects.ContainsKey(id),s.Id+" → "+id);
        foreach(var group in Buffs.Values.GroupBy(b=>b.StackGroup==""?b.Id:b.StackGroup))Need(group.Select(b=>b.Stacking).Distinct().Count()==1,group.Key+"同组Buff策略冲突");
        foreach(var skill in Skills.Values)
        {
            if(skill.Condition=="missing_buff")Need(skill.Effects.Any(id=>Effects[id].Kind=="ApplyBuff"),skill.Id+"缺少待检查Buff");
            if(skill.TargetPolicy=="self")Need(!skill.Effects.Any(id=>Effects[id].Kind=="Damage"&&Effects[id].Extent<=0),skill.Id+"自身中心伤害必须声明范围");
        }
        foreach(var a in Abilities.Values){Need(Skills.ContainsKey(a.Id),a.Id);foreach(var id in a.Effects)Need(AbilityEffects.ContainsKey(id),id);foreach(var id in a.Buffs)Need(Buffs.ContainsKey(id),id);if(a.Root>0)Need(Buffs.TryGetValue("buff_stun",out var stun)&&stun.Mechanic=="stun",a.Id);}
        foreach(var fx in AbilityEffects.Values)if(fx.BuffId!="")Need(Buffs.ContainsKey(fx.BuffId),fx.Id);
        foreach(var b in Buffs.Values){if(b.Periodic!="")Need(Effects.ContainsKey(b.Periodic),b.Id);foreach(var id in b.Triggers)Need(Triggers.TryGetValue(id,out var t)&&t.OwnerBuff==b.Id,b.Id);}
        foreach(var t in Triggers.Values)Need(Effects.ContainsKey(t.Effect)&&Buffs.TryGetValue(t.OwnerBuff,out var b)&&b.Triggers.Contains(t.Id),t.Id);
        foreach(var e in Effects.Values)
        {
            if(e.Kind is "ApplyBuff" or "MaintainAura")Need(Buffs.ContainsKey(e.BuffId),e.Id);
            if(e.Kind=="MaintainAura")Need(Buffs[e.BuffId].Stacking=="Strongest"&&Buffs[e.BuffId].Target is "ally" or "enemy",e.Id+"光环须声明独立来源择强及友敌范围");
            if(e.Kind=="SpawnProjectile")Need(Effects.ContainsKey(e.Impact),e.Id);
            foreach(var id in e.ResolveEffects)Need(Effects.ContainsKey(id),e.Id);
            if(e.Kind=="AbilityCast")Need(Abilities.ContainsKey(e.MechanicId),e.Id);
            if(e.Kind is "AbilityAttack" or "AbilityCooldown" or "AbilityMirror")Need(AbilityEffects.ContainsKey(e.MechanicId),e.Id);
            if(e.Kind=="AbilityDot")Need(Buffs.TryGetValue(e.MechanicId,out var buff)&&buff.Mechanic=="dot",e.Id);
        }
        var active=new HashSet<string>();var visited=new HashSet<string>();
        void Visit(string id)
        {
            if(active.Contains(id))throw new InvalidDataException("技能执行引用成环："+id);
            if(!visited.Add(id))return;active.Add(id);
            if(id.StartsWith("e:"))
            {
                var e=Effects[id[2..]];if(e.Kind is "ApplyBuff" or "MaintainAura")Visit("b:"+e.BuffId);
                if(e.Kind=="SpawnProjectile")Visit("e:"+e.Impact);foreach(var next in e.ResolveEffects)Visit("e:"+next);
                if(e.Kind=="AbilityCast"){var a=Abilities[e.MechanicId];foreach(var buff in a.Buffs)Visit("b:"+buff);foreach(var fx in a.Effects)if(AbilityEffects[fx].BuffId!="")Visit("b:"+AbilityEffects[fx].BuffId);}
            }
            else {var b=Buffs[id[2..]];if(b.Periodic!="")Visit("e:"+b.Periodic);foreach(var trigger in b.Triggers)Visit("e:"+Triggers[trigger].Effect);}
            active.Remove(id);
        }
        foreach(var id in Effects.Keys)Visit("e:"+id);
    }
}
