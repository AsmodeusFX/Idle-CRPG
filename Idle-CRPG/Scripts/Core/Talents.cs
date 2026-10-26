using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>学习等级与额外等级分开；加成不提供学习资格，不计入系内投入。SkillId 为空时沿用 CastTalent 旧分派，非空时引用同名 CSV 主动定义。</summary>
public record TalentNode(string Id,string Hero,int Tree,string TreeName,int Row,int Col,string Name,string Description,int MaxRank,bool BonusAllowed,string Kind,string Prerequisite,int RequiredRank,int RequiredPoints,string Effect,double Value,double PerRank,double Cooldown,float Range,float Radius,DamageType DamageType,float ProjectileSpeed,string SkillId,string ExclusiveGroup="",bool ClassTrait=false);
public record TalentBonus(string Source,string Scope,string Target,int Levels);
public sealed class TalentConfiguration
{
    public Dictionary<string,TalentNode> Nodes=new();
    public Dictionary<string,int> Rules=new();
    public Dictionary<string,string> StartingSkills=new();
    public int Rule(string id)=>Rules[id];
    public static TalentConfiguration Load(string root,Config config)
    {
        var c=new TalentConfiguration();
        foreach(var r in CsvTable.Load("system/config.csv",File.ReadAllText(Path.Combine(root,"system/config.csv")),"id","value").Rows)c.Rules.Add(r.Id,r.Integer("value"));
        foreach(string id in new[]{"talent_initial_points","talent_points_per_level","talent_row_points","active_skill_max_slots","active_skill_initial_slots","talent_reset_base_cost","talent_reset_cost_per_point","talent_leap_ticks","talent_windup_ticks","talent_recovery_ticks","world_follow_spacing_milli","offline_cap_seconds","offline_xp_per_hour","offline_kp_per_hour","offline_gold_per_hour"})if(!c.Rules.TryGetValue(id,out int v)||v<0||v>100000)throw new InvalidDataException("系统规则缺失或越界："+id);
        if(c.Rule("talent_leap_ticks")<1||c.Rule("talent_windup_ticks")<1)throw new InvalidDataException("技能前摇必须大于0");
        if(c.Rule("active_skill_max_slots") is <1 or >5||c.Rule("active_skill_initial_slots")>c.Rule("active_skill_max_slots"))throw new InvalidDataException("主动槽位须在0～5内");
        if(c.Rule("world_follow_spacing_milli") is <1 or >1000)throw new InvalidDataException("世界跟随间距无效");
        foreach(string key in new[]{"ranged_retreat_distance_milli","ranged_retreat_cooldown_milli","targeting_local_range_milli","targeting_reevaluate_milli","targeting_budget","combat_entry_delay_milli","combat_entry_alert_cap_milli","combat_entry_pass_depth_milli","combat_entry_threat_range_milli","combat_entry_melee_depth_milli","combat_entry_ranged_depth_milli","combat_entry_melee_spread_milli","combat_entry_ranged_spread_milli","dungeon_queue_spacing_milli","party_catchup_distance_milli","party_catchup_speed_pct","dungeon_door_wait_offset_milli","dungeon_door_wait_timeout_milli","summon_follow_distance_milli","summon_catchup_speed_pct","summon_blink_distance_milli","summon_blink_delay_milli","summon_entry_speed_pct"})
            if(!c.Rules.TryGetValue(key,out int value)||value<1||value>100000)throw new InvalidDataException("索敌规则缺失或越界："+key);
        // 召唤物跟随：追赶倍率不得低于100%（否则落后反而减速），闪现距离必须大于正常跟随距离，否则边界处会互斥抖动。
        if(c.Rule("summon_catchup_speed_pct")<100)throw new InvalidDataException("召唤物追赶倍率不得低于100%");
        if(c.Rule("summon_blink_distance_milli")<=c.Rule("summon_follow_distance_milli"))throw new InvalidDataException("召唤物闪现距离必须大于跟随距离");
        // 跑图追赶：触发距离必须大于队列间距，追赶倍率不得低于100%，门前等待必须有正超时。
        if(c.Rule("party_catchup_distance_milli")<=c.Rule("dungeon_queue_spacing_milli"))throw new InvalidDataException("跑图追赶距离必须大于队列间距");
        if(c.Rule("party_catchup_speed_pct")<100)throw new InvalidDataException("跑图追赶倍率不得低于100%");
        if(c.Rule("dungeon_door_wait_timeout_milli")<100)throw new InvalidDataException("门前等待超时过短");
        // 入场部署加速同样只能高于正式战斗速度，不允许配置成低于100%变相减速。
        if(c.Rule("summon_entry_speed_pct")<100)throw new InvalidDataException("召唤物入场加速倍率不得低于100%");
        if(c.Rule("combat_entry_melee_depth_milli")<=c.Rule("combat_entry_ranged_depth_milli")||c.Rule("combat_entry_melee_spread_milli")<=c.Rule("combat_entry_ranged_spread_milli"))throw new InvalidDataException("近战入场深入距离及横向间距必须大于远程/法系");
        // 入口通行：近距离威胁必须早于通行边界触发，否则通行限制会被自身的威胁测试立刻抵消；
        // 敌方兜底上限必须高于基础警觉，否则参考前方入房进度没有意义。
        if(c.Rule("combat_entry_threat_range_milli")>=c.Rule("combat_entry_pass_depth_milli"))throw new InvalidDataException("入口近距离威胁距离必须小于通行深度");
        if(c.Rule("combat_entry_alert_cap_milli")<=c.Rule("combat_entry_delay_milli"))throw new InvalidDataException("敌方警觉兜底上限必须大于基础警觉时间");
        string[] columns="id,hero,tree,tree_name,row,col,name,description,max_rank,bonus_allowed,kind,prerequisite,required_rank,required_points,effect,value,per_rank,cooldown,range,radius,damage_type,projectile_speed,skill_id,exclusive_group,class_trait".Split(',');
        foreach(var r in CsvTable.Load("skill/talent.csv",File.ReadAllText(Path.Combine(root,"skill/talent.csv")),columns).Rows)
        {
            var n=new TalentNode(r.Id,r.Required("hero"),r.Integer("tree"),r.Required("tree_name"),r.Integer("row"),r.Integer("col"),r.Required("name"),r.Required("description"),r.Integer("max_rank"),r.Integer("bonus_allowed") is 1,r.Required("kind"),r["prerequisite"],r.Integer("required_rank"),r.Integer("required_points"),r.Required("effect"),r.Number("value"),r.Number("per_rank"),r.Number("cooldown"),(float)r.Number("range"),(float)r.Number("radius"),r.Enum<DamageType>("damage_type"),(float)r.Number("projectile_speed"),r["skill_id"],r["exclusive_group"],r["class_trait"]=="1");
            if(r.Integer("bonus_allowed") is not (0 or 1)||!config.Heroes.Any(h=>h.Id==n.Hero)||n.Tree is <0 or >2||n.Col is <0 or >2||n.Row is <0 or >100||n.MaxRank is <1 or >100||n.RequiredPoints< -1||n.RequiredRank<0||n.Value<0||n.PerRank<0||n.Cooldown<0||n.Range<0||n.Radius<0||n.Kind is not ("active" or "passive")||n.Kind=="active"&&(n.Cooldown<0.05||n.Cooldown>5000||n.Range<=0)||n.Kind=="passive"&&n.Effect is not ("atk" or "armor" or "crit_rate" or "engagement_capacity")||!float.IsFinite(n.Range)||!float.IsFinite(n.Radius)||!float.IsFinite(n.ProjectileSpeed)||n.ProjectileSpeed<0||n.Effect=="shot"&&n.ProjectileSpeed<=0||n.Kind=="active"&&n.Effect is not ("bash" or "whirlwind" or "leap" or "shout" or "weaken" or "shot" or "nova" or "data"))throw new InvalidDataException("天赋节点无效："+n.Id);
            // 绑定节点只能引用同名主动定义，冷却/距离必须与 CSV 一致，避免界面与战斗读取两份不同数值。
            if(n.SkillId!=""&&(n.Kind!="active"||n.SkillId!=n.Id||!config.Skills.Skills.TryGetValue(n.SkillId,out var bound)||bound.Kind!="ActiveSkill"||bound.Cooldown!=checked((int)Math.Ceiling(n.Cooldown*20))||Math.Abs(bound.Range-n.Range)>.0001f))throw new InvalidDataException("天赋绑定技能无效："+n.Id);
            if(n.Effect=="data"&&n.SkillId=="")throw new InvalidDataException("数据驱动主动缺少skill_id："+n.Id);
            c.Nodes.Add(n.Id,n);
        }
        foreach(var group in c.Nodes.Values.GroupBy(n=>(n.Hero,n.Tree)))
            if(group.Select(n=>(n.Row,n.Col)).Distinct().Count()!=group.Count()||group.Select(n=>n.TreeName).Distinct().Count()!=1)throw new InvalidDataException("天赋位置重叠或系名不一致");
        foreach(var n in c.Nodes.Values)
        {
            if(n.Prerequisite!=""&&(!c.Nodes.TryGetValue(n.Prerequisite,out var p)||p.Hero!=n.Hero||p.Tree!=n.Tree||p.Row>=n.Row||n.RequiredRank<1||n.RequiredRank>p.MaxRank))throw new InvalidDataException("天赋前置必须为同系更早行："+n.Id);
            if(n.Prerequisite==""&&n.RequiredRank!=0)throw new InvalidDataException("无前置节点时前置等级须为0："+n.Id);
        }
        foreach(var row in CsvTable.Load("skill/hero_starting_skill.csv",File.ReadAllText(Path.Combine(root,"skill/hero_starting_skill.csv")),"id","skill_id").Rows)
        {
            if(!c.Nodes.TryGetValue(row.Required("skill_id"),out var n)||n.Hero!=row.Id||n.Kind!="active"||(n.SkillId==""&&n.Effect is not ("bash" or "leap" or "whirlwind" or "shot" or "nova"))||n.Row!=0||n.Prerequisite!=""||c.Required(n)!=0)throw new InvalidDataException("初始技能必须为本职业首排无前置主动："+row.Id);
            c.StartingSkills.Add(row.Id,n.Id);
        }
        if(!c.StartingSkills.Keys.ToHashSet().SetEquals(config.Heroes.Select(h=>h.Id)))throw new InvalidDataException("每个职业须恰有一个初始技能");
        foreach(var n in c.Nodes.Values)
        {
            // 绑定节点直接使用 skill.csv 的定义，不再生成旧 hit_player/投射物模板。
            if(n.Kind!="active"||n.SkillId!="")continue;
            if(n.Effect=="shot")
            {
                config.Skills.Effects.Add(n.Id+"_impact",new(n.Id+"_impact","Damage",n.DamageType,1,"","","",0,0,Array.Empty<string>(),0));
                config.Skills.Effects.Add(n.Id+"_projectile",new(n.Id+"_projectile","SpawnProjectile",n.DamageType,0,"","",n.Id+"_impact",n.ProjectileSpeed,0,Array.Empty<string>(),0));
            }
            config.Skills.Skills.Add(n.Id,new(n.Id,"ActiveSkill",checked((int)Math.Ceiling(n.Cooldown*20)),n.Effect=="leap"?c.Rule("talent_leap_ticks"):c.Rule("talent_windup_ticks"),c.Rule("talent_recovery_ticks"),1,n.Range,new[]{"hit_player"},n.Name));
        }
        return c;
    }
    public int Required(TalentNode n)=>n.RequiredPoints<0?n.Row*Rule("talent_row_points"):n.RequiredPoints;
}
public sealed partial class Config
{
    public TalentConfiguration Talents=null!;
}
public sealed partial class GameSession
{
    public int ActiveSkillSlots=>Config.LegacySkillRuntime?Math.Min(Config.Talents.Rule("active_skill_max_slots"),Config.Talents.Rule("active_skill_initial_slots")+(int)StarValue("skill_slots")):4;
    private double StarValue(string attr)=>Stars.Configuration.Nodes.Values.Where(n=>n.Attribute==attr).Sum(n=>n.ValuePerLevel*Stars.Level(n.Id));
    public int TalentPoints(Actor hero)=>Config.Talents.Rule("talent_initial_points")+(hero.Level-1)*Config.Talents.Rule("talent_points_per_level")-TalentInvestment(hero);
    public int TreeInvestment(Actor hero,int tree)=>hero.Talents.Where(p=>Config.Talents.Nodes[p.Key].Tree==tree).Sum(p=>p.Value);
    public int TalentLevel(Actor hero,string id)
    {
        var n=Config.Talents.Nodes[id];int learned=hero.Talents.GetValueOrDefault(id);if(learned==0||!n.BonusAllowed)return learned;
        return checked(learned+hero.TalentBonuses.Where(b=>b.Scope=="all"||b.Scope=="node"&&b.Target==id||b.Scope=="tree"&&b.Target==n.Tree.ToString()).Sum(b=>b.Levels));
    }
    public double TalentValue(Actor hero,TalentNode n)=>TalentLevel(hero,n.Id)==0?0:n.Value+n.PerRank*(TalentLevel(hero,n.Id)-1);
    /// <summary>前置仅统计手动学习点；首排默认0，后续每排默认增加5点。</summary>
    public bool CanLearnTalent(Actor hero,string id,out string reason)
    {
        if(!Config.LegacySkillRuntime)return CanLearnFixedTalent(hero,id,out reason);
        if(!Config.Talents.Nodes.TryGetValue(id,out var n)||n.Hero!=hero.SpecId)reason="此角色没有该节点";
        else if(hero.Talents.GetValueOrDefault(id)>=n.MaxRank)reason="已达可学习最高等级";
        else if(TreeInvestment(hero,n.Tree)<Config.Talents.Required(n))reason=$"需在{n.TreeName}投入{Config.Talents.Required(n)}点";
        else if(n.Prerequisite!=""&&hero.Talents.GetValueOrDefault(n.Prerequisite)<n.RequiredRank)reason=$"需学习{Config.Talents.Nodes[n.Prerequisite].Name} {n.RequiredRank}级";
        // 同小树互斥分支：已选另一分支时拒绝，需先免费局部改选。
        else if(n.ExclusiveGroup!=""&&hero.Talents.Any(p=>p.Value>0&&p.Key!=id&&Config.Talents.Nodes[p.Key].ExclusiveGroup==n.ExclusiveGroup))reason="与已选分支互斥：请先在英雄页免费改选";
        else if(n.ClassTrait&&!ClassTraitUnlocked(hero.SpecId))reason="该职业特性需在本轮星图解锁后才能学习";
        else if(TalentPoints(hero)<=0)reason="可用天赋点不足";
        else{reason="";return true;}return false;
    }
    /// <summary>
    /// 职业特性学习资格：本轮星图按职业购买解锁节点后共享给该职业全部上阵实例。
    /// 当前内容尚未投放该星图节点时不做额外门槛，避免出现"有节点但永远不可学"的死内容。
    /// </summary>
    public bool ClassTraitUnlocked(string classId)
    {
        var gates=Config.Stars?.Nodes.Values.Where(n=>n.Attribute=="class_trait_unlock").ToArray()??Array.Empty<StarNode>();
        if(gates.Length==0)return Config.LegacySkillRuntime;
        // 约定：职业特性解锁节点 id 为 s_trait_<classId>，购买后本轮该职业全部实例共享资格。
        return gates.Any(node=>node.TargetId==classId&&Stars.Level(node.Id)>0);
    }
    /// <summary>
    /// 免费局部洗点：只重置指定技能区域（tree）的投入，点数按该区域已投总量返还；
    /// 不改变等级、不删除其他区域投入、不返还星图金币；操作原子，失败不留半条状态。
    /// </summary>
    public bool ResetTalentRegion(string heroId,int tree,out string reason)
    {
        var h=FindHeroInstance(heroId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");
        if(!Config.LegacySkillRuntime)return ResetFixedRegion(h,tree,out reason);
        if(tree<0||!Config.Talents.Nodes.Values.Any(n=>n.Hero==h.SpecId&&n.Tree==tree)){reason="没有该技能区域";return false;}
        var inRegion=h.Talents.Where(p=>Config.Talents.Nodes[p.Key].Tree==tree).Select(p=>p.Key).ToArray();
        if(inRegion.Length==0){reason="该区域没有已投入点数";return false;}
        int refunded=inRegion.Sum(id=>h.Talents[id]);
        foreach(string id in inRegion)h.Talents.Remove(id);
        // 只清理该区域技能引用：装配槽、光环维持、来源加成与换入等待各自保持一致。
        h.SkillIds=h.SkillIds.Where(id=>!inRegion.Contains(id)).ToArray();
        h.ActiveAuraSkills.RemoveAll(inRegion.Contains);
        h.TalentBonuses.RemoveAll(b=>b.Scope=="node"&&inRegion.Contains(b.Target));
        foreach(string id in inRegion)h.SkillSwapLocks.Remove(id);
        if(Floors.Count>0){var action=skillState.Actions.FirstOrDefault(a=>a.Owner==Heroes.IndexOf(h));if(action!=null&&!h.SkillIds.Contains(action.Active)){action.Active="";action.Windup=0;}}
        PruneSummons();PruneAuraActivations();ResetSkillRoom();MarkChanged();
        Note($"局部洗点：{h.Name} 区域{tree} 返还{refunded}点");
        reason="";return true;
    }
    public bool LearnTalent(string heroId,string id,out string reason)
    {
        var h=FindHeroInstance(heroId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");if(!CanLearnTalent(h,id,out reason))return false;
        bool first=!h.Talents.ContainsKey(id);h.Talents[id]=h.Talents.GetValueOrDefault(id)+1;
        if(!Config.LegacySkillRuntime){SyncFixedSkills(h);MarkChanged();Note("学习天赋："+Config.Talents.Nodes[id].Name);return true;}
        // 仅首次学会主动技能时填空位；升级、被动及满槽均不改装配，战斗冷却共用装配命令。
        if(first&&Config.Talents.Nodes[id].Kind=="active"&&Heroes.Contains(h)&&h.SkillIds.Length<ActiveSkillSlots&&!h.SkillIds.Contains(id))
            SetTalentLoadout(heroId,h.SkillIds.Append(id),out _);
        Note("学习天赋："+Config.Talents.Nodes[id].Name);return true;
    }
    public long TalentResetCost(Actor h)=>Config.LegacySkillRuntime?(long)Math.Ceiling((Config.Talents.Rule("talent_reset_base_cost")+(long)TalentInvestment(h)*Config.Talents.Rule("talent_reset_cost_per_point"))*(1-Math.Clamp(StarValue("talent_reset_discount"),0,1))):0;
    /// <summary>洗点需星图解锁，城镇无传送时扣费并返还全部投入，不修改其他英雄。</summary>
    public bool ResetTalents(string heroId,out string reason)
    {
        if(!Config.LegacySkillRuntime){reason="请在英雄页免费重置当前技能区域；S1基础攻击不会移除";return false;}
        var h=FindHeroInstance(heroId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");
        if(StarValue("talent_reset")<1)reason="请先在星图解锁洗点";
        else if(World?.Stage!=CampaignStage.Village||World.PortalPhase!=PortalPhase.None)reason="请在城镇且传送结束后洗点";
        else if(TalentInvestment(h)==0)reason="没有需要重置的天赋";
        else if(Gold<TalentResetCost(h))reason="洗点金币不足";
        else {Gold-=TalentResetCost(h);h.Talents.Clear();h.SkillIds=h.SkillIds.Where(id=>!Config.Talents.Nodes.ContainsKey(id)).ToArray();h.SkillSwapLocks.Clear();PruneSummons();PruneAuraActivations();Note("天赋已重置："+h.Name);reason="";return true;}return false;
    }
    /// <summary>供装备/强化来源替换贡献；同一来源幂等更新，卸下传空列表。</summary>
    public void SetTalentBonuses(string heroId,string source,IEnumerable<TalentBonus> bonuses)
    {
        var h=FindHeroInstance(heroId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");var next=bonuses.ToArray();
        if(string.IsNullOrWhiteSpace(source)||next.Any(b=>b.Source!=source||b.Levels is <0 or >1000||b.Scope is not ("node" or "tree" or "all")||b.Scope=="node"&&(!Config.Talents.Nodes.TryGetValue(b.Target,out var n)||n.Hero!=h.SpecId)||b.Scope=="tree"&&b.Target is not ("0" or "1" or "2")))throw new ArgumentException("技能加成来源或目标无效");
        h.TalentBonuses.RemoveAll(b=>b.Source==source);h.TalentBonuses.AddRange(next);MarkChanged();
    }
    /// <summary>玩家装配只接受已学习主动技能，按槽位顺序自动施放；不重置全房间技能现场。</summary>
    public bool SetTalentLoadout(string heroId,IEnumerable<string> ids,out string reason)
    {
        if(!Config.LegacySkillRuntime){reason="固定职业技能学习后自动施放，不支持装配或卸下";return false;}
        var h=FindHeroInstance(heroId)??throw new ArgumentException("英雄实例ID不存在或职业不唯一");var next=ids.ToArray();
        if(next.Length>ActiveSkillSlots||next.Distinct().Count()!=next.Length||next.Any(id=>!Config.Talents.Nodes.TryGetValue(id,out var n)||n.Kind!="active"||n.Hero!=h.SpecId||h.Talents.GetValueOrDefault(id)==0)){reason="技能未学习、重复或超过已解锁槽位";return false;}
        bool fighting=(World==null||World.Stage==CampaignStage.Dungeon)&&Floors.Count>0&&!Floor.Rooms[CurrentRoom].Clear;
        foreach(string id in next.Except(h.SkillIds))if(fighting)h.SkillSwapLocks[id]=Math.Max(h.SkillSwapLocks.GetValueOrDefault(id),Config.Skills.Skills[id].Cooldown/20d);
        if(Floors.Count>0){var a=skillState.Actions.FirstOrDefault(a=>a.Owner==Heroes.IndexOf(h));if(a!=null&&!next.Contains(a.Active)){a.Active="";a.Windup=0;}}
        h.SkillIds=next;PruneSummons();PruneAuraActivations();MarkChanged();reason="";return true;
    }
    public double SkillCooldown(Actor h,string id)=>Math.Max(h.SkillSwapLocks.GetValueOrDefault(id),skillState.Actions.FirstOrDefault(a=>a.Owner==CombatId(h))?.Cooldowns.GetValueOrDefault(id)??0);
    private void AdvanceSwapLocks(float dt)
    {foreach(var h in Heroes.Concat(reserveHeroes))foreach(var id in h.SkillSwapLocks.Keys.ToArray())h.SkillSwapLocks[id]=Math.Max(0,h.SkillSwapLocks[id]-dt);}
    private double PassiveTalentValue(Actor h,string attr)
    {
        double total=0;foreach(var p in h.Talents){var n=Config.Talents.Nodes[p.Key];if(n.Kind=="passive"&&n.Effect==attr)total+=TalentValue(h,n);}return total;
    }
    /// <summary>展示和战斗共用被动属性贡献，不写回基础属性，避免升级/读档重复叠加。</summary>
    public double HeroTalentAttribute(Actor h,string attr,double basis)=>basis+PassiveTalentValue(h,attr);
    public bool CanResetTalents=>Config.LegacySkillRuntime&&StarValue("talent_reset")>=1;
    /// <summary>跳斩前摇期间连续接近目标，渲染抛物线高度不改变逻辑坐标。</summary>
    public float TalentLeapHeight(Actor h)
    {
        var a=skillState.Actions.FirstOrDefault(a=>a.Owner==Heroes.IndexOf(h));
        return a!=null&&Config.Talents.Nodes.TryGetValue(a.Active,out var n)&&n.Effect=="leap"?(float)Math.Sin(Math.Clamp(a.Windup/Config.Talents.Rule("talent_leap_ticks")*20,0,1)*Math.PI)*28:0;
    }
    private void ValidateTalentCapacity(StarProgression next)
    {
        int cap=TalentSlotCapacity(Config,next);
        if(Heroes.Concat(reserveHeroes).Any(h=>h.SkillIds.Count(id=>Config.Talents.Nodes.ContainsKey(id))>cap))throw new InvalidOperationException("星图变更不能锁住已装配的主动技能槽");
    }
    private static int TalentSlotCapacity(Config c,StarProgression stars)=>c.LegacySkillRuntime?Math.Min(c.Talents.Rule("active_skill_max_slots"),c.Talents.Rule("active_skill_initial_slots")+(int)stars.Configuration.Nodes.Values.Where(n=>n.Attribute=="skill_slots").Sum(n=>n.ValuePerLevel*stars.Level(n.Id))):4;
    private void CastTalent(Actor owner,Actor target,TalentNode n,long originCast)
    {
        double value=TalentValue(owner,n);
        if(n.Effect=="shot"){SpawnGeneric(new(){Owner=CombatId(owner),Target=CombatId(target),Skill=n.Id,Coefficient=value,OriginCast=originCast},SkillConfig.Effects[n.Id+"_projectile"]);return;}
        if(n.Effect is "shout" or "weaken")
        {
            string buff=n.Effect=="shout"?"talent_battle_shout":"talent_weaken";
            foreach(var victim in n.Effect=="shout"?Heroes.Where(h=>h.Alive&&Vector2.Distance(h.Position,owner.Position)<=n.Radius):Enemies(CombatId(owner)).Where(h=>Vector2.Distance(h.Position,owner.Position)<=n.Radius))
            {ApplyBuff(buff,CombatId(owner),CombatId(victim),n.Id,value,null);var b=skillState.Buffs.LastOrDefault(b=>b.Id==buff&&b.Target==CombatId(victim));if(b!=null)b.Value=value;}
            return;
        }
        var victims=n.Effect=="bash"?new[]{target}:Enemies(CombatId(owner)).Where(e=>Vector2.Distance(e.Position,n.Effect is "leap" or "nova"?target.Position:owner.Position)<=n.Radius).ToArray();
        foreach(var victim in victims)SettleDamage(owner,victim,n.Id,value,n.DamageType,n.Effect=="nova",originCast:originCast);
    }
    /// <summary>恢复时拒绝非法投入、外职业节点、越界加成和未学习装配；旧档空集合自然补齐可用点数。</summary>
    private static void ValidateTalents(Config c,Actor h)
    {
        if(!c.LegacySkillRuntime&&c.Heroes.Any(spec=>spec.Id==h.SpecId))
        {
            if(h.RetiredTalents==null||h.LegacySkillIds==null||h.RetiredTalents.Select(p=>p.NodeId).Distinct().Count()!=h.RetiredTalents.Count||h.RetiredTalents.Any(p=>!c.Talents.Nodes.TryGetValue(p.NodeId,out var node)||(!c.IsRetiredTalent(p.NodeId)&&!node.ClassTrait)||node.Hero!=h.SpecId||p.Rank<1||p.Rank>node.MaxRank))throw new InvalidDataException("旧技能投入隔离记录无效");
            if(h.Talents.Any(p=>!c.Talents.Nodes.TryGetValue(p.Key,out var node)||node.Hero!=h.SpecId||c.TalentRegion(node)==0||p.Value<1||p.Value>node.MaxRank))throw new InvalidDataException("固定技能投入无效");
            int investment=h.Talents.Values.Sum()-(h.StarterPointWaived?1:0)+h.RetiredTalents.Where(p=>!p.Returned).Sum(p=>p.Rank);
            if(investment>c.Talents.Rule("talent_initial_points")+(h.Level-1)*c.Talents.Rule("talent_points_per_level"))throw new InvalidDataException("固定技能点超出预算");
            if(h.Talents.Keys.Select(id=>c.Talents.Nodes[id]).Where(n=>n.ExclusiveGroup!="").GroupBy(n=>n.ExclusiveGroup).Any(group=>group.Count()>1))throw new InvalidDataException("固定技能互斥冲突");
            if(h.SkillSwapLocks.Any(p=>!c.Skills.Skills.ContainsKey(p.Key)||!double.IsFinite(p.Value)||p.Value<0)||h.TalentBonuses.Any(b=>b==null||string.IsNullOrWhiteSpace(b.Source)||b.Levels is <0 or >1000||b.Scope is not ("all" or "tree" or "node")||b.Scope=="node"&&(!c.Talents.Nodes.TryGetValue(b.Target,out var node)||node.Hero!=h.SpecId)||b.Scope=="tree"&&b.Target is not ("0" or "1" or "2")))throw new InvalidDataException("固定技能加成/冷却字段无效");
            return;
        }
        if(h.Talents==null||h.TalentBonuses==null||h.SkillSwapLocks==null||h.Talents.Any(p=>!c.Talents.Nodes.TryGetValue(p.Key,out var n)||n.Hero!=h.SpecId||p.Value<1||p.Value>n.MaxRank)||h.Talents.Values.Sum()>c.Talents.Rule("talent_initial_points")+(h.Level-1)*c.Talents.Rule("talent_points_per_level"))throw new InvalidDataException("天赋投入存档无效");
        var pending=h.Talents.Keys.ToHashSet();var accepted=new HashSet<string>();var invested=new int[3];
        while(pending.Count>0)
        {
            string? id=pending.FirstOrDefault(id=>{var n=c.Talents.Nodes[id];return invested[n.Tree]>=c.Talents.Required(n)&&(n.Prerequisite==""||accepted.Contains(n.Prerequisite)&&h.Talents[n.Prerequisite]>=n.RequiredRank);});
            if(id==null)throw new InvalidDataException("天赋前置投入不合法");var n=c.Talents.Nodes[id];invested[n.Tree]+=h.Talents[id];accepted.Add(id);pending.Remove(id);
        }
        if(h.SkillIds.Any(id=>c.Talents.Nodes.TryGetValue(id,out var n)&&(n.Hero!=h.SpecId||h.Talents.GetValueOrDefault(id)==0))||h.SkillSwapLocks.Any(p=>!c.Skills.Skills.ContainsKey(p.Key)||!double.IsFinite(p.Value)||p.Value<0)||h.TalentBonuses.Any(b=>b==null||string.IsNullOrWhiteSpace(b.Source)||b.Levels is <0 or >1000||b.Scope is not ("all" or "tree" or "node")||b.Scope=="node"&&(!c.Talents.Nodes.TryGetValue(b.Target,out var n)||n.Hero!=h.SpecId)||b.Scope=="tree"&&b.Target is not ("0" or "1" or "2")))throw new InvalidDataException("天赋装配或额外等级无效");
    }
}
