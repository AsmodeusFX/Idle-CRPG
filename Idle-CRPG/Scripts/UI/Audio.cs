using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    // 复用 Idle-Sword 的程序生成占位音及总线/轮转限频设计；不从音频回写游戏状态。
    private readonly Dictionary<string,AudioStream> audioStreams=new();
    private readonly Dictionary<string,double> lastSound=new();
    private AudioStreamPlayer[] soundPlayers=Array.Empty<AudioStreamPlayer>();
    private AudioStreamPlayer musicPlayer=null!;
    private int soundCursor,soundPlays;
    private string? audioError;
    private GameSession? soundSession;
    private long lastHits;
    private int lastKills,lastLevel,lastRetries,lastTrades,lastCleared,lastAlive;
    private bool audioReleased;

    private void InitializeAudio()
    {
        foreach(string bus in new[]{"Music","SFX"})
            if(AudioServer.GetBusIndex(bus)<0){AudioServer.AddBus();AudioServer.SetBusName(AudioServer.BusCount-1,bus);}
        ApplyAudio();
        foreach(var spec in config.Audio)
        {
            var stream=GD.Load<AudioStream>(spec.Path);
            if(stream==null){audioError="缺少音频资源："+spec.Path;continue;}
            audioStreams[spec.Id]=stream;
        }
        soundPlayers=Enumerable.Range(0,8).Select(_=>new AudioStreamPlayer {Bus="SFX"}).ToArray();foreach(var p in soundPlayers)AddChild(p);
        musicPlayer=new AudioStreamPlayer {Bus="Music"};AddChild(musicPlayer);
        var bgm=config.Audio.Single(a=>a.Kind=="music");
        if(audioStreams.TryGetValue(bgm.Id,out var music))
        {
            if(music is AudioStreamWav wav)
            {
                // GetLength 对 PCM 与导入后的压缩 WAV 都适用；不能把压缩数据字节数当采样数。
                int frames=Math.Max(1,(int)Math.Round(wav.GetLength()*wav.MixRate));
                wav.LoopMode=AudioStreamWav.LoopModeEnum.Forward;wav.LoopBegin=0;wav.LoopEnd=frames;
            }
            musicPlayer.Stream=music;musicPlayer.VolumeDb=bgm.VolumeDb;musicPlayer.Play();
        }
        if(audioError!=null)GD.PushWarning(audioError);
    }
    private void ApplyAudio()
    {
        foreach(var pair in new[]{("Music",preferences.MusicVolume),("SFX",preferences.SoundVolume)})
        {
            int index=AudioServer.GetBusIndex(pair.Item1);if(index<0)continue;
            AudioServer.SetBusMute(index,pair.Item2<=0);AudioServer.SetBusVolumeDb(index,pair.Item2<=0?-80:Mathf.LinearToDb(pair.Item2/100));
        }
    }
    private void PlaySound(string id)
    {
        if(audioReleased||soundPlayers.Length==0||!audioStreams.TryGetValue(id,out var stream))return;
        var spec=config.Audio.Single(a=>a.Id==id);double now=Time.GetTicksMsec()/1000.0;
        if(now-lastSound.GetValueOrDefault(id,double.NegativeInfinity)<spec.MinGap)return;
        lastSound[id]=now;
        var player=soundPlayers[soundCursor];soundCursor=(soundCursor+1)%soundPlayers.Length;player.Stream=stream;player.VolumeDb=spec.VolumeDb;player.Play();soundPlays++;
    }
    private void ResetSoundBaseline()
    {
        soundSession=session;
        if(session==null)return;
        lastHits=session.HitCount;lastKills=session.Kills;lastLevel=session.Heroes.Sum(h=>h.Level);lastRetries=session.Retries;
        lastTrades=session.World?.Trades??0;lastCleared=session.World?.Progress.Cleared.Count??0;lastAlive=session.Heroes.Count(h=>h.Alive);
    }
    private void TrackSounds()
    {
        if(!ReferenceEquals(soundSession,session)){ResetSoundBaseline();return;}if(session==null)return;
        if(session.HitCount>lastHits)PlaySound("sfx_hit");
        if(session.Kills>lastKills)PlaySound("sfx_kill");
        if(session.Heroes.Sum(h=>h.Level)>lastLevel)PlaySound("sfx_breakthrough");
        if(session.Retries>lastRetries)PlaySound("sfx_deny");
        if((session.World?.Trades??0)>lastTrades)PlaySound("sfx_buy");
        if((session.World?.Progress.Cleared.Count??0)>lastCleared)PlaySound("sfx_reward");
        if(session.Heroes.Count(h=>h.Alive)>lastAlive)PlaySound("sfx_cast_buff");
        ResetSoundBaseline();
    }
    private void ReleaseAudio()
    {
        if(audioReleased)return;audioReleased=true;
        if(musicPlayer!=null){musicPlayer.Stop();musicPlayer.Stream=null;}
        foreach(var player in soundPlayers){player.Stop();player.Stream=null;}
        foreach(var stream in audioStreams.Values)stream.Dispose();audioStreams.Clear();
    }
}
