using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// 核心体验模拟：用真实 GameSession 推进，A=纯挂机，B=挂机+现有成长操作。
/// 只读正式配置与正式接口，不写玩家存档；结果 CSV 输出到 artifacts/economy。
/// 游戏时间按 20Hz 逻辑步换算：30分钟=36000 tick，2小时=144000，4小时=288000。
/// </summary>
public static class EconomySimChecks
{
    private const int TicksPerMinute=1200;
    private static readonly int[] Checkpoints={30,120,240};

    public static void Run(string root,int minutes)
    {
        var config=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));
        string outDir=Path.Combine(root,"artifacts/economy");
        Directory.CreateDirectory(outDir);
        var summary=new List<string>{"strategy,minute,hero_levels,gold,kills,retries,chapter,difficulty,party,star_nodes,talent_spent,skill_slots,summon_slots"};
        var series=new List<string>{"strategy,minute,gold,kills,level0,chapter,room,stage"};
        var events=new List<string>{"strategy,minute,kind,detail"};

        Simulate("A",config,minutes,false,summary,series,events);
        Simulate("B",config,minutes,true,summary,series,events);

        File.WriteAllLines(Path.Combine(outDir,"summary.csv"),summary);
        File.WriteAllLines(Path.Combine(outDir,"timeseries.csv"),series);
        File.WriteAllLines(Path.Combine(outDir,"events.csv"),events);
        foreach(var line in summary.Skip(1))Console.WriteLine("SIM "+line);
        Console.WriteLine($"PASS: economy simulation completed for {minutes} game minutes per strategy; CSV at artifacts/economy (summary/timeseries/events)");
    }

    private static void Simulate(string name,Config config,int minutes,bool grow,List<string> summary,List<string> series,List<string> events)
    {
        var game=GameSession.CreateWorld(config,20261023,new[]{"warrior"});
        int totalTicks=minutes*TicksPerMinute;
        int nextSeries=0,nextPurchases=0;
        var bought=new HashSet<string>(StringComparer.Ordinal);
        var learned=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        for(int tick=0;tick<totalTicks;tick++)
        {
            game.Tick(.05f);
            int minute=tick/TicksPerMinute;
            if(grow&&tick>=nextPurchases)
            {
                nextPurchases=tick+TicksPerMinute; // 每游戏分钟尝试一次成长
                Grow(name,game,bought,learned,minute,events);
            }
            if(minute==nextSeries)
            {
                nextSeries++;
                double level=game.Heroes.Count==0?0:game.Heroes.Average(h=>h.Level);
                series.Add(string.Join(',',name,minute,game.Gold,game.Kills,level.ToString("0.0",CultureInfo.InvariantCulture),
                    game.ChapterIndex+1,game.CurrentRoom,game.World?.Stage.ToString()));
            }
            if(Checkpoints.Contains(minute)&&tick%TicksPerMinute==0&&minute>0)
            {
                int slots=BoughtCount(bought,"skill_slots")+config.Talents.Rule("active_skill_initial_slots");
                summary.Add(string.Join(',',name,minute,
                    string.Join('|',game.Heroes.Select(h=>$"{h.SpecId}:{h.Level}:{h.Experience}")),
                    game.Gold,game.Kills,game.Retries,game.ChapterIndex+1,game.DifficultyIndex,game.Heroes.Count,
                    bought.Count,learned.Values.Sum(v=>v.Count),slots,game.Summons.Count));
            }
        }
        // 收尾行：循环按 tick<total 推进，最后一分钟可能尚未命中检查点。
        int finalSlots=BoughtCount(bought,"skill_slots")+config.Talents.Rule("active_skill_initial_slots");
        summary.Add(string.Join(',',name,minutes,
            string.Join('|',game.Heroes.Select(h=>$"{h.SpecId}:{h.Level}:{h.Experience}")),
            game.Gold,game.Kills,game.Retries,game.ChapterIndex+1,game.DifficultyIndex,game.Heroes.Count,
            bought.Count,learned.Values.Sum(v=>v.Count),finalSlots,game.Summons.Count));
    }

    /// <summary>只用正式接口：星图购买、天赋学习、技能装配、编队请求；失败不伪造成功。</summary>
    internal static void Grow(string strategy,GameSession game,HashSet<string>bought,Dictionary<string,List<string>>learned,double minute,List<string> events,bool stars=true,bool growth=true,bool party=true)
    {
        // 1) 星图：优先队伍人数与技能槽，再买最便宜的攻击/生命节点。
        string[] order={"s_party_1","s_party_2","s_party_3","s_party_4","s_skill_1","s_skill_2","s_skill_3","s_skill_4","s_root","s_hp","s_atk","s_hp_01","s_atk_01","s_hp_02","s_atk_02"};
        foreach(string id in stars?order:Array.Empty<string>())
        {
            if(bought.Contains(id))continue;
            if(!game.BuyStar(id,out string reason))continue;
            bought.Add(id);
            events.Add($"{strategy},{minute},star,{id}");
        }
        // 2) 天赋：按行、列顺序学到学不动为止。
        foreach(var hero in growth?game.Heroes.ToArray():Array.Empty<Actor>())
        {
            var list=learned.TryGetValue(hero.SpecId,out var existing)?existing:learned[hero.SpecId]=new();
            foreach(var node in game.Config.Talents.Nodes.Values.Where(n=>n.Hero==hero.SpecId).OrderBy(n=>n.Row).ThenBy(n=>n.Col))
            {
                if(!game.LearnTalent(hero.SpecId,node.Id,out _))continue;
                list.Add(node.Id);
                events.Add($"{strategy},{minute},talent,{hero.SpecId}:{node.Id}");
            }
        }
        // 3) 技能装配：已学主动节点按槽位上阵（槽位由星图解锁，超限会被正式接口拒绝）。
        int slots=1+CountLevel(game,"skill_slots");
        foreach(var hero in growth?game.Heroes.ToArray():Array.Empty<Actor>())
        {
            if(!learned.TryGetValue(hero.SpecId,out var nodes))continue;
            var actives=nodes.Where(id=>game.Config.Talents.Nodes.TryGetValue(id,out var n)&&n.Kind=="active").Take(Math.Max(1,slots-0)).ToArray();
            if(actives.Length==0)continue;
            try{game.EquipSkills(hero.SpecId,actives);}
            catch(ArgumentException){/* 超槽位或非法组合：保持原装配 */}
        }
        // 4) 编队：星图扩编后按已解锁职业补人。
        int capacity=1+CountLevel(game,"party_slots");
        if(party&&game.Heroes.Count<capacity)
        {
            string[] unlocked={"warrior","ranger","mage"};
            var roster=unlocked.Take(capacity).ToArray();
            if(game.RequestFormation(roster,out string reason))events.Add($"{strategy},{minute},formation,{string.Join('|',roster)}");
            else if(reason!="")events.Add($"{strategy},{minute},formation_blocked,{reason}");
        }
    }

    private static int CountLevel(GameSession game,string attribute)
    {
        var snapshot=game.Stars.Capture();
        return snapshot.Ranks.Where(r=>game.Config.Stars.Nodes[r.Id].Attribute==attribute).Sum(r=>r.Level);
    }

    private static int BoughtCount(HashSet<string>bought,string attribute)=>bought.Count(id=>id.StartsWith("s_skill_",StringComparison.Ordinal)&&attribute=="skill_slots");
}
