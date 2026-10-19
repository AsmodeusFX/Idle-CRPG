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
    private double hudElapsed;
    private readonly MotionPresentation motion=new();
    private float motionAlpha=1;
    private V VisualPosition(Actor actor)=>motion.Position(session!,actor,motionAlpha);
    private V VisualWorldPosition=>motion.WorldPosition(session!,motionAlpha);
    private Vector2 origin;
    private float zoom = 17;
    private Font font = null!;
    private readonly Color[] heroColors = { new("65d9cb"), new("e7ba71"), new("94a9ff") };

    public override void _Ready()
    {
        var startup=System.Diagnostics.Stopwatch.StartNew();
        config = Config.Load(ProjectSettings.GlobalizePath("res://config"));
        double configMs=startup.Elapsed.TotalMilliseconds;
        InitializeClient();
        font = ThemeDB.FallbackFont;
        BuildSceneCanvas();
        RenderingServer.SetDefaultClearColor(new Color("0b1019"));
        var canvas = new CanvasLayer(); AddChild(canvas);
        uiLayer=canvas;
        BuildHud(canvas);
        smoke=OS.GetCmdlineUserArgs().Contains("--smoke-test");
        capture=OS.GetCmdlineUserArgs().Contains("--capture");
        if(OS.GetCmdlineUserArgs().Contains("--capture-local"))captureView="local";
        if(OS.GetCmdlineUserArgs().Contains("--capture-combat"))captureView="combat";
        if(OS.GetCmdlineUserArgs().Contains("--capture-travel"))captureView="travel";
        if(OS.GetCmdlineUserArgs().Contains("--capture-village"))captureView="village";
        if(OS.GetCmdlineUserArgs().Contains("--capture-town-return"))captureView="town-return";
        if(OS.GetCmdlineUserArgs().Contains("--capture-town-out"))captureView="town-out";
        string? townCapture=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--capture-town-act"));
        if(townCapture!=null)captureView="village";
        BuildStarPanels();BuildSettings();
        ResumeProgress();
        if(OS.GetCmdlineUserArgs().Contains("--performance-test"))GD.Print($"STARTUP_MS config={configMs:F1}; ready={startup.Elapsed.TotalMilliseconds:F1}");
        if(smoke && session==null) Start();
        bool captureSetup=OS.GetCmdlineUserArgs().Contains("--capture-setup");
        if(captureSetup){session=null;captureView="setup";viewMode=0;}
        if(capture && session==null&&!captureSetup) Start();
        if(capture&&!captureSetup)
        {
            viewMode=captureView=="world"?1:2;
            int ticks=captureView=="world"?24000:captureView is "travel" or "village" or "town-return" or "town-out"?0:400;
            for(int i=0;i<ticks;i++)session!.Tick(0.05f);
            if(captureView=="village"){for(int i=0;i<50000&&session!.World!.Stage!=CampaignStage.Village;i++)session.Tick(0.05f);paused=true;}
            if(captureView=="travel"){for(int i=0;i<50000&&session!.World!.Stage==CampaignStage.Village;i++)session.Tick(.05f);paused=true;}
            if(captureView=="town-return")
            {
                session!.SetGmBattle(new(InstantDungeonClear:true));
                for(int i=0;i<50000&&!(session.World!.TownReturnPortalVisible&&session.World.PortalPhase==PortalPhase.None);i++)session.Tick(.05f);
                paused=true;
            }
            if(captureView=="town-out")
            {
                session!.World!.Progress.Cleared.UnionWith(config.Dungeons.Where(d=>!d.Castle&&config.Rulers[d.RegionId]=="A01-C01").Select(d=>d.Id));
                for(int i=0;i<50000&&session.World.PortalPhase!=PortalPhase.Using;i++)session.Tick(.05f);
                paused=true;
            }
            if(captureView=="local") for(int i=0;i<50000&&session!.World!.Stage!=CampaignStage.Dungeon;i++)session.Tick(0.05f);
            if(captureView=="combat")
            {
                for(int i=0;i<50000;i++)
                {
                    if(session!.World!.Stage==CampaignStage.Dungeon&&!session.MovingBetweenRooms&&session.Floor.Rooms[session.CurrentRoom].Monsters.Count(m=>m.Alive)>=3)break;
                    session.Tick(.05f);
                }
                for(int i=0;i<10;i++)session!.Tick(.05f);paused=true;
            }
            if(OS.GetCmdlineUserArgs().Contains("--capture-castle"))
            {
                for(int i=0;i<500000;i++){if(i%100==0&&config.Dungeons.Any(d=>d.Castle&&session!.World!.Progress.CastleUnlocked(config,d.Id)))break;session!.Tick(.05f);}
                captureView="castle";viewMode=1;paused=true;
            }
            if(OS.GetCmdlineUserArgs().Contains("--capture-stairs"))
            {
                foreach(var h in session!.Heroes)h.Hp=h.MaxHp=100000;
                for(int i=0;i<100000;i++)
                {
                    session.Tick(.05f);
                    if(session.World!.Stage==CampaignStage.Dungeon&&session.Status=="楼梯集合")break;
                }
                captureView="stairs";viewMode=2;paused=true;
            }
            if(townCapture!=null)
            {
                int act=int.Parse(townCapture.Substring("--capture-town-act".Length));
                if(act<2||act>5)throw new ArgumentOutOfRangeException(nameof(act));
                while(session!.ChapterIndex<act-1)
                {
                    int chapter=session.ChapterIndex;session.CompleteGmChapter();
                    for(int i=0;i<5000&&session.ChapterIndex==chapter;i++)session.Tick(.05f);
                    if(session.ChapterIndex==chapter)throw new Exception("城镇预览跨章停滞");
                }
                session.SetGmBattle(new(InstantDungeonClear:true));
                for(int i=0;i<20000&&!(session.World!.TownReturnPortalVisible&&session.World.Stage==CampaignStage.Village&&session.World.PortalPhase==PortalPhase.None);i++)session.Tick(.05f);
                config=session.Config;OpenCurrentChapter();captureView="town-act"+act;viewMode=0;paused=true;cameraReady=false;
            }
            Refresh();ResetSoundBaseline();
        }
        if(OS.GetCmdlineUserArgs().Contains("--capture-settings")){captureView="settings";ToggleSettings();}
        if(OS.GetCmdlineUserArgs().Contains("--capture-reset")){captureView="reset";ToggleSettings();RequestReset();}
        if(OS.GetCmdlineUserArgs().Contains("--capture-stars")){captureView="stars";SelectStarView(3);BuyStarNode("s_root");BuyStarNode("s_hp");BuyStarNode("s_atk");}
        if(OS.GetCmdlineUserArgs().Contains("--capture-star-editor")){captureView="star-editor";viewMode=3;ToggleStarEditor();EditorClickNode("s_root");}
        if(OS.GetCmdlineUserArgs().Contains("--capture-gm")){captureView="gm";ToggleGm();}
        if(smoke){RunClientSmoke();RunSceneSmoke();RunHudSmoke();RunChapterUiSmoke();RunAct1UiSmoke();RunGmSmoke();RunStarSmoke();}
        if(OS.GetCmdlineUserArgs().Contains("--display-smoke"))RunDisplaySmoke();
        if(OS.GetCmdlineUserArgs().Contains("--performance-test"))RunPerformanceSmoke();
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--persistence-write" or "--persistence-read"))RunReopenSmoke();
    }

    private void Start()
    {
        if(!int.TryParse(seed.Text,out int value)) { summary.Text="Seed 请输入有效整数。"; return; }
        var ids=config.Heroes.Where((_,i)=>choices[i].ButtonPressed).Select(h=>h.Id).ToArray();
        if(ids.Length!=3) {summary.Text="请选择恰好 3 位成员。";return;}
        session=GameSession.CreateWorld(config,value,ids); paused=false; accumulator=0; pause.Text="暂停";
        cameraReady=false;
        UpdatePartyControls();ResetSoundBaseline();SaveNow();PlaySound("sfx_reward");
        Refresh(); QueueRedraw();
    }

    public override void _Process(double delta)
    {
        long profileStart=System.Diagnostics.Stopwatch.GetTimestamp();
        if(quitRequested)return;
        TickClient(delta);
        if(session!=null && !paused && !settingsRoot.Visible && !resetConfirmation.Visible && !gmRoot.Visible && !_editingStars)
        {
            AdvanceSimulation(delta);
            motionAlpha=(float)(accumulator/.05);
            hudElapsed+=delta;
            if(hudElapsed>=.1){hudElapsed=0;Refresh();}
        }
        else {motionAlpha=1;if(session!=null)motion.BeforeTick(session);}
        SyncCampaignConfig();TrackSounds();AutoSave(delta);
        TickStarPanels();
        UpdateCamera(delta);sceneCanvas.QueueRedraw();
        if(performanceRun){processTimes.Add(Milliseconds(profileStart));frameTimes.Add(delta*1000);}
        if(capture && ++smokeFrames==10) Capture();
        if(smoke && ++smokeFrames>=120 && !quitRequested && !displaySmokeActive) { GD.Print("SMOKE_OK: "+session?.Status); QuitSafely(); }
    }
    /// <summary>城镇、室外、地牢和传送统一按倍速消费固定逻辑步；表现仍按帧插值。</summary>
    private void AdvanceSimulation(double delta)
    {
        accumulator+=Math.Min(delta,0.25)*speed;
        while(accumulator>=0.05){motion.BeforeTick(session!);session!.Tick(0.05f);accumulator-=0.05;}
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
        if(session!=null&&!ReferenceEquals(config,session.Config)){SyncCampaignConfig();return;}
        RefreshHud();RefreshCastleInteractions();
        if(viewMode==1)RefreshWorldPage();
        if(session==null)return;
        var s=session;
        var w=s.World!;
        string status=w.Stage==CampaignStage.Dungeon?$"{s.CurrentFloorDisplayName} · {s.Status}\n第 {s.FloorIndex+1}/{s.Floors.Count} 层 · 清理 {s.Floor.Rooms.Count(r=>r.Clear)}/{s.Floor.Rooms.Count}":w.Status;
        summary.Text=$"{status}  |  击杀 {s.Kills:N0}";
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
        sceneCanvas?.QueueRedraw();
    }
    private void PaintScene()
    {
        DrawRect(SceneRect,new Color("080d15"));
        if(session==null) {DrawString(font,new Vector2(320,430),"选择小队，开启第一次远征",HorizontalAlignment.Left,-1,24,new Color("91a6b7"));return;}
        var world=session.World!;
        bool showWorld=viewMode==1;
        if(showWorld){DrawWorld();return;}
        if(!world.InDungeonScene)DrawVillageOrTravel();else DrawDungeon();
        DrawPortalPerformance();
    }
    private void DrawDungeon()
    {
        if(session==null)return;
        var f=session.Floor;
        var visible=f.Rooms.Where(r=>r.Revealed).ToArray();
        foreach(var p in f.Passages.Where(p=>p.Revealed))
        {
            var a=f.Rooms[p.A]; var b=f.Rooms[p.B];
            var left=a.DoorTowards(b);var right=b.DoorTowards(a);
            Quad(V.Min(left,right)-new V(0.65f),V.Max(left,right)+new V(0.65f),new Color("23323e"));
        }
        foreach(var r in visible.OrderBy(r=>r.X+r.Y))
        {
            var c=r.Center;
            Quad(c-r.HalfSize,c+r.HalfSize,r.Id==session.CurrentRoom?new Color("344651"):new Color("202e3b"));
            for(float i=-r.HalfWidth+.5f;i<r.HalfWidth;i++)
            {
                DrawLine(Project(c+new V(i,-r.HalfHeight)),Project(c+new V(i,r.HalfHeight)),new Color(0.5f,0.6f,0.7f,0.08f));
            }
            for(float i=-r.HalfHeight+.5f;i<r.HalfHeight;i++)DrawLine(Project(c+new V(-r.HalfWidth,i)),Project(c+new V(r.HalfWidth,i)),new Color(.5f,.6f,.7f,.08f));
            // 后侧墙体，保持前侧角色可读性。
            var wall=new[]{Project(c+new V(-r.HalfWidth,r.HalfHeight)),Project(c-r.HalfSize),Project(c+new V(r.HalfWidth,-r.HalfHeight))};
            for(int i=0;i<2;i++)DrawColoredPolygon(new[]{wall[i],wall[i+1],wall[i+1]-new Vector2(0,24),wall[i]-new Vector2(0,24)},new Color("526675"));
            DrawPolyline(wall.Select(p=>p-new Vector2(0,24)).ToArray(),new Color("889a9e"),3,true);
            foreach(int id in r.Neighbors)
            {
                var door=r.DoorTowards(f.Rooms[id]);
                var pos=Project(door);
                DrawRect(new Rect2(pos-new Vector2(10,27),new Vector2(20,29)),new Color("d3b779"),false,3);DrawRect(new Rect2(pos-new Vector2(7,24),new Vector2(14,24)),new Color("131e28"));
            }
            var label=Project(c-r.HalfSize+new V(.3f,.3f))-new Vector2(10,30);
            DrawString(font,label,$"{r.Id+1:D2}  {(r.Boss?"楼梯 / BOSS":r.Clear?"已清理":"交战区域")}",HorizontalAlignment.Left,-1,13,new Color("c1d0d8"));
            if(r.Event!="") DrawString(font,Project(r.EventPosition),r.Event+(r.EventUsed?" ✓":""),HorizontalAlignment.Left,-1,13,new Color("e4bf7c"));
            foreach(var loot in r.Loot.Where(l=>!l.Collected))
            {
                var p=Project(loot.Position);
                DrawCircle(p,6,new Color("f0ca79"));DrawCircle(p,3,new Color("8c6335"));
            }
            if(r.Id==0||r.Boss&&(config.Town==null||session.FloorIndex<session.Floors.Count-1))
            {
                var p=Project(f.StairPosition(r));
                var tint=r.Id==0?new Color("81cbd5"):new Color("e4bf7c");
                // 墙上阶梯入口使用宽拱框和递进踏步，与矩形房门区分。
                DrawColoredPolygon(new[]{p+new Vector2(-18,0),p+new Vector2(-18,-24),p+new Vector2(0,-38),p+new Vector2(18,-24),p+new Vector2(18,0)},new Color("101b25"));
                DrawPolyline(new[]{p+new Vector2(-18,0),p+new Vector2(-18,-24),p+new Vector2(0,-38),p+new Vector2(18,-24),p+new Vector2(18,0)},tint,3,true);
                for(int step=0;step<4;step++)DrawLine(p+new Vector2(-14+step*2,-3-step*6),p+new Vector2(14-step*2,-3-step*6),tint,2,true);
                string text=r.Id==0?(session.FloorIndex==0?"入口 ↑":"上层 ↑"):(session.FloorIndex==session.Floors.Count-1?"出口 ↑":"下层 ↓");
                DrawString(font,p+new Vector2(-25,-44),text,HorizontalAlignment.Left,-1,12,tint);
            }
        }
        foreach(var a in visible.SelectMany(r=>r.Monsters).Concat(session.Heroes).Where(a=>a.Alive).OrderBy(a=>a.Position.X+a.Position.Y))
        {
            int index=session.Heroes.IndexOf(a); var color=index>=0?heroColors[index]:a.Boss?new Color("ffc56b"):new Color("dc6b70");
            DrawFighter(Project(VisualPosition(a)),color,index,a.Boss,a.Hp/a.MaxHp,a.Name);
            if(index>=0&&a.Timer>a.Cooldown-.14f)
            {
                var target=f.Rooms[session.CurrentRoom].Monsters.Where(m=>m.Alive).OrderBy(m=>V.DistanceSquared(a.Position,m.Position)).FirstOrDefault();
                if(target!=null)DrawLine(Project(VisualPosition(a))-new Vector2(0,18),Project(VisualPosition(target))-new Vector2(0,15),new Color(color.R,color.G,color.B,.6f),2,true);
            }
        }
        foreach(var h in session.Heroes.Where(h=>!h.Alive))
        {
            var p=Project(VisualPosition(h));
            DrawLine(p-new Vector2(6,6),p+new Vector2(6,6),new Color("a4b8c9"),3);
            DrawLine(p+new Vector2(-6,6),p+new Vector2(6,-6),new Color("a4b8c9"),3);
            DrawString(font,p+new Vector2(10,0),h.Name+" 待复活",HorizontalAlignment.Left,-1,13,new Color("c1d0d8"));
        }
        if(session.ReviveTarget!=null && session.Rescuer!=null)
        {
            var p=Project(VisualPosition(session.ReviveTarget))+new Vector2(-20,12);
            DrawLine(Project(VisualPosition(session.Rescuer)),Project(VisualPosition(session.ReviveTarget)),new Color("65d9cb"),1);
            DrawRect(new Rect2(p.X,p.Y,40,4),new Color("111820"));
            DrawRect(new Rect2(p.X,p.Y,40*session.ReviveElapsed/config.ReviveSeconds,4),new Color("65d9cb"));
        }
        if(session.Won) DrawString(font,new Vector2(420,155),session.Status,HorizontalAlignment.Left,-1,32,new Color("f0cf91"));
    }
}
