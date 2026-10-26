using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// S15-C1-A-1 工程自检：四职业16固定技能与4职业特性T必须真实生效（伤害/治疗/Buff/召唤/光环），
/// 未学S2-S4不得施放，T互斥且不占主动槽，FixedSkillsVersion=2 读档与旧档槽位重排迁移可用。
/// 只读配置与内存会话，不写玩家进度。
/// </summary>
public static class C1ASkillChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C1A_SKILL: "+why);}

    // 设计ID → 生产技能ID 的显式映射（C1-A-1 唯一口径）
    private static readonly (string Hero,string Design,int Slot,string Skill)[] Mapping={
        ("warrior","BAR-S1",1,"barb_bash"),("warrior","BAR-S2",2,"barb_whirlwind_window"),
        ("warrior","BAR-S3",3,"barb_taunt"),("warrior","BAR-S4",4,"barb_ancestor"),
        ("ranger","RAN-S1",1,"ran_shot"),("ranger","RAN-S2",2,"ran_multishot"),
        ("ranger","RAN-S3",3,"ran_guided_arrow"),("ranger","RAN-S4",4,"ran_valkyrie"),
        ("mage","SOR-S1",1,"mage_fireball"),("mage","SOR-S2",2,"sor_frost_nova"),
        ("mage","SOR-S3",3,"sor_chain_lightning"),("mage","SOR-S4",4,"sor_meteor"),
        ("priest","PAL-S1",1,"paladin_smite"),("priest","PAL-S2",2,"pal_shield_bash"),
        ("priest","PAL-S3",3,"pal_prayer"),("priest","PAL-S4",4,"pal_hammer")
    };

    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        // 1) 16槽位映射与定义完整性
        foreach(var m in Mapping)
        {
            var slots=c.ClassSkillSlots(m.Hero);
            Need(slots[m.Slot-1]==m.Skill,$"{m.Design} 槽位映射错误：期望{m.Skill} 实际{slots[m.Slot-1]}");
            Need(c.Skills.Skills.TryGetValue(m.Skill,out var def)&&def.Effects.Length>0,$"{m.Skill} 缺少主动定义或效果");
            Need(c.Talents.Nodes.TryGetValue(m.Skill,out var node)&&node.SkillId==m.Skill&&node.Kind=="active",$"{m.Skill} 缺少可学习绑定节点");
        }
        foreach(var hero in new[]{"warrior","ranger","mage","priest"})
            Need(c.ClassSkillSlots(hero).All(id=>id!=""),$"{hero} 仍有空技能槽");
        // 2) 四职业特性T：存在、可观察、互斥且不占S1-S4
        foreach(var hero in new[]{"warrior","ranger","mage","priest"})
            Need(c.Talents.Nodes.Values.Any(n=>n.Hero==hero&&n.ClassTrait),$"{hero} 缺少职业特性T节点");
        Need(!c.ClassSkillSlots("priest").Contains("pal_guard_aura"),"T光环不得占用S1-S4槽");

        // 3) 逐技能真实行为（正式技能/AI/伤害管线，固定seed）
        foreach(var m in Mapping)
        {
            var game=GameSession.CreateSkillPreview(c,m.Hero,m.Skill,1,"rat",3,1);
            var hero=game.Heroes[0];float hp0=hero.Hp;long hits0=game.HitCount;
            for(int i=0;i<400;i++)game.TickSkillPreview(.05f);
            bool damage=game.HitCount>hits0;
            bool summon=game.Summons.Count>0;
            bool aura=hero.ActiveAuraSkills.Count>0;
            bool healed=hero.Hp>hp0+.01f;
            bool buffed=game.Capture().SkillState.Buffs.Count>0;
            Need(damage||summon||aura||healed||buffed,$"{m.Skill} 400tick内没有任何可观察行为");
            if(m.Skill is "barb_ancestor" or "ran_valkyrie")Need(summon,$"{m.Skill} 未召唤出实体");
            if(m.Skill=="pal_prayer")Need(healed,$"{m.Skill} 未产生真实治疗");
            if(m.Skill=="sor_frost_nova")Need(buffed||damage,$"{m.Skill} 未产生减速或伤害");
            if(m.Skill=="pal_shield_bash")Need(damage,"盾击未造成伤害");
        }

        // 4) 未学习S2-S4不得施放；学习后进入队列且仅本人
        var world=GameSession.CreateNewWorld(c,20261026,new[]{"warrior","warrior"});
        var w=world.Heroes[0];
        Need(world.LearnedClassSkills(w).SequenceEqual(new[]{"barb_bash"}),"未学习时队列含未学技能");
        world.GrantGmResources(10000,100000);
        while(world.UpgradeHero(w.HeroInstanceId,out _)){}
        Need(world.LearnTalent(w.HeroInstanceId,"barb_whirlwind_window",out string why1),$"学习S2失败：{why1}");
        Need(world.LearnedClassSkills(w).SequenceEqual(new[]{"barb_bash","barb_whirlwind_window"}),"学习S2后队列不正确");
        Need(!world.LearnedClassSkills(world.Heroes[1]).Contains("barb_whirlwind_window"),"S2学习串到同职另一实例");
        // 局部洗点只清S2，S1保底
        Need(world.ResetTalentRegion(w.HeroInstanceId,2,out string why2),$"局部洗点失败：{why2}");
        Need(world.LearnedClassSkills(w).SequenceEqual(new[]{"barb_bash"}),"洗点后S1未保底或残留S2");

        // 5) T互斥与实例独立（守护/进攻光环同一时刻只能一条）
        var pal=GameSession.CreateWorld(c,20261027,new[]{"priest","priest"});
        var p0=pal.Heroes[0];pal.GrantGmResources(10000,100000);
        while(pal.UpgradeHero(p0.HeroInstanceId,out _)){}
        // C1-B B1：四职业T资格已收口为真实门禁，学习T前必须先购买本职业资格节点。
        Need(pal.BuyStar("s_root",out string gateRoot),$"购买星图根失败：{gateRoot}");
        Need(pal.BuyStar("s_atk",out string gateAtk),$"购买攻击节点失败：{gateAtk}");
        Need(pal.BuyStar("C-PAL",out string gatePal),$"购买圣骑士资格失败：{gatePal}");
        Need(pal.LearnTalent(p0.HeroInstanceId,"priest_trait",out string why3),$"学习守护T失败：{why3}");
        Need(!pal.LearnTalent(p0.HeroInstanceId,"pal_trait_zeal",out _),"互斥T仍可同时学习");
        Need(!pal.Heroes[1].Talents.ContainsKey("priest_trait"),"T投入串到同职另一实例");
        for(int i=0;i<40;i++)pal.Tick(.05f);
        Need(p0.ActiveAuraSkills.Contains("pal_guard_aura"),"守护T未维持光环");
        Need(!p0.SkillIds.Contains("pal_guard_aura"),"T光环误入主动施法队列");
        // 洗掉T后光环撤销
        Need(pal.ResetTalentRegion(p0.HeroInstanceId,5,out string why4),$"T洗点失败：{why4}");
        for(int i=0;i<3;i++)pal.Tick(.05f);
        Need(!p0.ActiveAuraSkills.Contains("pal_guard_aura"),"T洗点后光环未撤销");

        // 6) FixedSkillsVersion=2 读写与旧档槽位重排迁移
        var live=GameSession.CreateWorld(c,20261028,new[]{"priest"});
        var ph=live.Heroes[0];live.GrantGmResources(10000,100000);
        while(live.UpgradeHero(ph.HeroInstanceId,out _)){}
        Need(live.BuyStar("s_root",out _)&&live.BuyStar("s_atk",out _)&&live.BuyStar("C-PAL",out _),"读档用例资格购买失败");
        Need(live.LearnTalent(ph.HeroInstanceId,"priest_trait",out string whyTrait),$"读档用例学习守护T失败：{whyTrait}");
        live.LearnTalent(ph.HeroInstanceId,"pal_prayer",out _);
        for(int i=0;i<20;i++)live.Tick(.05f);
        var snap=live.CaptureProgress();
        Need(snap.FixedSkillsVersion==2,$"新档固定技能版本应为2，实际{snap.FixedSkillsVersion}");
        var restored=GameSession.Restore(c,snap);
        var rh=restored.Heroes[0];
        Need(rh.ActiveAuraSkills.Contains("pal_guard_aura"),"读档后T光环丢失");
        Need(restored.LearnedClassSkills(rh).SequenceEqual(live.LearnedClassSkills(ph)),"读档后技能队列不一致");

        // 旧档：v1 + 旧槽位（S3 为 pal_fanaticism，现已不在固定顺位）+ 旧队列，必须可读并隔离释放点数
        var old=GameSession.CreateWorld(c,20261029,new[]{"priest"});
        var oh=old.Heroes[0];old.GrantGmResources(10000,100000);
        while(old.UpgradeHero(oh.HeroInstanceId,out _)){}
        var legacy=old.CaptureProgress();
        legacy.FixedSkillsVersion=1;
        legacy.Heroes[0].Talents["pal_fanaticism"]=1;
        legacy.Heroes[0].SkillIds=new[]{"paladin_smite","pal_prayer","pal_fanaticism"};
        var migrated=GameSession.Restore(c,legacy);
        var mh=migrated.Heroes[0];
        Need(migrated.CaptureProgress().FixedSkillsVersion==2,"旧档未升级到固定技能版本2");
        Need(mh.SkillIds.SequenceEqual(new[]{"paladin_smite"}),"旧档未按新槽位重排队列：实际 "+string.Join(',',mh.SkillIds));
        Need(mh.RetiredTalents.Any(p=>p.NodeId=="pal_fanaticism"&&!p.Returned),"旧档退休主动未进入隔离记录");

        Console.WriteLine("C1A_SKILL_PASS: 16/16槽位映射与真实行为、未学不出手、T互斥/实例独立/洗点撤销、v2读档与v1槽位重排迁移");
    }
}
