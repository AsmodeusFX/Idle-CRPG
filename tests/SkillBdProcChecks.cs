using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillBdProcChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("BD_PROC: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        string QaDir()
        {
            string qa=Path.Combine(root,"artifacts/qa/bd-proc-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
            foreach(var source in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill"),"*.csv"))File.Copy(source,Path.Combine(qa,"skill",Path.GetFileName(source)));
            foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"tests/fixtures/skill_bd_slice_c3"),"*.csv"))
            {string destination=Path.Combine(qa,"skill",Path.GetFileName(file));File.WriteAllText(destination,File.ReadAllText(destination).TrimEnd()+"\n"+string.Join('\n',File.ReadAllLines(file).Skip(1))+"\n");}
            return qa;
        }
        var definitions=SkillConfiguration.Load(QaDir(),c.Attributes);
        foreach(var entry in definitions.Skills.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Skills.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Effects.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Effects.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Buffs.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Buffs.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Triggers.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Triggers.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Chains.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Chains.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.ProcLimits.Where(e=>e.Key.StartsWith("QA_")))c.Skills.ProcLimits.Add(entry.Key,entry.Value);

        GameSession Battle(int needed)
        {
            for(int seed=900;seed<1100;seed++)
            {
                var game=new GameSession(c,seed,new[]{"priest","warrior","mage"});
                for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
                var room=game.Floor.Rooms[game.CurrentRoom];if(room.Monsters.Count!=needed)continue;
                var state=game.Capture().SkillState;
                state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();state.Corpses.Clear();state.ProcFires.Clear();
                for(int i=0;i<game.Heroes.Count;i++)
                {
                    var h=game.Heroes[i];h.Position=room.Center+new Vector2(i*.35f,0);h.Hp=h.MaxHp=200;h.Attack=10;h.Defense=0;h.Range=100;h.Timer=999;
                    h.Battle.CriticalChance=0;h.Battle.Dodge=0;h.SkillIds=Array.Empty<string>();h.StartingSkillGranted=true;
                }
                for(int i=0;i<room.Monsters.Count;i++)
                {
                    var m=room.Monsters[i];m.Position=room.Center+new Vector2(1.6f,i*.1f);m.Hp=m.MaxHp=100000;m.Defense=0;m.Range=100;m.Attack=0;m.Timer=999;
                    m.Battle.Dodge=0;m.Battle.BlockChance=0;m.Battle.LightningResistance=0;m.Battle.FireResistance=0;m.Battle.ColdResistance=0;m.Battle.PoisonResistance=0;
                    m.SkillIds=Array.Empty<string>();
                }
                return game;
            }
            throw new Exception("BD_PROC: 无法构造所需怪物数量");
        }
        void Tick(GameSession game,int count=1){for(int i=0;i<count;i++)game.Tick(.05f);}
        string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});

        // 持续旋风：周期Buff按自身当前位置每20 tick脉冲一次，范围内敌人只走唯一伤害路径，范围外不受影响。
        var whirl=Battle(3);var whirlRoom=whirl.Floor.Rooms[whirl.CurrentRoom];whirlRoom.HalfWidth=whirlRoom.HalfHeight=5;
        whirl.Heroes[0].SkillIds=new[]{"QA_WHIRLWIND_WINDOW"};
        whirl.Heroes[0].Position=whirlRoom.Center;
        whirlRoom.Monsters[0].Position=whirlRoom.Center+new Vector2(1.5f,0);
        whirlRoom.Monsters[1].Position=whirlRoom.Center+new Vector2(1.2f,1.2f);
        whirlRoom.Monsters[2].Position=whirlRoom.Center+new Vector2(3.8f,0);
        long beforeWhirl=whirl.HitCount;Tick(whirl,115);
        Need(whirl.Capture().SkillState.Buffs.All(b=>b.Id!="QA_WHIRLWIND_BUFF"),"旋风窗口到期后Buff仍残留");
        Need(whirl.HitCount-beforeWhirl==10,"旋风脉冲次数或范围错误");
        Need(Math.Abs(whirlRoom.Monsters[0].Hp-99975)<.0001f&&Math.Abs(whirlRoom.Monsters[1].Hp-99975)<.0001f,"旋风脉冲没有按统一伤害路径结算");
        Need(whirlRoom.Monsters[2].Hp==100000,"旋风命中了窗口范围外的敌人");
        long afterWhirl=whirl.HitCount;Tick(whirl,60);Need(whirl.HitCount==afterWhirl,"旋风窗口结束后继续脉冲");
        var whirlCopy=GameSession.Restore(c,SaveStore.Decode(c,Encode(whirl)).Session!);Need(Encode(whirl)==Encode(whirlCopy),"旋风窗口后完整快照不能确定性恢复");
        Need(whirl.CaptureProgress().SkillState.ProcFires.Count==0,"正式检查点保存了Proc限次现场");

        // 旋风以施法者当前位置为圆心：移动后后续脉冲命中新位置附近敌人，旧位置不再命中。
        var moving=Battle(3);var movingRoom=moving.Floor.Rooms[moving.CurrentRoom];movingRoom.HalfWidth=movingRoom.HalfHeight=5;
        moving.Heroes[0].SkillIds=new[]{"QA_WHIRLWIND_WINDOW"};moving.Heroes[0].Position=movingRoom.Center;
        movingRoom.Monsters[0].Position=movingRoom.Center+new Vector2(3.8f,0);
        movingRoom.Monsters[1].Position=movingRoom.Center+new Vector2(-4,4);
        movingRoom.Monsters[2].Position=movingRoom.Center+new Vector2(4,-4);
        Tick(moving,45);
        Need(movingRoom.Monsters[0].Hp==100000,"移动前的旋风错误命中远处敌人");
        moving.Heroes[0].Position=movingRoom.Center+new Vector2(2.2f,0);
        long beforeMove=moving.HitCount;Tick(moving,70);
        Need(Math.Abs(movingRoom.Monsters[0].Hp-99985)<.0001f,"旋风没有跟随施法者当前位置继续脉冲");
        Need(moving.HitCount-beforeMove==3,"旋风移动后脉冲次数错误");

        // Proc每Cast上限：一次连锁施法命中3个目标，声明上限2的触发只结算2次。
        var cast=Battle(3);var castRoom=cast.Floor.Rooms[cast.CurrentRoom];
        cast.Heroes[0].SkillIds=new[]{"QA_PROC_CAST_SKILL"};
        long beforeCast=cast.HitCount;Tick(cast,2);
        Need(cast.HitCount-beforeCast==5,"Proc每Cast上限没有限制派生伤害次数");
        Need(Math.Abs(castRoom.Monsters[0].Hp-99980)<.0001f&&Math.Abs(castRoom.Monsters[1].Hp-99990)<.0001f&&Math.Abs(castRoom.Monsters[2].Hp-99997.5f)<.0001f,"Proc派生伤害按目标或来源解析错误");
        var castState=cast.Capture().SkillState;
        Need(castState.ProcFires.Where(f=>f.Trigger=="QA_PROC_CAST_TRIGGER").Sum(f=>f.Count)==2,"Proc每Cast计数错误");
        var castCopy=GameSession.Restore(c,SaveStore.Decode(c,Encode(cast)).Session!);Need(Encode(cast)==Encode(castCopy),"Proc限次后完整快照不能确定性恢复");

        // Proc每目标上限：同一目标在一次施法内被两个伤害效果命中，只允许触发一次。
        var same=Battle(1);var sameRoom=same.Floor.Rooms[same.CurrentRoom];
        same.Heroes[0].SkillIds=new[]{"QA_PROC_TARGET_SKILL"};
        long beforeSame=same.HitCount;Tick(same,2);
        Need(same.HitCount-beforeSame==3,"Proc每目标上限没有限制同目标重复触发");
        Need(Math.Abs(sameRoom.Monsters[0].Hp-99970)<.0001f,"Proc每目标限次伤害结算错误");
        Need(same.Capture().SkillState.ProcFires.Where(f=>f.Trigger=="QA_PROC_TARGET_TRIGGER").Sum(f=>f.Count)==1,"Proc每目标计数错误");

        // 配置拒绝：限次必须引用已声明触发、至少一项生效；AreaPulse范围必须为正。
        bool Rejected(Action<string> mutate)
        {
            string dir=QaDir();mutate(dir);
            try{SkillConfiguration.Load(dir,c.Attributes);return false;}catch(Exception){return true;}
        }
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/proc_limit.csv"),File.ReadAllText(Path.Combine(dir,"skill/proc_limit.csv")).Replace("QA_PROC_CAST_TRIGGER,2,0","QA_MISSING_TRIGGER,2,0"))),"Proc限次引用未知触发未拒绝");
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/proc_limit.csv"),File.ReadAllText(Path.Combine(dir,"skill/proc_limit.csv")).Replace("QA_PROC_TARGET_TRIGGER,0,1","QA_PROC_TARGET_TRIGGER,0,0"))),"Proc限次全为0未拒绝");
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/effect.csv"),File.ReadAllText(Path.Combine(dir,"skill/effect.csv")).Replace("QA_WHIRLWIND_PULSE,qa,AreaPulse,Physical,0.5,2","QA_WHIRLWIND_PULSE,qa,AreaPulse,Physical,0.5,0"))),"旋风脉冲范围非正未拒绝");

        var production=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        Need(!production.Skills.ProcLimits.ContainsKey("QA_PROC_CAST_TRIGGER")&&!production.Skills.Skills.ContainsKey("QA_WHIRLWIND_WINDOW")&&!production.Skills.Effects.ContainsKey("QA_WHIRLWIND_PULSE"),"QA旋风/Proc定义泄漏生产配置");
        Console.WriteLine("PASS: C3 sustained whirlwind window at caster position with periodic pulses and expiry, proc per-cast cap, proc per-target cap, direct-hit filter without self-recursion, snapshot round-trip, formal checkpoint clearing, invalid config rejection");
    }
}
