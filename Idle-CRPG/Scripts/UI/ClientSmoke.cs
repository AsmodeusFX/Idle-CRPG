using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;

public partial class Main
{
    private bool displaySmokeActive;
    private void RunReopenSmoke()
    {
        try
        {
            if(OS.GetCmdlineUserArgs().Contains("--persistence-write"))
            {
                Start();for(int i=0;i<800;i++)session!.Tick(.05f);
                speed=2;paused=true;viewMode=1;selectedRegion=1;SaveNow();
                preferences.MusicVolume=35;PersistSettings();
                GD.Print("PERSISTENCE_WRITE_OK");
            }
            else
            {
                var saved=saveStore.Load(config);
                if(session==null||saved?.Session==null||!paused||speed!=2||start.Visible||seed.Editable||preferences.MusicVolume!=35)throw new Exception("重新启动未自动恢复界面/偏好");
                string expected=SaveStore.Encode(new SaveDocument {Session=saved.Session});
                string actual=SaveStore.Encode(new SaveDocument {Session=session.Capture()});
                if(expected!=actual)throw new Exception("重新启动恢复状态不一致");
                GD.Print("PERSISTENCE_READ_OK: automatic startup restore matches saved snapshot");
            }
            QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
    private async void RunDisplaySmoke()
    {
        if(DisplayServer.GetName()=="headless")return;
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
            SaveNow();var loaded=saveStore.Load(config);Check(loaded?.Session!=null,"自动保存");
            var original=session;RequestReset();resetConfirmation.Hide();Check(ReferenceEquals(session,original)&&saveStore.Load(config)?.Session!=null,"取消重置改变进度");
            ResetProgress();Check(session==null&&saveStore.Load(config)?.Session==null&&start.Visible,"确认重置未清除进度");
            Check(settingsStore.Load().MusicVolume==35,"重置清除了音量设置");
            Start();Check(session!=null&&!start.Visible,"重置后重新选队开局");
            SaveNow();int plays=soundPlays;lastSound.Remove("sfx_click");PlaySound("sfx_click");Check(soundPlays==plays+1,"音效播放");
            GD.Print("CLIENT_SMOKE_OK: settings, audio, save/load, cancel/confirm reset, new expedition");
        }
        catch(Exception e) {GD.PushError(e.ToString());QuitSafely(1);}
    }
}
