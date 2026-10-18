using System;

namespace IdleCrpg.Core;

/// <summary>本次运行的 GM 战斗参数；不属于成长、科技或存档，重新创建/读取会话后恢复默认。</summary>
public record GmBattleSettings(bool Invincible=false,float MonsterHp=1,float MonsterAttack=1,int RoomCountBonus=0);

public sealed partial class GameSession
{
    /// <summary>只读调试状态；UI 必须通过 SetGmBattle 命令修改。</summary>
    public GmBattleSettings Gm { get; private set; }=new();

    /// <summary>调整当局战斗参数；倍率立即生效，数量只参与后续房间首次生成，不触发存档。</summary>
    public void SetGmBattle(GmBattleSettings settings)
    {
        if(settings==null||!float.IsFinite(settings.MonsterHp)||settings.MonsterHp<.5f||settings.MonsterHp>50||!float.IsFinite(settings.MonsterAttack)||settings.MonsterAttack<.5f||settings.MonsterAttack>50||settings.RoomCountBonus is < -999 or > 999)
            throw new ArgumentOutOfRangeException(nameof(settings),"GM 参数超出范围");
        Gm=settings;
    }

    /// <summary>GM 发放已实现的金币和共享经验；成长复用正常经验公式，到账资源按正常进度保存。</summary>
    public void GrantGmResources(long gold=0,int experience=0)
    {
        if(gold<0||experience<0||experience>1000000)throw new ArgumentOutOfRangeException(nameof(experience));
        long total=checked(Gold+gold);
        _=checked(Experience+experience);
        Gold=total;
        if(experience>0)GainExperience(experience);
        if(gold>0||experience>0)Note($"GM：获得 {gold:N0} 金币 / {experience:N0} 共享经验");
    }

    /// <summary>伤害只有此处计算：基础伤害至少 1；GM 血量倍率用承伤除以倍率实现等效生命，保留当前生命比例且不污染出生模板。</summary>
    private float AttackDamage(Actor actor,Actor target)
    {
        bool hero=Heroes.Contains(actor);
        if(!hero&&Gm.Invincible)return 0;
        float damage=Math.Max(1,actor.Attack*(hero?1:Gm.MonsterAttack)-target.Defense);
        return hero?damage/Gm.MonsterHp:damage;
    }
}
