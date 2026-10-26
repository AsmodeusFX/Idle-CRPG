using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleCrpg.Core;

/// <summary>模拟输出：统一 CSV/JSON 写出、配置指纹与业务一致性哈希；只写 artifacts。</summary>
public sealed class SimulationRecorder
{
    public List<string> Events { get; } = new() { "sim_time,tick,event_type,target,result,reason,detail" };
    public List<string> Ledger { get; } = new() { "sim_time,tick,source,item_id,amount,balance,note" };
    public List<string> Snapshots { get; } = new() { "sim_time,scenario,seed,chapter,difficulty,region,map,stage,heroes,gold,kills,retries,party,star_nodes,star_gold,pending_castles,waiting_approval,floor,room,status" };
    public List<string> Diagnostics { get; } = new() { "sim_time,tick,classification,reason,tags,evidence" };
    public List<string> AssetLedger {get;}=new(){"event_id,run_id,tick_or_time,source,action,hero_instance_id,asset_type,definition_id,delta,before,after,origin_or_receipt"};
    public void Asset(AssetEvent e)=>AssetLedger.Add(string.Join(',',Q(e.EventId),Q(e.RunId),e.LogicTick,Q(e.Source),Q(e.Action),Q(e.HeroInstanceId),Q(e.AssetType),Q(e.DefinitionId),e.Delta,Q(e.Before),Q(e.After),Q(e.Origin)));

    private static string Q(string? value) => "\"" + (value ?? "unavailable").Replace("\"", "\"\"") + "\"";
    private static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public void Event(long tick, string type, string target, string result, string reason, string detail = "")
        => Events.Add(string.Join(',', Num(tick / 20d), tick.ToString(CultureInfo.InvariantCulture), Q(type), Q(target), Q(result), Q(reason), Q(detail)));

    public void Gold(long tick, string source, string itemId, long amount, long balance, string note = "")
        => Ledger.Add(string.Join(',', Num(tick / 20d), tick.ToString(CultureInfo.InvariantCulture), Q(source), Q(itemId), amount.ToString(CultureInfo.InvariantCulture), balance.ToString(CultureInfo.InvariantCulture), Q(note)));

    public void Snapshot(long tick, string scenario, int seed, int chapter, int difficulty, int region, string map, string stage, string heroes, long gold, int kills, int retries, int party, int starNodes, long starGold, string pending, bool waiting, int floor, int room, string status)
        => Snapshots.Add(string.Join(',', Num(tick / 20d), Q(scenario), seed.ToString(CultureInfo.InvariantCulture), chapter.ToString(CultureInfo.InvariantCulture), difficulty.ToString(CultureInfo.InvariantCulture), region.ToString(CultureInfo.InvariantCulture), Q(map), Q(stage), Q(heroes), gold.ToString(CultureInfo.InvariantCulture), kills.ToString(CultureInfo.InvariantCulture), retries.ToString(CultureInfo.InvariantCulture), party.ToString(CultureInfo.InvariantCulture), starNodes.ToString(CultureInfo.InvariantCulture), starGold.ToString(CultureInfo.InvariantCulture), Q(pending), waiting ? "1" : "0", floor.ToString(CultureInfo.InvariantCulture), room.ToString(CultureInfo.InvariantCulture), Q(status)));

    public void Diagnostic(DiagnosticAlert alert)
        => Diagnostics.Add(string.Join(',', Num(alert.SimTime), alert.Tick.ToString(CultureInfo.InvariantCulture), Q(alert.Classification), Q(alert.Reason), Q(string.Join('|', alert.Tags)), Q(alert.Evidence)));

    /// <summary>配置指纹：按相对路径排序后对全部 CSV 内容做 SHA256；用于确认“用的就是这套表”。</summary>
    public static string ConfigFingerprint(string configRoot)
    {
        var files = Directory.GetFiles(configRoot, "*.csv", SearchOption.AllDirectories)
            .Select(path => (Path: path, Relative: Path.GetRelativePath(configRoot, path).Replace('\\', '/')))
            .OrderBy(x => x.Relative, StringComparer.Ordinal).ToArray();
        using var sha = SHA256.Create();
        using var stream = new MemoryStream();
        foreach (var file in files)
        {
            var name = Encoding.UTF8.GetBytes(file.Relative + "\n");
            stream.Write(name, 0, name.Length);
            var bytes = File.ReadAllBytes(file.Path);
            stream.Write(bytes, 0, bytes.Length);
        }
        stream.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>源码指纹：正式 Core 脚本与测试脚本（排除 bin/obj）的 SHA256，用于区分未提交改动。</summary>
    public static string SourceFingerprint(string root)
    {
        var scopes = new[] { Path.Combine(root, "Idle-CRPG/Scripts"), Path.Combine(root, "tests") };
        var files = scopes.Where(Directory.Exists)
            .SelectMany(scope => Directory.GetFiles(scope, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: path, Relative: Path.GetRelativePath(root, path).Replace('\\', '/')))
            .OrderBy(x => x.Relative, StringComparer.Ordinal).ToArray();
        using var sha = SHA256.Create();
        using var stream = new MemoryStream();
        foreach (var file in files)
        {
            var name = Encoding.UTF8.GetBytes(file.Relative + "\n");
            stream.Write(name, 0, name.Length);
            var bytes = File.ReadAllBytes(file.Path);
            stream.Write(bytes, 0, bytes.Length);
        }
        stream.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>代码版本：直读 .git（不依赖 git CLI）；缺失记为 unavailable。</summary>
    public static string CodeVersion(string root)
    {
        try
        {
            string head = Path.Combine(root, ".git", "HEAD");
            if (!File.Exists(head)) return "unavailable";
            string text = File.ReadAllText(head).Trim();
            string gitDir = Path.Combine(root, ".git");
            if (text.StartsWith("ref:", StringComparison.Ordinal))
            {
                string reference = text[4..].Trim();
                string refPath = Path.Combine(gitDir, reference.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(refPath) ? $"{reference}={File.ReadAllText(refPath).Trim()}" : $"{reference}=unavailable";
            }
            return "detached=" + text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return "unavailable"; }
    }

    /// <summary>业务一致性哈希：只覆盖机器数据行，不含文件名与墙钟元信息。</summary>
    public string BusinessHash()
    {
        using var sha = SHA256.Create();
        using var stream = new MemoryStream();
        void Feed(IEnumerable<string> rows)
        {
            foreach (var row in rows) { var bytes = Encoding.UTF8.GetBytes(row + "\n"); stream.Write(bytes, 0, bytes.Length); }
        }
        Feed(Snapshots); Feed(Events); Feed(Ledger); Feed(Diagnostics);
        stream.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    public void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "events.csv"), Events, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(directory, "ledger.csv"), Ledger, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(directory, "snapshots.csv"), Snapshots, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(directory, "diagnostics.csv"), Diagnostics, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(directory,"asset_ledger.csv"),AssetLedger,new UTF8Encoding(false));
    }
}
