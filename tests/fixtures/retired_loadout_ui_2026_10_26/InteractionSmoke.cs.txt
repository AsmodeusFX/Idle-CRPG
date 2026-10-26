using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;

public partial class Main
{
    /// <summary>隔离会话验证点击填空、拖放交换和非法目标；草稿不改变当前技能或阵容。</summary>
    private void RunInteractionSmoke()
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("INTERACTION_UI: "+why);}
        var previous=session;var oldConfig=config;int mode=viewMode;
        try
        {
            session=GameSession.CreateNewWorld(config,20261017,"warrior");session.GrantGmResources(10000,100000);while(session.UpgradeHero(session.Heroes[0].HeroInstanceId,out _)){}
            session.BuyStar("s_root",out _);session.BuyStar("s_skill_1",out _);session.BuyStar("s_party_1",out _);
            for(int i=0;i<10;i++)session.LearnTalent("warrior","barb_bash",out _);
            for(int i=0;i<10;i++)session.LearnTalent("warrior","barb_voice",out _);
            // 嘲讽前置战斗吼叫、持续旋风前置旋风斩；恢复旧节点后按正式前置链学习。
            foreach(var id in new[]{"barb_shout","barb_taunt","barb_whirlwind","barb_whirlwind_window"})session.LearnTalent("warrior",id,out _);
            session.SetTalentLoadout("warrior",new[]{"barb_bash"},out _);selectedHero="warrior";SelectStarView(4);OpenTalentEditor("warrior",0);
            Button Skill(string name)=>talentList.GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith(name));
            Skill("嘲讽").EmitSignal(Button.SignalName.Pressed);Need(talentDraft.SequenceEqual(new[]{"barb_bash","barb_taunt"}),"有空槽时替换了已选中的猛击");
            Need(session.Heroes[0].SkillIds.Length==1,"草稿改变正式装配");
            Skill("持续旋风").EmitSignal(Button.SignalName.Pressed);Need(!talentDraft.Contains("barb_whirlwind_window")&&talentEditorHint.Text.Contains("槽位已满"),"满槽未选择就覆盖技能");
            var source=(InteractionButton)Skill("嘲讽");var payload=source.DragData!();var first=talentEditorSlots.GetChild<InteractionButton>(0);
            Need(source.Details!().Contains("冷却")&&first._CanDropData(Vector2.Zero,payload),"拖动缺说明或目标未接受已学技能");first._DropData(Vector2.Zero,payload);
            Need(talentDraft.SequenceEqual(new[]{"barb_taunt","barb_bash"}),"拖动已装配技能没有交换");
            var locked=talentEditorSlots.GetChild<InteractionButton>(4);Need(!locked._CanDropData(Vector2.Zero,payload),"锁定技能槽接受拖放");
            Need(!first._CanDropData(Vector2.Zero,InteractionButton.Payload("talent","mage","barb_bash"))&&!first._CanDropData(Vector2.Zero,InteractionButton.Payload("talent",session.Heroes[0].HeroInstanceId,"barb_mastery")),"跨英雄或被动拖放未拒绝");
            talentEditor.Hide();talentStamp="";RefreshTalentPage();var live=talentBar.GetChild<InteractionButton>(1);live._DropData(Vector2.Zero,InteractionButton.Payload("talent",session.Heroes[0].HeroInstanceId,"barb_taunt"));
            Need(session.Heroes[0].SkillIds.SequenceEqual(new[]{"barb_bash","barb_taunt"}),"树面板拖放未走装配命令");
            OpenFormation(0);Need(heroNotice.Visible&&!formationRoot.Visible&&session.PendingFormation.Count==0,"锁队不能进入编队草稿");heroNotice.Hide();
            Need(talentTrees.GetChild<VBoxContainer>(0).GetChild(1) is DragScrollContainer&&talentList.GetParent() is DragScrollContainer,"树/技能列表未支持拖动浏览");
            GD.Print("INTERACTION_UI_SMOKE_OK: auto-fill, full-slot safety, skill/hero drag info and swaps, locked/cross-owner gates, draft boundaries and drag-scroll controls");
        }
        finally{talentEditor.Hide();formationRoot.Hide();session=previous;config=oldConfig;viewMode=mode;selectedHero="";talentStamp="";Refresh();TickStarPanels();}
    }
}
