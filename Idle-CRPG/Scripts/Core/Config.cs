using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record UnitSpec(string Id, string Name, float Hp, float Attack, float Defense, float Speed, float Range, float Cooldown);

public sealed class Config
{
    public int FloorsMin, FloorsMax, RoomsMin, RoomsMax, MonstersMin, MonstersMax;
    public float EventChance;
    public List<UnitSpec> Heroes = new(), Monsters = new();

    public static Config Load(string directory)
    {
        var rows = Read(Path.Combine(directory, "dungeon.csv"));
        if (rows.Count != 1) throw new InvalidDataException("dungeon.csv 必须恰有一行数据");
        var r = rows[0];
        var c = new Config {
            FloorsMin = Integer(r, "floors_min"), FloorsMax = Integer(r, "floors_max"),
            RoomsMin = Integer(r, "rooms_min"), RoomsMax = Integer(r, "rooms_max"),
            MonstersMin = Integer(r, "monsters_min"), MonstersMax = Integer(r, "monsters_max"),
            EventChance = Number(r, "event_chance"),
            Heroes = Units(Path.Combine(directory, "heroes.csv")), Monsters = Units(Path.Combine(directory, "monsters.csv"))
        };
        if (c.FloorsMin < 1 || c.FloorsMax < c.FloorsMin || c.FloorsMax > 20 || c.RoomsMin < 3 || c.RoomsMax < c.RoomsMin || c.RoomsMax > 20 || c.MonstersMin < 1 || c.MonstersMax < c.MonstersMin || c.MonstersMax > 30 || c.EventChance < 0 || c.EventChance > 1 || c.Heroes.Count < 3 || c.Monsters.Count == 0)
            throw new InvalidDataException("地牢配置范围非法");
        return c;
    }

    private static List<UnitSpec> Units(string path)
    {
        var result = Read(path).Select(r => new UnitSpec(r["id"], r["name"], Number(r,"hp"), Number(r,"attack"), Number(r,"defense"), Number(r,"speed"), Number(r,"range"), Number(r,"cooldown"))).ToList();
        if (result.Select(u => u.Id).Distinct().Count() != result.Count || result.Any(u => string.IsNullOrWhiteSpace(u.Id) || string.IsNullOrWhiteSpace(u.Name) || u.Hp <= 0 || u.Attack <= 0 || u.Defense < 0 || u.Speed <= 0 || u.Range <= 0 || u.Cooldown <= 0)) throw new InvalidDataException(path + " 职业/怪物字段非法");
        return result;
    }

    private static List<Dictionary<string,string>> Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) throw new InvalidDataException(path + " 空表");
        var headers = lines[0].TrimStart('\uFEFF').Split(',');
        var result = new List<Dictionary<string,string>>();
        foreach (var line in lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var cells = line.Split(',');
            if (cells.Length != headers.Length) throw new InvalidDataException(path + " 列数错误");
            result.Add(headers.Select((h,i) => (h, cells[i])).ToDictionary(x => x.h, x => x.Item2));
        }
        return result;
    }
    private static float Number(Dictionary<string,string> r, string key) => r.TryGetValue(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && float.IsFinite(n) ? n : throw new InvalidDataException("非法数值: " + key);
    private static int Integer(Dictionary<string,string> r, string key) { var n = Number(r,key); return n == MathF.Truncate(n) ? checked((int)n) : throw new InvalidDataException("需要整数: " + key); }
}
