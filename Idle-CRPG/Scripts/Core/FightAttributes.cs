using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record FightAttributeDefinition(string Id,string Name,string Category,string ValueType,string Aggregation,string[] Operations,double Default,string Display,bool Visible);
public record FightAttributeRule(double Baseline,double? Minimum,double? Maximum);
/// <summary>属性 ID、允许操作、默认值及边界的唯一注册表；注册新 ID 不会凭空创造新的战斗机制。</summary>
public sealed class FightAttributeCatalog
{
    public IReadOnlyDictionary<string,FightAttributeDefinition> Definitions { get; }
    public IReadOnlyDictionary<string,FightAttributeRule> Rules { get; }
    private FightAttributeCatalog(Dictionary<string,FightAttributeDefinition> definitions,Dictionary<string,FightAttributeRule> rules){Definitions=definitions;Rules=rules;}
    public static FightAttributeCatalog Load(string root)
    {
        CsvTable Table(string name,params string[] columns){string path=Path.Combine(root,"combat",name+".csv");return CsvTable.Load(path,File.ReadAllText(path),columns);}
        var definitions=new Dictionary<string,FightAttributeDefinition>(StringComparer.Ordinal);
        foreach(var r in Table("fightattr","attr_id","name","category","value_type","aggregation","allowed_ops","default_value","display_format","ui_visible").Rows)
        {
            var def=new FightAttributeDefinition(r.Id,r.Required("name"),r.Required("category"),r.Required("value_type"),r.Required("aggregation"),r.List("allowed_ops"),r.Number("default_value"),r.Required("display_format"),r.Boolean("ui_visible"));
            string[] permitted=def.Aggregation switch {"base_flat_pct"=>new[]{"base","flat","pct"},"add" or "add_clamp"=>new[]{"add"},"multiply"=>new[]{"multiply"},_=>throw r.Error("aggregation","未知聚合方式")};
            if(def.ValueType is not ("number" or "ratio")||def.Display is not ("number" or "integer" or "percentage")||def.Operations.Length==0||def.Operations.Distinct().Count()!=def.Operations.Length||def.Operations.Any(op=>!permitted.Contains(op)))throw r.Error("allowed_ops","属性操作与聚合方式不匹配");
            definitions.Add(def.Id,def);
        }
        var rules=new Dictionary<string,FightAttributeRule>(StringComparer.Ordinal);
        foreach(var r in Table("fightattr_rules","id","baseline","min_value","max_value").Rows)
        {
            if(!definitions.ContainsKey(r.Id))throw r.Error("id","规则引用了未注册属性");
            double? min=r["min_value"]==""?null:r.Number("min_value"),max=r["max_value"]==""?null:r.Number("max_value");double baseline=r.Number("baseline");
            if(min>max||min.HasValue&&baseline<min||max.HasValue&&baseline>max)throw r.Error("baseline","基线与属性上下界不匹配");
            rules.Add(r.Id,new(baseline,min,max));
        }
        if(rules.Count!=definitions.Count)throw new InvalidDataException("属性注册和规则表必须一一对应");
        var catalog=new FightAttributeCatalog(definitions,rules);
        foreach(var def in definitions.Values){catalog.ValidateValue(def.Id,def.Default);if(def.Default!=rules[def.Id].Baseline)throw new InvalidDataException("属性默认值与基线不一致："+def.Id);}
        foreach(string required in new[]{"atk","hp","armor","move_speed","attack_range","basic_cooldown","dodge_rate","crit_rate","crit_damage","block_rate","block_reduction","attack_speed_pct","cooldown_reduction_pct","all_damage_pct","skill_damage_pct","basic_attack_damage_pct","physical_damage_pct","fire_damage_pct","cold_damage_pct","lightning_damage_pct","poison_damage_pct","ranged_damage_pct","boss_damage_pct","skill_coefficient_bonus","power_damage_pct","fire_resistance","cold_resistance","lightning_resistance","poison_resistance","vulnerable_damage_pct"})catalog.Require(required);
        return catalog;
    }
    public FightAttributeDefinition Require(string id)=>Definitions.TryGetValue(id,out var definition)?definition:throw new InvalidDataException("未注册属性："+id);
    public void ValidateValue(string id,double value)
    {var rule=Rules[Require(id).Id];if(!double.IsFinite(value)||rule.Minimum.HasValue&&value<rule.Minimum||rule.Maximum.HasValue&&value>rule.Maximum)throw new InvalidDataException("属性值超出边界："+id);}
    public void ValidateContribution(BuffAttributeSpec attribute)
    {
        var def=Require(attribute.Attribute);
        if(attribute.Operation=="base"||!def.Operations.Contains(attribute.Operation)||!double.IsFinite(attribute.Value)||attribute.Operation=="multiply"&&attribute.Value<=0||(def.Category is "general_damage" or "skill_damage" or "vulnerable")&&attribute.Value<0)throw new InvalidDataException("属性贡献操作无效："+attribute.Attribute);
    }
    public double Aggregate(string id,double basis,IEnumerable<BuffAttributeSpec> values)
    {
        var def=Require(id);var declarations=values.ToArray();foreach(var declaration in declarations)ValidateContribution(declaration);
        double value=def.Aggregation switch {
            "base_flat_pct"=>(basis+declarations.Where(a=>a.Operation=="flat").Sum(a=>a.Value))*(1+declarations.Where(a=>a.Operation=="pct").Sum(a=>a.Value)),
            "add" or "add_clamp"=>basis+declarations.Sum(a=>a.Value),
            "multiply"=>declarations.Aggregate(basis,(v,a)=>v*a.Value),
            _=>throw new InvalidDataException("属性聚合未实现")};
        if(!double.IsFinite(value))throw new InvalidDataException("属性聚合溢出："+id);
        var rule=Rules[id];return Math.Clamp(value,rule.Minimum??double.MinValue,rule.Maximum??double.MaxValue);
    }
}
