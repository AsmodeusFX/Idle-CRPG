using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;

public partial class Main
{
    private void RunAct1UiSmoke()
    {
        var original=session;var originalConfig=config;int originalView=viewMode;
        void Check(bool ok,string why){if(!ok)throw new Exception("ACT1_UI_SMOKE: "+why);}
        try
        {
            config=Config.Load(ProjectSettings.GlobalizePath("res://config"));session=GameSession.CreateWorld(config,962,new[]{"warrior","ranger","mage"});
            session.World!.Progress.Cleared.UnionWith(new[]{"A01-C01","A02-C01"});Refresh();SelectStarView(0);
            Check(branchCards.Count==1&&branchCards.Values.Single().Text.Contains("挑战支线-埋骨之地"),"支线入口或等级信息");
            branchCards.Values.Single().EmitSignal(Button.SignalName.Pressed);
            Check(session.BranchQueue.Count==1&&branchCards.Values.Single().Disabled,"支线按钮未入计划或未防重复");
            SelectStarView(1);Check(config.WorldWidth==8&&castleRows.Count==8&&selectedRegion==36,"正式8x8地图或中央营地");
            int index=config.AreaParts.FindIndex(a=>a.Id=="ap4");config.AreaParts[index]=config.AreaParts[index] with {UnlockMode="all"};config.RegionPrerequisites.Add(new(4,"A01-C01"));
            SelectMapTile(4);Check(selectedTileInfo.Text.Contains("全部前置")&&gateConditions!.Text.Contains("鲜血荒地岗哨")&&gateConditions.Text.Contains("冰冷之原哨站"),"AND详情未显示全部条件");
            session.World.Progress.Cleared.Remove("A01-C01");RefreshWorldPage();Check(!session.World.Progress.AreaPartUnlocked(config,"ap4"),"AND未满足仍开放");
            config=Config.Load(ProjectSettings.GlobalizePath("res://config"));session=GameSession.CreateWorld(config,963,new[]{"warrior","ranger","mage"});
            session.World!.Progress.Cleared.UnionWith(config.Dungeons.Where(d=>!d.Castle&&config.Rulers[d.RegionId]=="A01-C01").Select(d=>d.Id));
            Check(session.RequestCastleChallenge("A01-C01"),"测试要塞请求");
            for(int fail=0;fail<2;fail++)
            {
                for(int i=0;i<20000&&session.World.Stage!=CampaignStage.Dungeon;i++)session.Tick(.05f);
                Check(session.World.Stage==CampaignStage.Dungeon,"测试要塞未进入");foreach(var hero in session.Heroes)hero.Hp=0;session.Tick(.05f);
                for(int i=0;i<3000&&session.World.Stage==CampaignStage.Village;i++)session.Tick(.05f);
            }
            SelectStarView(0);Refresh();
            var card=castleCards["A01-C01"];var retry=card.GetNode<Label>("Content/Words/Retry");
            Check(retry.Visible&&retry.Text=="（再次挑战）"&&retry.GetThemeColor("font_color")==new Color("ff6262")&&!card.Disabled,"红色再次挑战按钮");
            card.EmitSignal(Button.SignalName.Pressed);Check(session.World.Target.Dungeon.Id=="A01-C01"&&!session.World.IsCastleRetry("A01-C01"),"再次挑战按钮未重新授权");
            GD.Print("ACT1_UI_SMOKE_OK: central town, manual branch card, 8 castles, explicit AND condition list and red manual retry button");
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
        finally{FinishProgressWrite();session=original;config=originalConfig;viewMode=originalView;OpenCurrentChapter();Refresh();TickStarPanels();SaveNow(true);}
    }
}
