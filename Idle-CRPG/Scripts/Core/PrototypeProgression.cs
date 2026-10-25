using System;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>M5首批[H]原型参数；非正式数值平衡，所有评级和KP参数可替换，不改变执行逻辑。</summary>
public sealed record PrototypeProgression(double HeroRatingBase,double HeroRatingPerLevel,double MonsterRatingPerLevel,double RatingExponent,double RatingMinimum,double RatingMaximum,int KillKp,int LmaxCost)
{
    public static PrototypeProgression Load(string root)
    {
        var table=CsvTable.Load("combat/prototype_progression.csv",File.ReadAllText(Path.Combine(root,"combat/prototype_progression.csv")),"id","value","status");
        double Get(string key){var row=table.Require(key);if(row.Required("status")!="H")throw new InvalidDataException("原型参数必须显式标记H");return row.Number("value");}
        var rule=new PrototypeProgression(Get("hero_rating_base"),Get("hero_rating_per_level"),Get("monster_rating_per_level"),Get("rating_exponent"),Get("rating_minimum"),Get("rating_maximum"),checked((int)Get("kill_kp")),checked((int)Get("lmax_cost")));
        if(table.Rows.Count!=8||rule.HeroRatingBase<=0||rule.HeroRatingPerLevel<=0||rule.MonsterRatingPerLevel<=0||rule.RatingExponent<=0||rule.RatingMinimum<=0||rule.RatingMaximum<1||rule.RatingMinimum>1||rule.KillKp<1||rule.LmaxCost<1||Get("kill_kp")!=rule.KillKp||Get("lmax_cost")!=rule.LmaxCost)throw new InvalidDataException("评级/KP原型参数无效");
        return rule;
    }
}
public sealed partial class Config
{
    public PrototypeProgression Progression {get;private set;}=null!;
}
public sealed partial class GameSession
{
    /// <summary>基础自然评级是确定性的主要来源；后续修饰器通过同一查询接口叠加，非随机装备门禁。</summary>
    private double NaturalRating(Actor actor)=>Heroes.Contains(actor)||Summons.Contains(actor)?Config.Progression.HeroRatingBase+(actor.Level-1)*Config.Progression.HeroRatingPerLevel:actor.Level*Config.Progression.MonsterRatingPerLevel;
    public double AttackRating(Actor actor)=>NaturalRating(actor)+actor.AttackRatingBonus;
    public double DefenseRating(Actor actor)=>NaturalRating(actor)+actor.DefenseRatingBonus;
}
