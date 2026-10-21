using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private void RunClassTalentSmoke()
    {
        var original=session;int mode=viewMode;
        try
        {
            foreach(var spec in config.Heroes)
            {
                session=GameSession.CreateWorld(config,20261017,new[]{spec.Id});selectedHero=spec.Id;talentStamp="";SelectStarView(4);RefreshTalentPage();
                if(session.TalentPoints(session.Heroes[0])!=0||session.Heroes[0].SkillIds.Single()!=config.Talents.StartingSkills[spec.Id]||talentTrees.GetChildCount()!=3)throw new Exception("职业初始点/技能/三系UI错误："+spec.Id);
                foreach(var column in talentTrees.GetChildren().OfType<VBoxContainer>())
                    if(!column.GetChild<ScrollContainer>(1).GetChild<Panel>(0).GetChildren().OfType<InteractionButton>().Any())throw new Exception("职业技能树仍是空白："+spec.Id);
            }
            GD.Print("CLASS_TALENT_UI_SMOKE_OK: all five class trees populated, named starting skills equipped and level-one point consumed");
        }
        finally{session=original;viewMode=mode;selectedHero="";talentStamp="";Refresh();TickStarPanels();}
    }
    /// <summary>真实学习/装配控件回归仅在隔离QA会话中运行，不自动修改玩家天赋。</summary>
    private void RunTalentSmoke()
    {
        var original=session;var oldConfig=config;int oldMode=viewMode;
        try
        {
            session=GameSession.CreateNewWorld(config,20261017,"warrior");session.GrantGmResources(10000,10000);selectedHero="warrior";
            SelectStarView(4);talentStamp="";RefreshTalentPage();
            if(talentTrees.GetChildCount()!=3||!talentContent.IsVisibleInTree())throw new Exception("三系天赋未显示");
            var column=talentTrees.GetChild<VBoxContainer>(2);var canvas=column.GetChild<ScrollContainer>(1).GetChild<Panel>(0);
            var bash=canvas.GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith("猛击"));bash.EmitSignal(Button.SignalName.Pressed);
            if(session.Heroes[0].Talents.GetValueOrDefault("barb_bash")!=2)throw new Exception("节点点击未学习");
            string tooltip=TalentTooltip(session.Heroes[0],"barb_bash");if(!tooltip.Contains("学习等级 2/20")||!tooltip.Contains("冷却")||!tooltip.Contains("下一级"))throw new Exception("悬浮说明不完整");
            var locked=talentTrees.GetChild<VBoxContainer>(2).GetChild<ScrollContainer>(1).GetChild<Panel>(0).GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith("旋风斩"));locked.EmitSignal(Button.SignalName.Pressed);
            if(session.Heroes[0].Talents.ContainsKey("barb_whirlwind")||!talentMessage.Text.Contains("投入"))throw new Exception("投入不足未阻止学习");
            OpenTalentEditor("warrior");if(talentEditorSlots.GetChildCount()!=5||talentList.GetChildCount()!=5||!talentEditor.Visible)throw new Exception("装配弹窗槽位/主动列表");
            talentList.GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith("旋风斩")).EmitSignal(Button.SignalName.Pressed);if(!talentEditorHint.Text.Contains("第2排"))throw new Exception("未学习技能缺少解锁位置提示");
            talentList.GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith("猛击")).EmitSignal(Button.SignalName.Pressed);
            var confirm=talentEditor.GetChildren().OfType<Panel>().Single().GetChildren().OfType<Button>().Single(b=>b.Text=="确认装配");confirm.EmitSignal(Button.SignalName.Pressed);
            if(talentEditor.Visible||!session.Heroes[0].SkillIds.SequenceEqual(new[]{"barb_bash"}))throw new Exception("确认装配未提交");
            SelectStarView(0);Refresh();var edit=hudSkillBars[0].GetChildren().OfType<Button>().Single(b=>b.Text=="编辑");edit.EmitSignal(Button.SignalName.Pressed);
            if(!talentEditor.Visible||talentEditorSlots.GetChild(0) is not TalentSlotButton||talentBar.GetChild(0) is not TalentSlotButton||hudSkillBars[0].GetChild(0) is not TalentSlotButton)throw new Exception("两处技能栏/编辑入口/共用冷却控件");
            talentEditor.Hide();
            GD.Print("TALENT_UI_SMOKE_OK: three trees, learn/gate, tooltip, locked skill hint, confirm loadout, HUD edit and shared radial cooldown controls");
        }
        finally{talentEditor.Hide();session=original;config=oldConfig;selectedHero="";talentStamp="";viewMode=oldMode;Refresh();TickStarPanels();}
    }
    private void PrepareTalentCapture(bool editor,bool game)
    {
        if(session==null)return;var h=session.Heroes.FirstOrDefault(h=>h.SpecId=="warrior");if(h==null)return;
        selectedHero=h.SpecId;session.GrantGmResources(10000,10000);
        session.BuyStar("s_root",out _);for(int i=1;i<=4;i++)session.BuyStar("s_skill_"+i,out _);
        for(int i=0;i<5;i++){session.LearnTalent(h.SpecId,"barb_bash",out _);session.LearnTalent(h.SpecId,"barb_shout",out _);session.LearnTalent(h.SpecId,"barb_mastery",out _);}
        foreach(string id in new[]{"barb_leap","barb_whirlwind","barb_weaken","barb_defense"})session.LearnTalent(h.SpecId,id,out _);
        session.SetTalentLoadout(h.SpecId,new[]{"barb_bash","barb_leap","barb_whirlwind","barb_shout","barb_weaken"},out _);
        // QA截图固定保留冷却遮罩，不推进模拟，也不保存真实玩家档案。
        foreach(string id in h.SkillIds)h.SkillSwapLocks[id]=config.Skills.Skills[id].Cooldown/40d;
        paused=true;SelectStarView(game?0:4);talentStamp="";RefreshTalentPage();Refresh();
        if(editor)OpenTalentEditor(h.SpecId);captureView=game?"talent-hud":editor?"talent-editor":"talents";
        if(OS.GetCmdlineUserArgs().Contains("--capture-talent-drag"))
        {
            var source=talentTrees.GetChild<VBoxContainer>(2).GetChild<ScrollContainer>(1).GetChild<Panel>(0).GetChildren().OfType<InteractionButton>().First(b=>b.Text.StartsWith("猛击"));
            GetViewport().WarpMouse(new Vector2(690,370));source.ForceDrag(source.DragData!(),source.CreateInformationPreview());captureView="talent-drag";
        }
    }
}
