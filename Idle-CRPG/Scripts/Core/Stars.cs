using System;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    public StarProgression Stars {get;private set;}=null!;
    private double starAttack,starHp;
    /// <summary>本轮公共XP获取倍率：星图成长类节点的合法加成，默认 1。不改变击杀经验本身的守恒对账口径。</summary>
    public double XpGainMultiplier=>1+Stars.Configuration.Nodes.Values.Where(n=>n.Attribute=="xp_gain_pct").Sum(n=>n.ValuePerLevel*Stars.Level(n.Id));
    /// <summary>手动消耗金币升级；先预演全部角色属性，再一次提交金币和等级，失败不扣费。</summary>
    public bool BuyStar(string id,out string reason)
    {
        if(!Stars.CanBuy(id,Gold,out reason))return false;
        var ranks=Stars.Capture().Ranks.Where(r=>r.Id!=id).Append(new StarRankSave(id,Stars.Level(id)+1)).ToArray();
        var next=new StarProgression(Stars.Configuration,new(ranks));var totals=next.Totals();
        ValidateStarTotals(totals);ValidateFormationCapacity(next);ValidateTalentCapacity(next);
        long cost=Stars.Configuration.Nodes[id].Costs[Stars.Level(id)];
        long before=Gold;Gold-=cost;AuditCurrency("player_command","buy_star","gold",before,Gold,origin:id);Stars=next;ApplyStarTotals(totals);Note("星图升级："+Stars.Configuration.Nodes[id].Name);return true;
    }
    public void ValidateStarConfiguration(StarConfiguration definition)
    {var next=new StarProgression(definition,Stars.Capture());ValidateStarTotals(next.Totals());ValidateFormationCapacity(next);ValidateTalentCapacity(next);}
    /// <summary>编辑器刷新定义只重算星图自身贡献的差量，保留等级成长、当前生命与其他属性。</summary>
    public void ReloadStars(StarConfiguration definition)
    {
        var next=new StarProgression(definition,Stars.Capture());var totals=next.Totals();ValidateStarTotals(totals);ValidateFormationCapacity(next);ValidateTalentCapacity(next);
        Config.Stars=definition;Stars=next;ApplyStarTotals(totals);Note("星图配置已刷新");
    }
    private void ValidateStarTotals(StarTotals totals)
    {
        foreach(var h in Heroes.Concat(reserveHeroes))
            if(!double.IsFinite(h.Attack+totals.Attack-starAttack)||h.Attack+totals.Attack-starAttack<=0||h.Attack+totals.Attack-starAttack>float.MaxValue||!double.IsFinite(h.MaxHp+totals.Hp-starHp)||h.MaxHp+totals.Hp-starHp<=0||h.MaxHp+totals.Hp-starHp>float.MaxValue)throw new InvalidOperationException("星图效果导致角色属性无效");
    }
    private void ApplyStarTotals(StarTotals totals)
    {
        foreach(var h in Heroes.Concat(reserveHeroes))
        {
            h.Attack=(float)(h.Attack+totals.Attack-starAttack);h.MaxHp=(float)(h.MaxHp+totals.Hp-starHp);h.Hp=Math.Min(h.Hp,h.MaxHp);
            // 评级类节点走预留Bonus字段（唯一来源），不修改等级推导的自然评级。
            h.AttackRatingBonus=StarValue("attack_rating");h.DefenseRatingBonus=StarValue("defense_rating");
        }
        for(int i=0;i<roomEntry.Count;i++)roomEntry[i]=roomEntry[i] with {Hp=Math.Min(roomEntry[i].Hp,Heroes[i].MaxHp)};
        starAttack=totals.Attack;starHp=totals.Hp;
    }
    /// <summary>只读派生值查询（QA/UI 用）：与战斗/经济消费点同一来源，不产生第二套计算。</summary>
    public double StarDerived(string attribute)=>StarValue(attribute);
    /// <summary>经济/消耗品/离线类星图乘率：全部是"1+和"，读取点唯一，不写回Actor永久属性（StarValue 已在Talents.cs按属性求和）。</summary>
    public double StarKillGoldPct=>StarValue("kill_gold_pct");
    public double StarSellGoldPct=>StarValue("sell_gold_pct");
    public double StarScrollDamagePct=>StarValue("scroll_damage_pct");
    public double StarPotionEffectPct=>StarValue("potion_effect_pct");
    public double StarPotionDurationPct=>StarValue("potion_duration_pct");
    public double StarTravelPct=>StarValue("travel_pct");
    public double StarOfflinePct=>StarValue("offline_pct");
}
