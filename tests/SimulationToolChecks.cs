using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// P2-1-M2 模拟工具自检：配置校验与拒绝、显式策略精确性、输出完整性、同 Seed 可重复性、
/// 以及异常分级器对“合法门槛/合法失败/经验平台/真实停滞”的区分。只读正式配置，不写玩家存档。
/// </summary>
public static class SimulationToolChecks
{
    public static void Run(string root)
    {
        void Need(bool value, string why) { if (!value) throw new Exception("SIM_TOOL: " + why); }
        string configRoot = Path.Combine(root, "Idle-CRPG/config");
        var config = Config.Load(configRoot);
        string scenarioDir = Path.Combine(root, "tests/Simulation");
        string qa = Path.Combine(root, "artifacts/qa/sim-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(qa);

        // 一、版本 1 兼容：历史四开关仍可加载并归一化为同一组策略。
        var a = SimulationPolicy.From(SimulationConfig.Load(Path.Combine(scenarioDir, "A_pure_idle.json")), config);
        Need(!a.CastleEnabled && a.StarMode == "none" && a.PartyMode == "none" && a.TalentMode == "none", "A 场景四开关归一化错误");
        var b = SimulationPolicy.From(SimulationConfig.Load(Path.Combine(scenarioDir, "B_guided_progress.json")), config);
        Need(b.CastleEnabled && b.StarMode == "legacy" && b.PartyMode == "legacy" && b.TalentMode == "legacy", "B 场景四开关归一化错误");
        foreach (string name in new[] { "C_castle_only", "D_solo_growth" })
            Need(SimulationPolicy.From(SimulationConfig.Load(Path.Combine(scenarioDir, name + ".json")), config).CastleEnabled, name + " 场景未启用城堡点击");

        // 二、版本 2 精确策略：E_min_party 的节点顺序/等级上限/编队名单必须等于文件内容。
        var e = SimulationPolicy.From(SimulationConfig.Load(Path.Combine(scenarioDir, "E_min_party.json")), config);
        Need(e.StarMode == "explicit" && e.StarNodes.Select(n => n.Id + "*" + n.MaxLevel).SequenceEqual(new[] { "s_root*1" }), "E 不得继续购买扩席");
        Need(e.PartyMode == "none" && SimulationConfig.Load(Path.Combine(scenarioDir,"E_min_party.json")).StartingHeroes!.SequenceEqual(new[]{"warrior","ranger","mage"}), "E 必须开局选队且不轮内扩席");
        Need(e.TalentMode == "none" && e.TalentLearn.Count == 0 && e.SkillEquip.Count == 0, "E 不应包含天赋/装配计划");
        Need(e.CastleEnabled && e.StarIntervalSeconds == 60 && e.StallWindowSeconds == 600, "E 城堡或诊断参数解析错误");

        // 三、拒绝非法配置：未知字段、版本混用、未知策略/节点/职业/技能、越界周期。
        string Write(string name, string json) { string path = Path.Combine(qa, name + ".json"); File.WriteAllText(path, json); return path; }
        void Reject(string name, string json, string why)
        {
            string path = Write(name, json);
            bool failed = false;
            try { SimulationPolicy.From(SimulationConfig.Load(path), config); }
            catch (Exception ex) when (ex is ArgumentException or IOException or System.Text.Json.JsonException) { failed = true; }
            Need(failed, why);
        }
        const string PolicyBase = "\"player_policy\":{\"castle\":{\"enabled\":false},";
        Reject("unknown_field", "{\"schema_version\":2,\"scenario_id\":\"x\",\"player_policy\":{\"castle\":{\"enabled\":false},\"unknown_key\":1}}", "未知字段没有拒绝");
        Reject("v2_no_policy", "{\"schema_version\":2,\"scenario_id\":\"x\"}", "版本 2 缺 player_policy 没有拒绝");
        Reject("v2_with_actions", "{\"schema_version\":2,\"scenario_id\":\"x\",\"player_actions\":{\"auto_castle_challenge\":true},\"player_policy\":{}}", "版本 2 混用四开关没有拒绝");
        Reject("v1_with_policy", "{\"schema_version\":1,\"scenario_id\":\"x\",\"player_actions\":{},\"player_policy\":{}}", "版本 1 混用 player_policy 没有拒绝");
        Reject("bad_mode", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"starmap\":{\"mode\":\"smart\"}}}", "未知策略名没有拒绝");
        Reject("unknown_star", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"starmap\":{\"mode\":\"explicit\",\"nodes\":[{\"id\":\"s_missing\",\"max_level\":1}]}}}", "不存在的星图节点没有拒绝");
        Reject("star_over_level", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"starmap\":{\"mode\":\"explicit\",\"nodes\":[{\"id\":\"s_root\",\"max_level\":99}]}}}", "星图等级上限越界没有拒绝");
        Reject("explicit_star_empty", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"starmap\":{\"mode\":\"explicit\",\"nodes\":[]}}}", "显式星图缺节点没有拒绝");
        Reject("unknown_hero", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"party\":{\"mode\":\"explicit\",\"roster\":[\"druid\"],\"target_size\":1}}}", "不存在的职业没有拒绝");
        Reject("dup_hero", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"party\":{\"mode\":\"explicit\",\"roster\":[\"warrior\",\"warrior\"],\"target_size\":2}}}", "重复职业没有拒绝");
        Reject("target_over_roster", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"party\":{\"mode\":\"explicit\",\"roster\":[\"warrior\"],\"target_size\":3}}}", "目标人数超过名单没有拒绝");
        Reject("roster_without_explicit", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"party\":{\"mode\":\"legacy\",\"roster\":[\"warrior\"]}}}", "非 explicit 却给名单没有拒绝");
        Reject("unknown_talent", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"talents\":{\"mode\":\"explicit\",\"learn\":[{\"hero\":\"warrior\",\"node\":\"missing_node\",\"ranks\":1}]}}}", "不存在的天赋节点没有拒绝");
        Reject("talent_wrong_hero", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"talents\":{\"mode\":\"explicit\",\"learn\":[{\"hero\":\"warrior\",\"node\":\"necro_teeth\",\"ranks\":1}]}}}", "跨职业天赋节点没有拒绝");
        Reject("unknown_skill", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"talents\":{\"mode\":\"explicit\",\"equip\":[{\"hero\":\"warrior\",\"skills\":[\"missing_skill\"]}]}}}", "不存在的技能没有拒绝");
        Reject("bad_interval", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"castle\":{\"enabled\":true,\"interval_seconds\":0}}}", "非法周期没有拒绝");
        Reject("bad_window", "{\"schema_version\":2,\"scenario_id\":\"x\"," + PolicyBase + "\"diagnostics\":{\"stall_window_seconds\":1}}}", "非法观察窗没有拒绝");
        Reject("unknown_start_hero", "{\"schema_version\":2,\"scenario_id\":\"x\",\"starting_heroes\":[\"druid\"]," + PolicyBase + "}}", "不存在的起始职业没有拒绝");
        // 默认关闭：缺省 player_policy 子对象时不得自动执行任何动作。
        var silent = SimulationPolicy.From(SimulationConfig.Load(Write("silent", "{\"schema_version\":2,\"scenario_id\":\"silent\",\"duration_minutes\":1,\"player_policy\":{}}")), config);
        Need(!silent.CastleEnabled && silent.StarMode == "none" && silent.PartyMode == "none" && silent.TalentMode == "none", "缺省策略没有默认关闭");

        // 四、运行自检场景两次：显式策略精确性、输出完整性、同 Seed 可重复性。
        string probe = Path.Combine(scenarioDir, "T_tool_probe.json");
        SimulationRunner.Run(root, probe);
        string outDir = Path.Combine(root, "artifacts/simulation", "T_tool_probe_" + 20261023);
        foreach (string file in new[] { "metadata.json", "snapshots.csv", "events.csv", "ledger.csv", "asset_ledger.csv", "diagnostics.csv", "fields.md", "diagnostics.md" })
            Need(File.Exists(Path.Combine(outDir, file)), "缺少输出文件：" + file);
        var first = new[] { "snapshots.csv", "events.csv", "ledger.csv", "diagnostics.csv" }.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(outDir, f)));
        var metadata = File.ReadAllText(Path.Combine(outDir, "metadata.json"));
        Need(metadata.Contains("config_fingerprint_sha256") && metadata.Contains("business_hash") && metadata.Contains("unavailable"), "metadata 缺少指纹/哈希/未采集声明");
        var events = first["events.csv"];
        Need(events.Contains("\"star_purchase\",\"s_root\",\"accepted\"") && events.Contains("cost=3"), "显式星图未按计划购买 s_root");
        Need(events.Contains("\"star_purchase\",\"s_hp\",\"accepted\"") && events.Contains("cost=4")&&!events.Contains("\"star_purchase\",\"s_party_"), "显式星图应买HP节点而非退休扩席");
        Need(!events.Contains("\"star_purchase\",\"s_skill_1\""), "执行了配置之外的星图购买");
        Need(events.Contains("\"party_reject\",\"warrior|ranger\"")&&!events.Contains("\"formation_request\""), "锁队必须拒绝旧策略换人且保留拒绝证据");
        Need(!events.Contains("\"talent_learn\""), "talents.mode=none 仍执行了天赋学习");
        Need(!events.Contains("\"skill_equip\""), "talents.mode=none 仍执行了技能装配");
        var ledger = first["ledger.csv"];
        Need(ledger.Contains("\"starmap_purchase\",\"s_root\",-3"), "金币流水缺少星图扣费明细");
        var snapshotRows = first["snapshots.csv"].Split('\n');
        Need(snapshotRows.Length >= 8, "密集采样行数不足（前 30 分钟每分钟）");
        // 城堡策略关闭时不得出现任何城堡点击（无隐性动作）。
        Need(!events.Contains("\"castle_click\""), "castle.enabled=false 仍然点击了城堡");
        // 多组同时到期时必须记录只执行一组的次序。
        Need(events.Contains("\"policy_order\"") && events.Contains("same_tick_contention"), "缺少同 Tick 多策略到期的次序记录");
        SimulationRunner.Run(root, probe);
        foreach (var pair in first) Need(File.ReadAllText(Path.Combine(outDir, pair.Key)) == pair.Value, "同 Seed 复跑输出不一致：" + pair.Key);

        // 四之二、指纹与性能：源码/配置指纹可区分改动；性能计数有界且非零。
        string configCopy = Path.Combine(qa, "config-fp");
        foreach (string file in Directory.GetFiles(configRoot, "*.csv", SearchOption.AllDirectories))
        {
            string target = Path.Combine(configCopy, Path.GetRelativePath(configRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
        string fingerprintBefore = SimulationRecorder.ConfigFingerprint(configCopy);
        string firstCsv = Directory.GetFiles(configCopy, "*.csv", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).First();
        File.AppendAllText(firstCsv, "\n# fingerprint probe\n");
        Need(SimulationRecorder.ConfigFingerprint(configCopy) != fingerprintBefore, "配置改动后指纹没有变化");
        string sourceFingerprint = SimulationRecorder.SourceFingerprint(root);
        Need(sourceFingerprint.Length == 64 && sourceFingerprint.All(ch => "0123456789abcdef".Contains(ch)), "源码指纹格式错误");
        var meta = System.Text.Json.JsonDocument.Parse(metadata).RootElement;
        Need(meta.TryGetProperty("source_fingerprint_sha256", out var sourceNode) && sourceNode.GetString() == sourceFingerprint, "metadata 未记录当前源码指纹");
        Need(meta.GetProperty("git_worktree").GetString()!.StartsWith("unknown"), "未声明工作区状态为 unknown");
        var perf = meta.GetProperty("perf");
        Need(perf.GetProperty("elapsed_ms").GetDouble() > 0 && perf.GetProperty("events").GetInt32() > 0 && perf.GetProperty("output_bytes").GetInt64() > 0, "性能计数缺失或为零");
        Need(perf.GetProperty("managed_heap_peak_mb").GetDouble() > 0, "缺少托管堆峰值采样");

        // 四之三、采样密度不得改变同 Seed 业务结果：事件与金币流水必须逐字节一致。
        string sparse = Path.Combine(scenarioDir, "T_tool_probe_sparse.json");
        SimulationRunner.Run(root, sparse);
        string sparseDir = Path.Combine(root, "artifacts/simulation", "T_tool_probe_sparse_" + 20261023);
        Need(File.ReadAllText(Path.Combine(sparseDir, "events.csv")) == first["events.csv"], "改变采样密度改变了事件流");
        Need(File.ReadAllText(Path.Combine(sparseDir, "ledger.csv")) == first["ledger.csv"], "改变采样密度改变了金币流水");
        Need(File.ReadAllText(Path.Combine(sparseDir, "snapshots.csv")) != first["snapshots.csv"], "稀疏采样未减少快照行（用例前提不成立）");

        // 四之四、可疑停滞现场落盘：M1 修复后长跑不再自然出现，这里直接验证落盘内容与复跑信息。
        {
            var probeCfg = SimulationConfig.Load(probe);
            var run = new SimulationRun(probeCfg, e, config, root, Path.Combine(qa, "stall-dump"));
            var alert = new DiagnosticAlert(123.45, 2469, AnomalyDiagnoser.SuspectedStuck, "测试用：无位移无目标",
                new[] { "hero_outside_room" }, "stage=Dungeon || hero outside room at door line");
            run.WriteStallDump(alert);
            string dump = Path.Combine(qa, "stall-dump", "stalls", "stall-2469.md");
            Need(File.Exists(dump), "可疑停滞现场未落盘");
            string text = File.ReadAllText(dump);
            foreach (string key in new[] { "tick 2469", "SUSPECTED_STUCK", "--seed=", "配置指纹", "源码指纹", "hero outside room" })
                Need(text.Contains(key), "停滞现场缺少字段：" + key);
        }

        // 五、异常分级器：合法门槛、合法失败、经验平台、真实停滞必须区分开。
        var stuck = new List<SimulationObservation>();
        var manual = new List<SimulationObservation>();
        var failing = new List<SimulationObservation>();
        var xpZero = new List<SimulationObservation>();
        var busy = new List<SimulationObservation>();
        AnomalyDiagnoser Make(int window = 60, int recheck = 60) => new(window, recheck, 5);
        for (int tick = 1; tick <= 400 * 20; tick++)
        {
            // 真实停滞：无位移/命中/失血/目标/入场，房间未清 → SUSPECTED_STUCK
            stuck.Add(new SimulationObservation
            {
                Tick = tick, SimTime = tick / 20d, Kills = 0, Gold = 0, Chapter = 1, Cleared = 0, Retries = 0, Trades = 0,
                LevelSum = 10, XpHundredths = 0, AnyMovement = false, AnyHit = false, AnyHpChange = false, AnyTarget = false,
                CombatEntryPending = false, RoomClear = false, StageDungeon = true, ManualGate = false,
                Tags = new[] { "hero_outside_room" }, Evidence = "hero outside room at door line"
            });
            // 手动门槛：等待玩家点击要塞 → MANUAL_GATE
            manual.Add(new SimulationObservation
            {
                Tick = tick, SimTime = tick / 20d, Kills = 0, Gold = 0, Chapter = 1, Cleared = 0, Retries = 0, Trades = 0,
                LevelSum = 10, XpHundredths = 0, AnyMovement = false, AnyHit = false, AnyHpChange = false, AnyTarget = false,
                CombatEntryPending = false, RoomClear = true, StageDungeon = false, ManualGate = true, Tags = new[] { "manual_gate" }
            });
            // 合法失败循环：10 秒重试一次 → COMBAT_FAILURE_LOOP
            failing.Add(new SimulationObservation
            {
                Tick = tick, SimTime = tick / 20d, Kills = 0, Gold = 0, Chapter = 1, Cleared = 0, Retries = tick / 200, Trades = 0,
                LevelSum = 10, XpHundredths = 0, AnyMovement = false, AnyHit = false, AnyHpChange = false, AnyTarget = false,
                CombatEntryPending = false, RoomClear = true, StageDungeon = false, ManualGate = false
            });
            // 经验平台：持续击杀但零经验 → XP_RATE_ZERO（不是死锁）
            xpZero.Add(new SimulationObservation
            {
                Tick = tick, SimTime = tick / 20d, Kills = tick / 40, Gold = 0, Chapter = 1, Cleared = 0, Retries = 0, Trades = 0,
                LevelSum = 21, XpHundredths = 0, AnyMovement = true, AnyHit = true, AnyHpChange = true, AnyTarget = true,
                CombatEntryPending = false, RoomClear = false, StageDungeon = true, ManualGate = false,
                Tags = new[] { "xp_rate_zero" }, Evidence = "level gap 5 => 0%"
            });
            // 正常推进：有击杀 → 不得告警
            busy.Add(new SimulationObservation
            {
                Tick = tick, SimTime = tick / 20d, Kills = tick / 20, Gold = tick, Chapter = 1, Cleared = 0, Retries = 0, Trades = 0,
                LevelSum = 21, XpHundredths = tick * 100L, AnyMovement = true, AnyHit = true, AnyHpChange = true, AnyTarget = true,
                CombatEntryPending = false, RoomClear = false, StageDungeon = true, ManualGate = false
            });
        }
        string Classify(List<SimulationObservation> observations)
        {
            var diagnoser = Make();
            foreach (var observation in observations) diagnoser.Observe(observation);
            var alerts = diagnoser.Alerts;
            Need(alerts.Count <= 5, "告警数量没有上限");
            return alerts.Count == 0 ? "none" : alerts[0].Classification;
        }
        Need(Classify(stuck) == AnomalyDiagnoser.SuspectedStuck, "AUD-001 类现场未被判为可疑停滞");
        Need(Classify(manual) == AnomalyDiagnoser.ManualGate, "手动城堡门槛未区分");
        Need(Classify(failing) == AnomalyDiagnoser.CombatFailureLoop, "合法失败重试未区分");
        Need(Classify(xpZero) == AnomalyDiagnoser.XpRateZero, "经验平台未区分");
        Need(Classify(busy) == "none", "正常推进被误报");
        // 同现场不无限重复：停滞观察窗 60 秒、复查 60 秒、上限 2 条
        var limited = new AnomalyDiagnoser(60, 60, 2);
        foreach (var observation in stuck) limited.Observe(observation);
        Need(limited.Alerts.Count <= 2, "重复告警没有限频/限次");
        Need(limited.Alerts.All(a => a.Evidence.Length > 0), "告警缺少有界现场证据");

        Console.WriteLine("PASS: simulation policy loader (v1 compat, v2 explicit, 20 rejection cases, default-off), explicit purchase/formation exactness, single-group-per-tick trace, castle-disabled silence, output completeness, same-seed byte-identical rerun, config/source fingerprints (incl. working-tree unknown), perf counters, sampling-density independence, and anomaly classification (stuck/manual gate/failure loop/xp platform/busy, bounded alerts)");
    }
}
