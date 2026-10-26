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
                session=GameSession.CreateWorld(config,20261017,new[]{spec.Id});selectedHero=session.Heroes[0].HeroInstanceId;talentStamp="";SelectStarView(4);RefreshTalentPage();
                var hero=session.Heroes[0];
                if(hero.SkillIds.Single()!=config.Talents.StartingSkills[spec.Id]||talentBar.GetChildCount()!=4||talentTrees.GetChildCount()!=5||session.ClassTraitUnlocked(spec.Id))throw new Exception("固定四技能/T锁定错误："+spec.Id);
                if(hudSkillBars[0].GetChildren().OfType<Button>().Any(b=>b.Text=="编辑"))throw new Exception("HUD旧编辑入口仍存在");
            }
            GD.Print("CLASS_TALENT_UI_SMOKE_OK: five class definitions, four fixed cards plus T, S1 present, no editor");
        }
        finally{session=original;viewMode=mode;selectedHero="";talentStamp="";Refresh();TickStarPanels();}
    }
    private void RunTalentSmoke()
    {
        var original=session;int mode=viewMode;
        try
        {
            session=GameSession.CreateNewWorld(config,20261017,new[]{"warrior","warrior"});session.GrantGmResources(10000,100000);
            var hero=session.Heroes[0];while(session.UpgradeHero(hero.HeroInstanceId,out _)){}
            selectedHero=hero.HeroInstanceId;selectedSkillRegion=2;SelectStarView(4);talentStamp="";RefreshTalentPage();
            var learn=talentRegionNodes.GetChildren().OfType<Button>().Single(b=>b.Name=="LearnTalent_barb_taunt");learn.EmitSignal(BaseButton.SignalName.Pressed);
            if(!hero.SkillIds.Contains("barb_taunt")||session.Heroes[1].SkillIds.Contains("barb_taunt"))throw new Exception("学习未自动生效或实例串改");
            talentStamp="";RefreshTalentPage();talentRegionReset.EmitSignal(BaseButton.SignalName.Pressed);
            if(hero.SkillIds.Contains("barb_taunt")||!hero.SkillIds.Contains("barb_bash"))throw new Exception("局部洗点丢S1或残留S2");
            selectedSkillRegion=5;talentStamp="";RefreshTalentPage();
            if(talentRegionNodes.GetChildren().OfType<Button>().Any(b=>!b.Disabled))throw new Exception("T未购资格可学习");
            session.BuyStar("s_root",out _);session.BuyStar("s_trait_warrior",out _);talentStamp="";RefreshTalentPage();
            var trait=talentRegionNodes.GetChildren().OfType<Button>().Single(b=>b.Name=="LearnTalent_warrior_trait");trait.EmitSignal(BaseButton.SignalName.Pressed);
            if(hero.Talents.GetValueOrDefault("warrior_trait")!=1||session.Heroes[1].Talents.ContainsKey("warrior_trait"))throw new Exception("T实例投资串改");
            GD.Print("TALENT_UI_SMOKE_OK: real fixed-skill/T controls, instance learning, local reset, S1 retained, class qualification");
        }
        finally{session=original;viewMode=mode;selectedHero="";talentStamp="";Refresh();TickStarPanels();}
    }
    private void PrepareTalentCapture(bool editor,bool game)
    {
        if(session==null)return;selectedHero=session.Heroes[0].HeroInstanceId;selectedSkillRegion=1;paused=true;
        SelectStarView(game?0:4);talentStamp="";RefreshTalentPage();Refresh();captureView=game?"fixed-talent-hud":"fixed-talents";
    }
}
