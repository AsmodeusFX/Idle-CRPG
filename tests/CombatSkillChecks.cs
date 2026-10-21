using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class CombatSkillChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception("COMBAT_SKILL_CHECK: "+reason);}
        void Near(double actual,double expected,string reason)=>Check(Math.Abs(actual-expected)<.00001,reason+$" ({actual} != {expected})");
        var c=Config.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        Check(c.Skills.Abilities.Count==15&&c.Skills.Buffs.Keys.Count(id=>!id.StartsWith("talent_"))==7&&c.Skills.Triggers.Count==2&&c.Skills.Skills.Keys.Count(id=>!c.Talents.Nodes.ContainsKey(id))==17,"迁移定义数量");
        Check(c.Skills.Skills["skill_01"].Cooldown==25&&c.Skills.Buffs["buff_haste"].Duration==120,"8Hz→20Hz 换算");
        var attack=new BattleAttributes {CriticalChance=1,CriticalDamage=.5};
        attack.Modifiers.AddRange(new[]{new DamageModifier(DamageBucket.Skill,.3,"spin"),new(DamageBucket.General,.3,Type:DamageType.Fire),new(DamageBucket.General,.3,RangedOnly:true),new(DamageBucket.General,.3,BossOnly:true),new(DamageBucket.Critical,.2),new(DamageBucket.Multiplier,1.5,"spin"),new(DamageBucket.Multiplier,2),new(DamageBucket.Vulnerable,.2,DebuffId:"vulnerable"),new(DamageBucket.Vulnerable,.3,DebuffId:"vulnerable"),new(DamageBucket.Vulnerable,.4,DebuffId:"wounded"),new(DamageBucket.Vulnerable,99,DebuffId:"absent"),new(DamageBucket.Skill,100,"other_skill")});
        var defense=new BattleAttributes {BlockChance=1,BlockReduction=.5};
        var input=new DamageInput("spin",100,20,3,DamageType.Fire,true,true,attack,defense,new HashSet<string>{"vulnerable","wounded"});
        var result=DamagePipeline.Resolve(input,c.CombatRules,new(123));
        Near(result.Dmg1,80,"DMG1 减法");Near(result.Skill,3.3,"300%+30%=330%");Near(result.General,1.9,"通用条件同池加算");Near(result.Critical,2.2,"暴击加算");Near(result.Multiplier,3,"独立倍率叠乘");Near(result.Vulnerable,2.1,"同 Debuff 加算、不同 Debuff 叠乘");Near(result.FinalDamage,3476.088,"完整五池算例");
        Check(result.Dmg2.Blocked&&result.Dmg2.Critical,"格挡和暴击可同时成立");
        var plain=input with {Attacker=new(),Defender=new(),Type=DamageType.Cold,Ranged=false,Boss=false};
        Near(DamagePipeline.Resolve(plain,c.CombatRules,new(123)).FinalDamage,240,"所有类型统一先减护甲、不应用抗性");
        Near(DamagePipeline.Resolve(plain with {Attack=1,Armor=100},c.CombatRules,new(123)).FinalDamage,3,"保底在 DMG1 而非最终伤害");
        var dodgeRng=new PersistentRandom(123);uint before=dodgeRng.State;
        var dodged=DamagePipeline.Resolve(input with {Defender=new(){Dodge=1,BlockChance=1}},c.CombatRules,dodgeRng);
        Check(dodged.Dmg2.Dodged&&!dodged.Dmg2.Critical&&!dodged.Dmg2.Blocked&&dodged.FinalDamage==0&&dodgeRng.State==before,"闪避短路且零/满概率不耗随机数");
        var expectedRng=new PersistentRandom(932701);double dodgeRoll=expectedRng.NextDouble(),blockRoll=expectedRng.NextDouble(),critRoll=expectedRng.NextDouble();
        var odds=DamagePipeline.Resolve(plain with {Attacker=new(){CriticalChance=.4},Defender=new(){Dodge=dodgeRoll/2,BlockChance=.6}},c.CombatRules,new(932701));
        Check(!odds.Dmg2.Dodged&&odds.Dmg2.Blocked==(blockRoll<.6)&&odds.Dmg2.Critical==(critRoll<.4),"闪避→格挡→暴击 RNG 顺序");
        bool rejected=false;try{DamagePipeline.Resolve(input with {Attacker=new(){Modifiers=new(){new(DamageBucket.Vulnerable,.2)}}},c.CombatRules,new(1));}catch(InvalidDataException){rejected=true;}
        Check(rejected,"易伤缺少稳定 DebuffId 未拒绝");

        c.Spawn=c.Spawn with {Min=4,Max=4};
        GameSession Battle(params string[] skills)
        {
            var g=new GameSession(c,785,new[]{"warrior","ranger","mage"});
            for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];
            for(int i=0;i<g.Heroes.Count;i++){var h=g.Heroes[i];h.Hp=h.MaxHp=10000;h.Attack=10;h.Timer=999;h.Defense=0;h.Position=room.Center+new Vector2(-1+i*.1f,0);}
            for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Hp=m.MaxHp=100000;m.Timer=999;m.Attack=10;m.Defense=0;m.Position=room.Center+new Vector2(0,1+i*.1f);}
            g.EquipSkills("warrior",skills);return g;
        }
        void Tick(GameSession game,int ticks){for(int i=0;i<ticks;i++)game.Tick(.05f);}
        string State(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});
        GameSession Copy(GameSession game)=>GameSession.Restore(c,SaveStore.Decode(c,State(game)).Session!);

        var none=GameSession.CreateWorld(c,1,new[]{"warrior","ranger","mage"});Check(none.Heroes.All(h=>h.SkillIds.Length==0),"迁移自动投放了技能");
        foreach(var ability in c.Skills.Abilities.Values)
        {
            var game=Battle(ability.Id);Tick(game,5);
            Check(game.Capture().SkillState.Actions.Single().Cooldowns.ContainsKey(ability.Id),"技能未 Commit："+ability.Id);
            Tick(game,80);
            var state=game.Capture().SkillState;
            if(ability.Form=="buff")Check(state.Buffs.Any(b=>ability.Buffs.Contains(b.Id)),"自身 Buff 未生效："+ability.Id);
            else Check(game.Floor.Rooms[game.CurrentRoom].Monsters.Any(m=>m.Hp<100000),"二维输出技能未命中："+ability.Id);
        }
        var cast=Battle("skill_01");Tick(cast,5);var actions=cast.Capture().SkillState.Actions;
        Check(actions.Count==1&&actions[0].Cooldowns.Count==1,"同一角色出现多个动作槽");
        var targetBefore=cast.Floor.Rooms[cast.CurrentRoom].Monsters.Sum(m=>m.Hp);var copy=Copy(cast);
        Tick(cast,30);Tick(copy,30);Check(State(cast)==State(copy),"在途技能完整快照不能确定性恢复");
        Check(cast.Floor.Rooms[cast.CurrentRoom].Monsters.Sum(m=>m.Hp)<targetBefore,"二维直射没有伤害");

        var delayed=Battle("skill_07");Tick(delayed,5);long hits=delayed.HitCount;Tick(delayed,5);Check(delayed.HitCount==hits,"延迟雷击过早命中");Tick(delayed,15);
        Check(delayed.Capture().SkillState.Buffs.Any(b=>b.Id=="buff_stun"),"延迟雷击没有眩晕");
        var fire=Battle("skill_02");Tick(fire,40);
        var fireState=fire.Capture().SkillState;Check(fireState.Flights.Any(f=>f.Form=="ground")&&fireState.Buffs.Any(b=>b.Id=="buff_burn"),"火雨落地没有转为火海/灼烧");
        var fireCopy=Copy(fire);Tick(fire,20);Tick(fireCopy,20);Check(State(fire)==State(fireCopy),"区域与 DoT 快照续接不一致");
        var compact=fire.CaptureProgress();Check(compact.SkillState.Actions.Count==0&&compact.SkillState.Buffs.Count==0&&compact.SkillState.Flights.Count==0,"正式存档保留了战斗现场");
        var restarted=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=compact})).Session!);
        Check(restarted.ActiveBuffCount==0&&restarted.ActiveFlightCount==0&&restarted.Heroes[0].SkillIds.SequenceEqual(new[]{"skill_02"}),"进房重开未清理临时技能状态或丢失装备");

        var shield=Battle("skill_14");Tick(shield,5);var shieldState=shield.Capture().SkillState;Check(shieldState.Buffs.Single(b=>b.Id=="buff_shield").Value==20,"护盾不是施放 ATK×2");
        shield.EquipSkills("warrior",Array.Empty<string>());Check(shield.ActiveBuffCount==0&&shield.ActiveFlightCount==0,"换技能没有清理监听/状态");
        var absorption=Battle("skill_14");absorption.Heroes[1].Hp=absorption.Heroes[2].Hp=0;Tick(absorption,5);
        var aggressor=absorption.Floor.Rooms[absorption.CurrentRoom].Monsters[0];aggressor.Attack=10;aggressor.Range=100;aggressor.Cooldown=100;
        aggressor.Timer=0;Tick(absorption,1);Check(absorption.Heroes[0].Hp==10000&&absorption.Capture().SkillState.Buffs.Single(b=>b.Id=="buff_shield").Value==10,"护盾没有优先吸收伤害");
        aggressor.Timer=0;Tick(absorption,1);Check(absorption.ActiveBuffCount==0&&absorption.Heroes[0].Hp==10000,"护盾破裂没有即时移除");
        aggressor.Timer=0;Tick(absorption,1);Check(absorption.Heroes[0].Hp==9990,"破盾后伤害仍被旧实例拦截");
        var mirror=Battle("skill_19","skill_01");Tick(mirror,16);
        Check(mirror.Capture().SkillState.Flights.Any(f=>f.Mirrored)&&mirror.ActiveFlightCount<8,"分身未复制首效果或发生递归");
        var frenzy=Battle("skill_09","skill_01");frenzy.Heroes[0].Battle.CriticalChance=1;Tick(frenzy,25);
        Check(frenzy.Capture().SkillState.Buffs.Any(b=>b.Id=="buff_crit"),"暴击触发 Buff 丢失");
        var scoped=Battle("skill_15");var hidden=scoped.Floor.Rooms.First(r=>!r.Revealed);var hiddenEnemy=Actor.From(c.Monsters[0]);hiddenEnemy.Hp=hiddenEnemy.MaxHp=100000;hiddenEnemy.Position=scoped.Floor.Rooms[scoped.CurrentRoom].Center;hidden.Monsters.Add(hiddenEnemy);Tick(scoped,80);
        Check(hiddenEnemy.Hp==100000&&!hidden.Revealed,"全场技能越过当前房间边界");
        var cancel=Battle("skill_01");Tick(cancel,1);var slot=cancel.Capture().SkillState.Actions.Single();cancel.Floor.Rooms[cancel.CurrentRoom].Monsters[slot.Target-cancel.Heroes.Count].Hp=0;Tick(cancel,3);
        Check(!slot.Cooldowns.ContainsKey("skill_01"),"前摇目标失效仍扣除冷却");

        var duration=Battle("skill_04");Tick(duration,5);var lasting=duration.Capture().SkillState.Buffs.Single();Near(lasting.Remaining,5.95,"6 秒 Buff 在 Commit 后经过一 Tick");Tick(duration,121);Check(duration.ActiveBuffCount==0,"Buff 到期未撤销");
        var roomClear=Battle("skill_19");Tick(roomClear,5);foreach(var m in roomClear.Floor.Rooms[roomClear.CurrentRoom].Monsters)m.Hp=0;
        for(int i=0;i<2000&&roomClear.CurrentRoom==1;i++)roomClear.Tick(.05f);
        // 不论此随机布局的首房编号，正式检查点始终不保留临时监听。
        Check(roomClear.CaptureProgress().SkillState.Buffs.Count==0,"房间检查点残留 Buff");
        string qa=Path.Combine(root,"artifacts/qa/skill-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
        foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill")))File.Copy(file,Path.Combine(qa,"skill",Path.GetFileName(file)));
        string skillFile=Path.Combine(qa,"skill/skill.csv"),original=File.ReadAllText(skillFile);File.WriteAllText(skillFile,original.Replace("cast_skill_01","missing_effect"));
        rejected=false;try{SkillConfiguration.Load(qa,c.Attributes);}catch(InvalidDataException){rejected=true;}Check(rejected,"悬空效果引用没有拒绝");
        File.WriteAllText(skillFile,original);string fxFile=Path.Combine(qa,"skill/effect.csv"),fxText=File.ReadAllText(fxFile);File.WriteAllText(fxFile,fxText.Replace("AbilityCast","NotImplemented"));
        rejected=false;try{SkillConfiguration.Load(qa,c.Attributes);}catch(CsvConfigurationException){rejected=true;}Check(rejected,"未知效果类型没有拒绝");
        Console.WriteLine("PASS: DMG1/2/3 five-pool arithmetic, event order, all 15 skills, 2D damage, buff/trigger lifecycle, source conversion, exact runtime saves and checkpoint reset");
    }
}
