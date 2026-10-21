using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class Config
{
    public FightAttributeCatalog Attributes { get; private set; }=null!;
    public SkillConfiguration Skills { get; private set; }=null!;
    public CombatRules CombatRules { get; private set; }=null!;
    private readonly Dictionary<string,(BattleAttributes Stats,bool Ranged)> combatProfiles=new();
    private readonly Dictionary<string,int> engagementCapacities=new();
    /// <summary>容量基础值只来自单位配置；当前有效值由会话统一聚合天赋与Buff。</summary>
    public int BaseEngagementCapacity(Actor actor)=>engagementCapacities.TryGetValue(actor.SpecId,out int capacity)?capacity:throw new InvalidDataException("单位缺少接敌容量："+actor.SpecId);
    private void LoadCombat(string directory)
    {
        Attributes=FightAttributeCatalog.Load(directory);Skills=SkillConfiguration.Load(directory,Attributes);CombatRules=CombatRules.Load(directory);
        string path=Path.Combine(directory,"combat/unit_attributes.csv");
        var table=CsvTable.Load(path,File.ReadAllText(path),"id","side","dodge_rate","crit_rate","crit_damage","block_rate","block_reduction","ranged","engagement_capacity");
        foreach(var r in table.Rows)
        {
            double Value(string column)=>r[column]==""?Attributes.Rules[column].Baseline:r.Number(column);
            bool hero=r.Required("side") switch {"hero"=>true,"monster"=>false,_=>throw r.Error("side","未知阵营")};
            if(!(hero?Heroes:Monsters).Any(s=>s.Id==r.Id))throw r.Error("id","单位引用无效");
            var stats=new BattleAttributes {Dodge=Value("dodge_rate"),CriticalChance=Value("crit_rate"),CriticalDamage=Value("crit_damage"),BlockChance=Value("block_rate"),BlockReduction=Value("block_reduction"),AttackSpeed=Attributes.Rules["attack_speed_pct"].Baseline,CooldownReduction=Attributes.Rules["cooldown_reduction_pct"].Baseline};stats.Validate();
            Attributes.ValidateValue("dodge_rate",stats.Dodge);Attributes.ValidateValue("crit_rate",stats.CriticalChance);Attributes.ValidateValue("crit_damage",stats.CriticalDamage);Attributes.ValidateValue("block_rate",stats.BlockChance);Attributes.ValidateValue("block_reduction",stats.BlockReduction);
            combatProfiles.Add(r.Id,(stats,r.Boolean("ranged")));
            int capacity=r.Integer("engagement_capacity");Attributes.ValidateValue("engagement_capacity",capacity);engagementCapacities.Add(r.Id,capacity);
        }
        if(Heroes.Concat(Monsters).Any(u=>!combatProfiles.ContainsKey(u.Id)))throw new InvalidDataException("单位缺少战斗属性配置");
        foreach(var unit in Heroes.Concat(Monsters))
        {Attributes.ValidateValue("atk",unit.Attack);Attributes.ValidateValue("hp",unit.Hp);Attributes.ValidateValue("armor",unit.Defense);Attributes.ValidateValue("move_speed",unit.Speed);Attributes.ValidateValue("attack_range",unit.Range);Attributes.ValidateValue("basic_cooldown",unit.Cooldown);}
    }
    /// <summary>首次生成单位应用额外战斗属性；基础攻击/护甲与成长仍沿用原配置，恢复旧档不覆盖累计成长。</summary>
    public void ApplyCombatProfile(Actor actor)
    {
        if(!combatProfiles.TryGetValue(actor.SpecId,out var profile))throw new InvalidDataException("缺少战斗属性："+actor.SpecId);
        actor.Battle=profile.Stats.Copy();actor.Ranged=profile.Ranged;
    }
    // 当前档的攻击方式属于战斗现场，不能在完整快照恢复时被基础模板覆盖；旧CombatVersion=0仍走ApplyCombatProfile。
    internal void ReconcileCombatMetadata(Actor actor){var hero=Heroes.FirstOrDefault(h=>h.Id==actor.SpecId);if(hero!=null)actor.Name=hero.Name;}
}
