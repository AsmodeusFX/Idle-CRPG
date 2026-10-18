using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>显式POI排程及原型内容定义；等级来自配置，绝不依据英雄等级或预算发放奖励。</summary>
public record PoiProfile(string Id,bool Optional,bool RequiredForCastle,int Floors,int Order,string Boss,string Interior,string Status);
public record PoiChallenge(string Poi,int Difficulty,int Challenge,int Recommended);
public record MapGate(string Id,int From,int To);
public record AreaStyle(string Castle,int Index,string Name,string Color);
public sealed partial class Config
{
    public int TownRegionId {get;private set;}
    public string MainClearPoi {get;private set;}="";
    public int MapVersion {get;private set;}=1;
    public List<MapGate> Gates=new();
    public Dictionary<string,PoiProfile> PoiProfiles=new();
    public List<PoiChallenge> PoiChallenges=new();
    public Dictionary<string,AreaStyle> AreaStyles=new();
    public bool IsTown(int region)=>region==TownRegionId;
    public bool Optional(string id)=>PoiProfiles.TryGetValue(id,out var profile)&&profile.Optional;
    public bool MainCleared(WorldProgress p)=>MainClearPoi==""?Dungeons.All(d=>p.Cleared.Contains(d.Id)):p.Cleared.Contains(MainClearPoi);
    public PoiChallenge Challenge(string id,int difficulty)=>PoiChallenges.SingleOrDefault(p=>p.Poi==id&&p.Difficulty==difficulty)??new(id,difficulty,Dungeons.Single(d=>d.Id==id).LevelMin,Dungeons.Single(d=>d.Id==id).LevelMin);
    public VillageSpec ReturnVillage(int region)=>Town!=null?Villages.Single(v=>v.RegionId==StartRegionId):Villages.FirstOrDefault(v=>v.RegionId==region)??Villages.Single(v=>v.RegionId==StartRegionId);
    /// <summary>同区四邻接直接通行，跨区只能经过显式关口；无Gate表的旧地图维持原导航。</summary>
    public bool CanCross(int from,int to)
    {
        var first=Regions.SingleOrDefault(r=>r.Id==from);var second=Regions.SingleOrDefault(r=>r.Id==to);
        if(first is not {Enterable:true}||second is not {Enterable:true})return false;
        if(from==to)return true;
        if(Math.Abs(first.X-second.X)+Math.Abs(first.Y-second.Y)!=1)return false;
        return Gates.Count==0||Rulers.TryGetValue(from,out var a)&&Rulers.TryGetValue(to,out var b)&&a==b||Gates.Any(g=>g.From==from&&g.To==to||g.From==to&&g.To==from);
    }
    private void LoadPoiConfiguration(string directory)
    {
        TownRegionId=0;MainClearPoi="";MapVersion=1;Gates=new();PoiProfiles=new();PoiChallenges=new();FloorPlans=new();AreaStyles=new();
        string path=Path.Combine(directory,"world/map_policy.csv");if(!File.Exists(path))return;
        var policy=CsvTable.Load(path,File.ReadAllText(path),"id","value");TownRegionId=policy.Require("town_region_id").Integer("value");MainClearPoi=policy.Require("main_clear_poi").Required("value");MapVersion=policy.Require("version").Integer("value");
        path=Path.Combine(directory,"world/gate.csv");Gates=CsvTable.Load(path,File.ReadAllText(path),"id","from_region","to_region").Rows.Select(r=>new MapGate(r.Id,r.Integer("from_region"),r.Integer("to_region"))).ToList();
        path=Path.Combine(directory,"dungeon/poi_profile.csv");
        PoiProfiles=CsvTable.Load(path,File.ReadAllText(path),"id","optional","required_for_castle","floors","auto_order","boss_name","interior","status").Rows.Select(r=>new PoiProfile(r.Id,r["optional"] switch {"0"=>false,"1"=>true,_=>throw r.Error("optional","必须为0或1")},r["required_for_castle"] switch {"0"=>false,"1"=>true,_=>throw r.Error("required_for_castle","必须为0或1")},r.Integer("floors"),r.Integer("auto_order"),r["boss_name"],r["interior"],r.Required("status"))).ToDictionary(p=>p.Id);
        path=Path.Combine(directory,"dungeon/poi_challenge.csv");
        PoiChallenges=CsvTable.Load(path,File.ReadAllText(path),"id","poi_id","difficulty","challenge","recommended").Rows.Select(r=>new PoiChallenge(r.Required("poi_id"),r.Integer("difficulty"),r.Integer("challenge"),r.Integer("recommended"))).ToList();
        LoadFloorPlans(directory);
        path=Path.Combine(directory,"world/area_style.csv");AreaStyles=CsvTable.Load(path,File.ReadAllText(path),"id","region_index","name","color").Rows.Select(r=>new AreaStyle(r.Id,r.Integer("region_index"),r.Required("name"),r.Required("color"))).ToDictionary(a=>a.Castle);
    }
    private void ValidatePoiConfiguration()
    {
        if(PoiProfiles.Count==0)return;
        if(AreaStyles.Count!=AreaParts.Count||AreaStyles.Values.Any(a=>a.Index<1||a.Color.Length!=6||a.Color.Any(ch=>!Uri.IsHexDigit(ch))))throw new InvalidDataException("区域显示配置非法");
        if(!Regions.Any(r=>r.Id==TownRegionId&&r.Enterable)||TownRegionId!=StartRegionId||!Dungeons.Any(d=>d.Id==MainClearPoi&&d.Castle)||PoiProfiles.Count!=Dungeons.Count||Dungeons.Any(d=>!PoiProfiles.ContainsKey(d.Id))||PoiProfiles.Values.Any(p=>p.Floors<1||p.Floors>20||p.Order<0||p.Status is not ("prototype" or "blocked")))throw new InvalidDataException("POI内容或营地配置无效");
        if(PoiChallenges.Count!=Dungeons.Count*3||PoiChallenges.Select(p=>(p.Poi,p.Difficulty)).Distinct().Count()!=PoiChallenges.Count||PoiChallenges.Any(p=>!PoiProfiles.ContainsKey(p.Poi)||p.Difficulty<0||p.Difficulty>2||p.Challenge<1||p.Challenge>MaxConfiguredLevel||p.Recommended<1))throw new InvalidDataException("POI三难度等级缺失或越界");
        foreach(var gate in Gates)
        {
            var a=Regions.SingleOrDefault(r=>r.Id==gate.From);var b=Regions.SingleOrDefault(r=>r.Id==gate.To);
            if(a==null||b==null||!a.Enterable||!b.Enterable||Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)!=1||Rulers.GetValueOrDefault(a.Id)==Rulers.GetValueOrDefault(b.Id))throw new InvalidDataException("跨区关口端点或归属无效");
        }
        if(Gates.Select(g=>(Math.Min(g.From,g.To),Math.Max(g.From,g.To))).Distinct().Count()!=Gates.Count)throw new InvalidDataException("重复关口");
    }
    private const int MaxConfiguredLevel=10000;
}
