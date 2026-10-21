using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class Config
{
    public IReadOnlyDictionary<int,int> ExperienceLevelRates {get;private set;}=null!;
    private void LoadExperienceRates(string root)
    {
        var rates=new SortedDictionary<int,int>();
        foreach(var r in CsvTable.Load("hero/experience_level_rate.csv",File.ReadAllText(Path.Combine(root,"hero/experience_level_rate.csv")),"id","percent").Rows)
        {
            int gap=r.Integer("id"),percent=r.Integer("percent");
            if(Math.Abs((long)gap)>10000||percent is <0 or >10000)throw new InvalidDataException("等级经验倍率越界");rates.Add(gap,percent);
        }
        if(!rates.ContainsKey(0)||rates[0]!=100||rates.First().Key>=0||rates.Last().Key<=0||rates.Zip(rates.Skip(1)).Any(p=>p.Second.Key!=p.First.Key+1||p.Second.Value>p.First.Value))throw new InvalidDataException("经验倍率须连续覆盖负/零/正等级差，零差100%，随角色相对等级上升不递增");
        ExperienceLevelRates=rates;
    }
    /// <summary>等级差=英雄等级-怪物等级；超出表边界沿用首/末行，首版最高300%，低5级及以上为0。</summary>
    public int MonsterExperiencePercent(int heroLevel,int monsterLevel)
    {
        int gap=Math.Clamp(heroLevel-monsterLevel,ExperienceLevelRates.Keys.Min(),ExperienceLevelRates.Keys.Max());return ExperienceLevelRates[gap];
    }
}
public sealed partial class GameSession
{
    /// <summary>兼容旧检查/API的排头经验投影；实际成长以每名Actor.Experience/Fraction为唯一来源。</summary>
    public int Experience=>Heroes.Count==0?0:Heroes[0].Experience;
    public int ExperienceRequired(Actor hero)=>hero.Level>=Config.MaxHeroLevel?0:Config.HeroLevels[hero.Level].Experience;
    /// <summary>同一基础奖励发给全部上阵者（包括倒地者），各按本人等级结算；候补不参与。</summary>
    private void GainMonsterExperience(int amount,int monsterLevel)
    {
        foreach(var hero in Heroes)GrantHeroExperience(hero,amount,Config.MonsterExperiencePercent(hero.Level,monsterLevel));
    }
    private void GainExperience(int amount)
    {foreach(var hero in Heroes)GrantHeroExperience(hero,amount,100);}
    /// <summary>整数百分之一经验避免倍率取整丢失；每人独立跨级，倒地升级仍保持0血量。</summary>
    private void GrantHeroExperience(Actor hero,int basis,int percent)
    {
        if(basis<=0||percent<=0)return;
        long hundredths=Math.Min((long)int.MaxValue*100+99,(long)hero.Experience*100+hero.ExperienceFraction+(long)basis*percent);
        hero.Experience=(int)(hundredths/100);hero.ExperienceFraction=(int)(hundredths%100);MarkChanged();
        while(hero.Level<Config.MaxHeroLevel&&hero.Experience>=Config.HeroLevels[hero.Level].Experience)
        {
            var before=Config.HeroLevels[hero.Level];var after=Config.HeroLevels[hero.Level+1];
            hero.Experience-=before.Experience;hero.Level++;
            float hpBonus=after.HpBonus-before.HpBonus;hero.MaxHp+=hpBonus;hero.Attack+=after.AttackBonus-before.AttackBonus;
            if(hero.Alive)hero.Hp=Math.Min(CombatMaximumHp(hero),hero.Hp+hpBonus);
            Note($"{hero.Name}成长至等级 {hero.Level}");
        }
    }
}
