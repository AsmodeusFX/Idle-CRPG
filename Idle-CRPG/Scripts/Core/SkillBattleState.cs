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
    public long NextId;
    public CombatTargetingState Targeting=new();
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
    public int Owner,Target,Depth;
    public double Coefficient=1;
    public long BuffInstance;
    public long OriginCast;
    public SkillAttackSnapshot? Snapshot;
    public SkillFlightState? Flight;
}
