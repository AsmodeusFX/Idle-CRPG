using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private Control partySetup=null!,gameHud=null!;
    private VBoxContainer interactionList=null!;
    private Label interactionEmpty=null!;
    private Label interactionHeading=null!;
    private ScrollContainer interactionScroll=null!;
    private Label teamChallengeLabel=null!;
    private readonly Label[] currencies=new Label[4];
    private readonly Label[] heroNames=new Label[5],heroStats=new Label[5];
    private readonly ProgressBar[] heroHealth=new ProgressBar[5];
    private readonly HudIcon[] heroIcons=new HudIcon[5];
    private readonly Dictionary<string,Button> castleCards=new();
    private readonly Dictionary<string,long> interactionArrival=new();
    private long nextInteractionArrival;
    private GameSession? interactionSession;

    private Panel HudPanel(Node parent,Vector2 position,Vector2 size)
    {
        var panel=new Panel {Position=position,Size=size};
        var style=new StyleBoxFlat {BgColor=new Color("101923"),BorderColor=new Color("2b3d4d"),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1};
        panel.AddThemeStyleboxOverride("panel",style);parent.AddChild(panel);return panel;
    }
    private Label HudText(Node parent,string value,Vector2 position,int size=14)
    {var label=UiLabel(value,size);label.Position=position;parent.AddChild(label);return label;}

    /// <summary>固定设计尺寸内的五区布局；角色区预留五席，当前上阵规则保持三人。</summary>
    private void BuildHud(CanvasLayer canvas)
    {
        HudPanel(canvas,new Vector2(24,20),new Vector2(1392,60));
        var tabs=new HBoxContainer {Position=new Vector2(36,32)};tabs.AddThemeConstantOverride("separation",6);canvas.AddChild(tabs);
        foreach(var pair in new[]{("游戏",0),("城堡",1),("星图",3)})
        {
            var button=new Button {Text=pair.Item1,CustomMinimumSize=new Vector2(104,36)};
            int mode=pair.Item2;button.Pressed+=()=>{SelectStarView(mode);PlaySound("sfx_click");};tabs.AddChild(button);
        }
        HudText(canvas,"深渊远征 / IDLE CRPG",new Vector2(840,37),20);
        var gmButton=new Button {Name="GmMenuButton",Text="GM",Position=new Vector2(1136,32),Size=new Vector2(80,36)};
        gmButton.Pressed+=ToggleGm;canvas.AddChild(gmButton);
        var settingsButton=new Button {Name="SettingsButton",Text="设置",Position=new Vector2(1224,32),Size=new Vector2(80,36)};
        settingsButton.Pressed+=ToggleSettings;canvas.AddChild(settingsButton);
        pause=new Button {Text="暂停",Position=new Vector2(1312,32),Size=new Vector2(80,36)};
        pause.Pressed+=()=>{paused=!paused;pause.Text=paused?"继续":"暂停";PlaySound("sfx_click");SaveNow();};canvas.AddChild(pause);
        gameHud=new Control {Size=new Vector2(1440,900),MouseFilter=Control.MouseFilterEnum.Ignore};canvas.AddChild(gameHud);
        rateButton=new Button {Text="速度 ×1",Position=new Vector2(38,602),Size=new Vector2(116,36)};
        rateButton.Pressed+=()=>{speed=speed==4?1:speed*2;rateButton.Text=$"速度 ×{speed}";PlaySound("sfx_click");SaveNow();};gameHud.AddChild(rateButton);

        HudPanel(gameHud,new Vector2(1080,92),new Vector2(336,558));
        for(int i=0;i<4;i++)
        {
            float x=i<3?1092+i*106:1092,y=i<3?104:143;
            gameHud.AddChild(new HudIcon {Kind=i==0?"coin":i==1?"junk":i==2?"xp":"point",Position=new Vector2(x,y),Size=new Vector2(24,24)});
            currencies[i]=HudText(gameHud,i==3?"成长点 —":"0",new Vector2(x+28,y+1),i==3?14:16);
            currencies[i].TooltipText=new[]{"金币","杂物数量","当前经验","成长点：后续开放"}[i];
        }
        var separator=new HSeparator {Position=new Vector2(1092,177),Size=new Vector2(312,2)};gameHud.AddChild(separator);
        interactionHeading=HudText(gameHud,"远征交互",new Vector2(1092,187),17);
        var scroll=new ScrollContainer {Position=new Vector2(1092,220),Size=new Vector2(312,417),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};gameHud.AddChild(scroll);
        interactionScroll=scroll;
        interactionList=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};interactionList.AddThemeConstantOverride("separation",7);scroll.AddChild(interactionList);
        interactionEmpty=UiLabel("暂无可用交互\n\n城堡满足挑战条件后在此显示。",14);interactionEmpty.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(interactionEmpty);

        // 开局选择占用交互区，进入远征后隐藏，避免永久占据战斗构图。
        partySetup=new Panel {Position=new Vector2(1092,187),Size=new Vector2(312,450)};gameHud.AddChild(partySetup);
        partySetup.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("101923")});
        var setup=new VBoxContainer {Position=new Vector2(10,10),Size=new Vector2(292,397)};partySetup.AddChild(setup);
        setup.AddChild(UiLabel("组建远征小队",19));
        seed=new LineEdit {Text="20261017",PlaceholderText="整数 seed"};setup.AddChild(seed);
        setup.AddChild(UiLabel("选择 3 位成员，出发后阵容锁定",14));
        choices=config.Heroes.Select(s=>new CheckBox {Text=$"{s.Name}  HP {s.Hp} / ATK {s.Attack}",ButtonPressed=s.Id!="priest"}).ToArray();
        foreach(var choice in choices){choice.Toggled+=_=>PlaySound("sfx_click");setup.AddChild(choice);}
        start=new Button {Text="开始世界远征",CustomMinimumSize=new Vector2(0,42)};start.Pressed+=Start;setup.AddChild(start);

        summary=HudText(gameHud,"选定小队后开始。",new Vector2(38,100),14);summary.Size=new Vector2(1008,40);summary.AutowrapMode=TextServer.AutowrapMode.WordSmart;summary.MouseFilter=Control.MouseFilterEnum.Ignore;
        HudPanel(gameHud,new Vector2(24,662),new Vector2(840,214));
        HudText(gameHud,"小队状态",new Vector2(38,669),16);
        teamChallengeLabel=HudText(gameHud,"",new Vector2(260,671),13);
        HudText(gameHud,"上阵席位  /  5",new Vector2(715,671),13);
        for(int i=0;i<5;i++)
        {
            float y=696+i*34;
            heroIcons[i]=new HudIcon {Kind="hero",Position=new Vector2(38,y),Size=new Vector2(28,28),Accent=heroColors[i%heroColors.Length]};gameHud.AddChild(heroIcons[i]);
            heroNames[i]=HudText(gameHud,"空缺席位",new Vector2(78,y+4),14);
            heroHealth[i]=new ProgressBar {Position=new Vector2(260,y+5),Size=new Vector2(210,20),ShowPercentage=false,MouseFilter=Control.MouseFilterEnum.Ignore};
            heroHealth[i].AddThemeStyleboxOverride("fill",new StyleBoxFlat {BgColor=new Color("397b70")});gameHud.AddChild(heroHealth[i]);
            heroStats[i]=HudText(gameHud,"—",new Vector2(486,y+4),14);
        }
        HudPanel(gameHud,new Vector2(880,662),new Vector2(536,214));
        HudText(gameHud,"药剂",new Vector2(896,669),16);
        HudText(gameHud,"后续开放",new Vector2(1300,671),13);
        for(int i=0;i<4;i++)
        {
            var slot=HudPanel(gameHud,new Vector2(896+i%2*252,704+i/2*76),new Vector2(240,64));
            slot.AddChild(new HudIcon {Kind="potion",Position=new Vector2(12,15),Size=new Vector2(32,32),Accent=new Color("536575")});
            HudText(slot,"药剂槽位",new Vector2(58,10),14);HudText(slot,"尚未开放",new Vector2(58,34),12);
        }
        roster=new Label();journal=new Label();gameHud.AddChild(roster);gameHud.AddChild(journal);roster.Hide();journal.Hide();
        help=HudText(gameHud,"",new Vector2(32,879),12);help.Modulate=new Color("91a6b7");
    }

    private static string CompactAmount(long value)=>value>=1000000?$"{value/1000000d:0.#}M":value>=10000?$"{value/1000d:0.#}K":value.ToString("N0");
    private void RefreshHud()
    {
        int waiting=session?.World==null?0:config.Dungeons.Count(d=>d.Castle&&!session.World.Progress.Cleared.Contains(d.Id)&&session.World.Progress.CastleUnlocked(config,d.Id)&&!session.World.PendingCastles.Contains(d.Id)&&session.World.Target.Dungeon.Id!=d.Id);
        teamChallengeLabel.Text=session==null?"":$"小队挑战等级 {session.TeamChallengeLevel} · 待批 {waiting} · 排队 {session.World?.PendingCastles.Count??0}";
        teamChallengeLabel.TooltipText=session?.World==null?"":string.Join("\n",session.World.PendingCastles.Select(id=>config.Dungeons.Single(d=>d.Id==id).Name+(session.World.Progress.CastleUnlocked(config,id)?" · 已批准待执行":" · 已批准，等待新前置满足")));
        partySetup.Visible=session==null;
        interactionHeading.Visible=session!=null;interactionScroll.Visible=session!=null;
        for(int i=0;i<5;i++)
        {
            var hero=session?.Heroes.ElementAtOrDefault(i);
            heroIcons[i].Modulate=hero==null?new Color("536575"):Colors.White;
            heroNames[i].Text=hero==null?"空缺席位":$"{hero.Name}  Lv.{hero.Level}";
            heroHealth[i].Visible=hero!=null;
            heroHealth[i].MaxValue=hero?.MaxHp??1;heroHealth[i].Value=hero?.Hp??0;
            heroStats[i].Text=hero==null?"—":$"HP {Math.Ceiling(hero.Hp)} / {hero.MaxHp:0}     ATK {hero.Attack:0}";
        }
        currencies[0].Text=CompactAmount(session?.Gold??0);currencies[1].Text=CompactAmount(session?.Inventory.Values.Sum()??0);currencies[2].Text=CompactAmount(session?.Experience??0);
    }

    /// <summary>按功能类型、首次刷新次序保留独立交互卡；目前仅投放城堡挑战，免费卡不显示消耗。</summary>
    private void RefreshCastleInteractions()
    {
        if(!ReferenceEquals(interactionSession,session))
        {
            foreach(var card in castleCards.Values){interactionList.RemoveChild(card);card.QueueFree();}
            castleCards.Clear();interactionArrival.Clear();nextInteractionArrival=0;interactionSession=session;
        }
        var world=session?.World;
        var available=world==null?Array.Empty<DungeonSpec>():config.Dungeons.Where(d=>d.Castle&&!world.Progress.Cleared.Contains(d.Id)&&world.Progress.CastleUnlocked(config,d.Id)).ToArray();
        var ids=available.Select(d=>d.Id).ToHashSet();
        foreach(var id in castleCards.Keys.Where(id=>!ids.Contains(id)).ToArray())
        {var card=castleCards[id];interactionList.RemoveChild(card);card.QueueFree();castleCards.Remove(id);interactionArrival.Remove(id);}
        foreach(var castle in available)
        {
            if(!interactionArrival.ContainsKey(castle.Id))interactionArrival[castle.Id]=nextInteractionArrival++;
            if(!castleCards.ContainsKey(castle.Id))
            {
                string id=castle.Id;
                var card=new Button {Name="CastleInteraction_"+id,CustomMinimumSize=new Vector2(0,94),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
                var content=new HBoxContainer {Name="Content",MouseFilter=Control.MouseFilterEnum.Ignore,OffsetLeft=10,OffsetTop=10,OffsetRight=-10,OffsetBottom=-10};
                card.AddChild(content);content.SetAnchorsPreset(Control.LayoutPreset.FullRect);content.AddThemeConstantOverride("separation",10);
                content.AddChild(new HudIcon {Kind="castle",CustomMinimumSize=new Vector2(32,32),MouseFilter=Control.MouseFilterEnum.Ignore,Accent=new Color("e7ba71")});
                var words=new VBoxContainer {Name="Words",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore};content.AddChild(words);
                var title=UiLabel("挑战"+castle.Name,16);title.Name="Title";title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;words.AddChild(title);
                var description=UiLabel("",13);description.Name="Description";description.AutowrapMode=TextServer.AutowrapMode.WordSmart;words.AddChild(description);
                var retry=UiLabel("（再次挑战）",14);retry.Name="Retry";retry.AddThemeColorOverride("font_color",new Color("ff6262"));retry.Visible=false;words.AddChild(retry);
                foreach(var word in words.GetChildren().OfType<Control>())word.MouseFilter=Control.MouseFilterEnum.Ignore;
                card.Pressed+=()=>{if(session?.RequestCastleChallenge(id)==true){PlaySound("sfx_reward");Refresh();SaveNow();}else PlaySound("sfx_deny");};
                interactionList.AddChild(card);castleCards[id]=card;
            }
            var button=castleCards[castle.Id];
            bool active=world!.Target.Dungeon.Id==castle.Id&&world.Stage is CampaignStage.TravelDungeon or CampaignStage.Dungeon;
            int queue=world.PendingCastles.ToList().IndexOf(castle.Id);
            button.Disabled=active||queue>=0;
            string state=active?(world.Stage==CampaignStage.Dungeon?"挑战中":"正在前往"):queue>=0?$"计划攻击 · 队列第 {queue+1} 位":"攻略城堡，占领整个统治区";
            var level=config.Challenge(castle.Id,session!.DifficultyIndex);
            button.GetNode<Label>("Content/Words/Description").Text=$"挑战等级 {level.Challenge} · 推荐小队等级 {level.Recommended}\n{state}"+(config.PoiProfiles.ContainsKey(castle.Id)?" · 原型内容":"");
            button.TooltipText=castle.Name+"\n"+state;
            button.GetNode<Label>("Content/Words/Retry").Visible=world.IsCastleRetry(castle.Id);
        }
        // 当前只有第四类功能；未来前三类依次排在城堡之前，同类沿用首次刷新序号。
        int index=1;
        foreach(var id in castleCards.Keys.OrderBy(id=>interactionArrival[id]))interactionList.MoveChild(castleCards[id],index++);
        interactionEmpty.Visible=castleCards.Count==0;
        RefreshBranchInteractions();
    }

    /// <summary>验证真实交互卡的独立按钮、刷新顺序与排队行为；仅由 QA 冒烟入口调用。</summary>
    private void RunHudSmoke()
    {
        var original=session;var originalConfig=config;int originalView=viewMode,originalSelected=selectedRegion;
        try
        {
            // 保留旧35城堡排队回归样本；正式第一章与支线由独立冒烟覆盖。
            config=Config.LoadLegacy(ProjectSettings.GlobalizePath("res://config"));session=GameSession.CreateWorld(config,123,new[]{"warrior","ranger","mage"});
            var world=session.World!;world.Progress.Cleared.Add("c1");
            foreach(var dungeon in config.Dungeons.Where(d=>!d.Castle))world.Progress.Cleared.Add(dungeon.Id);
            Refresh();
            if(castleCards.Count!=3||heroNames.Length!=5||currencies.Length!=4||partySetup.Visible)throw new Exception("五区布局或独立城堡按钮不完整");
            var order=castleCards.Keys.OrderBy(id=>interactionArrival[id]).ToArray();
            var buttons=order.Select(id=>castleCards[id]).ToArray();
            buttons[2].EmitSignal(Button.SignalName.Pressed);
            buttons[0].EmitSignal(Button.SignalName.Pressed);
            buttons[1].EmitSignal(Button.SignalName.Pressed);
            if(world.Target.Dungeon.Id!=order[2]||!world.PendingCastles.SequenceEqual(new[]{order[0],order[1]}))throw new Exception("独立按钮未按点击顺序排队");
            Refresh();
            if(buttons.Any(b=>!b.Disabled)||!order.SequenceEqual(castleCards.Keys.OrderBy(id=>interactionArrival[id])))throw new Exception("刷新改变了同类型按钮顺序或允许重复点击");
            if(SceneRect.End.Y>=662)throw new Exception("战斗视口覆盖底部状态区");
            SelectStarView(1);
            if(selectedRegion!=world.CurrentRegion||detailRegion!=selectedRegion||!selectedTileTitle.Text.Contains(config.Regions.Single(r=>r.Id==selectedRegion).Name))throw new Exception("打开城堡页未默认选中玩家所在格或缺少详情");
            if(gameHud.Visible||sceneCanvas.Visible||rateButton.IsVisibleInTree()||!castlePage.Visible||castleRows.Count!=config.Dungeons.Count(d=>d.Castle))throw new Exception("城堡页没有占满全局内容区或遗漏城堡");
            if(castleStates["c1"].Text!="已占领"||castleStates[order[0]].Text!="计划攻击")throw new Exception("城堡列表攻略状态不同步");
            var last=config.Dungeons.Last(d=>d.Castle);castleRows[last.Id].EmitSignal(Button.SignalName.Pressed);
            if(selectedRegion!=last.RegionId)throw new Exception("城堡列表无法定位地图");
            var region=config.Regions[5];
            _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=MapPoint(new System.Numerics.Vector2(region.X+.5f,region.Y+.5f))});
            if(selectedRegion!=region.Id||castlePage.MouseFilter!=Control.MouseFilterEnum.Ignore)throw new Exception("全局地图点击坐标或输入透传不正确");
            if(detailRegion!=region.Id||!castleRows[config.Rulers[region.Id]].HasThemeStyleboxOverride("normal")||selectedDungeonLabels.Count!=config.Dungeons.Count(d=>d.RegionId==region.Id)||SelectedTileColor==SelectedDominionColor)throw new Exception("格子详情、清单高亮或两级边框颜色不同步");
            SelectStarView(0);
            SelectStarView(1);
            if(selectedRegion!=world.CurrentRegion)throw new Exception("再次打开城堡页未重置到当前格");
            SelectStarView(0);
            if(!rateButton.IsVisibleInTree()||!SceneRect.HasPoint(rateButton.Position)||!SceneRect.HasPoint(rateButton.Position+rateButton.Size))throw new Exception("倍速未限定在游戏视口内");
            if(pause.Size!=uiLayer.GetNode<Button>("SettingsButton").Size||pause.Size!=uiLayer.GetNode<Button>("GmMenuButton").Size)throw new Exception("右上角按钮尺寸不一致");
            GD.Print("HUD_SMOKE_OK: independent castle cards, queue, full-page castle list/map, linked selection, game-only speed and equal header buttons");
        }
        catch(Exception error){GD.PushError(error.ToString());QuitSafely(1);}
        finally{session=original;config=originalConfig;viewMode=originalView;selectedRegion=originalSelected;Refresh();TickStarPanels();}
    }
}

/// <summary>原型 HUD 矢量占位图标，不依赖外部美术资源。</summary>
public partial class HudIcon : Control
{
    public string Kind="hero";
    public Color Accent=new("e7ba71");
    public override void _Draw()
    {
        var c=Size/2;float r=Math.Min(Size.X,Size.Y)*.4f;
        if(Kind=="coin"){DrawCircle(c,r,Accent);DrawCircle(c,r*.62f,new Color("735426"));DrawLine(c+new Vector2(0,-r*.5f),c+new Vector2(0,r*.5f),Accent,2);}
        else if(Kind=="castle")
        {
            DrawRect(new Rect2(c-new Vector2(r,r*.5f),new Vector2(r*2,r*1.4f)),Accent);
            for(int i=0;i<3;i++)DrawRect(new Rect2(c+new Vector2(-r+i*r*.8f,-r),new Vector2(r*.4f,r)),Accent);
            DrawRect(new Rect2(c+new Vector2(-r*.2f,r*.2f),new Vector2(r*.4f,r*.7f)),new Color("101923"));
        }
        else if(Kind=="hero")
        {DrawCircle(c+new Vector2(0,-r*.55f),r*.3f,Accent);DrawColoredPolygon(new[]{c+new Vector2(0,-r*.1f),c+new Vector2(r*.7f,r),c+new Vector2(-r*.7f,r)},Accent);DrawLine(c+new Vector2(r*.7f,r*.5f),c+new Vector2(r*.9f,-r*.8f),new Color("c1d0d8"),2);}
        else if(Kind=="potion")
        {DrawCircle(c+new Vector2(0,r*.3f),r*.7f,Accent);DrawRect(new Rect2(c+new Vector2(-r*.25f,-r*.8f),new Vector2(r*.5f,r)),Accent);DrawLine(c+new Vector2(-r*.35f,-r*.8f),c+new Vector2(r*.35f,-r*.8f),new Color("a0acb2"),3);}
        else if(Kind=="xp")
        {DrawString(ThemeDB.FallbackFont,c+new Vector2(-11,6),"XP",HorizontalAlignment.Left,-1,14,new Color("71c6e4"));}
        else if(Kind=="junk")
        {DrawCircle(c+new Vector2(0,r*.25f),r*.72f,new Color("a4b8c9"));DrawLine(c+new Vector2(-r*.35f,-r*.7f),c+new Vector2(r*.35f,-r*.7f),new Color("a4b8c9"),4);DrawLine(c+new Vector2(-r*.4f,-r*.25f),c+new Vector2(r*.4f,-r*.25f),new Color("526575"),2);}
        else
        {DrawColoredPolygon(new[]{c+new Vector2(0,-r),c+new Vector2(r,0),c+new Vector2(0,r),c+new Vector2(-r,0)},Kind=="xp"?new Color("71c6e4"):Kind=="junk"?new Color("a4b8c9"):new Color("8d80bd"));}
    }
}
