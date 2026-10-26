using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>旧投入原样隔离；Returned仅表示玩家已主动取回点数，原节点/等级永久保留用于追溯。</summary>
public sealed record RetiredTalent(string NodeId,int Rank,bool Returned=false);
public sealed partial class Config
{
    // 仅测试程序集显式启用归档行为，正式配置加载器和UI不设置此开关。
    internal bool LegacySkillRuntime=false;
    /// <summary>固定技能顺位为1～4，T为5；旧被动沿原三系归入对应区域，未绑定旧主动为0（退休）。</summary>
    public int TalentRegion(TalentNode node)
    {
        if(node.ClassTrait||node.Kind=="trait"||InTraitRoute(node))return 5;
        if(node.Kind=="passive")return node.Tree+1;
        // 修饰器/触发源跟随被作用的槽位技能（S2xx→区域2、S3xx→3、S4xx→4），保证区域洗点与UI分组一致。
        int index=Array.IndexOf(ClassSkillSlots(node.Hero),node.Kind is "modifier" or "trigger"?node.SkillId:node.Id);
        return index<0?0:index+1;
    }
    /// <summary>T路线节点：前置链（含&amp;表达式）可达职业特性根的节点一律归入T区域，避免特性路线节点散落到S区域。</summary>
    private bool InTraitRoute(TalentNode node)
    {
        var stack=new Stack<string>();var seen=new HashSet<string>();
        void Push(string expression){foreach(string token in expression.Split('&',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))stack.Push(token.Split('@')[0]);}
        Push(node.Prerequisite);
        while(stack.Count>0)
        {
            string id=stack.Pop();if(!seen.Add(id))continue;
            if(!Talents.Nodes.TryGetValue(id,out var parent))continue;
            if(parent.ClassTrait)return true;
            Push(parent.Prerequisite);
        }
        return false;
    }
    /// <summary>未进入固定职业定义的旧主动只保留归档，不参与生产学习或自动施放。</summary>
    public bool IsRetiredTalent(string id)=>Talents.Nodes.TryGetValue(id,out var node)&&TalentRegion(node)==0;
}
public sealed partial class GameSession
{
    /// <summary>唯一生产调度队列；SkillIds仅为兼容持久字段和展示缓存，不授予学习资格。</summary>
    public string[] LearnedClassSkills(Actor hero)=>Config.ClassSkillSlots(hero.SpecId)
        .Where(id=>id!=""&&(id==Config.Talents.StartingSkills[hero.SpecId]||hero.Talents.GetValueOrDefault(id)>0)).ToArray();
    private void SyncFixedSkills(Actor hero)
    {
        if(Config.LegacySkillRuntime)return;
        hero.SkillIds=LearnedClassSkills(hero);
        // C1-A：职业特性T绑定的光环由学习状态持续维持，不占S1-S4槽、不进入自动施法队列。
        var talentAuras=TalentAuraSkills(hero);
        foreach(string aura in talentAuras)if(!hero.ActiveAuraSkills.Contains(aura))hero.ActiveAuraSkills.Add(aura);
        hero.ActiveAuraSkills.RemoveAll(id=>!hero.SkillIds.Contains(id)&&!talentAuras.Contains(id));
    }
    /// <summary>已学习的职业特性T所绑定的光环技能（如守护/进攻光环）；仅按实例自身投入判定。</summary>
    private string[] TalentAuraSkills(Actor hero)=>hero.Talents.Where(p=>p.Value>0&&Config.Talents.Nodes.TryGetValue(p.Key,out var node)&&node.ClassTrait&&node.SkillId!=""&&IsAuraSkill(node.SkillId))
        .Select(p=>Config.Talents.Nodes[p.Key].SkillId).Distinct().ToArray();
    /// <summary>区域可退投入不含S1基础一点；其他区域和实例的投入不参与。</summary>
    public int RegionInvestment(Actor hero,int region)=>hero.Talents.Where(p=>Config.TalentRegion(Config.Talents.Nodes[p.Key])==region)
        .Sum(p=>Math.Max(0,p.Value-(p.Key==Config.Talents.StartingSkills[hero.SpecId]?1:0)));
    /// <summary>尚未由玩家取回的历史点数仍占原实例预算。</summary>
    public int RetiredTalentPoints(Actor hero)=>hero.RetiredTalents.Where(p=>!p.Returned).Sum(p=>p.Rank);
    /// <summary>用户2026-10-26裁决：免费主动返还旧点数，保留原始记录；重复操作不会再次返还。</summary>
    public bool ReturnRetiredTalentPoints(string heroId,out string reason)
    {
        var hero=FindHeroInstance(heroId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");
        int points=RetiredTalentPoints(hero);
        if(points==0){reason="没有尚未返还的旧技能点";return false;}
        hero.RetiredTalents=hero.RetiredTalents.Select(p=>p with {Returned=true}).ToList();
        MarkChanged();Note($"旧技能投入返还：{hero.Name} {points}点（原始记录保留）");reason="";return true;
    }
    /// <summary>前置支持 "A@1&B@2" 表达式（跨区域终点节点）；裸ID保留按同区域上溯的旧语义，
    /// 旧主动或跨区域旧前置不成为新技能的隐藏门禁。</summary>
    private (string Token,int Need)? UnmetPrerequisite(Actor hero,TalentNode node)
    {
        if(node.Kind=="active"||node.Prerequisite=="")return null;
        if(node.Prerequisite.Contains('@')||node.Prerequisite.Contains('&'))
        {
            foreach(string raw in node.Prerequisite.Split('&',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
            {
                var parts=raw.Split('@');string pid=parts[0];
                int need=parts.Length>1&&int.TryParse(parts[1],out int parsed)?parsed:Math.Max(1,node.RequiredRank);
                if(hero.Talents.GetValueOrDefault(pid)<need)return (pid,need);
            }
            return null;
        }
        var seen=new HashSet<string>();string cursor=node.Prerequisite;
        while(cursor!=""&&seen.Add(cursor))
        {
            var parent=Config.Talents.Nodes[cursor];
            if(Config.TalentRegion(parent)==Config.TalentRegion(node))
                return hero.Talents.GetValueOrDefault(cursor)<Math.Max(1,node.RequiredRank)?(cursor,Math.Max(1,node.RequiredRank)):null;
            cursor=parent.Prerequisite;
        }
        return null;
    }
    private bool CanLearnFixedTalent(Actor hero,string id,out string reason)
    {
        var unmet=Config.Talents.Nodes.TryGetValue(id,out var node)?UnmetPrerequisite(hero,node):null;
        if(!Config.Talents.Nodes.TryGetValue(id,out node)||node.Hero!=hero.SpecId)reason="此角色没有该节点";
        else if(Config.TalentRegion(node)==0)reason="旧主动已退休，原投入可免费返还";
        else if(hero.Talents.GetValueOrDefault(id)>=node.MaxRank)reason="已达可学习最高等级";
        else if(node.ClassTrait&&!ClassTraitUnlocked(hero.SpecId))reason="需要在星图购买该职业特性资格（尚未投放资格时仍保持锁定）";
        else if(node.ExclusiveGroup!=""&&hero.Talents.Any(p=>p.Value>0&&p.Key!=id&&Config.Talents.Nodes[p.Key].ExclusiveGroup==node.ExclusiveGroup))reason="与已选分支互斥：请先免费重置该区域";
        else if(unmet is { } requirement)reason="需学习 "+Config.Talents.Nodes[requirement.Token].Name+" 至"+requirement.Need+"级";
        else if(node.Kind!="active"&&RegionInvestment(hero,Config.TalentRegion(node))<Config.Talents.Required(node))reason=$"需在当前区域投入{Config.Talents.Required(node)}点";
        else if(TalentPoints(hero)<=0)reason="可用天赋点不足";
        else {reason="";return true;}
        return false;
    }
    private bool ResetFixedRegion(Actor hero,int region,out string reason)
    {
        if(region is <1 or >5){reason="技能区域须为S1～S4或T";return false;}
        int points=RegionInvestment(hero,region);
        if(points==0){reason="该区域没有可退投入（S1基础攻击保留）";return false;}
        string starter=Config.Talents.StartingSkills[hero.SpecId];
        var removed=hero.Talents.Keys.Where(id=>Config.TalentRegion(Config.Talents.Nodes[id])==region).ToHashSet();
        foreach(string id in removed)if(id==starter)hero.Talents[id]=1;else hero.Talents.Remove(id);
        // 装备等来源加成保留为休眠贡献；TalentLevel对未学节点返回0，不因洗点销毁来源权益。
        foreach(string id in removed)hero.SkillSwapLocks.Remove(id);
        SyncFixedSkills(hero);
        // 仅撤销此实例此区域的动作/队列/Buff/召唤；其他实例与区域的CD保持原值。
        int owner=Heroes.IndexOf(hero);
        var action=skillState.Actions.FirstOrDefault(a=>a.Owner==owner);
        if(action!=null)
        {
            foreach(string id in removed)action.Cooldowns.Remove(id);
            if(removed.Contains(action.Active)){action.Active="";action.Windup=0;}
        }
        skillState.Pending.RemoveAll(p=>p.Owner==owner&&removed.Contains(p.Skill));
        skillState.Flights.RemoveAll(p=>p.Owner==owner&&removed.Contains(p.Skill));
        skillState.Buffs.RemoveAll(p=>p.Owner==owner&&(removed.Contains(p.Ability)||removed.Contains(p.AuraSkill)));
        PruneSummons();PruneAuraActivations();MarkChanged();Note($"免费重置：{hero.Name} 区域{region} {points}点");reason="";return true;
    }
    /// <summary>职业特性T绑定的光环来源：只按本实例已学T节点判定，读档迁移不得把它当作未装配技能清掉。</summary>
    private static bool IsTalentAura(Config config,Actor hero,string auraSkill)=>hero.Talents.Any(p=>p.Value>0&&config.Talents.Nodes.TryGetValue(p.Key,out var node)&&node.ClassTrait&&node.SkillId==auraSkill);
    private static void MigrateFixedSkills(Config config,SessionSnapshot state)
    {
        if(config.LegacySkillRuntime)return;
        if(state.FixedSkillsVersion is <0 or >2)throw new InvalidDataException("固定技能版本不支持");
        foreach(var hero in state.Heroes.Concat(state.ReserveHeroes))
        {
            if(hero.RetiredTalents==null||hero.LegacySkillIds==null)throw new InvalidDataException("缺少旧技能隔离字段");
            if(hero.SkillIds==null||hero.SkillIds.Any(id=>!config.Skills.Skills.ContainsKey(id)))throw new InvalidDataException("未知旧技能引用，保留原档等待人工迁移");
            if(state.FixedSkillsVersion==2)
            {
                var expected=config.ClassSkillSlots(hero.SpecId).Where(id=>id!=""&&hero.Talents.GetValueOrDefault(id)>0).ToArray();
                if(!hero.SkillIds.SequenceEqual(expected))throw new InvalidDataException("固定技能队列与个人学习状态不一致");
            }
            if(state.FixedSkillsVersion==0)
            {
                hero.LegacySkillData=System.Text.Json.JsonSerializer.Serialize(new{hero.HeroInstanceId,hero.SpecId,hero.Talents,hero.SkillIds,hero.TalentBonuses,hero.SkillSwapLocks,hero.ActiveAuraSkills});
                hero.LegacySkillIds=hero.SkillIds.ToArray();
                foreach(var pair in hero.Talents.Where(p=>config.IsRetiredTalent(p.Key)||config.Talents.Nodes.TryGetValue(p.Key,out var node)&&node.ClassTrait&&!config.Stars.Nodes.Values.Any(gate=>gate.Attribute=="class_trait_unlock"&&gate.TargetId==hero.SpecId&&state.Stars.Ranks.Any(rank=>rank.Id==gate.Id&&rank.Level>0))).ToArray())
                {
                    if(!hero.RetiredTalents.Any(p=>p.NodeId==pair.Key))hero.RetiredTalents.Add(new(pair.Key,pair.Value));
                    hero.Talents.Remove(pair.Key);
                }
                hero.ActiveAuraSkills.RemoveAll(config.IsRetiredTalent);
                foreach(string id in hero.SkillSwapLocks.Keys.Where(config.IsRetiredTalent).ToArray())hero.SkillSwapLocks.Remove(id);
                hero.TalentBonuses.RemoveAll(b=>b.Scope=="node"&&config.IsRetiredTalent(b.Target));
            }
            if(state.FixedSkillsVersion<2)
            {
                // C1-A 槽位重排迁移：旧档中不再属于S1～S4固定顺位的主动转入隔离记录并免费返还点数，
                // 已学的新槽技能与被动保持不变；旧技能现场在下方统一作废，不复活退休技能。
                var slots=config.ClassSkillSlots(hero.SpecId);
                foreach(var pair in hero.Talents.Where(p=>config.Talents.Nodes.TryGetValue(p.Key,out var node)&&node.Kind=="active"&&!node.ClassTrait&&!slots.Contains(p.Key)).ToArray())
                {
                    if(!hero.RetiredTalents.Any(p=>p.NodeId==pair.Key))hero.RetiredTalents.Add(new(pair.Key,pair.Value));
                    hero.Talents.Remove(pair.Key);
                }
                hero.ActiveAuraSkills.RemoveAll(id=>!slots.Contains(id)&&!IsTalentAura(config,hero,id));
            }
            string starter=config.Talents.StartingSkills[hero.SpecId];
            if(state.FixedSkillsVersion==0&&hero.Talents.GetValueOrDefault(starter)==0)hero.StarterPointWaived=true;
            hero.Talents[starter]=Math.Max(1,hero.Talents.GetValueOrDefault(starter));hero.StartingSkillGranted=true;
            hero.SkillIds=config.ClassSkillSlots(hero.SpecId).Where(id=>id!=""&&hero.Talents.GetValueOrDefault(id)>0).ToArray();
            hero.ActiveAuraSkills.RemoveAll(id=>!hero.SkillIds.Contains(id)&&!IsTalentAura(config,hero,id));
        }
        if(state.FixedSkillsVersion==0)
        {
            // 旧运行现场不得复活旧技能；正式档本来即采用房间重开语义。
            state.SkillState=new();state.Summons.RemoveAll(s=>config.IsRetiredTalent(s.SummonSkill));
            state.RoomEntrySummons.RemoveAll(s=>config.IsRetiredTalent(s.SummonSkill));
        }
        state.FixedSkillsVersion=2;
    }
}
