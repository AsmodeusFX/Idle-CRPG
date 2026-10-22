using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillBdChainChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("BD_CHAIN: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        string QaDir()
        {
            string qa=Path.Combine(root,"artifacts/qa/bd-chain-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
            foreach(var source in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill"),"*.csv"))File.Copy(source,Path.Combine(qa,"skill",Path.GetFileName(source)));
            foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"tests/fixtures/skill_bd_slice_c1"),"*.csv"))
            {string destination=Path.Combine(qa,"skill",Path.GetFileName(file));File.WriteAllText(destination,File.ReadAllText(destination).TrimEnd()+"\n"+string.Join('\n',File.ReadAllLines(file).Skip(1))+"\n");}
            return qa;
        }
        var definitions=SkillConfiguration.Load(QaDir(),c.Attributes);
        foreach(var entry in definitions.Skills.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Skills.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Effects.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Effects.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Buffs.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Buffs.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Chains.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Chains.Add(entry.Key,entry.Value);

        GameSession Battle(int needed)
        {
            for(int seed=900;seed<1100;seed++)
            {
                var game=new GameSession(c,seed,new[]{"priest","warrior","mage"});
                for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
                var room=game.Floor.Rooms[game.CurrentRoom];if(room.Monsters.Count<needed)continue;
                var state=game.Capture().SkillState;
                state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
                for(int i=0;i<game.Heroes.Count;i++)
                {
                    var h=game.Heroes[i];h.Position=room.Center+new Vector2(i*.35f,0);h.Hp=h.MaxHp=200;h.Attack=10;h.Defense=0;h.Range=100;h.Timer=999;
                    h.Battle.CriticalChance=0;h.Battle.Dodge=0;h.SkillIds=Array.Empty<string>();h.StartingSkillGranted=true;
                }
                for(int i=0;i<room.Monsters.Count;i++)
                {
                    var m=room.Monsters[i];m.Position=room.Center+new Vector2(1.6f,i*.1f);m.Hp=m.MaxHp=100000;m.Defense=0;m.Range=100;m.Attack=0;m.Timer=999;
                    m.Battle.Dodge=0;m.Battle.BlockChance=0;m.Battle.LightningResistance=0;
                }
                return game;
            }
            throw new Exception("BD_CHAIN: 无法构造所需怪物数量");
        }
        void Tick(GameSession game,int count=1){for(int i=0;i<count;i++)game.Tick(.05f);}
        string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});
        SkillBuffState[] Buffs(GameSession game)=>game.Capture().SkillState.Buffs.ToArray();

        // 三目标链：11.6→11.7→11.8 的确定性顺序，系数按 1/0.5/0.25 衰减。
        var g=Battle(3);var room=g.Floor.Rooms[g.CurrentRoom];var monsters=room.Monsters;
        g.Heroes[0].SkillIds=new[]{"QA_CHAIN_LIGHTNING"};long hits=g.HitCount;
        Tick(g,5);
        Need(g.HitCount-hits==3,"连锁没有命中三个目标");
        Need(Math.Abs(monsters[0].Hp-99990)<.0001f&&Math.Abs(monsters[1].Hp-99995)<.0001f&&Math.Abs(monsters[2].Hp-99997.5f)<.0001f,"连锁逐跳衰减或雷电抗性解析错误");
        Need(g.LastDamage!.OriginCast>0,"连锁伤害没有继承同一次施法来源");
        var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(g)).Session!);Need(Encode(g)==Encode(copy),"连锁后完整快照不能确定性恢复");

        // 目标上限：最多 3 个不同目标，第四个不受伤。
        var capped=Battle(4);var cappedRoom=capped.Floor.Rooms[capped.CurrentRoom];capped.Heroes[0].SkillIds=new[]{"QA_CHAIN_LIGHTNING"};Tick(capped,5);
        Need(cappedRoom.Monsters[3].Hp==100000&&cappedRoom.Monsters[0].Hp<100000&&cappedRoom.Monsters[1].Hp<100000&&cappedRoom.Monsters[2].Hp<100000,"连锁超过目标上限或命中重复目标");

        // 跳跃距离不足时自然终止，不跨距离拉怪。
        var shortHop=Battle(3);var hopRoom=shortHop.Floor.Rooms[shortHop.CurrentRoom];hopRoom.Monsters[2].Position=hopRoom.Center+new Vector2(1.6f,2f);
        shortHop.Heroes[0].SkillIds=new[]{"QA_CHAIN_LIGHTNING"};Tick(shortHop,5);
        Need(hopRoom.Monsters[0].Hp<100000&&hopRoom.Monsters[1].Hp<100000&&hopRoom.Monsters[2].Hp==100000,"连锁忽略跳跃距离或继续命中超距目标");

        // 单目标时链自然结束，不重复命中同一单位。
        var single=Battle(1);single.Heroes[0].SkillIds=new[]{"QA_CHAIN_LIGHTNING"};long singleHits=single.HitCount;Tick(single,5);
        Need(single.HitCount-singleHits==1&&single.Floor.Rooms[single.CurrentRoom].Monsters[0].Hp==99990,"单目标连锁重复命中");

        // 链根死亡后继续跳到下一目标，击杀只结算一次经验/击杀事实。
        var kill=Battle(2);var killRoom=kill.Floor.Rooms[kill.CurrentRoom];killRoom.Monsters[0].Hp=1;var killMonster=killRoom.Monsters[0];
        kill.Heroes[0].SkillIds=new[]{"QA_CHAIN_LIGHTNING"};int kills=kill.Kills;Tick(kill,5);
        Need(!killMonster.Alive&&kill.Kills==kills+1&&killRoom.Monsters[1].Hp<100000,"链根死亡后没有继续跳跃或重复结算击杀");

        // 雷电抗性只进入目标防御解析，不重复作为易伤。
        var resistant=Battle(3);var resistantRoom=resistant.Floor.Rooms[resistant.CurrentRoom];resistantRoom.Monsters[0].Battle.LightningResistance=.5;
        resistant.Heroes[0].SkillIds=new[]{"QA_CHAIN_LIGHTNING"};Tick(resistant,5);
        Need(Math.Abs(resistantRoom.Monsters[0].Hp-99995)<.0001f,"连锁伤害没有按目标抗性解析");

        // 信念光环复用既有光环生命周期，减抗只作用于范围内敌人，卸槽立即撤销。
        var conviction=Battle(1);var convictionRoom=conviction.Floor.Rooms[conviction.CurrentRoom];var resisted=convictionRoom.Monsters[0];resisted.Battle.LightningResistance=.2;
        conviction.Heroes[0].SkillIds=new[]{"QA_CONVICTION","QA_CHAIN_LIGHTNING"};Tick(conviction,8);
        Need(Buffs(conviction).Any(b=>b.Target==conviction.Heroes.Count&&b.Id=="QA_CONVICTION_BUFF"),"信念光环没有对范围内敌人施加独立减抗来源");
        Need(Math.Abs(resisted.Hp-99989)<.0001f,"信念减抗没有进入连锁伤害的目标防御解析");
        conviction.EquipSkills("priest",Array.Empty<string>());
        Need(!conviction.Heroes[0].ActiveAuraSkills.Contains("QA_CONVICTION")&&!Buffs(conviction).Any(b=>b.Id=="QA_CONVICTION_BUFF"),"卸槽后信念光环或减抗来源残留");
        var outside=Battle(1);var outsideRoom=outside.Floor.Rooms[outside.CurrentRoom];outsideRoom.HalfWidth=outsideRoom.HalfHeight=5;
        outside.Heroes[0].SkillIds=new[]{"QA_CONVICTION"};Tick(outside,6);
        outside.Heroes[0].Position=outsideRoom.Center+new Vector2(4.4f,4.4f);Tick(outside);
        Need(!Buffs(outside).Any(b=>b.Id=="QA_CONVICTION_BUFF"),"离开信念范围后减抗来源未撤销");

        // 配置拒绝：目标数、衰减与引用都必须有界且有效。
        bool Rejected(Action<string> mutate)
        {
            string dir=QaDir();mutate(dir);
            try{SkillConfiguration.Load(dir,c.Attributes);return false;}catch(Exception){return true;}
        }
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/chain.csv"),File.ReadAllText(Path.Combine(dir,"skill/chain.csv")).Replace("QA_CHAIN,QA_CHAIN,3,0.5,0.5","QA_CHAIN,QA_CHAIN,0,0.5,0.5"))),"非法连锁目标数未拒绝");
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/chain.csv"),File.ReadAllText(Path.Combine(dir,"skill/chain.csv")).Replace("QA_CHAIN,QA_CHAIN,3,0.5,0.5","QA_CHAIN,QA_CHAIN,3,0.5,1.5"))),"非法连锁衰减未拒绝");
        Need(Rejected(dir=>File.WriteAllText(Path.Combine(dir,"skill/effect.csv"),File.ReadAllText(Path.Combine(dir,"skill/effect.csv")).Replace("Lightning,1,,,,,,,,,,QA_CHAIN","Lightning,1,,,,,,,,,,QA_MISSING"))),"无效连锁引用未拒绝");

        Need(!LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config")).Skills.Chains.ContainsKey("QA_CHAIN"),"QA连锁定义泄漏生产配置");
        Console.WriteLine("PASS: C1 bounded chain root/jump/decay, target cap and jump range, no duplicate targets, kill continuation, resistance routing, conviction aura composition and cleanup, invalid config rejection");
    }
}
