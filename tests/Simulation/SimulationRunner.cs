using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using IdleCrpg.Core;

/// <summary>
/// P2-1-M2 模拟运行器：只推进正式 GameSession.Tick(0.05f)，只经正式命令执行玩家操作，
/// 逐 Tick 采集事件/金币流水/经验对账，并按分类记录可疑停滞。不写玩家存档。
/// </summary>
public static class SimulationRunner
{
    public static void Run(string root, string path)
    {
        var cfg = SimulationConfig.Load(path);
        var config = LegacySkillConfig.Load(Path.Combine(root, "Idle-CRPG/config"));
        var policy = SimulationPolicy.From(cfg, config);
        string directory = Path.Combine(root, "artifacts/simulation", cfg.ScenarioId + "_" + cfg.Seed);
        var run = new SimulationRun(cfg, policy, config, root, directory);
        var summary = run.Execute();
        Console.WriteLine($"PASS simulation {directory}");
        Console.WriteLine("SIM_HASH " + summary.BusinessHash);
        Console.WriteLine("SIM_LEDGER " + summary.LedgerLine);
        Console.WriteLine("SIM_XP " + summary.XpLine);
        Console.WriteLine("SIM_DIAG " + summary.DiagnosticLine);
        Console.WriteLine("SIM_END " + summary.EndLine);
    }
}

public sealed record SimulationSummary(string BusinessHash, string LedgerLine, string XpLine, string DiagnosticLine, string EndLine);

internal sealed class SimulationRun
{
    private readonly SimulationConfig cfg;
    private readonly SimulationPolicy policy;
    private readonly Config config;
    private readonly GameSession game;
    private readonly SimulationRecorder rec = new();
    private readonly AnomalyDiagnoser diag;
    private readonly string root, directory;

    // 逐 Tick 镜像（只保存判定所需的标量，避免每帧分配大对象）
    private long[] heroXpCumulative = Array.Empty<long>();
    private int[] heroLevels = Array.Empty<int>();
    /// <summary>本步开始时的 HP/坐标，只供诊断比较“是否发生了移动/失血”。</summary>
    private float[] heroHpBefore = Array.Empty<float>();
    private Vector2[] heroPosBefore = Array.Empty<Vector2>();
    private string heroSignature = "";
    private int lastKills, lastRetries, lastTrades, lastChapter = -1;
    private long lastGold, lastHitCount;
    private string lastStage = "", lastDungeon = "", castleAttempt = "";
    private int lastFloor = -1, lastRoom = -1;
    private readonly Dictionary<Actor, bool> monsterAlive = new();
    /// <summary>按对象身份记录已入账战利品；不得因换房清空，否则回访同一房间会重复计入金币。</summary>
    private readonly HashSet<RoomLoot> lootCredited = new();
    /// <summary>按房间对象身份记录事件已结算；房间 ID 在楼层间重复，不能用 int 作键。</summary>
    private readonly Dictionary<Room, bool> eventUsed = new();
    private readonly HashSet<string> castlesAvailable = new(StringComparer.Ordinal);
    private readonly HashSet<string> clearedMirror = new(StringComparer.Ordinal);
    private readonly HashSet<string> pendingMirror = new(StringComparer.Ordinal);

    // 策略执行状态
    private readonly HashSet<string> boughtNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> starPlanLevels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lastReject = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> learnedPlan = new(StringComparer.Ordinal);
    private long nextCastle, nextStar, nextParty, nextTalent;
    private int castleRequests;
    private long starGoldSpent;
    private bool starDone, partySettled, talentSettled;

    // 对账与计数
    private long goldIn, goldOut, goldUnattributed;
    private int xpTicksVerified, xpTicksLevelUp, xpTicksEvent, xpTicksUnexplained;
    private long xpEarnedVerified, xpEarnedLevelUp, xpEarnedUnexplained;
    private int castlesWon, wipes, trades;
    private long[] levelPrefix = Array.Empty<long>();

    public SimulationRun(SimulationConfig cfg, SimulationPolicy policy, Config config, string root, string directory)
    {
        this.cfg = cfg; this.policy = policy; this.config = config; this.root = root; this.directory = directory;
        string[] start = cfg.StartingHeroes is { Count: > 0 } heroes ? heroes.ToArray() : new[] { "warrior" };
        game = GameSession.CreateWorld(config, cfg.Seed, start);
        diag = new AnomalyDiagnoser(policy.StallWindowSeconds, policy.RecheckSeconds, policy.MaxAlerts);
        BuildLevelPrefix();
    }

    private void BuildLevelPrefix()
    {
        var levels = game.Config.HeroLevels.OrderBy(kv => kv.Key).ToArray();
        levelPrefix = new long[levels.Length + 1];
        for (int i = 0; i < levels.Length; i++) levelPrefix[i + 1] = levelPrefix[i] + levels[i].Value.Experience * 100L;
    }

    public SimulationSummary Execute()
    {
        rec.Snapshot(0, cfg.ScenarioId, cfg.Seed, game.ChapterIndex + 1, game.DifficultyIndex, game.World?.CurrentRegion ?? -1,
            game.World?.Target.Dungeon.Id ?? "", game.World?.Stage.ToString() ?? "", HeroText(), game.Gold, game.Kills, game.Retries,
            game.Heroes.Count, game.Stars.Capture().Ranks.Count(), starGoldSpent, string.Join('|', game.World?.PendingCastles ?? Array.Empty<string>()),
            game.World?.WaitingApproval ?? false, game.Floors.Count > 0 ? game.FloorIndex + 1 : 0, game.CurrentRoom, game.Status);
        SyncMirrors();
        long totalTicks = cfg.DurationMinutes * 1200L;
        for (long tick = 1; tick <= totalTicks; tick++)
        {
            CaptureBefore();
            game.Tick(.05f);
            ObserveTick(tick);
            RunPolicy(tick);
            ObserveDiagnostics(tick);
            MaybeSample(tick);
        }
        return Finalize(totalTicks);
    }

    // ---------- 逐 Tick 采集 ----------

    private void CaptureBefore()
    {
        EnsureHeroArrays();
        for (int i = 0; i < game.Heroes.Count; i++)
        {
            var hero = game.Heroes[i];
            heroXpCumulative[i] = CumulativeHundredths(hero);
            heroLevels[i] = hero.Level;
            heroHpBefore[i] = hero.Hp;
            heroPosBefore[i] = hero.Position;
        }
        lastGold = game.Gold; lastKills = game.Kills; lastRetries = game.Retries; lastTrades = game.World?.Trades ?? 0;
        lastHitCount = game.HitCount;
    }

    private void EnsureHeroArrays()
    {
        if (heroXpCumulative.Length == game.Heroes.Count) return;
        int count = game.Heroes.Count;
        heroXpCumulative = new long[count]; heroLevels = new int[count]; heroHpBefore = new float[count]; heroPosBefore = new Vector2[count];
    }

    private long CumulativeHundredths(Actor hero)
    {
        int level = Math.Clamp(hero.Level, 1, levelPrefix.Length - 1);
        return levelPrefix[level - 1] + hero.Experience * 100L + hero.ExperienceFraction;
    }

    private void ObserveTick(long tick)
    {
        var world = game.World;
        // 1) 阶段/地图/楼层/房间切换
        string stage = world?.Stage.ToString() ?? "None";
        string dungeon = game.ActiveDungeon?.Id ?? "";
        int floor = game.Floors.Count > 0 ? game.FloorIndex + 1 : 0;
        int room = game.CurrentRoom;
        if (stage != lastStage || dungeon != lastDungeon || floor != lastFloor || room != lastRoom)
        {
            rec.Event(tick, "state", dungeon, "observed", $"{stage}|floor={floor}|room={room}", game.Status);
            if (dungeon != lastDungeon)
            {
                // 城堡挑战是否失败：离开该地牢且没有记为通关（全灭返村、改目标或章节切换）。
                if (castleAttempt != "" && castleAttempt != dungeon && !game.World!.Progress.Cleared.Contains(castleAttempt))
                {
                    rec.Event(tick, "castle_failure", castleAttempt, "not_cleared", "left_dungeon", $"retries={game.Retries}");
                    castleAttempt = "";
                }
                if (dungeon != "")
                {
                    bool isCastle = game.ActiveDungeon?.Castle == true;
                    rec.Event(tick, isCastle ? "castle_enter" : "dungeon_enter", dungeon, "started", isCastle ? "castle" : "normal",
                        $"chapter={game.ChapterIndex + 1} floors={game.Floors.Count}");
                    if (isCastle) castleAttempt = dungeon;
                }
            }
            // 换房/换层不清空镜像：怪物与战利品都按对象身份登记，回访同一房间不得重复计数。
            lastFloor = floor; lastRoom = room;
        }
        lastStage = stage; lastDungeon = dungeon;

        // 2) 新刷怪事实（只观测当前房间；内容在首次进入时生成）
        if (game.Floors.Count > 0)
        {
            var current = game.Floor.Rooms[game.CurrentRoom];
            if (current.Generated)
            {
                foreach (var monster in current.Monsters)
                    if (!monsterAlive.ContainsKey(monster)) monsterAlive[monster] = monster.Alive;
            }
        }

        // 3) 击杀与经验
        int killsNow = game.Kills;
        var killed = new List<Actor>();
        if (killsNow != lastKills)
        {
            foreach (var pair in monsterAlive.Where(p => p.Value && !p.Key.Alive).Select(p => p.Key).ToArray())
            {
                monsterAlive[pair] = false; killed.Add(pair);
                rec.Event(tick, "kill", pair.SpecId, pair.Boss ? "boss" : "normal", $"lv={pair.Level}",
                    $"reward_xp={pair.RewardXp} attacker=unavailable chapter={game.ChapterIndex + 1}");
            }
        }
        long eventXpBasis = 0;
        if (game.Floors.Count > 0)
        {
            var current = game.Floor.Rooms[game.CurrentRoom];
            if (current.Event == "宝箱" && current.EventUsed)
            {
                bool firstTime = !eventUsed.ContainsKey(current);
                if (firstTime) eventUsed[current] = true;
                if (firstTime)
                {
                    eventXpBasis = 8;
                    rec.Event(tick, "room_event", $"room#{current.Id}", "settled", current.Event, "basis=8");
                }
            }
        }
        ObserveExperience(tick, killed, eventXpBasis);

        // 4) 战利品拾取（金币入账单一来源之一）
        if (game.Floors.Count > 0)
        {
            foreach (var loot in game.Floor.Rooms[game.CurrentRoom].Loot)
            {
                if (!loot.Collected || !lootCredited.Add(loot)) continue;
                goldIn += loot.Gold;
                rec.Gold(tick, "loot_pickup", $"room#{game.CurrentRoom}", loot.Gold, game.Gold, $"items={loot.Items.Sum(i => i.Count)}");
                rec.Event(tick, "loot_pickup", $"room#{game.CurrentRoom}", "collected", $"gold={loot.Gold}", $"items={loot.Items.Sum(i => i.Count)}");
            }
        }

        // 5) 金币来源：售卖（唯一在村庄内发生的金币变化）
        int tradesNow = world?.Trades ?? 0;
        long tickGoldDelta = game.Gold - lastGold;
        if (tradesNow != lastTrades)
        {
            long sold = tickGoldDelta;
            goldIn += sold;
            rec.Gold(tick, "junk_sale", "village", sold, game.Gold, $"trades={tradesNow}");
            rec.Event(tick, "village_trade", world?.Village.Name ?? "village", "sold", $"gold={sold}", $"trades={tradesNow}");
            trades = tradesNow;
        }

        // 6) 失败返村与重试
        if (game.Retries != lastRetries)
        {
            wipes += game.Retries - lastRetries;
            rec.Event(tick, "wipe", dungeon, "retry", "all_dead", $"retries={game.Retries}");
        }

        // 7) 章节与通关
        if (game.ChapterIndex + 1 != lastChapter)
        {
            lastChapter = game.ChapterIndex + 1;
            rec.Event(tick, "chapter_unlock", lastChapter.ToString(), "observed", "", $"difficulty={game.DifficultyIndex}");
        }
        if (world != null)
        {
            foreach (string id in world.Progress.Cleared)
                if (clearedMirror.Add(id))
                {
                    bool castle = game.Config.Dungeons.Single(d => d.Id == id).Castle;
                    rec.Event(tick, castle ? "castle_result" : "dungeon_clear", id, "victory", castle ? "castle" : "normal", $"cleared={clearedMirror.Count}");
                    if (castle)
                    {
                        castlesWon++;
                        if (castleAttempt == id) castleAttempt = "";
                    }
                }
            foreach (string id in world.PendingCastles)
                if (pendingMirror.Add(id)) rec.Event(tick, "castle_queued", id, "pending", "player_request", $"queue={world.PendingCastles.Count}");
            foreach (var castle in game.Config.Dungeons.Where(d => d.Castle && !world.Progress.Cleared.Contains(d.Id) && world.Progress.CastleUnlocked(game.Config, d.Id)))
                if (castlesAvailable.Add(castle.Id)) rec.Event(tick, "castle_available", castle.Id, "unlocked", "formal_gate", $"challenge={game.Config.Challenge(castle.Id, game.DifficultyIndex).Challenge}");
        }

        // 8) 编队生效
        string signature = string.Join('|', game.Heroes.Select(h => h.SpecId));
        if (signature != heroSignature)
        {
            if (heroSignature != "") rec.Event(tick, "formation_applied", signature, "applied", "town_service", $"party={game.Heroes.Count}");
            heroSignature = signature;
        }

        // 9) 位置/伤害/目标事实（供诊断分类，不写文件）
        lastRetries = game.Retries; lastKills = killsNow;
    }

    /// <summary>经验对账：累计经验按“升级门槛前缀 + 当前经验”求增量，逐 Tick 与击杀/事件基础值核对。</summary>
    private void ObserveExperience(long tick, List<Actor> killed, long eventXpBasis)
    {
        if (game.Heroes.Count == 0) return;
        EnsureHeroArrays();
        for (int i = 0; i < game.Heroes.Count; i++)
        {
            var hero = game.Heroes[i];
            long after = CumulativeHundredths(hero);
            long actual = after - heroXpCumulative[i];
            bool levelChanged = hero.Level != heroLevels[i];
            long expected = 0;
            if (!levelChanged && (killed.Count > 0 || eventXpBasis > 0))
            {
                foreach (var monster in killed) expected += (long)monster.RewardXp * game.Config.MonsterExperiencePercent(hero.Level, monster.Level);
                if (eventXpBasis > 0 && game.Floors.Count > 0)
                    expected += eventXpBasis * game.Config.MonsterExperiencePercent(hero.Level, Math.Max(1, game.Floor.Rooms[game.CurrentRoom].SnapshotLevel));
            }
            if (levelChanged)
            {
                xpTicksLevelUp++; xpEarnedLevelUp += actual;
                rec.Event(tick, "level_up", hero.SpecId, hero.Level.ToString(CultureInfo.InvariantCulture), $"xp_after={hero.Experience}+{hero.ExperienceFraction}/100", $"hundredths_delta={actual} expected_before_level={expected}");
            }
            else if (actual != 0 || expected != 0)
            {
                if (actual == expected)
                {
                    xpTicksVerified++; xpEarnedVerified += actual;
                    if (eventXpBasis > 0) xpTicksEvent++;
                }
                else
                {
                    xpTicksUnexplained++; xpEarnedUnexplained += actual;
                    rec.Event(tick, "xp_mismatch", hero.SpecId, "unexplained", $"actual={actual} expected={expected}", $"lv={hero.Level} kills={killed.Count} event_basis={eventXpBasis}");
                }
            }
            heroXpCumulative[i] = after; heroLevels[i] = hero.Level;
        }
    }

    // ---------- 玩家策略（每次最多执行一组，避免同 Tick 多动作） ----------

    private void RunPolicy(long tick)
    {
        // 固定优先级：城堡 → 星图 → 编队 → 天赋；同一 Tick 只允许一组动作。
        if (Due(ref nextCastle, tick, policy.CastleIntervalSeconds, policy.CastleEnabled)) { ExecCastle(tick); return; }
        if (Due(ref nextStar, tick, policy.StarIntervalSeconds, policy.StarMode != "none")) { ExecStarmap(tick); return; }
        if (Due(ref nextParty, tick, policy.PartyIntervalSeconds, policy.PartyMode != "none")) { ExecParty(tick); return; }
        if (Due(ref nextTalent, tick, policy.TalentIntervalSeconds, policy.TalentMode != "none")) ExecTalents(tick);
    }

    private static bool Due(ref long next, long tick, int intervalSeconds, bool enabled)
    {
        if (!enabled) return false;
        if (next == 0) next = tick;
        if (next > tick) return false;
        next = tick + intervalSeconds * 20L;
        return true;
    }

    private void ExecCastle(long tick)
    {
        var world = game.World;
        if (world == null) return;
        if (policy.CastleMaxRequests > 0 && castleRequests >= policy.CastleMaxRequests) return;
        var candidate = game.Config.Dungeons
            .Where(d => d.Castle && !world.Progress.Cleared.Contains(d.Id) && world.Progress.CastleUnlocked(game.Config, d.Id)
                        && !world.PendingCastles.Contains(d.Id) && world.Target.Dungeon.Id != d.Id)
            .OrderBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
        if (candidate == null)
        {
            if (lastReject.GetValueOrDefault("castle") != "no_eligible_castle")
            {
                lastReject["castle"] = "no_eligible_castle";
                rec.Event(tick, "castle_check", "", "blocked", "no_eligible_castle",
                    $"waiting_approval={world.WaitingApproval} pending={world.PendingCastles.Count} target={world.Target.Dungeon.Id}");
            }
            return;
        }
        bool accepted = game.RequestCastleChallenge(candidate.Id);
        if (accepted) castleRequests++;
        lastReject["castle"] = accepted ? "" : "api_rejected";
        rec.Event(tick, "castle_click", candidate.Id, accepted ? "accepted" : "rejected", accepted ? "player_request" : "api_rejected",
            $"challenge={game.Config.Challenge(candidate.Id, game.DifficultyIndex).Challenge} requests={castleRequests}");
    }

    private static readonly string[] LegacyStarOrder = { "s_party_1", "s_party_2", "s_party_3", "s_party_4", "s_skill_1", "s_skill_2", "s_skill_3", "s_skill_4", "s_root", "s_hp", "s_atk", "s_hp_01", "s_atk_01", "s_hp_02", "s_atk_02" };

    private void ExecStarmap(long tick)
    {
        if (policy.StarMode == "legacy")
        {
            foreach (string id in LegacyStarOrder)
            {
                if (boughtNodes.Contains(id)) continue;
                if (!game.Config.Stars.Nodes.ContainsKey(id)) continue;
                long cost = game.Config.Stars.Nodes[id].Costs[game.Stars.Level(id)];
                if (!game.BuyStar(id, out string reason)) { RecordReject(tick, "star", id, reason); continue; }
                boughtNodes.Add(id); starGoldSpent += cost; goldOut += cost;
                rec.Gold(tick, "starmap_purchase", id, -cost, game.Gold, $"level={game.Stars.Level(id)}");
                rec.Event(tick, "star_purchase", id, "accepted", "formal_command", $"cost={cost} level={game.Stars.Level(id)}");
            }
            // 历史列表内全部点亮才算完成；曾因金币不足的节点仍会按周期重试，不提前锁死。
            if (!starDone && LegacyStarOrder.All(id => !game.Config.Stars.Nodes.ContainsKey(id) || game.Stars.Level(id) >= 1))
            {
                starDone = true;
                rec.Event(tick, "star_done", "", "observed", "legacy_list_exhausted", $"nodes={boughtNodes.Count} gold_spent={starGoldSpent}");
            }
            return;
        }
        // explicit：按配置顺序与等级上限购买，前置/金币由正式接口拒绝并记录原因
        foreach (var step in policy.StarNodes)
        {
            starPlanLevels.TryAdd(step.Id, 0);
            long guard = 0;
            while (game.Stars.Level(step.Id) < step.MaxLevel && guard++ < 64)
            {
                long cost = game.Config.Stars.Nodes[step.Id].Costs[game.Stars.Level(step.Id)];
                if (!game.BuyStar(step.Id, out string reason)) { RecordReject(tick, "star", step.Id, reason); break; }
                starPlanLevels[step.Id] = game.Stars.Level(step.Id);
                starGoldSpent += cost; goldOut += cost;
                rec.Gold(tick, "starmap_purchase", step.Id, -cost, game.Gold, $"level={game.Stars.Level(step.Id)}");
                rec.Event(tick, "star_purchase", step.Id, "accepted", "formal_command", $"cost={cost} level={game.Stars.Level(step.Id)}/{step.MaxLevel}");
            }
        }
        if (!starDone && policy.StarNodes.All(s => game.Stars.Level(s.Id) >= s.MaxLevel))
        {
            starDone = true;
            rec.Event(tick, "star_done", "", "observed", "plan_completed", $"gold_spent={starGoldSpent}");
        }
    }

    private void RecordReject(long tick, string kind, string target, string reason)
    {
        string key = kind + ":" + target;
        if (lastReject.GetValueOrDefault(key) == reason) return;
        lastReject[key] = reason;
        rec.Event(tick, kind + "_reject", target, "rejected", reason, "");
    }

    private void ExecParty(long tick)
    {
        if (partySettled) return;
        int capacity = game.PartyCapacity;
        string[] candidates = policy.PartyMode == "explicit"
            ? policy.PartyRoster.ToArray()
            : new[] { "warrior", "ranger", "mage" };
        // explicit 的目标人数是策略意图；席位不足时只按当前席位编入并继续等待扩编，不能提前判定“已就绪”。
        int desired = policy.PartyMode == "explicit" ? policy.PartyTargetSize : candidates.Length;
        int target = Math.Min(desired, capacity);
        var requested = candidates.Take(target).ToArray();
        foreach (string id in candidates)
        {
            bool active = game.Heroes.Any(h => h.SpecId == id) || game.ReserveHeroes.Any(h => h.SpecId == id);
            if (active) continue;
            if (!game.HeroUnlocked(id))
            {
                string key = "party:" + id;
                if (lastReject.GetValueOrDefault(key) != "locked") { lastReject[key] = "locked"; rec.Event(tick, "party_skip", id, "not_added", "locked", $"kills={game.Kills} need={game.Config.HeroUnlockKills.GetValueOrDefault(id)}"); }
                continue;
            }
            if (!requested.Contains(id))
            {
                lastReject["party:" + id] = "over_capacity";
                rec.Event(tick, "party_skip", id, "not_added", "over_capacity", $"capacity={capacity} target={target}");
            }
        }
        if (requested.SequenceEqual(game.Heroes.Select(h => h.SpecId)))
        {
            if (requested.Length >= desired)
            {
                partySettled = true;
                rec.Event(tick, "party_settled", string.Join('|', requested), "observed", "already_active", $"capacity={capacity}");
            }
            else
            {
                string key = "party:capacity";
                if (lastReject.GetValueOrDefault(key) != $"{capacity}/{desired}")
                {
                    lastReject[key] = $"{capacity}/{desired}";
                    rec.Event(tick, "party_skip", "", "waiting", "capacity_limited", $"capacity={capacity} desired={desired}");
                }
            }
            return;
        }
        bool ok = game.RequestFormation(requested, out string reason);
        if (ok)
        {
            // 只有达到策略目标人数才算就绪；席位不足时先按当前席位编入，扩编后继续补人。
            if (requested.Length >= desired) partySettled = true;
            rec.Event(tick, "formation_request", string.Join('|', requested), "accepted", "formal_command", $"capacity={capacity} target={target} desired={desired}");
        }
        else RecordReject(tick, "party", string.Join('|', requested), reason);
    }

    private Actor? HeroById(string id) => game.Heroes.FirstOrDefault(h => h.SpecId == id);

    private void ExecTalents(long tick)
    {
        if (talentSettled) return;
        if (policy.TalentMode == "legacy")
        {
            bool progressed = false;
            foreach (var hero in game.Heroes.ToArray())
            {
                foreach (var node in game.Config.Talents.Nodes.Values.Where(n => n.Hero == hero.SpecId).OrderBy(n => n.Row).ThenBy(n => n.Col))
                {
                    if (!game.LearnTalent(hero.SpecId, node.Id, out _)) continue;
                    progressed = true;
                    rec.Event(tick, "talent_learn", hero.SpecId, node.Id, "formal_command", $"rank={game.TalentLevel(hero, node.Id)}");
                }
                int slots = game.ActiveSkillSlots;
                var actives = hero.Talents.Keys.Where(id => game.Config.Talents.Nodes.TryGetValue(id, out var n) && n.Kind == "active")
                    .OrderBy(id => game.Config.Talents.Nodes[id].Row).ThenBy(id => game.Config.Talents.Nodes[id].Col).Take(Math.Max(1, slots)).ToArray();
                if (actives.Length > 0 && !actives.SequenceEqual(hero.SkillIds))
                {
                    if (game.SetTalentLoadout(hero.SpecId, actives, out string reason))
                    {
                        progressed = true;
                        rec.Event(tick, "skill_equip", hero.SpecId, string.Join('|', actives), "formal_command", $"slots={slots}");
                    }
                    else RecordReject(tick, "skill", hero.SpecId, reason);
                }
            }
            // 历史策略按周期重扫：升级会带来新天赋点，不能因为某一轮“无可学节点”就永久停手。
            if (!progressed && lastReject.GetValueOrDefault("talent:legacy") == "") lastReject["talent:legacy"] = "no_more_legal_nodes_now";
            if (progressed) lastReject["talent:legacy"] = "";
            return;
        }
        // explicit：只执行配置中明确列出的学习与装配
        bool blocked = false;
        foreach (var step in policy.TalentLearn)
        {
            var hero = HeroById(step.Hero);
            if (hero == null) { blocked = true; RecordReject(tick, "talent", step.Node, "hero_not_deployed"); continue; }
            while (game.TalentLevel(hero, step.Node) < step.Ranks)
            {
                if (!game.LearnTalent(step.Hero, step.Node, out string reason)) { blocked = true; RecordReject(tick, "talent", step.Node, reason); break; }
                rec.Event(tick, "talent_learn", step.Hero, step.Node, "formal_command", $"rank={game.TalentLevel(hero, step.Node)}/{step.Ranks}");
            }
        }
        foreach (var step in policy.SkillEquip)
        {
            var hero = HeroById(step.Hero);
            if (hero == null) { blocked = true; RecordReject(tick, "skill", step.Hero, "hero_not_deployed"); continue; }
            string[] ids = step.Skills!.ToArray();
            if (ids.SequenceEqual(hero.SkillIds)) continue;
            if (game.SetTalentLoadout(step.Hero, ids, out string reason))
                rec.Event(tick, "skill_equip", step.Hero, string.Join('|', ids), "formal_command", $"slots={game.ActiveSkillSlots}");
            else { blocked = true; RecordReject(tick, "skill", step.Hero, reason); }
        }
        if (!blocked)
        {
            talentSettled = true;
            rec.Event(tick, "talent_settled", "", "observed", "plan_completed", "");
        }
    }

    // ---------- 诊断 ----------

    private void ObserveDiagnostics(long tick)
    {
        bool needEvidence = tick - diag.LastProgressTick >= diag.WindowTicks && tick % 20 == 0 && diag.Alerts.Count < policy.MaxAlerts;
        var observation = BuildObservation(tick, needEvidence);
        foreach (var alert in diag.Observe(observation)) rec.Diagnostic(alert);
    }

    private SimulationObservation BuildObservation(long tick, bool withEvidence)
    {
        var world = game.World;
        bool moved = false, hpChanged = false;
        for (int i = 0; i < Math.Min(game.Heroes.Count, heroPosBefore.Length); i++)
        {
            if (Vector2.Distance(game.Heroes[i].Position, heroPosBefore[i]) > 0.0005f) moved = true;
            if (Math.Abs(game.Heroes[i].Hp - heroHpBefore[i]) > 0.0001f) hpChanged = true;
        }
        bool stageDungeon = world?.Stage == CampaignStage.Dungeon;
        bool roomClear = game.Floors.Count > 0 && game.Floor.Rooms[game.CurrentRoom].Clear;
        bool manualGate = world != null && (world.WaitingApproval ||
            (!policy.CastleEnabled && game.Config.Dungeons.Any(d => d.Castle && !world.Progress.Cleared.Contains(d.Id) && world.Progress.CastleUnlocked(game.Config, d.Id))));
        var tags = new List<string>();
        if (ManualGateActive(world)) tags.Add("manual_gate");
        if (XpRateZero()) tags.Add("xp_rate_zero");
        if (game.Floors.Count > 0)
        {
            var hero = game.Heroes.FirstOrDefault(h => h.Alive);
            if (hero != null && !SpatialTolerance.InsideRoom(game.Floor.Rooms[game.CurrentRoom], hero.Position)) tags.Add("hero_outside_room");
        }
        long xp = 0; foreach (var hero in game.Heroes) xp += CumulativeHundredths(hero);
        return new SimulationObservation
        {
            Tick = tick,
            SimTime = tick / 20d,
            Kills = game.Kills,
            Gold = game.Gold,
            Chapter = game.ChapterIndex + 1,
            Cleared = world?.Progress.Cleared.Count ?? 0,
            Retries = game.Retries,
            Trades = world?.Trades ?? 0,
            LevelSum = game.Heroes.Sum(h => h.Level),
            XpHundredths = xp,
            AnyMovement = moved,
            AnyHit = game.HitCount != lastHitCount,
            AnyHpChange = hpChanged,
            AnyTarget = game.MonsterTargets.Any(),
            CombatEntryPending = game.CombatEntryRemaining > 0,
            RoomClear = roomClear,
            StageDungeon = stageDungeon,
            ManualGate = manualGate,
            Tags = tags,
            Evidence = withEvidence ? Evidence(tick) : ""
        };
    }

    private bool ManualGateActive(WorldCampaign? world)
        => world != null && (world.WaitingApproval || (!policy.CastleEnabled && game.Config.Dungeons.Any(d => d.Castle && !world.Progress.Cleared.Contains(d.Id) && world.Progress.CastleUnlocked(game.Config, d.Id))));

    /// <summary>当前存活怪物对全部上阵英雄的等级差倍率是否都为 0（经验平台事实，不是死锁）。</summary>
    private bool XpRateZero()
    {
        if (game.Floors.Count == 0 || game.Heroes.Count == 0) return false;
        var monsters = game.Floor.Rooms[game.CurrentRoom].Monsters.Where(m => m.Alive).ToArray();
        if (monsters.Length == 0) return false;
        return game.Heroes.All(h => monsters.All(m => game.Config.MonsterExperiencePercent(h.Level, m.Level) == 0));
    }

    /// <summary>有界现场：固定上限行数，只放当前事实，不做推断。</summary>
    private string Evidence(long tick)
    {
        var lines = new List<string>();
        var world = game.World;
        lines.Add($"t={tick / 20d:0.0}s stage={world?.Stage} target={world?.Target.Dungeon.Id} farming={world?.Target.Farming} region={world?.CurrentRegion}");
        lines.Add($"chapter={game.ChapterIndex + 1} difficulty={game.DifficultyIndex} dungeon={game.ActiveDungeon?.Id} floor={(game.Floors.Count > 0 ? game.FloorIndex + 1 : 0)} room={game.CurrentRoom} status={game.Status}");
        lines.Add($"gold={game.Gold} kills={game.Kills} retries={game.Retries} trades={world?.Trades} pending={string.Join('|', world?.PendingCastles ?? Array.Empty<string>())} waiting={world?.WaitingApproval}");
        foreach (var hero in game.Heroes.Take(5))
            lines.Add($"hero {hero.SpecId} L{hero.Level} hp={hero.Hp:0}/{hero.MaxHp:0} pos=({hero.Position.X:0.00},{hero.Position.Y:0.00}) alive={hero.Alive} xp={hero.Experience}+{hero.ExperienceFraction}/100");
        if (game.Floors.Count > 0)
        {
            var room = game.Floor.Rooms[game.CurrentRoom];
            lines.Add($"room generated={room.Generated} clear={room.Clear} event={room.Event} half=({room.HalfWidth:0.0},{room.HalfHeight:0.0}) center=({room.Center.X:0.0},{room.Center.Y:0.0}) loot_pending={room.Loot.Count(l => !l.Collected)}");
            lines.Add("monsters " + string.Join("; ", room.Monsters.Where(m => m.Alive).Take(6).Select(m => $"{m.SpecId}L{m.Level}({m.Position.X:0.0},{m.Position.Y:0.0})hp={m.Hp:0}")));
            lines.Add("targets " + string.Join(";", game.MonsterTargets.Select(t => $"#{t.Owner}->{t.Target}:{t.Kind}")));
            bool outside = game.Heroes.Any(h => h.Alive && !SpatialTolerance.InsideRoom(room, h.Position));
            lines.Add($"spatial room_boundary={SpatialTolerance.RoomBoundary} move_arrival={SpatialTolerance.MoveArrival} hero_outside_room={outside} combat_entry_remaining={game.CombatEntryRemaining:0.00}");
            lines.AddRange(game.EntryDiagnostics().Take(4));
        }
        lines.Add($"revive target={game.ReviveTarget?.SpecId ?? "-"} rescuer={game.Rescuer?.SpecId ?? "-"} elapsed={game.ReviveElapsed:0.0}");
        return string.Join(" || ", lines);
    }

    // ---------- 采样与收尾 ----------

    private void MaybeSample(long tick)
    {
        if (tick % 1200 != 0) return;
        int minute = (int)(tick / 1200);
        bool dense = cfg.DenseEveryMinutes > 0 && minute <= cfg.DenseSampleMinutes && minute % cfg.DenseEveryMinutes == 0;
        bool sparse = minute % cfg.SampleIntervalMinutes == 0;
        if (!dense && !sparse) return;
        rec.Snapshot(tick, cfg.ScenarioId, cfg.Seed, game.ChapterIndex + 1, game.DifficultyIndex, game.World?.CurrentRegion ?? -1,
            game.World?.Target.Dungeon.Id ?? "", game.World?.Stage.ToString() ?? "", HeroText(), game.Gold, game.Kills, game.Retries,
            game.Heroes.Count, game.Stars.Capture().Ranks.Count(), starGoldSpent, string.Join('|', game.World?.PendingCastles ?? Array.Empty<string>()),
            game.World?.WaitingApproval ?? false, game.Floors.Count > 0 ? game.FloorIndex + 1 : 0, game.CurrentRoom, game.Status);
    }

    private string HeroText() => string.Join('|', game.Heroes.Select(h => $"{h.SpecId}:{h.Level}:{h.Experience}:{h.ExperienceFraction}"));

    private void SyncMirrors()
    {
        lastGold = game.Gold; lastKills = game.Kills; lastRetries = game.Retries; lastTrades = game.World?.Trades ?? 0;
        lastHitCount = game.HitCount; lastStage = game.World?.Stage.ToString() ?? ""; lastDungeon = game.ActiveDungeon?.Id ?? "";
        lastFloor = game.Floors.Count > 0 ? game.FloorIndex + 1 : 0; lastRoom = game.CurrentRoom;
        lastChapter = game.ChapterIndex + 1;
        heroSignature = string.Join('|', game.Heroes.Select(h => h.SpecId));
        EnsureHeroArrays();
        for (int i = 0; i < game.Heroes.Count; i++)
        {
            heroXpCumulative[i] = CumulativeHundredths(game.Heroes[i]); heroLevels[i] = game.Heroes[i].Level;
            heroHpBefore[i] = game.Heroes[i].Hp; heroPosBefore[i] = game.Heroes[i].Position;
        }
        if (game.World != null) foreach (string id in game.World.Progress.Cleared) clearedMirror.Add(id);
    }

    private SimulationSummary Finalize(long totalTicks)
    {
        long expected = goldIn - goldOut;
        long diff = game.Gold - expected;
        goldUnattributed = diff;
        // 收尾快照使用与常规采样相同的格式，保证尾部状态可复核
        rec.Snapshot(totalTicks, cfg.ScenarioId, cfg.Seed, game.ChapterIndex + 1, game.DifficultyIndex, game.World?.CurrentRegion ?? -1,
            game.World?.Target.Dungeon.Id ?? "", game.World?.Stage.ToString() ?? "", HeroText(), game.Gold, game.Kills, game.Retries,
            game.Heroes.Count, game.Stars.Capture().Ranks.Count(), starGoldSpent, string.Join('|', game.World?.PendingCastles ?? Array.Empty<string>()),
            game.World?.WaitingApproval ?? false, game.Floors.Count > 0 ? game.FloorIndex + 1 : 0, game.CurrentRoom, game.Status);
        string hash = rec.BusinessHash();
        rec.WriteAll(directory);
        File.WriteAllText(Path.Combine(directory, "metadata.json"), Metadata(hash, totalTicks), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "fields.md"), Fields(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "diagnostics.md"), DiagnosticsMarkdown(), new UTF8Encoding(false));

        var diagnosticCounts = diag.Alerts.GroupBy(a => a.Classification).ToDictionary(g => g.Key, g => g.Count());
        string ledgerLine = $"in={goldIn} out={goldOut} expected={expected} actual={game.Gold} diff={diff} unattributed={goldUnattributed}";
        string xpLine = $"verified_ticks={xpTicksVerified} level_up_ticks={xpTicksLevelUp} event_ticks={xpTicksEvent} unexplained_ticks={xpTicksUnexplained} verified_hundredths={xpEarnedVerified} unexplained_hundredths={xpEarnedUnexplained}";
        string diagLine = diag.Alerts.Count == 0 ? "none" : string.Join(",", diagnosticCounts.Select(kv => $"{kv.Key}={kv.Value}"));
        string endLine = $"chapter={game.ChapterIndex + 1} stage={game.World?.Stage} gold={game.Gold} kills={game.Kills} retries={game.Retries} cleared={game.World?.Progress.Cleared.Count ?? 0} castles_won={castlesWon} party={game.Heroes.Count} levels={string.Join('/', game.Heroes.Select(h => h.Level))}";
        return new SimulationSummary(hash, ledgerLine, xpLine, diagLine, endLine);
    }

    private string Metadata(string hash, long totalTicks)
    {
        var payload = new
        {
            schema = "p2-1-m2/1",
            scenario = cfg.ScenarioId,
            scenario_note = cfg.ScenarioNote,
            config_schema_version = cfg.SchemaVersion,
            seed = cfg.Seed,
            duration_minutes = cfg.DurationMinutes,
            sample_interval_minutes = cfg.SampleIntervalMinutes,
            dense_sample_minutes = cfg.DenseSampleMinutes,
            dense_every_minutes = cfg.DenseEveryMinutes,
            policy = policy.Summarize(),
            config_root = "Idle-CRPG/config",
            config_fingerprint_sha256 = SimulationRecorder.ConfigFingerprint(Path.Combine(root, "Idle-CRPG/config")),
            code_version = SimulationRecorder.CodeVersion(root),
            created_at = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
            sim_end_tick = totalTicks,
            sim_end_seconds = totalTicks / 20.0,
            business_hash = hash,
            counters = new
            {
                gold_in = goldIn,
                gold_out = goldOut,
                gold_expected = goldIn - goldOut,
                gold_actual = game.Gold,
                gold_diff = game.Gold - (goldIn - goldOut),
                star_gold_spent = starGoldSpent,
                castle_requests = castleRequests,
                castles_won = castlesWon,
                wipes = wipes,
                trades = trades,
                kills = game.Kills,
                xp_verified_ticks = xpTicksVerified,
                xp_level_up_ticks = xpTicksLevelUp,
                xp_event_ticks = xpTicksEvent,
                xp_unexplained_ticks = xpTicksUnexplained,
                xp_verified_hundredths = xpEarnedVerified,
                xp_unexplained_hundredths = xpEarnedUnexplained,
                diagnostics = diag.Alerts.GroupBy(a => a.Classification).ToDictionary(g => g.Key, g => g.Count())
            },
            unavailable = new[]
            {
                "kill_attacker_id（结算只记录死亡事实，攻击者不可观测）",
                "per_skill_damage（无逐技能伤害来源）",
                "incoming_damage_breakdown（仅能由英雄 HP 变化推导本步失血）",
                "castle_failure_reason（失败只表现为全灭返村，具体致死序列不可观测）",
                "xp_source_monster_per_award（同一 Tick 多击杀且跨级时无法逐笔归因）"
            }
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Fields() => string.Join("\n", new[]
    {
        "# 模拟输出字段说明（P2-1-M2）",
        "",
        "时间列：`sim_time` 为完成 Tick × 0.05 秒（20Hz），`tick` 为原始逻辑步；事件时间一律来自 Tick，不用外层分钟。",
        "",
        "## snapshots.csv",
        "| 字段 | 含义 | 单位 |",
        "|---|---|---|",
        "| chapter | 当前章节（1 起） | - |",
        "| difficulty | 难度索引（0 普通/1 噩梦/2 地狱） | - |",
        "| region | 当前地块编号 | - |",
        "| map | 当前目标地牢 ID | - |",
        "| stage | 世界阶段（TravelDungeon/Dungeon/TravelVillage/Village） | - |",
        "| heroes | `职业:等级:整数经验:分数经验` 以 \\| 分隔 | - |",
        "| gold / star_gold | 金币余额 / 已花星图金币 | 金币 |",
        "| kills / retries / party / star_nodes | 累计击杀 / 全灭返村次数 / 上阵人数 / 已点亮星图节点数 | - |",
        "| pending_castles / waiting_approval | 排队城堡 / 等待玩家点击要塞 | - |",
        "| floor / room | 当前楼层（1 起）/ 房间序号 | - |",
        "",
        "## events.csv",
        "`event_type`：state、dungeon_enter/clear、castle_available/click/queued/enter/result/failure、star_purchase/reject/done、talent_learn/reject、skill_equip/reject、formation_request/applied、party_skip、kill、level_up、xp_mismatch、loot_pickup、village_trade、wipe、chapter_unlock、room_event。",
        "`result`：observed/accepted/rejected/blocked/started/collected/sold/retry/victory/not_cleared/locked 等运行事实。",
        "`reason`：官方接口返回的拒绝理由或观察口径（formal_command、formal_gate、player_request 等）。",
        "",
        "## ledger.csv",
        "`source`：loot_pickup（拾取）、junk_sale（村庄售卖）、starmap_purchase（星图购买）、talent_respec（洗点，未使用）。",
        "`amount` 为带符号金币数；`balance` 为该笔发生后的余额。对账：Σamount = 期末余额，差额必须为 0。",
        "",
        "## diagnostics.csv",
        "`classification`：MANUAL_GATE、XP_RATE_ZERO、COMBAT_FAILURE_LOOP、SUSPECTED_STUCK、RUNNING、UNDETERMINED。",
        "`evidence` 为有界现场（≤ 固定行数，` || ` 分隔），只含当前状态，不含推断。"
    });

    private string DiagnosticsMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 诊断记录");
        sb.AppendLine();
        sb.AppendLine($"- 观察窗：{policy.StallWindowSeconds} 游戏秒；复查间隔 {policy.RecheckSeconds} 秒；告警上限 {policy.MaxAlerts}。");
        sb.AppendLine("- `MANUAL_GATE` 与 `COMBAT_FAILURE_LOOP` 属合法门槛/合法失败；`SUSPECTED_STUCK` 才表示无正当理由的不进展。");
        sb.AppendLine();
        if (diag.Alerts.Count == 0) { sb.AppendLine("本轮未触发任何告警。"); return sb.ToString(); }
        foreach (var alert in diag.Alerts)
        {
            sb.AppendLine($"## {alert.Classification} @ {alert.SimTime:0.0}s (tick {alert.Tick})");
            sb.AppendLine();
            sb.AppendLine($"- 原因：{alert.Reason}");
            sb.AppendLine($"- 标签：{(alert.Tags.Count == 0 ? "无" : string.Join(" / ", alert.Tags))}");
            sb.AppendLine();
            sb.AppendLine("```text");
            foreach (string line in alert.Evidence.Split(" || ")) sb.AppendLine(line);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
