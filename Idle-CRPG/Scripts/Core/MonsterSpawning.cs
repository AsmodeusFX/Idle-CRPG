using System;
using System.IO;

namespace IdleCrpg.Core;

/// <summary>未来科技树输入，各项独立；当前没有科技树、加点或 UI。</summary>
public record SpawnModifiers(int MinBonus=0,int MaxBonus=0,int LevelBonus=0,float DropMultiplier=1,float ValueMultiplier=1);
public record SpawnRule(int Min,int Max,int Level,float HpGrowth,float AttackGrowth,float DefenseGrowth,float BossHp,float BossAttack,int XpBase,int BossReward,float DropMultiplier,float ValueGrowth)
{
    public void Validate(SpawnModifiers m)
    {
        if(Min<1||Max<Min||Level<1||HpGrowth<0||AttackGrowth<0||DefenseGrowth<0||BossHp<1||BossAttack<1||XpBase<1||BossReward<1||!float.IsFinite(DropMultiplier)||DropMultiplier<0||!float.IsFinite(ValueGrowth)||ValueGrowth<0||m.MinBonus<0||m.MaxBonus<0||m.LevelBonus<0||Min+(long)m.MinBonus>Max+(long)m.MaxBonus||Max+(long)m.MaxBonus>1000||Level+(long)m.LevelBonus>10000||!float.IsFinite(m.DropMultiplier)||m.DropMultiplier<0||!float.IsFinite(m.ValueMultiplier)||m.ValueMultiplier<=0)
            throw new InvalidDataException("刷怪规则/科技加成非法");
    }
    /// <summary>房间首次进入时使用本次快照构造怪物；英雄等级不参与。</summary>
    public Actor Create(UnitSpec spec,int level,bool boss,SpawnModifiers modifiers)
    {
        var a=Actor.From(spec); a.Level=level; a.Boss=boss;
        a.MaxHp*= (1+HpGrowth*(level-1))*(boss?BossHp:1); a.Hp=a.MaxHp;
        a.Attack*= (1+AttackGrowth*(level-1))*(boss?BossAttack:1); a.Defense+=DefenseGrowth*(level-1);
        a.RewardXp=checked(XpBase*level*(boss?BossReward:1));
        a.DropMultiplier=DropMultiplier*modifiers.DropMultiplier;
        a.ValueMultiplier=(1+ValueGrowth*(level-1))*modifiers.ValueMultiplier*(boss?BossReward:1);
        if(boss)a.Name="守门者";
        return a;
    }
}
