using System;
using System.Linq;
namespace IdleCrpg.Core;
public sealed partial class GameSession
{
    /// <summary>普通四席可选择1–4人，同职可重复；新职业仍遵守初始解锁条件。</summary>
    public static GameSession CreateNewWorld(Config config,int seed,System.Collections.Generic.IEnumerable<string> heroIds)
    {
        var ids=heroIds.ToArray();
        if(ids.Length is <1 or >4||ids.Any(id=>!config.HeroUnlockKills.TryGetValue(id,out int kills)||kills!=0))throw new ArgumentException("请选择1–4名初始解锁英雄，允许同职业重复");
        return CreateWorld(config,seed,ids);
    }
    /// <summary>玩家创角只允许一名初始解锁职业；多角色构造保留给旧档迁移与回归。</summary>
    public static GameSession CreateNewWorld(Config config,int seed,string heroId)
    {
        if(!config.HeroUnlockKills.TryGetValue(heroId,out var kills)||kills!=0)throw new ArgumentException("初始创角职业尚未解锁");
        return CreateWorld(config,seed,new[]{heroId});
    }
}
