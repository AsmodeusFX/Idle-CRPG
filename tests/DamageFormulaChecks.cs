using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class DamageFormulaChecks
{
    public static void Run()
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("DAMAGE_FORMULA: "+why);}
        void Near(double actual,double expected,string why)=>Need(Math.Abs(actual-expected)<.000001,why+$" ({actual} != {expected})");
        var rules=new CombatRules(1.5,-1,.75,new[]{(1,100d),(101,200d)});
        var attack=new BattleAttributes {CriticalChance=1};
        attack.Modifiers.AddRange(new[]{new DamageModifier(DamageBucket.General,.3),new(DamageBucket.Power,.5),new(DamageBucket.Vulnerable,.2,Type:DamageType.Physical,DebuffId:"physical"),new(DamageBucket.Vulnerable,.1,Type:DamageType.Fire,DebuffId:"fire")});
        var input=new DamageInput("mixed",100,100,1,DamageType.Physical,true,false,attack,new(){BlockChance=1,BlockReduction=.5,FireResistance=.2},new HashSet<string>{"physical","fire"},1,new[]{new DamagePortion(DamageType.Physical,.6),new(DamageType.Fire,.4)});
        var rng=new PersistentRandom(123);uint before=rng.State;
        var mixed=DamagePipeline.Resolve(input,rules,rng);
        Near(mixed.Components[0].Damage,105.3,"物理分量手算");Near(mixed.Components[1].Damage,51.48,"火分量手算");Near(mixed.FinalDamage,156.78,"单实例混合总伤害");
        Need(mixed.Dmg2.Blocked&&mixed.Dmg2.Critical&&rng.State==before,"混合分量不能额外掷随机数");
        var plain=input with {Attacker=new(),Defender=new(),Coefficient=1,Portions=null};
        Near(DamagePipeline.Resolve(plain,rules,new(1)).FinalDamage,100,"同评级不受旧Armor减伤");
        Near(DamagePipeline.Resolve(plain with {AttackerLevel=101},rules,new(1)).FinalDamage,100,"旧等级K不再参与伤害");
        Near(rules.ArmorK(51),150,"控制点线性插值");
        Near(DamagePipeline.Resolve(plain with {Attack=0},rules,new(1)).FinalDamage,0,"零基础无隐含保底");
        Near(DamagePipeline.Resolve(plain with {Coefficient=0},rules,new(1)).FinalDamage,0,"零技能系数无隐含保底");
        var fire=plain with {Type=DamageType.Fire,Defender=new(){FireResistance=-.2}};
        Near(DamagePipeline.Resolve(fire,rules,new(1)).FinalDamage,120,"负抗增伤且元素不受护甲");
        Near(DamagePipeline.Resolve(fire with {Defender=new(){FireResistance=-10}},rules,new(1)).FinalDamage,200,"负抗只在防御端截断");
        Near(DamagePipeline.Resolve(fire with {Defender=new(){FireResistance=10}},rules,new(1)).FinalDamage,25,"抗性上限且无完全免疫");
        var pools=new BattleAttributes();pools.Modifiers.AddRange(new[]{new DamageModifier(DamageBucket.Power,.5),new(DamageBucket.Power,1),new(DamageBucket.Vulnerable,.2,DebuffId:"a"),new(DamageBucket.Vulnerable,.3,DebuffId:"b"),new(DamageBucket.Vulnerable,10,DebuffId:"missing")});
        var combined=DamagePipeline.Resolve(fire with {Attacker=pools,Defender=new(),Debuffs=new HashSet<string>{"a","b"}},rules,new(1));
        Near(combined.Power,2.5,"威能同池加算");Near(combined.Vulnerable,1.5,"不同易伤ID不额外叠乘");Near(combined.FinalDamage,375,"池间相乘");
        var oddInput=input with {Attacker=new(){CriticalChance=.4},Defender=new(){Dodge=.1,BlockChance=.3},Portions=null};
        var singleRng=new PersistentRandom(772);var mixedRng=new PersistentRandom(772);
        var single=DamagePipeline.Resolve(oddInput,rules,singleRng);
        var multi=DamagePipeline.Resolve(oddInput with {Portions=input.Portions},rules,mixedRng);
        Need(single.Dmg2==multi.Dmg2&&singleRng.State==mixedRng.State,"事件事实与RNG次数不能随分量数增加");
        bool Reject(DamageInput value){try{DamagePipeline.Resolve(value,rules,new(1));return false;}catch(InvalidDataException){return true;}}
        Need(Reject(plain with {Portions=new[]{new DamagePortion(DamageType.Fire,.5)}}),"不足1的分量权重未拒绝");
        Need(Reject(plain with {Portions=new[]{new DamagePortion(DamageType.Fire,.5),new(DamageType.Fire,.5)}}),"重复类型未拒绝");
        Need(Reject(plain with {AttackerLevel=0})&&Reject(plain with {AttackerLevel=102}),"非法攻击者等级未拒绝");
        Need(Reject(fire with {Defender=new(){FireResistance=double.NaN}}),"非有限抗性未拒绝");
        var negativePower=new BattleAttributes();negativePower.Modifiers.AddRange(new[]{new DamageModifier(DamageBucket.Power,-1),new(DamageBucket.Power,-1)});
        Need(Reject(plain with {Attack=0,Attacker=negativePower}),"零伤害不能掩盖非法负威能倍率");
        Console.WriteLine("PASS: typed defenses, attacker-level armor curve, negative/capped resistance, additive 4+1, shared mixed-hit facts/RNG, zero damage and invalid inputs");
    }

    public static void RunSession(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("DAMAGE_SESSION: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        GameSession Battle()
        {
            var game=new GameSession(c,785,new[]{"warrior"});
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];var state=game.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Buffs.Clear();state.Flights.Clear();state.Pending.Clear();
            var hero=game.Heroes[0];hero.Position=room.Center-Vector2.UnitX;hero.Hp=hero.MaxHp=100000;hero.Timer=999;hero.SkillIds=Array.Empty<string>();
            for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Hp=i==0?100000:0;m.MaxHp=100000;m.Timer=999;m.Position=room.Center+Vector2.UnitX;}
            return game;
        }
        var mixed=Battle();var hero=mixed.Heroes[0];var room=mixed.Floor.Rooms[mixed.CurrentRoom];var victim=room.Monsters[0];
        hero.Attack=100;hero.Range=100;hero.Timer=0;victim.Hp=1;
        var original=c.Skills.Effects["hit_player"];
        c.Skills.Effects["hit_player"]=original with {Portions=new[]{new DamagePortion(DamageType.Physical,.6),new(DamageType.Fire,.4)}};
        // 同动作排两份致死效果，第二份必须由统一入口拒绝，分量也不能独立发奖。
        var basic=c.Skills.Skills["basic_player"];c.Skills.Skills["basic_player"]=basic with {Effects=new[]{"hit_player","hit_player"}};
        int kills=mixed.Kills;long hits=mixed.HitCount;mixed.Tick(.05f);
        Need(mixed.Kills==kills+1&&mixed.HitCount==hits+1&&mixed.LastDamage?.Components.Count==2,"混合致死或同Tick再次致死重复应用HP/奖励");
        Need(!victim.Alive&&mixed.PublicExperience>0,"混合致死未走正式击杀经验入口");
        c.Skills.Effects["hit_player"]=original;c.Skills.Skills["basic_player"]=basic;

        var flightGame=Battle();flightGame.Heroes[0].Level=7;flightGame.EquipSkills("warrior",new[]{"skill_01"});
        for(int i=0;i<5;i++)flightGame.Tick(.05f);
        var flight=flightGame.Capture().SkillState.Flights.First();Need(flight.Snapshot.Level==7,"飞行没有快照出手等级");
        flightGame.Heroes[0].Level=37;Need(flight.Snapshot.Level==7,"升级改写在途攻击等级");
        string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});
        var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(flightGame)).Session!);
        for(int i=0;i<20;i++){flightGame.Tick(.05f);copy.Tick(.05f);}
        Need(Encode(flightGame)==Encode(copy),"新等级快照续行不确定");

        var scaled=Battle();scaled.Heroes[0].Attack=10;scaled.EquipSkills("warrior",new[]{"skill_01"});for(int i=0;i<5;i++)scaled.Tick(.05f);
        var scaledFlight=scaled.Capture().SkillState.Flights.First();scaledFlight.PowerScale=.5;
        scaledFlight.Snapshot.Attributes.Modifiers.Add(new(DamageBucket.Skill,.3));
        scaled.Floor.Rooms[scaled.CurrentRoom].Monsters[0].Defense=0;scaled.Heroes[0].SkillIds=Array.Empty<string>();
        double scaledExpected=scaledFlight.Snapshot.Attack*.5*(scaledFlight.Coefficient+.3)*Math.Clamp(Math.Pow(scaledFlight.Snapshot.AttackRating/scaled.DefenseRating(scaled.Floor.Rooms[scaled.CurrentRoom].Monsters[0]),c.Progression.RatingExponent),c.Progression.RatingMinimum,c.Progression.RatingMaximum);
        for(int i=0;i<20;i++)scaled.Tick(.05f);
        Need(scaled.LastDamage!=null&&Math.Abs(scaled.LastDamage.FinalDamage-scaledExpected)<.000001,"分身/周期比例必须同时缩放技能系数加值，不得泄露额外伤害");

        var legacy=Battle().Capture();legacy.CombatVersion=1;
        legacy.Heroes[0].Battle.Modifiers.Add(new(DamageBucket.Power,1.5));
        legacy.Heroes[0].Level=9;legacy.Gold=1234;
        var restored=GameSession.Restore(c,legacy);
        Need(restored.Heroes[0].Battle.Modifiers.Single().Value==.5&&restored.Gold==1234&&restored.Heroes[0].Level==9,"旧倍率或已有成长迁移错误");
        var again=GameSession.Restore(c,restored.Capture());Need(again.Heroes[0].Battle.Modifiers.Single().Value==.5,"二次恢复重复减1");
        Need(again.CaptureProgress().SkillState.Flights.Count==0,"正式检查点泄露新在途现场");
        var oldFlight=Battle();oldFlight.Heroes[0].Level=8;oldFlight.EquipSkills("warrior",new[]{"skill_01"});for(int i=0;i<5;i++)oldFlight.Tick(.05f);
        var oldSnapshot=oldFlight.Capture();oldSnapshot.CombatVersion=1;oldSnapshot.SkillState.Flights[0].Snapshot.Level=1;
        var converted=GameSession.Restore(c,oldSnapshot);Need(converted.Capture().SkillState.Flights[0].Snapshot.Level==8,"旧在途缺等级未按旧快照攻方补齐");

        string qa=Path.Combine(root,"artifacts/qa/formula-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"combat"));
        File.Copy(Path.Combine(root,"Idle-CRPG/config/combat/damage_rule.csv"),Path.Combine(qa,"combat/damage_rule.csv"));
        File.WriteAllText(Path.Combine(qa,"combat/armor_curve.csv"),"id,k\n1,0\n10000,10004\n");
        bool rejected=false;try{CombatRules.Load(qa,10000);}catch(InvalidDataException){rejected=true;}Need(rejected,"非正K配置未拒绝");
        File.WriteAllText(Path.Combine(qa,"combat/armor_curve.csv"),"id,k\n1,5\n5,9\n");
        rejected=false;try{CombatRules.Load(qa,10000);}catch(InvalidDataException){rejected=true;}Need(rejected,"未覆盖等级范围的K曲线未拒绝");
        Console.WriteLine("PASS: mixed HP/death/reward once, in-flight level snapshots/replay, versioned modifier migration/idempotence, compact checkpoints and invalid K configurations");
    }
}
