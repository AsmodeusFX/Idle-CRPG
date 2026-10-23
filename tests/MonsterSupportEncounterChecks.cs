using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>E3支援及敌方有限召唤测试；全部为内存隔离配置。</summary>
public static class MonsterSupportEncounterChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("E3 SUPPORT: "+why);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        c.Skills.Buffs.Add("QA_E3_SUPPORT",new("QA_E3_SUPPORT",40,"RefreshDuration",new[]{new BuffAttributeSpec("atk","flat",5)},"",0,Array.Empty<string>(),"",0,0,"ally"));
        c.Skills.Effects.Add("QA_E3_SUPPORT_APPLY",new("QA_E3_SUPPORT_APPLY","ApplyBuff",DamageType.Physical,0,"QA_E3_SUPPORT","","",0,4,Array.Empty<string>(),0));
        c.Skills.Skills.Add("QA_E3_SUPPORT_CAST",new("QA_E3_SUPPORT_CAST","ActiveSkill",10,3,3,1,4,new[]{"QA_E3_SUPPORT_APPLY"},"隔离支援","self"));
        GameSession Battle()
        {
            var g=new GameSession(c,789,new[]{"warrior","mage"});
            foreach(var h in g.Heroes){h.Hp=h.MaxHp=100000;h.SkillIds=Array.Empty<string>();h.Timer=999;}
            for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var r=g.Floor.Rooms[g.CurrentRoom];r.Monsters.Clear();r.Origins.Clear();r.ContentCleared=false;
            for(int i=0;i<5;i++){var m=Actor.From(c.Monsters[0]);c.ApplyCombatProfile(m);m.Hp=m.MaxHp=10000;m.Position=r.Center+new Vector2(i*.2f,0);m.Range=100;m.Timer=999;m.Attack=10;r.Monsters.Add(m);}
            while(g.CombatEntryRemaining>0)g.Tick(.05f);
            g.Capture().SkillState.Targeting=new();
            foreach(var h in g.Heroes){h.Position=r.Center;h.Range=100;h.Timer=999;}
            r.Monsters[4].Ranged=true;r.Monsters[4].SkillIds=new[]{"QA_E3_SUPPORT_CAST"};
            return g;
        }
        void Tick(GameSession g,int count){for(int i=0;i<count;i++)g.Tick(.05f);}
        var game=Battle();Tick(game,12);var room=game.Floor.Rooms[game.CurrentRoom];var state=game.Capture().SkillState;
        Need(state.Buffs.Count(x=>x.Id=="QA_E3_SUPPORT")==5,"支援必须作用于本方怪物并保留来源");
        Need(state.Buffs.Where(x=>x.Id=="QA_E3_SUPPORT").All(x=>x.Owner==game.Heroes.Count+4),"Buff来源");
        // 验证真实伤害而非仅验证Buff存在；相同随机状态下只移除攻击加成。
        var baseline=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=game.Capture()})).Session!);
        baseline.Capture().SkillState.Buffs.RemoveAll(x=>x.Id=="QA_E3_SUPPORT");
        foreach(var sample in new[]{game,baseline})
        {
            foreach(var h in sample.Heroes){h.Defense=0;h.Battle.Dodge=0;h.Battle.BlockChance=0;}
            var m=sample.Floor.Rooms[sample.CurrentRoom].Monsters[0];m.Battle.CriticalChance=0;m.Timer=0;
        }
        Tick(game,1);Tick(baseline,1);
        Need(game.LastDamage!.FinalDamage>baseline.LastDamage!.FinalDamage,"支援攻击加成必须实际进入统一伤害管线");
        Tick(game,50);Need(state.Buffs.Count(x=>x.Id=="QA_E3_SUPPORT")==5,"重复施加不得无限堆叠");
        // 独立限时Buff脱离范围继续存续；来源死亡也不等同于光环立即撤销。
        room.Monsters[4].SkillIds=Array.Empty<string>();
        var remaining=state.Buffs.Where(x=>x.Id=="QA_E3_SUPPORT").Min(x=>x.Remaining);
        room.Monsters[4].Position=room.Center+new Vector2(room.HalfWidth-.3f,room.HalfHeight-.3f);
        Tick(game,1);Need(state.Buffs.Any(x=>x.Id=="QA_E3_SUPPORT"),"定时Buff不得因出范围消失");
        room.Monsters[4].Hp=1;room.Monsters[4].Position=game.Heroes[0].Position;game.Heroes[0].Attack=100000;game.Heroes[0].Timer=0;
        // 把其余怪物移远，确保统一普攻选中支援源。
        foreach(var m in room.Monsters.Take(4))m.Position=room.Center+Vector2.UnitX*2;
        // D2稳定友方目标：夹具显式进入正常重新索敌。
        state.Targeting.FriendlyTargets.Clear();
        Tick(game,1);Need(!room.Monsters[4].Alive,"支援源必须由统一伤害路径击杀");
        Need(state.Buffs.Any(x=>x.Id=="QA_E3_SUPPORT"&&x.Target!=game.Heroes.Count+4),"来源死亡不应删除独立定时Buff");
        game.Heroes[0].Timer=999;Tick(game,45);Need(!state.Buffs.Any(x=>x.Id=="QA_E3_SUPPORT"),"定时Buff必须到期");
        Console.WriteLine("PASS E3 T07: enemy support/source/refresh/no stacking/timed lifecycle after range and source death");
        var row=File.ReadAllLines(Path.Combine(root,"tests/fixtures/monster_encounters/enemy_summon_e3.csv"))[1].Split(',');
        var rule=new EnemySummonRule(row[0],row[1],row[2],row[3],int.Parse(row[4]),int.Parse(row[5]),bool.Parse(row[6]));
        c.EnemySummons.Add(rule.Id,rule);
        c.Skills.Effects.Add("QA_E3_SUMMON_EFFECT",new("QA_E3_SUMMON_EFFECT","Summon",DamageType.Physical,0,"",rule.Id,"",0,0,Array.Empty<string>(),0));
        c.Skills.Skills.Add(rule.Skill,new(rule.Skill,"ActiveSkill",10,3,3,1,4,new[]{"QA_E3_SUMMON_EFFECT"},"测试敌方召唤","self"));
        GameSession SummonBattle()
        {
            var g=Battle();var r=g.Floor.Rooms[g.CurrentRoom];r.Monsters.RemoveAt(4);
            var owner=c.Spawn.Create(c.Monsters.Single(m=>m.Id=="guard"),1,false,new());c.ApplyCombatProfile(owner);owner.Position=r.Center;owner.Timer=999;owner.Hp=owner.MaxHp=10000;r.Monsters.Add(owner);
            r.Origins.Clear();foreach(var m in r.Monsters)r.Origins.Add(new(m.SpecId,m.Level,false,m.Position,new()));
            g.Capture().SkillState=new();Prepare(g,r);return g;
        }
        void Prepare(GameSession g,Room r)=>GameSession.PrepareEnemySummons(c,r);
        var sg=SummonBattle();var sr=sg.Floor.Rooms[sg.CurrentRoom];Tick(sg,80);
        Need(sr.Monsters.Count==14&&sr.Monsters.Count(m=>m.EnemySummonSource>=0&&m.Alive)==3,"召唤槽与存活上限");
        var source=sr.Monsters[4];source.Timer=999;
        foreach(var m in sr.Monsters)m.Timer=999;
        var first=sr.Monsters.First(m=>m.EnemySummonSource>=0&&m.Alive);first.Hp=1;first.Position=sg.Heroes[0].Position;
        foreach(var m in sr.Monsters.Where(m=>m!=first&&m.Alive))m.Position=sr.Center+Vector2.UnitX*2;
        int kills=sg.Kills;long xp=sg.Experience;int corpseCount=sg.Capture().SkillState.Corpses.Count;
        sg.Capture().SkillState.Targeting.FriendlyTargets.Clear();
        first.RewardXp=50;first.DropMultiplier=100;sg.Heroes[0].Attack=10000;sg.Heroes[0].Timer=0;Tick(sg,1);sg.Heroes[0].Timer=999;
        Need(!first.Alive&&sg.Kills==kills+1&&sg.Experience>xp&&sg.Capture().SkillState.Corpses.Count==corpseCount+1&&sr.Loot.Count>0,"仆从死亡须沿统一经验掉落尸体链");
        Tick(sg,30);Need(sr.Monsters.Count(m=>m.EnemySummonSource>=0&&m.Alive)==3&&sr.Monsters.Count(m=>m.EnemySummonActivated)==4,"死亡释放上限且不得复用ID");
        var copy=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=sg.Capture()})).Session!);Tick(sg,15);Tick(copy,15);
        Need(SaveStore.Encode(new(){Session=sg.Capture()})==SaveStore.Encode(new(){Session=copy.Capture()}),"召唤完整快照续行");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=sg.CaptureProgress()})).Session!);
        Need(reopened.Floor.Rooms[reopened.CurrentRoom].Monsters.Count==14&&reopened.Floor.Rooms[reopened.CurrentRoom].Monsters.All(m=>!m.EnemySummonActivated),"正式读档重开召唤槽");
        // 消耗所有单次槽，测试总上限有限终止。
        for(int round=0;round<9;round++){foreach(var child in sr.Monsters.Where(m=>m.EnemySummonSource>=0))child.Hp=0;Tick(sg,20);}
        Need(sr.Monsters.Count(m=>m.EnemySummonActivated)==9&&!sr.Monsters.Any(m=>m.EnemySummonSource>=0&&m.Alive),"总上限必须停止再生");
        var retain=SummonBattle();Tick(retain,40);var rr=retain.Floor.Rooms[retain.CurrentRoom];rr.Monsters[4].Hp=0;Tick(retain,1);
        Need(rr.Monsters.Any(m=>m.EnemySummonSource>=0&&m.Alive)&&!rr.Clear,"默认来源死亡后仆从保留阻止清房");
        c.EnemySummons[rule.Id]=rule with {PersistAfterOwnerDeath=false};
        var remove=SummonBattle();Tick(remove,40);var rem=remove.Floor.Rooms[remove.CurrentRoom];rem.Monsters[4].Hp=0;int removeKills=remove.Kills;Tick(remove,1);
        Need(!rem.Monsters.Any(m=>m.EnemySummonSource>=0&&m.Alive)&&remove.Kills==removeKills,"配置撤销不等于击杀");
        Console.WriteLine("PASS E3 T06: cap/source/unique ID/rewards+corpse/refill/total cap/snapshot/checkpoint/owner death policies");
        var directory=Path.Combine(root,"Idle-CRPG/config");
        foreach(string id in Enumerable.Range(1,8).Select(i=>"M0"+i))
        {
            var pc=Config.Load(directory);pc.LoadMonsterPreviews(directory);
            var node=pc.Talents.Nodes.Values.First(n=>n.Hero=="warrior"&&n.Kind=="active");
            var preview=GameSession.CreateSkillPreview(pc,"warrior",node.Id,1,pc.Monsters[0].Id,2,1);
            preview.SetPreviewMonsters(id,2,1,directory);
            if(id=="M05")preview.Floor.Rooms[0].Monsters[0].Boss=true; // T08显式代理，仅验证现有Boss标记与范围技能接口。
            for(int i=0;i<160;i++)preview.TickSkillPreview(.05f);
            Need(preview.Floor.Rooms[0].Monsters.Any(m=>m.Name.StartsWith(id)),"GM原型加载 "+id);
            if(id=="M05")Need(preview.Capture().SkillState.Actions.Any(a=>a.Cooldowns.ContainsKey("preview_M05")),"Boss代理必须真实提交范围技能");
            if(id=="M07")Need(preview.Floor.Rooms[0].Monsters.Any(m=>m.EnemySummonActivated),"GM召唤原型必须实际召唤");
        }
        Console.WriteLine("PASS GM monster preview: M01-M08 load and live simulation");
        Console.WriteLine("PASS E3 T08 TEST_PROXY: Boss flag and existing area skill; real chapter Boss remains outside this package");
    }
}
