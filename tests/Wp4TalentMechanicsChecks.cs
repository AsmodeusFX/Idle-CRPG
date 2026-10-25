using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// WP4 机制切片：天赋互斥分支、免费局部洗点、职业特性标记与按实例隔离。
/// 只验证机制与数据合同，不代表五职业完整技能内容或正式数值已交付。
/// </summary>
public static class Wp4TalentMechanicsChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("WP4_TALENT: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        // 数据合同：互斥组至少两名同职业成员，职业特性节点每职业各一。
        foreach(var group in c.Talents.Nodes.Values.Where(n=>n.ExclusiveGroup!="").GroupBy(n=>n.ExclusiveGroup))
        {
            Check(group.Count()>=2&&group.Select(n=>n.Hero).Distinct().Count()==1,$"互斥组 {group.Key} 成员不足或跨职业");
        }
        foreach(var hero in c.Heroes)
        {
            Check(c.Talents.Nodes.Values.Count(n=>n.Hero==hero.Id&&n.ClassTrait)==1,$"{hero.Id} 的职业特性节点数量不是 1");
        }
        Check(c.Talents.Nodes.Values.Any(n=>n.ClassTrait&&n.Hero=="warrior"),"缺少野蛮人职业特性节点");
        // 4+1 机制容量：每职业四槽齐全（允许显式留空），已投放槽必须引用本职业可学习主动。
        int deployed=0;
        foreach(var hero in c.Heroes)
        {
            var slots=c.ClassSkillSlots(hero.Id);
            Check(slots.Length==4,$"{hero.Id} 技能槽数量不是4");
            deployed+=c.DeployedClassSkills(hero.Id);
            foreach(string id in slots.Where(id=>id!=""))Check(c.Talents.Nodes.TryGetValue(id,out var slotNode)&&slotNode.Hero==hero.Id&&slotNode.Kind=="active",$"{id} 不是本职业可学习主动技能");
        }
        Check(deployed==16,$"已投放职业主动技能数应为16，实际 {deployed}");

        var game=GameSession.CreateNewWorld(c,5150,new[]{"warrior","warrior"});
        var first=game.Heroes[0];var second=game.Heroes[1];
        game.GrantGmResources(experience:300000);
        for(int level=0;level<8;level++)Check(game.UpgradeHero(first.HeroInstanceId,out string up),$"升级失败：{up}");
        int startPoints=game.TalentPoints(first);
        for(int rank=0;rank<5;rank++)Check(game.LearnTalent(first.HeroInstanceId,"barb_shout",out string shout),$"战吼学习失败：{shout}");
        Check(game.LearnTalent(first.HeroInstanceId,"barb_taunt",out string taunt),$"嘲讽学习失败：{taunt}");
        Check(!game.CanLearnTalent(first,"barb_weaken",out string exclusive)&&exclusive.Contains("互斥"),"同树互斥分支未被拒绝："+exclusive);
        Check(game.TalentPoints(first)==startPoints-6,$"点消耗异常：{startPoints}->{game.TalentPoints(first)}");
        int secondPoints=game.TalentPoints(second);
        var secondTalents=second.Talents.ToDictionary(p=>p.Key,p=>p.Value);

        // 免费局部洗点：只重置区域0，区域1投入保留，另一实例不受影响。
        Check(game.LearnTalent(first.HeroInstanceId,"barb_mastery",out string mastery),$"区域1学习失败：{mastery}");
        int beforeReset=game.TalentPoints(first);
        Check(game.ResetTalentRegion(first.HeroInstanceId,0,out string reset),$"局部洗点失败：{reset}");
        Check(game.TalentPoints(first)==beforeReset+6,$"局部洗点返还点数不正确：{beforeReset}->{game.TalentPoints(first)}");
        Check(!first.Talents.ContainsKey("barb_taunt")&&!first.Talents.ContainsKey("barb_shout"),"洗点后仍残留该区域节点");
        Check(first.Talents.GetValueOrDefault("barb_mastery")==1,"局部洗点误删其他区域投入");
        Check(!first.SkillIds.Contains("barb_taunt"),"洗点后技能仍在自动施放队列");
        Check(game.TalentPoints(second)==secondPoints&&second.Talents.Count==secondTalents.Count&&secondTalents.All(p=>second.Talents.GetValueOrDefault(p.Key)==p.Value),"洗点影响到另一实例");

        // 改选另一分支并读档核对（点数与投入一致，不重复返还）。
        for(int rank=0;rank<5;rank++)Check(game.LearnTalent(first.HeroInstanceId,"barb_shout",out string shout2),$"改选后重学战吼失败：{shout2}");
        Check(game.LearnTalent(first.HeroInstanceId,"barb_weaken",out string weaken),$"改选分支学习失败：{weaken}");
        Check(!game.CanLearnTalent(first,"barb_taunt",out _),"改选后仍可回到原分支");
        int afterSwitch=game.TalentPoints(first);
        var reloaded=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=game.CaptureProgress()})).Session!);
        var reloadedFirst=reloaded.Heroes[0];
        Check(reloaded.TalentPoints(reloadedFirst)==afterSwitch,"读档后可用点漂移");
        Check(reloadedFirst.Talents.GetValueOrDefault("barb_weaken")==1&&!reloadedFirst.Talents.ContainsKey("barb_taunt"),"读档后互斥选择不一致");
        Check(reloadedFirst.Talents.GetValueOrDefault("barb_mastery")==1,"读档后区域1投入丢失");
        var twice=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=reloaded.CaptureProgress()})).Session!);
        Check(twice.TalentPoints(twice.Heroes[0])==afterSwitch,"二次读档重复返还点数");

        Console.WriteLine("WP4_TALENT_PASS: 互斥分支、免费局部洗点、职业特性标记与实例隔离在存档前后一致");
    }
}
