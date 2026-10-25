using System;
using System.Linq;
using Godot;
using IdleCrpg.Core;
public partial class Main
{
    private void RunHeroSmoke()
    {
        var original=session;var savedConfig=config;
        try
        {
            session=GameSession.CreateNewWorld(config,20261017,new[]{"warrior","warrior"});Refresh();SelectStarView(4);TickHeroPage();
            if(!heroPage.Visible||gameHud.Visible||sceneCanvas.Visible||heroSlots.GetChildCount()!=5)throw new Exception("英雄页或未来席位结构缺失");
            heroSlots.GetChild<Button>(1).EmitSignal(Button.SignalName.Pressed);
            if(selectedHero!=session.Heroes[1].HeroInstanceId)throw new Exception("同职角色选择串号");
            heroManageButton.EmitSignal(Button.SignalName.Pressed);
            if(!heroNotice.Visible||formationRoot.Visible||session.PendingFormation.Count!=0)throw new Exception("锁队告知或旧编队门禁失效");
            heroNotice.Hide();SelectStarView(0);Refresh();
            if(heroAdd.Any(b=>b.Visible)||!heroNames[2].Text.Contains("锁定"))throw new Exception("本轮空席错误开放加入");
            GD.Print("HERO_UI_SMOKE_OK: full-page hero view, duplicate instance selection, locked roster notice and no add-seat bypass");
        }
        finally{heroNotice.Hide();session=original;config=savedConfig;formationRoot.Hide();heroStamp="";Refresh();}
    }
}
