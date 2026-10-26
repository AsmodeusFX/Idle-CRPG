using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>六部位基础定义：一件装备只有一个主属性（攻击或生命），数值随等级线性增长。</summary>
public sealed record EquipmentBase(string Id,string Slot,string Attr,double BaseValue,double PerLevel,string Name);
/// <summary>四品质只放大同级数值，不引入词缀。</summary>
public sealed record EquipmentQuality(string Id,string Name,double Multiplier,int Weight);
/// <summary>实际装备实例：绑定唯一英雄实例，随存档恢复。</summary>
public sealed record EquipmentItem(string BaseId,string Slot,string Attr,int Level,string Quality,double Value)
{
    public double Attack=>Attr=="atk"?Value:0;
    public double Hp=>Attr=="hp"?Value:0;
}
public sealed record EquipmentSave(int Hero,bool Candidate,string Slot,string BaseId,int Level,string Quality,double Value);

public sealed partial class Config
{
    public List<EquipmentBase> EquipmentBases=new();
    public List<EquipmentQuality> EquipmentQualities=new();
    private static readonly string[] GearSlots={"weapon","helm","chest","gloves","boots","ring"};
    /// <summary>装备数值唯一公式：基础值 + 每级成长 × (等级-1)，再乘品质倍率，保留两位。</summary>
    public double EquipmentValue(EquipmentBase spec,int level,EquipmentQuality quality)=>
        Math.Round((spec.BaseValue+spec.PerLevel*(Math.Max(1,level)-1))*quality.Multiplier,2);
    public EquipmentBase EquipmentBaseOf(string id)=>EquipmentBases.Single(b=>b.Id==id);
    public EquipmentQuality EquipmentQualityOf(string id)=>EquipmentQualities.Single(q=>q.Id==id);
    public void LoadEquipment(string root)
    {
        EquipmentBases.Clear();EquipmentQualities.Clear();
        var bases=CsvTable.Load("item/equipment.csv",File.ReadAllText(Path.Combine(root,"item/equipment.csv")),"id","slot","attr","base_value","per_level","name");
        foreach(var r in bases.Rows)EquipmentBases.Add(new(r.Id,r.Required("slot"),r.Required("attr"),r.Number("base_value"),r.Number("per_level"),r.Required("name")));
        var qualities=CsvTable.Load("item/equipment_quality.csv",File.ReadAllText(Path.Combine(root,"item/equipment_quality.csv")),"id","name","multiplier","weight");
        foreach(var r in qualities.Rows)EquipmentQualities.Add(new(r.Id,r.Required("name"),r.Number("multiplier"),r.Integer("weight")));
        if(EquipmentBases.Select(b=>b.Slot).Distinct().Count()!=GearSlots.Length||!GearSlots.All(slot=>EquipmentBases.Any(b=>b.Slot==slot)))throw new InvalidDataException("装备必须覆盖六部位");
        if(EquipmentBases.Any(b=>b.Attr is not ("atk" or "hp")||!(b.BaseValue>0)||b.PerLevel<0||!double.IsFinite(b.BaseValue)||!double.IsFinite(b.PerLevel)))throw new InvalidDataException("装备基础属性非法");
        if(EquipmentQualities.Count!=4||EquipmentQualities.Select(q=>q.Id).Distinct().Count()!=4||EquipmentQualities.Any(q=>!(q.Multiplier>0)||q.Weight<=0)||EquipmentQualities.Sum(q=>q.Weight)!=100)throw new InvalidDataException("装备品质必须四档、倍率为正且权重和=100");
    }
}

public sealed partial class GameSession
{
    /// <summary>每名英雄实例的穿戴与待穿候选；同职业不同实例互不共享。</summary>
    private sealed class GearState
    {
        public readonly Dictionary<string,EquipmentItem> Worn=new();
        public readonly Dictionary<string,EquipmentItem> Candidates=new();
        public (double Attack,double Hp) Totals()
        {
            double attack=0,hp=0;
            foreach(var item in Worn.Values){attack+=item.Attack;hp+=item.Hp;}
            return (attack,hp);
        }
    }
    private readonly Dictionary<string,GearState> gear=new();
    private GearState GearOf(Actor hero)
    {
        if(!gear.TryGetValue(hero.HeroInstanceId,out var state))gear[hero.HeroInstanceId]=state=new();
        return state;
    }
    public EquipmentItem? WornEquipment(Actor hero,string slot)=>GearOf(hero).Worn.GetValueOrDefault(slot);
    public EquipmentItem? CandidateEquipment(Actor hero,string slot)=>GearOf(hero).Candidates.GetValueOrDefault(slot);
    public (double Attack,double Hp) EquipmentTotals(Actor hero)=>GearOf(hero).Totals();

    /// <summary>击杀掉落：[H] 概率由原型参数控制；只掉本轮实际出阵英雄可穿的实例绑定装备。</summary>
    internal void TryDropEquipment(Actor monster)
    {
        if(Heroes.Count==0||lootRandom.NextDouble()>=Config.Progression.EquipmentDropPct)return;
        var hero=Heroes[lootRandom.Next(0,Heroes.Count)];
        var spec=Config.EquipmentBases[lootRandom.Next(0,Config.EquipmentBases.Count)];
        int level=Math.Clamp(monster.Level,1,Config.MaxMonsterLevel);
        var quality=PickQuality();
        var item=new EquipmentItem(spec.Id,spec.Slot,spec.Attr,level,quality.Id,Config.EquipmentValue(spec,level,quality));
        var state=GearOf(hero);
        var best=state.Candidates.GetValueOrDefault(spec.Slot);
        // 候选只保留"更好"者；跨属性不比较优劣，保留最新一件供玩家人工取舍。
        if(best==null||item.Value>best.Value){state.Candidates[spec.Slot]=item;AuditEquipment("monster_drop","candidate",hero,spec.Slot,best,item);}
        else AuditEquipment("monster_drop","inferior_not_kept",hero,spec.Slot,best,best);
        MarkChanged();
    }
    private EquipmentQuality PickQuality()
    {
        int roll=lootRandom.Next(0,100),sum=0;
        foreach(var quality in Config.EquipmentQualities){sum+=quality.Weight;if(roll<sum)return quality;}
        return Config.EquipmentQualities[^1];
    }

    /// <summary>把候选穿上：按"新总减旧总"调整角色数值，保持 Attack/MaxHp 与穿戴总和一致。</summary>
    public bool EquipCandidate(string heroInstanceId,string slot)
    {
        var hero=FindHeroInstance(heroInstanceId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");
        var state=GearOf(hero);
        if(!state.Candidates.TryGetValue(slot,out var candidate))return false;
        state.Worn.TryGetValue(slot,out var previous);
        state.Worn[slot]=candidate;state.Candidates.Remove(slot);
        AuditEquipment("player_command","consume_candidate",hero,slot,candidate,null);
        AuditEquipment("player_command","equip",hero,slot,previous,candidate);
        ApplyGearTotals(hero,state);
        if(previous!=null)OfferBack(hero,previous,candidate);
        Note($"装备 {hero.Name} {slot}：Lv.{candidate.Level} {Config.EquipmentQualityOf(candidate.Quality).Name}");
        MarkChanged();return true;
    }
    /// <summary>仅测试程序集：直接放入候选，用于构造"4件/5件"等边界；不开放给玩家入口。</summary>
    internal void QaOfferEquipment(string heroInstanceId,string baseId,int level,string qualityId)
    {
        var hero=FindHeroInstance(heroInstanceId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");
        var spec=Config.EquipmentBaseOf(baseId);var quality=Config.EquipmentQualityOf(qualityId);
        var item=new EquipmentItem(spec.Id,spec.Slot,spec.Attr,level,quality.Id,Config.EquipmentValue(spec,level,quality));
        var state=GearOf(hero);var best=state.Candidates.GetValueOrDefault(spec.Slot);
        if(best==null||item.Value>best.Value){state.Candidates[spec.Slot]=item;AuditEquipment("qa_fixture","candidate",hero,spec.Slot,best,item);}
    }
    /// <summary>隔离边界检查使用：卸到空候选位，复用唯一属性差量，不新增玩家自动卸装行为。</summary>
    internal bool QaUnequipEquipment(string heroInstanceId,string slot)
    {
        var hero=FindHeroInstance(heroInstanceId)??throw new ArgumentException("英雄实例不存在");var state=GearOf(hero);
        if(state.Candidates.ContainsKey(slot)||!state.Worn.Remove(slot,out var item))return false;
        state.Candidates[slot]=item;ApplyGearTotals(hero,state);AuditEquipment("qa_fixture","unequip",hero,slot,item,null);MarkChanged();return true;
    }
    /// <summary>换下的旧件不销毁：若它仍优于当前候选则回到候选位。</summary>
    private void OfferBack(Actor hero,EquipmentItem previous,EquipmentItem equipped)
    {
        var state=GearOf(hero);
        var best=state.Candidates.GetValueOrDefault(previous.Slot);
        if(best==null||previous.Value>best.Value){state.Candidates[previous.Slot]=previous;AuditEquipment("equip_return","candidate",hero,previous.Slot,best,previous);}
    }
    /// <summary>某部位是否存在"可替换"候选：空位或同属性且更高（跨属性视为人工取舍，不算自动可替换）。</summary>
    public bool HasReplaceable(Actor hero,string slot)
    {
        var state=GearOf(hero);var candidate=state.Candidates.GetValueOrDefault(slot);
        if(candidate==null)return false;
        var worn=state.Worn.GetValueOrDefault(slot);
        return worn==null||(worn.Attr==candidate.Attr&&candidate.Value>worn.Value);
    }
    /// <summary>该英雄可替换部位数量。</summary>
    public int ReplaceableCount(Actor hero)=>Config.EquipmentBases.Count(b=>HasReplaceable(hero,b.Slot));
    /// <summary>该英雄是否还有可升级空间且余额足够（右栏只显示可升级角色）。</summary>
    public bool CanUpgradeHero(Actor hero)=>hero.Level<Config.MaxHeroLevel&&PublicExperience>=ExperienceRequired(hero);
    /// <summary>"穿戴更好装备"：把该英雄所有可替换部位一次穿上（单英雄操作，不是全队批量门槛）。</summary>
    public int EquipBestFor(Actor hero)
    {
        int applied=0;
        foreach(var baseSpec in Config.EquipmentBases)if(HasReplaceable(hero,baseSpec.Slot)&&EquipCandidate(hero.HeroInstanceId,baseSpec.Slot))applied++;
        if(applied>0)Note($"{hero.Name} 一键穿上 {applied} 件更好的装备");
        return applied;
    }
    /// <summary>穿戴全队所有可替换部位（右栏"穿戴所有可替换装备"按钮；单次点击代表玩家明确操作）。</summary>
    public int EquipAllReplaceable()
    {
        int applied=0;
        foreach(var hero in Heroes)foreach(var baseSpec in Config.EquipmentBases)
            if(HasReplaceable(hero,baseSpec.Slot)&&EquipCandidate(hero.HeroInstanceId,baseSpec.Slot))applied++;
        if(applied>0)Note($"穿戴所有可替换装备：{applied} 件");
        return applied;
    }
    /// <summary>可批量换装的数量（供 UI 显示门槛与是否可点）。</summary>
    public int BatchEquipReadyCount()
    {
        int count=0;
        foreach(var hero in Heroes)
        {
            var state=GearOf(hero);
            foreach(var pair in state.Candidates)
            {
                var worn=state.Worn.GetValueOrDefault(pair.Key);
                if(worn==null||(worn.Attr==pair.Value.Attr&&pair.Value.Value>worn.Value))count++;
            }
        }
        return count;
    }
    /// <summary>批量换装：仅当至少5个不同"英雄×部位"存在同属性严格更优候选时执行；4项及以下不批处理。</summary>
    public int BatchEquipCandidates()
    {
        var upgrades=new List<(string Hero,string Slot)>();
        foreach(var hero in Heroes)
        {
            var state=GearOf(hero);
            foreach(var pair in state.Candidates)
            {
                var worn=state.Worn.GetValueOrDefault(pair.Key);
                if(worn==null||(worn.Attr==pair.Value.Attr&&pair.Value.Value>worn.Value))upgrades.Add((hero.HeroInstanceId,pair.Key));
            }
        }
        if(upgrades.Count<5)return 0;
        int applied=0;
        foreach(var (heroId,slot) in upgrades)if(EquipCandidate(heroId,slot))applied++;
        Note($"批量换装 {applied} 件");return applied;
    }
    /// <summary>回城结算：只自动卖出"同属性且不优于已穿"的候选，跨属性候选与更优候选保留给玩家。</summary>
    internal long SellInferiorEquipment()
    {
        long gold=0;
        foreach(var hero in Heroes)
        {
            var state=GearOf(hero);
            foreach(var pair in state.Candidates.ToArray())
            {
                if(!state.Worn.TryGetValue(pair.Key,out var worn))continue;
                if(worn.Attr!=pair.Value.Attr||pair.Value.Value>worn.Value)continue;
                // C1-B F02：出售收益乘率只作用于玩家确认的出售，不影响掉落或离线收益。
                long price=checked((long)Math.Ceiling(pair.Value.Value*0.35*(1+StarSellGoldPct)));
                gold+=price;state.Candidates.Remove(pair.Key);AuditEquipment("village_trade","sell_candidate",hero,pair.Key,pair.Value,null);
            }
        }
        if(gold>0){Note($"回城卖出次优装备，获得 {gold} 金币");MarkChanged();}
        return gold;
    }

    private void ApplyGearTotals(Actor hero,GearState state)
    {
        var totals=state.Totals();
        hero.Attack+=(float)(totals.Attack-hero.EquipmentAttack);
        hero.MaxHp+=(float)(totals.Hp-hero.EquipmentHp);
        hero.EquipmentAttack=(float)totals.Attack;hero.EquipmentHp=(float)totals.Hp;
        if(hero.Hp>hero.MaxHp)hero.Hp=hero.MaxHp;
    }

    internal List<EquipmentSave> CaptureGear()
    {
        var saves=new List<EquipmentSave>();
        foreach(var hero in Heroes)
        {
            int index=Heroes.IndexOf(hero);
            if(!gear.TryGetValue(hero.HeroInstanceId,out var state))continue;
            foreach(var item in state.Worn.Values)saves.Add(new(index,false,item.Slot,item.BaseId,item.Level,item.Quality,item.Value));
            foreach(var item in state.Candidates.Values)saves.Add(new(index,true,item.Slot,item.BaseId,item.Level,item.Quality,item.Value));
        }
        return saves;
    }
    /// <summary>读档重建：装备总和必须与 Actor 已保存的 EquipmentAttack/EquipmentHp 一致，不一致则整体拒绝。</summary>
    internal void RestoreGear(IEnumerable<EquipmentSave> saves)
    {
        gear.Clear();
        foreach(var save in saves)
        {
            var hero=Heroes[save.Hero];
            var spec=Config.EquipmentBaseOf(save.BaseId);
            if(spec.Slot!=save.Slot)throw new InvalidDataException("装备部位与定义不一致："+save.BaseId);
            var item=new EquipmentItem(save.BaseId,save.Slot,spec.Attr,save.Level,save.Quality,save.Value);
            var state=GearOf(hero);
            var target=save.Candidate?state.Candidates:state.Worn;
            if(target.ContainsKey(save.Slot))throw new InvalidDataException("同一英雄同一部位重复装备");
            target[save.Slot]=item;
        }
        foreach(var hero in Heroes)
        {
            var totals=GearOf(hero).Totals();
            if(Math.Abs(hero.EquipmentAttack-totals.Attack)>.01||Math.Abs(hero.EquipmentHp-totals.Hp)>.01)throw new InvalidDataException("装备加成与角色数值不一致");
        }
    }
    internal static void ValidateGear(Config config,IEnumerable<EquipmentSave> saves,int heroCount)
    {
        var seen=new HashSet<(int,bool,string)>();
        foreach(var save in saves)
        {
            if(save.Hero<0||save.Hero>=heroCount)throw new InvalidDataException("装备英雄索引无效");
            var spec=config.EquipmentBases.SingleOrDefault(b=>b.Id==save.BaseId)??throw new InvalidDataException("装备定义不存在："+save.BaseId);
            var quality=config.EquipmentQualities.SingleOrDefault(q=>q.Id==save.Quality)??throw new InvalidDataException("装备品质不存在："+save.Quality);
            if(spec.Slot!=save.Slot||save.Level<1||save.Level>config.MaxMonsterLevel||!double.IsFinite(save.Value)||save.Value<=0)throw new InvalidDataException("装备字段无效："+save.BaseId);
            if(Math.Abs(config.EquipmentValue(spec,save.Level,quality)-save.Value)>.011)throw new InvalidDataException("装备数值与等级品质不一致："+save.BaseId);
            if(!seen.Add((save.Hero,save.Candidate,save.Slot)))throw new InvalidDataException("同一英雄同一部位重复装备");
        }
    }
}
