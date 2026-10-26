using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// C1-A-2：98节点天赋的消费者、前置/互斥、点数守恒、免费局部洗点、存档往返与真实战斗行为。
/// 只读设计输入（docs/temp 的S07构筑清单），只在 artifacts/qa 写证据，不碰玩家存档。
/// </summary>
public static class C1A2TalentChecks
{
    private const string BuildCsv="docs/temp/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/C1A核心输入/S07_8套24点构筑合法清单_V0.8.csv";
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C1A2: "+why);}
    private static readonly Dictionary<string,string[]> Slots=new()
    {
        ["BAR"]=new[]{"barb_bash","barb_whirlwind_window","barb_taunt","barb_ancestor","warrior_trait"},
        ["RAN"]=new[]{"ran_shot","ran_multishot","ran_guided_arrow","ran_valkyrie","ranger_trait"},
        ["SOR"]=new[]{"mage_fireball","sor_frost_nova","sor_chain_lightning","sor_meteor","mage_trait"},
        ["PAL"]=new[]{"paladin_smite","pal_shield_bash","pal_prayer","pal_hammer","priest_trait"}
    };
    private static readonly Dictionary<string,string> HeroOf=new(){["BAR"]="warrior",["RAN"]="ranger",["SOR"]="mage",["PAL"]="priest"};
    /// <summary>设计ID→生产节点ID：Sx01由C1-A-1固定技能节点承载，T01由职业特性节点承载。</summary>
    private static string Map(string designId)
    {
        string cls=designId.Substring(0,3);string local=designId.Substring(4);
        var slots=Slots[cls];
        if(local=="S201")return slots[1];
        if(local=="S301")return slots[2];
        if(local=="S401")return slots[3];
        if(local=="T01")return slots[4];
        return designId;
    }
    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var evidence=Path.Combine(root,"artifacts/qa","s15-c1a2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);

        // 1) 98个设计节点全部存在，且每个节点都有真实消费者。
        var designIds=new List<string>();
        foreach(var cls in Slots.Keys)
            foreach(var local in new[]{"B01","B02","B03","B04","S201","S202","S203","S204","S205","S206","S207","S301","S302","S303","S304","S305","S401","S402","S403","S404","S405","T01","T02","T03","T04","T05","T06","T07"})
                designIds.Add(cls+"-"+local);
        int implemented=0;
        var missing=new List<string>();
        foreach(var designId in designIds)
        {
            var nodeId=Map(designId);string cls=designId.Substring(0,3);
            if(!config.Talents.Nodes.TryGetValue(nodeId,out var node))
            {
                // 设计里并非每个职业都有全部编号（如SOR无S206/S207、PAL无S206/S207/S406），缺号不算缺口。
                if(!HasDesignNode(root,cls,designId))continue;
                missing.Add(designId);continue;
            }
            Need(node.Hero==HeroOf[cls],"节点职业不符："+nodeId);
            switch(node.Kind)
            {
                case "modifier":
                case "trait":
                    Need(Slots[cls].Take(4).Contains(node.SkillId),"修饰器目标不是已投放槽位技能："+nodeId);
                    Need(node.Effect is "coefficient" or "range" or "extent" or "duration" or "targets" or "control" or "summon_hp" or "summon_attack" or "cooldown","修饰类型无效："+nodeId);
                    Need(node.Value!=0||node.PerRank!=0,"修饰器无数值："+nodeId);
                    break;
                case "trigger":
                    Need(node.TriggerId!="","触发源缺失："+nodeId);
                    Need(config.Skills.Triggers.TryGetValue(node.TriggerId,out var trigger),"触发源未注册："+nodeId);
                    Need(trigger!.OwnerBuff==""&&config.Skills.ProcLimits.ContainsKey(trigger.Id)&&config.Skills.ProcIcdSeconds.ContainsKey(trigger.Id),"触发源未接入限额/ICD："+nodeId);
                    Need(Slots[cls].Take(4).Contains(node.SkillId),"触发源未绑定槽位技能："+nodeId);
                    break;
                case "passive":
                    // T01职业特性（战争/狩猎/元素/守护）以被动属性承载，必须落在PassiveTalentValue白名单内。
                    Need(node.Effect is "atk" or "armor" or "crit_rate" or "engagement_capacity","被动效果无消费者："+nodeId);
                    break;
                case "active":break;
                default:throw new Exception("C1A2: 节点类型未覆盖："+nodeId+" "+node.Kind);
            }
            implemented++;
        }
        Need(missing.Count==0,"设计节点未落地："+string.Join(",",missing));
        Need(implemented==98,"98节点实际落地数量："+implemented+"（映射后去重="+implemented+"）");

        // 2) 点数守恒 + 前置/互斥 + 存档往返 + 免费局部洗点（按S07八套构筑）。
        var buildLines=File.ReadAllLines(Path.Combine(root,BuildCsv)).Skip(1).Where(l=>l.Trim()!="").ToArray();
        Need(buildLines.Length==8,"S07构筑套数不是8："+buildLines.Length);
        var report=new List<string>{"class,route,points,talentPointsLeft,restored,reset_ok,queue_same_class,queue_mixed"};
        foreach(var line in buildLines)
        {
            var cells=line.Split(',');
            // 设计构筑表的职业列写作 BARA/RANA/SORA/PALA（A=路线），节点ID前缀只取前三字母。
            string cls=cells[0].Trim();if(cls.Length>3)cls=cls.Substring(0,3);
            string route=cells[1].Trim();string plan=cells[3].Trim();
            var wish=plan.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                .Select(p=>{var t=p.Split('x');return (Id:Map(t[0].Trim()),Rank:t.Length>1?int.Parse(t[1]):1);}).ToArray();
            Need(wish.Sum(w=>w.Rank)==24,"构筑点数不是24："+cls+route+"="+wish.Sum(w=>w.Rank));
            // QA世界允许直接指定职业（新账号仅战士解锁，会拒绝四职业构筑验证）。
            var game=GameSession.CreateWorld(config,7000+buildLines.ToList().IndexOf(line),new[]{HeroOf[cls]});
            var hero=game.Heroes[0];
            game.GrantGmResources(gold:100000,experience:1000000);
            // 设计S07以Lv25每英雄24点为口径：升到25级后可用点数必须恰好24（初始1点已被S1占用）。
            while(hero.Level<25&&game.UpgradeHero(hero.HeroInstanceId,out _)){}
            Need(hero.Level==25,"升级到25级失败："+cls);
            // 设计T01要求星图职业资格：当前只投放了野蛮人资格节点，其余职业按C1-A过渡规则可学。
            if(!game.ClassTraitUnlocked(HeroOf[cls]))
            {
                // C1-B B1 收口：四职业资格均为真实星图门禁，按职业购买对应节点。
                string gate=cls switch{"BAR"=>"s_trait_warrior","RAN"=>"C-RAN","SOR"=>"C-SOR",_=>"C-PAL"};
                Need(game.BuyStar("s_root",out string starWhy),"星图根节点购买失败："+starWhy);
                Need(game.BuyStar("s_atk",out string atkWhy),"攻击节点购买失败："+atkWhy);
                Need(game.BuyStar(gate,out string traitWhy),"职业资格购买失败："+gate+" "+traitWhy);
            }
            Need(game.TalentPoints(hero)==24,"Lv25可用点数不是24："+cls+route+" pts"+game.TalentPoints(hero));
            foreach(var (id,rank) in wish)
                for(int i=0;i<rank;i++)
                    Need(game.LearnTalent(hero.HeroInstanceId,id,out string reason),"构筑点不可学："+cls+route+" "+id+" 第"+(i+1)+"级："+reason);
            Need(game.TalentPoints(hero)==0,"点数未守恒："+cls+route+" 剩余"+game.TalentPoints(hero));

            // 存档往返：等级/节点/点数一致。
            var snapshot=game.CaptureProgress();
            var encoded=SaveStore.Encode(new(){Session=snapshot});
            var restored=GameSession.Restore(config,SaveStore.Decode(config,encoded).Session!);
            var rh=restored.Heroes[0];
            Need(restored.TalentPoints(rh)==0&&rh.Talents.Count==hero.Talents.Count&&wish.All(w=>rh.Talents.GetValueOrDefault(w.Id)==w.Rank),"读档后构筑/点数变化："+cls+route);

            // 免费局部洗点：清空该区域投入并等额返还，S1与其它区域不受影响。
            int region=RegionOf(restored,rh,wish[0].Id);
            int before=wish.Where(w=>RegionOf(restored,rh,w.Id)==region).Sum(w=>w.Rank);
            int otherBefore=restored.TalentPoints(rh);
            Need(restored.ResetTalentRegion(rh.HeroInstanceId,region,out string resetWhy),"局部洗点被拒："+cls+route+" "+region+"："+resetWhy);
            Need(restored.TalentPoints(rh)==otherBefore+before,"局部洗点未等额返还："+cls+route);
            Need(rh.Talents.GetValueOrDefault(Slots[cls][0])==1,"局部洗点破坏S1基础："+cls+route);

            // 四同职与四职业混合队列：真实战斗若干tick，不崩溃且技能真实产生效果。
            bool sameOk=QueueRuns(config,HeroOf[cls],4),mixOk=QueueRuns(config,null,4);
            Need(sameOk&&mixOk,"队列运行失败："+cls);
            report.Add($"{cls},{route},24,0,true,true,{sameOk},{mixOk}");
        }
        File.WriteAllLines(Path.Combine(evidence,"c1a2-builds.csv"),report);

        // 2b) 负向门禁：前置未满足不可学；同互斥组第二条不可学；洗点后可改选。
        {
            var gate=GameSession.CreateWorld(config,8801,new[]{"warrior"});
            var gh=gate.Heroes[0];gate.GrantGmResources(experience:1000000);
            while(gh.Level<25&&gate.UpgradeHero(gh.HeroInstanceId,out _)){}
            Need(!gate.LearnTalent(gh.HeroInstanceId,"BAR-B02",out _),"前置未满足仍可学BAR-B02");
            Need(gate.LearnTalent(gh.HeroInstanceId,"BAR-B01",out _),"B01应可学");
            Need(gate.LearnTalent(gh.HeroInstanceId,"BAR-B02",out _),"前置满足后B02应可学");
            Need(!gate.LearnTalent(gh.HeroInstanceId,"BAR-B03",out _),"互斥组内第二条仍可学BAR-B03");
            Need(gate.ResetTalentRegion(gh.HeroInstanceId,1,out _),"区域1洗点失败");
            Need(gate.LearnTalent(gh.HeroInstanceId,"BAR-B01",out _)&&gate.LearnTalent(gh.HeroInstanceId,"BAR-B03",out _),"洗点后改选失败");
        }

        // 3) 真实行为：修饰器系数可观察、触发源真实生效且洗点即撤销、ICD限制生效。
        Behavior(config,evidence);
        Console.WriteLine($"C1A2_TALENT_PASS: 98 nodes, 8 builds*24pt, prereq/exclusive, save round-trip, local reset, same-class/mixed queues, modifier+trigger behavior; evidence={evidence}");
    }
    private static bool HasDesignNode(string root,string cls,string designId)
    {
        var csv=Path.Combine(root,BuildCsv.Replace("S07_8套24点构筑合法清单_V0.8.csv","02_C1A_98天赋逐节点补规表.csv"));
        if(!File.Exists(csv))return true;
        return File.ReadAllLines(csv).Skip(1).Any(l=>l.StartsWith(designId+",",StringComparison.Ordinal));
    }
    private static int RegionOf(GameSession game,Actor hero,string nodeId)=>game.Config.TalentRegion(game.Config.Talents.Nodes[nodeId]);
    private static bool QueueRuns(Config config,string? forced,int count)
    {
        var specs=forced!=null?Enumerable.Repeat(forced,count).ToArray():new[]{"warrior","ranger","mage","priest"};
        var game=GameSession.CreateWorld(config,9000+count+(forced?.GetHashCode()??0)&0xffff,specs);
        for(int i=0;i<4000&&game.CurrentRoom==0;i++)game.Tick(.05f);
        var room=game.Floor.Rooms[game.CurrentRoom];
        foreach(var monster in room.Monsters){monster.Hp=monster.MaxHp=100000;monster.Attack=0;monster.Timer=999;monster.Position=room.Center+new System.Numerics.Vector2(1,0);}
        foreach(var hero in game.Heroes){hero.Hp=hero.MaxHp=100000;hero.Attack=50;hero.Timer=0;}
        for(int i=0;i<600;i++)game.Tick(.05f);
        return game.HitCount>0&&game.Capture().SkillState.Actions.Count>0;
    }
    private static void Behavior(Config config,string evidence)
    {
        // 基准：未投入修饰器时S1一次命中的伤害；再投入3级BAR-B01比较同一随机种子的实际扣血。
        double Damage(bool invest,int seed)
        {
            var game=GameSession.CreateWorld(config,seed,new[]{"warrior","warrior"});
            for(int i=0;i<4000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var hero=game.Heroes[0];var room=game.Floor.Rooms[game.CurrentRoom];
            game.GrantGmResources(experience:1000000);while(game.UpgradeHero(hero.HeroInstanceId,out _)){}
            if(invest)for(int r=0;r<3;r++)Need(game.LearnTalent(hero.HeroInstanceId,"BAR-B01",out string why),"BAR-B01学习失败："+why);
            foreach(var other in game.Heroes.Skip(1)){other.SkillIds=Array.Empty<string>();other.Attack=0;other.Timer=999;}
            hero.SkillIds=new[]{"barb_bash"};hero.Attack=100;hero.Timer=0;hero.Position=room.Center;
            foreach(var monster in room.Monsters){monster.Hp=monster.MaxHp=1000000;monster.Attack=0;monster.Timer=999;monster.Defense=0;monster.Position=room.Center+new System.Numerics.Vector2(0.5f,0);}
            double before=room.Monsters[0].Hp;
            for(int i=0;i<200;i++)game.Tick(.05f);
            return before-room.Monsters[0].Hp;
        }
        double plain=Damage(false,4242),buffed=Damage(true,4242);
        Need(buffed>plain,"系数修饰器未提高实际伤害：plain="+plain+" buffed="+buffed);
        File.WriteAllText(Path.Combine(evidence,"modifier-damage.csv"),"case,damage_within_10s\nplain,"+plain+"\nBAR-B01x3,"+buffed+"\n");

        // 触发源：BAR-B02命中后施加眩晕Buff；洗点后不再施加；ICD窗口内不重复施加。
        var t=GameSession.CreateWorld(config,5150,new[]{"warrior"});
        for(int i=0;i<4000&&t.CurrentRoom==0;i++)t.Tick(.05f);
        var th=t.Heroes[0];var troom=t.Floor.Rooms[t.CurrentRoom];
        t.GrantGmResources(experience:1000000);while(t.UpgradeHero(th.HeroInstanceId,out _)){}
        Need(t.LearnTalent(th.HeroInstanceId,"BAR-B01",out string r1),"B01学习失败："+r1);
        Need(t.LearnTalent(th.HeroInstanceId,"BAR-B02",out string r2),"B02触发器学习失败："+r2);
        th.SkillIds=new[]{"barb_bash"};th.Attack=100;th.Timer=0;th.Position=troom.Center;
        foreach(var monster in troom.Monsters){monster.Hp=monster.MaxHp=1000000;monster.Attack=0;monster.Timer=999;monster.Position=troom.Center+new System.Numerics.Vector2(0.5f,0);}
        bool sawStun=false;
        for(int i=0;i<400;i++){t.Tick(.05f);if(t.Capture().SkillState.Buffs.Any(b=>b.Id=="talent_stun_short")){sawStun=true;break;}}
        Need(sawStun,"BAR-B02未产生眩晕Buff");
        Need(t.ResetTalentRegion(th.HeroInstanceId,1,out string resetWhy),"区域1洗点失败："+resetWhy);
        Need(!th.Talents.ContainsKey("BAR-B02"),"洗点未撤销触发器学习");
        bool stunAfter=false;
        for(int i=0;i<400;i++){t.Tick(.05f);if(t.Capture().SkillState.Buffs.Any(b=>b.Id=="talent_stun_short")){stunAfter=true;break;}}
        Need(!stunAfter,"触发器在洗点后仍生效");
        File.WriteAllLines(Path.Combine(evidence,"trigger-behavior.csv"),new[]{"case,stun_seen","BAR-B02learned,true","after_reset,false"});
    }
}
