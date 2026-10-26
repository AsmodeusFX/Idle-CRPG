using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed record StarNode(string Id,string Name,int MaxLevel,string Currency,IReadOnlyList<long> Costs,string Attribute,string Operation,double ValuePerLevel,string Icon,string TargetId="");
public sealed record StarLayout(string Id,int Col,int Row,IReadOnlyList<string> Prerequisites,string Label);
public sealed record StarRankSave(string Id,int Level);
public sealed record StarProgressSave(StarRankSave[] Ranks);
public readonly record struct StarTotals(double Attack,double Hp);

/// <summary>迁移 Idle-Diablo 三表合同；唯一根、任一前置、无环。首版仅全队固定攻击和生命上限。</summary>
public sealed class StarConfiguration
{
    internal bool AllowRetiredPurchases=false;
    public bool IsRetired(string id)=>Nodes.TryGetValue(id,out var node)&&node.Attribute is "skill_slots" or "party_slots" or "talent_reset" or "talent_reset_discount";
    /// <summary>已实现真实消费者的星图属性清单（C1-B）：每项在Stars/战斗/经济/存档中有唯一读取点。</summary>
    public static readonly string[] KnownAttributes=new[]{
        // 旧9节点保留的既有属性
        "attack","hp","xp_gain_pct","class_trait_unlock","party_slots","skill_slots","talent_reset","talent_reset_discount",
        // C1-B B1：战斗属性（EffectiveValue/EffectiveAttributes 已有消费）
        "attack_speed_pct","crit_rate","element_damage_pct","block_rate","element_resistance",
        // C1-B B1：评级与消耗品/经济/离线
        "attack_rating","defense_rating","scroll_damage_pct","potion_duration_pct","potion_effect_pct",
        "travel_pct","kill_gold_pct","sell_gold_pct","offline_pct"};
    public static bool IsKnownAttribute(string attribute)=>Array.IndexOf(KnownAttributes,attribute)>=0;
    public const string ContentHeader="id,name,max_level,cost_currency,cost,attr_id,operation,value_per_level,icon,target_id";
    public const string LayoutHeader="id,col,row,prereq,label",PlanHeader="id,intent";
    public const string ContentPath="star/star.csv",LayoutPath="star/star_layout.csv",PlanPath="star/star_plan.csv";
    public IReadOnlyDictionary<string,StarNode> Nodes {get;}
    public IReadOnlyDictionary<string,StarLayout> Layout {get;}
    public StarConfiguration(IEnumerable<StarNode> nodes,IEnumerable<StarLayout> layout)
    {
        var n=nodes.ToDictionary(x=>x.Id,x=>x with {Costs=Array.AsReadOnly(x.Costs.ToArray())},StringComparer.Ordinal);
        var l=layout.ToDictionary(x=>x.Id,x=>x with {Prerequisites=Array.AsReadOnly(x.Prerequisites.ToArray())},StringComparer.Ordinal);
        if(n.Count==0||n.Count>2000||!n.Keys.ToHashSet().SetEquals(l.Keys))throw new InvalidOperationException("星图内容与布局 ID 必须一一对应且不能为空（最多 2000 节点）");
        foreach(var node in n.Values)
        {
            if(string.IsNullOrWhiteSpace(node.Id)||string.IsNullOrWhiteSpace(node.Name)||node.MaxLevel<1||node.MaxLevel>10000||node.Costs.Count!=node.MaxLevel||node.Costs.Any(c=>c<0)||node.Currency!="gold"||!double.IsFinite(node.ValuePerLevel)||node.ValuePerLevel<0||node.ValuePerLevel*node.MaxLevel>float.MaxValue||node.Icon is not ("attack" or "defense" or "utility"))throw new InvalidOperationException("星图节点名称、等级、金币价目或效果无效："+node.Id);
            if(node.Attribute=="")
            {if(node.Operation!=""||node.ValuePerLevel!=0)throw new InvalidOperationException("占位节点不能有效果");}
            else if(node.Attribute=="party_slots"&&(node.MaxLevel!=1||node.ValuePerLevel!=1))throw new InvalidOperationException("扩编节点必须单级且增加一席");else if(node.Attribute=="class_trait_unlock"&&(node.MaxLevel!=1||node.ValuePerLevel!=1||string.IsNullOrWhiteSpace(node.TargetId)))throw new InvalidOperationException("职业特性解锁节点必须单级、加一且指定职业："+node.Id);else if(node.Attribute!="class_trait_unlock"&&node.TargetId!="")throw new InvalidOperationException("只有职业特性解锁节点可以指定目标："+node.Id);else if(!IsKnownAttribute(node.Attribute)||node.Operation!="flat")throw new InvalidOperationException("星图属性不在已实现消费清单内："+node.Id+" → "+node.Attribute);
        }
        if(n.Values.Any(x=>(x.Attribute is "skill_slots" or "talent_reset") && (x.MaxLevel!=1||x.ValuePerLevel!=1))||n.Values.Where(x=>x.Attribute=="talent_reset_discount").Sum(x=>x.ValuePerLevel*x.MaxLevel)>1)throw new InvalidOperationException("技能槽/洗点解锁必须单级加一，洗点折扣总和不得超过100%");
        if(l.Values.Count(x=>x.Prerequisites.Count==0)!=1)throw new InvalidOperationException("星图必须恰好一个根节点");
        if(l.Values.Select(x=>(x.Col,x.Row)).Distinct().Count()!=l.Count)throw new InvalidOperationException("星图格子不能重叠");
        if(l.Values.Any(x=>Math.Abs((long)x.Col)>10000||Math.Abs((long)x.Row)>10000))throw new InvalidOperationException("星图坐标超出编辑范围");
        var visited=new HashSet<string>();var visiting=new HashSet<string>();
        void Visit(string id)
        {
            if(visited.Contains(id))return;
            if(!visiting.Add(id))throw new InvalidOperationException("星图连线不能成环");
            var parents=l[id].Prerequisites;
            if(parents.Distinct().Count()!=parents.Count)throw new InvalidOperationException("前置不能重复");
            foreach(var p in parents){if(!l.ContainsKey(p))throw new InvalidOperationException("前置不存在："+p);Visit(p);}
            visiting.Remove(id);visited.Add(id);
        }
        foreach(var id in l.Keys)Visit(id);
        Nodes=new System.Collections.ObjectModel.ReadOnlyDictionary<string,StarNode>(n);Layout=new System.Collections.ObjectModel.ReadOnlyDictionary<string,StarLayout>(l);
    }
    public static StarConfiguration Load(string content,string layout)
    {
        var nodes=CsvTable.Load(ContentPath,content,ContentHeader.Split(',')).Rows.Select(r=>new StarNode(r.Id,r.Required("name"),r.Integer("max_level"),r.Required("cost_currency"),Array.AsReadOnly(r.List("cost").Select(v=>long.Parse(v,CultureInfo.InvariantCulture)).ToArray()),r["attr_id"],r["operation"],r.Number("value_per_level"),r["icon"],r["target_id"]));
        var positions=CsvTable.Load(LayoutPath,layout,LayoutHeader.Split(',')).Rows.Select(r=>new StarLayout(r.Id,r.Integer("col"),r.Integer("row"),Array.AsReadOnly(r.List("prereq")),r["label"]));
        return new(nodes,positions);
    }
}

public sealed class StarProgression
{
    private readonly Dictionary<string,int> ranks=new(StringComparer.Ordinal);
    public StarConfiguration Configuration {get;}
    public StarProgression(StarConfiguration config,StarProgressSave? saved=null)
    {
        Configuration=config;
        if(saved!=null)
        {
            if(saved.Ranks==null)throw new InvalidOperationException("缺少星图等级");
            foreach(var rank in saved.Ranks)
                if(rank==null||!config.Nodes.TryGetValue(rank.Id,out var n)||rank.Level<1||rank.Level>n.MaxLevel||n.Attribute==""||!ranks.TryAdd(rank.Id,rank.Level))throw new InvalidOperationException("星图存档存在未知、重复或非法等级");
            foreach(var id in ranks.Keys)if(!PrerequisitesMet(id))throw new InvalidOperationException("星图存档前置未点亮");
        }
        _=Totals();
    }
    public int Level(string id)=>ranks.GetValueOrDefault(id);
    public bool PrerequisitesMet(string id)=>Configuration.Layout[id].Prerequisites.Count==0||Configuration.Layout[id].Prerequisites.Any(p=>Level(p)>0);
    public bool Visible(string id)=>(Configuration.AllowRetiredPurchases||!Configuration.IsRetired(id))&&(Level(id)>0||PrerequisitesMet(id));
    public bool CanBuy(string id,long gold,out string reason)
    {
        if(!Configuration.Nodes.TryGetValue(id,out var n))reason="节点不存在";
        else if(!Configuration.AllowRetiredPurchases&&Configuration.IsRetired(id))reason="旧技能槽/扩席购买已退休；历史购买保留，不自动退款";
        else if(n.Attribute=="")reason="占位节点尚未配置效果";
        else if(!PrerequisitesMet(id))reason="任一点亮一条前置即可解锁";
        else if(Level(id)>=n.MaxLevel)reason="已达最高等级";
        else if(gold<n.Costs[Level(id)])reason=$"金币不足：需要 {n.Costs[Level(id)]}，持有 {gold}";
        else {reason="";return true;}
        return false;
    }
    public StarTotals Totals()
    {
        double attack=0,hp=0;
        foreach(var rank in ranks){var n=Configuration.Nodes[rank.Key];if(n.Attribute=="attack")attack+=n.ValuePerLevel*rank.Value;else if(n.Attribute=="hp")hp+=n.ValuePerLevel*rank.Value;}
        if(!double.IsFinite(attack)||!double.IsFinite(hp)||attack>float.MaxValue||hp>float.MaxValue)throw new InvalidOperationException("星图加成溢出");
        return new(attack,hp);
    }
    public StarProgressSave Capture()=>new(ranks.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new StarRankSave(p.Key,p.Value)).ToArray());
}

public sealed partial class Config
{
    public StarConfiguration Stars=null!;
    private void LoadStars(string directory)=>Stars=StarConfiguration.Load(File.ReadAllText(Path.Combine(directory,StarConfiguration.ContentPath)),File.ReadAllText(Path.Combine(directory,StarConfiguration.LayoutPath)));
}
