using System;
using System.Linq;
using Godot;
using IdleCrpg.Core;
public partial class Main
{
    private void RunHeroSmoke()
    {
        SelectStarView(4);TickHeroPage();
        if(!heroPage.Visible||gameHud.Visible||sceneCanvas.Visible||heroSlots.GetChildCount()!=5)throw new Exception("英雄全屏页/五槽位错误");
        OpenHeroPicker(4);if(!heroNotice.DialogText.Contains("尚未解锁"))throw new Exception("锁定席位未提示");
        heroNotice.Hide();SelectStarView(0);
        var original=session;var savedConfig=config;
        try
        {
            session=GameSession.CreateNewWorld(config,20261017,"warrior");Refresh();
            if(heroNames[1].Text!="席位未解锁"||heroAdd[1].Visible)throw new Exception("未解锁席位误显示空缺加号");
            session.GrantGmResources(1000,0);session.BuyStar("s_root",out _);session.BuyStar("s_party_1",out _);Refresh();
            if(heroNames[1].Text!="席位空缺"||!heroAdd[1].Visible||heroAdd[2].Visible)throw new Exception("已解锁空缺席位状态错误");
            heroAdd[1].EmitSignal(Button.SignalName.Pressed);
            if(!formationRoot.Visible||formationSeats.GetChildCount()!=5||formationCandidates.GetChildCount()!=config.Heroes.Count)throw new Exception("底部加号未弹出完整编队UI");
            if(!formationCandidates.GetNode<Button>("FormationHero_priest").Disabled)throw new Exception("未解锁英雄可编队");
            formationCandidates.GetNode<Button>("FormationHero_mage").EmitSignal(Button.SignalName.Pressed);
            formationConfirm.EmitSignal(Button.SignalName.Pressed);heroNotice.Hide();
            if(formationRoot.Visible||session.Heroes.Count!=1||!session.PendingFormation.SequenceEqual(new[]{"warrior","mage"})||!formationPlanCard.Visible)throw new Exception("编队确认未加入计划或立即改变阵容");
            formationPlanCard.EmitSignal(Button.SignalName.Pressed);
            if(!formationRoot.Visible||formationDraft[1]!="mage")throw new Exception("计划卡未恢复待执行草稿");
            formationRoot.Hide();session.CancelFormation();Refresh();if(formationPlanCard.Visible)throw new Exception("撤销计划后卡片未移除");SelectStarView(4);TickHeroPage();heroSlots.GetChild<Button>(1).EmitSignal(Button.SignalName.Pressed);if(!formationRoot.Visible)throw new Exception("英雄页空槽未弹出编队UI");formationRoot.Hide();SelectStarView(0);
        }
        finally{heroNotice.Hide();session=original;config=savedConfig;formationRoot.Hide();heroStamp="";Refresh();}
        GD.Print("HERO_UI_SMOKE_OK: full-page heroes, modal formation, locked/empty HUD slots, hero locks, confirm-to-plan, plan editing and cancel");
    }
}
