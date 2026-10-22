using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillBdSupportChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("BD_SUPPORT: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        string qa=Path.Combine(root,"artifacts/qa/bd-support-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
        foreach(var source in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill"),"*.csv"))File.Copy(source,Path.Combine(qa,"skill",Path.GetFileName(source)));
        foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"tests/fixtures/skill_bd_slice_b1"),"*.csv"))
        {string destination=Path.Combine(qa,"skill",Path.GetFileName(file));File.WriteAllText(destination,File.ReadAllText(destination).TrimEnd()+"\n"+string.Join('\n',File.ReadAllLines(file).Skip(1))+"\n");}
        var definitions=SkillConfiguration.Load(qa,c.Attributes);
        foreach(var entry in definitions.Skills.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Skills.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Effects.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Effects.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Buffs.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Buffs.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Abilities.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Abilities.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.AbilityEffects.Where(e=>e.Key.StartsWith("QA_")))c.Skills.AbilityEffects.Add(entry.Key,entry.Value);
        GameSession Battle()
        {
            var game=new GameSession(c,785,new[]{"priest","warrior","mage"});
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];var state=game.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
            for(int i=0;i<game.Heroes.Count;i++){var h=game.Heroes[i];h.Position=room.Center+new Vector2(i*.35f,0);h.Hp=60;h.MaxHp=200;h.Attack=1;h.Range=100;h.Timer=999;h.SkillIds=Array.Empty<string>();h.StartingSkillGranted=true;}
            for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Position=room.Center+new Vector2(1.6f,i*.1f);m.Hp=m.MaxHp=100000;m.Defense=0;m.Range=100;m.Attack=0;m.Timer=999;}
            return game;
        }
        void Tick(GameSession game,int count=1){for(int i=0;i<count;i++)game.Tick(.05f);}
        string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});
        SkillBuffState[] Buffs(GameSession game)=>game.Capture().SkillState.Buffs.ToArray();

        var g=Battle();g.Heroes[0].SkillIds=new[]{"QA_PRAYER","QA_FANATICISM"};g.Heroes[1].SkillIds=new[]{"QA_FAN_STRONG"};Tick(g,25);
        Need(g.Heroes[0].ActiveAuraSkills.Count==2&&g.Heroes[0].Hp>60,"两种主动光环未同时激活/祈祷未周期治疗");
        var action=g.Capture().SkillState.Actions.Single(a=>a.Owner==0);long firstRoot=g.Capture().SkillState.NextId;
        Need(action.Cooldowns.Count==2,"维持光环不应每步重新施放");
        g.Heroes[2].Timer=10;Tick(g);Need(Math.Abs(g.Heroes[2].Timer-9.925)<.00001,"同组光环没有择强或重复叠加");
        g.Heroes[1].Position=g.Floor.Rooms[g.CurrentRoom].Center+new Vector2(0,g.Floor.Rooms[g.CurrentRoom].HalfHeight-.4f);
        g.Heroes[2].Timer=10;Tick(g);Need(Math.Abs(g.Heroes[2].Timer-9.9375)<.00001,"强光环离开后没有回退仍合法的弱来源");
        Tick(g,120);Need(g.Capture().SkillState.NextId==firstRoot,"持续光环刷新重复注册实例或重走施法");
        var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(g)).Session!);Tick(g,20);Tick(copy,20);if(Encode(g)!=Encode(copy)){File.WriteAllText(Path.Combine(qa,"left.json"),Encode(g));File.WriteAllText(Path.Combine(qa,"right.json"),Encode(copy));throw new Exception("Aura snapshot mismatch: "+qa);}
        var compact=g.CaptureProgress();Need(compact.Heroes[0].ActiveAuraSkills.Count==2&&compact.SkillState.Buffs.Count==0,"跨房激活状态与临时贡献边界混淆");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=compact})).Session!);Need(reopened.Heroes[0].ActiveAuraSkills.Count==2,"正式房间重开丢失激活记录");
        g.EquipSkills("priest",new[]{"QA_PRAYER"});Need(!g.Heroes[0].ActiveAuraSkills.Contains("QA_FANATICISM"),"卸槽没有立即撤销激活");
        var dead=Battle();dead.Heroes[0].SkillIds=new[]{"QA_PRAYER"};Tick(dead,8);dead.Heroes[0].Hp=0;Tick(dead);Need(dead.Heroes[0].ActiveAuraSkills.Count==0&&!Buffs(dead).Any(b=>b.Owner==0&&b.AuraSkill!=""),"来源死亡残留光环贡献");
        var noRevive=Battle();noRevive.Heroes[0].SkillIds=new[]{"QA_PRAYER"};noRevive.Heroes[2].Hp=0;Tick(noRevive,30);Need(noRevive.Heroes[2].Hp==0,"祈祷隐式复活了队友");

        var crossing=Battle();crossing.SetGmBattle(new(Invincible:true));crossing.Heroes[0].SkillIds=new[]{"QA_PRAYER","QA_FANATICISM"};Tick(crossing,25);
        int oldRoom=crossing.CurrentRoom;var cleared=crossing.Floor.Rooms[oldRoom];foreach(var m in cleared.Monsters)m.Hp=0;cleared.ContentCleared=true;cleared.EventUsed=true;cleared.Loot.Clear();
        for(int i=0;i<10000&&crossing.CurrentRoom==oldRoom;i++)Tick(crossing);
        Need(crossing.CurrentRoom!=oldRoom&&crossing.Heroes[0].ActiveAuraSkills.Count==2,"跨房没有保留光环激活");
        Tick(crossing,25);Need(!crossing.Capture().SkillState.Actions.Any(a=>a.Owner==0&&a.Cooldowns.Keys.Any(id=>id is "QA_PRAYER" or "QA_FANATICISM")),"跨房维持不应重新激活施法");

        var curses=Battle();curses.Heroes[2].SkillIds=new[]{"QA_AMPLIFY","QA_LOWER_RESIST"};Tick(curses,20);
        var curseRoom=curses.Floor.Rooms[curses.CurrentRoom];var target=curseRoom.Monsters[0];int targetId=curses.Heroes.Count;
        Need(Buffs(curses).Any(b=>b.Target==targetId&&b.Id=="QA_AMPLIFY_BUFF")&&Buffs(curses).Any(b=>b.Target==targetId&&b.Id=="QA_LOWER_BUFF"),"不同主要诅咒没有共存");
        var instances=Buffs(curses).Select(b=>b.Instance).ToArray();Tick(curses,110);Need(instances.All(id=>Buffs(curses).Any(b=>b.Instance==id)),"已有有效诅咒仍反复刷新");
        curses.Heroes[2].SkillIds=Array.Empty<string>();foreach(var m in curseRoom.Monsters.Skip(1))m.Hp=0;
        curses.Heroes[0].Attack=10;curses.Heroes[0].Timer=0;target.Battle.FireResistance=.5;Tick(curses);
        Need(Math.Abs(curses.LastDamage!.FinalDamage-15)<.00001&&curses.LastDamage.General==1,"物理易伤重复进通用池或没有生效");
        var basic=c.Skills.Effects["hit_player"];c.Skills.Effects["hit_player"]=basic with {DamageType=DamageType.Fire};curses.Heroes[0].Timer=0;Tick(curses);
        Need(Math.Abs(curses.LastDamage!.FinalDamage-7)<.00001&&curses.LastDamage.Vulnerable==1,"物理易伤污染火伤或减抗重复进入增伤池");c.Skills.Effects["hit_player"]=basic;

        var taunt=Battle();Tick(taunt);int owner=taunt.Heroes.Count;var lockBefore=taunt.MonsterTargets.First(l=>l.Owner==owner);
        taunt.Capture().SkillState.Actions.Add(new(){Owner=owner,Target=lockBefore.Target,Active="skill_01",Windup=.6});taunt.Heroes[0].SkillIds=new[]{"QA_TAUNT"};Tick(taunt,8);
        Need(taunt.MonsterTargets.First(l=>l.Owner==owner).Target==lockBefore.Target&&taunt.Capture().SkillState.Actions.First(a=>a.Owner==owner).Target==lockBefore.Target,"嘲讽打断已提交前摇或改写技能目标");
        Tick(taunt,12);Need(taunt.MonsterTargets.All(l=>l.Kind==MonsterLockKind.Forced&&l.Target==0),"安全边界未应用Forced或错误受普通容量限制");
        Tick(taunt,40);Need(taunt.MonsterTargets.All(l=>l.Kind!=MonsterLockKind.Forced),"嘲讽到期未恢复普通索敌");
        Need(taunt.MonsterTargets.Count(l=>l.Kind==MonsterLockKind.Normal&&l.Target==0)<=taunt.EngagementCapacity(taunt.Heroes[0]),"嘲讽结束遗留超额普通预约");

        var nova=Battle();nova.Heroes[2].SkillIds=new[]{"QA_FROST_NOVA"};nova.Heroes[2].Attack=10;Tick(nova,7);
        Need(nova.LastDamage?.Components[0].Type==DamageType.Cold&&Buffs(nova).Any(b=>b.Id=="QA_CHILL_BUFF")&&!Buffs(nova).Any(b=>b.Id=="buff_stun"),"新星未以自身近敌触发冰伤/减速或擅自冻结");
        var safe=Battle();safe.Heroes[2].SkillIds=new[]{"QA_FROST_NOVA"};foreach(var m in safe.Floor.Rooms[safe.CurrentRoom].Monsters)m.Ranged=true;Tick(safe,10);Need(!safe.Capture().SkillState.Actions.Any(a=>a.Cooldowns.ContainsKey("QA_FROST_NOVA")),"无近战威胁也释放防御新星");
        var guided=Battle();guided.Heroes[2].SkillIds=new[]{"QA_GUIDED_ARROW"};guided.Heroes[2].Attack=10;Tick(guided,5);
        var flight=guided.Capture().SkillState.Flights.Single();guided.Floor.Rooms[guided.CurrentRoom].Monsters[flight.Target-guided.Heroes.Count].Hp=0;Tick(guided,30);
        Need(guided.HitCount>0,"导引箭失效目标没有复用既有有界重选");

        var bad=GameSession.Restore(c,SaveStore.Decode(c,Encode(crossing)).Session!).Capture();bad.Heroes[0].ActiveAuraSkills.Add("missing");bool rejected=false;
        try{GameSession.Restore(c,bad);}catch(InvalidDataException){rejected=true;}Need(rejected,"非法激活光环存档未拒绝");
        Need(!LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config")).Skills.Skills.Keys.Any(id=>id.StartsWith("QA_")),"B1测试技能泄露生产配置");
        Console.WriteLine("PASS: B1 two maintained auras, strongest/fallback, periodic heal/no revive, cross-room activation/checkpoints, typed curses/resistance, safe forced locks, defensive nova and bounded guided retarget");
    }
}
