using System;
using System.Linq;
namespace IdleCrpg.Core;
public sealed partial class GameSession
{
    /// <summary>玩家创角只允许一名初始解锁职业；多角色构造保留给旧档迁移与回归。</summary>
    public static GameSession CreateNewWorld(Config config,int seed,string heroId)
    {
        if(!config.HeroUnlockKills.TryGetValue(heroId,out var kills)||kills!=0)throw new ArgumentException("初始创角职业尚未解锁");
        return CreateWorld(config,seed,new[]{heroId});
    }
}
