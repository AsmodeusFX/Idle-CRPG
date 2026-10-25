using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class CampaignPackageChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("CAMPAIGN_PACKAGE: "+why);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        Need(c.ChapterMaps.Select(m=>m.WorldWidth).SequenceEqual(new[]{8,9,10,7,11}),"五章尺寸");
        Need(c.ChapterMaps.Sum(m=>m.Dungeons.Count)==231&&c.ChapterMaps.Sum(m=>m.AreaParts.Count)==42&&c.ChapterMaps.Sum(m=>m.FloorPlans.Count)==1515,"231POI/42区域/1515难度楼层");
        Need(c.ChapterMaps[1].TownRegions.Count==2&&c.ChapterMaps[2].TownRegions.Count==2,"两格安全城镇");
        foreach(var map in c.ChapterMaps)
        {
            Need(map.Waypoints.Count==map.AreaParts.Count,"每区一个点");
            Need(!WorldNavigation.Walkable(map,new Vector2(-.01f,.5f)),"负数坐标截断穿越");
            if(map.ChapterIndex==3)continue;
            int split=map.ChapterIndex==1?1:4;int left=map.ChapterIndex==1?2:5;int right=map.ChapterIndex==1?3:6;int merge=map.ChapterIndex==1?4:7;
            string Castle(int i)=>map.AreaParts.Single(a=>a.Id=="ap"+i).CastleId;
            var p=new WorldProgress();p.Cleared.UnionWith(map.Dungeons.Where(d=>d.Castle&&!map.Optional(d.Id)&&d.Id!=Castle(left)&&d.Id!=Castle(right)&&d.Id!=Castle(merge)).Select(d=>d.Id));
            Need(p.AreaPartUnlocked(map,"ap"+left)&&p.AreaPartUnlocked(map,"ap"+right),"并行开放");
            Need(!p.AreaPartUnlocked(map,"ap"+merge),"AND零项");p.Cleared.Add(Castle(left));Need(!p.AreaPartUnlocked(map,"ap"+merge),"AND一项");p.Cleared.Add(Castle(right));Need(p.AreaPartUnlocked(map,"ap"+merge),"AND两项");
        }
        var fifth=c.ChapterMaps[4];int Tile(int row,int col)=>(row-1)*fifth.WorldWidth+col;
        Need(!fifth.CanCross(Tile(6,7),Tile(6,8)),"第五章新增格偷渡");
        var migration=GameSession.CreateWorld(c,21,new[]{"warrior","ranger","mage"}).CaptureProgress();migration.World!.MapVersion=2;migration.Gold=123;
        migration.World.Cleared.Add("A01-D01");var resumed=GameSession.Restore(c,migration);Need(resumed.Gold==123&&resumed.World!.Progress.Cleared.Contains("A01-D01"),"第一章补丁迁移丢进度");
        var queued=resumed.CaptureProgress();queued.World!.PendingCastles.Add("A07-C01");var held=GameSession.Restore(c,queued);
        for(int i=0;i<3000&&held.World!.Stage==CampaignStage.Village;i++)held.Tick(.05f);
        Need(held.World!.PendingCastles.Contains("A07-C01")&&!held.World.Progress.Cleared.Contains("A05-C01"),"新门禁静默删除批准队列或伪造前置");
        var waiting=GameSession.CreateWorld(c,22,new[]{"warrior","ranger","mage"});waiting.World!.Progress.Cleared.UnionWith(c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]=="A01-C01").Select(d=>d.Id));
        for(int i=0;i<3000&&waiting.World.Stage==CampaignStage.Village;i++)waiting.Tick(.05f);
        Need(waiting.World.WaitingApproval&&waiting.World.Target.Farming,"待批准未反馈继续复刷");waiting.Tick(.05f);
        var waited=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=waiting.CaptureProgress()})).Session!);
        Need(waited.World!.WaitApprovalTicks==waiting.World.WaitApprovalTicks&&waited.World.WaitApprovalTicks>0,"授权等待观测未保存");
        // 全15张图真实走动/进场，用GM跳过战斗；所有要塞仍由测试显式授权，不能从普通候选自动发起。
        var run=GameSession.CreateWorld(c,987,new[]{"warrior","ranger","mage"});run.SetGmBattle(new(InstantDungeonClear:true));
        for(int tick=0;tick<2000000;tick++)
        {
            if(tick%50==0)
            {
                var map=run.Config;foreach(var castle in map.Dungeons.Where(d=>d.Castle&&!map.Optional(d.Id)&&!run.World!.Progress.Cleared.Contains(d.Id)&&run.World.Progress.CastleUnlocked(map,d.Id)))TestPlayerActions.RequestCastle(run,castle.Id);
            }
            run.Tick(.05f);
            if(run.LatestMapIndex==14&&run.Config.MainCleared(run.World!.Progress))break;
        }
        Need(run.LatestMapIndex==14&&run.Config.MainCleared(run.World!.Progress),"15图自动流程未走完");
        var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=run.CaptureProgress()})).Session!);
        Need(restored.LatestMapIndex==14&&restored.Gold==0&&restored.Kills==0,"全程GM进度保存/无伪造奖励");
        Console.WriteLine("PASS: 5 chapter packages, 42 regions/231 POIs/505 floors, parallel AND gates, two-tile towns, no fifth-act shortcut, v2 migration and actual 15-map GM progression");
    }
}
