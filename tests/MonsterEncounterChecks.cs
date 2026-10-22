using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Globalization;
using IdleCrpg.Core;

/// <summary>E1隔离遭遇：只构造内存会话，不注册正式怪物池，不读写玩家存档。</summary>
public static class MonsterEncounterChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("ENCOUNTER E1: "+why);}
    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var path=Path.Combine(root,"tests/fixtures/monster_encounters");
        string[][] Rows(string file)=>File.ReadAllLines(Path.Combine(path,file)).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Split(',')).ToArray();
        var types=Rows("archetypes.csv").ToDictionary(x=>x[0]);
        var members=Rows("members.csv");
        float Number(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
        foreach(var row in types.Values)
        {
            Need(row.Length==8&&config.Monsters.Any(x=>x.Id==row[1]),"原型模板引用");
            Need(row.Skip(2).Take(5).All(x=>float.IsFinite(Number(x))&&Number(x)>0),"原型正数属性");
            Need(bool.TryParse(row[7],out _),"攻击模式");
        }
        foreach(var row in members)Need(row.Length==3&&types.ContainsKey(row[1])&&int.TryParse(row[2],out int count)&&count>0,"遭遇引用/数量");
        Need(members.Select(x=>x[0]).Distinct().SequenceEqual(new[]{"T01","T02","T03","T04"}),"E1仅四房");
        // 原型数值只存在TEST_ONLY CSV；收益沿原管线计入此内存会话，不向玩家发放。
        GameSession Create(string scene,int seed,params string[] heroes)
        {
            var game=new GameSession(config,seed,heroes);
            foreach(var h in game.Heroes){h.Hp=h.MaxHp=100000;h.SkillIds=Array.Empty<string>();h.Timer=999;}
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            Need(game.CurrentRoom!=0&&game.CombatEntryRemaining>0,"必须真实走门进入并保留警觉");
            var room=game.Floor.Rooms[game.CurrentRoom];room.Monsters.Clear();room.Origins.Clear();room.ContentCleared=false;
            var rows=members.Where(x=>x[0]==scene).ToArray();int total=rows.Sum(x=>int.Parse(x[2]));int index=0;
            foreach(var member in rows)for(int i=0;i<int.Parse(member[2]);i++)
            {
                var r=types[member[1]];var actor=Actor.From(config.Monsters.Single(x=>x.Id==r[1]));config.ApplyCombatProfile(actor);
                actor.Name=member[1];actor.Hp=actor.MaxHp=Number(r[2]);actor.Attack=Number(r[3]);actor.Speed=Number(r[4]);actor.Range=Number(r[5]);actor.Cooldown=Number(r[6]);actor.Ranged=bool.Parse(r[7]);actor.SkillIds=Array.Empty<string>();
                // 规则网格在当前房间内缩区域展开，不堆在入口；二维坐标为Core权威。
                int columns=4;float x=(index%columns+.5f)/columns;float y=(index/columns+.5f)/(float)Math.Ceiling(total/(double)columns);
                actor.Position=room.Center+new Vector2((x*2-1)*(room.HalfWidth-1),(y*2-1)*(room.HalfHeight-1));index++;
                room.Monsters.Add(actor);
            }
            return game;
        }
        foreach(string scene in new[]{"T01","T02","T03","T04"})
        foreach(var party in new[]{new[]{"warrior"},new[]{"mage"},new[]{"warrior","mage"},config.Heroes.Select(x=>x.Id).ToArray()})
        {
            var game=Create(scene,785,party);var room=game.Floor.Rooms[game.CurrentRoom];
            Need(room.Monsters.Count==(scene=="T02"?12:scene=="T03"?3:8),"房间数量");
            Need(room.Monsters.All(game.InCombatRoom)&&room.Monsters.Select(x=>x.Position).Distinct().Count()==room.Monsters.Count,"出生合法且分散");
            var initial=room.Monsters.Select(x=>x.Position).ToArray();game.Tick(.05f);
            Need(!game.MonsterTargets.Any()&&room.Monsters.Select(x=>x.Position).SequenceEqual(initial),"警觉期不得提前索敌移动");
            for(int tick=0;tick<500;tick++)
            {
                game.Tick(.05f);
                foreach(var group in game.MonsterTargets.Where(x=>x.Kind==MonsterLockKind.Normal).GroupBy(x=>x.Target))
                    Need(group.Count()<=game.EngagementCapacity(game.Heroes[group.Key]),"正常预约超过容量");
            }
            Need(game.MonsterTargets.Any(),"激活后无锁定");
            Need(game.Heroes.Any(x=>x.Hp<x.MaxHp),"怪物必须完成真实攻击周期");
            Need(room.Monsters.All(game.InCombatRoom),"移动越界");
            foreach(var target in game.MonsterTargets)
                Need(room.Monsters[target.Owner-game.Heroes.Count].Ranged==(target.Kind==MonsterLockKind.Ranged),"远程容量语义");
            var repeated=Create(scene,785,party);for(int tick=0;tick<501;tick++)repeated.Tick(.05f);
            Need(SaveStore.Encode(new(){Session=game.Capture()})==SaveStore.Encode(new(){Session=repeated.Capture()}),"固定seed完整事实不一致");
            Console.WriteLine($"PASS E1 {scene}: party={party.Length}, spawn/delay/attack/capacity/bounds/determinism");
        }
        // 射程内射手站定：停用英雄行动只隔离站定断言，不绕开怪物索敌或伤害。
        var ranged=Create("T04",786,"warrior");var arena=ranged.Floor.Rooms[ranged.CurrentRoom];
        while(ranged.CombatEntryRemaining>0)ranged.Tick(.05f);
        ranged.Capture().SkillState.Targeting.EntryPrepared=false;
        ranged.Heroes[0].Position=arena.Center;ranged.Heroes[0].Range=100;ranged.Heroes[0].Timer=999;
        foreach(var actor in arena.Monsters)actor.Position=arena.Center+Vector2.UnitX;
        var shooters=arena.Monsters.Where(x=>x.Ranged).ToArray();var positions=shooters.Select(x=>x.Position).ToArray();
        for(int i=0;i<40;i++)ranged.Tick(.05f);
        Need(shooters.Select(x=>x.Position).SequenceEqual(positions),"射程内普通射手不应风筝");
        Console.WriteLine("PASS E1 T04: ranged holds in range; TEST_ONLY memory sessions only");
    }
}
