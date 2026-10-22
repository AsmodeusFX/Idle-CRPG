using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace IdleCrpg.Core;

/// <summary>敌方召唤模板：上限为每来源存活/总生成上限；死亡保留策略不影响玩家侧统一撤销规则。</summary>
public record EnemySummonRule(string Id,string CasterTemplate,string Skill,string ChildTemplate,int AliveCap,int TotalCap,bool PersistAfterOwnerDeath=true);
public sealed partial class Config
{
    /// <summary>隔离内容显式注册；正式怪物池当前不投放，临时上限后续接正式数值。</summary>
    public Dictionary<string,EnemySummonRule> EnemySummons=new();
}
public sealed partial class GameSession
{
    /// <summary>进房前预留有限槽位，死亡槽位不复用，防止友方召唤ID和尸体事实漂移。</summary>
    internal static void PrepareEnemySummons(Config config,Room room)
    {
        if(room.Monsters.Any(m=>m.EnemySummonSource>=0))return;
        var sources=room.Monsters.Where(m=>m.EnemySummonSource<0).ToArray();
        foreach(var rule in config.EnemySummons.Values)
        {
            if(rule.AliveCap<1||rule.TotalCap<rule.AliveCap||rule.TotalCap>100||!config.Monsters.Any(m=>m.Id==rule.CasterTemplate)||!config.Monsters.Any(m=>m.Id==rule.ChildTemplate)||!config.Skills.Skills.ContainsKey(rule.Skill))throw new InvalidDataException("敌方召唤配置无效");
            foreach(var owner in sources.Where(m=>m.SpecId==rule.CasterTemplate))
            {
                if(room.Monsters.Count+rule.TotalCap>1000)throw new InvalidDataException("敌方召唤超出房间实体上限");
                owner.SkillIds=owner.SkillIds.Append(rule.Skill).Distinct().ToArray();int source=room.Monsters.IndexOf(owner);
                for(int i=0;i<rule.TotalCap;i++)
                {
                    var child=config.Spawn.Create(config.Monsters.Single(m=>m.Id==rule.ChildTemplate),owner.Level,false,new());config.ApplyCombatProfile(child);
                    child.Hp=0;child.Position=owner.Position;child.EnemySummonSource=source;child.EnemySummonRule=rule.Id;room.Monsters.Add(child);
                }
            }
        }
    }
    private void EnemySummonEffect(SkillEffectWork work,EnemySummonRule rule)
    {
        var room=Floor.Rooms[CurrentRoom];var owner=CombatActor(work.Owner);
        if(owner is not {Alive:true}||!room.Monsters.Contains(owner)||!owner.SkillIds.Contains(work.Skill))return;
        int source=room.Monsters.IndexOf(owner);
        var slots=room.Monsters.Where(m=>m.EnemySummonSource==source&&m.EnemySummonRule==rule.Id).ToArray();
        if(slots.Count(m=>m.Alive)>=rule.AliveCap)return;
        var child=slots.FirstOrDefault(m=>!m.EnemySummonActivated);if(child==null)return;
        child.EnemySummonActivated=true;child.Hp=child.MaxHp;
        child.Position=Vector2.Clamp(owner.Position+new Vector2(.4f,.3f),room.Center-room.HalfSize+new Vector2(.3f),room.Center+room.HalfSize-new Vector2(.3f));
        Floor.ActualSpawned++;effectiveEffectVersion++;
        PushVisual(CombatVisualKind.Summon,work.Skill,rule.Id,work.Owner,CombatId(child),owner.Position,child.Position,0,0);
    }
    /// <summary>配置撤销不是击杀，不发奖励或尸体；默认保留子单位继续参与原清房判定。</summary>
    private void PruneEnemySummons()
    {
        var room=Floor.Rooms[CurrentRoom];
        foreach(var child in room.Monsters.Where(m=>m.Alive&&m.EnemySummonSource>=0))
            if(!room.Monsters[child.EnemySummonSource].Alive&&!Config.EnemySummons[child.EnemySummonRule].PersistAfterOwnerDeath)
            {child.Hp=0;int id=CombatId(child);ReleaseCombatTargets(id);skillState.Buffs.RemoveAll(b=>b.Owner==id||b.Target==id);}
    }
}
