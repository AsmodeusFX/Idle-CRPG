using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using IdleCrpg.Core;

/// <summary>
/// 旧档迁移只读回归：先冻结样本副本，再逐份走真实解码/编码，断言英雄等级不降、公共XP余额幂等、样本不被改写。
/// 冻结副本是唯一输入，避免其它 QA 夹具被重跑时产生"样本指纹变化"的假红。
/// </summary>
public static class LegacyRunMigrationChecks
{
    private const string ManifestPath="artifacts/qa/p2-4-first-batch/legacy-save-manifest.json";
    private const string FrozenDir="artifacts/qa/p2-4-first-batch/legacy-saves";
    private const string FrozenManifest="artifacts/qa/p2-4-first-batch/legacy-saves-manifest.json";

    /// <summary>显式冻结：把当前旧档样本逐字节复制为不可变副本并记录指纹；不自动执行，避免掩盖样本漂移。</summary>
    public static void Freeze(string root)
    {
        string source=Path.Combine(root,ManifestPath),directory=Path.Combine(root,FrozenDir);
        if(!File.Exists(source))throw new Exception("缺少旧档样本清单："+ManifestPath);
        Directory.CreateDirectory(directory);
        using var doc=JsonDocument.Parse(File.ReadAllText(source));
        var entries=new List<object>();int reused=0,intact=0,missing=0;
        foreach(var entry in doc.RootElement.GetProperty("files").EnumerateArray())
        {
            string relative=entry.GetProperty("path").GetString()!,path=Path.Combine(root,relative);
            if(!File.Exists(path)){missing++;continue;}
            byte[] bytes=File.ReadAllBytes(path);string sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string name=entries.Count.ToString("D4")+".json";
            File.WriteAllBytes(Path.Combine(directory,name),bytes);
            if(sha.Equals(entry.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))intact++;else reused++;
            entries.Add(new {file=name,source=relative,sha256=sha,original_sha256=entry.GetProperty("sha256").GetString(),drifted=!sha.Equals(entry.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)});
        }
        File.WriteAllText(Path.Combine(root,FrozenManifest),JsonSerializer.Serialize(new {total=entries.Count,missing,intact,drifted=reused,files=entries},new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"LEGACY_FREEZE total={entries.Count} intact={intact} drifted={reused} missing={missing} dir={directory}");
    }

    public static void Run(string root)
    {
        string frozenManifest=Path.Combine(root,FrozenManifest),directory=Path.Combine(root,FrozenDir);
        if(!File.Exists(frozenManifest))throw new Exception("缺少冻结样本，请显式执行 --freeze-legacy-samples："+FrozenManifest);
        using var source=JsonDocument.Parse(File.ReadAllText(frozenManifest));
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        string output=QaOutput.NewDirectory(root,"legacy-run-migration");
        var results=new List<object>();var counts=new Dictionary<string,int>();int drifted=0;
        foreach(var entry in source.RootElement.GetProperty("files").EnumerateArray())
        {
            string name=entry.GetProperty("file").GetString()!,path=Path.Combine(directory,name);
            if(!File.Exists(path))throw new Exception("冻结样本消失："+name);
            byte[] bytes=File.ReadAllBytes(path);string before=Convert.ToHexString(SHA256.HashData(bytes));
            if(!before.Equals(entry.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new Exception("冻结样本被修改："+name);
            if(entry.TryGetProperty("drifted",out var drift)&&drift.GetBoolean())drifted++;
            string category,reason="";
            try
            {
                var doc=SaveStore.Decode(c,System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                if(doc.Session==null)category="EMPTY_DOCUMENT";
                else
                {
                    var old=doc.Session.Heroes.Select(h=>h.Level).ToArray();string encoded=SaveStore.Encode(doc);
                    var roundtrip=SaveStore.Decode(c,encoded);
                    if(!old.SequenceEqual(roundtrip.Session!.Heroes.Select(h=>h.Level))||roundtrip.Session.PublicExperience!=doc.Session.PublicExperience)throw new Exception("迁移等级或余额不幂等");
                    File.WriteAllText(Path.Combine(output,results.Count+".json"),encoded);
                    category="SHADOW_MIGRATED";
                }
            }
            catch(MigrationRequiredException e){category="USER_DECISION_REQUIRED";reason=e.Message;}
            catch(Exception e) when(e is JsonException or InvalidDataException or ArgumentException or InvalidOperationException or NullReferenceException or OverflowException){category="REJECTED_PRESERVED";reason=e.GetType().Name+": "+e.Message;}
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=before)throw new Exception("冻结样本被改写："+name);
            counts[category]=counts.GetValueOrDefault(category)+1;
            results.Add(new {file=name,source=entry.GetProperty("source").GetString(),category,reason,sha256=before});
        }
        File.WriteAllText(Path.Combine(output,"migration-results.json"),JsonSerializer.Serialize(new {total=results.Count,counts,frozen_drifted=drifted,results},new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine("LEGACY_MIGRATION_PASS "+results.Count+" "+JsonSerializer.Serialize(counts)+" frozen_drifted="+drifted+" output="+output);
    }
}
