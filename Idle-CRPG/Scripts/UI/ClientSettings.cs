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
    private bool progressReadBlocked;
    private ConfirmationDialog? legacyDialog;
    private LegacySelectionRequiredException? pendingLegacy;
    private AcceptDialog? migrationNotice;
    private string progressBlockedReason="";
    private AccountState pendingAccount=new();
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
        automation=args.Contains("--first-batch-smoke")||args.Contains("--smoke-test")||args.Contains("--capture")||args.Contains("--performance-test")||args.Contains("--legacy-migration-smoke")||args.Contains("--c2a-boss-smoke")||reopen;
        string directory=automation?ProjectSettings.GlobalizePath("res://../artifacts/qa/"+(reopen?"reopen":Guid.NewGuid().ToString("N"))):ProjectSettings.GlobalizePath("user://");
        saveStore=new SaveStore(Path.Combine(directory,"progress_v1.json"));settingsStore=new SettingsStore(Path.Combine(directory,"settings.json"));
        preferences=settingsStore.Load();
        if(args.Contains("--persistence-write"))saveStore.Reset();
        if(args.Contains("--legacy-migration-smoke"))SeedLegacyMigrationFixture();
        GetTree().AutoAcceptQuit=false;
        ApplyDisplay();GetWindow().SizeChanged+=WindowResized;
        InitializeAudio();
    }
    private void ResumeProgress()
    {
        SaveDocument? document;
        try{document=saveStore.Load(config);}
        // C-LEGACY-5：超额旧档不得自动裁剪，先让玩家明确选择本轮最多4名英雄。
        catch(LegacySelectionRequiredException e){ShowLegacyMigration(e);return;}
        catch(MigrationRequiredException e){BlockProgress(e.Message);ShowMigrationBlockedNotice(e.Message);return;}
        pendingAccount=document?.Account.Copy()??new();
        if(document?.Session!=null)
        {
            session=GameSession.Restore(config,document.Session);config=session.Config;OpenCurrentChapter();speed=document.Speed;paused=document.Paused;viewMode=0;selectedRegion=document.SelectedRegion;
            seed.Text=session.Seed.ToString();UpdatePartyControls();ResetSoundBaseline();Refresh();
            savedRevision=session.Revision;
        }
        if(saveStore.LastWarning!=null)help.Text=saveStore.LastWarning;
        // 有限离线结算只在打开存档时发生一次，结果明确告知玩家，且不含自动升级/换装等动作。
        if(saveStore.LastOffline is {Seconds:>0} offline)
        {
            string summary=$"离线 {offline.Seconds/60} 分钟：公共XP +{offline.Experience}、KP +{offline.Kp}、金币 +{offline.Gold}。不会自动升级或换装。";
            help.Text=summary;var notice=new AcceptDialog {Title="离线收益（有限时长）",DialogText=summary};AddChild(notice);notice.PopupCentered(new Vector2I(520,180));
        }
        if(saveStore.RecoveryReset)
        {
            var notice=new AcceptDialog {Title="存档已自动重置",DialogText=saveStore.LastWarning};AddChild(notice);notice.PopupCentered(new Vector2I(560,180));
        }
    }
    /// <summary>需要玩家裁决的旧档/账号状态：保留原档并记录原因，但不永久锁死创角入口（点开始游戏可重开提示）。</summary>
    private void BlockProgress(string reason)
    {
        progressReadBlocked=true;progressBlockedReason=reason;help.Text=reason;
        if(summary!=null)summary.Text="旧档已保留：请先完成迁移提示，或在设置中明确清除所有进度。";
        UpdatePartyControls();
    }
    /// <summary>非迁移类读档阻断的提示框；确认按钮保持在视口内，可重复打开。</summary>
    private void ShowMigrationBlockedNotice(string reason)
    {
        if(migrationNotice!=null&&GodotObject.IsInstanceValid(migrationNotice))migrationNotice.QueueFree();
        var notice=new AcceptDialog {Title="旧档已保留 · 需确认迁移",DialogText=reason+"\n\n点“开始游戏”可再次打开本提示；迁移未完成前不会改写原档。"};
        AddChild(notice);migrationNotice=notice;notice.PopupCentered(new Vector2I(680,240));
    }
    /// <summary>
    /// 旧档超额迁移选择框（C-LEGACY-5）：玩家勾选要留在本轮的英雄（1–4名），其余进入只读迁移隔离区。
    /// 未确认前不写盘；确认后走 SaveStore 的迁移事务，再按正常流程重新恢复进度。
    /// 取消或迁移失败都不清除旧档，也不锁死创角：点“开始游戏”可再次打开本选择框。
    /// </summary>
    private void ShowLegacyMigration(LegacySelectionRequiredException e)
    {
        BlockProgress(e.Message);
        pendingLegacy=e;
        if(legacyDialog!=null&&GodotObject.IsInstanceValid(legacyDialog)){legacyDialog.QueueFree();legacyDialog=null;}
        var dialog=new ConfirmationDialog {Title="旧档迁移 · 选择本轮英雄（最多4名）",OkButtonText="确认迁移",CancelButtonText="稍后处理"};
        // 自动换行标签在弹窗首次布局前会报告极大的最小高度，把窗口撑到视口之外，
        // 确认/取消按钮随之落到屏幕下方无法点击。这里固定窗口尺寸、关闭内容撑开，改用显式换行。
        dialog.WrapControls=false;
        dialog.DialogHideOnOk=false;
        var columns=new VBoxContainer {Name="LegacyColumns",CustomMinimumSize=new Vector2(600,0)};
        columns.AddThemeConstantOverride("separation",6);
        int reserve=Math.Max(0,e.Document.Run?.ReserveHeroes.Count??0);
        columns.AddChild(new Label {Text=$"旧档含 {e.Candidates.Count} 名英雄{(reserve>0?$"、{reserve} 名候补":"")}：新版本轮最多 4 名。\n未确认前不会改动原档；冻结的英雄只读保留，不上阵、\n不能作为候补、不解锁第5席。"});
        var hint=new Label {Text="默认保留前 4 名：勾掉不想带入本轮的英雄，再点“确认迁移”。"};
        columns.AddChild(hint);
        var checks=new System.Collections.Generic.List<(int Index,CheckBox Box)>();
        var ok=dialog.GetOkButton();
        void Sync()
        {
            int count=checks.Count(c=>c.Box.ButtonPressed);
            foreach(var c in checks)c.Box.Disabled=!c.Box.ButtonPressed&&count>=4;
            ok.Disabled=count is <1 or >4;
        }
        foreach(var candidate in e.Candidates)
        {
            bool locked=config.HeroUnlockKills.TryGetValue(candidate.SpecId,out int unlockKills)&&unlockKills>0;
            // 默认勾选前4名，避免确认按钮一打开就是灰态、玩家以为迁移不可用。
            var check=new CheckBox {Text=$"#{candidate.Index+1} {candidate.Name} Lv.{candidate.Level}"+(locked?"（旧档锁定职业，本轮保留）":""),ButtonPressed=candidate.Index<4};
            check.Toggled+=_=>Sync();checks.Add((candidate.Index,check));columns.AddChild(check);
        }
        dialog.AddChild(columns);
        dialog.Confirmed+=()=>{
            var keep=checks.Where(c=>c.Box.ButtonPressed).Select(c=>c.Index).ToArray();
            if(keep.Length is <1 or >4){hint.Text="请选择 1–4 名英雄后再确认迁移。";return;}
            try
            {
                var migrated=saveStore.LoadWithLegacySelection(config,e.Document,keep);
                pendingLegacy=null;progressReadBlocked=false;progressBlockedReason="";
                help.Text=$"已完成迁移：本轮 {migrated.Heroes.Count} 名英雄；冻结 {saveStore.Legacy.Heroes.Count} 名只读保留。";
                dialog.Hide();legacyDialog=null;dialog.QueueFree();
                ResumeProgress();
            }
            // 迁移失败必须保留弹窗与旧档，否则玩家既回不到旧档、也进不了创角。
            catch(Exception error){hint.Text="迁移失败，原档保持不变："+error.Message;help.Text=hint.Text;GD.PushError("LEGACY_MIGRATION_FAILED: "+error);}
        };
        dialog.Canceled+=()=>{
            dialog.Hide();
            help.Text="已暂不迁移：原档与备份保持不变；点“开始游戏”可再次打开迁移选择。";
            if(summary!=null)summary.Text="旧档待迁移：点“开始游戏”选择本轮 1–4 名英雄。";
        };
        AddChild(dialog);legacyDialog=dialog;dialog.PopupCentered(new Vector2I(720,440));
        Sync();
    }
    private void UpdatePartyControls()
    {
        partySetup.Visible=session==null;RefreshHud();RefreshCastleInteractions();
        // 阻断状态不再禁用开始按钮：按钮始终可点，点击后重新打开迁移提示，避免出现无出口的死锁。
        start.Visible=session==null;start.Disabled=false;seed.Editable=session==null;
        for(int i=0;i<choices.Length;i++)
        {
            choices[i].Disabled=session!=null||config.HeroUnlockKills[config.Heroes[i].Id]>0;
            partyCounts[i].Editable=!choices[i].Disabled;
            if(session!=null)choices[i].SetPressedNoSignal(session.Heroes.Any(h=>h.SpecId==config.Heroes[i].Id));
        }
        pause.Text=paused?"继续":"暂停";rateButton.Text=$"速度 ×{speed}";
    }
    /// <summary>只按持久状态变更保存；命中、走路和计时不产生保存请求，无周期战斗快照。</summary>
    private void AutoSave(double delta)
    {
        if(PreviewOpen)return;
        if(progressWrite!=null)
        {
            if(!progressWrite.IsCompleted)return;
            FinishProgressWrite();
        }
        if(session==null)return;sinceSave+=delta;
        if(forceProgressSave||(session.Revision!=savedRevision&&sinceSave>=config.AutosaveMinSeconds))
        {
            // 主线程只复制已分离引用的检查点，编码、Flush 和原子替换都在后台串行完成。
            var document=saveStore.PreserveLegacy(new SaveDocument {Session=session.CaptureProgress(),Speed=speed,Paused=paused,ViewMode=viewMode,SelectedRegion=SavedSelectedRegion});
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
        if(PreviewOpen){if(wait)CloseSkillPreview();else return;}
        if(!wait){forceProgressSave=true;AutoSave(0);return;}
        FinishProgressWrite();
        if(session==null||saveStore==null)return;
        try {saveStore.Save(session,speed,paused,viewMode,SavedSelectedRegion);savedRevision=session.Revision;sinceSave=0;forceProgressSave=false;if(saveStatus!=null)saveStatus.Text="进度已自动保存";}
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
        var abandon=new Button {Text="放弃本轮并重新选队（保留账号）",CustomMinimumSize=new Vector2(0,42)};
        abandon.Pressed+=()=>{if(session==null)return;var dialog=new ConfirmationDialog {Title="确认放弃本轮",DialogText="清除本轮等级、XP、KP、星图和地图进度；账号永久状态保留，不发重生奖励。"};AddChild(dialog);dialog.Confirmed+=AbandonCurrentRun;dialog.PopupCentered(new Vector2I(580,210));};box.AddChild(abandon);
        // WP7：正式重生与放弃本轮是不同命令，只结算已批准的账号事实。
        var rebirth=new Button {Text="正式重生（需完成第4章主线）",CustomMinimumSize=new Vector2(0,42)};
        rebirth.Pressed+=()=>{
            if(session==null)return;
            if(!session.CanRebirth(out string why)){help.Text="暂不能重生："+why;return;}
            var dialog=new ConfirmationDialog {Title="确认正式重生",DialogText="本轮英雄、等级、XP、KP、金币、星图、装备、卷轴、药剂与地图进度将清零；\n账号通关事实与轮次序号保留，随后重新选队。此操作不可撤销。"};
            AddChild(dialog);
            dialog.Confirmed+=()=>{
                if(!session!.Rebirth(out IdleCrpg.Core.AccountState account,out string reason)){help.Text="重生失败："+reason;return;}
                FinishProgressWrite();saveStore.PreserveLegacy(new SaveDocument {Session=session.CaptureProgress()});
                saveStore.WriteEncoded(SaveStore.Encode(saveStore.PreserveLegacy(new SaveDocument {Account=account})));
                session=null;pendingAccount=account;UpdatePartyControls();settingsRoot.Hide();
                help.Text="已正式重生：账号权益保留，请重新选队开启新的一轮。";
            };
            dialog.PopupCentered(new Vector2I(620,230));
        };box.AddChild(rebirth);
        var reset=new Button {Text="清除所有进度",CustomMinimumSize=new Vector2(0,42)};reset.AddThemeColorOverride("font_color",new Color("e89a91"));reset.Pressed+=RequestReset;box.AddChild(reset);
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
        pendingAccount=new();progressReadBlocked=false;progressBlockedReason="";pendingLegacy=null;
        // 清除全部进度是唯一销毁冻结旧档的入口，提示框一并关闭，避免留下过期引用。
        if(legacyDialog!=null&&GodotObject.IsInstanceValid(legacyDialog))legacyDialog.QueueFree();
        if(migrationNotice!=null&&GodotObject.IsInstanceValid(migrationNotice))migrationNotice.QueueFree();
        legacyDialog=null;migrationNotice=null;
        session=null;formationRoot.Hide();talentEditor.Hide();foreach(var choice in choices)choice.SetPressedNoSignal(false);choices[0].SetPressedNoSignal(true);paused=false;speed=1;accumulator=0;sinceSave=0;savedRevision=-1;viewMode=0;selectedRegion=-1;
        for(int i=0;i<partyCounts.Length;i++)partyCounts[i].Value=i==0?1:0;
        UpdatePartyControls();ResetSoundBaseline();settingsRoot.Hide();
        summary.Text="进度已重置，请选择一位英雄开始远征。";roster.Text="";journal.Text="";help.Text="世界无遭遇战 · 通关后必先回村交易 · 无新目标时自动复刷";PlaySound("sfx_reward");QueueRedraw();
    }
    private void AbandonCurrentRun()
    {
        if(session==null)return;FinishProgressWrite();
        var account=session.AbandonRun();
        try{saveStore.PreserveLegacy(new SaveDocument {Session=session.CaptureProgress()});saveStore.WriteEncoded(SaveStore.Encode(saveStore.PreserveLegacy(new SaveDocument {Account=account,Run=null})));}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){help.Text="放弃失败，原轮次保留："+e.Message;return;}
        pendingAccount=account;session=null;forceProgressSave=false;savedRevision=-1;viewMode=0;paused=false;accumulator=0;
        config=config.ChapterMaps[0];OpenCurrentChapter();formationRoot.Hide();talentEditor.Hide();settingsRoot.Hide();
        for(int i=0;i<partyCounts.Length;i++)partyCounts[i].Value=i==0?1:0;
        UpdatePartyControls();ResetSoundBaseline();Refresh();help.Text="已放弃本轮，账号保留，请重新选队。";
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
