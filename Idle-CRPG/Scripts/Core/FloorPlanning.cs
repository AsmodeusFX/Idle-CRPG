using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>V0.5规划数据，不等同于已生成房间、怪物或可领取奖励。</summary>
public record FloorPlan(string Poi,int Difficulty,int Index,string Name,int Rows,int Columns,int Active,int Combat,int Elite,int Arena,int MonsterLevel,int BossActors,int MinimumKills,int TargetKills,int MaximumKills);
public record DifficultyBand(string Difficulty,string Chapter,int Minimum,int Maximum,int RecommendedMinimum,int RecommendedMaximum);
public record FloorRunReport(string Poi,int Floor,int Seed,int PlannedCells,int GeneratedRooms,int GeneratedPassages,int PlannedKills,int ActualSpawned,int ActualKillFacts,long ElapsedTicks);
public sealed partial class Config
{
    public List<FloorPlan> FloorPlans=new();
    public List<DifficultyBand> DifficultyBands=new();
    private void LoadFloorPlans(string directory)
    {
        FloorPlans=new();string path=Path.Combine(directory,"dungeon/floor_plan.csv");if(!File.Exists(path))return;
        var columns=CsvTable.ReadRecords(path,File.ReadAllText(path))[0].Cells;
        foreach(var r in CsvTable.Load(path,File.ReadAllText(path),columns).Rows)
        {
            int N(string id)=>r.Integer(id);
            int difficulty=r.Required("DifficultyId") switch {"NORMAL"=>0,"NIGHTMARE"=>1,"HELL"=>2,_=>throw r.Error("DifficultyId","未知难度")};
            var p=new FloorPlan(r.Required("PoiId"),difficulty,N("FloorIndex"),r.Required("FloorDisplayName"),N("LayoutRows"),N("LayoutCols"),N("ActiveCellTarget"),N("CombatCellCount"),N("EliteCellCount"),N("BossArenaCellCount"),N("MonsterLevel"),N("BossActorCount"),N("PlannedKillMin"),N("PlannedKillTarget"),N("PlannedKillMax"));
            int sum=new[]{"Entry","Corridor","Combat","Elite","Event","Exit","BossArena"}.Sum(id=>N(id+"CellCount"));
            if(p.Rows<1||p.Columns<1||p.Active<1||p.Active>p.Rows*p.Columns||sum!=p.Active||new[]{"Entry","Corridor","Combat","Elite","Event","Exit","BossArena"}.Any(id=>N(id+"CellCount")<0)||p.BossActors is <0 or >1||(p.Arena>0)!=(p.BossActors==1)||p.MonsterLevel<1||p.MonsterLevel>MaxMonsterLevel||p.MinimumKills<0||p.MinimumKills>p.TargetKills||p.TargetKills>p.MaximumKills)throw r.Error("ActiveCellTarget","格数或预算不一致");
            foreach(string suffix in new[]{"Min","Target","Max"})
            {
                int expected=KillBudget(p.Combat,N("CombatMonstersPerCell"+suffix),p.Elite,N("ElitePerCell"),N("EliteEscortPerCell"+suffix),p.BossActors,N("BossEscort"+suffix));
                if(expected!=N("PlannedKill"+suffix))throw r.Error("PlannedKill"+suffix,"规划击杀预算错误");
            }
            FloorPlans.Add(p);
        }
        if(FloorPlans.Select(p=>(p.Poi,p.Difficulty,p.Index)).Distinct().Count()!=FloorPlans.Count)throw new InvalidDataException("重复楼层配置");
        foreach(var poi in PoiProfiles.Values)for(int difficulty=0;difficulty<3;difficulty++)
            if(!FloorPlans.Where(p=>p.Poi==poi.Id&&p.Difficulty==difficulty).OrderBy(p=>p.Index).Select(p=>p.Index).SequenceEqual(Enumerable.Range(1,poi.Floors)))throw new InvalidDataException("逐层配置不连续或缺失："+poi.Id);
        if(FloorPlans.Any(p=>!PoiProfiles.ContainsKey(p.Poi)))throw new InvalidDataException("楼层引用不存在POI");
        path=Path.Combine(directory,"dungeon/difficulty_bands.csv");columns=CsvTable.ReadRecords(path,File.ReadAllText(path))[0].Cells;
        DifficultyBands=CsvTable.Load(path,File.ReadAllText(path),columns).Rows.Select(r=>new DifficultyBand(r.Required("DifficultyId"),r.Required("ChapterId"),r.Integer("MonsterLevelMin"),r.Integer("MonsterLevelMax"),r.Integer("RecommendedPlayerLevelMin"),r.Integer("RecommendedPlayerLevelMax"))).ToList();
        if(DifficultyBands.Count!=15||DifficultyBands.Select(b=>(b.Difficulty,b.Chapter)).Distinct().Count()!=15||DifficultyBands.Any(b=>b.Minimum<1||b.Maximum<b.Minimum||b.RecommendedMinimum<1||b.RecommendedMaximum<b.RecommendedMinimum))throw new InvalidDataException("难度章节等级区间非法");
    }
    /// <summary>Boss按遭遇数计，Arena模块面积不参与乘法；该纯函数不修改任何进度或资源。</summary>
    public static int KillBudget(int combat,int combatCount,int elite,int eliteCount,int escorts,int bossActors,int bossEscorts)
    {
        if(new[]{combat,combatCount,elite,eliteCount,escorts,bossActors,bossEscorts}.Any(n=>n<0)||bossActors>1)throw new ArgumentOutOfRangeException(nameof(bossActors));
        return checked(combat*combatCount+elite*(eliteCount+escorts)+bossActors*(1+bossEscorts));
    }
}
public sealed partial class GameSession
{
    /// <summary>规划与实际分栏报告；当前生成器仍沿用原房间/走廊规模，不伪造达到规划格数。</summary>
    public IReadOnlyList<FloorRunReport> FloorReports()=>Floors.Select((f,index)=>
    {
        var plan=ActiveDungeon==null?null:Config.FloorPlans.FirstOrDefault(p=>p.Poi==ActiveDungeon.Id&&p.Difficulty==DifficultyIndex&&p.Index==index+1);
        return new FloorRunReport(ActiveDungeon?.Id??"",index+1,f.Seed,plan?.Active??0,f.Rooms.Count,f.Passages.Count,plan?.TargetKills??0,f.ActualSpawned,f.ActualKillFacts,f.ElapsedTicks);
    }).ToArray();
}
