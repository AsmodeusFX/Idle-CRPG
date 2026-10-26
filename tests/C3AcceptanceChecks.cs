using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleCrpg.Core;

/// <summary>
/// S15-C3（首轮数值配置与运行验收 V0.7）的工程自检入口。
/// <para>
/// C3-0：参数→真实消费者的逐行映射与来源哈希（只读，不覆盖生产配置）。
/// C3-1：固定Seed的XP/KP/Gold逐笔守恒、收据去重与同Seed可复现。
/// C3-2：按设计实验矩阵跑受控场景（记录未建模维度为 ACTION_NOT_MODELED，不用代理动作冒充）。
/// C3-3：生产数值与设计候选的差异表——只提出，不擅自覆盖正式数值。
/// </para>
/// </summary>
public static class C3AcceptanceChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C3: "+why);}

    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        string evidence=Path.Combine(root,"artifacts/qa","s15-c3-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        string scenarioDir=Path.Combine(evidence,"scenarios");Directory.CreateDirectory(scenarioDir);
        var summary=new List<string>{"section,item,value"};

        // ── C3-0 参数→消费者映射 ───────────────────────────────────────────────
        var map=new List<string>{"param_family,candidate_field,unit,consumer_path,verified,evidence,authority_state,note"};
        void Row(string family,string field,string unit,string consumer,string file,string needle,string state,string note)
        {
            string where=Probe(root,file,needle);
            bool ok=where!="";
            map.Add($"{family},{field},{unit},{consumer},{(ok?"yes":"no")},{where},{state},{note}");
            Need(ok,$"参数 {family} 缺少真实消费者：{file} 中未找到 {needle}");
        }
        Row("XP_POOL","PublicExperience","xp","GameSession.GainExperience→AuditCurrency(xp)","Idle-CRPG/Scripts/Core/HeroExperience.cs","AuditCurrency(source,\"receive\",\"xp\"","H_QA_ONLY","收入与消耗都进 AssetAudit，不另建账本");
        Row("XP_PER_MONSTER_LEVEL","RewardXp","xp/kill","SpawnRewardXp(spawn_rule.xp_base)","Idle-CRPG/Scripts/Core/MonsterSpawning.cs","RewardXp=checked(XpBase*level","H_QA_ONLY","按实际生成怪级取 xp_base，不走章号");
        Row("HERO_LEVEL_COST","UpgradeXpCost","xp/level","GameSession.UpgradeHero→herolevel.exp","Idle-CRPG/Scripts/Core/HeroExperience.cs","PublicExperience-=cost","H_QA_ONLY","一次点击恰升一级，失败零变更");
        Row("HERO_BASE_GROWTH","Attack/MaxHp","atk,hp/level","herolevel.attack_bonus/hp_bonus","Idle-CRPG/Scripts/Core/HeroExperience.cs","AttackBonus","H_QA_ONLY","只生效增量；装备独立差量");
        Row("AR_DR_RATING","AttackRating/DefenseRating","rating","DamagePipeline.Resolve","Idle-CRPG/Scripts/Core/DamagePipeline.cs","AttackRating","H_QA_ONLY","全员独立 AR/DR，无 Armor 双活");
        Row("MONSTER_RATING","MonsterRatingPerLevel","rating","PrototypeProgression.MonsterRatingPerLevel","Idle-CRPG/Scripts/Core/PrototypeProgression.cs","MonsterRatingPerLevel","H_QA_ONLY","实际怪级唯一，同源快照");
        Row("LMAX_KP_COST","LmaxCost","kp/rank","GameSession.BuyKpUpgrade","Idle-CRPG/Scripts/Core/KpProgression.cs","Config.Progression.LmaxCost","H_QA_ONLY","逐级+1并写账；生产=10，设计候选逐阶不同");
        Row("LMIN_PLAYER","MonsterLevelMin","level","BuyKpUpgrade(MonsterLevelMinimum) 拒绝","Idle-CRPG/Scripts/Core/KpProgression.cs","MonsterLevelMinimum","C_DISABLED","玩家命令与存档均保持1");
        Row("MONSTER_HP_ATK","monsterlevel hp/attack factor","multiplier","Config.MonsterLevels","Idle-CRPG/Scripts/Core/LevelConfiguration.cs","MonsterLevels=monsters","H_QA_ONLY","1..10000 旧表不因31档XP输入截断");
        Row("P0_ENCOUNTER_PACING","FloorPlan/spawn_rule","room/cell","Reveal→EncounterPlan→spawn_rule","Idle-CRPG/Scripts/Core/EncounterPlans.cs","EncounterFor","P_IMPLEMENTED_C2A","计划击杀不强制刷怪");
        Row("GOLD_KILL","loot.Gold","gold/kill","CollectLoot→AuditCurrency(gold)","Idle-CRPG/Scripts/Core/GameSession.cs","AuditCurrency(\"loot_pickup\"","H_QA_ONLY","拾取入账只一次，星图/药剂乘率同点");
        Row("GOLD_STAR_BONUS","StarKillGoldPct","percent","CollectLoot 入账点","Idle-CRPG/Scripts/Core/GameSession.cs","StarKillGoldPct","P_IMPLEMENTED_C1B","只放大合法击杀源");
        Row("STAR_PRICE","StarRankCost","gold/rank","Stars.BuyStar","Idle-CRPG/Scripts/Core/Stars.cs","AuditCurrency(\"player_command\",\"buy_star\"","H_QA_ONLY","逐Rank扣费+效果，保存一致");
        Row("STAR_XP_BONUS","XpGainMultiplier","percent","GainExperience","Idle-CRPG/Scripts/Core/HeroExperience.cs","XpGainMultiplier","P_IMPLEMENTED_C1B","只放大购买后XP，不倒算");
        Row("EQUIPMENT_DROP","DropChance[level]","probability","TryDropEquipment→drop_table","Idle-CRPG/Scripts/Core/Equipment.cs","DropRuleOfLevel","H_QA_ONLY","C2-B 按实际怪级");
        Row("EQUIPMENT_QUALITY","common/uncommon/rare/legendary","id/multiplier","Config.LoadEquipment + 存档校验","Idle-CRPG/Scripts/Core/Equipment.cs","EquipmentQualityOf","C_ID_H_VALUES","生产品质ID不重命名");
        Row("EQUIPMENT_SALE","SellInferiorEquipment","gold/item","SellJunk→AuditCurrency(gold)","Idle-CRPG/Scripts/Core/GameSession.cs","AuditCurrency(\"village_trade\"","H_QA_ONLY","跨属性保护，不重复卖出");
        Row("SCROLL_DROPS","SC01..05","probability","TryDropConsumable/UseScroll","Idle-CRPG/Scripts/Core/Consumables.cs","UseScroll","H_QA_ONLY","5槽固定，禁自动使用");
        Row("POTION_DROPS","PO01..04","probability","potionSlots/AdvanceConsumables","Idle-CRPG/Scripts/Core/Consumables.cs","AdvanceConsumables","H_QA_ONLY","4槽固定，active tick 冻结");
        Row("OFFLINE_YIELD","SettleOffline","xp,kp,gold/hour","SaveStore/Offline settlement","Idle-CRPG/Scripts/Core/Rebirth.cs","SettleOffline","H_QA_OPTIONAL","幂等、独立于在线预算");
        map.Add("L1_TO_L31_TIMELINE,chapter Lmax/Hero level,level/milestone,runtime only,unavailable,no-single-consumer,H_QA_ONLY,由 C3-2 实跑事件观测，无独立配置消费者");
        File.WriteAllLines(Path.Combine(evidence,"c3_consumer_map.csv"),map);
        summary.Add($"C3-0,consumer_rows,{map.Count-1}");

        // 来源哈希：设计冻结输入 + 生产配置，用于"用的就是这套表"的门禁。
        var hashes=new List<string>{"file,scope,sha256"};
        string temp=Path.Combine(root,"docs/temp/P2-4 S15_C3_首轮数值配置与运行验收_V0.7");
        foreach(string name in new[]{"C3_31怪级联合参数待校准_H-B1.csv","C3_英雄1到25级XP升级费用与累计_H-B1.csv","C3_实机模拟实验矩阵_P.csv","C3_配置参数与消费者生产映射.csv","C3_四章必经静态内容与数值输入修正_H.csv"})
        {
            string path=Path.Combine(temp,name);
            hashes.Add($"{name},design_ref,{(File.Exists(path)?Sha256(path):"missing")}");
        }
        foreach(string relative in new[]{"monster/spawn_rule.csv","monster/monsterlevel.csv","hero/herolevel.csv","combat/prototype_progression.csv","item/drop_table.csv","monster/boss_phase.csv","chapters/act1/dungeon/castle_threat.csv"})
        {
            string path=Path.Combine(root,"Idle-CRPG/config",relative.Replace('/',Path.DirectorySeparatorChar));
            hashes.Add($"{relative},production_config,{(File.Exists(path)?Sha256(path):"missing")}");
        }
        File.WriteAllLines(Path.Combine(evidence,"c3_source_hashes.csv"),hashes);
        Need(hashes.All(h=>h.Split(',')[2]!="missing"),"来源哈希缺失");

        // ── C3-1 固定Seed：守恒、收据去重、同Seed可复现 ──────────────────────
        // 时长可用环境变量缩短以便快速定位缺口；正式跑默认 600 游戏分钟。
        int minutes=int.TryParse(Environment.GetEnvironmentVariable("C3_MINUTES"),out int overrideMinutes)?overrideMinutes:600;
        string baseline=WriteScenario(scenarioDir,"C3_BASELINE_P4",new[]{"warrior","ranger","mage","priest"},600,240,3301);
        var runA=RunScenario(root,baseline,3301,"c3a");
        var runB=RunScenario(root,baseline,3301,"c3b");
        string assetA=Path.Combine(runA,"asset_ledger.csv"),assetB=Path.Combine(runB,"asset_ledger.csv");
        Need(File.Exists(assetA)&&File.Exists(assetB),"资产流水未导出");
        Need(Sha256(assetA)==Sha256(assetB),"同Seed两次运行的资产流水不一致（不可复现）");
        var conservation=VerifyConservation(assetA);
        File.WriteAllLines(Path.Combine(evidence,"c3_conservation.csv"),conservation);
        Need(conservation.Skip(1).All(r=>r.EndsWith(",ok")),$"三账不闭合：{string.Join(';',conservation.Skip(1).Where(r=>!r.EndsWith(",ok")))}");
        // 收据去重：同一 MonsterKill 收据键不得重复入账（合法重进房间是新的 DungeonEntrySerial）。
        var keys=File.ReadAllLines(assetA).Skip(1).Select(line=>SplitCsv(line))
            .Where(c=>c.Length>11&&c[6]=="kp"&&c[4]=="receive").Select(c=>c[11]).ToArray();
        Need(keys.Length>0,"没有观测到KP入账");
        Need(keys.Distinct().Count()==keys.Length,"KP击杀收据键重复入账");
        summary.Add($"C3-1,ledger_rows,{File.ReadAllLines(assetA).Length-1}");
        summary.Add($"C3-1,deterministic_hash,{Sha256(assetA)[..16]}");

        // ── C3-2 实验矩阵 ────────────────────────────────────────────────────
        var matrix=new List<string>{"scenario,party,interval_minutes,seed,chapter_reached,lmax_final,hero_levels,kills,gold,public_xp,ch1_arrive_min,ch2_arrive_min,ch3_arrive_min,ch4_arrive_min,status,notes"};
        var parties=new (string Id,string[] Heroes)[]{
            ("P4",new[]{"warrior","ranger","mage","priest"}),
            ("P2R2P",new[]{"ranger","ranger","priest","priest"}),
            ("P4SAME",new[]{"warrior","warrior","warrior","warrior"}),
            ("P1",new[]{"warrior"})};
        foreach(var (party,heroes) in parties)
        foreach(int intervalMinutes in new[]{10,45})
        foreach(int seed in (party=="P4"?new[]{3301,3302}:new[]{3301}))
        {
            string id=$"C3_{party}_{intervalMinutes}MIN_{seed}";
            string path=WriteScenario(scenarioDir,id,heroes,intervalMinutes*60,minutes,seed);
            string directory=RunScenario(root,path,seed,"c3");
            var metrics=ReadMetrics(directory);
            matrix.Add($"{id},{party},{intervalMinutes},{seed},{metrics.Chapter},{metrics.Lmax},{metrics.Heroes},{metrics.Kills},{metrics.Gold},{metrics.PublicXp},{metrics.Arrive[1]},{metrics.Arrive[2]},{metrics.Arrive[3]},{metrics.Arrive[4]},{metrics.Status},");
        }
        // 未建模维度必须显式记录，不能用代理动作冒充完成。
        foreach(string gap in new[]{
            "OPTIONAL_CONTENT_POLICY:auto-advance 只有一个选路策略，无 NO_OPTIONAL/HALF 开关 → ACTION_NOT_MODELED",
            "EQUIPMENT_POLICY:产品禁止自动换装 → ACTION_NOT_MODELED（需玩家手动点击）",
            "CONSUMABLE_POLICY:产品禁止自动使用卷轴/药剂 → ACTION_NOT_MODELED",
            "QA_REDUCED_ENCOUNTER_COVERAGE_80/70:需要生成期降覆盖率开关，当前无 → NOT_MODELED"})
            matrix.Add($"GAP,-,-,-,-,-,-,-,-,-,-,-,-,-,-,NOT_MODELED,\"{gap}\"");
        File.WriteAllLines(Path.Combine(evidence,"c3_matrix.csv"),matrix);

        // ── C3-3 差异表（只提出，不覆盖生产数值）────────────────────────────
        var proposals=new List<string>{"param,production_value,design_candidate,delta_note,impact,approval"};
        proposals.Add($"lmax_cost,{config.Progression.LmaxCost}（每级固定）,\"14/28/42/56…（逐阶KP价格，CumLmaxKPCost 见表）\",\"生产为平铺10，设计为递增\",\"Lmax推进节奏与KP总需求\",REQUIRES_USER_APPROVAL");
        proposals.Add($"hero_level_exp,{config.HeroLevels[1].Experience}×等级（1→2=18）,\"100/160/220/260/310…（1→25 累计 54120）\",\"生产是线性18×等级，设计为分段递增\",\"四英雄升级时间与XP预算\",REQUIRES_USER_APPROVAL");
        proposals.Add($"hero_rating_per_level,{config.Progression.HeroRatingPerLevel},10（设计 AR=10×等级）,\"一致\",\"AR/DR 门槛\",NO_CHANGE");
        proposals.Add($"monster_rating_per_level,{config.Progression.MonsterRatingPerLevel},10,\"一致\",\"怪物评级\",NO_CHANGE");
        proposals.Add($"equipment_drop_fallback,{config.Progression.EquipmentDropPct},0.018–0.0216（按怪级）,\"仅旧组合回退口径\",\"旧组合掉落率更高\",NO_CHANGE（C2-B 等级表已生效）");
        File.WriteAllLines(Path.Combine(evidence,"c3_tuning_proposals.csv"),proposals);
        File.WriteAllLines(Path.Combine(evidence,"c3_summary.csv"),summary);

        Console.WriteLine($"C3_PASS: consumer map {map.Count-1} rows, conservation ok, matrix {matrix.Count-5} scenarios @{minutes}min, proposals {proposals.Count-1} (none applied); evidence={evidence}");
    }

    private static string Probe(string root,string relative,string needle)
    {
        string path=Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));
        if(!File.Exists(path))return "";
        var lines=File.ReadAllLines(path);
        for(int i=0;i<lines.Length;i++)if(lines[i].Contains(needle,StringComparison.Ordinal))return $"{relative}:{i+1}";
        return "";
    }
    private static string Sha256(string path)
    {
        using var sha=SHA256.Create();
        using var stream=File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
    /// <summary>极简CSV切分：仅用于本模块自写的流水（字段已加引号且不内嵌换行）。</summary>
    private static string[] SplitCsv(string line)
    {
        var cells=new List<string>();var current=new StringBuilder();bool quoted=false;
        foreach(char ch in line)
        {
            if(ch=='"'){quoted=!quoted;continue;}
            if(ch==','&&!quoted){cells.Add(current.ToString());current.Clear();continue;}
            current.Append(ch);
        }
        cells.Add(current.ToString());return cells.ToArray();
    }

    private static string WriteScenario(string directory,string id,string[] heroes,int intervalSeconds,int minutes,int seed)
    {
        string roster=string.Join(",",heroes.Select(h=>"\""+h+"\""));
        string json=$$"""
{
  "schema_version": 2,
  "scenario_id": "{{id}}",
  "scenario_note": "C3 实验矩阵：显式玩家点击代理（逐名升级/根节点星图/城堡），非自动升级",
  "seed": {{seed}},
  "duration_minutes": {{minutes}},
  "sample_interval_minutes": 30,
  "dense_sample_minutes": 0,
  "dense_every_minutes": 0,
  "starting_heroes": [{{roster}}],
  "player_policy": {
    "castle": { "enabled": true, "interval_seconds": {{intervalSeconds}}, "max_requests": 0 },
    "starmap": { "mode": "explicit", "interval_seconds": {{intervalSeconds}}, "nodes": [ { "id": "s_root", "max_level": 1 } ] },
    "party": { "mode": "none" },
    "talents": { "mode": "none" },
    "upgrade": { "mode": "greedy", "interval_seconds": {{intervalSeconds}} },"lmax": { "enabled": true, "interval_seconds": {{intervalSeconds}} },
    "diagnostics": { "stall_window_seconds": 600, "recheck_seconds": 300, "max_alerts": 5 }
  }
}
""";
        string path=Path.Combine(directory,id+".json");
        File.WriteAllText(path,json,new UTF8Encoding(false));
        return path;
    }

    private static string RunScenario(string root,string path,int seed,string tag)
    {
        var cfg=SimulationConfig.Load(path);
        string directory=Path.Combine(root,"artifacts/simulation",cfg.ScenarioId+"_"+seed+"_"+tag);
        if(Directory.Exists(directory))Directory.Delete(directory,true);
        SimulationRunner.Run(root,path,seed,tag);
        Need(Directory.Exists(directory),"模拟输出目录未生成："+directory);
        return directory;
    }

    /// <summary>三账闭合：每个资产按流水增量求和，必须等于首末余额差。</summary>
    private static List<string> VerifyConservation(string assetLedger)
    {
        var rows=new List<string>{"asset,event_count,sum_delta,first_before,last_after,result"};
        var data=File.ReadAllLines(assetLedger).Skip(1).Select(SplitCsv).ToArray();
        foreach(string asset in new[]{"xp","kp","gold"})
        {
            var entries=data.Where(c=>c.Length>11&&c[6]==asset).ToArray();
            if(entries.Length==0){rows.Add($"{asset},0,0,0,0,no_events");continue;}
            long sum=entries.Sum(c=>long.Parse(c[8],CultureInfo.InvariantCulture));
            long firstBefore=long.Parse(entries[0][9],CultureInfo.InvariantCulture);
            long lastAfter=long.Parse(entries[^1][10],CultureInfo.InvariantCulture);
            bool ok=sum==lastAfter-firstBefore;
            rows.Add($"{asset},{entries.Length},{sum},{firstBefore},{lastAfter},{(ok?"ok":"mismatch")}");
        }
        return rows;
    }

    private sealed record Metrics(int Chapter,string Lmax,string Heroes,int Kills,long Gold,long PublicXp,string Status,Dictionary<int,string> Arrive);

    private static Metrics ReadMetrics(string directory)
    {
        var lines=File.ReadAllLines(Path.Combine(directory,"snapshots.csv"));
        var header=SplitCsv(lines[0]);
        int Col(string name)=>Array.IndexOf(header,name);
        int chapterCol=Col("chapter"),heroesCol=Col("heroes"),goldCol=Col("gold"),killsCol=Col("kills"),statusCol=Col("status"),timeCol=Col("sim_time");
        var rows=lines.Skip(1).Where(l=>l.Trim()!="").Select(SplitCsv).ToArray();
        var last=rows[^1];
        int chapter=int.Parse(last[chapterCol],CultureInfo.InvariantCulture);
        string heroes=last[heroesCol];
        // Lmax 只能从真实购买事件读；没有购买事件就写 unavailable，不用英雄等级冒充。
        var eventLines=File.ReadAllLines(Path.Combine(directory,"events.csv")).Skip(1).Select(SplitCsv).ToArray();
        var lmaxEvents=eventLines.Where(c=>c.Length>6&&c[2]=="lmax_purchase"&&c[4]=="applied").ToArray();
        string lmax=lmaxEvents.Length==0?"unavailable":lmaxEvents.Max(c=>int.Parse(c[3],CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
        long publicXp=heroes.Split('|',StringSplitOptions.RemoveEmptyEntries)
            .Select(p=>long.Parse(p.Split("public=")[1].Split(':')[0],CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
        var arrive=new Dictionary<int,string>();
        foreach(var row in rows)
        {
            int c=int.Parse(row[chapterCol],CultureInfo.InvariantCulture);
            if(!arrive.ContainsKey(c))arrive[c]=row[timeCol];
        }
        string At(int c)=>arrive.TryGetValue(c,out var v)?v:"unavailable";
        return new(chapter,lmax,heroes,int.Parse(last[killsCol],CultureInfo.InvariantCulture),long.Parse(last[goldCol],CultureInfo.InvariantCulture),publicXp,last[statusCol],
            new Dictionary<int,string>{{1,At(1)},{2,At(2)},{3,At(3)},{4,At(4)}});
    }
}
