using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// WP4 星图侧切片：经验成长节点、按职业共享的特性解锁资格、购买扣金与读档一致性。
/// 验证的是机制与数据合同，节点价格/数值为 [H] 原型。
/// </summary>
public static class Wp4StarChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("WP4_STAR: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var game=GameSession.CreateNewWorld(c,6611,new[]{"warrior","warrior","ranger"});
        game.GrantGmResources(gold:10000,experience:200000);
        foreach(var hero in game.Heroes.Take(2))Check(game.UpgradeHero(hero.HeroInstanceId,out string up),$"升级失败：{up}");

        // 未购买特性解锁前，两个同职业实例都学不了职业特性；其他职业同样锁定。
        var warriorOne=game.Heroes[0];var warriorTwo=game.Heroes[1];
        Check(!game.CanLearnTalent(warriorOne,"warrior_trait",out string locked)&&locked.Contains("星图"),"未解锁时职业特性未被拒绝："+locked);
        Check(!game.CanLearnTalent(game.Heroes[2],"ranger_trait",out _),"游侠特性在未解锁时可学");

        // 成长类节点：购买后公共XP获取按比例提高，且只扣一次金币。
        long goldBefore=game.Gold;
        Check(game.BuyStar("s_root",out string rootWhy),$"根节点购买失败：{rootWhy}");
        Check(game.BuyStar("s_xp_01",out string xpWhy),$"经验节点购买失败：{xpWhy}");
        Check(game.XpGainMultiplier>1.0,$"经验增益未生效：{game.XpGainMultiplier}");
        long goldAfterXp=game.Gold;
        Check(goldAfterXp==goldBefore-game.Config.Stars.Nodes["s_root"].Costs[0]-game.Config.Stars.Nodes["s_xp_01"].Costs[0],"经验节点扣金不正确");
        long poolBefore=game.PublicExperience;
        game.GrantGmResources(experience:100);
        long gained=game.PublicExperience-poolBefore;
        Check(gained==(long)Math.Floor(100*game.XpGainMultiplier),$"入池经验未应用星图增益：{gained}");

        // 职业特性解锁：购买一次，同职业全部实例共享资格，不需重复购买，也不自动扣天赋点。
        Check(game.BuyStar("s_trait_warrior",out string traitWhy),$"特性解锁购买失败：{traitWhy}");
        // C1-B B1 收口：四职业资格均需购买，未购买的同职/他职都保持锁定。
        Check(game.ClassTraitUnlocked("warrior")&&!game.ClassTraitUnlocked("ranger")&&!game.ClassTraitUnlocked("priest")&&!game.ClassTraitUnlocked("mage"),"职业特性资格购买/收口规则错误");
        int pointsTwo=game.TalentPoints(warriorTwo);
        Check(game.LearnTalent(warriorOne.HeroInstanceId,"warrior_trait",out string learnOne),$"本职业特性学习失败：{learnOne}");
        Check(game.LearnTalent(warriorTwo.HeroInstanceId,"warrior_trait",out string learnTwo),$"同职业另一实例学习失败：{learnTwo}");
        Check(game.TalentPoints(warriorTwo)==pointsTwo-1,"同职业另一实例的天赋点未被独立扣除");
        Check(!game.CanLearnTalent(game.Heroes[2],"ranger_trait",out _),"其他职业被误开放特性");

        // 读档：星图等级、经验增益与职业资格保持，重复读档不重复扣金。
        long goldAfterTrait=game.Gold;
        var reloaded=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=game.CaptureProgress()})).Session!);
        Check(reloaded.Gold==goldAfterTrait&&reloaded.XpGainMultiplier==game.XpGainMultiplier,"读档后金币或经验增益不一致");
        Check(reloaded.ClassTraitUnlocked("warrior")&&!reloaded.ClassTraitUnlocked("ranger"),"读档后职业特性资格丢失或误开放");
        var again=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=reloaded.CaptureProgress()})).Session!);
        Check(again.Gold==goldAfterTrait&&again.Stars.Level("s_trait_warrior")==1,"二次读档重复扣金或等级漂移");

        Console.WriteLine("WP4_STAR_PASS: 经验成长节点、按职业共享的特性解锁、同职业双实例独立投点与读档一致");
    }
}
