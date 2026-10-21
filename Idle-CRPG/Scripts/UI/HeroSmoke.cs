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
        heroNotice.Hide();OpenHeroPicker(4);if(heroNotice.Visible||!heroSlots.GetChild<Button>(4).Disabled||!heroSlots.GetChild<Button>(4).TooltipText.Contains("尚未解锁"))throw new Exception("锁定席位应禁用并使用悬停提示，不弹窗");
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
            formationConfirm.EmitSignal(Button.SignalName.Pressed);
            if(formationRoot.Visible||heroNotice.Visible||session.Heroes.Count!=1||!session.PendingFormation.SequenceEqual(new[]{"warrior","mage"})||!formationPlanCard.Visible)throw new Exception("编队确认应加入计划并关闭编辑，不弹成功提示或立即改变阵容");
            formationPlanCard.EmitSignal(Button.SignalName.Pressed);
            if(!formationRoot.Visible||formationDraft[1]!="mage")throw new Exception("计划卡未恢复待执行草稿");
            formationRoot.Hide();session.CancelFormation();Refresh();if(formationPlanCard.Visible)throw new Exception("撤销计划后卡片未移除");SelectStarView(4);TickHeroPage();
            var emptySlot=heroSlots.GetChild<Button>(1);RefreshHeroes();
            if(!ReferenceEquals(emptySlot,heroSlots.GetChild<Button>(1)))throw new Exception("属性刷新重建了槽位，会丢失点击");
            emptySlot.EmitSignal(Button.SignalName.Pressed);if(!formationRoot.Visible||formationSelected!=1)throw new Exception("英雄页空槽未打开编队并定位席位");formationRoot.Hide();
            if(heroManageButton.Position.Y!=heroSlots.Position.Y||heroManageButton.Position.X<heroSlots.Position.X+heroSlots.Size.X)throw new Exception("调整编队未放在队伍槽位同一行或与槽位重叠");
            heroManageButton.EmitSignal(Button.SignalName.Pressed);if(!formationRoot.Visible)throw new Exception("顶部调整编队入口无效");formationRoot.Hide();SelectStarView(0);
        }
        finally{heroNotice.Hide();session=original;config=savedConfig;formationRoot.Hide();heroStamp="";Refresh();}
        GD.Print("HERO_UI_SMOKE_OK: full-page heroes, modal formation, locked/empty HUD slots, hero locks, confirm-to-plan, plan editing and cancel");
    }
}
