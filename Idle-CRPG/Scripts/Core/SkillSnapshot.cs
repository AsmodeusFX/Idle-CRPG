using System;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    private static void ValidateSkillSnapshot(Config config,SessionSnapshot snapshot)
    {
        var state=snapshot.SkillState;
        if(state==null||state.Actions==null||state.Buffs==null||state.Flights==null||state.Pending==null||state.Corpses==null||state.ProcFires==null||state.NextId<0||state.Flights.Count>config.Skills.Setting("max_flights")||state.Pending.Count>config.Skills.MaxPending||state.Actions.Count>1003||state.Buffs.Count>1003*config.Skills.Buffs.Count)throw new InvalidDataException("技能现场数量或字段无效");
        bool inRoom=(snapshot.World==null||snapshot.World.Stage==CampaignStage.Dungeon)&&snapshot.FloorIndex>=0&&snapshot.FloorIndex<snapshot.Floors.Count&&snapshot.CurrentRoom>=0&&snapshot.CurrentRoom<snapshot.Floors[snapshot.FloorIndex].Rooms.Count;
        int monsters=inRoom?snapshot.Floors[snapshot.FloorIndex].Rooms[snapshot.CurrentRoom].Monsters.Count:0;
        int friendlies=inRoom?snapshot.Heroes.Count+snapshot.Summons!.Count:0;
        int count=inRoom?friendlies+monsters:0;
        bool Owner(int id)=>id>=0&&id<count;
        bool Friendly(int id)=>id>=0&&(id<snapshot.Heroes.Count||id>=snapshot.Heroes.Count+monsters&&id<friendlies+monsters);
        bool Time(double value)=>double.IsFinite(value)&&value>=0;
        bool Point(System.Numerics.Vector2 point)=>float.IsFinite(point.X)&&float.IsFinite(point.Y);
        bool Origin(long id)=>id>=0&&id<=state.NextId;
        if(state.ProcTick<0||state.ProcIcds==null||state.ProcIcds.Any(p=>p==null)||state.ProcIcds.Count>(long)count*config.Skills.Triggers.Count||state.ProcIcds.Select(p=>(p.Owner,p.Trigger)).Distinct().Count()!=state.ProcIcds.Count)throw new InvalidDataException("Proc ICD时钟/数量或重复归属无效");
        foreach(var icd in state.ProcIcds)
            if(icd==null||!Owner(icd.Owner)||!config.Skills.Triggers.ContainsKey(icd.Trigger)||!config.Skills.ProcIcdSeconds.TryGetValue(icd.Trigger,out double seconds)||seconds<=0||icd.ReadyTick<0||icd.ReadyTick>state.ProcTick+(long)Math.Ceiling(seconds*config.Skills.TickRate))throw new InvalidDataException("Proc ICD现场引用或到期Tick无效");
        if(snapshot.RoomCheckpoint&&(state.ProcTick!=0||state.ProcIcds.Count>0))throw new InvalidDataException("正式检查点不得保存Proc ICD");
        void Attack(SkillAttackSnapshot attack)
        {
            if(attack.Attributes==null||!Time(attack.Attack)||attack.Level<1||attack.Level>Math.Max(config.MaxHeroLevel,config.MaxMonsterLevel))throw new InvalidDataException("攻击属性/等级快照无效");
            attack.Attributes.Validate();
        }
        var targeting=state.Targeting;
        if(targeting!=null&&(!Time(targeting.EntryElapsed)||!Point(targeting.EntryAnchor)||!Point(targeting.EntryDirection)))throw new InvalidDataException("战斗入场现场无效");
        if(targeting!=null&&(targeting.DeployedMask<0||targeting.DeployedMask>=(1<<snapshot.Heroes.Count)))throw new InvalidDataException("入场展开成员掩码无效");
        if(targeting!=null&&(targeting.EntryPassMask<0||targeting.EntryEngagedMask<0||(long)targeting.EntryPassMask>=(1L<<friendlies)||(long)targeting.EntryEngagedMask>=(1L<<friendlies)))throw new InvalidDataException("入口通行成员掩码无效");
        if(snapshot.RoomCheckpoint&&targeting?.DeployedMask!=0)throw new InvalidDataException("正式检查点不得保存展开现场");
        if(targeting!=null&&(targeting.FloorIndex< -1||targeting.RoomIndex< -1||(targeting.Locks?.Count>0||targeting.EntryPrepared)&&(targeting.FloorIndex!=snapshot.FloorIndex||targeting.RoomIndex!=snapshot.CurrentRoom)))throw new InvalidDataException("普通索敌现场房间归属无效");
        if(targeting==null||targeting.Locks==null||targeting.Capacities==null||!Time(targeting.Clock)||targeting.Cursor<0||targeting.Cursor>1000||targeting.CandidateMask<0||targeting.CandidateMask>=(1<<friendlies)||targeting.Capacities.Length!=0&&targeting.Capacities.Length!=friendlies||targeting.Capacities.Any(v=>v<0||v>100000)||targeting.Locks.Count>1000||targeting.Locks.Select(l=>l?.Owner).Distinct().Count()!=targeting.Locks.Count)throw new InvalidDataException("普通索敌现场或预约数量无效");
        foreach(var locked in targeting.Locks)
            if(locked==null||!inRoom||locked.Owner<snapshot.Heroes.Count||locked.Owner>=snapshot.Heroes.Count+monsters||locked.Target<0||locked.Target>=friendlies||!Enum.IsDefined(locked.Kind)||!Time(locked.NextEvaluation))throw new InvalidDataException("普通目标引用或锁定类型无效");
        if(targeting.FriendlyTargets==null||targeting.FriendlyTargets.Count>friendlies||targeting.FriendlyTargets.Any(p=>!Owner(p.Key)||!Friendly(p.Key)||p.Value<snapshot.Heroes.Count||p.Value>=snapshot.Heroes.Count+monsters)||snapshot.RoomCheckpoint&&targeting.FriendlyTargets.Count>0)throw new InvalidDataException("友方稳定目标引用无效");
        if(snapshot.RoomCheckpoint&&(targeting.Locks.Count!=0||targeting.Capacities.Length!=0||targeting.Clock!=0||targeting.CandidateMask!=0||targeting.Cursor!=0||targeting.FloorIndex!=-1||targeting.RoomIndex!=-1||targeting.EntryPrepared||targeting.EntryElapsed!=0||targeting.EntryAnchor!=System.Numerics.Vector2.Zero||targeting.EntryDirection!=System.Numerics.Vector2.Zero||targeting.EntryPassMask!=0||targeting.EntryEngagedMask!=0))throw new InvalidDataException("正式检查点不得保存普通索敌战斗现场");
        void Flight(SkillFlightState f,bool allowExpired)
        {
            if(f==null||!Owner(f.Owner)||!Owner(f.Target)||f.Depth<0||f.Depth>config.Skills.MaxDepth||!config.Skills.Skills.ContainsKey(f.Skill)||!double.IsFinite(f.Life)||!allowExpired&&f.Life<0||!Time(f.MaxLife)||!Time(f.Delay)||!Time(f.Hold)||!Time(f.Coefficient)||!Time(f.PowerScale)||!Time(f.Pulse)||!Time(f.Elapsed)||!float.IsFinite(f.Arc)||f.Snapshot==null||f.Snapshot.Attributes==null||!Time(f.Snapshot.Attack)||!Point(f.Position)||!Point(f.Previous)||!Point(f.Start)||!Point(f.TargetPoint)||!Point(f.Direction)||f.Hit==null||f.Hit.Any(id=>!Owner(id))||f.Hit.Distinct().Count()!=f.Hit.Count||f.Reacquired<0||f.Reacquired>config.Skills.Setting("max_reacquire")||f.GenericEffect==""&&!config.Skills.AbilityEffects.ContainsKey(f.Effect)||f.GenericEffect!=""&&!config.Skills.Effects.ContainsKey(f.GenericEffect)||f.Form is not ("projectile" or "target" or "ground" or "generic" or "entity"))throw new InvalidDataException("投射物或原因快照无效");
            if(!Origin(f.OriginCast))throw new InvalidDataException("飞行施法来源无效");
            Attack(f.Snapshot);
        }
        foreach(var a in state.Actions)
        {
            if(!Time(a.RetreatCooldown)||!Point(a.RetreatGoal)||a.RetreatThreat< -1||a.RetreatThreat>=0&&(!Owner(a.RetreatThreat)||Friendly(a.Owner)==Friendly(a.RetreatThreat)))throw new InvalidDataException("退让现场无效");
            if(!Owner(a.Owner)||a.Active!=""&&!config.Skills.Skills.ContainsKey(a.Active)||!Time(a.Windup)||!Time(a.Recovery)||a.Cooldowns==null||a.Cooldowns.Any(p=>!config.Skills.Skills.ContainsKey(p.Key)||!Time(p.Value)))throw new InvalidDataException("技能时间轴无效");
        }
        if(state.Actions.Select(a=>a.Owner).Distinct().Count()!=state.Actions.Count)throw new InvalidDataException("角色动作槽重复");
        foreach(var b in state.Buffs)
        {
            if(!Owner(b.Owner)||!Owner(b.Target)||!config.Skills.Buffs.ContainsKey(b.Id)||!config.Skills.Skills.ContainsKey(b.Ability)||!Time(b.Remaining)||!Time(b.Periodic)||!Time(b.Value)||!Time(b.Coefficient)||b.Instance<1||b.Instance>state.NextId)throw new InvalidDataException("Buff 现场无效");
            if(b.Cause!=null)Flight(b.Cause,true);
            if(!Origin(b.OriginCast)||b.AuraSkill==null||b.AuraSkill!=""&&(!ActorFor(b.Owner).ActiveAuraSkills.Contains(b.AuraSkill)||b.Ability!=b.AuraSkill||!config.Skills.Skills[b.AuraSkill].Effects.Any(id=>config.Skills.Effects[id].Kind=="MaintainAura"&&config.Skills.Effects[id].BuffId==b.Id)))throw new InvalidDataException("光环来源/激活状态无效");
        }
        Actor ActorFor(int id)
        {
            if(id<snapshot.Heroes.Count)return snapshot.Heroes[id];
            int index=id-snapshot.Heroes.Count;
            if(index<monsters)return snapshot.Floors[snapshot.FloorIndex].Rooms[snapshot.CurrentRoom].Monsters[index];
            return snapshot.Summons![index-monsters];
        }
        if(state.Buffs.GroupBy(b=>(b.Id,b.Target)).Any(group=>config.Skills.Buffs[group.Key.Id].Stacking=="Strongest"?group.Select(b=>(b.Owner,b.Ability,b.AuraSkill)).Distinct().Count()!=group.Count():group.Count()!=1)||state.Buffs.Select(b=>b.Instance).Distinct().Count()!=state.Buffs.Count)throw new InvalidDataException("Buff 实例或同源贡献重复");
        foreach(var f in state.Flights)Flight(f,false);
        foreach(var work in state.Pending)
        {
            if(work.Trigger==null||work.Trigger!=""&&(!config.Skills.Triggers.TryGetValue(work.Trigger,out var trigger)||trigger.Effect!=work.Effect))throw new InvalidDataException("Proc效果队列触发来源无效");
            if(!Owner(work.Owner)||!Owner(work.Target)||!config.Skills.Effects.ContainsKey(work.Effect)||!config.Skills.Skills.ContainsKey(work.Skill)||work.Depth<0||work.Depth>config.Skills.MaxDepth||!Time(work.Coefficient))throw new InvalidDataException("Effect 队列引用无效");
            if(work.Flight!=null)Flight(work.Flight,true);
            if(!Origin(work.OriginCast))throw new InvalidDataException("效果施法来源无效");
            if(work.Snapshot!=null)Attack(work.Snapshot);
        }
        // 尸体权利只属于当前房间的死亡事实：正式检查点不保存，读档由重开的房间战斗重新产生。
        if(state.Corpses.Count>monsters||state.Corpses.Select(c=>c.ActorId).Distinct().Count()!=state.Corpses.Count)throw new InvalidDataException("尸体现场数量或重复");
        foreach(var corpse in state.Corpses)
        {
            if(corpse==null||!inRoom||corpse.ActorId<snapshot.Heroes.Count||corpse.ActorId>=snapshot.Heroes.Count+monsters||!Point(corpse.Position)||ActorFor(corpse.ActorId).Alive)throw new InvalidDataException("尸体现场引用无效");
        }
        // Proc计数按根源施法与目标唯一；上限与运行时分片修剪一致，正式检查点不保存。
        if(state.ProcFires.Count>4096||state.ProcFires.Select(f=>(f.OriginCast,f.Trigger,f.Target)).Distinct().Count()!=state.ProcFires.Count)throw new InvalidDataException("Proc限次现场数量或重复");
        foreach(var fire in state.ProcFires)
            if(fire==null||!config.Skills.Triggers.ContainsKey(fire.Trigger)||!Owner(fire.Target)||fire.OriginCast<0||fire.OriginCast>state.NextId||fire.Count<1||fire.Count>1000)throw new InvalidDataException("Proc限次现场引用无效");
        if(snapshot.RoomCheckpoint&&(state.Actions.Count>0||state.Buffs.Count>0||state.Flights.Count>0||state.Pending.Count>0||state.Corpses.Count>0||state.ProcFires.Count>0))throw new InvalidDataException("正式检查点不应包含技能战斗现场");
    }
}
