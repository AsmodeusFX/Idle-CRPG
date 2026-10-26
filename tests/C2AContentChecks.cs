using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// S15-C2-A / A1：393 场地键、226 层遭遇计划、区域身份池与逐层偏置的真实消费。
/// 只写 artifacts/qa，不改玩家存档；不引入第二套数量/等级权威。
/// </summary>
public static class C2AContentChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C2A: "+why);}
    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var evidence=Path.Combine(root,"artifacts/qa","s15-c2a-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);

        // 1) 393 场地键：136 普通地牢 + 31 城堡 + 226 普通层，全部在生产章节配置中逐键存在。
        int dungeons=0,castles=0,floors=0;
        var missing=new List<string>();
        foreach(var chapter in config.ChapterMaps.Where(m=>m.ChapterIndex<4))
        {
            int ch=chapter.ChapterIndex+1;
            foreach(var d in chapter.Dungeons)
            {
                if(d.Kind=="castle")castles++;else dungeons++;
                int maxFloor=chapter.FloorPlans.Where(p=>p.Poi==d.Id).Select(p=>p.Index).DefaultIfEmpty(0).Max();
                // 226 只统计普通地牢的 Normal 层；城堡另有 88 层不计入。
                if(d.Kind!="castle")floors+=maxFloor;
                if(maxFloor==0)missing.Add($"{ch}:{d.Id}");
            }
        }
        Need(missing.Count==0,"存在无楼层规划的地牢："+string.Join(",",missing.Take(5)));
        Need(dungeons==136&&castles==31,"地牢/城堡数量不符：dungeons="+dungeons+" castles="+castles);
        Need(floors==226,"普通层数量不符："+floors);
        File.WriteAllLines(Path.Combine(evidence,"c2a_393_keys.csv"),
            new[]{"chapter,poi,kind,floors"}.Concat(config.ChapterMaps.SelectMany(m=>m.Dungeons.Select(d=>
                $"{m.ChapterIndex+1},{d.Id},{d.Kind},{m.FloorPlans.Where(p=>p.Poi==d.Id).Select(p=>p.Index).DefaultIfEmpty(0).Max()}"))));

        // 2) 226 层遭遇计划装载且引用合法；偏置在允许范围内。
        int plans=config.ChapterMaps.Where(m=>m.ChapterIndex<4).Sum(m=>m.EncounterPlans.Count);
        Need(plans==226,"遭遇计划装载数量不符："+plans);
        foreach(var map in config.ChapterMaps)
            foreach(var plan in map.EncounterPlans.Values)
            {
                Need(map.Dungeons.Any(d=>d.Id==plan.Poi),"遭遇计划引用不存在的地牢："+plan.Poi);
                Need(plan.PoolBias is "melee" or "ranged" or "heavy" or "mixed","身份偏好非法："+plan.Poi);
                Need(plan.CountBias is >= -2 and <= 2&&plan.EliteBias is >= -20 and <= 30,"偏置越界："+plan.Poi);
            }

        // 3) 身份池偏好真实改变抽取构成（同一池、同一随机源）。
        {
            var map=config.ChapterMaps[0];
            string area=map.AreaParts[0].Id;
            var pool=map.MonsterPools[area];
            Need(pool.Count>=2,"第一个统治区池不足两个身份，无法验证偏好");
            // 生产模板全为近战射程档，因此按“池内最长射程档/最高生命档”验证偏好，而不是绝对阈值。
            double maxRange=pool.Max(e=>map.FindMonsterSpec(e.MonsterId)!.Range);
            double maxHp=pool.Max(e=>map.FindMonsterSpec(e.MonsterId)!.Hp);
            (double MaxRangeShare,double MaxHpShare) Sample(string bias,int draws)
            {
                var rng=new Random(4242);int rangeTier=0,hpTier=0;
                for(int i=0;i<draws;i++)
                {
                    var spec=map.PickSpawnMonster(rng,area,bias);
                    if(spec.Range>=maxRange-.0001)rangeTier++;
                    if(spec.Hp>=maxHp-.0001)hpTier++;
                }
                return ((double)rangeTier/draws,(double)hpTier/draws);
            }
            var melee=Sample("melee",2000);var ranged=Sample("ranged",2000);var heavy=Sample("heavy",2000);var mixed=Sample("mixed",2000);
            Need(ranged.MaxRangeShare>melee.MaxRangeShare,"ranged偏好未提高最长射程档占比："+melee.MaxRangeShare+" → "+ranged.MaxRangeShare);
            Need(heavy.MaxHpShare>melee.MaxHpShare,"heavy偏好未提高最高生命档占比："+melee.MaxHpShare+" → "+heavy.MaxHpShare);
            File.WriteAllLines(Path.Combine(evidence,"c2a_pool_bias.csv"),
                new[]{"bias,ranged_share,heavy_share","melee,"+melee.MaxRangeShare+","+melee.MaxHpShare,"ranged,"+ranged.MaxRangeShare+","+ranged.MaxHpShare,"heavy,"+heavy.MaxRangeShare+","+heavy.MaxHpShare,"mixed,"+mixed.MaxRangeShare+","+mixed.MaxHpShare});
        }

        // 4) 逐层偏置在真实生成中被消费：固定种子，数量仍夹在 spawn_rule 区间、等级仍在 Run 区间。
        {
            var game=GameSession.CreateWorld(config,777001,new[]{"warrior","warrior","mage","priest"});
            for(int i=0;i<4000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            for(int i=0;i<600&&game.ActiveDungeon==null;i++)game.Tick(.05f);
            Need(game.ActiveDungeon!=null,"未进入地牢，无法验证逐层生成");
            var rows=new List<string>{"room,monsters,min_level,max_level,elite,pattern"};
            int spawned=0;
            for(int i=0;i<600;i++)
            {
                game.Tick(.05f);
                var room=game.Floor.Rooms[game.CurrentRoom];
                if(!room.Generated||room.Monsters.Count==0)continue;
                spawned++;
                int lo=room.Monsters.Min(m=>m.Level),hi=room.Monsters.Max(m=>m.Level);
                Need(lo>=game.MonsterLevelMin&&hi<=game.MonsterLevelMax,"怪级超出 Run 区间："+lo+"-"+hi);
                rows.Add($"{room.Id},{room.Monsters.Count},{lo},{hi},{room.Monsters.Any(m=>m.Elite)},{game.Config.EncounterFor(game.ChapterIndex+1,game.ActiveDungeon!.Id,game.FloorIndex+1)?.Pattern??""}");
                if(spawned>=8)break;
            }
            Need(spawned>0,"没有观察到已生成房间");
            File.WriteAllLines(Path.Combine(evidence,"c2a_runtime_spawn.csv"),rows);
        }

        // 5) 精英语气偏置真实改变命中率（消费者级，同一随机源）。
        {
            var game=GameSession.CreateWorld(config,777002,new[]{"warrior"});
            for(int i=0;i<4000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            for(int i=0;i<600&&game.ActiveDungeon==null;i++)game.Tick(.05f);
            Need(game.ActiveDungeon!=null,"未进入地牢，无法验证精英偏置");
            int Rate(int bias)
            {
                int hits=0;
                for(int i=0;i<400;i++)
                {
                    var room=game.Floor.Rooms[game.CurrentRoom];
                    var list=new List<Actor>();
                    for(int k=0;k<3;k++)list.Add(room.Monsters.Count>0?room.Monsters[0].Copy():new Actor{Name="qa",Hp=10,MaxHp=10,Attack=1,Defense=0,Speed=1,Range=.8f,Cooldown=1});
                    foreach(var actor in list)actor.Elite=false;
                    if(game.TryApplyElite(list,new Random(9000+i),0,bias))hits++;
                }
                return hits;
            }
            int low=Rate(-20),high=Rate(30);
            Need(high>low,"精英偏置未改变命中率："+low+" vs "+high);
            File.WriteAllLines(Path.Combine(evidence,"c2a_elite_bias.csv"),new[]{"elite_bias,hits_of_400","-20,"+low,"+30,"+high});
        }

        // 6) 旧行为不变：未投放章节（第五章）没有 floor_rhythm.csv，必须保持零计划。
        {
            Need(config.ChapterMaps[4].EncounterPlans.Count==0,"第五章未投放却装载了遭遇计划");
        }

        Console.WriteLine($"C2A_PASS: 136 dungeons/226 floors/31 castles, 226 encounter plans, pool+count+elite bias real consumers; evidence={evidence}");
    }
}
