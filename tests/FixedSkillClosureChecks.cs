using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using IdleCrpg.Core;

public static class FixedSkillClosureChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("FIXED_SKILLS: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var team=GameSession.CreateNewWorld(c,1601,new[]{"warrior","warrior","warrior","warrior"});
        Need(team.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==4&&team.ActiveSkillSlots==4,"四同职业实例/固定定义数量");
        // C1-A-1 后五职业共20个槽位全部投放，另有2条职业特性T光环：其余旧主动归入区域0退休。
        Need(c.Talents.Nodes.Values.Count(n=>n.Kind=="active"&&c.TalentRegion(n)==0)==25,"旧主动/退休主动分类不全");
        Need(team.Heroes.All(h=>h.SkillIds.SequenceEqual(new[]{"barb_bash"})),"S1开局已学");
        Need(!team.SetTalentLoadout(team.Heroes[0].HeroInstanceId,Array.Empty<string>(),out _),"旧装配仍可用");
        bool rejected=false;try{team.EquipSkills(team.Heroes[0].HeroInstanceId,Array.Empty<string>());}catch(InvalidOperationException){rejected=true;}Need(rejected,"EquipSkills生产命令未拒绝");
        team.GrantGmResources(gold:10000,experience:100000);
        foreach(var member in team.Heroes)team.UpgradeHero(member.HeroInstanceId,out _);
        foreach(string id in c.Stars.Nodes.Keys.Where(c.Stars.IsRetired))Need(!team.BuyStar(id,out _)&&!team.Stars.Visible(id),"旧星图槽位可购买/可见："+id);
        Need(!team.CanLearnTalent(team.Heroes[0],"barb_shout",out _),"旧主动仍可学习");
        // 已投放资格节点的职业仍须购买；尚未投放资格节点的职业按C1-A过渡规则可学。
        // C1-B B1 收口后：四职业资格节点均已投放，必须购买；尚未投放资格节点的死灵维持可学。
        Need(!team.ClassTraitUnlocked("warrior")&&!team.ClassTraitUnlocked("mage")&&!team.ClassTraitUnlocked("ranger")&&!team.ClassTraitUnlocked("priest"),"四职业资格未收口为真实门禁");
        Need(team.ClassTraitUnlocked("necromancer"),"未投放资格节点的职业被误锁死");
        Need(team.BuyStar("s_root",out _)&&team.BuyStar("s_trait_warrior",out _),"资格购买");
        var hero=team.Heroes[0];var other=team.Heroes[1];int otherPoints=team.TalentPoints(other);
        Need(team.LearnTalent(hero.HeroInstanceId,"warrior_trait",out _)&&other.Talents.GetValueOrDefault("warrior_trait")==0&&team.TalentPoints(other)==otherPoints,"职业资格共享但投资不独立");
        // 同区域互斥与局部重置：仅QA内存改节点组，不投放新生产内容。
        while(team.UpgradeHero(hero.HeroInstanceId,out _)){}
        var mastery=c.Talents.Nodes["barb_mastery"];var attack=c.Talents.Nodes["barb_attack"];
        c.Talents.Nodes[mastery.Id]=mastery with {ExclusiveGroup="QA_fixed_branch"};c.Talents.Nodes[attack.Id]=attack with {ExclusiveGroup="QA_fixed_branch"};
        Need(team.LearnTalent(hero.HeroInstanceId,mastery.Id,out _)&&!team.LearnTalent(hero.HeroInstanceId,attack.Id,out _),"互斥未拒绝");
        Need(team.ResetTalentRegion(hero.HeroInstanceId,2,out _)&&team.LearnTalent(hero.HeroInstanceId,attack.Id,out _),"局部改选未返还");
        Need(hero.Talents.GetValueOrDefault("warrior_trait")==1&&hero.Talents.GetValueOrDefault("barb_bash")==1,"局部重置串区/S1丢失");
        c.Talents.Nodes[mastery.Id]=mastery;c.Talents.Nodes[attack.Id]=attack;

        // 旧档权益：原投入保留，主动返还一次；旧星图金币不退款；真实SaveStore保存后跨Run保留隔离记录。
        var old=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));
        var legacy=GameSession.CreateNewWorld(old,1602,new[]{"warrior"});legacy.GrantGmResources(gold:10000,experience:100000);
        while(legacy.UpgradeHero(legacy.Heroes[0].HeroInstanceId,out _)){}
        for(int rank=0;rank<5;rank++)Need(legacy.LearnTalent(legacy.Heroes[0].HeroInstanceId,"barb_shout",out _),"旧档样本学习");
        legacy.BuyStar("s_root",out _);legacy.BuyStar("s_skill_1",out _);legacy.BuyStar("s_party_1",out _);
        var state=legacy.CaptureProgress();long gold=state.Gold;var originalLoadout=state.Heroes[0].SkillIds.ToArray();
        string raw=SaveStore.Encode(new(){Session=state});
        string sampleDirectory=Path.Combine(root,"artifacts/qa/p2-4-v16/fixed-closure/inputs");Directory.CreateDirectory(sampleDirectory);
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
        string input=Path.Combine(sampleDirectory,"legacy-"+hash+".json");if(!File.Exists(input))File.WriteAllText(input,raw);
        var migrated=GameSession.Restore(c,state);var migratedHero=migrated.Heroes[0];int available=migrated.TalentPoints(migratedHero);
        Need(migrated.RetiredTalentPoints(migratedHero)==5&&migratedHero.LegacySkillIds.SequenceEqual(originalLoadout)&&!migratedHero.SkillIds.Contains("barb_shout")&&migrated.Gold==gold,"迁移未保留价值或擅自退款");
        Need(migrated.ReturnRetiredTalentPoints(migratedHero.HeroInstanceId,out _)&&migrated.TalentPoints(migratedHero)==available+5&&!migrated.ReturnRetiredTalentPoints(migratedHero.HeroInstanceId,out _),"旧点返还不是幂等");
        string output=Path.Combine(root,"artifacts/qa/p2-4-v16/fixed-closure");Directory.CreateDirectory(output);
        var store=new SaveStore(Path.Combine(output,"progress.json"));store.Save(migrated);
        var document=store.Load(c)!;var restored=GameSession.Restore(c,document.Run!);
        Need(restored.TalentPoints(restored.Heroes[0])==available+5&&restored.RetiredTalentPoints(restored.Heroes[0])==0&&restored.Gold==gold,"重读重复补偿");
        var fresh=GameSession.StartRun(c,1603,new[]{"warrior"},restored.AbandonRun());store.Save(fresh);
        Need(store.Load(c)!.Legacy.Skills.Single().Investments.Single(p=>p.NodeId=="barb_shout").Returned&&store.Legacy.Stars.Count==1,"放弃后隔离丢失");
        Need(File.ReadAllText(input)==raw,"旧档输入样本被改写");

        GameSession Battle(int seed,string[] heroes)
        {
            var game=GameSession.CreateWorld(c,seed,heroes);
            for(int tick=0;tick<20000&&game.CurrentRoom==0;tick++)game.Tick(.05f);
            while(game.CombatEntryRemaining>0)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            foreach(var h in game.Heroes){h.Hp=h.MaxHp=100000;h.Position=room.Center;h.Timer=999;h.Attack=1;}
            foreach(var m in room.Monsters){m.Hp=m.MaxHp=100000;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(1,0);}
            return game;
        }
        var fight=Battle(1604,new[]{"warrior","warrior"});var fighter=fight.Heroes[0];
        fight.GrantGmResources(experience:1000);fight.UpgradeHero(fighter.HeroInstanceId,out _);
        for(int tick=0;tick<35;tick++)fight.Tick(.05f);
        Need(!fight.Capture().SkillState.Buffs.Any(b=>b.Id=="barb_taunt_buff"),"未学S2已有特效");
        Need(fight.LearnTalent(fighter.HeroInstanceId,"barb_whirlwind_window",out _),"S2学习失败");
        for(int tick=0;tick<35;tick++)fight.Tick(.05f);
        Need(fight.Capture().SkillState.Buffs.Any(b=>b.Id=="barb_whirlwind_buff"&&b.Owner==0)&&fight.SkillCooldown(fighter,"barb_whirlwind_window")>0,"S2没有实际Buff/CD");
        double otherCd=fight.SkillCooldown(fight.Heroes[1],"barb_bash");
        Need(fight.ResetTalentRegion(fighter.HeroInstanceId,2,out _)&&!fight.Capture().SkillState.Buffs.Any(b=>b.Owner==0&&b.Id=="barb_whirlwind_buff")&&fight.SkillCooldown(fight.Heroes[1],"barb_bash")==otherCd,"洗点残留/串其他英雄CD");
        for(int tick=0;tick<100;tick++)fight.Tick(.05f);
        Need(!fight.Capture().SkillState.Buffs.Any(b=>b.Id=="barb_whirlwind_buff")&&fighter.SkillIds.Contains("barb_bash"),"洗点后S2复活或S1丢失");

        // 多来源Buff/撤销单源：守护光环（T）按来源区分，四同职各一份贡献。
        var priests=Battle(1605,new[]{"priest","priest","priest","priest"});
        priests.GrantGmResources(experience:10000);foreach(var priest in priests.Heroes)priests.UpgradeHero(priest.HeroInstanceId,out _);
        // C1-B B1：圣骑士T需先购买职业资格（同职业共享一次）。
        priests.GrantGmResources(gold:10000);
        Need(priests.BuyStar("s_root",out string auraRoot),$"圣骑士资格购买失败：{auraRoot}");
        Need(priests.BuyStar("s_atk",out string auraAtk),$"圣骑士资格购买失败：{auraAtk}");
        Need(priests.BuyStar("C-PAL",out string auraGate),$"圣骑士资格购买失败：{auraGate}");
        foreach(var priest in priests.Heroes)Need(priests.LearnTalent(priest.HeroInstanceId,"priest_trait",out string auraWhy),"四同职光环学习："+auraWhy);
        for(int tick=0;tick<60;tick++)priests.Tick(.05f);
        var buffs=priests.Capture().SkillState.Buffs.Where(b=>b.Id=="pal_guard_buff"&&b.Target==0).ToArray();
        Need(buffs.Select(b=>b.Owner).Distinct().Count()==4,"同目标四来源Buff未真实存在");
        File.WriteAllLines(Path.Combine(output,"buff_sources.csv"),new[]{"buff_id,target_combat_id,source_hero_instance_id,remaining,stacking"}.Concat(buffs.Select(b=>$"{b.Id},{b.Target},{priests.Heroes[b.Owner].HeroInstanceId},{b.Remaining},Strongest")));
        var qaSnapshot=priests.Capture();
        var full=GameSession.Restore(c,qaSnapshot);Need(full.Capture().SkillState.Buffs.Count(b=>b.Id=="pal_guard_buff"&&b.Target==0)==4,"完整快照丢多来源Buff");
        Need(priests.ResetTalentRegion(priests.Heroes[0].HeroInstanceId,5,out _)&&priests.Capture().SkillState.Buffs.Where(b=>b.Id=="pal_guard_buff"&&b.Target==0).Select(b=>b.Owner).Distinct().Count()<=3,"撤销单来源误删其他贡献");
        var progress=GameSession.Restore(c,priests.CaptureProgress());Need(!progress.Capture().SkillState.Buffs.Any(),"正式检查点复活旧Buff现场");
        var rangers=Battle(1606,new[]{"ranger","ranger"});rangers.GrantGmResources(experience:10000);
        foreach(var ranger in rangers.Heroes){rangers.UpgradeHero(ranger.HeroInstanceId,out _);Need(rangers.LearnTalent(ranger.HeroInstanceId,"ran_valkyrie",out _),"S3召唤学习");}
        for(int tick=0;tick<60;tick++)rangers.Tick(.05f);
        Need(rangers.Summons.Where(s=>s.Alive).Select(s=>s.SummonOwnerInstanceId).Distinct().Count()==2,"重复职业召唤来源不独立");
        double independentCooldown=rangers.SkillCooldown(rangers.Heroes[1],"ran_valkyrie");
        Need(rangers.ResetTalentRegion(rangers.Heroes[0].HeroInstanceId,4,out _)&&rangers.Summons.Where(s=>s.Alive).All(s=>s.SummonOwnerInstanceId==rangers.Heroes[1].HeroInstanceId)&&rangers.SkillCooldown(rangers.Heroes[1],"ran_valkyrie")==independentCooldown,"局部洗点串另一实例召唤/CD");
        Console.WriteLine("FIXED_SKILLS_PASS: 4+T authority, 25 retired nodes, migration/refund idempotence, legacy quarantine across Run, real S2 effect/reset, four-source same-target Buff and checkpoint semantics");
    }
}
