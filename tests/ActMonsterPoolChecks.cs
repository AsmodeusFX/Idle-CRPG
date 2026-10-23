using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>P1-5 切片C1：章节怪物身份表与统治区投放池的隔离检查。</summary>
public static class ActMonsterPoolChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("ACT_MONSTER: "+why);}
        string configRoot=Path.Combine(root,"Idle-CRPG/config");
        var c=LegacySkillConfig.LoadLegacy(configRoot);

        // 身份表：49个候选内容身份，独立ID，不得与正式怪物冲突。
        Need(c.ActMonsters.Count==49,"章节怪物身份数量不是49");
        Need(c.ActMonsters.Select(m=>m.Id).Distinct().Count()==49,"章节怪物身份ID重复");
        Need(!c.ActMonsters.Any(m=>c.Monsters.Any(b=>b.Id==m.Id)),"章节身份与正式怪物ID冲突");
        foreach(var monster in c.ActMonsters)
        {
            var actor=Actor.From(monster);
            c.ApplyCombatProfile(actor);
            Need(c.Drops.Any(d=>d.MonsterId==monster.Id),"身份缺少掉落定义："+monster.Id);
            Need(c.BaseEngagementCapacity(actor)>=0,"身份缺少接敌容量："+monster.Id);
        }

        // 池覆盖：正式配置下每章每个统治区都要有池，权重合计100，引用有效。
        var prod=LegacySkillConfig.Load(configRoot);
        Need(prod.ActMonsters.Count==49,"正式配置缺少章节怪物身份");
        foreach(var map in prod.ChapterMaps)
        {
            Need(map.MonsterPools.Count==map.AreaParts.Count,"统治区池覆盖数量不一致：第"+(map.ChapterIndex+1)+"章");
            foreach(var part in map.AreaParts)
            {
                bool found=map.MonsterPools.TryGetValue(part.Id,out var pool);
                Need(found&&pool!=null,"统治区缺少投放池："+part.Id);
                var entries=pool!;
                Need(entries.Sum(e=>e.Weight)==100,"权重合计不是100："+part.Id);
                Need(entries.All(e=>e.Weight>0&&e.Weight<=100),"权重越界："+part.Id);
                Need(entries.Select(e=>e.MonsterId).Distinct().Count()==entries.Count,"同区重复身份："+part.Id);
                Need(entries.All(e=>map.FindMonsterSpec(e.MonsterId)!=null),"身份引用无效："+part.Id);
            }
        }

        // 加权抽取：同种子可复现，且大致符合权重。
        var map0=prod.ChapterMaps[0];
        var first=new Random(1234);var second=new Random(1234);
        var a=Enumerable.Range(0,1000).Select(_=>map0.PickSpawnMonster(first,"ap1").Id).ToArray();
        var b=Enumerable.Range(0,1000).Select(_=>map0.PickSpawnMonster(second,"ap1").Id).ToArray();
        Need(a.SequenceEqual(b),"同种子加权抽取不可复现");
        foreach(var entry in map0.MonsterPools["ap1"])
        {
            int expected=entry.Weight*10,actual=a.Count(x=>x==entry.MonsterId);
            Need(Math.Abs(actual-expected)<=70,"权重抽样偏差过大："+entry.MonsterId+" 实际"+actual+" 期望"+expected);
        }

        // 世界远征：进房生成的怪物必须来自本次地牢所在统治区的池。
        var worldConfig=LegacySkillConfig.Load(configRoot);
        worldConfig.FloorsMin=worldConfig.FloorsMax=1;worldConfig.RoomsMin=worldConfig.RoomsMax=3;
        var game=GameSession.CreateWorld(worldConfig,25,new[]{"warrior","ranger","mage"});
        for(int i=0;i<20000&&(game.World!.Stage!=CampaignStage.Dungeon||game.CurrentRoom==0);i++)game.Tick(.05f);
        Need(game.World!.Stage==CampaignStage.Dungeon,"未进入世界地牢");
        var activeMap=game.Config;var dungeon=game.ActiveDungeon!;
        var areaPart=activeMap.AreaPart(dungeon.RegionId).Id;
        var allowed=activeMap.MonsterPools[areaPart].Select(e=>e.MonsterId).ToHashSet();
        var room=game.Floor.Rooms[game.CurrentRoom];
        Need(room.Monsters.Count>0,"进入的房间没有生成怪物");
        Need(room.Monsters.All(m=>allowed.Contains(m.SpecId)),"世界刷怪未使用统治区池："+string.Join(',',room.Monsters.Select(m=>m.SpecId)));

        // 无池地图（legacy第一章）必须回落到原全局怪物表，旧回归不受内容层影响。
        Need(c.MonsterPools.Count==0,"legacy第一章不应加载投放池");
        var solo=c;
        var single=new GameSession(solo,7,new[]{"warrior","ranger","mage"});
        for(int i=0;i<10000&&single.CurrentRoom==0;i++)single.Tick(.05f);
        var soloRoom=single.Floor.Rooms[single.CurrentRoom];
        Need(soloRoom.Monsters.Count>0&&soloRoom.Monsters.All(m=>solo.Monsters.Any(b=>b.Id==m.SpecId)),"无池时未回落到原怪物表");

        // 校验拒绝：重复身份、权重不合计100、未知身份都必须加载失败，而不是静默跳过。
        string qa=Path.Combine(root,"artifacts/qa/act-pool-"+Guid.NewGuid().ToString("N"));
        foreach(var file in Directory.EnumerateFiles(configRoot,"*.csv",SearchOption.AllDirectories))
        {string target=Path.Combine(qa,Path.GetRelativePath(configRoot,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
        string poolPath=Path.Combine(qa,"chapters/act1/monster/region_pool.csv"),poolText=File.ReadAllText(poolPath);
        void Reject(string text,string why)
        {
            File.WriteAllText(poolPath,text);bool failed=false;
            try{Config.Load(qa);}catch(Exception error)when(error is InvalidDataException or CsvConfigurationException){failed=true;}
            Need(failed,why);
        }
        Reject(poolText.Replace("ap1,a1_fallen,45","ap1,a1_fallen,40"),"权重合计非100未拒绝");
        Reject(poolText.Replace("ap1,a1_fallen,45","ap1,a1_fallen,45\nap1,a1_fallen,0"),"同区重复身份未拒绝");
        Reject(poolText.Replace("a1_fallen","a9_unknown"),"未知身份引用未拒绝");
        Reject(string.Join("\n",poolText.Split('\n').Where(l=>!l.StartsWith("ap1,"))),"统治区缺池未拒绝");
        File.WriteAllText(poolPath,poolText);

        Console.WriteLine("PASS: 49 chapter monster identities with profile/drops, per-areapart weighted pools covering all five chapters, deterministic weighted selection, world spawn bound to region pool, legacy fallback to base monsters, and rejection of duplicate/misweighted/unknown/missing pools");
    }
}
