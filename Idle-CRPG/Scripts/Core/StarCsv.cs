using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace IdleCrpg.Core;

public sealed class CsvConfigurationException : InvalidOperationException
{
    public CsvConfigurationException(string file, int line, string id, string column, string reason)
        : base($"{file}:{line}: [{id}] {column}: {reason}")
    { SourceFile = file; SourceLine = line; DefinitionId = id; Column = column; }
    public string SourceFile { get; }
    public int SourceLine { get; }
    public string DefinitionId { get; }
    public string Column { get; }
}

/// <summary>迁移自 Idle-Diablo 的严格 CSV：支持 BOM、引号内逗号/换行和双引号转义。</summary>
public sealed class CsvTable
{
    private readonly IReadOnlyDictionary<string, CsvRow> _byId;
    private CsvTable(string file, string[] columns, List<CsvRow> rows)
    {
        SourceFile = file; Columns = Array.AsReadOnly(columns); Rows = rows.AsReadOnly();
        _byId = new ReadOnlyDictionary<string, CsvRow>(rows.ToDictionary(r => r.Id, StringComparer.Ordinal));
    }
    public string SourceFile { get; }
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<CsvRow> Rows { get; }
    public CsvRow Require(string id) => _byId.TryGetValue(id, out var row) ? row
        : throw new CsvConfigurationException(SourceFile, 1, id, Columns[0], "unknown reference");
    public bool Contains(string id) => _byId.ContainsKey(id);
    public static CsvTable Load(string file, string text, params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(text);
        var records = Parse(file, text.TrimStart('\uFEFF'));
        if (records.Count == 0 || !records[0].Cells.SequenceEqual(columns, StringComparer.Ordinal))
            throw new CsvConfigurationException(file, 1, "<header>", "schema", "expected " + string.Join(',', columns));
        var rows = new List<CsvRow>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records.Skip(1))
        {
            if (record.Cells.Length != columns.Length)
                throw new CsvConfigurationException(file, record.Line, "<row>", "schema", $"expected {columns.Length} cells, got {record.Cells.Length}");
            var row = new CsvRow(file, record.Line, columns, record.Cells);
            if (string.IsNullOrWhiteSpace(row.Id) || !ids.Add(row.Id)) throw row.Error(columns[0], "empty or duplicate id");
            rows.Add(row);
        }
        return new CsvTable(file, columns, rows);
    }
    public static IReadOnlyList<(int Line, string[] Cells)> ReadRecords(string file, string text) => Parse(file, text.TrimStart('\uFEFF'));
    private static List<(int Line, string[] Cells)> Parse(string file, string text)
    {
        var output = new List<(int, string[])>(); var cells = new List<string>(); var value = new StringBuilder();
        int line = 1, start = 1; bool quoted = false, closed = false, touched = false;
        void Cell() { cells.Add(value.ToString()); value.Clear(); closed = false; }
        void Record()
        {
            if (touched || cells.Count != 0 || value.Length != 0) { Cell(); output.Add((start, cells.ToArray())); }
            cells.Clear(); value.Clear(); touched = false; closed = false;
        }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; } else { quoted = false; closed = true; } }
                else { value.Append(c); if (c == '\n') line++; }
                continue;
            }
            if (closed && c != ',' && c != '\r' && c != '\n')
                throw new CsvConfigurationException(file, line, "<row>", "csv", "unexpected text after closing quote");
            if (c == '"')
            {
                if (value.Length != 0) throw new CsvConfigurationException(file, line, "<row>", "csv", "quote inside unquoted cell");
                quoted = true; touched = true;
            }
            else if (c == ',') { touched = true; Cell(); }
            else if (c is '\r' or '\n')
            {
                Record(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                line++; start = line;
            }
            else { value.Append(c); touched = true; }
        }
        if (quoted) throw new CsvConfigurationException(file, start, "<row>", "csv", "unterminated quote");
        Record(); return output;
    }
}

public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, string> _values;
    internal CsvRow(string file, int line, string[] columns, string[] values)
    {
        SourceFile = file; Line = line; Id = values[0];
        _values = new ReadOnlyDictionary<string, string>(columns.Zip(values).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal));
    }
    public string SourceFile { get; }
    public int Line { get; }
    public string Id { get; }
    public string this[string column] => _values[column];
    public CsvConfigurationException Error(string column, string reason) => new(SourceFile, Line, Id, column, reason);
    public string Required(string column) => !string.IsNullOrWhiteSpace(this[column]) ? this[column] : throw Error(column, "required value missing");
    public double Number(string column)
    {
        if (!double.TryParse(Required(column), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw Error(column, "expected finite invariant number");
        return n;
    }
    public int Integer(string column) => int.TryParse(Required(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : throw Error(column, "expected integer");
    public bool Boolean(string column) => Required(column) switch { "true" => true, "false" => false, _ => throw Error(column, "expected true or false") };
    public T Enum<T>(string column) where T : struct, Enum => System.Enum.TryParse<T>(Required(column), false, out var e) && System.Enum.IsDefined(e) && !char.IsDigit(this[column][0]) ? e : throw Error(column, "unknown " + typeof(T).Name);
    public string[] List(string column) => this[column].Length == 0 ? Array.Empty<string>() : this[column].Split(';');
}
