using Godot;
using IdleCrpg.Core;
using System;
using System.IO;

public partial class Main
{
    private VBoxContainer starPage=null!;
    private Control gmRoot=null!;
    private GameSession? starUiSession;
    private string? starEditorConfigRoot;
    private Label UiLabel(string text,int size=14)
    {var label=new Label {Text=text};label.AddThemeFontSizeOverride("font_size",size);return label;}
    private Button UiButton(Node parent,string text,Action action)
    {var b=new Button {Text=text,CustomMinimumSize=new Vector2(0,30)};b.Pressed+=()=>{action();PlaySound("sfx_click");};parent.AddChild(b);return b;}
    private string GoldText()=>(_editingStars?"星图节点编辑器 · 金币 ":"星图 · 金币 ")+(session?.Gold.ToString("N0")??"0");
    private void BuildStarPanels()
    {
        BuildWorldPage();BuildHeroPage();
        starPage=new VBoxContainer {Position=GlobalPageRect.Position+new Vector2(8,4),Size=GlobalPageRect.Size-new Vector2(16,8),Visible=false};
        starPage.AddThemeConstantOverride("separation",8);uiLayer.AddChild(starPage);
        BuildGmMenu();BuildFormationUi();BuildTalentEditor();
    }
    private void ToggleGm()
    {RefreshGm();gmRoot.Visible=!gmRoot.Visible;PlaySound("sfx_panel");}
    private void SelectStarView(int mode)
    {
        if(_editingStars&&mode!=3)
        {
            if(_starDraft.Dirty){_starHint.Text="请先保存三表或重新加载丢弃草稿，再离开编辑器。";return;}
            _editingStars=false;
        }
        viewMode=mode;TickStarPanels();SaveNow();
    }
    private void RefreshStarPage()
    {
        foreach(var child in starPage.GetChildren()){starPage.RemoveChild(child);child.QueueFree();}
        starUiSession=session;_starDisplayedGold=-1;BuildStarUi();starPage.Visible=viewMode==3;sceneCanvas.Visible=viewMode is not (1 or 3);
    }
    private void TickStarPanels()
    {
        bool show=viewMode==3;
        bool global=viewMode==1||show||viewMode==4;TickHeroPage();
        bool openCastle=viewMode==1&&!castlePage.Visible;
        gameHud.Visible=!PreviewOpen&&!global&&session!=null;partySetup.Visible=!PreviewOpen&&!global&&session==null;
        ((Control)sceneCanvas.GetParent()).Size=SceneRect.Size;castlePage.Visible=viewMode==1;
        rateButton.Visible=!PreviewOpen&&!global&&session!=null;
        summary.Visible=!PreviewOpen&&!global;
        if(openCastle){OpenCurrentChapter();RefreshWorldPage();if(selectedRegion>=0&&MapConfig.Rulers.TryGetValue(selectedRegion,out var ruler))castleListScroll.EnsureControlVisible(castleRows[ruler]);}
        if(viewMode==1)castleMap.QueueRedraw();
        if(show&&(!starPage.Visible||!ReferenceEquals(starUiSession,session)))RefreshStarPage();
        starPage.Visible=show;sceneCanvas.Visible=!global;
        if(show)UpdateStarUi();
    }
}
