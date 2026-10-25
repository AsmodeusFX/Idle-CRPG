using System;
using System.Linq;
using IdleCrpg.Core;

/// <summary>旧空间/战斗回归的显式QA前置；不编译进Godot，不是玩家自动成长策略。</summary>
public static class TestPlayerActions
{
    public static void UpgradeWithClicks(GameSession game)
    {
        foreach(var hero in game.Heroes)while(game.UpgradeHero(hero.HeroInstanceId,out _)){}
    }
    public static bool RequestCastle(GameSession game,string id)
    {
        if(game.Config.Dungeons.Any(d=>d.Id==id&&d.Castle))game.QaMonsterRange(Math.Max(game.MonsterLevelMax,game.CastleRequiredMonsterLevel(id)));
        return game.RequestCastleChallenge(id);
    }
    public static bool RequestBranch(GameSession game,int difficulty,int chapter,string id)
    {
        var map=game.Config.ChapterMaps[chapter];
        if(map.Dungeons.Any(d=>d.Id==id&&d.Castle))game.QaMonsterRange(Math.Max(game.MonsterLevelMax,map.PoiProfiles.ContainsKey(id)?map.Challenge(id,difficulty).Challenge:map.Dungeons.Single(d=>d.Id==id).LevelMin));
        return game.RequestBranchChallenge(difficulty,chapter,id);
    }
}
namespace IdleCrpg.Core
{
    public sealed partial class GameSession
    {
        /// <summary>仅测试程序集的隔离前置，旧门禁/路径套件单独测试其原本负责的边界。</summary>
        internal void QaMonsterRange(int maximum){MonsterLevelMax=maximum;World?.SyncMonsterLevelRange(this);}
        /// <summary>仅测试程序集：构造弱队/极端 [Lmin,Lmax] 反例现场（如 [3,3]）。
        /// 只作用于内存会话；该现场无法写入正式存档（读档校验要求 MonsterLevelMin==1），因此不会泄漏成玩家入口。</summary>
        internal void QaRiskRange(int minimum,int maximum)
        {
            MonsterLevelMin=Math.Max(1,minimum);MonsterLevelMax=Math.Max(MonsterLevelMin,maximum);
            World?.SyncMonsterLevelRange(this);
        }
        internal void QaResolveHit(Actor target)=>SettleDamage(Heroes[0],target,"basic_player",100000,DamageType.Physical,false);
    }
}
