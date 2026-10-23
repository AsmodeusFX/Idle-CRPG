using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>一次逐 Tick 观测：只放可判定的布尔/计数事实，不含推测。</summary>
public sealed record SimulationObservation
{
    public long Tick { get; init; }
    public double SimTime { get; init; }
    public int Kills { get; init; }
    public long Gold { get; init; }
    public int Chapter { get; init; }
    public int Cleared { get; init; }
    public int Retries { get; init; }
    public int Trades { get; init; }
    public int LevelSum { get; init; }
    public long XpHundredths { get; init; }
    public bool AnyMovement { get; init; }
    public bool AnyHit { get; init; }
    public bool AnyHpChange { get; init; }
    public bool AnyTarget { get; init; }
    public bool CombatEntryPending { get; init; }
    public bool RoomClear { get; init; }
    public bool StageDungeon { get; init; }
    /// <summary>正常手动门槛：等待玩家点击要塞，或策略明确禁止点击但存在已解锁要塞。</summary>
    public bool ManualGate { get; init; }
    /// <summary>观察到的附加事实标签（如 xp_rate_zero、hero_outside_room）。</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    /// <summary>有界现场文本；由调用方限制行数。</summary>
    public string Evidence { get; init; } = "";
}

/// <summary>
/// 可疑停滞观察器：不以“多久没击杀”直接定罪，而是结合行动/位移/伤害/门槛/阶段做分级。
/// 只读测试侧实现，不进入正式 Core、不写玩家存档。
/// </summary>
public sealed class AnomalyDiagnoser
{
    public const string ManualGate = "MANUAL_GATE";
    public const string XpRateZero = "XP_RATE_ZERO";
    public const string CombatFailureLoop = "COMBAT_FAILURE_LOOP";
    public const string SuspectedStuck = "SUSPECTED_STUCK";
    public const string Running = "RUNNING";
    public const string Undetermined = "UNDETERMINED";

    private readonly long windowTicks, recheckTicks;
    private readonly int maxAlerts;
    private readonly List<DiagnosticAlert> alerts = new();
    private readonly List<DiagnosticAlert> xpAlerts = new();
    private readonly Dictionary<string, long> lastAlertTick = new(StringComparer.Ordinal);
    private readonly HashSet<string> xpZeroReported = new(StringComparer.Ordinal);

    private long lastProgressTick;
    private long progress = long.MinValue;
    private long lastXpChangeTick;
    private long lastKillTick;
    private int lastKillsSeen;
    private long xpHundredths = long.MinValue;
    private int killsAtProgress;
    private int retriesAtProgress;
    private bool moved, hit, hpChanged, hadTarget, entryPending;

    public AnomalyDiagnoser(int stallWindowSeconds, int recheckSeconds, int maxAlerts)
    {
        windowTicks = Math.Max(1, stallWindowSeconds) * 20L;
        recheckTicks = Math.Max(1, recheckSeconds) * 20L;
        this.maxAlerts = Math.Max(1, maxAlerts);
    }

    /// <summary>全部告警：停滞分级在前，经验平台提示在后；两者预算互不占用。</summary>
    public IReadOnlyList<DiagnosticAlert> Alerts => alerts.Concat(xpAlerts).ToArray();
    /// <summary>最近一次“有进展”的 Tick；调用方据此决定是否构建现场证据。</summary>
    public long LastProgressTick => lastProgressTick;
    public long WindowTicks => windowTicks;

    /// <summary>每 Tick 调用一次；返回本次新增的告警（可能为空）。</summary>
    public IReadOnlyList<DiagnosticAlert> Observe(SimulationObservation o)
    {
        if (lastProgressTick == 0) { lastProgressTick = o.Tick; lastXpChangeTick = o.Tick; }
        var produced = new List<DiagnosticAlert>();
        // 失败返村次数不计入“有进展”：重试本身不是推进，否则合法失败循环会被当成持续推进而漏报。
        long signature = ((long)o.Kills * 1_000_003) ^ ((long)o.Gold * 31) ^ ((long)o.Chapter << 40) ^ ((long)o.Cleared << 32) ^ ((long)o.Trades << 12);
        if (signature != progress)
        {
            progress = signature;
            lastProgressTick = o.Tick;
            killsAtProgress = o.Kills;
            retriesAtProgress = o.Retries;
            moved = hit = hpChanged = hadTarget = entryPending = false;
        }
        moved |= o.AnyMovement; hit |= o.AnyHit; hpChanged |= o.AnyHpChange; hadTarget |= o.AnyTarget; entryPending |= o.CombatEntryPending;
        if (o.XpHundredths != xpHundredths) { xpHundredths = o.XpHundredths; lastXpChangeTick = o.Tick; }
        if (o.Kills > lastKillsSeen) { lastKillsSeen = o.Kills; lastKillTick = o.Tick; }

        // 经验平台：观察窗内确有击杀、但经验一个增量都没有，属于规则事实（等级差 0%），不是死锁。
        bool killing = lastKillTick > lastXpChangeTick;
        if (killing && o.Tick - lastXpChangeTick >= windowTicks && o.Tags.Contains("xp_rate_zero"))
        {
            string key = $"xp:{o.Chapter}:{o.LevelSum}";
            // 经验平台是提示而不是停滞证据，单独限额，避免占满停滞告警预算。
            if (xpZeroReported.Add(key) && xpAlerts.Count < maxAlerts)
            {
                var alert = new DiagnosticAlert(o.SimTime, o.Tick, XpRateZero, "持续击杀但经验增量为 0（等级差倍率为 0）", o.Tags, o.Evidence);
                xpAlerts.Add(alert); produced.Add(alert);
            }
        }

        if (o.Tick - lastProgressTick < windowTicks) return produced;

        // 命中率限制：同一分类按 recheck 间隔复查，不无限重复打印。
        string classification = Classify(o);
        if (classification == Running) return produced;   // 有行动但无收益：记录为状态，不发告警
        if (lastAlertTick.TryGetValue(classification, out long previous) && o.Tick - previous < recheckTicks) return produced;
        if (alerts.Count >= maxAlerts) return produced;

        string reason = classification switch
        {
            ManualGate => "存在已解锁要塞但未由玩家点击，属于正常手动门槛",
            CombatFailureLoop => "持续失败返村重试，属于合法战斗失败",
            SuspectedStuck => "无位移、无命中、无目标、无入场通行，且房间未清理",
            Undetermined => "长时间无进展且信号互相矛盾（非战斗阶段或状态不明）",
            _ => "未知"
        };
        var result = new DiagnosticAlert(o.SimTime, o.Tick, classification, reason, o.Tags, o.Evidence);
        alerts.Add(result); produced.Add(result); lastAlertTick[classification] = o.Tick;
        // 一次停滞只认一次根因，避免同一现场反复计入不同分类。
        lastProgressTick = o.Tick;
        return produced;
    }

    /// <summary>分级：先排除合法门槛与合法失败，再只在“完全没有行动”时判定可疑停滞。</summary>
    private string Classify(SimulationObservation o)
    {
        if (o.ManualGate) return ManualGate;
        if (o.Retries - retriesAtProgress >= 2) return CombatFailureLoop;
        bool noAction = !moved && !hit && !hpChanged && !hadTarget && !entryPending;
        if (noAction && o.StageDungeon && !o.RoomClear) return SuspectedStuck;
        if (noAction) return Undetermined;
        return Running;
    }
}

public sealed record DiagnosticAlert(double SimTime, long Tick, string Classification, string Reason, IReadOnlyList<string> Tags, string Evidence);
