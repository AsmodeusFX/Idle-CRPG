using System;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>玩家一次授权整条支线，难度和章节是稳定进度键的一部分。</summary>
public record BranchRequest(int Difficulty,int Chapter,string Castle);
public sealed partial class GameSession
{
    public bool RequestBranchChallenge(int difficulty,int chapter,string castle)
    {
        if(!MapUnlocked(difficulty,chapter))return false;
        var map=Config.ChapterMaps[chapter];var progress=MapProgress(difficulty,chapter);var request=new BranchRequest(difficulty,chapter,castle);
        if(!map.Dungeons.Any(d=>d.Id==castle&&d.Castle&&map.Optional(d.Id))||!progress.AreaPartUnlocked(map,map.AreaParts.Single(a=>a.CastleId==castle).Id)||progress.Cleared.Contains(castle)||ActiveBranch==request||branchQueue.Contains(request))return false;
        branchQueue.Add(request);Note("已安排支线："+map.Dungeons.Single(d=>d.Id==castle).Name);return true;
    }
    /// <summary>交易后才跨图；支线传送是玩家授权的跨章节例外，章内仍遵循关口与步行。</summary>
    internal bool AfterVillageTrade()
    {
        if(ActiveBranch!=null)
        {
            if(!World!.Progress.Cleared.Contains(ActiveBranch.Castle))return false;
            var progress=World.Progress;int key=DifficultyIndex*5+ChapterIndex;
            if(key<LatestMapIndex)completedMaps[key]=progress;
            DifficultyIndex=LatestMapIndex/5;Config=Config.ChapterMaps[LatestMapIndex%5];World=suspendedWorld??throw new InvalidOperationException("支线缺少返回进程");
            if(key==LatestMapIndex){World.Progress.Cleared.UnionWith(progress.Cleared);World.Progress.Explored.UnionWith(progress.Explored);}
            ActiveBranch=null;suspendedWorld=null;ClearMapScene();Note("支线完成，返回最新章节继续远征");return true;
        }
        bool advanced=AdvanceChapter();
        while(branchQueue.Count>0)
        {
            var request=branchQueue[0];branchQueue.RemoveAt(0);var progress=MapProgress(request.Difficulty,request.Chapter);
            if(progress.Cleared.Contains(request.Castle))continue;
            World!.PrepareResume();suspendedWorld=World;
            Config=Config.ChapterMaps[request.Chapter];DifficultyIndex=request.Difficulty;ActiveBranch=request;
            World=new WorldCampaign(Config,DifficultyIndex,progress,request.Castle);ClearMapScene();Note("传送至支线章节村庄，开始已授权支线计划");return true;
        }
        return advanced;
    }
    private void ClearMapScene()
    {
        ResetSkillRoom();Floors.Clear();FloorIndex=CurrentRoom=0;ActiveDungeon=null;Won=false;destination=-1;phase=0;regroup=0;ResetRescue();roomEntry.Clear();
        for(int i=0;i<Heroes.Count;i++)Heroes[i].Position=new(0,i*.2f);
    }
    private void RestoreBranchState(SessionSnapshot s)
    {
        if(s.BranchQueue==null||s.BranchQueue.Count>15||s.BranchQueue.Distinct().Count()!=s.BranchQueue.Count)throw new InvalidDataException("支线计划非法");
        bool Valid(BranchRequest b)=>MapUnlocked(b.Difficulty,b.Chapter)&&Config.ChapterMaps[b.Chapter].Dungeons.Any(d=>d.Id==b.Castle&&d.Castle&&Config.ChapterMaps[b.Chapter].Optional(d.Id));
        if(s.BranchQueue.Any(b=>!Valid(b)))throw new InvalidDataException("支线计划引用非法");branchQueue.AddRange(s.BranchQueue);
        ActiveBranch=s.ActiveBranch;
        if(ActiveBranch==null)
        {
            if(s.SuspendedWorld!=null||DifficultyIndex*5+ChapterIndex!=LatestMapIndex)throw new InvalidDataException("远征章节与最新进度不一致");return;
        }
        if(!Valid(ActiveBranch)||ActiveBranch.Difficulty!=DifficultyIndex||ActiveBranch.Chapter!=ChapterIndex||s.SuspendedWorld==null||World==null||World.BranchCastle!=ActiveBranch.Castle&&!World.IsCastleRetry(ActiveBranch.Castle))throw new InvalidDataException("当前支线现场非法");
        var resume=Config.ChapterMaps[LatestMapIndex%5];WorldCampaign.Validate(resume,s.SuspendedWorld);
        if(s.SuspendedWorld.Stage!=CampaignStage.TravelDungeon)throw new InvalidDataException("返回远征必须从已完成交易的行进点恢复");
        suspendedWorld=WorldCampaign.Restore(resume,s.SuspendedWorld,LatestMapIndex/5);
        int key=DifficultyIndex*5+ChapterIndex;if(key<LatestMapIndex)completedMaps[key]=World!.Progress;
    }
}
