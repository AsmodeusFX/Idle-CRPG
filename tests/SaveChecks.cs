using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SaveChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("SAVE_CHECK: "+message);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));c.FloorsMin=c.FloorsMax=1;c.RoomsMin=c.RoomsMax=3;
        string directory=Path.Combine(root,"artifacts","save-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var store=new SaveStore(Path.Combine(directory,"progress.json"));
        var game=GameSession.CreateWorld(c,777,new[]{"warrior","ranger","mage"});
        foreach(var h in game.Heroes){h.Hp=h.MaxHp=100000;}
        string State(GameSession g)=>SaveStore.Encode(new SaveDocument {Session=g.Capture()});
        GameSession Roundtrip(GameSession g)
        {
            // 原始模型快照仍供确定性回归使用；正式 Save 单独验证房间检查点语义。
            store.WriteEncoded(SaveStore.Encode(new SaveDocument {Session=g.Capture(),Speed=4,Paused=true,ViewMode=1,SelectedRegion=1}));var loaded=store.Load(c);Check(loaded?.Session!=null,"存档没有读取");
            Check(loaded!.Speed==4&&loaded.Paused&&loaded.ViewMode==1&&loaded.SelectedRegion==1,"界面状态丢失");
            var resumed=GameSession.Restore(c,loaded.Session!);Check(State(g)==State(resumed),"完整状态没有恢复");return resumed;
        }
        // 各场景精确恢复，随后继续推进两个独立实例，检查掉落随机序列与奖励无重复。
        var seen=new System.Collections.Generic.HashSet<CampaignStage>();
        for(int tick=0;tick<10000&&seen.Count<4;tick++)
        {
            var stage=game.World!.Stage;
            if(seen.Add(stage))
            {
                var copy=Roundtrip(game);
                for(int i=0;i<120;i++){game.Tick(.05f);copy.Tick(.05f);}
                Check(State(game)==State(copy),"读档续玩不一致/随机序列变化："+stage);
            }
            game.Tick(.05f);
        }
        Check(seen.Count==4,"未覆盖全部世界场景");
        // 正在交战的房间与半程救援读条均不重新生成或丢失计时。
        for(int i=0;i<10000&&(game.World!.Stage!=CampaignStage.Dungeon||game.CurrentRoom==0);i++)game.Tick(.05f);
        foreach(var m in game.Floor.Rooms[game.CurrentRoom].Monsters)m.Hp=0;
        game.Heroes[0].Hp=0;game.Heroes[0].Position=game.Heroes[1].Position;
        for(int i=0;i<20;i++)game.Tick(.05f);
        Check(game.ReviveElapsed>0&&game.ReviveElapsed<c.ReviveSeconds,"未构造半程救援");
        var rescueCopy=Roundtrip(game);for(int i=0;i<100;i++){game.Tick(.05f);rescueCopy.Tick(.05f);}
        Check(State(game)==State(rescueCopy),"救援进度恢复后不一致");
        // 非法引用、损坏主档与备份恢复；恢复后首次写盘不能覆盖健康备份。
        store.Save(game);store.Save(game);File.WriteAllText(store.PathName,"{broken");
        var recovered=store.Load(c);Check(recovered?.Session!=null&&store.LastWarning!=null,"没有恢复备份");
        store.Save(GameSession.Restore(c,recovered!.Session!));Check(SaveStore.Decode(c,File.ReadAllText(store.PathName+".bak")).Session!=null,"健康备份被损坏主档替换");
        var invalid=SaveStore.Decode(c,File.ReadAllText(store.PathName));invalid.Session!.World!.TargetId="missing";
        bool rejected=false;try{SaveStore.Decode(c,SaveStore.Encode(invalid));}catch(InvalidDataException){rejected=true;}Check(rejected,"无效配置引用未拒绝");
        store.Reset();Check(store.Load(c)?.Session==null&&SaveStore.Decode(c,File.ReadAllText(store.PathName+".bak")).Session==null,"重置后备份复活旧档");
        // 重置后新档保存，备份仍只能恢复重置后的进度。
        var fresh=GameSession.CreateWorld(c,5,new[]{"warrior","ranger","mage"});store.Save(fresh);
        Check(store.Load(c)?.Session?.Seed==5,"重置后无法保存新档");
        var settingsStore=new SettingsStore(Path.Combine(directory,"settings.json"));
        var settings=new AppSettings {Width=1600,Height=1000,MusicVolume=35,SoundVolume=0,Fullscreen=true};settingsStore.Save(settings);
        store.Reset();var preferences=settingsStore.Load();Check(preferences.Width==1600&&preferences.Height==1000&&preferences.MusicVolume==35&&preferences.SoundVolume==0&&preferences.Fullscreen,"设置未持久化或被重置清除");
        settings.Width=0;settings.MusicVolume=float.NaN;settings.SoundVolume=200;settingsStore.Save(settings);preferences=settingsStore.Load();
        Check(preferences.Width>=800&&float.IsFinite(preferences.MusicVolume)&&preferences.SoundVolume==100,"设置边界未校验");
        Console.WriteLine("PASS: exact save/load continuation, routes/combat/trade/revival, RNG/rewards, backup recovery, invalid references, reset and independent settings");
    }
}
