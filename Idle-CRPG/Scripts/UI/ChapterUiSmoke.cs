using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;

public partial class Main
{
    /// <summary>真实控件验证章节门禁、自动换图后的列表重建以及旧章浏览不改变远征。</summary>
    private void RunChapterUiSmoke()
    {
        var original=session;var originalConfig=config;int originalView=viewMode;
        void Check(bool ok,string why){if(!ok)throw new Exception("CHAPTER_UI_SMOKE: "+why);}
        try
        {
            session=GameSession.CreateWorld(config,876,new[]{"warrior","ranger","mage"});config=session.Config;
            SelectStarView(0);SelectStarView(1);
            Check(difficultyPicker.IsItemDisabled(1)&&chapterTabs[1].Disabled&&chapterTabs[1].TooltipText!="","未解锁入口/提示");
            BrowseChapter(1,0);Check(mapDifficulty==0&&mapChapter==0,"锁定难度仍可进入");
            for(int i=0;i<5;i++)
            {
                var s=session.CaptureProgress();var w=s.World!;var village=config.Villages.Single(v=>v.RegionId==config.StartRegionId);var region=config.Regions.Single(r=>r.Id==village.RegionId);var npc=config.Npcs.Single(n=>n.Id==village.NpcId);
                w.Stage=CampaignStage.Village;w.PortalPhase=PortalPhase.None;w.PortalElapsed=0;w.Position=new System.Numerics.Vector2(region.X+village.X,region.Y+village.Y);w.VillageId=village.Id;w.Route.Clear();w.VillagePaths.Clear();w.TownPhase=TownVisitPhase.Exit;w.TradeElapsed=0;
                w.Explored=config.Regions.Select(r=>r.Id).ToList();w.Cleared=config.Dungeons.Select(d=>d.Id).ToList();w.PendingCastles.Clear();s.Floors.Clear();s.ActiveDungeonId="";
                foreach(var hero in s.Heroes)hero.Position=config.Town!=null?config.TownWaypoint:npc.Position;
                session=GameSession.Restore(config,s);session.Tick(.05f);SyncCampaignConfig();RefreshWorldPage();
                Check(ViewingCurrentMap&&MapConfig==session.Config&&castleRows.Count==config.Dungeons.Count(d=>d.Castle),"自动换章后的地图/清单");
                var last=config.Regions.Last();SelectMapTile(last.Id);
                Check(detailRegion==last.Id,"新尺寸地图格子详情");
                foreach(var blocked in config.Regions.Where(r=>!r.Enterable))
                {
                    SelectMapTile(blocked.Id);
                    Check(selectedTileInfo.Text.Contains("不可进入")&&selectedDungeonLabels.Count==0,"阻断格详情或空清单");
                }
            }
            Check(session.DifficultyIndex==1&&!difficultyPicker.IsItemDisabled(1)&&difficultyPicker.IsItemDisabled(2),"难度解锁控件");
            var target=session.World!.Target;BrowseChapter(0,4);
            Check(!ViewingCurrentMap&&MapConfig.WorldWidth==12&&MapWorld!.Progress.Cleared.Count==MapConfig.Dungeons.Count&&session.World.Target==target,"旧章查看改变远征或完成记录");
            SelectMapTile(MapConfig.Regions.Last().Id);SaveNow(true);
            Check(saveStore.Load(config)?.Session?.DifficultyIndex==1,"浏览异尺寸旧图导致存档不可恢复");
            BrowseChapter(1,1);Check(mapDifficulty==0&&mapChapter==4,"锁定章节仍可切换");
            SelectStarView(0);SelectStarView(1);Check(ViewingCurrentMap,"重新打开未回当前难度章节");
            GD.Print("CHAPTER_UI_SMOKE_OK: locked controls/tooltips, automatic map changes, chapter lists, archived map browsing and safe saves");
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
        finally{session=original;config=originalConfig;viewMode=originalView;OpenCurrentChapter();Refresh();TickStarPanels();}
    }
}
