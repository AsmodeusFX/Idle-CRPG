using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>旧战斗元数据只转换一次；保留成长和技能，不回写玩家文件，不续存怪物战斗现场。</summary>
    private static void MigrateCombatFormula(Config config,SessionSnapshot snapshot)
    {
        if(snapshot.CombatVersion is <0 or >2)throw new InvalidDataException("不支持的战斗存档版本");
        if(snapshot.CombatVersion==2)return;
        if(snapshot.Heroes==null||snapshot.ReserveHeroes==null||snapshot.Floors==null||snapshot.SkillState==null)throw new InvalidDataException("旧战斗现场缺失");
        var all=snapshot.Heroes.Concat(snapshot.ReserveHeroes).Concat(snapshot.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters));
        var converted=new HashSet<BattleAttributes>();
        void ConvertAttributes(BattleAttributes value)
        {
            if(value==null)throw new InvalidDataException("旧战斗属性缺失");
            if(!converted.Add(value))return;
            value.Validate();
            for(int i=0;i<value.Modifiers.Count;i++)
            {
                var m=value.Modifiers[i];if(m.Bucket!=DamageBucket.Power)continue;
                if(m.Value<=0)throw new InvalidDataException("旧独立倍率必须为正");
                value.Modifiers[i]=m with {Value=m.Value-1};
            }
        }
        foreach(var actor in all)
        {
            if(snapshot.CombatVersion==0)config.ApplyCombatProfile(actor);
            else ConvertAttributes(actor.Battle);
        }
        bool inRoom=snapshot.FloorIndex>=0&&snapshot.FloorIndex<snapshot.Floors.Count&&snapshot.CurrentRoom>=0&&snapshot.CurrentRoom<snapshot.Floors[snapshot.FloorIndex].Rooms.Count;
        var actors=inRoom?snapshot.Heroes.Concat(snapshot.Floors[snapshot.FloorIndex].Rooms[snapshot.CurrentRoom].Monsters).ToArray():Array.Empty<Actor>();
        var attacks=new HashSet<SkillAttackSnapshot>();
        void Attack(int owner,SkillAttackSnapshot attack)
        {
            if(attack==null||owner<0||owner>=actors.Length)throw new InvalidDataException("旧攻击快照来源无效");
            if(!attacks.Add(attack))return;
            ConvertAttributes(attack.Attributes);
            // 老版本没有出手等级；只能采用该完整快照的攻方等级。新版本不再补写或实时追溯。
            attack.Level=actors[owner].Level;
        }
        void Flight(SkillFlightState? flight){if(flight!=null)Attack(flight.Owner,flight.Snapshot);}
        if(snapshot.SkillState.Flights==null||snapshot.SkillState.Buffs==null||snapshot.SkillState.Pending==null)throw new InvalidDataException("旧技能现场缺失");
        foreach(var flight in snapshot.SkillState.Flights)Flight(flight);
        foreach(var buff in snapshot.SkillState.Buffs)Flight(buff.Cause);
        foreach(var work in snapshot.SkillState.Pending){Flight(work.Flight);if(work.Snapshot!=null)Attack(work.Owner,work.Snapshot);}
        snapshot.CombatVersion=2;
    }
}
