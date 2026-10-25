using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleCrpg.Core;

/// <summary>
/// 模拟场景配置（测试专用，不进入正式存档）。
/// 版本 1 保留历史四开关；版本 2 必须使用 player_policy 精确描述合法玩家操作，二者不得混用。
/// 未知字段一律拒绝，避免拼写错误静默变成“没有执行”。
/// </summary>
public sealed record SimulationConfig
{
    public int SchemaVersion { get; init; } = 1;
    public string ScenarioId { get; init; } = "custom";
    public string ScenarioNote { get; init; } = "";
    public int Seed { get; init; } = 20261023;
    public int DurationMinutes { get; init; } = 240;
    /// <summary>常规采样间隔（分钟）。</summary>
    public int SampleIntervalMinutes { get; init; } = 30;
    /// <summary>密集采样沿用 t=0 起的前 N 分钟（分钟）。</summary>
    public int DenseSampleMinutes { get; init; } = 30;
    /// <summary>密集采样每 N 分钟一行；0 表示关闭密集采样。</summary>
    public int DenseEveryMinutes { get; init; } = 1;
    /// <summary>版本 1 兼容字段：所有策略共用的检查周期。</summary>
    public int ActionCheckIntervalSeconds { get; init; } = 60;
    /// <summary>版本 1 兼容字段：城堡点击重试冷却。</summary>
    public int CastleRetryCooldownSeconds { get; init; } = 60;
    /// <summary>起始上阵职业；缺省为单人 warrior，保持历史 A–E 语义。</summary>
    public List<string>? StartingHeroes { get; init; }
    /// <summary>版本 1 兼容字段：四个总开关。版本 2 禁止使用。</summary>
    public SimulationActions? PlayerActions { get; init; }
    /// <summary>版本 2 必需字段：可组合的合法玩家策略。</summary>
    public PlayerPolicyRule? PlayerPolicy { get; init; }

    public static SimulationConfig Load(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var config = JsonSerializer.Deserialize<SimulationConfig>(File.ReadAllText(path).TrimStart('\uFEFF'), options)
            ?? throw new ArgumentException("空模拟配置");
        if (config.SchemaVersion is not (1 or 2)) throw new ArgumentException("不支持的模拟配置版本：" + config.SchemaVersion);
        if (string.IsNullOrWhiteSpace(config.ScenarioId) || config.ScenarioId.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
            throw new ArgumentException("场景名只能包含字母、数字与下划线");
        if (config.DurationMinutes <= 0 || config.DurationMinutes > 1440) throw new ArgumentException("模拟时长必须在 1..1440 分钟");
        if (config.SampleIntervalMinutes <= 0 || config.DenseEveryMinutes < 0 || config.DenseSampleMinutes < 0)
            throw new ArgumentException("采样参数不合法");
        if (config.StartingHeroes != null)
        {
            if (config.StartingHeroes.Count is < 1 or > 5) throw new ArgumentException("starting_heroes 必须 1..5 人");
            if (config.StartingHeroes.Distinct(StringComparer.Ordinal).Count() != config.StartingHeroes.Count)
                throw new ArgumentException("starting_heroes 职业重复");
        }
        return config;
    }
}

/// <summary>历史四开关：只用于版本 1 场景的兼容加载。</summary>
public sealed record SimulationActions
{
    public bool AutoCastleChallenge { get; init; }
    public bool AutoHeroGrowth { get; init; }
    public bool AutoStarmapPurchase { get; init; }
    public bool AutoPartyManagement { get; init; }
}

/// <summary>城堡交互策略：允许/禁止模拟点击，并指定检查周期与请求上限。</summary>
public sealed record CastlePolicyRule
{
    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 60;
    /// <summary>0 表示不限制。</summary>
    public int MaxRequests { get; init; }
}

/// <summary>星图购买的一步：节点与本次策略允许的最高等级。</summary>
public sealed record StarStepRule
{
    public string Id { get; init; } = "";
    public int MaxLevel { get; init; } = 1;
}

/// <summary>星图购买策略：none/legacy/explicit；explicit 必须给出节点顺序与等级上限。</summary>
public sealed record StarPolicyRule
{
    public string Mode { get; init; } = "none";
    public int IntervalSeconds { get; init; } = 60;
    public List<StarStepRule>? Nodes { get; init; }
}

/// <summary>编队策略：none/legacy/explicit；explicit 必须给出候选职业与期望人数。</summary>
public sealed record PartyPolicyRule
{
    public string Mode { get; init; } = "none";
    public int IntervalSeconds { get; init; } = 60;
    public List<string>? Roster { get; init; }
    /// <summary>0 表示按当前席位填满。</summary>
    public int TargetSize { get; init; }
}

/// <summary>天赋学习的一步：某英雄的某节点学到 N 级。</summary>
public sealed record TalentLearnRule
{
    public string Hero { get; init; } = "";
    public string Node { get; init; } = "";
    public int Ranks { get; init; } = 1;
}

/// <summary>主动技能装配的一步：某英雄装配哪些技能。</summary>
public sealed record SkillEquipRule
{
    public string Hero { get; init; } = "";
    public List<string>? Skills { get; init; }
}

/// <summary>天赋/主动技能策略：none/legacy/explicit。</summary>
public sealed record TalentPolicyRule
{
    public string Mode { get; init; } = "none";
    public int IntervalSeconds { get; init; } = 60;
    public List<TalentLearnRule>? Learn { get; init; }
    public List<SkillEquipRule>? Equip { get; init; }
}

/// <summary>玩家手动升级策略：none/greedy（按最便宜可升级英雄逐次点击，代表显式玩家决策）。</summary>
public sealed record UpgradePolicyRule
{
    public string Mode { get; init; } = "none";
    public int IntervalSeconds { get; init; } = 60;
}

/// <summary>可疑停滞观察器参数；默认 600 游戏秒仅作为嫌疑触发条件。</summary>
public sealed record DiagnosticsRule
{
    public int StallWindowSeconds { get; init; } = 600;
    public int RecheckSeconds { get; init; } = 300;
    public int MaxAlerts { get; init; } = 5;
}

/// <summary>版本 2 的策略集合；缺省子对象表示该动作被禁止。</summary>
public sealed record PlayerPolicyRule
{
    public CastlePolicyRule? Castle { get; init; }
    public StarPolicyRule? Starmap { get; init; }
    public PartyPolicyRule? Party { get; init; }
    public TalentPolicyRule? Talents { get; init; }
    public UpgradePolicyRule? Upgrade { get; init; }
    public DiagnosticsRule? Diagnostics { get; init; }
}

/// <summary>校验并归一化后的运行策略；所有数值与目标都已对正式配置核对。</summary>
public sealed record SimulationPolicy
{
    public bool CastleEnabled { get; init; }
    public int CastleIntervalSeconds { get; init; }
    public int CastleMaxRequests { get; init; }
    public string StarMode { get; init; } = "none";
    public int StarIntervalSeconds { get; init; }
    public IReadOnlyList<StarStepRule> StarNodes { get; init; } = Array.Empty<StarStepRule>();
    public string PartyMode { get; init; } = "none";
    public int PartyIntervalSeconds { get; init; }
    public IReadOnlyList<string> PartyRoster { get; init; } = Array.Empty<string>();
    public int PartyTargetSize { get; init; }
    public string TalentMode { get; init; } = "none";
    public int TalentIntervalSeconds { get; init; }
    public IReadOnlyList<TalentLearnRule> TalentLearn { get; init; } = Array.Empty<TalentLearnRule>();
    public IReadOnlyList<SkillEquipRule> SkillEquip { get; init; } = Array.Empty<SkillEquipRule>();
    public string UpgradeMode { get; init; } = "none";
    public int UpgradeIntervalSeconds { get; init; }
    public int StallWindowSeconds { get; init; } = 600;
    public int RecheckSeconds { get; init; } = 300;
    public int MaxAlerts { get; init; } = 5;

    private static readonly string[] Modes = { "none", "legacy", "explicit" };

    /// <summary>把场景配置解析为可执行策略；非法策略名、未知节点/职业/技能或越界周期一律拒绝。</summary>
    public static SimulationPolicy From(SimulationConfig cfg, Config config)
    {
        if (cfg.StartingHeroes != null)
            foreach (string id in cfg.StartingHeroes)
                if (!config.Heroes.Any(h => h.Id == id)) throw new ArgumentException("起始职业不存在：" + id);
        if (cfg.SchemaVersion == 1)
        {
            if (cfg.PlayerActions == null) throw new ArgumentException("版本 1 场景必须提供 player_actions");
            if (cfg.PlayerPolicy != null) throw new ArgumentException("版本 1 场景不得同时提供 player_policy（请升级为版本 2）");
            var a = cfg.PlayerActions;
            return new SimulationPolicy
            {
                CastleEnabled = a.AutoCastleChallenge,
                CastleIntervalSeconds = CheckInterval(cfg.CastleRetryCooldownSeconds, "castle_retry_cooldown_seconds"),
                CastleMaxRequests = 0,
                StarMode = a.AutoStarmapPurchase ? "legacy" : "none",
                StarIntervalSeconds = CheckInterval(cfg.ActionCheckIntervalSeconds, "action_check_interval_seconds"),
                PartyMode = a.AutoPartyManagement ? "legacy" : "none",
                PartyIntervalSeconds = CheckInterval(cfg.ActionCheckIntervalSeconds, "action_check_interval_seconds"),
                TalentMode = a.AutoHeroGrowth ? "legacy" : "none",
                TalentIntervalSeconds = CheckInterval(cfg.ActionCheckIntervalSeconds, "action_check_interval_seconds"),
                UpgradeMode = "none",
                UpgradeIntervalSeconds = CheckInterval(cfg.ActionCheckIntervalSeconds, "action_check_interval_seconds")
            };
        }
        if (cfg.PlayerPolicy == null) throw new ArgumentException("版本 2 场景必须提供 player_policy");
        if (cfg.PlayerActions != null) throw new ArgumentException("版本 2 场景不得使用 player_actions 总开关（请改用 player_policy）");
        var p = cfg.PlayerPolicy;
        var castle = p.Castle ?? new CastlePolicyRule { Enabled = false };
        var star = p.Starmap ?? new StarPolicyRule();
        var party = p.Party ?? new PartyPolicyRule();
        var talents = p.Talents ?? new TalentPolicyRule();
        var upgrade = p.Upgrade ?? new UpgradePolicyRule();
        var diagnostics = p.Diagnostics ?? new DiagnosticsRule();
        if (!Modes.Contains(star.Mode)) throw new ArgumentException("未知星图策略：" + star.Mode);
        if (!Modes.Contains(party.Mode)) throw new ArgumentException("未知编队策略：" + party.Mode);
        if (!Modes.Contains(talents.Mode)) throw new ArgumentException("未知天赋策略：" + talents.Mode);
        if (upgrade.Mode is not ("none" or "greedy")) throw new ArgumentException("未知升级策略：" + upgrade.Mode);
        if (castle.MaxRequests < 0) throw new ArgumentException("castle.max_requests 不能为负");

        IReadOnlyList<StarStepRule> starNodes = Array.Empty<StarStepRule>();
        if (star.Mode == "explicit")
        {
            if (star.Nodes == null || star.Nodes.Count == 0) throw new ArgumentException("显式星图策略必须给出 nodes");
            if (star.Nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != star.Nodes.Count)
                throw new ArgumentException("星图节点重复");
            foreach (var node in star.Nodes)
            {
                if (!config.Stars.Nodes.TryGetValue(node.Id, out var definition)) throw new ArgumentException("星图节点不存在：" + node.Id);
                if (node.MaxLevel < 1 || node.MaxLevel > definition.MaxLevel) throw new ArgumentException($"星图节点 {node.Id} 的 max_level 必须在 1..{definition.MaxLevel}");
            }
            starNodes = star.Nodes.ToArray();
        }

        IReadOnlyList<string> roster = Array.Empty<string>();
        if (party.Mode == "explicit")
        {
            if (party.Roster == null || party.Roster.Count == 0) throw new ArgumentException("显式编队策略必须给出 roster");
            if (party.Roster.Distinct(StringComparer.Ordinal).Count() != party.Roster.Count) throw new ArgumentException("编队职业重复");
            if (party.TargetSize < 1 || party.TargetSize > 5) throw new ArgumentException("party.target_size 必须在 1..5");
            if (party.TargetSize > party.Roster.Count) throw new ArgumentException("party.target_size 不能超过 roster 人数");
            foreach (string id in party.Roster)
                if (!config.Heroes.Any(h => h.Id == id)) throw new ArgumentException("职业不存在：" + id);
            roster = party.Roster.ToArray();
        }
        else if (party.Roster != null) throw new ArgumentException("非 explicit 编队策略不得给出 roster");

        IReadOnlyList<TalentLearnRule> learn = Array.Empty<TalentLearnRule>();
        IReadOnlyList<SkillEquipRule> equip = Array.Empty<SkillEquipRule>();
        if (talents.Mode == "explicit")
        {
            learn = talents.Learn?.ToArray() ?? Array.Empty<TalentLearnRule>();
            equip = talents.Equip?.ToArray() ?? Array.Empty<SkillEquipRule>();
            if (learn.Count == 0 && equip.Count == 0) throw new ArgumentException("显式天赋策略必须给出 learn 或 equip");
            foreach (var step in learn)
            {
                if (!config.Heroes.Any(h => h.Id == step.Hero)) throw new ArgumentException("天赋计划中的职业不存在：" + step.Hero);
                if (!config.Talents.Nodes.TryGetValue(step.Node, out var node)) throw new ArgumentException("天赋节点不存在：" + step.Node);
                if (node.Hero != step.Hero) throw new ArgumentException($"节点 {step.Node} 不属于 {step.Hero}");
                if (step.Ranks < 1 || step.Ranks > node.MaxRank) throw new ArgumentException($"节点 {step.Node} 的 ranks 必须在 1..{node.MaxRank}");
            }
            foreach (var step in equip)
            {
                if (!config.Heroes.Any(h => h.Id == step.Hero)) throw new ArgumentException("装配计划中的职业不存在：" + step.Hero);
                if (step.Skills == null || step.Skills.Count == 0) throw new ArgumentException("装配计划必须给出 skills：" + step.Hero);
                if (step.Skills.Distinct(StringComparer.Ordinal).Count() != step.Skills.Count) throw new ArgumentException("装配技能重复：" + step.Hero);
                foreach (string id in step.Skills)
                {
                    bool talent = config.Talents.Nodes.ContainsKey(id);
                    if (!talent && (!config.Skills.Skills.TryGetValue(id, out var skill) || skill.Kind != "ActiveSkill"))
                        throw new ArgumentException("技能不存在或不是主动技能：" + id);
                }
            }
        }
        else if (talents.Learn != null || talents.Equip != null) throw new ArgumentException("非 explicit 天赋策略不得给出 learn/equip");

        return new SimulationPolicy
        {
            CastleEnabled = castle.Enabled,
            CastleIntervalSeconds = CheckInterval(castle.IntervalSeconds, "castle.interval_seconds"),
            CastleMaxRequests = castle.MaxRequests,
            StarMode = star.Mode,
            StarIntervalSeconds = CheckInterval(star.IntervalSeconds, "starmap.interval_seconds"),
            StarNodes = starNodes,
            PartyMode = party.Mode,
            PartyIntervalSeconds = CheckInterval(party.IntervalSeconds, "party.interval_seconds"),
            PartyRoster = roster,
            PartyTargetSize = party.Mode == "explicit" ? party.TargetSize : 0,
            TalentMode = talents.Mode,
            TalentIntervalSeconds = CheckInterval(talents.IntervalSeconds, "talents.interval_seconds"),
            TalentLearn = learn,
            SkillEquip = equip,
                UpgradeMode = upgrade.Mode,
                UpgradeIntervalSeconds = CheckInterval(upgrade.IntervalSeconds, "upgrade.interval_seconds"),
            StallWindowSeconds = CheckWindow(diagnostics.StallWindowSeconds, "diagnostics.stall_window_seconds"),
            RecheckSeconds = CheckInterval(diagnostics.RecheckSeconds, "diagnostics.recheck_seconds"),
            MaxAlerts = Math.Clamp(diagnostics.MaxAlerts, 1, 50)
        };
    }

    private static int CheckInterval(int value, string name)
    {
        if (value < 1 || value > 3600) throw new ArgumentException($"{name} 必须在 1..3600 秒");
        return value;
    }
    private static int CheckWindow(int value, string name)
    {
        if (value < 30 || value > 86400) throw new ArgumentException($"{name} 必须在 30..86400 秒");
        return value;
    }

    /// <summary>可追溯的策略摘要，写入 metadata 与报告。</summary>
    public string Summarize()
    {
        var star = StarMode == "explicit" ? "explicit[" + string.Join(">", StarNodes.Select(n => $"{n.Id}*{n.MaxLevel}")) + "]" : StarMode;
        var party = PartyMode == "explicit" ? "explicit[" + string.Join("/", PartyRoster) + $"#{PartyTargetSize}]" : PartyMode;
        var talent = TalentMode == "explicit"
            ? "explicit[learn=" + string.Join(";", TalentLearn.Select(l => $"{l.Hero}:{l.Node}*{l.Ranks}")) + ";equip=" + string.Join(";", SkillEquip.Select(e => $"{e.Hero}:{string.Join("|", e.Skills ?? new List<string>())}")) + "]"
            : TalentMode;
        return $"castle={(CastleEnabled ? "on" : "off")}@{CastleIntervalSeconds}s starmap={star}@{StarIntervalSeconds}s party={party}@{PartyIntervalSeconds}s talents={talent}@{TalentIntervalSeconds}s";
    }
}
