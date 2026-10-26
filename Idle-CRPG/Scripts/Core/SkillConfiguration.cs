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
public record TriggerDefinition(string Id,string Event,string Effect,string OwnerBuff,string Filter,double Chance=1);
public record AbilityDefinition(string Id,string Form,string Targeting,string Hits,double Root,int Level,int MaxLevel,string[] Effects,string[] Buffs);
/// <summary>QA/B2 独立召唤原型：属性按主人施法时的基础值快照继承，不继承临时光环或装备现场。
/// role 显式声明近战/远程/支援战斗定位，用于入场部署，不按显示名或职业推断；支援仅影响落位，不新增战斗行为。
/// duration_seconds 是唯一生命周期定义：0为永续召唤，正数按20Hz固定逻辑步限时；不另设并行字段。</summary>
public record SummonDefinition(string Id,string Name,int MaxCount,double HpRatio,double AttackRatio,double DefenseRatio,float Speed,float Range,double Cooldown,int EngagementCapacity,string Role,double Duration=0)
{
    /// <summary>只有显式 ranged 角色参与远程展开与远程承伤标签；支援不会因射程被误判为远程。</summary>
    public bool RangedCombat=>Role=="ranged";
    /// <summary>0秒即永续；只有永续召唤获得脱战自动补员资格。</summary>
    public bool Permanent=>Duration<=0;
}
/// <summary>QA/C1 有界链原型：max_targets 含链根，逐跳系数乘以 damage_decay；仅显式 Chain Effect 获得连锁资格。</summary>
public record ChainDefinition(string Id,string Name,int MaxTargets,float JumpRange,double DamageDecay);
/// <summary>QA/C3 Proc 限次原型：0 表示不限；限次按一次根源施法（OriginCast）与目标计数，不改变既有无限次触发。</summary>
public record ProcLimitDefinition(string Id,int MaxPerCast,int MaxPerTarget);
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
    public Dictionary<string,SummonDefinition> Summons { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,ChainDefinition> Chains { get; }=new(StringComparer.Ordinal);
    public Dictionary<string,ProcLimitDefinition> ProcLimits { get; }=new(StringComparer.Ordinal);
    /// <summary>按TriggerID的可选ICD秒数；缺行或0不启用，运行时向上量化为模拟Tick。</summary>
    public Dictionary<string,double> ProcIcdSeconds { get; }=new(StringComparer.Ordinal);
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
    private static readonly string[] SummonColumns="id,name,max_count,hp_ratio,attack_ratio,defense_ratio,speed,range,cooldown_seconds,engagement_capacity,role,duration_seconds".Split(',');
    private static readonly string[] ChainColumns="id,name,max_targets,jump_range,damage_decay".Split(',');
    private static readonly string[] ProcLimitColumns="id,max_per_cast,max_per_target".Split(',');
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
            if(def.TargetPolicy is not ("enemy" or "self" or "ally_lowest_hp" or "corpse")||def.Condition is not ("" or "nearby_melee" or "missing_buff"))throw r.Error("target_policy","技能目标或释放条件无效");
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
                "Summon"=>new[]{"mechanic_id"},
                "Chain"=>new[]{"damage_type","share","mechanic_id"},
                "CorpseExplosion"=>new[]{"damage_type","share","extent"},
                "AreaPulse"=>new[]{"damage_type","share","extent"},
                _=>throw r.Error("kind","未实现的效果类型")};
            foreach(string column in EffectColumns.Skip(3))if(!allowed.Contains(column)&&r[column]!="")throw r.Error(column,"字段不适用于此效果类型");
            if(kind=="SpawnProjectile"&&(r["behavior"]!="HitAllOnPathThenExpire"||r["spawn_tick"]!="HoldOnSpawnTick"))throw r.Error("behavior","未实现的弹道策略");
            DamageType damage=kind is "Damage" or "Chain" or "CorpseExplosion" or "AreaPulse"?r.Enum<DamageType>("damage_type"):DamageType.Physical;
            var def=new EffectDefinition(r.Id,kind,damage,kind is "Damage" or "Heal" or "Chain" or "CorpseExplosion" or "AreaPulse"?r.Number("share"):0,r["buff_id"],r["mechanic_id"],r["impact_effect"],kind=="SpawnProjectile"?(float)r.Number("speed"):0,r["extent"]!=""?(float)r.Number("extent"):0,r.List("resolve_effect_ids"),kind=="SpawnEntity"?r.Integer("lifetime_ticks"):0);
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
            if(!float.IsFinite(def.Extent)||def.Extent<0||kind=="Heal"&&def.Share>1||kind=="MaintainAura"&&def.Extent<=0||kind=="CorpseExplosion"&&def.Extent<=0||kind=="AreaPulse"&&def.Extent<=0)throw r.Error("extent","效果范围/治疗比例无效");
            if(kind=="SpawnProjectile")
            {
                int count=r.Integer("projectile_count");float spread=(float)r.Number("spread_degrees");
                if(count is <1 or >64||!float.IsFinite(spread)||spread<0||spread>180||count==1&&spread!=0)throw r.Error("projectile_count","扇形弹数1～64、夹角0～180度；单发夹角须为0");
                def=def with {ProjectileCount=count,SpreadDegrees=spread};
            }
            c.Effects.Add(def.Id,def);
        }
        foreach(var r in Table("summon",SummonColumns).Rows)
        {
            var def=new SummonDefinition(r.Id,r.Required("name"),r.Integer("max_count"),r.Number("hp_ratio"),r.Number("attack_ratio"),r.Number("defense_ratio"),(float)r.Number("speed"),(float)r.Number("range"),r.Number("cooldown_seconds"),r.Integer("engagement_capacity"),r.Required("role"),r.Number("duration_seconds"));
            if(string.IsNullOrWhiteSpace(def.Name)||def.MaxCount is <1 or >5||def.HpRatio<=0||def.AttackRatio<=0||def.DefenseRatio<0||!float.IsFinite(def.Speed)||def.Speed<=0||!float.IsFinite(def.Range)||def.Range<=0||!double.IsFinite(def.Cooldown)||def.Cooldown<=0||def.EngagementCapacity<0||def.Role is not ("melee" or "ranged" or "support"))throw r.Error("max_count","召唤原型属性/数量/接敌容量/战斗定位无效");
            // 生命周期只有这一列：0永续、正数限时，负值或非有限值拒绝，避免“未配置”被当成限时0秒。
            if(!double.IsFinite(def.Duration)||def.Duration<0)throw r.Error("duration_seconds","召唤持续时间必须是非负有限秒数（0=永续）");
            attributes?.ValidateValue("engagement_capacity",def.EngagementCapacity);
            c.Summons.Add(def.Id,def);
        }
        foreach(var r in Table("chain",ChainColumns).Rows)
        {
            var def=new ChainDefinition(r.Id,r.Required("name"),r.Integer("max_targets"),(float)r.Number("jump_range"),r.Number("damage_decay"));
            if(string.IsNullOrWhiteSpace(def.Name)||def.MaxTargets is <1 or >12||!float.IsFinite(def.JumpRange)||def.JumpRange<=0||!double.IsFinite(def.DamageDecay)||def.DamageDecay is <0 or >1)throw r.Error("max_targets","链式原型目标数1～12、跳跃距离>0、每跳衰减0～1");
            c.Chains.Add(def.Id,def);
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
        string triggerPath=Path.Combine(directory,"skill/trigger.csv"),triggerText=File.ReadAllText(triggerPath);
        string[] triggerColumns="id,event_kind,effect_id,owner_buff,origin_filter".Split(',');
        var triggerRecords=CsvTable.ReadRecords(triggerPath,triggerText);
        bool hasChance=triggerRecords.Count>0&&triggerRecords[0].Cells.Contains("proc_chance");
        if(hasChance)triggerColumns=triggerColumns.Append("proc_chance").ToArray();
        foreach(var r in CsvTable.Load(triggerPath,triggerText,triggerColumns).Rows)
        {
            if(!new[]{"OnHit","OnCritical","OnBlock","OnDamageDealt","OnKill","OnSkillCommit"}.Contains(r["event_kind"])||r["origin_filter"] is not ("" or "primary_cast" or "direct"))throw r.Error("event_kind","未实现的事件或来源过滤");
            double chance=hasChance&&r["proc_chance"]!=""?r.Number("proc_chance"):1;
            if(!double.IsFinite(chance)||chance<0||chance>1)throw r.Error("proc_chance","触发概率须在0～1内，缺省为1");
            // owner_buff 为空表示"由已学天赋trigger节点注册的常驻触发源"，不依附任何Buff，注销随洗点发生。
            c.Triggers.Add(r.Id,new(r.Id,r["event_kind"],r.Required("effect_id"),r["owner_buff"],r["origin_filter"],chance));
        }
        foreach(var r in Table("proc_limit",ProcLimitColumns).Rows)
        {
            var def=new ProcLimitDefinition(r.Id,r.Integer("max_per_cast"),r.Integer("max_per_target"));
            if(!c.Triggers.ContainsKey(def.Id)||def.MaxPerCast is <0 or >1000||def.MaxPerTarget is <0 or >1000||def.MaxPerCast==0&&def.MaxPerTarget==0)throw r.Error("max_per_cast","Proc限次须引用trigger，单个上限1～1000且至少一项生效");
            c.ProcLimits.Add(def.Id,def);
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
        string icdPath=Path.Combine(directory,"skill/proc_icd.csv");
        if(File.Exists(icdPath))foreach(var r in CsvTable.Load(icdPath,File.ReadAllText(icdPath),"id","icd_seconds").Rows)
        {
            double seconds=r.Number("icd_seconds");
            if(!c.Triggers.ContainsKey(r.Id)||!double.IsFinite(seconds)||seconds<0||seconds* c.TickRate>int.MaxValue)throw r.Error("icd_seconds","ICD须引用Trigger且为可量化的非负有限秒数");
            c.ProcIcdSeconds.Add(r.Id,seconds);
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
            if(skill.TargetPolicy=="corpse")Need(skill.Effects.Any(id=>Effects[id].Kind=="CorpseExplosion"),skill.Id+"尸体目标缺少尸爆效果");
            if(skill.TargetPolicy=="self")Need(!skill.Effects.Any(id=>Effects[id].Kind=="Damage"&&Effects[id].Extent<=0),skill.Id+"自身中心伤害必须声明范围");
        }
        foreach(var a in Abilities.Values){Need(Skills.ContainsKey(a.Id),a.Id);foreach(var id in a.Effects)Need(AbilityEffects.ContainsKey(id),id);foreach(var id in a.Buffs)Need(Buffs.ContainsKey(id),id);if(a.Root>0)Need(Buffs.TryGetValue("buff_stun",out var stun)&&stun.Mechanic=="stun",a.Id);}
        foreach(var fx in AbilityEffects.Values)if(fx.BuffId!="")Need(Buffs.ContainsKey(fx.BuffId),fx.Id);
        foreach(var b in Buffs.Values){if(b.Periodic!="")Need(Effects.ContainsKey(b.Periodic),b.Id);foreach(var id in b.Triggers)Need(Triggers.TryGetValue(id,out var t)&&t.OwnerBuff==b.Id,b.Id);}
        foreach(var t in Triggers.Values)
        {
            Need(Effects.ContainsKey(t.Effect),t.Id);
            // owner_buff 为空=天赋常驻触发源：注册/注销由已学talent.trigger_id决定，不要求Buff反向引用。
            if(t.OwnerBuff=="")continue;
            Need(Buffs.TryGetValue(t.OwnerBuff,out var triggerOwner)&&triggerOwner.Triggers.Contains(t.Id),t.Id);
        }
        foreach(var e in Effects.Values)
        {
            if(e.Kind is "ApplyBuff" or "MaintainAura")Need(Buffs.ContainsKey(e.BuffId),e.Id);
            if(e.Kind=="MaintainAura")Need(Buffs[e.BuffId].Stacking=="Strongest"&&Buffs[e.BuffId].Target is "ally" or "enemy",e.Id+"光环须声明独立来源择强及友敌范围");
            if(e.Kind=="SpawnProjectile")Need(Effects.ContainsKey(e.Impact),e.Id);
            foreach(var id in e.ResolveEffects)Need(Effects.ContainsKey(id),e.Id);
            if(e.Kind=="Summon")Need(Summons.ContainsKey(e.MechanicId),e.Id+" → "+e.MechanicId);
            if(e.Kind=="Chain")Need(Chains.ContainsKey(e.MechanicId),e.Id+" → "+e.MechanicId);
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
