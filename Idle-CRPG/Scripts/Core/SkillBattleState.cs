using System.Collections.Generic;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>只供内部完整快照复现的临时房间状态；正式进度保存时清空，读档重新开战。</summary>
public sealed class SkillBattleState
{
    public List<SkillActionState> Actions=new();
    public List<SkillBuffState> Buffs=new();
    public List<SkillFlightState> Flights=new();
    public List<SkillEffectWork> Pending=new();
    // 本房死亡事实产生的可消费尸体权利；正式检查点清空，跨房不携带。
    public List<CorpseState> Corpses=new();
    // 按(根源施法,触发,目标)累计的Proc次数；仅proc_limit.csv显式声明的触发使用。
    public List<ProcFireState> ProcFires=new();
    public long ProcTick;
    public List<ProcIcdState> ProcIcds=new();
    public long NextId;
    public CombatTargetingState Targeting=new();
}
/// <summary>当前房间内OwnerID+TriggerID独立的下次可触发Tick，仅完整内部快照保留。</summary>
public sealed class ProcIcdState
{
    public int Owner;
    public string Trigger="";
    public long ReadyTick;
}
/// <summary>尸体 token 与怪物 Actor 分离：Actor 提供位置，消费权利只在本房技能现场中原子移除一次。</summary>
public sealed class CorpseState
{
    public int ActorId;
    public Vector2 Position;
}
/// <summary>一次根源施法内的Proc计数；OriginCast只作关联键，不改变事件资格。</summary>
public sealed class ProcFireState
{
    public long OriginCast;
    public string Trigger="";
    public int Target;
    public int Count;
}
public sealed class SkillActionState
{
    public int Owner,Target;
    public string Active="";
    public double Windup,Recovery;
    // 退让独立计时；请求等到动作安全边界，不取消已提交技能。
    public double RetreatCooldown;
    public int RetreatThreat=-1;
    public bool RetreatMoving;
    public Vector2 RetreatGoal;
    public Dictionary<string,double> Cooldowns=new();
}
public sealed class SkillAttackSnapshot
{
    public double Attack;
    public double AttackRating=1;
    // 与攻方属性同时快照，弹道飞行中升级不改旧攻击的护甲K等级。
    public int Level=1;
    public BattleAttributes Attributes=new();
}
public sealed class SkillBuffState
{
    public long Instance;
    public long OriginCast;
    public string AuraSkill="";
    public string Id="",Ability="";
    public int Owner,Target;
    public double Remaining,Periodic,Value,Coefficient;
    public SkillFlightState? Cause;
}
public sealed class SkillFlightState
{
    public long Id;
    public long OriginCast;
    public string Skill="",Effect="",Form="",Attribution="";
    public int Owner,Target,Depth,Reacquired;
    public Vector2 Position,Previous,Start,TargetPoint,Direction;
    public double Life,MaxLife,Delay,Hold,Pulse,Coefficient,Elapsed,PowerScale=1;
    public float Arc;
    public bool Active=true,Mirrored,Pierced,PrimaryFirst,CriticalNotified;
    public string GenericEffect="";
    public float GenericSpeed,GenericRadius;
    public bool? Critical;
    public List<int> Hit=new();
    public SkillAttackSnapshot Snapshot=new();
}
public sealed class SkillEffectWork
{
    public string Effect="",Skill="";
    public string Trigger="";
    public int Owner,Target,Depth;
    public double Coefficient=1;
    public long BuffInstance;
    public long OriginCast;
    public SkillAttackSnapshot? Snapshot;
    public SkillFlightState? Flight;
    /// <summary>由已学天赋trigger节点注册的常驻触发源：没有BuffInstance支撑，校验时按触发器OwnerBuff是否为空区分。</summary>
    public bool TalentTrigger;
}
