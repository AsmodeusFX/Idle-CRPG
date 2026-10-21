using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace IdleCrpg.Core;
public sealed partial class Config
{
    public Dictionary<string,int> HeroUnlockKills {get;private set;}=new();
    private void LoadHeroUnlocks(string directory)
    {
        HeroUnlockKills=Read(Path.Combine(directory,"hero/hero_unlocks.csv")).ToDictionary(r=>r["id"],r=>Integer(r,"kills"));
        if(!Heroes.Select(h=>h.Id).ToHashSet().SetEquals(HeroUnlockKills.Keys)||HeroUnlockKills.Values.Any(n=>n<0))throw new InvalidDataException("英雄解锁条件必须完整且非负");
    }
}
public sealed partial class GameSession
{
    /// <summary>旧档保留已有阵容，新档一席，手动购买星图扩编至五席。</summary>
    public int PartyCapacity=>FormationCapacity(Stars);
    /// <summary>累计击杀随关键进度保存，达到配置门槛即开放职业，无额外奖励结算。</summary>
    public bool HeroUnlocked(string id)=>Config.HeroUnlockKills.TryGetValue(id,out var kills)&&Kills>=kills;
    /// <summary>兼容旧增员命令，统一转为待执行编队计划，禁止绕过回城和NPC交互。</summary>
    public bool DeployHero(string id,out string reason)=>RequestFormation(Heroes.Select(h=>h.SpecId).Append(id),out reason);
}