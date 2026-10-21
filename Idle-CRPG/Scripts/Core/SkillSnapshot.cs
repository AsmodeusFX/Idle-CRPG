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
        var targeting=state.Targeting;
        if(targeting!=null&&(!Time(targeting.EntryElapsed)||!Point(targeting.EntryAnchor)||!Point(targeting.EntryDirection)))throw new InvalidDataException("战斗入场现场无效");
        if(targeting!=null&&(targeting.DeployedMask<0||targeting.DeployedMask>=(1<<snapshot.Heroes.Count)))throw new InvalidDataException("入场展开成员掩码无效");
        if(snapshot.RoomCheckpoint&&targeting?.DeployedMask!=0)throw new InvalidDataException("正式检查点不得保存展开现场");
        if(targeting!=null&&(targeting.FloorIndex< -1||targeting.RoomIndex< -1||(targeting.Locks?.Count>0||targeting.EntryPrepared)&&(targeting.FloorIndex!=snapshot.FloorIndex||targeting.RoomIndex!=snapshot.CurrentRoom)))throw new InvalidDataException("普通索敌现场房间归属无效");
        if(targeting==null||targeting.Locks==null||targeting.Capacities==null||!Time(targeting.Clock)||targeting.Cursor<0||targeting.Cursor>1000||targeting.CandidateMask<0||targeting.CandidateMask>=(1<<snapshot.Heroes.Count)||targeting.Capacities.Length!=0&&targeting.Capacities.Length!=snapshot.Heroes.Count||targeting.Capacities.Any(v=>v<0||v>100000)||targeting.Locks.Count>1000||targeting.Locks.Select(l=>l?.Owner).Distinct().Count()!=targeting.Locks.Count)throw new InvalidDataException("普通索敌现场或预约数量无效");
        foreach(var locked in targeting.Locks)
            if(locked==null||!inRoom||locked.Owner<snapshot.Heroes.Count||!Owner(locked.Owner)||locked.Target<0||locked.Target>=snapshot.Heroes.Count||!Enum.IsDefined(locked.Kind)||!Time(locked.NextEvaluation))throw new InvalidDataException("普通目标引用或锁定类型无效");
        if(snapshot.RoomCheckpoint&&(targeting.Locks.Count!=0||targeting.Capacities.Length!=0||targeting.Clock!=0||targeting.CandidateMask!=0||targeting.Cursor!=0||targeting.FloorIndex!=-1||targeting.RoomIndex!=-1||targeting.EntryPrepared||targeting.EntryElapsed!=0||targeting.EntryAnchor!=System.Numerics.Vector2.Zero||targeting.EntryDirection!=System.Numerics.Vector2.Zero))throw new InvalidDataException("正式检查点不得保存普通索敌战斗现场");
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
