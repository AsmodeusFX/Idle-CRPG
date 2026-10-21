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
            session=GameSession.CreateNewWorld(config,20261017,"warrior");session.GrantGmResources(10000,10000);
            session.BuyStar("s_root",out _);session.BuyStar("s_skill_1",out _);session.BuyStar("s_skill_2",out _);session.BuyStar("s_party_1",out _);
            for(int i=0;i<5;i++)session.LearnTalent("warrior","barb_bash",out _);
            foreach(var id in new[]{"barb_shout","barb_leap","barb_whirlwind"})session.LearnTalent("warrior",id,out _);
            session.SetTalentLoadout("warrior",new[]{"barb_bash"},out _);selectedHero="warrior";SelectStarView(4);OpenTalentEditor("warrior",0);
            Button Skill(string name)=>talentList.GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith(name));
            Skill("战斗吼叫").EmitSignal(Button.SignalName.Pressed);Need(talentDraft.SequenceEqual(new[]{"barb_bash","barb_shout"}),"有空槽时替换了已选中的猛击");
            talentEditorSlots.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);Skill("跳斩").EmitSignal(Button.SignalName.Pressed);
            Need(talentDraft.SequenceEqual(new[]{"barb_bash","barb_shout","barb_leap"})&&session.Heroes[0].SkillIds.Length==1,"点击填空/草稿边界");
            Skill("旋风斩").EmitSignal(Button.SignalName.Pressed);Need(!talentDraft.Contains("barb_whirlwind")&&talentEditorHint.Text.Contains("槽位已满"),"满槽未选择就覆盖技能");
            var source=(InteractionButton)Skill("跳斩");var payload=source.DragData!();var first=talentEditorSlots.GetChild<InteractionButton>(0);
            Need(source.Details!().Contains("冷却")&&first._CanDropData(Vector2.Zero,payload),"拖动缺说明或目标未接受已学技能");first._DropData(Vector2.Zero,payload);
            Need(talentDraft.SequenceEqual(new[]{"barb_leap","barb_shout","barb_bash"}),"拖动已装配技能没有交换");
            var locked=talentEditorSlots.GetChild<InteractionButton>(4);Need(!locked._CanDropData(Vector2.Zero,payload),"锁定技能槽接受拖放");
            Need(!first._CanDropData(Vector2.Zero,InteractionButton.Payload("talent","mage","barb_bash"))&&!first._CanDropData(Vector2.Zero,InteractionButton.Payload("talent","warrior","barb_mastery")),"跨英雄或被动拖放未拒绝");
            talentEditor.Hide();talentStamp="";RefreshTalentPage();var live=talentBar.GetChild<InteractionButton>(1);live._DropData(Vector2.Zero,InteractionButton.Payload("talent","warrior","barb_shout"));
            Need(session.Heroes[0].SkillIds.SequenceEqual(new[]{"barb_bash","barb_shout"}),"树面板拖放未走装配命令");
            OpenFormation(0);formationCandidates.GetNode<Button>("FormationHero_mage").EmitSignal(Button.SignalName.Pressed);
            Need(formationDraft[0]=="warrior"&&formationDraft[1]=="mage"&&session.Heroes.Count==1&&session.PendingFormation.Count==0,"英雄点击未优先填空或即刻改变队伍");
            var mage=formationCandidates.GetNode<InteractionButton>("FormationHero_mage");var seat=formationSeats.GetChild<InteractionButton>(0);seat._DropData(Vector2.Zero,mage.DragData!());
            Need(formationDraft[0]=="mage"&&formationDraft[1]=="warrior","英雄拖动没有交换已有席位");
            var blocked=formationSeats.GetChild<InteractionButton>(4);Need(!blocked._CanDropData(Vector2.Zero,InteractionButton.Payload("hero","formation","mage")),"未解锁席位接受英雄");
            Need(!formationSeats.GetChild<InteractionButton>(0)._CanDropData(Vector2.Zero,InteractionButton.Payload("hero","formation","priest")),"未解锁英雄接受拖入");
            formationCandidates.GetNode<Button>("FormationHero_ranger").EmitSignal(Button.SignalName.Pressed);Need(!formationDraft.Contains("ranger"),"满队点击擅自替换英雄");
            formationSeats.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);formationCandidates.GetNode<Button>("FormationHero_ranger").EmitSignal(Button.SignalName.Pressed);
            Need(formationDraft[0]=="ranger"&&formationDraft[1]=="warrior","显式选择未替换英雄");
            Need(talentTrees.GetChild<VBoxContainer>(0).GetChild(1) is DragScrollContainer&&talentList.GetParent() is DragScrollContainer,"树/技能列表未支持拖动浏览");
            GD.Print("INTERACTION_UI_SMOKE_OK: auto-fill, full-slot safety, skill/hero drag info and swaps, locked/cross-owner gates, draft boundaries and drag-scroll controls");
        }
        finally{talentEditor.Hide();formationRoot.Hide();session=previous;config=oldConfig;viewMode=mode;selectedHero="";talentStamp="";Refresh();TickStarPanels();}
    }
}
