using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public enum DamageBucket { Skill,General,Critical,Multiplier,Vulnerable }
/// <summary>Value 在加算池中是额外比例，在 Multiplier 池中是直接倍率；易伤必须携带稳定 DebuffId。</summary>
public record DamageModifier(DamageBucket Bucket,double Value,string SkillId="",DamageType? Type=null,bool RangedOnly=false,bool BossOnly=false,string DebuffId="");
/// <summary>角色额外战斗属性。原有 Attack/Defense 仍是聚合后的攻击/护甲；临时 Buff 不写回这些持久字段。</summary>
public sealed class BattleAttributes
{
    public double Dodge,CriticalChance,CriticalDamage,BlockChance,BlockReduction,AttackSpeed,CooldownReduction;
    public List<DamageModifier> Modifiers=new();
    public BattleAttributes Copy()=>new() {Dodge=Dodge,CriticalChance=CriticalChance,CriticalDamage=CriticalDamage,BlockChance=BlockChance,BlockReduction=BlockReduction,AttackSpeed=AttackSpeed,CooldownReduction=CooldownReduction,Modifiers=Modifiers.ToList()};
    public void Validate()
    {
        if(new[]{Dodge,CriticalChance,BlockChance,BlockReduction}.Any(n=>!double.IsFinite(n)||n<0||n>1)||new[]{CriticalDamage,AttackSpeed,CooldownReduction}.Any(n=>!double.IsFinite(n)||n<0)||Modifiers==null)throw new InvalidDataException("战斗属性范围无效");
        foreach(var m in Modifiers)
            if(m==null||!Enum.IsDefined(m.Bucket)||m.Type.HasValue&&!Enum.IsDefined(m.Type.Value)||!double.IsFinite(m.Value)||m.Value<0||m.Bucket==DamageBucket.Multiplier&&m.Value<=0||m.Bucket==DamageBucket.Vulnerable&&string.IsNullOrWhiteSpace(m.DebuffId))throw new InvalidDataException("伤害乘区定义无效");
    }
}
public record DamageInput(string SkillId,double Attack,double Armor,double Coefficient,DamageType Type,bool Ranged,bool Boss,BattleAttributes Attacker,BattleAttributes Defender,IReadOnlySet<string> Debuffs);
public record CombatOutcome(bool Dodged,bool Blocked,bool Critical);
/// <summary>可审阅的三阶段明细；FinalDamage 是扣护盾/无敌/GM 之前的伤害，实际扣血与奖励只由 GameSession 应用一次。</summary>
public record DamageBreakdown(double Dmg1,CombatOutcome Dmg2,double Skill,double General,double Critical,double Multiplier,double Vulnerable,double Block,double FinalDamage);
public sealed record CombatRules(double MinimumBase,double BaseCritical)
{
    public static CombatRules Load(string root)
    {
        string path=Path.Combine(root,"combat/damage_rule.csv");var table=CsvTable.Load(path,File.ReadAllText(path),"id","value");
        double min=table.Require("minimum_base_damage").Number("value"),critical=table.Require("base_critical_multiplier").Number("value");
        if(min<0||critical<1)throw new InvalidDataException("伤害公式参数无效");return new(min,critical);
    }
}

/// <summary>唯一伤害公式：DMG1 减法保底 → DMG2 闪避/格挡/暴击瀑布 → DMG3 五类乘区及格挡。无元素抗性旁路。</summary>
public static class DamagePipeline
{
    public static DamageBreakdown Resolve(DamageInput input,CombatRules rules,PersistentRandom random,bool? criticalSnapshot=null)
    {
        input.Attacker.Validate();input.Defender.Validate();
        if(!double.IsFinite(input.Attack)||!double.IsFinite(input.Armor)||!double.IsFinite(input.Coefficient)||input.Attack<0||input.Armor<0||input.Coefficient<0||!Enum.IsDefined(input.Type))throw new InvalidDataException("伤害输入无效");
        double basis=Math.Max(rules.MinimumBase,input.Attack-input.Armor);
        bool Roll(double rate)=>rate>=1||rate>0&&random.NextDouble()<rate;
        if(Roll(input.Defender.Dodge))return new(basis,new(true,false,false),0,0,0,0,0,0,0);
        bool block=Roll(input.Defender.BlockChance),critical=criticalSnapshot??Roll(input.Attacker.CriticalChance);
        var mods=input.Attacker.Modifiers.Where(m=>(m.SkillId==""||m.SkillId==input.SkillId)&&(!m.Type.HasValue||m.Type==input.Type)&&(!m.RangedOnly||input.Ranged)&&(!m.BossOnly||input.Boss)).ToArray();
        double Sum(DamageBucket bucket)=>mods.Where(m=>m.Bucket==bucket).Sum(m=>m.Value);
        double skill=input.Coefficient+Sum(DamageBucket.Skill),general=1+Sum(DamageBucket.General);
        double crit=critical?rules.BaseCritical+input.Attacker.CriticalDamage+Sum(DamageBucket.Critical):1;
        double multi=mods.Where(m=>m.Bucket==DamageBucket.Multiplier).Aggregate(1d,(v,m)=>v*m.Value);
        double vulnerable=mods.Where(m=>m.Bucket==DamageBucket.Vulnerable&&input.Debuffs.Contains(m.DebuffId)).GroupBy(m=>m.DebuffId).Aggregate(1d,(v,group)=>v*(1+group.Sum(m=>m.Value)));
        double blocked=block?1-input.Defender.BlockReduction:1;
        double final=basis*skill*general*crit*multi*vulnerable*blocked;
        if(!double.IsFinite(final)||final<0||final>float.MaxValue)throw new InvalidDataException("伤害乘区溢出");
        return new(basis,new(false,block,critical),skill,general,crit,multi,vulnerable,blocked,final);
    }
}
