using Godot;
using IdleCrpg.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private CanvasLayer uiLayer=null!;
    private Control settingsRoot=null!;
    private ConfirmationDialog resetConfirmation=null!;
    private OptionButton resolutionBox=null!;
    private CheckButton fullscreenBox=null!;
    private HSlider musicSlider=null!,soundSlider=null!;
    private Label musicValue=null!,soundValue=null!,saveStatus=null!;
    private Button rateButton=null!;
    private AppSettings preferences=new();
    private SettingsStore settingsStore=null!;
    private SaveStore saveStore=null!;
    private bool automation,closing;
    private bool quitRequested;
    private double sinceSave,settingsDelay;
    private long savedRevision=-1;
    private bool settingsDirty;
    private Vector2I? pendingWindowSize;
    private Task? progressWrite;
    private long writingRevision;
    private bool forceProgressSave;
    // 编辑器内嵌窗口由编辑器管理，不能修改原生窗口的模式、尺寸和位置。
    private bool NativeWindowAvailable => DisplayServer.GetName()!="headless"&&!Engine.IsEmbeddedInEditor();
    private void RefreshDisplayControls()
    {
        resolutionBox.Disabled=!NativeWindowAvailable||preferences.Fullscreen;
        fullscreenBox.Disabled=!NativeWindowAvailable;
        string hint=Engine.IsEmbeddedInEditor()?"内嵌运行时请在编辑器调整尺寸，或独立运行游戏。":"";
        resolutionBox.TooltipText=hint;fullscreenBox.TooltipText=hint;
    }

    private void InitializeClient()
    {
        var args=OS.GetCmdlineUserArgs();bool reopen=args.Contains("--persistence-write")||args.Contains("--persistence-read");
        automation=args.Contains("--smoke-test")||args.Contains("--capture")||args.Contains("--performance-test")||reopen;
        string directory=automation?ProjectSettings.GlobalizePath("res://../artifacts/qa/"+(reopen?"reopen":Guid.NewGuid().ToString("N"))):ProjectSettings.GlobalizePath("user://");
        saveStore=new SaveStore(Path.Combine(directory,"progress_v1.json"));settingsStore=new SettingsStore(Path.Combine(directory,"settings.json"));
        preferences=settingsStore.Load();
        if(args.Contains("--persistence-write"))saveStore.Reset();
        GetTree().AutoAcceptQuit=false;
        ApplyDisplay();GetWindow().SizeChanged+=WindowResized;
        InitializeAudio();
    }
    private void ResumeProgress()
    {
        var document=saveStore.Load(config);
        if(document?.Session!=null)
        {
            session=GameSession.Restore(config,document.Session);speed=document.Speed;paused=document.Paused;viewMode=document.ViewMode;selectedRegion=document.SelectedRegion;
            seed.Text=session.Seed.ToString();UpdatePartyControls();ResetSoundBaseline();Refresh();
            savedRevision=session.Revision;
        }
        if(saveStore.LastWarning!=null)help.Text=saveStore.LastWarning;
    }
    private void UpdatePartyControls()
    {
        partySetup.Visible=session==null;RefreshHud();RefreshCastleInteractions();
        start.Visible=session==null;seed.Editable=session==null;
        for(int i=0;i<choices.Length;i++)
        {
            choices[i].Disabled=session!=null;
            if(session!=null)choices[i].SetPressedNoSignal(session.Heroes.Any(h=>h.SpecId==config.Heroes[i].Id));
        }
        pause.Text=paused?"继续":"暂停";rateButton.Text=$"速度 ×{speed}";
    }
    /// <summary>只按持久状态变更保存；命中、走路和计时不产生保存请求，无周期战斗快照。</summary>
    private void AutoSave(double delta)
    {
        if(progressWrite!=null)
        {
            if(!progressWrite.IsCompleted)return;
            FinishProgressWrite();
        }
        if(session==null)return;sinceSave+=delta;
        if(forceProgressSave||(session.Revision!=savedRevision&&sinceSave>=config.AutosaveMinSeconds))
        {
            // 主线程只复制已分离引用的检查点，编码、Flush 和原子替换都在后台串行完成。
            var document=new SaveDocument {Session=session.CaptureProgress(),Speed=speed,Paused=paused,ViewMode=viewMode,SelectedRegion=selectedRegion};
            writingRevision=session.Revision;sinceSave=0;forceProgressSave=false;
            progressWrite=Task.Run(()=>saveStore.WriteEncoded(SaveStore.Encode(document)));
        }
    }
    private void FinishProgressWrite()
    {
        if(progressWrite==null)return;
        try {progressWrite.GetAwaiter().GetResult();savedRevision=writingRevision;if(saveStatus!=null)saveStatus.Text="进度已自动保存";}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        {if(help!=null)help.Text="保存失败，将重试："+e.Message;}
        finally {progressWrite=null;}
    }
    private void SaveNow(bool wait=false)
    {
        if(!wait){forceProgressSave=true;AutoSave(0);return;}
        FinishProgressWrite();
        if(session==null||saveStore==null)return;
        try {saveStore.Save(session,speed,paused,viewMode,selectedRegion);savedRevision=session.Revision;sinceSave=0;forceProgressSave=false;if(saveStatus!=null)saveStatus.Text="进度已自动保存";}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        {sinceSave=0;if(help!=null)help.Text="保存失败，将重试："+e.Message;}
    }
    public override void _Notification(int what)
    {
        if(what==NotificationWMCloseRequest)
        {
            SaveNow(true);PersistSettings();closing=true;QuitSafely();
        }
        if(what==NotificationApplicationFocusOut)SaveNow();
    }
    public override void _ExitTree()
    {
        if(!closing){SaveNow(true);if(settingsStore!=null)PersistSettings();}
        ReleaseAudio();
    }
    private async void QuitSafely(int code=0)
    {
        if(quitRequested)return;quitRequested=true;
        // 启动即退出的自动检查也先让播放器处理排队的 Play，再发送 Stop。
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        ReleaseAudio();
        // 给音频线程处理 Stop 的一次混音周期，避免循环音乐的播放资源滞留到引擎关闭。
        await ToSignal(GetTree().CreateTimer(.08),SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit(code);
    }

    private void BuildSettings()
    {
        settingsRoot=new Control {Visible=false,Size=new Vector2(1440,900),MouseFilter=Control.MouseFilterEnum.Stop};uiLayer.AddChild(settingsRoot);
        var shade=new ColorRect {Color=new Color(0,0,0,.75f),Size=new Vector2(1440,900)};settingsRoot.AddChild(shade);
        var panel=new Panel {Position=new Vector2(420,140),Size=new Vector2(600,620)};settingsRoot.AddChild(panel);
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("14202d"),BorderColor=new Color("405567"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=10,CornerRadiusTopRight=10,CornerRadiusBottomLeft=10,CornerRadiusBottomRight=10});
        var box=new VBoxContainer {Position=new Vector2(450,163),Size=new Vector2(540,565)};box.AddThemeConstantOverride("separation",16);settingsRoot.AddChild(box);
        var heading=new HBoxContainer();box.AddChild(heading);
        var title=new Label {Text="设置",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};title.AddThemeFontSizeOverride("font_size",26);heading.AddChild(title);
        var close=new Button {Text="关闭",CustomMinimumSize=new Vector2(80,36)};close.Pressed+=ToggleSettings;heading.AddChild(close);
        box.AddChild(new Label {Text="界面尺寸"});
        resolutionBox=new OptionButton();foreach(var size in config.Windows)resolutionBox.AddItem($"{size.Width} × {size.Height}");
        resolutionBox.ItemSelected+=index=>{if(!NativeWindowAvailable||index<0||index>=config.Windows.Count)return;var p=config.Windows[(int)index];preferences.Width=p.Width;preferences.Height=p.Height;ApplyDisplay();PersistSettings();PlaySound("sfx_click");};box.AddChild(resolutionBox);
        fullscreenBox=new CheckButton {Text="全屏显示（窗口尺寸在窗口模式生效）"};fullscreenBox.Toggled+=value=>{if(!NativeWindowAvailable)return;preferences.Fullscreen=value;ApplyDisplay();RefreshDisplayControls();PersistSettings();};box.AddChild(fullscreenBox);RefreshDisplayControls(); if(Engine.IsEmbeddedInEditor())box.AddChild(new Label {Text="内嵌运行：尺寸由编辑器控制，请独立运行以调整窗口。"});
        var musicRow=new HBoxContainer();box.AddChild(musicRow);musicRow.AddChild(new Label {Text="音乐音量",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});musicValue=new Label();musicRow.AddChild(musicValue);
        musicSlider=new HSlider {MinValue=0,MaxValue=100,Step=1};musicSlider.ValueChanged+=value=>{preferences.MusicVolume=(float)value;ApplyAudio();RefreshVolumeText();QueueSettingsSave();};box.AddChild(musicSlider);
        var soundRow=new HBoxContainer();box.AddChild(soundRow);soundRow.AddChild(new Label {Text="音效音量",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});soundValue=new Label();soundRow.AddChild(soundValue);
        soundSlider=new HSlider {MinValue=0,MaxValue=100,Step=1};soundSlider.ValueChanged+=value=>{preferences.SoundVolume=(float)value;ApplyAudio();RefreshVolumeText();QueueSettingsSave();};soundSlider.DragEnded+=_=>PlaySound("sfx_click");box.AddChild(soundSlider);
        box.AddChild(new HSeparator());saveStatus=new Label {Text="进度自动保存 · 下次打开自动继续"};box.AddChild(saveStatus);
        box.AddChild(new Label {Text="重置会清除队伍成长、星图、金币、背包与世界进度。\n窗口和音量设置保留。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var reset=new Button {Text="重置进度",CustomMinimumSize=new Vector2(0,42)};reset.AddThemeColorOverride("font_color",new Color("e89a91"));reset.Pressed+=RequestReset;box.AddChild(reset);
        resetConfirmation=new ConfirmationDialog {Title="确认重置进度",DialogText="确定清除全部游戏进度吗？\n角色成长、星图加点、金币、背包、探索与占领进度都会清除。\n此操作无法撤销，画面和音量设置不受影响。",OkButtonText="确认重置",CancelButtonText="取消",Exclusive=true};AddChild(resetConfirmation);
        resetConfirmation.Confirmed+=ResetProgress;
    }
    private void ToggleSettings()
    {
        if(_editingStars){_starHint.Text="请先保存或丢弃草稿并退出节点编辑器，再打开设置。";return;}
        if(settingsRoot.Visible){settingsRoot.Hide();PersistSettings();}
        else
        {
            while(resolutionBox.ItemCount>config.Windows.Count)resolutionBox.RemoveItem(resolutionBox.ItemCount-1);
            int selected=config.Windows.FindIndex(p=>p.Width==preferences.Width&&p.Height==preferences.Height);
            if(selected<0){resolutionBox.AddItem($"{preferences.Width} × {preferences.Height}（当前）");selected=resolutionBox.ItemCount-1;}
            resolutionBox.Select(selected);RefreshDisplayControls();
            fullscreenBox.SetPressedNoSignal(preferences.Fullscreen);musicSlider.SetValueNoSignal(preferences.MusicVolume);soundSlider.SetValueNoSignal(preferences.SoundVolume);RefreshVolumeText();
            SaveNow();settingsRoot.Show();
        }
        PlaySound("sfx_panel");
    }
    private void RequestReset() {PlaySound("sfx_deny");resetConfirmation.PopupCentered(new Vector2I(530,210));}
    private void ResetProgress()
    {
        FinishProgressWrite();
        forceProgressSave=false;
        try {saveStore.Reset();}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) {help.Text="重置失败，原进度保留："+e.Message;return;}
        session=null;paused=false;speed=1;accumulator=0;sinceSave=0;savedRevision=-1;viewMode=0;selectedRegion=-1;
        UpdatePartyControls();ResetSoundBaseline();settingsRoot.Hide();
        summary.Text="进度已重置，请选择小队开始远征。";roster.Text="";journal.Text="";help.Text="世界无遭遇战 · 通关后必先回村交易 · 无新目标时自动复刷";PlaySound("sfx_reward");QueueRedraw();
    }
    private void RefreshVolumeText() {musicValue.Text=$"{preferences.MusicVolume:0}%";soundValue.Text=$"{preferences.SoundVolume:0}%";}
    private void QueueSettingsSave() {settingsDirty=true;settingsDelay=.5;}
    private void PersistSettings()
    {
        if(settingsStore==null)return;
        try {settingsStore.Save(preferences);settingsDirty=false;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) {if(help!=null)help.Text="设置保存失败："+e.Message;}
    }
    private void TickClient(double delta)
    {
        if(pendingWindowSize is {} target){pendingWindowSize=null;if(NativeWindowAvailable){DisplayServer.WindowSetSize(target);GetWindow().MoveToCenter();}}
        if(settingsDirty&&(settingsDelay-=delta)<=0)PersistSettings();
    }
    private void ApplyDisplay()
    {
        if(!NativeWindowAvailable){pendingWindowSize=null;return;}
        if(preferences.Fullscreen){pendingWindowSize=null;DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);return;}
        bool leaving=DisplayServer.WindowGetMode()!=DisplayServer.WindowMode.Windowed;
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var size=new Vector2I(preferences.Width,preferences.Height);
        if(leaving)pendingWindowSize=size;else {DisplayServer.WindowSetSize(size);GetWindow().MoveToCenter();}
    }
    private void WindowResized()
    {
        if(!NativeWindowAvailable||preferences.Fullscreen||DisplayServer.WindowGetMode()!=DisplayServer.WindowMode.Windowed||pendingWindowSize!=null)return;
        var size=DisplayServer.WindowGetSize();preferences.Width=size.X;preferences.Height=size.Y;QueueSettingsSave();
    }
}
