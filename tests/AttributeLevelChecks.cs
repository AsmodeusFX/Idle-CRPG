using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

public static class AttributeLevelChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("ATTRIBUTE_LEVEL_CHECK: "+why);}
        var config=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));var attrs=config.Attributes;
        Check(attrs.Definitions.Count==attrs.Rules.Count&&attrs.Definitions.ContainsKey("dodge_rate")&&attrs.Definitions.ContainsKey("power_damage_pct"),"属性与规则没有一一注册");
        Check(attrs.Aggregate("atk",100,new[]{new BuffAttributeSpec("atk","flat",20),new("atk","pct",.5)})==180,"基础/固定/百分比聚合");
        Check(attrs.Aggregate("crit_rate",.8,new[]{new BuffAttributeSpec("crit_rate","add",.5)})==1,"概率规则上限");
        Check(attrs.Aggregate("power_damage_pct",0,new[]{new BuffAttributeSpec("power_damage_pct","add",.5),new("power_damage_pct","add",1)})==1.5,"威能贡献没有同池加算");
        bool rejected=false;try{attrs.ValidateContribution(new("not_registered","add",1));}catch(InvalidDataException){rejected=true;}Check(rejected,"未注册属性未拒绝");
        rejected=false;try{attrs.ValidateContribution(new("atk","add",1));}catch(InvalidDataException){rejected=true;}Check(rejected,"非法属性操作未拒绝");
        var game=GameSession.CreateWorld(config,345,new[]{"warrior","ranger","mage"});
        var hp=game.Heroes.Select(h=>h.MaxHp).ToArray();var attack=game.Heroes.Select(h=>h.Attack).ToArray();var armor=game.Heroes.Select(h=>h.Defense).ToArray();
        game.Heroes[0].Hp-=10;game.Heroes[1].Hp=0;
        game.GrantGmResources(experience:config.HeroLevels[1].Experience-1);Check(game.Heroes[0].Level==1,"未达到表中经验就升级");
        game.GrantGmResources(experience:1);
        for(int i=0;i<3;i++)Check(game.Heroes[i].Level==2&&game.Heroes[i].Attack==attack[i]+4&&game.Heroes[i].MaxHp==hp[i]+18&&game.Heroes[i].Defense==armor[i],"英雄升级不只改变 ATK/HP 或表值错误");
        Check(game.Heroes[0].Hp==hp[0]+8&&game.Heroes[1].Hp==0,"升级治疗不是原有增量规则，或复活了阵亡队员");
        var restored=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=game.CaptureProgress()})).Session!);
        Check(restored.Heroes[0].Attack==game.Heroes[0].Attack&&restored.Heroes[0].MaxHp==game.Heroes[0].MaxHp,"读档重复叠加等级贡献");
        var spec=config.Monsters.Single(m=>m.Id=="guard");var normal=config.Spawn.Create(spec,5,false,new());var boss=config.Spawn.Create(spec,5,true,new());
        Check(Math.Abs(normal.MaxHp-spec.Hp*1.72)<.001&&Math.Abs(normal.Attack-spec.Attack*1.48)<.001,"怪物等级未使用表中倍率");
        Check(normal.Defense==spec.Defense&&boss.Defense==spec.Defense,"怪物等级仍改变了护甲");
        Check(boss.MaxHp==normal.MaxHp*config.Spawn.BossHp&&Math.Abs(boss.Attack-normal.Attack*config.Spawn.BossAttack)<.001,"BOSS 倍率重复应用或丢失");
        Check(normal.RewardXp==config.Spawn.XpBase*5&&boss.RewardXp==normal.RewardXp*config.Spawn.BossReward,"等级表改变了独立经验规则");
        rejected=false;try{config.Spawn.Create(spec,config.MaxMonsterLevel+1,false,new());}catch(InvalidDataException){rejected=true;}Check(rejected,"怪物超过表尾未拒绝");

        string qa=Path.Combine(root,"artifacts/qa/attribute-level-"+Guid.NewGuid().ToString("N"));string source=Path.Combine(root,"Idle-CRPG/config");
        foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
        {string target=Path.Combine(qa,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
        string heroFile=Path.Combine(qa,"hero/herolevel.csv");var heroLines=File.ReadAllLines(heroFile);File.WriteAllLines(heroFile,heroLines.Take(4));
        var capped=LegacySkillConfig.LoadLegacy(qa);var cappedGame=GameSession.CreateWorld(capped,1,new[]{"warrior","ranger","mage"});cappedGame.GrantGmResources(experience:10000);
        Check(capped.MaxHeroLevel==3&&cappedGame.Heroes.All(h=>h.Level==3)&&cappedGame.Heroes[0].Attack==capped.Heroes[0].Attack+8,"英雄表尾没有作为上限");
        File.WriteAllLines(heroFile,new[]{heroLines[0],heroLines[1],heroLines[3]});
        rejected=false;try{LegacySkillConfig.LoadLegacy(qa);}catch(CsvConfigurationException){rejected=true;}Check(rejected,"等级缺行未拒绝");File.WriteAllLines(heroFile,heroLines);
        string buffFile=Path.Combine(qa,"skill/buff.csv");var buffText=File.ReadAllText(buffFile);File.WriteAllText(buffFile,buffText.Replace("attack_speed_pct","missing_attribute"));
        rejected=false;try{LegacySkillConfig.LoadLegacy(qa);}catch(InvalidDataException){rejected=true;}Check(rejected,"Buff 绕过属性注册表");File.WriteAllText(buffFile,buffText);
        string rulesFile=Path.Combine(qa,"combat/fightattr_rules.csv");var rulesText=File.ReadAllText(rulesFile);File.WriteAllText(rulesFile,rulesText.Replace("crit_rate,0,0,1","crit_rate,2,0,1"));
        rejected=false;try{LegacySkillConfig.LoadLegacy(qa);}catch(CsvConfigurationException){rejected=true;}Check(rejected,"非法属性基线没有拒绝");
        Console.WriteLine("PASS: fightattr registry/operations/bounds, hero cumulative growth, monster ATK/HP-only scaling, level caps, save retention and malformed CSV rejection");
    }
}
