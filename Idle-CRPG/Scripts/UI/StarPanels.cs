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
        starPage=new VBoxContainer {Position=SceneRect.Position+new Vector2(8,4),Size=SceneRect.Size-new Vector2(16,8),Visible=false};
        starPage.AddThemeConstantOverride("separation",8);uiLayer.AddChild(starPage);
        gmRoot=new Control {Size=new Vector2(1440,900),Visible=false,MouseFilter=Control.MouseFilterEnum.Stop};uiLayer.AddChild(gmRoot);
        gmRoot.AddChild(new ColorRect {Size=new Vector2(1440,900),Color=new Color(0,0,0,.8f)});
        gmRoot.AddChild(new Panel {Position=new Vector2(420,225),Size=new Vector2(600,350)});
        var box=new VBoxContainer {Position=new Vector2(448,250),Size=new Vector2(544,290)};box.AddThemeConstantOverride("separation",18);gmRoot.AddChild(box);
        box.AddChild(UiLabel("GM · 调试与开发工具",24));
        box.AddChild(UiLabel("游戏调试功能统一从此处进入。\n当前提供星图节点编辑器。",16));
        UiButton(box,"星图节点编辑器",()=>{gmRoot.Hide();viewMode=3;if(!_editingStars)ToggleStarEditor();else RefreshStarPage();}).Name="StarEditorEntry";
        UiButton(box,"关闭 GM",()=>gmRoot.Hide());
    }
    private void ToggleGm()
    {gmRoot.Visible=!gmRoot.Visible;PlaySound("sfx_panel");}
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
        starUiSession=session;_starDisplayedGold=-1;BuildStarUi();starPage.Visible=viewMode==3;sceneCanvas.Visible=viewMode!=3;
    }
    private void TickStarPanels()
    {
        bool show=viewMode==3;
        if(show&&(!starPage.Visible||!ReferenceEquals(starUiSession,session)))RefreshStarPage();
        starPage.Visible=show;sceneCanvas.Visible=!show;
        if(show)UpdateStarUi();
    }
}
