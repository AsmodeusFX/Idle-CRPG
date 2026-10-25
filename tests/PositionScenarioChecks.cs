using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>P0 第2轮站位开发输入：五英雄+三骷髅在五类敌人配置下的确定性位置与目标关系。</summary>
public static class PositionScenarioChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("POSITION_SCENARIO: "+why);}
        var c=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));
        string[] party={"warrior","ranger","mage","necromancer"};

        (GameSession Game,Room Room) Scene(string name,int melee,int ranged,bool boss,bool hunter,bool area)
        {
            var game=new GameSession(c,4242,party);
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            var state=game.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
            game.Summons.Clear();
            for(int i=0;i<game.Heroes.Count;i++)
            {
                var hero=game.Heroes[i];
                hero.Position=room.Center+new Vector2(-room.HalfWidth+.9f,-.6f+i*.3f);
                hero.Hp=hero.MaxHp=100000;hero.Attack=10;hero.Timer=0;
            }
            game.Heroes.Last().SkillIds=new[]{"nec_raise_skeleton"};
            for(int i=0;i<3;i++)
            {
                var skeleton=game.Heroes.Last().Copy();
                skeleton.Name="骷髅"+(i+1);skeleton.SummonDefinitionId="nec_skeleton";skeleton.SummonSkill="nec_raise_skeleton";
                skeleton.SummonOwner=game.Heroes.Count-1;skeleton.Hp=skeleton.MaxHp=500;skeleton.Attack=8;skeleton.Range=.9f;skeleton.Timer=0;
                skeleton.Position=game.Heroes.Last().Position+new Vector2(.3f+i*.25f,.4f);
                game.Summons.Add(skeleton);
            }
            var monsters=room.Monsters.OrderBy(m=>m.Timer).ToArray();
            int total=Math.Max(1,melee+ranged);
            for(int i=0;i<monsters.Length;i++)
            {
                var monster=monsters[i];bool use=i<total;
                monster.Hp=monster.MaxHp=use?(boss&&i==0?200000:100000):0;
                monster.Attack=use?6:0;monster.Defense=0;monster.Timer=999;monster.Speed=1.6f;
                monster.Ranged=use&&i>=melee;
                monster.TargetPolicy=hunter&&i==0?MonsterTargetPolicy.BacklinePriority:MonsterTargetPolicy.Ordinary;
                monster.SkillIds=area&&i==0?new[]{"mob_area_pulse"}:Array.Empty<string>();
                monster.Position=room.Center+new Vector2(room.HalfWidth-.9f,-.6f+i*.35f);
            }
            for(int i=0;i<220;i++)game.Tick(.05f);
            Console.WriteLine($"--- {name} ---");
            foreach(var hero in game.Heroes)
                Console.WriteLine($"  英雄 {hero.Name,-4} 位置({hero.Position.X:0.00},{hero.Position.Y:0.00}) 容量{game.EngagementCapacity(hero)} 目标{Target(game.Heroes.IndexOf(hero))}");
            foreach(var summon in game.Summons)
                Console.WriteLine($"  召唤 {summon.Name,-4} 位置({summon.Position.X:0.00},{summon.Position.Y:0.00}) 容量{game.EngagementCapacity(summon)} 存活{summon.Alive}");
            foreach(var monster in room.Monsters.Where(m=>m.Hp>0).Take(6))
                Console.WriteLine($"  怪物 {(monster.Ranged?"远程":"近战")} 位置({monster.Position.X:0.00},{monster.Position.Y:0.00}) 策略{monster.TargetPolicy}");
            Need(room.Monsters.Any(m=>m.Hp>0),"场景没有生成存活怪物："+name);
            Need(game.Heroes.All(h=>Math.Abs(h.Position.X-room.Center.X)<=room.HalfWidth+.01f),"英雄越出房间："+name);
            return (game,room);

            string Target(int index)
            {
                var targeting=game.Capture().SkillState.Targeting;
                if(targeting.FriendlyTargets.TryGetValue(index,out int id))return $"#{id}";
                int locked=targeting.Locks.Count(l=>l.Target==index);
                return locked>0?$"被{locked}锁":"无";
            }
        }

        Scene("单Boss",1,0,true,false,false);
        Scene("五普通近战",5,0,false,false,false);
        Scene("近远混合",3,2,false,false,false);
        Scene("M06猎手",1,0,false,true,false);
        Scene("M05范围怪",0,1,false,false,true);

        Console.WriteLine("PASS: five deterministic positioning scenarios (single boss, five melee, mixed, hunter, area caster) with five heroes plus three skeletons; positions and target relations printed for round-2 positioning work");
    }
}
