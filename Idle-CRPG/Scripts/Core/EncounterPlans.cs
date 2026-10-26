using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>
/// 逐层遭遇计划（S15-C2-A）：只做"同一生成事务里的内容选择偏置"，
/// 不持有怪物数量/等级/奖励的第二权威——数量仍在 spawn_rule 区间内，等级仍取 Run.[Lmin,Lmax]。
/// </summary>
public sealed record EncounterPlan(int Chapter,string Poi,int Floor,string Rhythm,string Pattern,string ElitePurpose,int CountBias,int EliteBias,string PoolBias);

public sealed partial class Config
{
    public Dictionary<string,EncounterPlan> EncounterPlans { get; private set; }=new(StringComparer.Ordinal);

    /// <summary>按章:POI:楼层取遭遇计划；没有配置返回 null，调用方必须保持旧行为。</summary>
    public EncounterPlan? EncounterFor(int chapter,string poi,int floor)
        =>EncounterPlans.TryGetValue($"{chapter}:{poi}:{floor}",out var plan)?plan:null;

    /// <summary>读取章节逐层遭遇计划；文件缺失表示该章未投放，不报错也不改变任何生成结果。</summary>
    private void LoadEncounterPlans(string mapRoot)
    {
        EncounterPlans=new(StringComparer.Ordinal);
        string path=Path.Combine(mapRoot,"dungeon/floor_rhythm.csv");
        if(!File.Exists(path))return;
        // 同章多行共享 chapter 值，不能用"首列唯一键"的 CsvTable.Load；这里按无键记录逐行读取。
        var records=CsvTable.ReadRecords(path,File.ReadAllText(path));
        string[] expected={"chapter","poi","floor","rhythm","pattern","elite_purpose","count_bias","elite_bias","pool_bias"};
        if(records.Count==0||!records[0].Cells.SequenceEqual(expected,StringComparer.Ordinal))
            throw new InvalidDataException("逐层遭遇计划："+path+" 表头必须为 "+string.Join(',',expected));
        for(int i=1;i<records.Count;i++)
        {
            var (line,cells)=records[i];
            if(cells.Length!=expected.Length)throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 列数错误");
            if(!int.TryParse(cells[0],out int chapter)||chapter!=ChapterIndex+1)throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 章号与目录不一致");
            string poi=cells[1].Trim(),rhythm=cells[3].Trim(),pattern=cells[4].Trim(),purpose=cells[5].Trim(),poolBias=cells[8].Trim();
            if(!int.TryParse(cells[2],out int floor))throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 楼层非法");
            if(!int.TryParse(cells[6],out int countBias)||countBias is < -2 or > 2)throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 数量偏置越界");
            if(!int.TryParse(cells[7],out int eliteBias)||eliteBias is < -20 or > 30)throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 精英偏置越界");
            if(Dungeons.All(d=>d.Id!=poi))throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 引用了不存在的地牢 {poi}");
            int maxFloor=FloorPlans.Where(p=>p.Poi==poi).Select(p=>p.Index).DefaultIfEmpty(1).Max();
            if(floor<1||floor>maxFloor)throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 楼层 {floor} 超出 {poi} 的 {maxFloor} 层");
            if(poolBias is not ("melee" or "ranged" or "heavy" or "mixed"))throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 身份偏好非法");
            if(pattern=="")throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 遭遇形态为空");
            var plan=new EncounterPlan(chapter,poi,floor,rhythm,pattern,purpose,countBias,eliteBias,poolBias);
            if(!EncounterPlans.TryAdd($"{chapter}:{poi}:{floor}",plan))throw new InvalidDataException($"逐层遭遇计划：{path}:{line} 同一楼层重复配置");
        }
    }
}
