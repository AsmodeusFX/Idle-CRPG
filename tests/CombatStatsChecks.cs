using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>P0：本房战斗观测计数。只读、有界、不入存档；换房重置。</summary>
public static class CombatStatsChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("COMBAT_STATS: "+why);}
        var c=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));

        var game=new GameSession(c,17,new[]{"warrior","ranger","mage"});
        for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
        var room=game.Floor.Rooms[game.CurrentRoom];
        var state=game.Capture().SkillState;
        state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
        for(int i=0;i<game.Heroes.Count;i++)
        {
            var hero=game.Heroes[i];
            hero.Position=room.Center+new Vector2(-room.HalfWidth+.8f,0);
            hero.Hp=hero.MaxHp=100000;hero.Attack=10;hero.Timer=0;hero.SkillIds=Array.Empty<string>();
        }
        for(int i=0;i<room.Monsters.Count;i++)
        {
            var monster=room.Monsters[i];
            monster.Position=room.Center+new Vector2(1.2f,i*.3f);monster.Hp=monster.MaxHp=1000;
            monster.Defense=0;monster.Attack=0;monster.Range=.7f;monster.Ranged=false;monster.Timer=999;
        }
        for(int i=0;i<80;i++)game.Tick(.05f);

        var stats=game.CombatStats;
        Need(stats.FirstAttackActor!="","首次友方普攻没有记录");
        Need(float.IsFinite(stats.FirstAttackDepth),"首次普攻深度不是有限值");
        Need(stats.NearestNeighbourMin>0&&float.IsFinite(stats.NearestNeighbourMin),"友方最近邻距离没有采样");
        Need(stats.MeleeSamples>0,"同目标近战样本为0");
        Need(stats.CapacityOccupiedSeconds>0,"容量占用时长为0");
        Need(stats.CapacityPeak>=1,"普通容量峰值未记录");

        var lines=string.Join("\n",game.CombatStatsDiagnostics());
        foreach(var key in new[]{"战斗统计","首次普攻","提前接战","同目标近战间距","远程浅区施法","容量"})
            Need(lines.Contains(key),"GM战斗统计缺少字段："+key);
        Need(string.Join("\n",game.EngagementDiagnostics()).Contains("战斗统计"),"主诊断窗口未包含战斗统计");

        // 清房会重置本房统计，不能把上一房数据带到下一房。
        foreach(var monster in room.Monsters)monster.Hp=0;
        for(int i=0;i<4;i++)game.Tick(.05f);
        Need(game.CombatStats.MeleeSamples==0&&game.CombatStats.FirstAttackActor=="","清房后本房统计未重置");
        Console.WriteLine("PASS: room combat stats record first attack depth, nearest-neighbour spacing, same-target melee spacing, capacity peak/occupied time and expose bounded GM lines, and reset on room clear");
    }
}
