using System;
using System.Collections.Generic;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>战斗表现事件类型；只描述可观察事实，不参与伤害、奖励或索敌计算。</summary>
public enum CombatVisualKind { Cast,Hit,Heal,Buff,Summon,Chain,CorpseExplosion,Pulse }

/// <summary>
/// 一次有界的临时表现事件。位置在事件产生时快照，UI 不需要再反查可能已死亡的 Actor。
/// 事件不进入正式存档、不改变随机源，也不作为第二套战斗状态。
/// </summary>
public sealed record CombatVisualEvent(
    long Sequence,long Tick,CombatVisualKind Kind,string Skill,string Detail,
    int Owner,int Target,Vector2 From,Vector2 To,double Amount,float Radius,
    DamageType Type,int Depth,bool Critical,bool Blocked,bool Dodged,bool Killed);

public sealed partial class GameSession
{
    private const int MaxVisualEvents=256;
    private readonly List<CombatVisualEvent> visualEvents=new();
    private long visualEventSequence;

    /// <summary>只读表现事件流；UI 按递增 Sequence 消费，超出上限时丢弃最旧事件。</summary>
    public IReadOnlyList<CombatVisualEvent> VisualEvents=>visualEvents;
    /// <summary>表现层按战斗 ID 解析当前房 Actor；越界或已移除返回 null，不改变索敌。</summary>
    public Actor? ActorForPresentation(int id)=>CombatActor(id);
    /// <summary>当前临时 Buff 只读入口；光环持续性表现使用，不作为存档字段。</summary>
    public IReadOnlyList<SkillBuffState> PresentationBuffs=>skillState.Buffs;
    /// <summary>本房尸体权利只读入口；仅用于地面标记，不消费尸体。</summary>
    public IReadOnlyList<CorpseState> PresentationCorpses=>skillState.Corpses;

    private long VisualTick
    {
        get
        {
            try { return Floors.Count>0?Floor.ElapsedTicks:0; }
            catch(ArgumentOutOfRangeException) { return 0; }
        }
    }
    /// <summary>记录一次表现事实；环形缓冲有界，避免长时间挂机积累无限事件。</summary>
    private void PushVisual(CombatVisualKind kind,string skill,string detail,int owner,int target,Vector2 from,Vector2 to,
        double amount=0,float radius=0,DamageType type=DamageType.Physical,int depth=0,
        bool critical=false,bool blocked=false,bool dodged=false,bool killed=false)
    {
        visualEvents.Add(new(++visualEventSequence,VisualTick,kind,skill,detail,owner,target,from,to,amount,radius,type,depth,critical,blocked,dodged,killed));
        if(visualEvents.Count>MaxVisualEvents)visualEvents.RemoveRange(0,visualEvents.Count-MaxVisualEvents);
    }
    private void ClearVisualEvents()=>visualEvents.Clear();
}
