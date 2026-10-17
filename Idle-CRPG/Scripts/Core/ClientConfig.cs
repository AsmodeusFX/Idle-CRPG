using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record AudioSpec(string Id,string Path,string Kind,float VolumeDb,float MinGap);
public record WindowPreset(int Width,int Height);
public sealed partial class Config
{
    public List<AudioSpec> Audio=new();
    public List<WindowPreset> Windows=new();
    public float AutosaveMinSeconds,CheckpointSeconds;
    private void LoadClientConfig(string directory)
    {
        Audio=Read(Path.Combine(directory,"audio/audio.csv")).Select(r=>new AudioSpec(r["id"],r["path"],r["kind"],Number(r,"volume_db"),Number(r,"min_gap"))).ToList();
        Windows=Read(Path.Combine(directory,"system/window.csv")).Select(r=>new WindowPreset(Integer(r,"width"),Integer(r,"height"))).ToList();
        var rows=Read(Path.Combine(directory,"system/client.csv"));if(rows.Count!=1)throw new InvalidDataException("client.csv 必须一行");
        AutosaveMinSeconds=Number(rows[0],"autosave_min_seconds");CheckpointSeconds=Number(rows[0],"checkpoint_seconds");
        if(Audio.Select(a=>a.Id).Distinct().Count()!=Audio.Count||Audio.Any(a=>!a.Path.StartsWith("res://Assets/Audio/")||a.Kind is not ("music" or "sfx")||a.MinGap<0)||Audio.Count(a=>a.Kind=="music")!=1||Windows.Count==0||Windows.Any(w=>w.Width<800||w.Height<500)||AutosaveMinSeconds<=0||CheckpointSeconds<AutosaveMinSeconds)throw new InvalidDataException("音频/窗口/自动保存配置无效");
    }
}
