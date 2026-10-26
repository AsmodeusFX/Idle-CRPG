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
        // 此夹具刻意关闭英雄全部技能以隔离怪物移动；固定S1不能在生产关闭，故显式使用历史QA调度。
        config.LegacySkillRuntime=true;
        var path=Path.Combine(root,"tests/fixtures/monster_encounters");
        string[][] Rows(string file)=>File.ReadAllLines(Path.Combine(path,file)).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Split(',')).ToArray();
        var types=Rows("archetypes.csv").Concat(Rows("archetypes_e2.csv")).ToDictionary(x=>x[0]);
        var members=Rows("members.csv").Concat(Rows("members_e2.csv")).ToArray();
        float Number(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
        foreach(var row in types.Values)
        {
            Need(row.Length==8&&config.Monsters.Any(x=>x.Id==row[1]),"原型模板引用");
            Need(row.Skip(2).Take(5).All(x=>float.IsFinite(Number(x))&&Number(x)>0),"原型正数属性");
            Need(bool.TryParse(row[7],out _),"攻击模式");
        }
        foreach(var row in members)Need(row.Length==3&&types.ContainsKey(row[1])&&int.TryParse(row[2],out int count)&&count>0,"遭遇引用/数量");
        Need(members.Select(x=>x[0]).Distinct().SequenceEqual(new[]{"T01","T02","T03","T04","T04C","T05"}),"E1/E2房间");
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
                if(member[1]=="M06")actor.TargetPolicy=MonsterTargetPolicy.BacklinePriority;
                if(member[1]=="M05")actor.SkillIds=new[]{"QA_E2_AREA"};
                // 规则网格在当前房间内缩区域展开，不堆在入口；二维坐标为Core权威。
                int columns=4;float x=(index%columns+.5f)/columns;float y=(index/columns+.5f)/(float)Math.Ceiling(total/(double)columns);
                actor.Position=room.Center+new Vector2((x*2-1)*(room.HalfWidth-1),(y*2-1)*(room.HalfHeight-1));index++;
                room.Monsters.Add(actor);
            }
            return game;
        }
        foreach(string scene in new[]{"T01","T02","T03","T04"})
        foreach(var party in new[]{new[]{"warrior"},new[]{"mage"},new[]{"warrior","mage"},config.Heroes.Take(4).Select(x=>x.Id).ToArray(),new[]{"necromancer"}})
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
        // 测试范围技能复用既有AreaPulse；所有定义只修改当前测试Config实例。
        config.Skills.Effects.Add("QA_E2_PULSE",config.Skills.Effects["barb_whirlwind_pulse"] with {Id="QA_E2_PULSE",Share=1,Extent=4});
        config.Skills.Skills.Add("QA_E2_AREA",new("QA_E2_AREA","ActiveSkill",60,3,3,1,5,new[]{"QA_E2_PULSE"},"测试范围攻击","enemy"));
        foreach(string scene in new[]{"T04C","T05"})
        {
            var battle=Create(scene,785,"warrior","mage");var r=battle.Floor.Rooms[battle.CurrentRoom];
            Need(r.Monsters.Count==8,"E2遭遇数量");
            var before=r.Monsters.Select(x=>x.Position).ToArray();battle.Tick(.05f);
            Need(!battle.MonsterTargets.Any()&&r.Monsters.Select(x=>x.Position).SequenceEqual(before),"E2警觉被绕过");
            for(int i=0;i<500;i++)battle.Tick(.05f);
            Need(battle.Heroes.Any(x=>x.Hp<x.MaxHp),"E2真实伤害");
            var again=Create(scene,785,"warrior","mage");for(int i=0;i<501;i++)again.Tick(.05f);
            Need(SaveStore.Encode(new(){Session=battle.Capture()})==SaveStore.Encode(new(){Session=again.Capture()}),"E2固定种子");
            Console.WriteLine("PASS E2 "+scene+": encounter/activation/damage/replay");
        }
        GameSession PolicyBattle()
        {
            var game=Create("T05",787,"warrior","mage");while(game.CombatEntryRemaining>0)game.Tick(.05f);
            game.Capture().SkillState.Targeting=new();
            var r=game.Floor.Rooms[game.CurrentRoom];
            for(int i=0;i<game.Heroes.Count;i++){game.Heroes[i].Position=r.Center+new Vector2(i*1.5f,0);game.Heroes[i].Range=100;game.Heroes[i].Timer=999;}
            foreach(var m in r.Monsters){m.Position=r.Center-Vector2.UnitX;m.Range=100;m.Timer=999;}
            game.Tick(.05f);return game;
        }
        var policy=PolicyBattle();var policyRoom=policy.Floor.Rooms[policy.CurrentRoom];int hunter=policy.Heroes.Count+6;
        Need(policy.MonsterTargets.Where(x=>x.Owner>=hunter).All(x=>x.Kind==MonsterLockKind.Special&&x.Target==1),"猎手必须显式选后排");
        Need(policy.MonsterTargets.Count(x=>x.Kind==MonsterLockKind.Normal&&x.Target==0)==3,"猎手不得污染普通容量");
        var state=policy.Capture().SkillState;
        state.Buffs.Add(new(){Instance=++state.NextId,Id="barb_taunt_buff",Owner=0,Target=hunter,Ability="barb_taunt",Remaining=.3,Coefficient=1});
        policy.Tick(.05f);Need(policy.MonsterTargets.Single(x=>x.Owner==hunter).Kind==MonsterLockKind.Forced,"Forced覆盖猎手");
        for(int i=0;i<10;i++)policy.Tick(.05f);
        Need(policy.MonsterTargets.Single(x=>x.Owner==hunter).Kind==MonsterLockKind.Special&&policy.MonsterTargets.Single(x=>x.Owner==hunter).Target==1,"嘲讽到期恢复后排策略");
        state.Actions.Add(new(){Owner=hunter,Target=1,Active="QA_E2_AREA",Windup=.4});
        state.Buffs.Add(new(){Instance=++state.NextId,Id="barb_taunt_buff",Owner=0,Target=hunter,Ability="barb_taunt",Remaining=1,Coefficient=1});
        policy.Tick(.05f);
        Need(policy.MonsterTargets.Single(x=>x.Owner==hunter).Target==1&&state.Actions.Single(x=>x.Owner==hunter).Target==1,"Forced不得改写前摇中的目标");
        for(int i=0;i<35;i++)policy.Tick(.05f);
        var restored=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=policy.Capture()})).Session!);
        for(int i=0;i<20;i++){policy.Tick(.05f);restored.Tick(.05f);}
        Need(SaveStore.Encode(new(){Session=policy.Capture()})==SaveStore.Encode(new(){Session=restored.Capture()}),"特殊策略快照续行");
        policy.Heroes[1].Position=policyRoom.Center+new Vector2(policyRoom.HalfWidth+2,0);policy.Tick(.05f);
        Need(policy.MonsterTargets.Where(x=>x.Owner>=hunter).All(x=>x.Target==0),"非法后排必须回退");
        var aoe=Create("T04C",788,"warrior","mage");while(aoe.CombatEntryRemaining>0)aoe.Tick(.05f);
        aoe.Capture().SkillState.Targeting=new();var aoeRoom=aoe.Floor.Rooms[aoe.CurrentRoom];
        foreach(var h in aoe.Heroes){h.Position=aoeRoom.Center;h.Range=100;h.Timer=999;}
        foreach(var m in aoeRoom.Monsters){m.Position=aoeRoom.Center+Vector2.UnitX;m.Timer=999;}
        var caster=aoeRoom.Monsters.First(x=>x.Name=="M05");
        foreach(var m in aoeRoom.Monsters.Where(x=>x!=caster))m.SkillIds=Array.Empty<string>();
        var hp=aoe.Heroes.Select(x=>x.Hp).ToArray();for(int i=0;i<10;i++)aoe.Tick(.05f);
        Need(aoe.Heroes.Select((h,i)=>h.Hp<hp[i]).All(x=>x),"范围攻击必须命中多个合法目标");
        Need(aoe.MonsterTargets.Single(x=>x.Owner==aoe.Heroes.Count+aoeRoom.Monsters.IndexOf(caster)).Kind==MonsterLockKind.Ranged,"范围技能不得覆盖普通锁定");
        Console.WriteLine("PASS E2: special backline/normal capacity/Forced expiry/invalid fallback/snapshot/area multi-target");
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
