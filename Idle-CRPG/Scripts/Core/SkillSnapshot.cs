using System;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    private static void ValidateSkillSnapshot(Config config,SessionSnapshot snapshot)
    {
        var state=snapshot.SkillState;
        if(state==null||state.Actions==null||state.Buffs==null||state.Flights==null||state.Pending==null||state.NextId<0||state.Flights.Count>config.Skills.Setting("max_flights")||state.Pending.Count>config.Skills.MaxPending||state.Actions.Count>1003||state.Buffs.Count>1003*config.Skills.Buffs.Count)throw new InvalidDataException("技能现场数量或字段无效");
        bool inRoom=(snapshot.World==null||snapshot.World.Stage==CampaignStage.Dungeon)&&snapshot.FloorIndex>=0&&snapshot.FloorIndex<snapshot.Floors.Count&&snapshot.CurrentRoom>=0&&snapshot.CurrentRoom<snapshot.Floors[snapshot.FloorIndex].Rooms.Count;
        int count=inRoom?snapshot.Heroes.Count+snapshot.Floors[snapshot.FloorIndex].Rooms[snapshot.CurrentRoom].Monsters.Count:0;
        bool Owner(int id)=>id>=0&&id<count;
        bool Time(double value)=>double.IsFinite(value)&&value>=0;
        bool Point(System.Numerics.Vector2 point)=>float.IsFinite(point.X)&&float.IsFinite(point.Y);
        void Flight(SkillFlightState f,bool allowExpired)
        {
            if(f==null||!Owner(f.Owner)||!Owner(f.Target)||f.Depth<0||f.Depth>config.Skills.MaxDepth||!config.Skills.Skills.ContainsKey(f.Skill)||!double.IsFinite(f.Life)||!allowExpired&&f.Life<0||!Time(f.MaxLife)||!Time(f.Delay)||!Time(f.Hold)||!Time(f.Coefficient)||!Time(f.PowerScale)||!Time(f.Pulse)||!Time(f.Elapsed)||!float.IsFinite(f.Arc)||f.Snapshot==null||f.Snapshot.Attributes==null||!Time(f.Snapshot.Attack)||!Point(f.Position)||!Point(f.Previous)||!Point(f.Start)||!Point(f.TargetPoint)||!Point(f.Direction)||f.Hit==null||f.Hit.Any(id=>!Owner(id))||f.Hit.Distinct().Count()!=f.Hit.Count||f.Reacquired<0||f.Reacquired>config.Skills.Setting("max_reacquire")||f.GenericEffect==""&&!config.Skills.AbilityEffects.ContainsKey(f.Effect)||f.GenericEffect!=""&&!config.Skills.Effects.ContainsKey(f.GenericEffect)||f.Form is not ("projectile" or "target" or "ground" or "generic" or "entity"))throw new InvalidDataException("投射物或原因快照无效");
            f.Snapshot.Attributes.Validate();
        }
        foreach(var a in state.Actions)
            if(!Owner(a.Owner)||a.Active!=""&&!config.Skills.Skills.ContainsKey(a.Active)||!Time(a.Windup)||!Time(a.Recovery)||a.Cooldowns==null||a.Cooldowns.Any(p=>!config.Skills.Skills.ContainsKey(p.Key)||!Time(p.Value)))throw new InvalidDataException("技能时间轴无效");
        if(state.Actions.Select(a=>a.Owner).Distinct().Count()!=state.Actions.Count)throw new InvalidDataException("角色动作槽重复");
        foreach(var b in state.Buffs)
        {
            if(!Owner(b.Owner)||!Owner(b.Target)||!config.Skills.Buffs.ContainsKey(b.Id)||!config.Skills.Skills.ContainsKey(b.Ability)||!Time(b.Remaining)||!Time(b.Periodic)||!Time(b.Value)||!Time(b.Coefficient)||b.Instance<1||b.Instance>state.NextId)throw new InvalidDataException("Buff 现场无效");
            if(b.Cause!=null)Flight(b.Cause,true);
        }
        if(state.Buffs.Select(b=>(b.Id,b.Target)).Distinct().Count()!=state.Buffs.Count||state.Buffs.Select(b=>b.Instance).Distinct().Count()!=state.Buffs.Count)throw new InvalidDataException("Buff 实例重复");
        foreach(var f in state.Flights)Flight(f,false);
        foreach(var work in state.Pending)
        {
            if(!Owner(work.Owner)||!Owner(work.Target)||!config.Skills.Effects.ContainsKey(work.Effect)||!config.Skills.Skills.ContainsKey(work.Skill)||work.Depth<0||work.Depth>config.Skills.MaxDepth||!Time(work.Coefficient))throw new InvalidDataException("Effect 队列引用无效");
            if(work.Flight!=null)Flight(work.Flight,true);
        }
        if(snapshot.RoomCheckpoint&&(state.Actions.Count>0||state.Buffs.Count>0||state.Flights.Count>0||state.Pending.Count>0))throw new InvalidDataException("正式检查点不应包含技能战斗现场");
    }
}
