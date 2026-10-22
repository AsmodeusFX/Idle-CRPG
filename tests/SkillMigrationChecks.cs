using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillMigrationChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("SKILL_MIGRATION: "+why);}
        var c=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));
        var nodes=c.Talents.Nodes;
        Need(nodes.Count==61,"正式天赋节点数量");
        Need(nodes.Values.Select(n=>n.Hero).Distinct().Count()==5&&c.Heroes.All(h=>nodes.Values.Count(n=>n.Hero==h.Id)>0&&nodes.Values.Where(n=>n.Hero==h.Id).Select(n=>n.Tree).Distinct().Count()==3),"五职业三系覆盖");
        // 旧节点 ID 全部保留，保证同 ID 存档不丢等级；新节点使用自己的 ID 并绑定同名主动定义。
        string[] legacyIds=("barb_voice barb_shout barb_weaken barb_courage barb_mastery barb_attack barb_defense barb_focus barb_bash barb_leap barb_whirlwind barb_power "+
            "ranger_archery ranger_shot ranger_precision ranger_explosive ranger_poison ranger_hide ranger_rally ranger_instinct ranger_suppress "+
            "mage_fire_mastery mage_fireball mage_flame_burst mage_frost_armor mage_icebolt mage_frost_nova mage_storm_mastery mage_lightning mage_storm "+
            "paladin_weapon paladin_smite paladin_hammer paladin_armor paladin_resolve paladin_fortitude paladin_might paladin_shock paladin_zeal "+
            "necro_bone_armor necro_teeth necro_soulbolt necro_spear necro_soul_mastery necro_will necro_curse_mastery necro_weakness necro_frailty").Split(' ');
        Need(legacyIds.All(nodes.ContainsKey),"旧节点 ID 丢失");
        foreach(var n in nodes.Values.Where(n=>n.SkillId!=""))
        {
            Need(n.SkillId==n.Id&&n.Kind=="active","绑定节点必须与同名主动一致："+n.Id);
            Need(c.Skills.Skills.TryGetValue(n.SkillId,out var skill)&&skill.Kind=="ActiveSkill","绑定技能缺失："+n.Id);
            Need(skill!.Effects.All(c.Skills.Effects.ContainsKey),"绑定技能效果缺失："+n.Id);
            Need(skill.Cooldown==checked((int)Math.Ceiling(n.Cooldown*20))&&Math.Abs(skill.Range-n.Range)<.0001f,"绑定技能冷却/距离与节点不一致："+n.Id);
        }
        Need(nodes.Values.All(n=>n.Effect!="data"||n.SkillId!=""),"数据驱动节点缺少绑定");
        var starters=new[]{("warrior","barb_bash"),("ranger","ran_multishot"),("mage","mage_fireball"),("priest","paladin_smite"),("necromancer","nec_raise_skeleton")};
        foreach(var (hero,starter) in starters)
        {
            Need(c.Talents.StartingSkills[hero]==starter,"初始技能映射："+hero);
            var game=new GameSession(c,34,new[]{hero});var h=game.Heroes[0];
            Need(h.Talents.GetValueOrDefault(starter)==1&&h.SkillIds.SequenceEqual(new[]{starter})&&game.TalentPoints(h)==0&&h.StartingSkillGranted,"新角色初始技能状态："+hero);
        }
        SessionSnapshot Round(SessionSnapshot snap)=>SaveStore.Decode(c,SaveStore.Encode(new(){Session=snap})).Session!;
        GameSession Reopen(SessionSnapshot snap)=>GameSession.Restore(c,Round(snap));

        // 旧布局移动到新三系后仍合法的投入必须原样保留，且第二次读档不再迁移。
        var merge=new GameSession(c,31,new[]{"necromancer"});merge.GrantGmResources(0,100000);
        var mergeSnap=merge.CaptureProgress();var mh=mergeSnap.Heroes[0];
        Need(mh.Level>=6,"迁移样本等级不足");
        mh.Talents.Clear();mh.Talents["necro_bone_armor"]=2;mh.Talents["necro_teeth"]=1;mh.Talents["necro_soulbolt"]=1;mh.Talents["necro_soul_mastery"]=1;mh.Talents["necro_will"]=1;mh.Talents["nec_raise_skeleton"]=1;
        mh.SkillIds=new[]{"nec_raise_skeleton"};mergeSnap.SkillTreeVersion=0;
        int Budget(int level)=>c.Talents.Rule("talent_initial_points")+(level-1)*c.Talents.Rule("talent_points_per_level");
        var merged=Reopen(mergeSnap);var mergedHero=merged.Heroes[0];
        Need(mergedHero.Talents.Count==6&&mergedHero.Talents["necro_will"]==1&&merged.TreeInvestment(mergedHero,0)==6,"合法旧投入未保留");
        Need(merged.TalentPoints(mergedHero)==Budget(mergedHero.Level)-7&&mergedHero.SkillIds.SequenceEqual(new[]{"nec_raise_skeleton"}),"迁移后点数或装配变化");
        Need(merged.CaptureProgress().SkillTreeVersion==1,"迁移未写版本");
        var second=Reopen(merged.CaptureProgress());
        Need(second.Heroes[0].Talents.Count==6&&second.Heroes[0].Talents["necro_will"]==1&&second.CaptureProgress().SkillTreeVersion==1,"二次读档重复迁移");

        // 重排后前置/系内投入不再成立的节点退款，并同步清空装配、换入冷却、光环与装备加成。
        var refund=new GameSession(c,32,new[]{"warrior"});refund.GrantGmResources(0,100000);
        var refundSnap=refund.CaptureProgress();var rh=refundSnap.Heroes[0];
        Need(rh.Level>=6,"退款样本等级不足");
        rh.Talents.Clear();rh.Talents["barb_shout"]=1;rh.Talents["barb_whirlwind_window"]=1;
        rh.SkillIds=new[]{"barb_whirlwind_window"};rh.SkillSwapLocks["barb_whirlwind_window"]=3;
        rh.TalentBonuses.Add(new TalentBonus("test-source","node","barb_whirlwind_window",2));
        refundSnap.SkillTreeVersion=0;
        var repaired=Reopen(refundSnap);var repairedHero=repaired.Heroes[0];
        Need(!repairedHero.Talents.ContainsKey("barb_whirlwind_window")&&repairedHero.Talents["barb_shout"]==1,"不兼容节点未退款或误删合法节点");
        Need(repairedHero.SkillIds.Length==0&&repairedHero.SkillSwapLocks.Count==0&&repairedHero.ActiveAuraSkills.Count==0&&repairedHero.TalentBonuses.Count==0,"退款后引用未清理");
        Need(repaired.TalentPoints(repairedHero)==Budget(repairedHero.Level)-1,"退款点数不正确");
        Need(repaired.CaptureProgress().Log.Any(line=>line.Contains("天赋树迁移")),"退款未写入日志");

        // 未知节点视为损坏存档直接拒绝，不做静默退款。
        var corrupt=refund.CaptureProgress();corrupt.Heroes[0].Talents["barb_missing"]=1;corrupt.SkillTreeVersion=0;
        bool rejected=false;try{GameSession.Restore(c,corrupt);}catch(InvalidDataException){rejected=true;}Need(rejected,"未知节点未拒绝");

        // 已发放初始技能的旧档不重复补发新初始技能。
        var oldStarter=new GameSession(c,33,new[]{"ranger"}).CaptureProgress();var oh=oldStarter.Heroes[0];
        oh.StartingSkillGranted=true;oh.Talents.Clear();oh.Talents["ranger_shot"]=1;oh.SkillIds=new[]{"ranger_shot"};oldStarter.SkillTreeVersion=0;
        var migratedStarter=Reopen(oldStarter);
        Need(migratedStarter.Heroes[0].Talents.ContainsKey("ranger_shot")&&!migratedStarter.Heroes[0].Talents.ContainsKey("ran_multishot"),"旧初始技能被重复补发");

        // 绑定节点确实按 skill.csv 的 effect/ability 定义执行：辅助光环与多箭伤害各验一项。
        GameSession Battle(string[] heroes,string skill)
        {
            var game=new GameSession(c,35,heroes);
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            for(int i=0;i<game.Heroes.Count;i++)
            {
                var h=game.Heroes[i];h.Position=room.Center+new Vector2((i-(game.Heroes.Count-1)/2f)*.3f,0);
                h.Hp=h.MaxHp=100000;h.Attack=i==0?10:1;h.Timer=999;h.SkillIds=i==0?new[]{skill}:Array.Empty<string>();
            }
            foreach(var m in room.Monsters){m.Position=room.Center+new Vector2(1,0);m.Hp=m.MaxHp=100000;m.Timer=999;m.Attack=0;}
            return game;
        }
        // 光环以友方为目标，额外带一名队友确保范围语义；伤害技能只带本职业避免其他来源干扰。
        var support=Battle(new[]{"priest","warrior"},"pal_prayer");
        for(int i=0;i<25;i++)support.Tick(.05f);
        Need(support.Capture().SkillState.Buffs.Any(b=>b.Id=="pal_prayer_buff")&&support.Heroes[0].ActiveAuraSkills.Contains("pal_prayer"),"祈祷光环未按绑定定义维持");
        var volley=Battle(new[]{"ranger"},"ran_multishot");int arrows=0;
        for(int i=0;i<40;i++){volley.Tick(.05f);arrows=Math.Max(arrows,volley.ActiveFlightCount);}
        Need(arrows==3,"多重箭未生成三支箭矢");
        Need(volley.Floor.Rooms[volley.CurrentRoom].Monsters.Any(m=>m.Hp<100000),"绑定伤害技能未命中");
        Console.WriteLine("PASS: 61-node three-tree layout, skill_id binding, legacy starter retention, merged-tree save migration, incompatible-node refund/log cleanup, unknown-node rejection, idempotence, bound support and volley execution");
    }
}
