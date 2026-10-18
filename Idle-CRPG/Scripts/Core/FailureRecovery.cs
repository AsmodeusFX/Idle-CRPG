using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class Config
{
    public int FailureThreshold {get;private set;}=2;
    private void LoadRecovery(string directory)
    {
        string p=Path.Combine(directory,"world/recovery.csv");var t=CsvTable.Load(p,File.ReadAllText(p),"id","value");FailureThreshold=t.Require("failure_threshold").Integer("value");
        if(FailureThreshold<1||FailureThreshold>100)throw new InvalidDataException("失败回刷阈值必须为1～100");
    }
}
public sealed partial class GameSession
{
    /// <summary>首版挑战等级取最低成员等级；后续替换为战力模型，不作为进入硬门槛。</summary>
    public int TeamChallengeLevel=>Heroes.Min(h=>h.Level);
}
public sealed partial class WorldCampaign
{
    private readonly Dictionary<string,int> failures=new(),pausedAtLevel=new();
    private readonly HashSet<string> retryCastles=new();
    private readonly PersistentRandom recoveryRandom=new(731913u);
    private int teamLevel=1;
    public bool IsCastleRetry(string id)=>retryCastles.Contains(id);
    /// <summary>仅高于小队挑战等级的失败计入回刷阈值；要塞被暂停后不得自动重新发起。</summary>
    internal void RecordFailure(GameSession game)
    {
        teamLevel=game.TeamChallengeLevel;string id=Target.Dungeon.Id;
        if(config.Challenge(id,difficulty).Challenge<=teamLevel)return;
        failures[id]=Math.Min(config.FailureThreshold,failures.GetValueOrDefault(id)+1);
        if(failures[id]<config.FailureThreshold){if(Target.Dungeon.Castle&&!pendingCastles.Contains(id))pendingCastles.Insert(0,id);return;}
        pausedAtLevel[id]=teamLevel;
        if(Target.Dungeon.Castle){retryCastles.Add(id);pendingCastles.Remove(id);if(BranchCastle==id)BranchCastle="";}
        game.Note($"{Target.Dungeon.Name} 连续失败达到{config.FailureThreshold}次，交易后转入匹配等级复刷");
    }
    private bool TryRecovery(out ExpeditionTarget target)
    {
        target=null!;
        foreach(var entry in pausedAtLevel.OrderBy(e=>e.Key).ToArray())
        {
            var dungeon=config.Dungeons.Single(d=>d.Id==entry.Key);
            if(!dungeon.Castle&&teamLevel!=entry.Value)
            {pausedAtLevel.Remove(entry.Key);failures.Remove(entry.Key);target=new(dungeon,false);return true;}
        }
        if(pausedAtLevel.Count==0)return false;
        var allowed=Progress.AccessibleRegions(config);
        bool Reachable(DungeonSpec d)
        {
            var tile=config.Regions.Single(r=>r.Id==d.RegionId);
            try{WorldNavigation.FindPath(config,Position,new(tile.X+d.X,tile.Y+d.Y),allowedRegions:allowed);return true;}catch(InvalidOperationException){return false;}
        }
        var available=config.Dungeons.Where(d=>!d.Castle&&Progress.Cleared.Contains(d.Id)&&allowed.Contains(d.RegionId)&&Reachable(d)).ToArray();
        if(available.Length==0)return false; // 尚无首通目标时沿用正常开拓，不虚构可刷副本。
        var eligible=available.Where(d=>config.Challenge(d.Id,difficulty).Challenge<=teamLevel).ToArray();
        int level=eligible.Length>0?eligible.Max(d=>config.Challenge(d.Id,difficulty).Challenge):available.Min(d=>config.Challenge(d.Id,difficulty).Challenge);
        var same=available.Where(d=>config.Challenge(d.Id,difficulty).Challenge==level).OrderBy(d=>d.Id).ToArray();
        target=new(same[recoveryRandom.Next(0,same.Length)],true);return true;
    }
    private void ClearFailure(string id){failures.Remove(id);pausedAtLevel.Remove(id);retryCastles.Remove(id);}
}
