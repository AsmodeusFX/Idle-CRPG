using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillBdCorpseChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("BD_CORPSE: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        string QaDir()
        {
            string qa=Path.Combine(root,"artifacts/qa/bd-corpse-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
            foreach(var source in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill"),"*.csv"))File.Copy(source,Path.Combine(qa,"skill",Path.GetFileName(source)));
            foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"tests/fixtures/skill_bd_slice_c2"),"*.csv"))
            {string destination=Path.Combine(qa,"skill",Path.GetFileName(file));File.WriteAllText(destination,File.ReadAllText(destination).TrimEnd()+"\n"+string.Join('\n',File.ReadAllLines(file).Skip(1))+"\n");}
            return qa;
        }
        var definitions=SkillConfiguration.Load(QaDir(),c.Attributes);
        foreach(var entry in definitions.Skills.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Skills.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Effects.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Effects.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Summons.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Summons.Add(entry.Key,entry.Value);

        GameSession Battle(int needed)
        {
            for(int seed=900;seed<1100;seed++)
            {
                var game=new GameSession(c,seed,new[]{"priest","warrior","mage"});
                for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
                var room=game.Floor.Rooms[game.CurrentRoom];if(room.Monsters.Count!=needed)continue;
                var state=game.Capture().SkillState;
                state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();state.Corpses.Clear();
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
            throw new Exception("BD_CORPSE: 无法构造所需怪物数量");
        }
        void Tick(GameSession game,int count=1){for(int i=0;i<count;i++)game.Tick(.05f);}
        string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});
        int MonsterId(GameSession game,int index)=>game.Heroes.Count+index;
        // 用第二个英雄的普攻制造一次真实死亡事实，尸体 token 由统一死亡路径登记。
        void KillFirstMonster(GameSession game)
        {
            var room=game.Floor.Rooms[game.CurrentRoom];var victim=room.Monsters[0];
            victim.Position=room.Center+new Vector2(1,0);victim.Hp=1;
            game.Heroes[1].Timer=0;game.Heroes[1].Range=100;
            for(int i=0;i<40&&victim.Alive;i++)Tick(game);
            game.Heroes[1].Timer=999;
            Need(!victim.Alive,"普通攻击没有制造死亡事实");
        }

        // 击杀产生一具可消费尸体；尸爆以尸体坐标为圆心对范围内两个存活敌人各结算一次。
        var blast=Battle(3);var blastRoom=blast.Floor.Rooms[blast.CurrentRoom];
        blastRoom.HalfWidth=blastRoom.HalfHeight=5;
        blast.Heroes[0].SkillIds=new[]{"QA_CORPSE_EXPLOSION"};
        blastRoom.Monsters[1].Position=blastRoom.Center+new Vector2(2.5f,.3f);
        blastRoom.Monsters[2].Position=blastRoom.Center+new Vector2(2.5f,-.3f);
        KillFirstMonster(blast);
        var blastState=blast.Capture().SkillState;
        Need(blastState.Corpses.Count==1&&blastState.Corpses[0].ActorId==MonsterId(blast,0),"击杀没有登记唯一尸体权利");
        long beforeHits=blast.HitCount;
        Tick(blast,2);
        Need(blast.HitCount-beforeHits==2,"尸爆没有恰好命中两个存活敌人");
        Need(Math.Abs(blastRoom.Monsters[1].Hp-99990)<.0001f&&Math.Abs(blastRoom.Monsters[2].Hp-99990)<.0001f,"尸爆伤害没有按唯一伤害路径结算");
        Need(blastState.Corpses.Count==0,"尸爆没有原子消费尸体");
        var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(blast)).Session!);Need(Encode(blast)==Encode(copy),"尸爆后完整快照不能确定性恢复");
        Need(copy.Capture().SkillState.Corpses.Count==0,"已消费尸体在完整快照恢复后重新出现");

        // 没有尸体时不施放也不进入冷却；尸体技能只在本房有未消费尸体时有目标。
        var idle=Battle(1);var idleState=idle.Capture().SkillState;idle.Heroes[0].SkillIds=new[]{"QA_CORPSE_EXPLOSION"};
        long idleHits=idle.HitCount;Tick(idle,200);
        Need(idle.HitCount==idleHits,"无尸体时尸体技能仍造成伤害");
        var idleAction=idleState.Actions.FirstOrDefault(a=>a.Owner==0);
        Need(idleAction==null||!idleAction.Cooldowns.ContainsKey("QA_CORPSE_EXPLOSION"),"无尸体时尸体技能进入冷却");

        // 同一具尸体的两个待执行尸爆只有先执行者消费成功，竞争失败不产生第二条伤害路径。
        var race=Battle(3);var raceRoom=race.Floor.Rooms[race.CurrentRoom];
        raceRoom.HalfWidth=raceRoom.HalfHeight=5;
        raceRoom.Monsters[1].Position=raceRoom.Center+new Vector2(2.5f,.3f);
        raceRoom.Monsters[2].Position=raceRoom.Center+new Vector2(2.5f,-.3f);
        KillFirstMonster(race);
        var raceState=race.Capture().SkillState;
        Need(raceState.Corpses.Count==1,"竞争场景缺少尸体");
        for(int i=0;i<2;i++)raceState.Pending.Add(new(){Effect="QA_CORPSE_BLAST",Skill="QA_CORPSE_EXPLOSION",Owner=0,Target=MonsterId(race,0),Coefficient=1});
        Tick(race);
        Need(Math.Abs(raceRoom.Monsters[1].Hp-99990)<.0001f&&Math.Abs(raceRoom.Monsters[2].Hp-99990)<.0001f,"同一尸体被重复消费或竞争失败产生第二条伤害");
        Need(raceState.Corpses.Count==0,"竞争消费后尸体权利残留");

        // 完整快照保留本房未消费尸体；正式检查点清空，读档重开房间战斗。
        var keep=Battle(3);KillFirstMonster(keep);
        var keepState=keep.Capture().SkillState;Need(keepState.Corpses.Count==1,"快照场景缺少尸体");
        var keepCopy=GameSession.Restore(c,SaveStore.Decode(c,Encode(keep)).Session!);
        Need(keepCopy.Capture().SkillState.Corpses.Count==1&&keepCopy.Capture().SkillState.Corpses[0].ActorId==MonsterId(keep,0),"完整快照丢失本房尸体权利");
        Need(keep.CaptureProgress().SkillState.Corpses.Count==0,"正式检查点保存了尸体权利");

        // 女武神复用召唤运行时，与骷髅按定义分别计数并共享同一主人基础属性快照。
        var pair=Battle(3);pair.Heroes[0].SkillIds=new[]{"QA_RAISE_VALKYRIE","QA_SUMMON_SKELETON"};
        Tick(pair,300);
        var valkyrie=pair.Summons.SingleOrDefault(s=>s.SummonDefinitionId=="QA_VALKYRIE");
        var skeleton=pair.Summons.SingleOrDefault(s=>s.SummonDefinitionId=="QA_SKELETON");
        Need(valkyrie!=null&&skeleton!=null&&pair.Summons.Count(s=>s.Alive)==2,"女武神与骷髅没有按定义共存");
        Need(Math.Abs(valkyrie!.MaxHp-120)<.0001f&&Math.Abs(valkyrie.Attack-6)<.0001f,"女武神没有继承主人基础攻击/生命比例");
        Need(Math.Abs(skeleton!.MaxHp-100)<.0001f&&Math.Abs(skeleton.Attack-5)<.0001f,"骷髅召唤属性被女武神影响");
        Tick(pair,300);Need(pair.Summons.Count(s=>s.Alive)==2,"女武神与骷髅超过各自定义上限");

        // 配置拒绝：尸爆范围必须为正，尸体目标必须携带尸爆效果。
        bool Rejected(Action<string> mutate)
        {
            string dir=QaDir();mutate(dir);
            try{SkillConfiguration.Load(dir,c.Attributes);return false;}catch(Exception){return true;}
        }
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/effect.csv"),File.ReadAllText(Path.Combine(dir,"skill/effect.csv")).Replace("QA_CORPSE_BLAST,qa,CorpseExplosion,Physical,1,4","QA_CORPSE_BLAST,qa,CorpseExplosion,Physical,1,0"))),"尸爆范围非正未拒绝");
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/skill.csv"),File.ReadAllText(Path.Combine(dir,"skill/skill.csv")).Replace("QA_CORPSE_EXPLOSION,qa,ActiveSkill,100,0,0,1,4,QA_CORPSE_BLAST","QA_CORPSE_EXPLOSION,qa,ActiveSkill,100,0,0,1,4,QA_SUMMON_SKELETON_EFFECT"))),"尸体目标缺少尸爆效果未拒绝");

        var production=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        Need(!production.Skills.Skills.ContainsKey("QA_CORPSE_EXPLOSION")&&!production.Skills.Effects.ContainsKey("QA_CORPSE_BLAST")&&!production.Skills.Summons.ContainsKey("QA_VALKYRIE"),"QA尸体/女武神定义泄漏生产配置");
        Console.WriteLine("PASS: C2 corpse token from real kill, atomic corpse explosion consumption, no-corpse skip without cooldown, competing explosion, full snapshot retention vs formal checkpoint clearing, valkyrie+skeleton coexistence, invalid config rejection");
    }
}
