using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;


namespace IdleCrpg.Core;

/// <summary>节点编辑草稿。意图是侧车数据，不进入游戏运行定义。</summary>
public sealed class StarEditorDocument
{
    public Dictionary<string, StarNode> Nodes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, StarLayout> Layout { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Intents { get; } = new(StringComparer.Ordinal);
    private readonly Stack<(string Content, string Layout, string Plan)> _undo = new(), _redo = new();
    public bool Dirty { get; private set; }
    public StarEditorDocument(string content, string layout, string plan)
    {
        var config = StarConfiguration.Load(content, layout);
        foreach (var pair in config.Nodes) Nodes.Add(pair.Key, pair.Value);
        foreach (var pair in config.Layout) Layout.Add(pair.Key, pair.Value);
        foreach (var row in CsvTable.Load(StarConfiguration.PlanPath, plan, "id", "intent").Rows)
        {
            if (!Nodes.ContainsKey(row.Id)) throw row.Error("id", "intent references missing talent");
            Intents.Add(row.Id, row["intent"]);
        }
    }
    public StarConfiguration Validate() => new(Nodes.Values, Layout.Values);
    private void BeforeChange() { _undo.Push(Texts()); _redo.Clear(); Dirty = true; }
    public string Create(int col, int row, string? copyFrom = null)
    {
        if (Layout.Values.Any(n => n.Col == col && n.Row == row)) throw new InvalidOperationException("目标格子已有节点。");
        BeforeChange();
        // 删除节点后也不复用旧ID；可读名独立于机器身份。
        string id = "s_" + Guid.NewGuid().ToString("N");
        Nodes[id] = copyFrom is null ? new(id, "未配置节点", 1, "gold", Array.AsReadOnly(new long[] { 0 }), "", "", 0, "utility")
            : Nodes[copyFrom] with { Id = id };
        Layout[id] = new(id, col, row, Array.Empty<string>(), "");
        Intents[id] = copyFrom is null ? "" : Intents.GetValueOrDefault(copyFrom, "");
        return id;
    }
    public void Move(string id, int col, int row)
    {
        if (Layout.Values.Any(n => n.Id != id && n.Col == col && n.Row == row)) throw new InvalidOperationException("目标格子已有节点。");
        BeforeChange(); Layout[id] = Layout[id] with { Col = col, Row = row };
    }
    public void SetNode(StarNode node, string label, string intent)
    {
        if (!Nodes.ContainsKey(node.Id)) throw new InvalidOperationException("节点ID不能改名。");
        BeforeChange(); Nodes[node.Id] = node; Layout[node.Id] = Layout[node.Id] with { Label = label }; Intents[node.Id] = intent;
    }
    public void SetPrerequisites(string id, IEnumerable<string> parents)
    { BeforeChange(); Layout[id] = Layout[id] with { Prerequisites = Array.AsReadOnly(parents.ToArray()) }; }
    public void SetRoot(string id)
    {
        BeforeChange();
        var roots = Layout.Values.Where(n => n.Prerequisites.Count == 0 && n.Id != id).ToArray();
        Layout[id] = Layout[id] with { Prerequisites = Array.Empty<string>() };
        foreach (var root in roots) Layout[root.Id] = root with { Prerequisites = Array.AsReadOnly(new[] { id }) };
    }
    public void Delete(string id)
    {
        BeforeChange(); var parents = Layout[id].Prerequisites;
        foreach (var node in Layout.Values.ToArray())
            if (node.Prerequisites.Contains(id)) Layout[node.Id] = node with {
                Prerequisites = Array.AsReadOnly(node.Prerequisites.Where(p => p != id).Concat(parents).Distinct().ToArray()) };
        Layout.Remove(id); Nodes.Remove(id); Intents.Remove(id);
    }
    public void Undo() { if (_undo.Count > 0) { _redo.Push(Texts()); RestoreDraft(_undo.Pop()); Dirty = true; } }
    public void Redo() { if (_redo.Count > 0) { _undo.Push(Texts()); RestoreDraft(_redo.Pop()); Dirty = true; } }
    // 草稿允许暂时未连线；撤销时不以完整配置校验阻断中间编辑状态。
    private void RestoreDraft((string Content, string Layout, string Plan) text)
    {
        Nodes.Clear(); Layout.Clear(); Intents.Clear();
        var rows = CsvTable.Load(StarConfiguration.ContentPath, text.Content, StarConfiguration.ContentHeader.Split(','));
        foreach (var r in rows.Rows)
        {
            var attr=r["attr_id"]; var op=r["operation"];
            Nodes[r.Id] = new(r.Id, r["name"], r.Integer("max_level"), r["cost_currency"], Array.AsReadOnly(r.List("cost").Select(v => long.Parse(v, CultureInfo.InvariantCulture)).ToArray()), attr, op, r.Number("value_per_level"), r["icon"], r["target_id"]);
        }
        foreach (var r in CsvTable.Load(StarConfiguration.LayoutPath, text.Layout, StarConfiguration.LayoutHeader.Split(',')).Rows)
            Layout[r.Id] = new(r.Id, r.Integer("col"), r.Integer("row"), Array.AsReadOnly(r.List("prereq")), r["label"]);
        foreach (var r in CsvTable.Load(StarConfiguration.PlanPath, text.Plan, "id", "intent").Rows) Intents[r.Id] = r["intent"];
    }
    private static string Csv(params object[] fields) => string.Join(',', fields.Select(f => "\"" + Convert.ToString(f, CultureInfo.InvariantCulture)!.Replace("\"", "\"\"") + "\""));
    public (string Content, string Layout, string Plan) Texts()
    {
        var nodes = Nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
        string content = StarConfiguration.ContentHeader + "\n" + string.Join('\n', nodes.Select(n => Csv(n.Id, n.Name, n.MaxLevel, n.Currency,
            string.Join(';', n.Costs), n.Attribute == "" ? "" : n.Attribute,
            n.Operation == "" ? "" : n.Operation, n.ValuePerLevel, n.Icon, n.TargetId))) + "\n";
        string layout = StarConfiguration.LayoutHeader + "\n" + string.Join('\n', nodes.Select(n => { var l = Layout[n.Id]; return Csv(n.Id, l.Col, l.Row, string.Join(';', l.Prerequisites), l.Label); })) + "\n";
        string plan = StarConfiguration.PlanHeader + "\n" + string.Join('\n', nodes.Select(n => Csv(n.Id, Intents.GetValueOrDefault(n.Id, "")))) + "\n";
        return (content, layout, plan);
    }
}
