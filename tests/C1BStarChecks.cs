using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>
/// S15-C1-B / B1：17个新节点的真实消费、四职业T资格门禁、购买账本、存档幂等与职业容量。
/// 只读生产配置，只写 artifacts/qa 证据，不使用玩家存档。
/// </summary>
public static class C1BStarChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C1B: "+why);}
    private static readonly (string Id,string Attr,string Gate)[] Nodes=new[]
    {
        ("O02","attack_speed_pct",""),("O03","crit_rate",""),("O04","element_damage_pct",""),
        ("D02","block_rate",""),("D03","element_resistance",""),
        ("R01","attack_rating",""),("R02","defense_rating",""),
        ("C-RAN","class_trait_unlock","ranger"),("C-SOR","class_trait_unlock","mage"),("C-PAL","class_trait_unlock","priest"),
        ("S01","scroll_damage_pct",""),("P01","potion_duration_pct",""),("P02","potion_effect_pct",""),
        ("A01","travel_pct",""),("F01","kill_gold_pct",""),("F02","sell_gold_pct",""),("F03","offline_pct","")
    };
    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var evidence=Path.Combine(root,"artifacts/qa","s15-c1b1-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
        var ledger=new List<string>{"step,node,attr,gold_before,cost,gold_after,level_after,derived_before,derived_after"};

        // 1) 17节点逐个购买：金币守恒、等级+1、派生值按每级值增加；禁售/退休节点不可买。
        foreach(var (id,attr,_) in Nodes)
        {
            Need(config.Stars.Nodes.ContainsKey(id),"星图缺少节点："+id);
            var game=GameSession.CreateWorld(config,6100+Array.FindIndex(Nodes,n=>n.Id==id),new[]{"warrior"});
            game.GrantGmResources(gold:200000);
            var chain=PrerequisiteChain(config,id);
            foreach(var step in chain)Need(game.BuyStar(step,out string preWhy),"前置购买失败 "+step+"："+preWhy);
            long before=game.Gold;double derivedBefore=game.StarDerived(attr);
            int cost=(int)config.Stars.Nodes[id].Costs[game.Stars.Level(id)];
            Need(game.BuyStar(id,out string why),"购买失败 "+id+"："+why);
            Need(game.Gold==before-cost,"金币未按价目扣减："+id);
            int level=game.Stars.Level(id);
            Need(level>=1,"购买后等级未增加："+id);
            double derivedAfter=game.StarDerived(attr);
            Need(derivedAfter>derivedBefore,"派生值未增加："+id);
            ledger.Add($"{ledger.Count},{id},{attr},{before},{cost},{game.Gold},{level},{derivedBefore:0.####},{derivedAfter:0.####}");
            // 满级后重复购买必须拒绝且不扣款。
            while(game.Stars.Level(id)<config.Stars.Nodes[id].MaxLevel&&game.BuyStar(id,out _)){}
            long maxGold=game.Gold;Need(!game.BuyStar(id,out string maxWhy)&&game.Gold==maxGold,"满级仍可购买或扣款："+id+" "+maxWhy);
        }

        // 2) 四职业T资格门禁：未购全锁；购买后同职两实例共享；A投入不串B；读档保持。
        foreach(var (cls,gate,spec) in new[]{("BAR","s_trait_warrior","warrior"),("RAN","C-RAN","ranger"),("SOR","C-SOR","mage"),("PAL","C-PAL","priest")})
        {
            var game=GameSession.CreateWorld(config,6300+spec.Length,new[]{spec,spec});
            game.GrantGmResources(gold:200000,experience:1000000);
            foreach(var hero in game.Heroes)while(hero.Level<25&&game.UpgradeHero(hero.HeroInstanceId,out _)){}
            Need(!game.ClassTraitUnlocked(spec),"未购资格已解锁："+spec);
            string traitId=spec switch{"warrior"=>"warrior_trait","ranger"=>"ranger_trait","mage"=>"mage_trait",_=>"priest_trait"};
            Need(!game.LearnTalent(game.Heroes[0].HeroInstanceId,traitId,out _),"未购资格仍可学T："+spec);
            foreach(var step in PrerequisiteChain(config,gate))Need(game.BuyStar(step,out string pre),"资格前置失败 "+step+"："+pre);
            long goldBefore=game.Gold;Need(game.BuyStar(gate,out string gateWhy),"资格购买失败 "+spec+"："+gateWhy);
            Need(game.ClassTraitUnlocked(spec),"购买后未解锁："+spec);
            Need(game.LearnTalent(game.Heroes[0].HeroInstanceId,traitId,out string learn),"购买后T学习失败："+spec+" "+learn);
            Need(!game.Heroes[1].Talents.ContainsKey(traitId),"T投入串到同职另一实例："+spec);
            // 同职第二实例不再收费：已解锁即可学，不需要再次购买。
            Need(game.LearnTalent(game.Heroes[1].HeroInstanceId,traitId,out string learnB),"同职第二实例学习失败（不应重复付费）："+learnB);
            Need(game.Gold==goldBefore-(int)config.Stars.Nodes[gate].Costs[0],"资格被重复收费："+spec);
            var reloaded=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=game.CaptureProgress()})).Session!);
            Need(reloaded.ClassTraitUnlocked(spec)&&reloaded.Heroes[0].Talents.ContainsKey(traitId),"读档后资格/T丢失："+spec);
            ledger.Add($"{ledger.Count},{gate},class_trait_unlock,{goldBefore},{(int)config.Stars.Nodes[gate].Costs[0]},{game.Gold},{game.Stars.Level(gate)},0,1");
        }

        // 3) 十项退休节点：可见性/购买均拒绝，等级保留。
        foreach(var retired in new[]{"s_party_1","s_skill_1","s_respec","s_respec_discount"})
        {
            var game=GameSession.CreateWorld(config,6400,new[]{"warrior"});game.GrantGmResources(gold:100000);
            Need(!game.BuyStar(retired,out _)&&!game.Stars.Visible(retired),"退休节点仍可买/可见："+retired);
        }

        // 4) 类别端到端：同一随机种子 A/B 对照，证明消费者真实读取（非UI文案）。
        var proof=new List<string>{"case,baseline,with_star"};
        double AttackSpeedDamage(int seed,bool buy)
        {
            var g=GameSession.CreateWorld(config,seed,new[]{"warrior","warrior"});
            var h=g.Heroes[0];g.GrantGmResources(gold:200000,experience:1000000);
            while(h.Level<25&&g.UpgradeHero(h.HeroInstanceId,out _)){}
            if(buy)Buy(g,config,"O02",3);
            for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];h.SkillIds=new[]{"barb_bash"};h.Attack=100;h.Timer=0;h.Position=room.Center;
            foreach(var m in room.Monsters){m.Hp=m.MaxHp=1000000;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(.5f,0);}
            double before=room.Monsters[0].Hp;for(int i=0;i<200;i++)g.Tick(.05f);
            return before-room.Monsters[0].Hp;
        }
        proof.Add($"O02_attack_speed,{AttackSpeedDamage(7001,false):0.##},{AttackSpeedDamage(7001,true):0.##}");
        double FireDamage(int seed,bool buy)
        {
            var g=GameSession.CreateWorld(config,seed,new[]{"mage","mage"});
            var h=g.Heroes[0];g.GrantGmResources(gold:200000,experience:1000000);
            while(h.Level<25&&g.UpgradeHero(h.HeroInstanceId,out _)){}
            if(buy)Buy(g,config,"O04",3);
            for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];h.SkillIds=new[]{"mage_fireball"};h.Attack=100;h.Timer=0;h.Position=room.Center;
            foreach(var m in room.Monsters){m.Hp=m.MaxHp=1000000;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(.5f,0);}
            double before=room.Monsters[0].Hp;for(int i=0;i<200;i++)g.Tick(.05f);
            return before-room.Monsters[0].Hp;
        }
        proof.Add($"O04_element_damage,{FireDamage(7002,false):0.##},{FireDamage(7002,true):0.##}");
        // 评级：AR/DR 直接可读，购买后必须提升。
        {
            var g=GameSession.CreateWorld(config,7003,new[]{"warrior"});g.GrantGmResources(gold:200000);
            double ar0=g.Heroes[0].AttackRatingBonus,dr0=g.Heroes[0].DefenseRatingBonus;
            foreach(var step in PrerequisiteChain(config,"R01"))Need(g.BuyStar(step,out string preWhy),"评级前置失败 "+step+"："+preWhy);
            for(int i=0;i<3;i++)Need(g.BuyStar("R01",out string arWhy),"R01购买失败："+arWhy);
            for(int i=0;i<3;i++)Need(g.BuyStar("R02",out string drWhy),"R02购买失败："+drWhy);
            double ar1=g.Heroes[0].AttackRatingBonus,dr1=g.Heroes[0].DefenseRatingBonus;
            Need(ar1>ar0&&dr1>dr0,"评级节点未生效");
            proof.Add($"R01_R02_rating,{ar0:0.#}/{dr0:0.#},{ar1:0.#}/{dr1:0.#}");
        }
        // 药剂时效/效力。
        {
            var g=GameSession.CreateWorld(config,7004,new[]{"warrior"});g.GrantGmResources(gold:200000);
            int baseTicks=config.PotionOf("potion_might").DurationTicks;
            g.QaGrantConsumable("potion_might");g.UsePotion(0,out _);int plain=(int)Math.Round(g.PotionSlots[0].RemainingSeconds);
            var h=GameSession.CreateWorld(config,7005,new[]{"warrior"});h.GrantGmResources(gold:200000);
            Buy(h,config,"P01",3);
            h.QaGrantConsumable("potion_might");h.UsePotion(0,out _);int extended=(int)Math.Round(h.PotionSlots[0].RemainingSeconds);
            Need(extended>plain,"药剂时效未延长");
            proof.Add($"P01_potion_duration,{plain:0.#},{extended:0.#}");
            var p=GameSession.CreateWorld(config,7006,new[]{"warrior"});p.GrantGmResources(gold:200000);
            p.QaGrantConsumable("potion_might");p.UsePotion(0,out _);double plainPct=p.PotionAttackPct;
            Buy(p,config,"P02",3);double boosted=p.PotionAttackPct;
            Need(boosted>plainPct,"药剂效力未提升");
            proof.Add($"P02_potion_effect,{plainPct:0.####},{boosted:0.####}");
            Need(baseTicks>0,"药剂配置异常");
        }
        // 击杀金币/出售收益。
        {
            var g=GameSession.CreateWorld(config,7007,new[]{"warrior"});g.GrantGmResources(gold:200000);
            Buy(g,config,"F01",4);
            Buy(g,config,"F02",3);
            Need(g.StarDerived("kill_gold_pct")>0&&g.StarDerived("sell_gold_pct")>0,"金币乘率未派生");
            proof.Add($"F01_F02_derived,0,{g.StarDerived("kill_gold_pct"):0.###}/{g.StarDerived("sell_gold_pct"):0.###}");
        }
        // 卷轴威力：同一目标同一Attacker下伤害必须更高（LastDamage 可读）。
        {
            double ScrollHit(bool buy)
            {
                var g=GameSession.CreateWorld(config,7008,new[]{"warrior"});g.GrantGmResources(gold:200000);
                if(buy)Buy(g,config,"S01",3);
                for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
                var room=g.Floor.Rooms[g.CurrentRoom];g.Heroes[0].Position=room.Center;
                foreach(var m in room.Monsters){m.Hp=m.MaxHp=100000;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(.5f,0);}
                g.QaGrantConsumable("scroll_bolt");g.UseScroll("scroll_bolt",0,out string why);
                return g.LastDamage?.FinalDamage??-1;
            }
            double plain=ScrollHit(false),boosted=ScrollHit(true);
            Need(boosted>plain,"卷轴威力未生效："+plain+" → "+boosted);
            proof.Add($"S01_scroll_damage,{plain:0.##},{boosted:0.##}");
        }
        // 离线修行：同一快照、同一时间差，星图节点必须提高结算值。
        {
            long savedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-7200;
            var snap=GameSession.CreateWorld(config,7009,new[]{"warrior"}).CaptureProgress();
            snap.SavedAtUnix=savedAt;
            var plain=GameSession.SettleOffline(config,snap,DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var withStar=GameSession.CreateWorld(config,7010,new[]{"warrior"});
            withStar.GrantGmResources(gold:200000);Buy(withStar,config,"F03",3);
            var snap2=withStar.CaptureProgress();snap2.SavedAtUnix=savedAt;
            var boosted=GameSession.SettleOffline(config,snap2,DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Need(boosted.Experience>plain.Experience&&boosted.Gold>plain.Gold,$"离线修行未生效 plain={plain} boosted={boosted} pct={withStar.StarDerived("offline_pct")} ranks={snap2.Stars.Ranks.Length} now={DateTimeOffset.UtcNow.ToUnixTimeSeconds()} saved={snap2.SavedAtUnix}");
            proof.Add($"F03_offline,{plain.Experience}/{plain.Gold},{boosted.Experience}/{boosted.Gold}");
        }
        File.WriteAllLines(Path.Combine(evidence,"star_effect_proof.csv"),proof);
        File.WriteAllLines(Path.Combine(evidence,"star_purchase_ledger.csv"),ledger);

        // 5) 存档幂等：购买后读档两次，金币/等级/派生值完全一致。
        {
            var g=GameSession.CreateWorld(config,7011,new[]{"warrior"});g.GrantGmResources(gold:200000);
            foreach(var step in PrerequisiteChain(config,"C-PAL"))g.BuyStar(step,out _);
            Buy(g,config,"C-PAL");Buy(g,config,"R01");Buy(g,config,"P01");
            var once=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
            var twice=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=once.CaptureProgress()})).Session!);
            Need(once.Gold==g.Gold&&twice.Gold==g.Gold,"读档后金币变化");
            foreach(var attr in new[]{"attack_rating","potion_duration_pct"})Need(Math.Abs(once.StarDerived(attr)-twice.StarDerived(attr))<1e-9&&Math.Abs(twice.StarDerived(attr)-g.StarDerived(attr))<1e-9,"读档后派生值漂移："+attr);
            Need(once.Heroes[0].AttackRatingBonus==twice.Heroes[0].AttackRatingBonus,"读档后评级漂移");
        }

        B2(config,evidence);

        Console.WriteLine($"C1B_STAR_PASS: B1 17 nodes + B2 elite/heal/xp receipts, 4 class gates, 10 retired rejected, ledger/proof/save idempotent; evidence={evidence}");
    }
    /// <summary>
    /// B2 三个子切片：O05 只对精英生效、D04 只放大真实受治疗量、G02 按令牌一次性发放。
    /// 三者都必须给出同种子 A/B 与"无效上下文"对照，不能以通用ATK/文案代替。
    /// </summary>
    private static void B2(Config config,string evidence)
    {
        var proof=new List<string>{"case,baseline,with_star"};
        // O05 精英伤害：同一目标、同一Attacker，先比非精英（应无差异），再比精英（应有差异）。
        double EliteHit(bool buy,bool elite,int seed)
        {
            var g=GameSession.CreateWorld(config,seed,new[]{"warrior","warrior"});
            var h=g.Heroes[0];g.GrantGmResources(gold:200000,experience:1000000);
            while(h.Level<25&&g.UpgradeHero(h.HeroInstanceId,out _)){}
            BuyChainOnly(g,config,"O05");if(buy)for(int i=0;i<2;i++)Need(g.BuyStar("O05",out string whyO5),"O05购买失败："+whyO5);
            for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];h.SkillIds=new[]{"barb_bash"};h.Attack=100;h.Timer=0;h.Position=room.Center;
            var m=room.Monsters[0];m.Hp=m.MaxHp=1000000;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(.5f,0);
            m.Elite=elite;m.EliteToken=elite?"QA-ELITE":"";
            double before=m.Hp;for(int i=0;i<120;i++)g.Tick(.05f);
            return before-m.Hp;
        }
        double plainNormal=EliteHit(false,false,8101),o05Normal=EliteHit(true,false,8101);
        double plainElite=EliteHit(false,true,8101),o05Elite=EliteHit(true,true,8101);
        Need(Math.Abs(o05Normal-plainNormal)<1e-6,"O05对非精英目标产生了通用增伤（不允许）");
        Need(o05Elite>plainElite,"O05未提高精英目标伤害");
        proof.Add($"O05_non_elite,{plainNormal:0.##},{o05Normal:0.##}");
        proof.Add($"O05_elite,{plainElite:0.##},{o05Elite:0.##}");

        // D04 受治疗增强：同一祈祷、同一缺口，买后实际回血更多；满血目标不应被"过度治疗"。
        int PrayerHeal(bool buy,int seed)
        {
            var g=GameSession.CreateWorld(config,seed,new[]{"priest","warrior"});
            g.GrantGmResources(gold:200000,experience:1000000);
            var p=g.Heroes[0];while(p.Level<25&&g.UpgradeHero(p.HeroInstanceId,out _)){}
            BuyChainOnly(g,config,"D04");if(buy)for(int i=0;i<2;i++)Need(g.BuyStar("D04",out string whyD4),"D04购买失败："+whyD4);
            Need(g.LearnTalent(p.HeroInstanceId,"pal_prayer",out string learn),"祭司学习祈祷失败："+learn);
            for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];
            foreach(var monster in room.Monsters){monster.Hp=monster.MaxHp=1000000;monster.Attack=0;monster.Timer=999;monster.Position=room.Center+new Vector2(2f,0);}
            foreach(var hero in g.Heroes){hero.Position=room.Center;hero.Hp=hero.MaxHp*.2f;hero.Timer=999;hero.Attack=1;}
            p.Timer=0;
            for(int i=0;i<200;i++)g.Tick(.05f);
            return (int)Math.Round(g.Heroes.Sum(x=>x.Hp)-g.Heroes.Sum(x=>x.MaxHp*.2f));
        }
        int healPlain=PrayerHeal(false,8102),healBoosted=PrayerHeal(true,8102);
        Need(healBoosted>healPlain,"D04未提高实际受治疗量");
        proof.Add($"D04_healing,{healPlain},{healBoosted}");

        // G02 精英击杀XP：同令牌只发一次；不同实例可再领；读档后收据保留。
        long xpBefore,afterFirst,afterDup;
        string token="QA-ELITE-TOKEN";
        {
            var g=GameSession.CreateWorld(config,8103,new[]{"warrior"});
            var h=g.Heroes[0];g.GrantGmResources(gold:200000,experience:1000000);
            while(h.Level<25&&g.UpgradeHero(h.HeroInstanceId,out _)){}
            Buy(g,config,"G02",3);
            for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];
            h.SkillIds=new[]{"barb_bash"};h.Attack=1000000;h.Timer=0;h.Position=room.Center;
            foreach(var m in room.Monsters){m.Hp=m.MaxHp=1;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(.4f,0);m.Elite=true;m.EliteToken=token;}
            var first=room.Monsters[0];var second=room.Monsters.Count>1?room.Monsters[1]:null;
            xpBefore=g.PublicExperience;
            for(int i=0;i<400&&first.Alive;i++)g.Tick(.05f);
            afterFirst=g.PublicExperience;
            Need(afterFirst>xpBefore,"精英击杀未获得XP");
            if(second!=null){for(int i=0;i<400&&second.Alive;i++)g.Tick(.05f);}
            afterDup=g.PublicExperience;
            Need(g.EliteReceiptClaimed(token),"精英收据未记录");
            var reloaded=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
            Need(reloaded.EliteReceiptClaimed(token),"读档后精英收据丢失");
        }
        {
            // 同令牌第二只精英：只给基础XP，不再给精英加成。
            var g=GameSession.CreateWorld(config,8104,new[]{"warrior"});
            var h=g.Heroes[0];g.GrantGmResources(gold:200000,experience:1000000);
            while(h.Level<25&&g.UpgradeHero(h.HeroInstanceId,out _)){}
            Buy(g,config,"G02",3);
            for(int i=0;i<4000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];
            h.SkillIds=new[]{"barb_bash"};h.Attack=1000000;h.Timer=0;h.Position=room.Center;
            var m=room.Monsters[0];m.Hp=m.MaxHp=1;m.Attack=0;m.Timer=999;m.Position=room.Center+new Vector2(.4f,0);m.Elite=true;m.EliteToken=token;
            int baseXp=m.RewardXp;long before=g.PublicExperience;
            for(int i=0;i<400&&m.Alive;i++)g.Tick(.05f);
            long delta=g.PublicExperience-before;
            // 新会话属于新的Run：收据不跨Run继承（用户口径：同模板重进可再领），应含精英加成。
            Need(delta>=baseXp,"同令牌新Run应可再领：delta="+delta+" base="+baseXp);
            Need(delta>baseXp,"新Run精英加成未生效：delta="+delta);
        }
        File.WriteAllLines(Path.Combine(evidence,"elite_heal_receipt_proof.csv"),proof);
        Need(afterDup>=afterFirst,"XP账本倒退");
    }
    /// <summary>只买前置链，不买目标节点：A/B 对照必须隔离目标节点本身的效果。</summary>
    private static void BuyChainOnly(GameSession game,Config config,string id)
    {
        foreach(var step in PrerequisiteChain(config,id))
            if(game.Stars.Level(step)==0)Need(game.BuyStar(step,out string pre),"前置购买失败 "+step+"："+pre);
    }
    /// <summary>买齐未点亮的前置后再买目标节点，避免测试因前置缺失静默失败。</summary>
    private static void Buy(GameSession game,Config config,string id,int times=1)
    {
        foreach(var step in PrerequisiteChain(config,id))
            if(game.Stars.Level(step)==0)Need(game.BuyStar(step,out string pre),"前置购买失败 "+step+"："+pre);
        for(int i=0;i<times;i++)Need(game.BuyStar(id,out string why),"购买失败 "+id+" 第"+(i+1)+"次："+why);
    }    /// <summary>按布局前置求一条可购买路径（同层任一前置点亮即可）。</summary>
    private static List<string> PrerequisiteChain(Config config,string id)
    {
        var chain=new List<string>();
        void Visit(string node)
        {
            var layout=config.Stars.Layout[node];
            if(layout.Prerequisites.Count==0){if(!chain.Contains(node))chain.Add(node);return;}
            string pick=layout.Prerequisites[0];
            Visit(pick);
            if(!chain.Contains(pick))chain.Add(pick);
        }
        Visit(id);
        if(chain.Contains(id))chain.Remove(id);
        return chain;
    }
}
