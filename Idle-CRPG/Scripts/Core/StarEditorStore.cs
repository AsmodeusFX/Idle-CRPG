using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;


namespace IdleCrpg.Core;

/// <summary>源码工具存储：检测外部修改；三文件失败回滚，不覆盖其他配置目录。</summary>
public sealed class StarEditorStore
{
    private readonly string[] _paths;
    private string[] _baseline = Array.Empty<string>();
    public StarEditorStore(string configRoot) => _paths = new[] { StarConfiguration.ContentPath, StarConfiguration.LayoutPath, StarConfiguration.PlanPath }
        .Select(p => Path.Combine(configRoot, p)).ToArray();
    public StarEditorDocument Load()
    {
        string[] texts = _paths.Select((p, i) => File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : i == 2 ? "id,intent\n" : throw new FileNotFoundException("缺少星图配置", p)).ToArray();
        var result = new StarEditorDocument(texts[0], texts[1], texts[2]); _baseline = texts; return result;
    }
    public void Save(StarEditorDocument document, StarProgressSave liveRanks)
    {
        var config = document.Validate(); _ = new StarProgression(config, liveRanks);
        if (_baseline.Length != 3) throw new InvalidOperationException("保存前必须重新加载源表。");
        for (int i = 0; i < 3; i++)
        {
            string current = File.Exists(_paths[i]) ? File.ReadAllText(_paths[i], Encoding.UTF8) : i == 2 ? "id,intent\n" : "";
            if (current != _baseline[i]) throw new InvalidOperationException("源表已被外部修改，请重新加载后合并，未覆盖磁盘文件。");
        }
        var text = document.Texts(); string[] next = { text.Content, text.Layout, text.Plan };
        string[] temps = _paths.Select(p => p + "." + Guid.NewGuid().ToString("N") + ".tmp").ToArray();
        var originals = _paths.Select(p => File.Exists(p) ? File.ReadAllBytes(p) : null).ToArray();
        int committed = 0;
        try
        {
            for (int i = 0; i < 3; i++) File.WriteAllText(temps[i], next[i], new UTF8Encoding(false));
            for (int i = 0; i < 3; i++) { File.Move(temps[i], _paths[i], true); committed++; }
            _baseline = next;
        }
        catch (Exception error)
        {
            var failures = new List<Exception> { error };
            for (int i = 0; i < committed; i++)
                try { if (originals[i] is null) File.Delete(_paths[i]); else File.WriteAllBytes(_paths[i], originals[i]!); }
                catch (Exception rollback) { failures.Add(rollback); }
            if (failures.Count > 1) throw new AggregateException("星图源表保存及回滚失败，请检查文件。", failures);
            throw;
        }
        finally { foreach (string path in temps) if (File.Exists(path)) File.Delete(path); }
    }
}
