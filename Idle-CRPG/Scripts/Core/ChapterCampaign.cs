using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed class ChapterProgressSave
{
    public bool ReturnPortalVisible;
    public int Difficulty,Chapter;
    public List<int> Explored=new();
    public List<string> Cleared=new();
}
public sealed partial class GameSession
{
    public int DifficultyIndex { get; private set; }
    public int ChapterIndex=>Config.ChapterIndex;
    private readonly Dictionary<int,WorldProgress> completedMaps=new();
    public int LatestMapIndex {get;private set;}
    public BranchRequest? ActiveBranch {get;private set;}
    private WorldCampaign? suspendedWorld;
    private readonly List<BranchRequest> branchQueue=new();
    public IReadOnlyList<BranchRequest> BranchQueue=>branchQueue.AsReadOnly();
    /// <summary>章节门禁按难度优先、章节次序推进；地图页查看不改变远征目标。</summary>
    public bool MapUnlocked(int difficulty,int chapter)=>difficulty>=0&&difficulty<3&&chapter>=0&&chapter<5&&difficulty*5+chapter<=LatestMapIndex;
    public WorldProgress MapProgress(int difficulty,int chapter)
    {
        if(!MapUnlocked(difficulty,chapter))throw new InvalidOperationException("章节尚未解锁");
        if(difficulty==DifficultyIndex&&chapter==ChapterIndex)return World?.Progress??new WorldProgress();
        if(suspendedWorld!=null&&difficulty*5+chapter==LatestMapIndex)return suspendedWorld.Progress;
        return completedMaps[difficulty*5+chapter];
    }
    /// <summary>只在完成当前章全部地牢且村庄交易结束后切换；最终地狱第五章继续正常复刷。</summary>
    internal bool AdvanceChapter()
    {
        if(World==null||ActiveBranch!=null||!Config.MainCleared(World.Progress))return false;
        int current=LatestMapIndex;if(current==14)return false;
        completedMaps[current]=World.Progress;
        // 星图定义由全程共享；GM发布后的新定义不能在跨章时退回旧配置副本。
        foreach(var map in Config.ChapterMaps)map.Stars=Config.Stars;
        int next=current+1;LatestMapIndex=next;DifficultyIndex=next/5;Config=Config.ChapterMaps[next%5];
        ClearSummons();ResetSkillRoom();Floors.Clear();FloorIndex=CurrentRoom=0;ActiveDungeon=null;Won=false;destination=-1;phase=0;regroup=0;ResetRescue();roomEntry.Clear();
        World=new WorldCampaign(Config,DifficultyIndex);
        World.StartTownDeparture(this);
        Note($"已进入{Config.Difficulties[DifficultyIndex].Name} · {Config.Chapters[ChapterIndex].Name}");
        return true;
    }
    private List<ChapterProgressSave> CaptureChapters()=>completedMaps.OrderBy(p=>p.Key).Select(p=>new ChapterProgressSave{ReturnPortalVisible=p.Value.ReturnPortalVisible,Difficulty=p.Key/5,Chapter=p.Key%5,Explored=p.Value.Explored.OrderBy(id=>id).ToList(),Cleared=p.Value.Cleared.OrderBy(id=>id).ToList()}).ToList();
    private void RestoreChapters(SessionSnapshot s)
    {
        DifficultyIndex=s.DifficultyIndex;LatestMapIndex=s.CampaignVersion==0?s.DifficultyIndex*5+s.ChapterIndex:s.LatestMapIndex;
        if(LatestMapIndex<0||LatestMapIndex>14||s.DifficultyIndex*5+s.ChapterIndex>LatestMapIndex)throw new InvalidDataException("Latest chapter index is invalid");
        if(s.CompletedMaps==null||s.CompletedMaps.Count!=LatestMapIndex)throw new InvalidDataException("章节历史不连续");
        foreach(var entry in s.CompletedMaps)
        {
            int key=entry.Difficulty*5+entry.Chapter;
            if(entry.Difficulty<0||entry.Difficulty>2||entry.Chapter<0||entry.Chapter>4||key>=LatestMapIndex||completedMaps.ContainsKey(key))throw new InvalidDataException("章节历史重复或越界");
            var map=Config.ChapterMaps[entry.Chapter];
            if(entry.Cleared==null||entry.Explored==null||entry.Cleared.Distinct().Count()!=entry.Cleared.Count||entry.Explored.Distinct().Count()!=entry.Explored.Count||entry.Cleared.Any(id=>!map.Dungeons.Any(d=>d.Id==id))||entry.Explored.Any(id=>!map.Regions.Any(r=>r.Id==id)))throw new InvalidDataException("已完成章节引用或完成度非法");
            var progress=new WorldProgress {ReturnPortalVisible=entry.ReturnPortalVisible};progress.Explored.UnionWith(entry.Explored);progress.Cleared.UnionWith(entry.Cleared);if(!map.MainCleared(progress))throw new InvalidDataException("Historical chapter main objective is incomplete");completedMaps.Add(key,progress);
        }
    }
}
