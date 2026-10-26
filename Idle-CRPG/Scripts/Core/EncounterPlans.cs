using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>
/// 逐层遭遇计划（S15-C2-A）：只做"同一生成事务里的内容选择偏置"，
/// 不持有怪物数量/等级/奖励的第二权威——数量仍在 spawn_rule 区间内，等级仍取 Run.[Lmin,Lmax]。
/// <para>
/// A2 追加两个字段：<c>Threat</c> 记录 S09/S10 的威胁形态名（出处账本用，不参与数值），
/// <c>Composition</c> 是身份档位循环（M近战/R远程/H重型/S辅助），总数仍由 spawn_rule 决定。
/// </para>
/// </summary>
public sealed record EncounterPlan(int Chapter,string Poi,int Floor,string Rhythm,string Pattern,string ElitePurpose,int CountBias,int EliteBias,string PoolBias,string Threat="",string Composition="");

public sealed partial class Config
{
    public Dictionary<string,EncounterPlan> EncounterPlans { get; private set; }=new(StringComparer.Ordinal);
    /// <summary>城堡威胁组合：key 为 <c>章:POI:楼层</c>；floor=0 表示该城堡全部楼层默认。</summary>
    public Dictionary<string,EncounterPlan> CastleThreats { get; private set; }=new(StringComparer.Ordinal);

    /// <summary>按章:POI:楼层取遭遇计划；先取逐层计划，再取城堡威胁组合的精确楼层，最后取该城堡的默认行。</summary>
    public EncounterPlan? EncounterFor(int chapter,string poi,int floor)
    {
        string key=$"{chapter}:{poi}:{floor}";
        if(EncounterPlans.TryGetValue(key,out var plan))return plan;
        if(CastleThreats.TryGetValue(key,out var exact))return exact;
        return CastleThreats.TryGetValue($"{chapter}:{poi}:0",out var fallback)?fallback:null;
    }

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

    /// <summary>
    /// 读取城堡威胁组合（S09）：只描述"这个城堡各楼层的敌群由哪些职能档组成"，
    /// 与 226 层普通地牢节拍表分开登记，因此不改变普通层合同计数。
    /// </summary>
    private void LoadCastleThreats(string mapRoot)
    {
        CastleThreats=new(StringComparer.Ordinal);
        string path=Path.Combine(mapRoot,"dungeon/castle_threat.csv");
        if(!File.Exists(path))return;
        var records=CsvTable.ReadRecords(path,File.ReadAllText(path));
        string[] expected={"id","poi","floor","threat","composition","count_bias","elite_bias"};
        if(records.Count==0||!records[0].Cells.SequenceEqual(expected,StringComparer.Ordinal))
            throw new InvalidDataException("城堡威胁组合："+path+" 表头必须为 "+string.Join(',',expected));
        for(int i=1;i<records.Count;i++)
        {
            var (line,cells)=records[i];
            if(cells.Length!=expected.Length)throw new InvalidDataException($"城堡威胁组合：{path}:{line} 列数错误");
            string poi=cells[1].Trim(),threat=cells[3].Trim(),composition=cells[4].Trim();
            if(threat=="")throw new InvalidDataException($"城堡威胁组合：{path}:{line} 威胁形态为空");
            if(!Dungeons.Any(d=>d.Id==poi&&d.Castle))throw new InvalidDataException($"城堡威胁组合：{path}:{line} 引用的不是城堡 {poi}");
            if(!int.TryParse(cells[2],out int floor))throw new InvalidDataException($"城堡威胁组合：{path}:{line} 楼层非法");
            int maxFloor=FloorPlans.Where(p=>p.Poi==poi).Select(p=>p.Index).DefaultIfEmpty(1).Max();
            if(floor<0||floor>maxFloor)throw new InvalidDataException($"城堡威胁组合：{path}:{line} 楼层 {floor} 超出 {poi} 的 {maxFloor} 层");
            if(!int.TryParse(cells[5],out int countBias)||countBias is < -2 or > 2)throw new InvalidDataException($"城堡威胁组合：{path}:{line} 数量偏置越界");
            if(!int.TryParse(cells[6],out int eliteBias)||eliteBias is < -20 or > 30)throw new InvalidDataException($"城堡威胁组合：{path}:{line} 精英偏置越界");
            var tokens=composition.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(tokens.Length is < 1 or > 8||tokens.Any(t=>t is not ("M" or "R" or "H" or "S")))throw new InvalidDataException($"城堡威胁组合：{path}:{line} 身份档位循环非法");
            var plan=new EncounterPlan(ChapterIndex+1,poi,floor,"castle",threat,"",countBias,eliteBias,"mixed",threat,composition);
            if(!CastleThreats.TryAdd($"{ChapterIndex+1}:{poi}:{floor}",plan))throw new InvalidDataException($"城堡威胁组合：{path}:{line} 同一城堡楼层重复配置");
            if(EncounterPlans.ContainsKey($"{ChapterIndex+1}:{poi}:{floor}"))throw new InvalidDataException($"城堡威胁组合：{path}:{line} 与普通层节拍表冲突");
        }
    }

    /// <summary>威胁组合里的每一档都必须在本统治区投放池里真实存在，避免"写了组合但抽不出该档"。</summary>
    private void ValidateCastleThreats()
    {
        foreach(var plan in CastleThreats.Values)
        {
            if(plan.Composition=="")continue;
            var castle=Dungeons.Single(d=>d.Id==plan.Poi);
            if(!MonsterPools.TryGetValue(AreaPart(castle.RegionId).Id,out var pool)||pool.Count==0)continue;
            foreach(string token in plan.Composition.Split(';',StringSplitOptions.RemoveEmptyEntries))
            {
                string archetype=token switch {"M"=>"melee","R"=>"ranged","H"=>"heavy","S"=>"support",_=>throw new InvalidDataException("城堡威胁组合：未知档位 "+token)};
                if(!pool.Any(e=>ArchetypeOf(e.MonsterId)==archetype))
                    throw new InvalidDataException($"城堡威胁组合：{plan.Poi} 第 {plan.Floor} 层需要的 {archetype} 档在投放池中不存在");
            }
        }
    }
}
