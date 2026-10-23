using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>P0：召唤物接敌容量。骷髅/女武神按近战前排定位配 1，三只骷髅必须提供 3 点普通容量。</summary>
public static class SummonCapacityChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("SUMMON_CAPACITY: "+why);}
        // 接敌容量是生产数值，必须用正式配置验证；旧技能基线会覆盖 summon.csv，不代表线上行为。
        var c=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));

        foreach(var id in new[]{"nec_skeleton","ran_valkyrie"})
        {
            var definition=c.Skills.Summons[id];
            Need(definition.EngagementCapacity==1,"召唤定义容量不是1："+id);
            Need(!definition.RangedCombat,"容量1的前提是近战前排定位："+id);
        }

        GameSession Battle()
        {
            // 死灵/法师自身普通容量为0，便于验证怪物只能靠召唤物的容量承接。
            var game=new GameSession(c,932,new[]{"necromancer","mage"});
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            var state=game.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
            foreach(var hero in game.Heroes)
            {
                hero.Position=room.Center+new Vector2(room.HalfWidth-.6f,room.HalfHeight-.6f);
                hero.Hp=hero.MaxHp=100000;hero.Attack=0;hero.Range=1.5f;hero.Timer=999;hero.SkillIds=Array.Empty<string>();
            }
            game.Heroes[0].SkillIds=new[]{"nec_raise_skeleton"};
            // 走图过程中自动补召过骷髅，先清空现场再布置三只测试召唤物。
            game.Summons.Clear();
            for(int i=0;i<3;i++)
            {
                var summon=game.Heroes[0].Copy();
                summon.Name="骷髅"+i;summon.SummonDefinitionId="nec_skeleton";summon.SummonSkill="nec_raise_skeleton";
                summon.SummonOwner=0;summon.Hp=summon.MaxHp=1000;summon.Attack=0;summon.Range=.9f;summon.Timer=999;
                summon.Position=room.Center+new Vector2(-.6f+i*.6f,0);
                game.Summons.Add(summon);
            }
            int index=0;
            foreach(var monster in room.Monsters)
            {
                monster.Position=room.Center+new Vector2(-2.2f,(index++%3)*.4f);monster.Hp=monster.MaxHp=100000;
                monster.Attack=0;monster.Defense=0;monster.Range=.7f;monster.Ranged=false;monster.Timer=999;monster.Speed=.0001f;
            }
            // 锁住补召冷却，避免自动补员干扰“死亡释放容量”的断言。
            var action=state.Actions.FirstOrDefault(a=>a.Owner==0);
            if(action==null){action=new SkillActionState{Owner=0};state.Actions.Add(action);}
            action.Cooldowns["nec_raise_skeleton"]=999;
            return game;
        }

        var game=Battle();
        var live=game.Summons.Where(s=>s.Alive).ToArray();
        Need(live.Length==3,"召唤数量不是3：total="+game.Summons.Count+" hp="+string.Join('/',game.Summons.Select(s=>s.Hp))+" owner="+string.Join('/',game.Summons.Select(s=>s.SummonOwner)));
        Need(live.Sum(game.EngagementCapacity)==3,"三只骷髅未提供3点容量");
        Need(live.All(s=>game.EngagementCapacity(s)==1),"单只骷髅容量不是1");

        int FriendlyIndex(Actor actor)=>game.Heroes.Count+game.Summons.IndexOf(actor);

        // 让怪物完成一次正常索敌。
        for(int i=0;i<40;i++)game.Tick(.05f);
        var summonIndices=live.Select(FriendlyIndex).ToHashSet();
        var locks=game.Capture().SkillState.Targeting.Locks;
        int normalOnSummons=locks.Count(l=>l.Kind==MonsterLockKind.Normal&&summonIndices.Contains(l.Target));
        Need(normalOnSummons>=1,"普通近战怪没有以骷髅为主锁");
        Need(normalOnSummons<=3,"普通预约超过三只骷髅的总容量");
        foreach(var index in summonIndices)
        {
            int normal=locks.Count(l=>l.Kind==MonsterLockKind.Normal&&l.Target==index);
            Need(normal<=1,"单只骷髅承接了超过1个普通预约");
        }

        // 死亡释放：击杀一只骷髅后总容量降为2，且指向它的锁被清理。
        var doomed=live[0];int doomedIndex=FriendlyIndex(doomed);
        doomed.Hp=0;
        for(int i=0;i<10;i++)game.Tick(.05f);
        var remaining=game.Summons.Where(s=>s.Alive).ToArray();
        Need(remaining.Length==2,"死亡骷髅未被清理");
        Need(remaining.Sum(game.EngagementCapacity)==2,"死亡后总容量没有降为2");
        var after=game.Capture().SkillState.Targeting.Locks;
        Need(!after.Any(l=>l.Target==doomedIndex),"死亡召唤物的锁未释放");
        var remainingIndices=remaining.Select(FriendlyIndex).ToHashSet();
        Need(after.Count(l=>l.Kind==MonsterLockKind.Normal&&remainingIndices.Contains(l.Target))<=2,"死亡后普通预约超过剩余容量");

        // 重新召唤补齐：回到3只时容量恢复3。
        var extra=game.Heroes[0].Copy();
        extra.Name="骷髅补";extra.SummonDefinitionId="nec_skeleton";extra.SummonSkill="nec_raise_skeleton";extra.SummonOwner=0;
        extra.Hp=extra.MaxHp=1000;extra.Attack=0;extra.Range=.9f;extra.Timer=999;
        extra.Position=game.Floor.Rooms[game.CurrentRoom].Center;
        game.Summons.Add(extra);
        Need(game.Summons.Where(s=>s.Alive).Sum(game.EngagementCapacity)==3,"补召后容量未恢复3");

        // 女武神超时释放：限时召唤到期必须撤销并让出容量，而不是只改CSV数值。
        var valkyrie=game.Heroes[0].Copy();
        valkyrie.Name="女武神";valkyrie.SummonDefinitionId="ran_valkyrie";valkyrie.SummonSkill="ran_valkyrie_summon";
        valkyrie.SummonOwner=0;valkyrie.Hp=valkyrie.MaxHp=1000;valkyrie.Attack=0;valkyrie.Range=.9f;valkyrie.Timer=999;
        valkyrie.SummonRemaining=5;valkyrie.Position=game.Floor.Rooms[game.CurrentRoom].Center;
        game.Summons.Add(valkyrie);
        Need(game.Summons.Where(s=>s.Alive).Sum(game.EngagementCapacity)==4,"女武神存活时容量未增加");
        int valkyrieIndex=FriendlyIndex(valkyrie);
        for(int i=0;i<140;i++)game.Tick(.05f);
        Need(!game.Summons.Any(s=>ReferenceEquals(s,valkyrie)&&s.Alive),"女武神超时未被撤销");
        Need(game.Summons.Where(s=>s.Alive).Sum(game.EngagementCapacity)==3,"女武神超时后容量未回到3");
        Need(!game.Capture().SkillState.Targeting.Locks.Any(l=>l.Target==valkyrieIndex),"女武神超时后锁未释放");

        Console.WriteLine("PASS: skeleton/valkyrie capacity=1, three skeletons give 3 normal slots, melee monsters take skeleton slots without exceeding per-unit capacity, death releases locks and lowers capacity, refill restores capacity, timed valkyrie expiry releases its slot");
    }
}
