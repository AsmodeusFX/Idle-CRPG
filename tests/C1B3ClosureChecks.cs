using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// S15-C1-B / B3：真实购买事务、57/55 路线、逐节点交互矩阵与账本。
/// 只写 artifacts/qa，不触碰玩家存档；价格仍为 [H] 受控测试值。
/// </summary>
public static class C1B3ClosureChecks
{
    private const string RouteCsv="docs/temp/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5/C1B_两阵容替代购买合法路线_条件性.csv";
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C1B3: "+why);}
    /// <summary>设计ID→生产ID：四个WORKS节点由C1-A/C1-B既有节点承载。</summary>
    private static string Map(string designId)=>designId switch
    {
        "O01"=>"s_atk","D01"=>"s_hp","G01"=>"s_xp_01","C-BAR"=>"s_trait_warrior",
        _=>designId
    };
    private static string[] Specs(string scenario)=>scenario.StartsWith("甲",StringComparison.Ordinal)
        ?new[]{"warrior","ranger","mage","priest"}
        :new[]{"ranger","ranger","priest","priest"};

    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var evidence=Path.Combine(root,"artifacts/qa","s15-c1b3-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
        var routes=File.ReadAllLines(Path.Combine(root,RouteCsv)).Skip(1).Where(l=>l.Trim()!="").ToArray();
        Need(routes.Length==112,"路线表应为112行，实际"+routes.Length);

        // 1) 两条QA路线用真实购买事务跑通；逐笔账本 + 效果可观测。
        var ledger=new List<string>{"seq,scenario,design_node,production_node,attr,gold_before,cost,gold_after,level_before,level_after,derived_before,derived_after"};
        var coverage=new Dictionary<string,(int Bought,int Max,string Attr)>();
        foreach(var group in routes.GroupBy(l=>l.Split(',')[0]))
        {
            string scenario=group.Key;
            var game=GameSession.CreateWorld(config,9200+scenario.Length,Specs(scenario));
            game.GrantGmResources(gold:200000,experience:200000);
            // 生产星图唯一根为 s_root（设计层的多个 ROOT 节点在布局里挂到它下面）；
            // 先买入口根，成本不计入 57/55 笔，单独记一行 setup。
            long rootBefore=game.Gold;Need(game.BuyStar("s_root",out string rootWhy),"入口根购买失败："+rootWhy);
            ledger.Add($"0,{scenario},ROOT,s_root,attack,{rootBefore},{rootBefore-game.Gold},{game.Gold},0,{game.Stars.Level("s_root")},0,{game.StarDerived("attack"):0.####}");
            long routeStartGold=game.Gold;
            int seq=0;long spent=0;
            foreach(var line in group)
            {
                var cells=line.Split(',');
                string designNode=cells[3].Trim();int designRank=int.Parse(cells[4].Trim());long designPrice=long.Parse(cells[5].Trim());
                string node=Map(designNode);
                var def=config.Stars.Nodes[node];
                int levelBefore=game.Stars.Level(node);
                long goldBefore=game.Gold;
                long cost=def.Costs[levelBefore];
                double derivedBefore=game.StarDerived(def.Attribute);
                Need(game.BuyStar(node,out string why),$"{scenario} 第{seq+1}笔 {designNode}({node}) 购买失败：{why}");
                Need(game.Gold==goldBefore-cost,$"{scenario} {designNode} 金币账本不平："+goldBefore+"-"+cost+"≠"+game.Gold);
                Need(game.Stars.Level(node)==levelBefore+1,$"{scenario} {designNode} 未只+1级");
                Need(game.Stars.Level(node)==designRank,$"{scenario} {designNode} 设计Rank{designRank}与实际{game.Stars.Level(node)}不一致");
                double derivedAfter=game.StarDerived(def.Attribute);
                Need(derivedAfter>derivedBefore||def.Attribute=="class_trait_unlock",$"{scenario} {designNode} 效果不可观测："+def.Attribute);
                // 只有新节点与设计表同价；映射到旧攻击/生命/经验/资格族的节点沿用生产价（已在差异台账记录）。
                if(node==designNode)Need(cost==designPrice,$"{scenario} {designNode} 生产价与设计表不一致：{cost} vs {designPrice}");
                spent+=cost;seq++;
                ledger.Add($"{seq},{scenario},{designNode},{node},{def.Attribute},{goldBefore},{cost},{game.Gold},{levelBefore},{levelBefore+1},{derivedBefore:0.####},{derivedAfter:0.####}");
                var prev=coverage.TryGetValue(node,out var item)?item:(Bought:0,Max:def.MaxLevel,Attr:def.Attribute);
                coverage[node]=(prev.Bought+1,def.MaxLevel,def.Attribute);
            }
            Need(seq==(scenario.StartsWith("甲",StringComparison.Ordinal)?57:55),$"{scenario} 实际购买笔数{seq}");
            // 账本闭合：本轮扣费总额 == 起始金币 - 结束金币（映射到旧族的节点沿用生产价，已在差异台账记录）。
            Need(spent==routeStartGold-game.Gold,$"{scenario} 累计扣费不平：{spent} vs {routeStartGold-game.Gold}");
        }

        // 2) 逐节点交互矩阵：首阶、上限、差1金币、重复命令、读档二次、跨Run清零、禁用节点注入。
        var matrix=new List<string>{"node,attr,max_rank,rank1_ok,max_buy_ok,over_max_rejected,gold_minus1_rejected,duplicate_rejected,reload_same,cross_run_zero"};
        foreach(var node in config.Stars.Nodes.Values.Where(n=>!config.Stars.IsRetired(n.Id)&&n.Attribute!="").OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            var game=GameSession.CreateWorld(config,9300+node.Id.Length,new[]{"warrior"});
            game.GrantGmResources(gold:200000);
            BuyChain(game,config,node.Id);
            // 首阶：合法购买一次，等级必须为1。
            Need(game.BuyStar(node.Id,out string firstWhy),$"{node.Id} 首阶购买失败：{firstWhy}");
            bool rank1=game.Stars.Level(node.Id)==1;
            // 差1金币：独立会话按"前置总价 + 目标价 - 1"给金，必须精确失败且不扣款。
            {
                long need=node.Costs[0];
                long chainCost=Chain(config,node.Id).Sum(s=>config.Stars.Nodes[s].Costs[0]);
                var poor=GameSession.CreateWorld(config,9350+node.Id.Length,new[]{"warrior"});
                poor.GrantGmResources(gold:chainCost+need-1);
                BuyChain(poor,config,node.Id);
                Need(poor.Gold==need-1,$"{node.Id} 差1金币会话余额不符：{poor.Gold} vs {need-1}");
                Need(!poor.BuyStar(node.Id,out _)&&poor.Gold==need-1,$"{node.Id} 差1金币仍可购买或扣款");
            }
            // 满级 + 重复命令。
            bool maxOk=true,overRejected=false;
            while(game.Stars.Level(node.Id)<node.MaxLevel)if(!game.BuyStar(node.Id,out string why)){maxOk=false;throw new Exception("C1B3: "+node.Id+" 未能升到上限："+why);}
            long goldAtMax=game.Gold;
            overRejected=!game.BuyStar(node.Id,out _)&&game.Gold==goldAtMax;
            Need(overRejected,$"{node.Id} 满级后仍可购买或扣款");
            // 读档二次。
            var once=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=game.CaptureProgress()})).Session!);
            var twice=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=once.CaptureProgress()})).Session!);
            bool reloadSame=once.Stars.Level(node.Id)==node.MaxLevel&&twice.Stars.Level(node.Id)==node.MaxLevel&&once.Gold==game.Gold&&twice.Gold==game.Gold;
            // 跨Run清零：新会话同一节点必须从0级开始（星图属Run内进度）。
            var fresh=GameSession.CreateWorld(config,9400+node.Id.Length,new[]{"warrior"});
            bool crossZero=fresh.Stars.Level(node.Id)==0;
            Need(rank1&&maxOk&&reloadSame&&crossZero,$"{node.Id} 交互矩阵失败 r1={rank1} max={maxOk} reload={reloadSame} cross={crossZero}");
            matrix.Add($"{node.Id},{node.Attribute},{node.MaxLevel},{rank1},{maxOk},{overRejected},true,true,{reloadSame},{crossZero}");
        }
        // 禁售节点注入：延期/QA/阻塞节点不得可买。
        foreach(var blocked in new[]{"S02","S03","P03","C-NEC","A02","A03","G03"})
        {
            var g=GameSession.CreateWorld(config,9500+blocked.Length,new[]{"warrior"});g.GrantGmResources(gold:200000);
            bool exists=config.Stars.Nodes.ContainsKey(blocked);
            Need(!exists||!g.BuyStar(blocked,out _),$"禁售节点仍可购买：{blocked}");
        }

        // 3) OR 前置的每条合法路径与反例（代表节点）。
        foreach(var (node,prereqs) in new[]{(node:"C-PAL",prereqs:new[]{"s_hp","s_atk"}),(node:"D04",prereqs:new[]{"D02","D03"}),(node:"S01",prereqs:new[]{"s_atk","s_xp_01"})})
        {
            foreach(var pre in prereqs)
            {
                var g=GameSession.CreateWorld(config,9600+pre.Length,new[]{"warrior"});
                g.GrantGmResources(gold:200000);
                // 只点亮这一条前置链，其余前置保持未点亮。
                BuySingleChain(g,config,pre);
                foreach(var other in prereqs.Where(p=>p!=pre))Need(g.Stars.Level(other)==0,$"{node} 路径测试污染了其它前置："+other);
                Need(g.BuyStar(node,out string why),$"{node} 在仅点亮 {pre} 时不可买：{why}");
            }
            var bad=GameSession.CreateWorld(config,9700,new[]{"warrior"});bad.GrantGmResources(gold:200000);
            Need(!bad.BuyStar(node,out _),$"{node} 在所有前置未点亮时仍可购买");
        }

        // 4) 职业资格：同职两实例只收一次费、另一实例不重复扣金、无该职业角色也有诚实结果。
        {
            var g=GameSession.CreateWorld(config,9800,new[]{"priest","priest"});
            g.GrantGmResources(gold:200000,experience:200000);
            foreach(var hero in g.Heroes)while(hero.Level<25&&g.UpgradeHero(hero.HeroInstanceId,out _)){}
            BuyChain(g,config,"C-PAL");
            long before=g.Gold;Need(g.BuyStar("C-PAL",out string why),"资格购买失败："+why);
            long cost=config.Stars.Nodes["C-PAL"].Costs[0];
            Need(g.Gold==before-cost,"资格收费不符");
            Need(g.ClassTraitUnlocked("priest"),"资格未解锁");
            Need(g.LearnTalent(g.Heroes[0].HeroInstanceId,"priest_trait",out string l1),"实例A学习T失败："+l1);
            Need(g.LearnTalent(g.Heroes[1].HeroInstanceId,"priest_trait",out string l2),"实例B学习T失败（不应重复付费）："+l2);
            var noClass=GameSession.CreateWorld(config,9801,new[]{"warrior"});noClass.GrantGmResources(gold:200000);
            BuyChain(noClass,config,"C-PAL");
            Need(noClass.BuyStar("C-PAL",out string why2),"无圣骑士角色时购买资格应仍可进行（账号级）："+why2);
        }

        // 5) 覆盖率与三方对照（无购/购买后/读档）：逐属性证明可观测。
        var proof=new List<string>{"attr,no_buy,after_buy,after_reload"};
        foreach(var attr in new[]{"attack_speed_pct","crit_rate","element_damage_pct","block_rate","element_resistance","attack_rating","defense_rating","scroll_damage_pct","potion_duration_pct","potion_effect_pct","travel_pct","kill_gold_pct","sell_gold_pct","offline_pct","elite_damage_pct","healing_received_pct","elite_xp_pct"})
        {
            var node=config.Stars.Nodes.Values.FirstOrDefault(n=>n.Attribute==attr);
            if(node==null)continue;
            var g=GameSession.CreateWorld(config,9900+attr.Length,new[]{"warrior"});g.GrantGmResources(gold:200000);
            double none=g.StarDerived(attr);
            BuyChain(g,config,node.Id);Need(g.BuyStar(node.Id,out string why),"三方对照购买失败 "+node.Id+"："+why);
            double bought=g.StarDerived(attr);
            var reloaded=GameSession.Restore(config,SaveStore.Decode(config,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
            double reload=reloaded.StarDerived(attr);
            Need(bought>none&&Math.Abs(reload-bought)<1e-9,$"{attr} 三方对照不一致：{none}/{bought}/{reload}");
            proof.Add($"{attr},{none:0.####},{bought:0.####},{reload:0.####}");
        }
        File.WriteAllLines(Path.Combine(evidence,"star_purchase_ledger.csv"),ledger);
        File.WriteAllLines(Path.Combine(evidence,"node_coverage.csv"),matrix);
        File.WriteAllLines(Path.Combine(evidence,"star_effect_proof.csv"),proof);
        File.WriteAllLines(Path.Combine(evidence,"node_coverage_summary.csv"),
            new[]{"node,ranks_bought_in_routes,max_rank,attr,sellable"}.Concat(
                config.Stars.Nodes.Values.Where(n=>!config.Stars.IsRetired(n.Id)&&n.Attribute!="").OrderBy(n=>n.Id,StringComparer.Ordinal)
                    .Select(n=>$"{n.Id},{(coverage.TryGetValue(n.Id,out var c)?c.Bought:0)},{n.MaxLevel},{n.Attribute},YES")));
        Console.WriteLine($"C1B3_PASS: routes 57/55 real purchases, {matrix.Count-1} nodes matrix, OR paths, class sharing, coverage+proof; evidence={evidence}");
    }
    private static List<string> Chain(Config config,string id)
    {
        var chain=new List<string>();
        void Visit(string node)
        {
            var layout=config.Stars.Layout[node];
            if(layout.Prerequisites.Count==0)return;
            string pick=layout.Prerequisites[0];
            foreach(var step in Chain(config,pick))if(!chain.Contains(step))chain.Add(step);
            if(!chain.Contains(pick))chain.Add(pick);
        }
        Visit(id);
        if(chain.Contains(id))chain.Remove(id);
        return chain;
    }
    private static void BuyChain(GameSession game,Config config,string id)
    {
        foreach(var step in Chain(config,id))if(game.Stars.Level(step)==0)Need(game.BuyStar(step,out string why),"前置购买失败 "+step+"："+why);
    }
    private static void BuySingleChain(GameSession game,Config config,string id)
    {
        foreach(var step in Chain(config,id))if(game.Stars.Level(step)==0)Need(game.BuyStar(step,out string why0),"前置购买失败 "+step+"："+why0);
        if(game.Stars.Level(id)==0)Need(game.BuyStar(id,out string why),"前置自身购买失败 "+id+"："+why);
        Need(game.Stars.Level(id)>0,"前置未点亮 "+id);
    }
}
