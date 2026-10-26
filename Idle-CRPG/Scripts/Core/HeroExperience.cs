using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;
public sealed partial class Config
{
    /// <summary>旧审计接口；等级差表已退出运行消费，无衰减统一100%。</summary>
    public IReadOnlyDictionary<int,int> ExperienceLevelRates {get;}=new Dictionary<int,int>();
    public int MonsterExperiencePercent(int heroLevel,int monsterLevel)=>100;
}
public sealed partial class GameSession
{
    /// <summary>Run公共余额，无玩法池上限；64位溢出报错，禁止截断吞奖励。</summary>
    public long PublicExperience {get;private set;}
    public int PublicExperienceFraction {get;private set;}
    /// <summary>兼容API也只读公共池，不投影Heroes[0]。</summary>
    public long Experience=>PublicExperience;
    public int ExperienceRequired(Actor hero)=>hero.Level>=Config.MaxHeroLevel?0:Config.HeroLevels[hero.Level].Experience;
    private void GainMonsterExperience(int amount,int monsterLevel)=>GainExperience(amount,"monster_kill");
    private void GainExperience(int amount,string source="gm")
    {if(amount<=0)return;long before=PublicExperience;var scaled=amount*XpGainMultiplier;long gained=(long)Math.Floor(scaled);int fraction=PublicExperienceFraction+(int)Math.Round((scaled-gained)*100);if(fraction>=100){gained+=fraction/100;fraction%=100;}PublicExperience=checked(PublicExperience+gained);PublicExperienceFraction=fraction;AuditCurrency(source,"receive","xp",before,PublicExperience);MarkChanged();}
    /// <summary>指定实例一次恰升一级；验证属性及余额后提交，失败零变更。</summary>
    public bool UpgradeHero(string heroInstanceId,out string reason)
    {
        var hero=Heroes.SingleOrDefault(h=>h.HeroInstanceId==heroInstanceId);
        if(hero==null){reason="英雄实例不存在";return false;}
        int cost=ExperienceRequired(hero);
        if(cost==0){reason="已达原型等级表上限";return false;}
        if(PublicExperience<cost){reason=$"公共XP不足：需要{cost}，当前{PublicExperience}";return false;}
        var before=Config.HeroLevels[hero.Level];var after=Config.HeroLevels[hero.Level+1];
        float hp=hero.MaxHp+after.HpBonus-before.HpBonus,attack=hero.Attack+after.AttackBonus-before.AttackBonus;
        if(!float.IsFinite(hp)||!float.IsFinite(attack)||hp<=0||attack<0){reason="升级属性溢出";return false;}
        long xpBefore=PublicExperience;PublicExperience-=cost;AuditCurrency("player_command","upgrade","xp",xpBefore,PublicExperience,hero.HeroInstanceId);hero.Level++;hero.Attack=attack;
        if(hero.Alive)hero.Hp=Math.Min(hp,hero.Hp+hp-hero.MaxHp);
        hero.MaxHp=hp;Note($"手动升级 {hero.HeroInstanceId} {hero.Name} → Lv.{hero.Level}，公共XP -{cost}，余额{PublicExperience}");reason="升级成功";return true;
    }
    private static void MigratePublicExperience(SessionSnapshot s)
    {
        if(s.PublicExperienceVersion is not (0 or 1)||s.HeroExperienceVersion is not (0 or 1))throw new InvalidDataException("不支持的经验版本");
        if(s.PublicExperienceVersion==0)
        {
            if(s.Experience<0||s.Heroes.Any(h=>h.Experience<0||h.ExperienceFraction is <0 or >99))throw new InvalidDataException("旧经验余额无效，不能折算");
            // v0共享余额只算一次；v1源码升级时已扣费，Actor.Experience仅是当前级未花余额，不能补发历史成本。
            long hundredths=s.HeroExperienceVersion==0?checked((long)s.Experience*100):s.Heroes.Sum(h=>checked((long)h.Experience*100+h.ExperienceFraction));
            s.PublicExperience=hundredths/100;s.PublicExperienceFraction=(int)(hundredths%100);s.PublicExperienceVersion=1;
            foreach(var hero in s.Heroes){hero.Experience=0;hero.ExperienceFraction=0;}
            s.Experience=0;s.HeroExperienceVersion=1;
        }
        if(s.PublicExperience<0||s.PublicExperienceFraction is <0 or >99||s.Heroes.Any(h=>h.Experience!=0||h.ExperienceFraction!=0))throw new InvalidDataException("公共XP或退役个人经验字段无效");
    }
}
