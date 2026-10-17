using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;

public partial class Main
{
    private bool displaySmokeActive;
    private void RunSceneSmoke()
    {
        try
        {
            if(session==null||viewMode==1||!castleButton.IsInsideTree())throw new Exception("默认视图或城堡控件不正确");
            for(int i=0;i<5000;i++)
            {
                if(session.World!.Stage==CampaignStage.Dungeon&&!session.MovingBetweenRooms&&session.Floor.Rooms[session.CurrentRoom].Monsters.Exists(m=>m.Alive))break;
                session.Tick(.05f);
            }
            for(int i=0;i<100;i++)UpdateCamera(.1);
            var room=session.Floor.Rooms[session.CurrentRoom];var anchor=cameraCenter;float scale=cameraZoom;
            if(System.Numerics.Vector2.Distance(anchor,room.Center)>.001f)throw new Exception("战斗镜头没有锁定房间中心");
            for(int i=0;i<4;i++){session.Tick(.05f);UpdateCamera(.1);}
            if(!session.MovingBetweenRooms&&(System.Numerics.Vector2.Distance(anchor,cameraCenter)>.001f||Math.Abs(scale-cameraZoom)>.001f))throw new Exception("战斗镜头随角色漂移");
            if((room.HalfWidth+room.HalfHeight)*2*scale>931)throw new Exception("完整房间未适配战斗视口");
            cameraCenter=room.Center+new System.Numerics.Vector2(3,0);cameraVelocity=System.Numerics.Vector2.Zero;
            var before=cameraCenter;UpdateCamera(1.0/60);
            if(System.Numerics.Vector2.Distance(before,cameraCenter)>.05f)throw new Exception("进房镜头起步突跳");
            for(int i=0;i<180;i++)UpdateCamera(1.0/60);
            if(System.Numerics.Vector2.Distance(cameraCenter,room.Center)>.001f)throw new Exception("镜头未平滑收敛到房间");
            GD.Print("SCENE_SMOKE_OK: default party view, room lock, complete room framing, clipped renderer");
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
    private void RunReopenSmoke()
    {
        try
        {
            if(OS.GetCmdlineUserArgs().Contains("--persistence-write"))
            {
                Start();for(int i=0;i<800;i++)session!.Tick(.05f);
                speed=2;paused=true;viewMode=1;selectedRegion=1;SaveNow(true);
                preferences.MusicVolume=35;PersistSettings();
                GD.Print("PERSISTENCE_WRITE_OK");
            }
            else
            {
                var saved=saveStore.Load(config);
                if(session==null||saved?.Session==null||!paused||speed!=2||start.Visible||seed.Editable||preferences.MusicVolume!=35)throw new Exception("重新启动未自动恢复界面/偏好");
                string expected=SaveStore.Encode(new SaveDocument {Session=GameSession.Restore(config,saved.Session).CaptureProgress()});
                string actual=SaveStore.Encode(new SaveDocument {Session=session.CaptureProgress()});
                if(expected!=actual)throw new Exception("重新启动恢复状态不一致");
                GD.Print("PERSISTENCE_READ_OK: automatic startup restore matches saved snapshot");
            }
            QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
    private async void RunDisplaySmoke()
    {
        if(!NativeWindowAvailable)
        {
            int width=preferences.Width,height=preferences.Height;bool fullscreen=preferences.Fullscreen;
            pendingWindowSize=new Vector2I(960,600);ApplyDisplay();WindowResized();TickClient(0);
            RefreshDisplayControls();
            resolutionBox.EmitSignal(OptionButton.SignalName.ItemSelected,0L);
            fullscreenBox.EmitSignal(BaseButton.SignalName.Toggled,!fullscreen);
            if(pendingWindowSize!=null||preferences.Width!=width||preferences.Height!=height||preferences.Fullscreen!=fullscreen||!resolutionBox.Disabled||!fullscreenBox.Disabled)
            {GD.PushError("DISPLAY_MANAGED_SMOKE_FAILED");QuitSafely(1);return;}
            GD.Print("DISPLAY_MANAGED_SMOKE_OK: native calls skipped, controls disabled, preferences preserved");
            return;
        }
        displaySmokeActive=true;
        try
        {
            int index=config.Windows.FindIndex(p=>p.Width==960&&p.Height==600);
            resolutionBox.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);
            await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);
            if(DisplayServer.WindowGetSize()!=new Vector2I(960,600))throw new Exception("窗口尺寸选项未生效");
            fullscreenBox.ButtonPressed=true;await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);
            if(DisplayServer.WindowGetMode()!=DisplayServer.WindowMode.Fullscreen)throw new Exception("全屏选项未生效");
            fullscreenBox.ButtonPressed=false;await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);
            if(DisplayServer.WindowGetSize()!=new Vector2I(960,600)||resolutionBox.Disabled)throw new Exception("退出全屏未恢复窗口尺寸");
            GD.Print("DISPLAY_SMOKE_OK: preset size, fullscreen, restored window size");
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
        finally{displaySmokeActive=false;}
    }
    /// <summary>走真实设置控件、存储与重置回调；所有自动检查都使用独立 QA 存档，不碰玩家进度。</summary>
    private void RunClientSmoke()
    {
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("CLIENT_SMOKE: "+message);}
        try
        {
            Check(audioError==null&&audioStreams.Count==config.Audio.Count,"音频加载");
            Check(musicPlayer.Stream is AudioStreamWav {LoopEnd:>0,LoopMode:AudioStreamWav.LoopModeEnum.Forward},"音乐循环");
            var music=(AudioStreamWav)musicPlayer.Stream!;
            Check(Math.Abs(music.LoopEnd-music.GetLength()*music.MixRate)<=1,"音乐循环没有覆盖完整音源");
            ToggleSettings();Check(settingsRoot.Visible,"设置按钮入口");
            musicSlider.Value=35;soundSlider.Value=0;PersistSettings();
            var read=settingsStore.Load();Check(read.MusicVolume==35&&read.SoundVolume==0,"音量持久化");
            Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("SFX")),"静音总线");
            soundSlider.Value=60;Check(!AudioServer.IsBusMute(AudioServer.GetBusIndex("SFX")),"恢复音效");
            SaveNow(true);var loaded=saveStore.Load(config);Check(loaded?.Session!=null,"自动保存");
            session!.SetSpawnModifiers(session.SpawnModifiers);AutoSave(config.AutosaveMinSeconds+1);
            Check(progressWrite!=null,"关键状态未安排后台存档");FinishProgressWrite();
            AutoSave(config.CheckpointSeconds+1);Check(progressWrite==null,"无状态变化仍周期写盘");
            var original=session;RequestReset();resetConfirmation.Hide();Check(ReferenceEquals(session,original)&&saveStore.Load(config)?.Session!=null,"取消重置改变进度");
            session.SetSpawnModifiers(session.SpawnModifiers);AutoSave(config.AutosaveMinSeconds+1);
            ResetProgress();Check(session==null&&saveStore.Load(config)?.Session==null&&start.Visible,"确认重置未清除进度/后台写盘复活旧档");
            Check(settingsStore.Load().MusicVolume==35,"重置清除了音量设置");
            Start();Check(session!=null&&!start.Visible,"重置后重新选队开局");
            SaveNow(true);int plays=soundPlays;lastSound.Remove("sfx_click");PlaySound("sfx_click");Check(soundPlays==plays+1,"音效播放");
            GD.Print("CLIENT_SMOKE_OK: settings, audio, save/load, cancel/confirm reset, new expedition");
        }
        catch(Exception e) {GD.PushError(e.ToString());QuitSafely(1);}
    }
}
