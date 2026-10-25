using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleCrpg.Core;

public sealed class SaveDocument
{
    [JsonIgnore] public int MigratedFromVersion;
    [JsonRequired] public int Version=5;
    public AccountState Account=new();
    public SessionSnapshot? Run;
    /// <summary>迁移隔离区：跨本轮存续，普通放弃/重生不清除，只有"清除全部进度"会清除。</summary>
    public LegacyQuarantine Legacy=new();
    /// <summary>存档时刻（Unix 秒）；0 表示旧档无时间戳，不做离线结算。</summary>
    public long SavedAtUnix;
    /// <summary>仅供工程调用兼容；JSON中的本轮权威只有Run。</summary>
    [JsonIgnore] public SessionSnapshot? Session {get=>Run;set {Run=value;if(value!=null)Account=value.Account.Copy();}}
    public int Speed=1,ViewMode,SelectedRegion=-1;
    public bool Paused;
}

/// <summary>版本化 JSON，临时文件落盘后原子替换；主档损坏时只从有效备份恢复。</summary>
public sealed class SaveStore(string path)
{
    private bool protectBackup;
    private string? migrationSource;
    /// <summary>本文件的迁移隔离区：读取后驻留，后续任何一次写盘都原样保留，直到明确重置。</summary>
    private LegacyQuarantine legacy=new();
    public static readonly JsonSerializerOptions JsonOptions=new() {IncludeFields=true,IgnoreReadOnlyProperties=true,WriteIndented=false};
    public string PathName=>path;
    public string? LastWarning { get; private set; }
    /// <summary>最近一次读档触发的离线结算（秒数与入账额），供界面说明与审计。</summary>
    public OfflineSettlement? LastOffline { get; private set; }
    public bool RecoveryReset {get;private set;}
    public static string Encode(SaveDocument document)
    {
        var node=JsonSerializer.SerializeToNode(document,JsonOptions)!.AsObject();
        if(document.Version<=4){var run=node["Run"]?.DeepClone();node.Remove("Run");node.Remove("Account");node["Session"]=run;}
        return node.ToJsonString(JsonOptions);
    }
    public static SaveDocument Decode(Config c,string text)
    {
        var document=JsonSerializer.Deserialize<SaveDocument>(text,JsonOptions)??throw new InvalidDataException("空存档");
        if(document.Version is not (1 or 2 or 3 or 4 or 5))throw new InvalidDataException("不支持的存档版本 "+document.Version);
        using(var json=JsonDocument.Parse(text))
        {
            if(document.Version<=4)
            {
                document.MigratedFromVersion=document.Version;
                if(!json.RootElement.TryGetProperty("Session",out var legacy))throw new InvalidDataException("旧档缺少Session");
                document.Run=legacy.ValueKind==JsonValueKind.Null?null:JsonSerializer.Deserialize<SessionSnapshot>(legacy.GetRawText(),JsonOptions);
                document.Account=new();document.Version=5;
            }
            else if(!json.RootElement.TryGetProperty("Run",out _)||!json.RootElement.TryGetProperty("Account",out _))throw new InvalidDataException("V5存档缺少Account或Run");
        }
        document.Account.Validate();if(document.Run!=null)document.Run.Account=document.Account.Copy();
        // C-LEGACY-5：旧档超出4席或含候补时不得自动裁剪，也不得静默清档；交由玩家选择后走迁移事务。
        if(document.Run!=null)
        {
            var run=document.Run;
            if(run.Heroes.Count>4||run.ReserveHeroes.Count>0)
            {
                if(run.RunVersion!=0)throw new InvalidDataException("本轮阵容超出4席或含候补，存档无效");
                throw new LegacySelectionRequiredException(document,"旧档含超额英雄或候补：请先确认本轮最多 4 名英雄。原档与备份保持不变，未做出选择前不会写入任何改动。");
            }
        }
        document.Legacy.Validate();
        int chapter=document.Session?.ChapterIndex??0;
        if(chapter<0||chapter>=c.ChapterMaps.Count)throw new InvalidDataException("存档章节参数错误");
        var map=c.ChapterMaps[chapter];
        if(document.Speed is not (1 or 2 or 4)||document.ViewMode<0||document.ViewMode>4||(document.SelectedRegion!=-1&&!map.Regions.Exists(r=>r.Id==document.SelectedRegion)))throw new InvalidDataException("存档界面参数错误");
        if(document.Session!=null)GameSession.Restore(c,document.Session);
        return document;
    }
    public SaveDocument? Load(Config c)
    {
        LastWarning=null;RecoveryReset=false;LastOffline=null;bool existed=File.Exists(path)||File.Exists(path+".bak");
        foreach(var candidate in new[]{path,path+".bak"})
        {
            if(!File.Exists(candidate))continue;
            try
            {
                var result=Decode(c,File.ReadAllText(candidate,Encoding.UTF8));
                if(result.MigratedFromVersion>0)
                {
                    // 真实样本先在内存走一次V5往返，再允许未来写盘；首次写入前保留独立旧版本副本。
                    _=Decode(c,Encode(result));migrationSource=candidate;
                }
                if(candidate!=path){LastWarning="主存档无法读取，已恢复备份进度";protectBackup=true;}
                legacy=result.Legacy.Copy();
                // 有限离线结算：以存档时间为锚点、按上限封顶，只结算 XP/KP/金币，并立即回写防重复领取。
                if(result.Run!=null&&result.SavedAtUnix>0)
                {
                    long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var report=GameSession.SettleOffline(c,result.Run,now);
                    bool settled=report.Seconds>0;
                    result.SavedAtUnix=now;
                    if(settled){LastOffline=report;WriteEncoded(Encode(result));}
                }
                return result;
            }
            // 配置删除导致的非法技能/天赋引用也进入备份恢复和自动重置，不能让旧账号启动时直接抛错。
            catch(MigrationRequiredException){throw;}
            catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NullReferenceException or OverflowException)
            {LastWarning="存档读取失败，原文件保留："+e.Message;}
        }
        if(existed)
        {
            throw new MigrationRequiredException("存档及备份无法恢复，原件保持不变。请确认迁移方案或明确清除所有进度。"+LastWarning);
        }
        return null;
    }
    public void Save(GameSession game,int speed=1,bool paused=false,int viewMode=0,int selectedRegion=-1)
    {
        var snapshot=game.CaptureProgress();snapshot.SavedAtUnix=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        WriteEncoded(Encode(new SaveDocument {Session=snapshot,Legacy=legacy.Copy(),Speed=speed,Paused=paused,ViewMode=viewMode,SelectedRegion=selectedRegion}));
    }
    /// <summary>当前文件的迁移隔离区副本（只读用途：UI 说明与回归断言）。</summary>
    public LegacyQuarantine Legacy=>legacy.Copy();
    /// <summary>
    /// 旧档超额英雄迁移事务（C-LEGACY-5）：玩家选定的最多4名留在本轮，其余与候补进入迁移隔离区。
    /// 可重复执行：重复调用不会重复冻结同一实例，也不会重复写入同一批冻结英雄。
    /// </summary>
    public GameSession LoadWithLegacySelection(Config c,SaveDocument document,IReadOnlyList<int> keep)
    {
        var run=document.Run??throw new InvalidDataException("没有可迁移的本轮状态");
        if(keep.Count is <1 or >4||keep.Distinct().Count()!=keep.Count||keep.Any(i=>i<0||i>=run.Heroes.Count))
            throw new ArgumentException("必须选择 1–4 名不同的本轮英雄");
        string runId=$"legacy-{run.Seed}-{run.DungeonSeed}";
        // 冻结ID按"来源槽位"确定性生成：同一旧档重复执行不会重复冻结，不同选择也不会互相覆盖。
        var overflow=new List<(Actor Hero,string Id)>();
        for(int i=0;i<run.Heroes.Count;i++)if(!keep.Contains(i))overflow.Add((run.Heroes[i],$"{runId}:frozen:{i+1}"));
        for(int i=0;i<run.ReserveHeroes.Count;i++)overflow.Add((run.ReserveHeroes[i],$"{runId}:frozen:reserve:{i+1}"));
        foreach(var (hero,id) in overflow)
        {
            if(string.IsNullOrWhiteSpace(hero.HeroInstanceId))hero.HeroInstanceId=id;
            if(document.Legacy.Heroes.Any(h=>h.HeroInstanceId==hero.HeroInstanceId))continue;
            document.Legacy.Heroes.Add(hero.Copy());
        }
        if(overflow.Count>0)
        {
            string note=$"冻结 {overflow.Count} 名旧档英雄：只读保留，不上阵、不参与战斗/天赋/装备，不能作为普通候补调换，也不解锁第5席；只有“清除全部进度”会删除。";
            if(!document.Legacy.Notes.Contains(note))document.Legacy.Notes.Add(note);
        }
        run.Heroes=keep.Select(i=>run.Heroes[i]).ToList();
        run.ReserveHeroes.Clear();run.PendingFormation=Array.Empty<string>();
        run.TeamLocked=true;run.FormationCapacityFloor=4;run.RunVersion=0;
        // 队伍成员变化后，旧战场的进房节点/召唤属主/救援索引按旧人数编码，必须整段作废，
        // 让读档按现有"房间重开"规则从进房位置重建，避免新旧人数混用产生非法引用。
        run.RoomEntry=new();
        run.Summons.Clear();run.RoomEntrySummons.Clear();
        run.ReviveTarget=-1;run.Rescuer=-1;run.ReviveElapsed=0;
        run.DoorOpenerHero=-1;run.DoorOpenerSummon=-1;run.DoorWait=0;run.DoorFallback=false;
        run.SkillState=new();
        document.Legacy.Validate();
        var session=GameSession.Restore(c,run);
        legacy=document.Legacy.Copy();
        Save(session);
        return session;
    }
    /// <summary>写入已在逻辑线程序列化的不可变内容；调用方必须串行写入，并在重置/退出前等待完成。</summary>
    public void WriteEncoded(string content)
    {
        if(migrationSource!=null&&File.Exists(migrationSource))
        {
            byte[] original=File.ReadAllBytes(migrationSource);string fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original));
            string backup=path+".pre-v5-"+fingerprint;
            if(!File.Exists(backup))File.WriteAllBytes(backup,original);
        }
        AtomicWrite(path,content,!protectBackup);
        protectBackup=false;migrationSource=null;
    }

    /// <summary>确认重置后写入空进度，同时覆盖备份，避免旧进度因备份恢复而重新出现；设置独立保存。</summary>
    public void Reset()
    {
        // 清除全部进度：连同迁移隔离区一起清除（唯一允许销毁冻结旧档数据的入口）。
        string empty=Encode(new SaveDocument());AtomicWrite(path,empty);
        AtomicWrite(path+".bak",empty,false);
        protectBackup=false;
        migrationSource=null;
        legacy=new();
    }
    internal static void AtomicWrite(string path,string text,bool backup=true)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        string temp=path+".tmp";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        {
            byte[] bytes=new UTF8Encoding(false).GetBytes(text);file.Write(bytes);file.Flush(true);
        }
        if(File.Exists(path))
        {
            // Windows 的短暂文件占用可能阻止原子替换；只在临时文件仍存在时有限重试，最终错误继续上抛。
            for(int attempt=0;;attempt++)
            {
                try {File.Replace(temp,path,backup?path+".bak":null);break;}
                catch(IOException) when(attempt<5&&File.Exists(temp)&&File.Exists(path))
                {System.Threading.Thread.Sleep(20*(attempt+1));}
            }
        }
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
