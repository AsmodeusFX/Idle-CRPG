using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using V = System.Numerics.Vector2;

public partial class Main : Node2D
{
    private Config config = null!;
    private GameSession? session;
    private Label summary = null!, roster = null!, journal = null!, help = null!;
    private int viewMode, selectedRegion=-1;
    private string captureView="world";
    private LineEdit seed = null!;
    private CheckBox[] choices = Array.Empty<CheckBox>();
    private Button pause = null!, start = null!;
    private bool paused, smoke, capture;
    private int speed = 1, smokeFrames;
    private double accumulator;
    private Vector2 origin;
    private float zoom = 17;
    private Font font = null!;
    private readonly Color[] heroColors = { new("65d9cb"), new("e7ba71"), new("94a9ff") };

    public override void _Ready()
    {
        config = Config.Load(ProjectSettings.GlobalizePath("res://config"));
        InitializeClient();
        font = ThemeDB.FallbackFont;
        RenderingServer.SetDefaultClearColor(new Color("0b1019"));
        var canvas = new CanvasLayer(); AddChild(canvas);
        uiLayer=canvas;
        var settingsButton=new Button {Text="设置",Position=new Vector2(992,32),Size=new Vector2(70,38)};
        settingsButton.Pressed+=ToggleSettings;canvas.AddChild(settingsButton);
        var title = new Label { Text="深渊远征  /  IDLE CRPG", Position=new Vector2(30,20) }; title.AddThemeFontSizeOverride("font_size",26); canvas.AddChild(title);
        var subtitle = new Label { Text="世界远征 · 自动探索 / 战斗 / 交易", Position=new Vector2(32,60) }; subtitle.Modulate=new Color("91a6b7"); canvas.AddChild(subtitle);
        var tabs=new HBoxContainer {Position=new Vector2(32,103)};canvas.AddChild(tabs);
        foreach(var pair in new[]{("自动跟随",0),("世界地图",1),("当前场景",2)})
        {
            var button=new Button {Text=pair.Item1,CustomMinimumSize=new Vector2(116,34)};
            int mode=pair.Item2;button.Pressed+=()=>{viewMode=mode;PlaySound("sfx_click");SaveNow();};tabs.AddChild(button);
        }
        var panel = new Panel { Position=new Vector2(1090,20), Size=new Vector2(325,850) }; canvas.AddChild(panel);
        var box = new VBoxContainer { Position=new Vector2(1110,38), Size=new Vector2(285,820) }; box.AddThemeConstantOverride("separation",9); canvas.AddChild(box);
        box.AddChild(new Label { Text="小队与远征" });
        seed=new LineEdit { Text="20261017", PlaceholderText="整数 seed" }; box.AddChild(seed);
        box.AddChild(new Label { Text="选择 3 位成员，出发后阵容锁定" });
        choices=config.Heroes.Select(s=>new CheckBox { Text=$"{s.Name}   HP {s.Hp} / 攻击 {s.Attack}", ButtonPressed=s.Id!="priest" }).ToArray();
        foreach(var c in choices) {c.Toggled+=_=>PlaySound("sfx_click");box.AddChild(c);}
        start=new Button { Text="开始世界远征", CustomMinimumSize=new Vector2(0,42) }; start.Pressed+=Start; box.AddChild(start);
        var buttons=new HBoxContainer(); box.AddChild(buttons);
        pause=new Button { Text="暂停", SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; pause.Pressed+=()=>{ paused=!paused; pause.Text=paused?"继续":"暂停";PlaySound("sfx_click");SaveNow(); }; buttons.AddChild(pause);
        rateButton=new Button { Text="速度 ×1", SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; rateButton.Pressed+=()=>{speed=speed==4?1:speed*2;rateButton.Text=$"速度 ×{speed}";PlaySound("sfx_click");SaveNow();}; buttons.AddChild(rateButton);
        summary=new Label { Text="选定小队后开始。", AutowrapMode=TextServer.AutowrapMode.WordSmart }; box.AddChild(summary);
        summary.AddThemeFontSizeOverride("font_size",15);
        roster=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart }; box.AddChild(roster);
        roster.AddThemeFontSizeOverride("font_size",15);
        box.AddChild(new HSeparator());
        journal=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart }; journal.AddThemeFontSizeOverride("font_size",14); journal.AddThemeColorOverride("font_color",new Color("a4b8c9")); box.AddChild(journal);
        help=new Label { Position=new Vector2(32,842), Text="世界无遭遇战 · 通关后必先回村交易 · 无新目标时自动复刷" }; help.Modulate=new Color("91a6b7"); canvas.AddChild(help);
        smoke=OS.GetCmdlineUserArgs().Contains("--smoke-test");
        capture=OS.GetCmdlineUserArgs().Contains("--capture");
        if(OS.GetCmdlineUserArgs().Contains("--capture-local"))captureView="local";
        if(OS.GetCmdlineUserArgs().Contains("--capture-village"))captureView="village";
        BuildSettings();
        ResumeProgress();
        if(smoke && session==null) Start();
        if(capture && session==null) Start();
        if(capture)
        {
            viewMode=captureView=="world"?1:2;
            int ticks=captureView=="world"?24000:400;
            for(int i=0;i<ticks;i++)session!.Tick(0.05f);
            if(captureView=="village") for(int i=0;i<50000&&session!.World!.Stage!=CampaignStage.Village;i++)session.Tick(0.05f);
            if(captureView=="local") for(int i=0;i<50000&&session!.World!.Stage!=CampaignStage.Dungeon;i++)session.Tick(0.05f);
            Refresh();ResetSoundBaseline();
        }
        if(OS.GetCmdlineUserArgs().Contains("--capture-settings")){captureView="settings";ToggleSettings();}
        if(OS.GetCmdlineUserArgs().Contains("--capture-reset")){captureView="reset";ToggleSettings();RequestReset();}
        if(smoke)RunClientSmoke();
        if(OS.GetCmdlineUserArgs().Contains("--display-smoke"))RunDisplaySmoke();
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--persistence-write" or "--persistence-read"))RunReopenSmoke();
    }

    private void Start()
    {
        if(!int.TryParse(seed.Text,out int value)) { summary.Text="Seed 请输入有效整数。"; return; }
        var ids=config.Heroes.Where((_,i)=>choices[i].ButtonPressed).Select(h=>h.Id).ToArray();
        if(ids.Length!=3) {summary.Text="请选择恰好 3 位成员。";return;}
        session=GameSession.CreateWorld(config,value,ids); paused=false; accumulator=0; pause.Text="暂停";
        UpdatePartyControls();ResetSoundBaseline();SaveNow();PlaySound("sfx_reward");
        Refresh(); QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if(quitRequested)return;
        TickClient(delta);
        if(session!=null && !paused && !settingsRoot.Visible && !resetConfirmation.Visible)
        {
            accumulator+=Math.Min(delta,0.25)*speed;
            while(accumulator>=0.05) { session.Tick(0.05f); accumulator-=0.05; }
            Refresh();
        }
        TrackSounds();AutoSave(delta);
        QueueRedraw();
        if(capture && ++smokeFrames==10) Capture();
        if(smoke && ++smokeFrames>=120 && !quitRequested && !displaySmokeActive) { GD.Print("SMOKE_OK: "+session?.Status); QuitSafely(); }
    }

    private async void Capture()
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var path=ProjectSettings.GlobalizePath($"res://../artifacts/preview-{captureView}.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print("CAPTURE_OK"); QuitSafely();
    }

    private void Refresh()
    {
        if(session==null)return;
        var s=session;
        var w=s.World!;
        string status=w.Stage==CampaignStage.Dungeon?$"{s.ActiveDungeon!.Name} · {s.Status}\n第 {s.FloorIndex+1}/{s.Floors.Count} 层 · 清理 {s.Floor.Rooms.Count(r=>r.Clear)}/{s.Floor.Rooms.Count}":w.Status;
        summary.Text=$"区域 {w.CurrentRegion} · {status}\n金币 {s.Gold:N0} · 杂物 {s.Inventory.Values.Sum()} · 击杀 {s.Kills}\n经验 {s.Experience}/{s.Heroes[0].Level*18} · 重挑战 {s.Retries}";
        roster.Text=string.Join("\n\n",s.Heroes.Select(h=>$"{h.Name}  Lv.{h.Level}  {(h.Alive?"":"阵亡")}\nHP {Math.Ceiling(h.Hp)} / {h.MaxHp:0}   攻击 {h.Attack:0}"));
        journal.Text="远征记录\n\n"+string.Join("\n",s.Log.TakeLast(3));
    }

    private Vector2 Project(V v) => origin+new Vector2((v.X-v.Y)*zoom,(v.X+v.Y)*zoom*0.5f);
    private void Quad(V min,V max,Color color)
    {
        var points=new[]{Project(min),Project(new V(max.X,min.Y)),Project(max),Project(new V(min.X,max.Y))};
        DrawColoredPolygon(points,color);
        DrawPolyline(points.Append(points[0]).ToArray(),new Color("516375"),1,true);
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(24,104,1040,716),new Color("080d15"));
        if(session==null) {DrawString(font,new Vector2(320,430),"选择小队，开启第一次远征",HorizontalAlignment.Left,-1,24,new Color("91a6b7"));return;}
        var world=session.World!;
        bool showWorld=viewMode==1||(viewMode==0&&world.Stage!=CampaignStage.Dungeon&&world.Stage!=CampaignStage.Village);
        if(showWorld){DrawWorld();return;}
        if(world.Stage!=CampaignStage.Dungeon){DrawVillageOrTravel();return;}
        DrawDungeon();
    }
    private void DrawDungeon()
    {
        if(session==null)return;
        var f=session.Floor;
        var visible=f.Rooms.Where(r=>r.Revealed).ToArray();
        var bounds=visible.SelectMany(r=>new[]{r.Center-new V(4),r.Center+new V(4),r.Center+new V(4,-4),r.Center+new V(-4,4)}).ToList();
        foreach(var passage in f.Passages.Where(p=>p.Revealed))
        {
            var a=f.Rooms[passage.A].Center; var b=f.Rooms[passage.B].Center; var d=V.Normalize(b-a);
            foreach(var p in new[]{a+d*3.5f,b-d*3.5f}) {bounds.Add(p-new V(1));bounds.Add(p+new V(1));}
        }
        var points=bounds.Select(v=>new Vector2(v.X-v.Y,(v.X+v.Y)*0.5f)).ToArray();
        float minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
        zoom=Math.Min(25,Math.Min(950/Math.Max(1,maxX-minX),610/Math.Max(1,maxY-minY)));
        origin=new Vector2(544-(minX+maxX)*zoom/2,457-(minY+maxY)*zoom/2);
        foreach(var p in f.Passages.Where(p=>p.Revealed))
        {
            var a=f.Rooms[p.A].Center; var b=f.Rooms[p.B].Center;
            var dir=V.Normalize(b-a); var left=a+dir*3.5f; var right=b-dir*3.5f;
            Quad(V.Min(left,right)-new V(0.65f),V.Max(left,right)+new V(0.65f),new Color("23323e"));
        }
        foreach(var r in visible.OrderBy(r=>r.X+r.Y))
        {
            var c=r.Center;
            Quad(c-new V(3.5f),c+new V(3.5f),r.Id==session.CurrentRoom?new Color("344651"):new Color("202e3b"));
            for(int i=-3;i<=3;i++)
            {
                DrawLine(Project(c+new V(i,-3.5f)),Project(c+new V(i,3.5f)),new Color(0.5f,0.6f,0.7f,0.08f));
                DrawLine(Project(c+new V(-3.5f,i)),Project(c+new V(3.5f,i)),new Color(0.5f,0.6f,0.7f,0.08f));
            }
            // 后侧墙体，保持前侧角色可读性。
            var wall=new[]{Project(c+new V(-3.5f,3.5f)),Project(c-new V(3.5f)),Project(c+new V(3.5f,-3.5f))};
            DrawPolyline(wall,new Color("637485"),5,true);
            foreach(int id in r.Neighbors)
            {
                var door=c+V.Normalize(f.Rooms[id].Center-c)*3.5f;
                var pos=Project(door);
                DrawCircle(pos,6,new Color("ddb879")); DrawCircle(pos,3,new Color("17232e"));
            }
            var label=Project(c+new V(-2.8f,-2.8f));
            DrawString(font,label,$"{r.Id+1:D2}  {(r.Boss?"楼梯 / BOSS":r.Clear?"已清理":"交战区域")}",HorizontalAlignment.Left,-1,13,new Color("c1d0d8"));
            if(r.Event!="") DrawString(font,Project(c+new V(1.7f,1.7f)),r.Event+(r.EventUsed?" ✓":""),HorizontalAlignment.Left,-1,13,new Color("e4bf7c"));
            if(r.Boss) DrawString(font,Project(c+new V(1.5f,-2)),"≡",HorizontalAlignment.Left,-1,28,new Color("e4bf7c"));
        }
        foreach(var a in visible.SelectMany(r=>r.Monsters).Concat(session.Heroes).Where(a=>a.Alive).OrderBy(a=>a.Position.X+a.Position.Y))
        {
            int index=session.Heroes.IndexOf(a); var color=index>=0?heroColors[index]:a.Boss?new Color("ffc56b"):new Color("dc6b70");
            var pos=Project(a.Position); float size=a.Boss?11:7;
            DrawCircle(pos+new Vector2(0,3),size,new Color(0,0,0,0.4f));
            DrawColoredPolygon(new[]{pos+new Vector2(0,-size*2),pos+new Vector2(size, -size),pos,pos+new Vector2(-size,-size)},color);
            DrawRect(new Rect2(pos.X-12,pos.Y-size*2-7,24,3),new Color("111820"));
            DrawRect(new Rect2(pos.X-12,pos.Y-size*2-7,24*a.Hp/a.MaxHp,3),color);
        }
        foreach(var h in session.Heroes.Where(h=>!h.Alive))
        {
            var p=Project(h.Position);
            DrawLine(p-new Vector2(6,6),p+new Vector2(6,6),new Color("a4b8c9"),3);
            DrawLine(p+new Vector2(-6,6),p+new Vector2(6,-6),new Color("a4b8c9"),3);
            DrawString(font,p+new Vector2(10,0),h.Name+" 待复活",HorizontalAlignment.Left,-1,13,new Color("c1d0d8"));
        }
        if(session.ReviveTarget!=null && session.Rescuer!=null)
        {
            var p=Project(session.ReviveTarget.Position)+new Vector2(-20,12);
            DrawLine(Project(session.Rescuer.Position),Project(session.ReviveTarget.Position),new Color("65d9cb"),1);
            DrawRect(new Rect2(p.X,p.Y,40,4),new Color("111820"));
            DrawRect(new Rect2(p.X,p.Y,40*session.ReviveElapsed/config.ReviveSeconds,4),new Color("65d9cb"));
        }
        if(session.Won) DrawString(font,new Vector2(420,155),session.Status,HorizontalAlignment.Left,-1,32,new Color("f0cf91"));
    }
}
