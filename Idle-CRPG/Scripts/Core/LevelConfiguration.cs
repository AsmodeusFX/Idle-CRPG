using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record HeroLevelDefinition(int Level,int Experience,float AttackBonus,float HpBonus);
public record MonsterLevelDefinition(int Level,float HpMultiplier,float AttackMultiplier);
public sealed partial class Config
{
    public IReadOnlyDictionary<int,HeroLevelDefinition> HeroLevels { get; private set; }=null!;
    public IReadOnlyDictionary<int,MonsterLevelDefinition> MonsterLevels { get; private set; }=null!;
    public int MaxHeroLevel=>HeroLevels.Count;
    public int MaxMonsterLevel=>MonsterLevels.Count;
    private void LoadLevels(string root)
    {
        CsvTable Table(string relative,params string[] columns){string path=Path.Combine(root,relative);return CsvTable.Load(path,File.ReadAllText(path),columns);}
        LoadExperienceRates(root);
        var heroes=new Dictionary<int,HeroLevelDefinition>();
        foreach(var r in Table("hero/herolevel.csv","id","exp","attack_bonus","hp_bonus").Rows)
        {
            int level=r.Integer("id");var row=new HeroLevelDefinition(level,r.Integer("exp"),(float)r.Number("attack_bonus"),(float)r.Number("hp_bonus"));
            if(level!=heroes.Count+1||row.Experience<1||row.AttackBonus<0||row.HpBonus<0||!float.IsFinite(row.AttackBonus)||!float.IsFinite(row.HpBonus)||level>1&&(row.AttackBonus<heroes[level-1].AttackBonus||row.HpBonus<heroes[level-1].HpBonus))throw r.Error("id","英雄等级必须从1连续递增，累计属性不能倒退");heroes.Add(level,row);
        }
        var monsters=new Dictionary<int,MonsterLevelDefinition>();
        foreach(var r in Table("monster/monsterlevel.csv","id","hp_multiplier","attack_multiplier").Rows)
        {
            int level=r.Integer("id");var row=new MonsterLevelDefinition(level,(float)r.Number("hp_multiplier"),(float)r.Number("attack_multiplier"));
            if(level!=monsters.Count+1||row.HpMultiplier<=0||row.AttackMultiplier<=0||!float.IsFinite(row.HpMultiplier)||!float.IsFinite(row.AttackMultiplier)||level>1&&(row.HpMultiplier<monsters[level-1].HpMultiplier||row.AttackMultiplier<monsters[level-1].AttackMultiplier))throw r.Error("id","怪物等级必须从1连续递增，倍率为正且不能倒退");monsters.Add(level,row);
        }
        if(heroes.Count==0||monsters.Count==0||heroes[1].AttackBonus!=0||heroes[1].HpBonus!=0||monsters[1].AttackMultiplier!=1||monsters[1].HpMultiplier!=1)throw new InvalidDataException("等级表必须有1级基线，英雄累计加值为0、怪物倍率为1");
        HeroLevels=heroes;MonsterLevels=monsters;Spawn=Spawn with {Levels=monsters};
        if(Dungeons.Any(d=>d.LevelMax+(long)Spawn.Level-1>MaxMonsterLevel))throw new InvalidDataException("地牢等级超过怪物等级表上限");
    }
}
