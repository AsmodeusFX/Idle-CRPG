using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>账号永久状态预留；本批不发AP、不解锁第五席或永久商品。</summary>
public sealed class AccountState
{
    public long AchievementPoints,RunOrdinal;
    public List<string> AchievementFacts=new(),PermanentUnlocks=new();
    public AccountState Copy()=>new(){AchievementPoints=AchievementPoints,RunOrdinal=RunOrdinal,AchievementFacts=new(AchievementFacts),PermanentUnlocks=new(PermanentUnlocks)};
    public void Validate()
    {
        if(AchievementPoints<0||RunOrdinal<0||AchievementFacts==null||PermanentUnlocks==null||AchievementFacts.Any(string.IsNullOrWhiteSpace)||PermanentUnlocks.Any(string.IsNullOrWhiteSpace)||AchievementFacts.Distinct().Count()!=AchievementFacts.Count||PermanentUnlocks.Distinct().Count()!=PermanentUnlocks.Count)throw new InvalidDataException("账号状态无效");
    }
}

/// <summary>需要用户介入的旧档迁移；调用方不得将此异常降级为重置新档。</summary>
public class MigrationRequiredException(string message):IOException(message);

public sealed partial class GameSession
{
    public AccountState Account {get;private set;}=new();
    /// <summary>本轮唯一标识；英雄ID由轮次和槽位组成，存档恢复不会重新分配。</summary>
    public string RunId {get;private set;}="";
    public bool TeamLocked {get;private set;}=true;
    /// <summary>放弃本轮仅保留账号状态，丢弃本轮成长；不发正式重生奖励。</summary>
    public AccountState AbandonRun()=>Account.Copy();
    /// <summary>本批只提供正式重生的禁止入口，资格和奖励在WP7实施。</summary>
    public bool TryRebirth(out string reason){reason="正式重生尚未开放（WP7），请勿以放弃本轮代替重生";return false;}
    public static GameSession StartRun(Config config,int seed,IEnumerable<string> heroes,AccountState account)
    {
        account.Validate();var game=CreateNewWorld(config,seed,heroes);game.Account=account.Copy();game.Account.RunOrdinal=checked(account.RunOrdinal+1);
        game.RunId=$"run-{seed}:{game.Account.RunOrdinal}";
        for(int i=0;i<game.Heroes.Count;i++)game.Heroes[i].HeroInstanceId=$"{game.RunId}:hero:{i+1}";
        return game;
    }
    private static void MigrateRunIdentity(SessionSnapshot s)
    {
        if(s.Heroes==null||s.ReserveHeroes==null||s.Summons==null||s.RoomEntrySummons==null||s.Account==null)throw new InvalidDataException("缺少轮次状态");
        if(s.RunVersion is not (0 or 1))throw new InvalidDataException("不支持的轮次版本");
        if(s.RunVersion==0)
        {
            if(s.Heroes.Count>4||s.ReserveHeroes.Count>0)throw new MigrationRequiredException("旧档含五人队伍或候补，需确认迁移；已保留原件和备份，不自动删人或清档");
            s.RunId=$"legacy-{s.Seed}-{s.DungeonSeed}";
            for(int i=0;i<s.Heroes.Count;i++)s.Heroes[i].HeroInstanceId=$"{s.RunId}:hero:{i+1}";
            s.PendingFormation=Array.Empty<string>();s.TeamLocked=true;s.FormationCapacityFloor=4;s.RunVersion=1;
            // 旧路径以职业作键；只丢弃可重算导航，不丢进度、坐标或奖励。
            foreach(var world in new[]{s.World,s.SuspendedWorld})world?.VillagePaths.Clear();
        }
        s.Account.Validate();
        if(string.IsNullOrWhiteSpace(s.RunId)||!s.TeamLocked||s.Heroes.Count is <1 or >4||s.ReserveHeroes.Count!=0||s.PendingFormation.Length!=0||s.Heroes.Any(h=>string.IsNullOrWhiteSpace(h.HeroInstanceId))||s.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()!=s.Heroes.Count)throw new InvalidDataException("轮次身份、锁队或英雄实例ID无效");
        foreach(var summon in s.Summons.Concat(s.RoomEntrySummons))
        {
            if(summon.SummonOwner<0||summon.SummonOwner>=s.Heroes.Count)throw new InvalidDataException("召唤属主索引无效");
            string owner=s.Heroes[summon.SummonOwner].HeroInstanceId;
            if(summon.SummonOwnerInstanceId=="")summon.SummonOwnerInstanceId=owner;
            if(summon.SummonOwnerInstanceId!=owner)throw new InvalidDataException("召唤属主实例ID不一致");
        }
    }
    /// <summary>永久实例ID定位；旧调用仅在该职业恰有一个实例时兼容，重复职业不会误选第一人。</summary>
    private Actor? FindHeroInstance(string id)
    {
        var exact=Heroes.SingleOrDefault(h=>h.HeroInstanceId==id);if(exact!=null)return exact;
        var old=Heroes.Where(h=>h.SpecId==id).ToArray();return old.Length==1?old[0]:null;
    }
}
