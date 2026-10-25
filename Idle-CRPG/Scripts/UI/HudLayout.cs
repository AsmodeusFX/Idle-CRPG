using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private Control partySetup=null!,gameHud=null!;
    private SpinBox[] partyCounts=Array.Empty<SpinBox>();
    private VBoxContainer interactionList=null!;
    private Label interactionEmpty=null!;
    private Label interactionHeading=null!;
    private ScrollContainer interactionScroll=null!;
    private readonly Button[] potionButtons=new Button[4];
    private readonly Button[] potionDiscards=new Button[4];
    private Label scrollBarLabel=null!;
    private Button scrollUseButton=null!;
    private Label teamChallengeLabel=null!;
    /// <summary>小队区公共XP余额；旧个人经验条语义已改为公共池，不保留每人私有账户。</summary>
    private Label teamExperienceLabel=null!;
    private readonly Label[] currencies=new Label[2];
    private readonly Button[] heroAdd=new Button[5];
    private readonly Label[] heroExperience=new Label[5];
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

    /// <summary>固定设计尺寸内的五区布局；创角单选，星图解锁至五席。</summary>
    private void BuildHud(CanvasLayer canvas)
    {
        HudPanel(canvas,new Vector2(24,20),new Vector2(1392,60));
        var tabs=new HBoxContainer {Position=new Vector2(36,32)};tabs.AddThemeConstantOverride("separation",6);canvas.AddChild(tabs);
        foreach(var pair in new[]{("游戏",0),("英雄",4),("星图",3),("地图",1)})
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
        rateButton=new Button {Text="速度 ×1",Position=new Vector2(38,564),Size=new Vector2(116,36)};
        rateButton.Pressed+=()=>{speed=speed==4?1:speed*2;rateButton.Text=$"速度 ×{speed}";PlaySound("sfx_click");SaveNow();};gameHud.AddChild(rateButton);

        HudPanel(gameHud,new Vector2(1080,92),new Vector2(336,558));
        for(int i=0;i<currencies.Length;i++)
        {
            float x=1092+i*156,y=104;
            string hint=i==0?"金币":"KP · 本轮击杀点；背包杂物仍在回城自动售卖";
            gameHud.AddChild(new HudIcon {Kind=i==0?"coin":"junk",Position=new Vector2(x,y),Size=new Vector2(24,24),TooltipText=hint});
            currencies[i]=HudText(gameHud,"0",new Vector2(x+28,y+1),16);
            currencies[i].TooltipText=hint;currencies[i].MouseFilter=Control.MouseFilterEnum.Stop;
        }
        var separator=new HSeparator {Position=new Vector2(1092,143),Size=new Vector2(312,2)};gameHud.AddChild(separator);
        interactionHeading=HudText(gameHud,"远征交互",new Vector2(1092,153),17);
        var scroll=new ScrollContainer {Position=new Vector2(1092,186),Size=new Vector2(312,451),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};gameHud.AddChild(scroll);
        interactionScroll=scroll;
        interactionList=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};interactionList.AddThemeConstantOverride("separation",7);scroll.AddChild(interactionList);
        interactionEmpty=UiLabel("暂无可用交互\n\n城堡满足挑战条件后在此显示。",14);interactionEmpty.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(interactionEmpty);

        // 开局选择占用交互区，进入远征后隐藏，避免永久占据战斗构图。
        partySetup=new Control {Position=CreationOffset,Size=new Vector2(1060,650)};canvas.AddChild(partySetup);
        seed=new LineEdit {Text="20261017",Position=new Vector2(416,520),Size=new Vector2(240,32)};partySetup.AddChild(seed);
        partyCounts=config.Heroes.Select((s,i)=>new SpinBox {MinValue=0,MaxValue=4,Step=1,Value=i==0?1:0,Position=new Vector2(100+i*182,425),Size=new Vector2(120,36),Editable=config.HeroUnlockKills[s.Id]==0}).ToArray();
        for(int i=0;i<partyCounts.Length;i++){int n=i;partyCounts[i].ValueChanged+=v=>{choices[n].SetPressedNoSignal(v>0);sceneCanvas.QueueRedraw();};partySetup.AddChild(partyCounts[i]);}
        choices=config.Heroes.Select((s,i)=>new CheckBox {Text=s.Name,ButtonPressed=i==0,Disabled=config.HeroUnlockKills[s.Id]>0,Position=new Vector2(85+i*182,i%2==0?380:315),Size=new Vector2(180,40),TooltipText=config.HeroUnlockKills[s.Id]>0?$"累计击杀 {config.HeroUnlockKills[s.Id]} 后解锁":"初始可用"}).ToArray();
        for(int i=0;i<choices.Length;i++){int n=i;var choice=choices[i];choice.Toggled+=on=>{partyCounts[n].Value=on?Math.Max(1,partyCounts[n].Value):0;PlaySound("sfx_click");sceneCanvas.QueueRedraw();};partySetup.AddChild(choice);}
        AddCampHitTargets();start=new Button {Text="开始游戏",Position=new Vector2(416,566),Size=new Vector2(240,48)};start.Pressed+=Start;partySetup.AddChild(start);
        summary=HudText(gameHud,"选择一位英雄，开启远征。",new Vector2(38,100),14);summary.Size=new Vector2(1008,40);summary.AutowrapMode=TextServer.AutowrapMode.WordSmart;summary.MouseFilter=Control.MouseFilterEnum.Ignore;
        HudPanel(gameHud,new Vector2(24,662),new Vector2(840,214));
        HudText(gameHud,"小队状态",new Vector2(38,669),16);
        teamChallengeLabel=HudText(gameHud,"",new Vector2(260,671),13);
        teamExperienceLabel=HudText(gameHud,"",new Vector2(560,671),13);teamExperienceLabel.Name="TeamPublicExperience";
        HudText(gameHud,"普通席位 / 4 · 已锁队",new Vector2(715,671),13);
        for(int i=0;i<5;i++)
        {
            float y=696+i*34;
            heroIcons[i]=new HudIcon {Kind="hero",Position=new Vector2(38,y),Size=new Vector2(28,28),Accent=heroColors[i%heroColors.Length]};gameHud.AddChild(heroIcons[i]);
            heroNames[i]=HudText(gameHud,"席位未解锁",new Vector2(78,y+4),14);
            int slot=i;heroAdd[i]=new Button {Name="HudFormationAdd"+i,Text="+",Position=new Vector2(209,y+1),Size=new Vector2(30,28),Visible=false};heroAdd[i].Pressed+=()=>OpenFormation(slot);gameHud.AddChild(heroAdd[i]);
            heroHealth[i]=new ProgressBar {Position=new Vector2(260,y+5),Size=new Vector2(210,20),ShowPercentage=false,MouseFilter=Control.MouseFilterEnum.Ignore};
            heroHealth[i].AddThemeStyleboxOverride("fill",new StyleBoxFlat {BgColor=new Color("397b70")});gameHud.AddChild(heroHealth[i]);
            heroExperience[i]=HudText(gameHud,"",new Vector2(260,y+8),10);heroExperience[i].Size=new Vector2(210,14);heroExperience[i].HorizontalAlignment=HorizontalAlignment.Center;heroExperience[i].MouseFilter=Control.MouseFilterEnum.Ignore;
            heroStats[i]=HudText(gameHud,"—",new Vector2(486,y+4),14);heroStats[i].Hide();
            hudSkillBars[i]=new HBoxContainer {Position=new Vector2(480,y),Size=new Vector2(375,28)};hudSkillBars[i].AddThemeConstantOverride("separation",2);gameHud.AddChild(hudSkillBars[i]);
        }
        HudPanel(gameHud,new Vector2(880,662),new Vector2(536,214));
        HudText(gameHud,"药剂（点击使用 · 待用可丢弃）",new Vector2(896,669),15);
        for(int i=0;i<4;i++)
        {
            int slotIndex=i;
            var button=new Button {Name="PotionSlot_"+i,Position=new Vector2(896+i%2*252,700+i/2*76),Size=new Vector2(240,62),Text=""};
            button.Pressed+=()=>UsePotionFromHud(slotIndex);
            gameHud.AddChild(button);potionButtons[i]=button;
            var discard=new Button {Name="PotionDiscard_"+i,Text="丢弃",Position=new Vector2(1100+i%2*252,724+i/2*76),Size=new Vector2(32,24),TooltipText="丢弃待用药剂以腾槽；生效中不可丢弃"};
            discard.Pressed+=()=>DiscardPotionFromHud(slotIndex);
            gameHud.AddChild(discard);potionDiscards[i]=discard;
        }
        var scrollBar=HudPanel(gameHud,new Vector2(24,610),new Vector2(1040,40));scrollBar.Name="ScrollQuickBar";
        scrollBarLabel=HudText(scrollBar,"卷轴快捷栏：暂无卷轴（击杀掉落）",new Vector2(12,10),13);
        scrollUseButton=new Button {Name="ScrollUse",Text="对最近敌人使用",Position=new Vector2(760,4),Size=new Vector2(170,30)};
        scrollUseButton.Pressed+=()=>{if(session==null)return;var room=session.Floor.Rooms[session.CurrentRoom];int index=room.Monsters.FindIndex(m=>m.Alive);
            var stock=session.ScrollStock.FirstOrDefault(p=>p.Value>0);if(index<0||stock.Key==null){help.Text=index<0?"当前没有可攻击目标":"没有卷轴库存";return;}
            help.Text=session.UseScroll(stock.Key,index,out string reason)?$"已使用{config.ScrollOf(stock.Key).Name}":reason;Refresh();SaveNow();};
        scrollBar.AddChild(scrollUseButton);
        roster=new Label();journal=new Label();gameHud.AddChild(roster);gameHud.AddChild(journal);roster.Hide();journal.Hide();
        help=HudText(gameHud,"",new Vector2(32,879),12);help.Modulate=new Color("91a6b7");
    }

    private static string CompactAmount(long value)=>value>=1000000?$"{value/1000000d:0.#}M":value>=10000?$"{value/1000d:0.#}K":value.ToString("N0");
    /// <summary>卷轴库存只显示数量；药剂槽显示待用/生效中（生效中仍占槽且不可丢弃）。</summary>
    private void RefreshConsumablesHud()
    {
        var slots=session?.PotionSlots;
        for(int i=0;i<potionButtons.Length;i++)
        {
            var button=potionButtons[i];var discard=potionDiscards[i];if(button==null||discard==null)continue;
            var slot=slots==null?null:slots[i];
            bool has=slot!=null&&slot.ItemId!="";
            button.Text=!has?"空槽（击杀掉落药剂）":$"{slot!.Name}\n"+(slot.Active?$"生效中 {slot.RemainingSeconds:0}s · 占槽":$"待用 · 点击使用");
            button.Disabled=session==null||!has||slot!.Active;
            discard.Visible=has;discard.Disabled=session==null||slot!.Active;
            discard.TooltipText=has&&slot!.Active?"生效中不可丢弃：产品尚未裁决是否允许提前结束效果":"丢弃待用药剂以腾槽";
        }
        if(scrollBarLabel!=null)
        {
            var stock=session==null?Array.Empty<System.Collections.Generic.KeyValuePair<string,int>>():session.ScrollStock.Where(p=>p.Value>0).ToArray();
            scrollBarLabel.Text=stock.Length==0?"卷轴快捷栏：暂无卷轴（击杀掉落）":"卷轴快捷栏："+string.Join(" · ",stock.Select(p=>$"{config.ScrollOf(p.Key).Name} ×{p.Value}"));
            if(scrollUseButton!=null)scrollUseButton.Disabled=session==null||stock.Length==0;
        }
    }
    private void UsePotionFromHud(int slot)
    {
        if(session==null)return;
        help.Text=session.UsePotion(slot,out string reason)?$"已饮用药剂：{session.PotionSlots[slot].Name}":reason;
        Refresh();SaveNow();
    }
    private void DiscardPotionFromHud(int slot)
    {
        if(session==null)return;
        help.Text=session.DiscardPotion(slot,out string reason)?"已丢弃待用药剂":reason;
        Refresh();SaveNow();
    }
    private void RefreshHud()
    {
        int waiting=session?.World==null?0:config.Dungeons.Count(d=>d.Castle&&!session.World.Progress.Cleared.Contains(d.Id)&&session.World.Progress.CastleUnlocked(config,d.Id)&&!session.World.PendingCastles.Contains(d.Id)&&session.World.Target.Dungeon.Id!=d.Id);
        teamChallengeLabel.Text=session==null?"":$"小队挑战等级 {session.TeamChallengeLevel} · 待批 {waiting} · 排队 {session.World?.PendingCastles.Count??0}";
        // 小队标题行宽度有限：只显示余额，升级规则放悬停提示，避免与右侧"普通席位/已锁队"文字重叠。
        teamExperienceLabel.Text=session==null?"":$"公共XP {session.PublicExperience:N0}";
        teamExperienceLabel.TooltipText=session==null?"":"公共XP由击杀全额入池，无等级差衰减；只有点击右侧指定英雄升级才消耗，且每次恰好升1级。";
        teamChallengeLabel.TooltipText=session?.World==null?"":string.Join("\n",session.World.PendingCastles.Select(id=>config.Dungeons.Single(d=>d.Id==id).Name+(session.World.Progress.CastleUnlocked(config,id)?" · 已批准待执行":" · 已批准，等待新前置满足")));
        partySetup.Visible=session==null;
        interactionHeading.Visible=session!=null;interactionScroll.Visible=session!=null;
        for(int i=0;i<5;i++)
        {
            var hero=session?.Heroes.ElementAtOrDefault(i);
            heroIcons[i].Modulate=hero==null?new Color("536575"):Colors.White;
            heroAdd[i].Visible=false;
            heroNames[i].TooltipText=hero==null?"":$"{hero.Name} Lv.{hero.Level}\n公共XP {session!.PublicExperience} · 下一等级费用 {session.ExperienceRequired(hero)}\n近战优先接敌容量 {session!.EngagementCapacity(hero)}（不是受击人数上限）\n公共池无等级差衰减；指定实例手动升级。";
            heroNames[i].MouseFilter=Control.MouseFilterEnum.Stop;
            heroNames[i].Text=hero==null?(i<4?"本轮空席（锁定）":"未来永久第五席"):$"{hero.Name}  Lv.{hero.Level}";
            heroHealth[i].Visible=hero!=null;
            heroExperience[i].Text=hero==null?"":$"HP {hero.Hp:0}/{hero.MaxHp:0} · AR {session!.AttackRating(hero):0} DR {session.DefenseRating(hero):0}";
            heroHealth[i].MaxValue=hero?.MaxHp??1;heroHealth[i].Value=hero?.Hp??0;
            heroStats[i].Text=hero==null?"—":$"HP {Math.Ceiling(hero.Hp)} / {hero.MaxHp:0}     ATK {hero.Attack:0}";
        }
        RefreshGrowthInteractions();
        RefreshHudSkills();
        RefreshConsumablesHud();
        currencies[0].Text=CompactAmount(session?.Gold??0);currencies[1].Text="KP "+CompactAmount(session?.Kp??0);currencies[1].TooltipText=$"KP本轮余额 · 背包杂物 {session?.Inventory.Values.Sum()??0}，回城自动售卖";
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
            button.Disabled=active||queue>=0||!session!.CanChallengeCastle(castle.Id,out _);
            string state=active?(world.Stage==CampaignStage.Dungeon?"挑战中":"正在前往"):queue>=0?$"计划攻击 · 队列第 {queue+1} 位":"攻略城堡，占领整个统治区";
            var level=config.Challenge(castle.Id,session!.DifficultyIndex);
            button.GetNode<Label>("Content/Words/Description").Text=$"挑战等级 {level.Challenge} · 推荐小队等级 {level.Recommended}\n{state}"+(config.PoiProfiles.ContainsKey(castle.Id)?" · 原型内容":"");
            session!.CanChallengeCastle(castle.Id,out string gateReason);button.TooltipText=castle.Name+"\n"+state+"\n"+gateReason;
            if(gateReason!="")button.GetNode<Label>("Content/Words/Description").Text+="\n"+gateReason;
            button.GetNode<Label>("Content/Words/Retry").Visible=world.IsCastleRetry(castle.Id);
        }
        // 当前只有第四类功能；未来前三类依次排在城堡之前，同类沿用首次刷新序号。
        RefreshGrowthInteractions();int index=upgradeCards.Count+3;
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
            SeedQaLmax(config.MaxMonsterLevel);var world=session.World!;world.Progress.Cleared.Add("c1");
            foreach(var dungeon in config.Dungeons.Where(d=>!d.Castle))world.Progress.Cleared.Add(dungeon.Id);
            Refresh();
            if(castleCards.Count!=3||heroNames.Length!=5||currencies.Length!=2||partySetup.Visible)throw new Exception("五区布局或独立城堡按钮不完整");
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
        else if(Kind=="level"){DrawString(ThemeDB.FallbackFont,c+new Vector2(-11,6),"Lv",HorizontalAlignment.Left,-1,14,new Color("71c6e4"));}
        else if(Kind=="xp")
        {DrawString(ThemeDB.FallbackFont,c+new Vector2(-11,6),"XP",HorizontalAlignment.Left,-1,14,new Color("71c6e4"));}
        else if(Kind=="junk")
        {DrawCircle(c+new Vector2(0,r*.25f),r*.72f,new Color("a4b8c9"));DrawLine(c+new Vector2(-r*.35f,-r*.7f),c+new Vector2(r*.35f,-r*.7f),new Color("a4b8c9"),4);DrawLine(c+new Vector2(-r*.4f,-r*.25f),c+new Vector2(r*.4f,-r*.25f),new Color("526575"),2);}
        else
        {DrawColoredPolygon(new[]{c+new Vector2(0,-r),c+new Vector2(r,0),c+new Vector2(0,r),c+new Vector2(-r,0)},Kind=="xp"?new Color("71c6e4"):Kind=="junk"?new Color("a4b8c9"):new Color("8d80bd"));}
    }
}
