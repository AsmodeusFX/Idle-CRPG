using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class ChapterChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("CHAPTER_CHECK: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        Check(c.Difficulties.Count==3&&c.Chapters.Count==5&&c.ChapterMaps.Count==5,"难度章节数量");
        Check(c.ChapterMaps[0]==c&&c.WorldWidth==16&&c.WorldHeight==16,"第一章兼容");
        Check(c.ChapterMaps.Select(m=>(m.WorldWidth,m.WorldHeight)).Distinct().Count()>1,"独立地图尺寸");
        foreach(var map in c.ChapterMaps){map.ValidateWorld();Check(map.Spawn.Levels.Count==c.MonsterLevels.Count,"等级配置绑定");}
        string Encode(GameSession g)=>SaveStore.Encode(new(){Session=g.CaptureProgress()});
        GameSession Copy(GameSession g)=>GameSession.Restore(c,SaveStore.Decode(c,Encode(g)).Session!);
        GameSession AtTrade(GameSession game,bool complete)
        {
            var map=game.Config;var s=game.CaptureProgress();var w=s.World!;
            var village=map.Villages.Single(v=>v.RegionId==map.StartRegionId);var region=map.Regions.Single(r=>r.Id==village.RegionId);var npc=map.Npcs.Single(n=>n.Id==village.NpcId);
            w.Stage=CampaignStage.Village;w.VillageId=village.Id;w.Position=new Vector2(region.X+village.X,region.Y+village.Y)+npc.Position/map.WorldUnits;
            w.OutdoorTrail.Clear();w.FollowGather=0;w.Route.Clear();w.VillagePaths.Clear();w.Explored=map.Regions.Select(r=>r.Id).ToList();w.Cleared=map.Dungeons.Where(d=>complete||!d.Castle).Select(d=>d.Id).ToList();w.TradeElapsed=0;w.PendingCastles.Clear();
            s.Floors.Clear();s.ActiveDungeonId="";s.SkillState=new();
            foreach(var hero in s.Heroes)hero.Position=npc.Position;
            return GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=s})).Session!);
        }
        var game=GameSession.CreateWorld(c,667,new[]{"warrior","ranger","mage"});game.GrantGmResources();
        var legacyNode=System.Text.Json.Nodes.JsonNode.Parse(SaveStore.Encode(new(){Version=2,Session=game.CaptureProgress()}))!;
        var legacySession=legacyNode["Session"]!.AsObject();legacySession.Remove("DifficultyIndex");legacySession.Remove("ChapterIndex");legacySession.Remove("CompletedMaps");
        var legacy=GameSession.Restore(c,SaveStore.Decode(c,legacyNode.ToJsonString()).Session!);
        Check(legacy.ChapterIndex==0&&legacy.DifficultyIndex==0&&legacy.Gold==game.Gold,"旧版存档未保留为普通第一章");
        Check(!game.MapUnlocked(0,1)&&!game.MapUnlocked(1,0),"提前解锁");
        var waiting=AtTrade(Copy(game),false);for(int i=0;i<60;i++)waiting.Tick(.05f);
        Check(waiting.ChapterIndex==0&&waiting.World!.Target.Farming,"普通地牢全清不应自动打城堡/跨章");
        long gold=game.Gold;
        for(int index=0;index<15;index++)
        {
            game=AtTrade(game,true);var max=game.Heroes.Select(h=>h.MaxHp).ToArray();
            for(int i=0;i<10;i++)game.Tick(.05f);
            Check(game.DifficultyIndex*5+game.ChapterIndex==index,"交易完成前跨章");
            game=Copy(game);
            var team=game.Heroes.ToArray();
            for(int i=0;i<35;i++)game.Tick(.05f);
            int expected=Math.Min(14,index+1);
            Check(game.DifficultyIndex*5+game.ChapterIndex==expected,"自动章节/难度推进");
            Check(game.Gold==gold&&game.Heroes.Select(h=>h.MaxHp).SequenceEqual(max),"跨章丢失资源或重复成长");
            Check(game.Heroes.SequenceEqual(team),"跨章替换了玩家角色实例");
            if(index<14)
            {
                Check(game.World!.Progress.Cleared.Count==0&&game.World.Progress.Explored.Count>=1,"新图继承了旧图完成记录");
                Check(game.MapProgress(index/5,index%5).Cleared.Count==c.ChapterMaps[index%5].Dungeons.Count,"旧图完成记录丢失");
                Check(!game.MapUnlocked((expected+1)/5,(expected+1)%5),"跳过下一章门禁");
            }
            else Check(game.World!.Target.Farming,"全难度通关后未持续复刷");
            string state=Encode(game);game=Copy(game);Check(Encode(game)==state,"跨图存档往返不一致");
        }
        var store=new SaveStore(Path.Combine(root,"artifacts/qa/chapters/progress.json"));store.Reset();store.Save(game,1,false,1);Check(store.Load(c)?.Session?.ChapterIndex==4,"章节存档落盘");
        store.Save(game,1,false,1);File.WriteAllText(store.PathName,"{broken");Check(store.Load(c)?.Session?.DifficultyIndex==2&&store.LastWarning!=null,"跨难度存档备份恢复");store.Reset();
        var bad=game.CaptureProgress();bad.CompletedMaps.RemoveAt(0);bool rejected=false;
        try{GameSession.Restore(c,bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"不连续章节历史未拒绝");
        string qa=Path.Combine(root,"artifacts/qa/chapter-config");
        foreach(string file in Directory.GetFiles(Path.Combine(root,"Idle-CRPG/config"),"*.csv",SearchOption.AllDirectories))
        {
            string target=Path.Combine(qa,Path.GetRelativePath(Path.Combine(root,"Idle-CRPG/config"),file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target,true);
        }
        string chapterFile=Path.Combine(qa,"legacy/chapter.csv"),text=File.ReadAllText(chapterFile);
        File.WriteAllText(chapterFile,text.Replace("desert,10,8","desert,11,8"));rejected=false;
        try{LegacySkillConfig.LoadLegacy(qa);}catch(InvalidDataException){rejected=true;}Check(rejected,"章节尺寸不一致未拒绝");
        File.WriteAllText(chapterFile,text.Replace("act2,","act1,"));rejected=false;
        try{LegacySkillConfig.LoadLegacy(qa);}catch(InvalidDataException){rejected=true;}Check(rejected,"重复章节ID未拒绝");File.WriteAllText(chapterFile,text);
        Console.WriteLine("PASS: 3 difficulties, 5 independent maps, sequential unlock, trade-before-transition, 15-map progression, resource retention, isolated progress, final farming and chapter saves");
    }
}
