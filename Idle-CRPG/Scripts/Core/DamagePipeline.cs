using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

// 数字顺序兼容历史快照；原第3项独立倍率迁移为威能额外比例。
public enum DamageBucket { Skill,General,Critical,Power,Vulnerable }
/// <summary>各池Value均为加算贡献；威能+50%填0.5，易伤须绑定有效DebuffId。</summary>
public record DamageModifier(DamageBucket Bucket,double Value,string SkillId="",DamageType? Type=null,bool RangedOnly=false,bool BossOnly=false,string DebuffId="");
/// <summary>角色额外战斗属性。原有 Attack/Defense 仍是聚合后的攻击/护甲；临时 Buff 不写回这些持久字段。</summary>
public sealed class BattleAttributes
{
    public double Dodge,CriticalChance,CriticalDamage,BlockChance,BlockReduction,AttackSpeed,CooldownReduction;
    public double FireResistance,ColdResistance,LightningResistance,PoisonResistance;
    public List<DamageModifier> Modifiers=new();
    public BattleAttributes Copy()=>new() {Dodge=Dodge,CriticalChance=CriticalChance,CriticalDamage=CriticalDamage,BlockChance=BlockChance,BlockReduction=BlockReduction,AttackSpeed=AttackSpeed,CooldownReduction=CooldownReduction,FireResistance=FireResistance,ColdResistance=ColdResistance,LightningResistance=LightningResistance,PoisonResistance=PoisonResistance,Modifiers=Modifiers.ToList()};
    /// <summary>原始抗性不提前截断；所有来源合并后仅在伤害防御解析时截断一次。</summary>
    public double Resistance(DamageType type)=>type switch {DamageType.Fire=>FireResistance,DamageType.Cold=>ColdResistance,DamageType.Lightning=>LightningResistance,DamageType.Poison=>PoisonResistance,_=>0};
    public void Validate()
    {
        if(new[]{Dodge,CriticalChance,BlockChance,BlockReduction}.Any(n=>!double.IsFinite(n)||n<0||n>1)||new[]{CriticalDamage,AttackSpeed,CooldownReduction}.Any(n=>!double.IsFinite(n)||n<0)||new[]{FireResistance,ColdResistance,LightningResistance,PoisonResistance}.Any(n=>!double.IsFinite(n))||Modifiers==null)throw new InvalidDataException("战斗属性范围无效");
        foreach(var m in Modifiers)
            if(m==null||!Enum.IsDefined(m.Bucket)||m.Type.HasValue&&!Enum.IsDefined(m.Type.Value)||!double.IsFinite(m.Value)||m.Value<(m.Bucket==DamageBucket.Power?-1:0)||m.Bucket==DamageBucket.Vulnerable&&string.IsNullOrWhiteSpace(m.DebuffId))throw new InvalidDataException("伤害乘区定义无效");
    }
}
/// <summary>混合伤害权重非负且合计1；同实例共享命中事实，最终只应用一次HP。</summary>
public record DamagePortion(DamageType Type,double Weight);
public record DamageInput(string SkillId,double Attack,double Armor,double Coefficient,DamageType Type,bool Ranged,bool Boss,BattleAttributes Attacker,BattleAttributes Defender,IReadOnlySet<string> Debuffs,int AttackerLevel=1,IReadOnlyList<DamagePortion>? Portions=null);
public record CombatOutcome(bool Dodged,bool Blocked,bool Critical);
/// <summary>可审阅的三阶段明细；FinalDamage 是扣护盾/无敌/GM 之前的伤害，实际扣血与奖励只由 GameSession 应用一次。</summary>
public record DamageComponent(DamageType Type,double Weight,double Skill,double General,double Power,double Vulnerable,double Critical,double Defense,double Damage);
public record DamageBreakdown(double Dmg1,CombatOutcome Dmg2,double Skill,double General,double Critical,double Power,double Vulnerable,double Block,double FinalDamage)
{
    public long OriginCast {get;init;}
    public IReadOnlyList<DamageComponent> Components {get;init;}=Array.Empty<DamageComponent>();
}
public sealed record CombatRules(double BaseCritical,double ResistanceMinimum,double ResistanceMaximum,IReadOnlyList<(int Level,double K)> ArmorCurve)
{
    /// <summary>按攻击者等级在CSV控制点间线性插值，禁止目标等级或英雄实时升级改变在途快照。</summary>
    public double ArmorK(int attackerLevel)
    {
        if(attackerLevel<1||attackerLevel>ArmorCurve[^1].Level)throw new InvalidDataException("攻击者等级超过护甲K曲线范围");
        for(int i=1;i<ArmorCurve.Count;i++)if(attackerLevel<=ArmorCurve[i].Level)
        {var a=ArmorCurve[i-1];var b=ArmorCurve[i];return a.K+(b.K-a.K)*(attackerLevel-a.Level)/(b.Level-a.Level);}
        return ArmorCurve[0].K;
    }
    public static CombatRules Load(string root,int maximumLevel)
    {
        string path=Path.Combine(root,"combat/damage_rule.csv");var table=CsvTable.Load(path,File.ReadAllText(path),"id","value");
        double critical=table.Require("base_critical_multiplier").Number("value"),min=table.Require("resistance_minimum").Number("value"),max=table.Require("resistance_maximum").Number("value");
        if(table.Rows.Count!=3||critical<1||min>=0||max<0||max>=1||min>=max)throw new InvalidDataException("伤害规则须明确暴击倍率与允许负抗、无完全免疫的截断边界");
        string curvePath=Path.Combine(root,"combat/armor_curve.csv");var curve=new List<(int Level,double K)>();
        foreach(var row in CsvTable.Load(curvePath,File.ReadAllText(curvePath),"id","k").Rows)
        {int level=row.Integer("id");double k=row.Number("k");if(level<1||k<=0||curve.Count>0&&(level<=curve[^1].Level||k<curve[^1].K))throw new InvalidDataException("护甲K控制点须等级递增、K为正且不倒退");curve.Add((level,k));}
        if(curve.Count==0||curve[0].Level!=1||curve[^1].Level<maximumLevel)throw new InvalidDataException("护甲K曲线必须从1级覆盖全部角色及怪物等级");
        return new(critical,min,max,curve);
    }
}

/// <summary>唯一公式：解析分量→共享闪避/格挡/暴击→分类型防御及4+1→合计伤害；无最终保底或第二套元素伤害入口。</summary>
public static class DamagePipeline
{
    public static DamageBreakdown Resolve(DamageInput input,CombatRules rules,PersistentRandom random,bool? criticalSnapshot=null)
    {
        input.Attacker.Validate();input.Defender.Validate();
        if(!double.IsFinite(input.Attack)||!double.IsFinite(input.Armor)||!double.IsFinite(input.Coefficient)||input.Attack<0||input.Armor<0||input.Coefficient<0||!Enum.IsDefined(input.Type)||input.Debuffs==null)throw new InvalidDataException("伤害输入无效");
        double k=rules.ArmorK(input.AttackerLevel),basis=input.Attack;
        var portions=input.Portions??new[]{new DamagePortion(input.Type,1)};
        if(portions.Count is <1 or >5||portions.Any(p=>p==null||!Enum.IsDefined(p.Type)||!double.IsFinite(p.Weight)||p.Weight<0)||portions.Select(p=>p.Type).Distinct().Count()!=portions.Count||Math.Abs(portions.Sum(p=>p.Weight)-1)>.000001)throw new InvalidDataException("伤害分量须类型唯一、权重非负且合计1");
        bool Roll(double rate)=>rate>=1||rate>0&&random.NextDouble()<rate;
        if(Roll(input.Defender.Dodge))return new(basis,new(true,false,false),0,0,0,0,0,0,0);
        bool block=Roll(input.Defender.BlockChance),critical=criticalSnapshot??Roll(input.Attacker.CriticalChance);
        double blocked=block?1-input.Defender.BlockReduction:1;
        var components=new List<DamageComponent>();
        foreach(var portion in portions)
        {
            var mods=input.Attacker.Modifiers.Where(m=>(m.SkillId==""||m.SkillId==input.SkillId)&&(!m.Type.HasValue||m.Type==portion.Type)&&(!m.RangedOnly||input.Ranged)&&(!m.BossOnly||input.Boss)).ToArray();
            double Sum(DamageBucket bucket)=>mods.Where(m=>m.Bucket==bucket).Sum(m=>m.Value);
            double skill=input.Coefficient+Sum(DamageBucket.Skill),general=1+Sum(DamageBucket.General),power=1+Sum(DamageBucket.Power);
            if(power<0)throw new InvalidDataException("威能加算后倍率不能为负");
            double vulnerable=1+mods.Where(m=>m.Bucket==DamageBucket.Vulnerable&&input.Debuffs.Contains(m.DebuffId)).Sum(m=>m.Value);
            double factor=critical?rules.BaseCritical+input.Attacker.CriticalDamage+Sum(DamageBucket.Critical):1;
            double defense=portion.Type==DamageType.Physical?k/(input.Armor+k):1-Math.Clamp(input.Defender.Resistance(portion.Type),rules.ResistanceMinimum,rules.ResistanceMaximum);
            double damage=basis*portion.Weight*skill*general*power*vulnerable*factor*defense*blocked;
            if(!double.IsFinite(damage)||damage<0)throw new InvalidDataException("伤害分量溢出");
            components.Add(new(portion.Type,portion.Weight,skill,general,power,vulnerable,factor,defense,damage));
        }
        double final=components.Sum(c=>c.Damage);
        if(!double.IsFinite(final)||final<0||final>float.MaxValue)throw new InvalidDataException("伤害乘区溢出");
        var first=components[0];return new(basis,new(false,block,critical),first.Skill,first.General,first.Critical,first.Power,first.Vulnerable,blocked,final){Components=components};
    }
}
