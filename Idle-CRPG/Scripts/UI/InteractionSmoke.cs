using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
public partial class Main
{
    /// <summary>旧装配拖放回归原文已归档；新版不接受拖放装配，仍验证锁队与浏览交互。</summary>
    private void RunInteractionSmoke()
    {
        var previous=session;int mode=viewMode;
        try
        {
            session=GameSession.CreateNewWorld(config,20261017,"warrior");selectedHero=session.Heroes[0].HeroInstanceId;SelectStarView(4);talentStamp="";RefreshTalentPage();
            if(session.SetTalentLoadout(selectedHero,Array.Empty<string>(),out _))throw new Exception("旧装配API未拒绝");
            bool rejected=false;try{session.EquipSkills(selectedHero,Array.Empty<string>());}catch(InvalidOperationException){rejected=true;}
            if(!rejected||talentEditor.Visible||talentBar.GetChildren().OfType<InteractionButton>().Any(b=>b.DragData!=null||b.AcceptDrop!=null))throw new Exception("旧编辑/装配拖放仍开放");
            OpenFormation(0);if(!heroNotice.Visible||formationRoot.Visible||session.PendingFormation.Count!=0)throw new Exception("锁队进入编队草稿");heroNotice.Hide();
            if(talentRegionNodes.GetParent() is not DragScrollContainer)throw new Exception("区域浏览未支持拖动");
            GD.Print("INTERACTION_UI_SMOKE_OK: retired loadout/drag rejected, fixed skills information, team lock and region scroll retained");
        }
        finally{talentEditor.Hide();formationRoot.Hide();session=previous;viewMode=mode;selectedHero="";talentStamp="";Refresh();TickStarPanels();}
    }
}
