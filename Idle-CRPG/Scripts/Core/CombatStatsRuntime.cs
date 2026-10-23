using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>本房战斗观测计数：只读、有界、仅内存；不参与任何判定，不进存档，不写磁盘。换房/清房时重置。</summary>
public sealed class RoomCombatStats
{
    public double Elapsed;
    /// <summary>本房第一次友方普攻时，攻击者距入口门线的深度（负值表示仍在门外）。</summary>
    public float FirstAttackDepth;
    public string FirstAttackActor="";
    /// <summary>入口通行被提前结束的原因计数。</summary>
    public Dictionary<string,int> EarlyEngage=new(StringComparer.Ordinal);
    /// <summary>同目标近战攻击者之间的最小间距、全场友方最近邻、明显重叠（&lt;0.35U）对数。</summary>
    public float SameTargetMinDistance=float.NaN;
    public float NearestNeighbourMin=float.NaN;
    public int OverlapPairs;
    public int MeleeSamples;
    /// <summary>远程角色在入口浅区（通行边界之前）施法的次数。</summary>
    public int RangedShallowCasts;
    /// <summary>AOE 单次命中人数分布：下标 1..5，5 表示 ≥5。</summary>
    public int[] AoeHits=new int[6];
    public int AoeCasts;
    public int HealCasts;
    public double HealRatioSum;
    public int RefillCasts;
    public double RefillDelaySum;
    /// <summary>普通容量占用峰值、释放次数与累计占用时长。</summary>
    public int CapacityPeak;
    public int CapacityReleases;
    public double CapacityOccupiedSeconds;
    /// <summary>0 容量后排被普通近战锁定的累计时长。</summary>
    public double BacklineMeleeSeconds;
}

public sealed partial class GameSession
{
    private RoomCombatStats combatStats=new();
    private int lastNormalLockCount;
    private double lastSummonDeathAt=-1;
    private int lastSummonAlive;

    /// <summary>本房观测数据只读拷贝，供GM窗口与检查使用；没有计量平台、没有磁盘写入。</summary>
    public RoomCombatStats CombatStats=>combatStats;

    private void RecordFirstAttack(Actor actor)
    {
        if(combatStats.FirstAttackActor!="")return;
        combatStats.FirstAttackActor=actor.Name;
        combatStats.FirstAttackDepth=skillState.Targeting.EntryPrepared?EntryDepth(actor.Position):0;
    }

    private void RecordEarlyEngage(string reason)
    {
        combatStats.EarlyEngage[reason]=combatStats.EarlyEngage.GetValueOrDefault(reason)+1;
    }

    /// <summary>施法时记录远程浅区施放与召唤补员延迟；不改变动作或冷却。</summary>
    private void RecordSkillCast(Actor actor,string skillId)
    {
        if(!IsFriendlyActor(actor))return;
        if(actor.Ranged&&skillState.Targeting.EntryPrepared&&EntryDepth(actor.Position)<EntryPassDepth)
            combatStats.RangedShallowCasts++;
        if(SoleSummonOf(skillId)!=null&&lastSummonDeathAt>=0)
        {
            combatStats.RefillCasts++;
            combatStats.RefillDelaySum+=Math.Max(0,combatStats.Elapsed-lastSummonDeathAt);
            lastSummonDeathAt=-1;
        }
    }

    private void RecordAoe(int hits)
    {
        if(hits<=0)return;
        combatStats.AoeCasts++;
        combatStats.AoeHits[Math.Min(hits,5)]++;
    }

    private void RecordHeal(Actor? target,float maximum)
    {
        if(target is not {Alive:true}||maximum<=0)return;
        combatStats.HealCasts++;
        combatStats.HealRatioSum+=target.Hp/maximum;
    }

    /// <summary>每逻辑步末尾采样一次：只读位置、锁与召唤数量，不写回任何状态。</summary>
    private void SampleCombatStats(Room room,float dt)
    {
        combatStats.Elapsed+=dt;
        var state=skillState.Targeting;
        var friends=Heroes.Concat(Summons).Where(a=>a.Alive&&InCombatRoom(a)).ToArray();
        if(friends.Length>1)
        {
            float nearest=float.MaxValue;
            for(int i=0;i<friends.Length;i++)
                for(int j=i+1;j<friends.Length;j++)
                    nearest=Math.Min(nearest,Vector2.Distance(friends[i].Position,friends[j].Position));
            if(float.IsFinite(nearest))
                combatStats.NearestNeighbourMin=float.IsNaN(combatStats.NearestNeighbourMin)?nearest:Math.Min(combatStats.NearestNeighbourMin,nearest);
        }

        // 同目标近战攻击者：最小间距、明显重叠对数。
        combatStats.MeleeSamples++;
        var groups=state.Locks
            .Where(l=>l.Kind==MonsterLockKind.Normal)
            .Select(l=>(Lock:l,Monster:CombatActor(l.Owner)))
            .Where(p=>p.Monster is {Alive:true}&&!p.Monster.Ranged)
            .GroupBy(p=>p.Lock.Target);
        foreach(var group in groups)
        {
            var positions=group.Select(g=>g.Monster!.Position).ToArray();
            for(int i=0;i<positions.Length;i++)
                for(int j=i+1;j<positions.Length;j++)
                {
                    float distance=Vector2.Distance(positions[i],positions[j]);
                    if(float.IsNaN(combatStats.SameTargetMinDistance)||distance<combatStats.SameTargetMinDistance)
                        combatStats.SameTargetMinDistance=distance;
                    if(distance<.35f)combatStats.OverlapPairs++;
                }
        }

        // 容量占用：峰值、释放次数、累计占用秒数。
        int normal=state.Locks.Count(l=>l.Kind==MonsterLockKind.Normal);
        if(normal>combatStats.CapacityPeak)combatStats.CapacityPeak=normal;
        if(normal<lastNormalLockCount)combatStats.CapacityReleases+=lastNormalLockCount-normal;
        if(normal>0)combatStats.CapacityOccupiedSeconds+=dt;
        lastNormalLockCount=normal;

        // 0 容量后排被普通近战锁定的时长。
        for(int i=0;i<Heroes.Count;i++)
        {
            var hero=Heroes[i];
            if(!hero.Alive||!InCombatRoom(hero)||EngagementCapacity(hero)>0)continue;
            bool meleeLocked=state.Locks.Any(l=>l.Kind==MonsterLockKind.Normal&&l.Target==i&&CombatActor(l.Owner) is {Alive:true} monster&&!monster.Ranged);
            if(meleeLocked)combatStats.BacklineMeleeSeconds+=dt;
        }

        // 召唤数量下降记一次死亡时刻，用于补召延迟。
        int summons=Summons.Count(s=>s.Alive);
        if(summons<lastSummonAlive)lastSummonDeathAt=combatStats.Elapsed;
        lastSummonAlive=summons;
    }

    /// <summary>GM 只读文本；数值随房间推进累积，换房重置。</summary>
    public IEnumerable<string> CombatStatsDiagnostics()
    {
        var s=combatStats;
        string early=s.EarlyEngage.Count==0?"无":string.Join("、",s.EarlyEngage.Select(p=>$"{p.Key}×{p.Value}"));
        string aoe=s.AoeCasts==0?"无":string.Join(" ",Enumerable.Range(1,5).Select(i=>$"{i}人:{(i==5?s.AoeHits[5]:s.AoeHits[i])}"));
        return new[]
        {
            $"战斗统计：时长{s.Elapsed:0.0}s",
            $"首次普攻：{(s.FirstAttackActor==""?"未发生":$"{s.FirstAttackActor} 深度{s.FirstAttackDepth:0.00}")}；提前接战：{early}",
            $"同目标近战间距：{(float.IsNaN(s.SameTargetMinDistance)?"无":s.SameTargetMinDistance.ToString("0.00"))}；友方最近邻：{(float.IsNaN(s.NearestNeighbourMin)?"无":s.NearestNeighbourMin.ToString("0.00"))}；重叠对数：{s.OverlapPairs}",
            $"远程浅区施法：{s.RangedShallowCasts}次；AOE命中分布：{aoe}",
            $"治疗：{(s.HealCasts==0?"无":$"{s.HealCasts}次，平均目标血量{(s.HealRatioSum/s.HealCasts*100):0}%")}；补召：{(s.RefillCasts==0?"无":$"{s.RefillCasts}次，平均延迟{(s.RefillDelaySum/s.RefillCasts):0.00}s")}",
            $"容量：峰值{s.CapacityPeak}，释放{s.CapacityReleases}次，累计占用{s.CapacityOccupiedSeconds:0.0}s；后排被近战锁定{s.BacklineMeleeSeconds:0.0}s"
        };
    }
}
