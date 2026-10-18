using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleCrpg.Core;

public sealed class SaveDocument
{
    [JsonRequired] public int Version=3;
    [JsonRequired] public SessionSnapshot? Session;
    public int Speed=1,ViewMode,SelectedRegion=-1;
    public bool Paused;
}

/// <summary>版本化 JSON，临时文件落盘后原子替换；主档损坏时只从有效备份恢复。</summary>
public sealed class SaveStore(string path)
{
    private bool protectBackup;
    public static readonly JsonSerializerOptions JsonOptions=new() {IncludeFields=true,IgnoreReadOnlyProperties=true,WriteIndented=false};
    public string PathName=>path;
    public string? LastWarning { get; private set; }
    public static string Encode(SaveDocument document)=>JsonSerializer.Serialize(document,JsonOptions);
    public static SaveDocument Decode(Config c,string text)
    {
        var document=JsonSerializer.Deserialize<SaveDocument>(text,JsonOptions)??throw new InvalidDataException("空存档");
        if(document.Version is not (1 or 2 or 3))throw new InvalidDataException("不支持的存档版本 "+document.Version);
        int chapter=document.Session?.ChapterIndex??0;
        if(chapter<0||chapter>=c.ChapterMaps.Count)throw new InvalidDataException("存档章节参数错误");
        var map=c.ChapterMaps[chapter];
        if(document.Speed is not (1 or 2 or 4)||document.ViewMode<0||document.ViewMode>3||(document.SelectedRegion!=-1&&!map.Regions.Exists(r=>r.Id==document.SelectedRegion)))throw new InvalidDataException("存档界面参数错误");
        if(document.Session!=null)GameSession.Restore(c,document.Session);
        return document;
    }
    public SaveDocument? Load(Config c)
    {
        LastWarning=null;
        foreach(var candidate in new[]{path,path+".bak"})
        {
            if(!File.Exists(candidate))continue;
            try
            {
                var result=Decode(c,File.ReadAllText(candidate,Encoding.UTF8));
                if(candidate!=path){LastWarning="主存档无法读取，已恢复备份进度";protectBackup=true;}
                return result;
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NullReferenceException or OverflowException)
            {LastWarning="存档读取失败，原文件保留："+e.Message;}
        }
        return null;
    }
    public void Save(GameSession game,int speed=1,bool paused=false,int viewMode=0,int selectedRegion=-1)
    {
        WriteEncoded(Encode(new SaveDocument {Session=game.CaptureProgress(),Speed=speed,Paused=paused,ViewMode=viewMode,SelectedRegion=selectedRegion}));
    }
    /// <summary>写入已在逻辑线程序列化的不可变内容；调用方必须串行写入，并在重置/退出前等待完成。</summary>
    public void WriteEncoded(string content)
    {
        AtomicWrite(path,content,!protectBackup);
        protectBackup=false;
    }

    /// <summary>确认重置后写入空进度，同时覆盖备份，避免旧进度因备份恢复而重新出现；设置独立保存。</summary>
    public void Reset()
    {
        string empty=Encode(new SaveDocument());AtomicWrite(path,empty);
        AtomicWrite(path+".bak",empty,false);
        protectBackup=false;
    }
    internal static void AtomicWrite(string path,string text,bool backup=true)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        string temp=path+".tmp";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        {
            byte[] bytes=new UTF8Encoding(false).GetBytes(text);file.Write(bytes);file.Flush(true);
        }
        if(File.Exists(path))File.Replace(temp,path,backup?path+".bak":null);
        else File.Move(temp,path);
    }
}

public sealed class AppSettings
{
    public int Version=1,Width=1440,Height=900;
    public bool Fullscreen;
    public float MusicVolume=30,SoundVolume=60;
    public void Sanitize()
    {
        Width=Math.Clamp(Width,800,7680);Height=Math.Clamp(Height,500,4320);
        MusicVolume=float.IsFinite(MusicVolume)?Math.Clamp(MusicVolume,0,100):30;
        SoundVolume=float.IsFinite(SoundVolume)?Math.Clamp(SoundVolume,0,100):60;
    }
}
/// <summary>显示与音频偏好和游戏进度分文件存放；重置进度不改变设置。</summary>
public sealed class SettingsStore(string path)
{
    public AppSettings Load()
    {
        try
        {
            if(!File.Exists(path))return new AppSettings();
            var settings=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path,Encoding.UTF8),SaveStore.JsonOptions)??new AppSettings();
            if(settings.Version!=1)return new AppSettings();settings.Sanitize();return settings;
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {return new AppSettings();}
    }
    public void Save(AppSettings settings) {settings.Sanitize();SaveStore.AtomicWrite(path,JsonSerializer.Serialize(settings,SaveStore.JsonOptions));}
}
