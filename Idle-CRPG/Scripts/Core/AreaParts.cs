using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record AreaPartSpec(string Id,string CastleId,bool InitiallyUnlocked);
public record AreaPartPrerequisite(string AreaPartId,string RequiredAreaPartId);

public sealed partial class Config
{
    public List<AreaPartSpec> AreaParts=new();
    public List<AreaPartPrerequisite> AreaPartPrerequisites=new();
    /// <summary>一个城堡的全部统治格子共同组成一个 areapart。</summary>
    public AreaPartSpec AreaPart(int region)=>AreaParts.Single(p=>p.CastleId==Rulers[region]);
    private void LoadAreaParts(string directory)
    {
        AreaParts=Read(Path.Combine(directory,"world/areapart.csv")).Select(r=>new AreaPartSpec(r["id"],r["castle_id"],Boolean(r,"initially_unlocked"))).ToList();
        AreaPartPrerequisites=Read(Path.Combine(directory,"world/areapart_prerequisite.csv"),true).Select(r=>new AreaPartPrerequisite(r["areapart_id"],r["required_areapart_id"])).ToList();
        LoadRegionPrerequisites(directory);
    }
    private void ValidateAreaParts()
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("areapart 配置："+why);}
        Need(AreaParts.Count==Dungeons.Count(d=>d.Castle)&&AreaParts.Select(p=>p.Id).Distinct().Count()==AreaParts.Count&&AreaParts.Select(p=>p.CastleId).Distinct().Count()==AreaParts.Count,"城堡与统治区必须一一对应");
        Need(AreaParts.All(p=>!string.IsNullOrWhiteSpace(p.Id)&&Dungeons.Any(d=>d.Castle&&d.Id==p.CastleId)),"ID/城堡引用错误");
        foreach(var part in AreaParts)
        {
            var cells=Regions.Where(r=>r.Enterable&&Rulers[r.Id]==part.CastleId).ToList();
            var reached=new HashSet<int>{cells[0].Id};var pending=new Queue<RegionSpec>();pending.Enqueue(cells[0]);
            while(pending.Count>0)
            {
                var cell=pending.Dequeue();foreach(var next in cells.Where(n=>Math.Abs(n.X-cell.X)+Math.Abs(n.Y-cell.Y)==1))if(reached.Add(next.Id))pending.Enqueue(next);
            }
            Need(reached.Count==cells.Count,"统治区必须连续，不得存在飞地");
            Need(Dungeons.Any(d=>!d.Castle&&Rulers[d.RegionId]==part.CastleId),"统治区必须配置普通地牢");
        }
        Need(AreaParts.Count(p=>p.InitiallyUnlocked)==1&&AreaPart(StartRegionId).InitiallyUnlocked,"仅出生统治区默认开放");
        Need(AreaPartPrerequisites.Distinct().Count()==AreaPartPrerequisites.Count,"重复前置关系");
        foreach(var edge in AreaPartPrerequisites)
        {
            var part=AreaParts.SingleOrDefault(p=>p.Id==edge.AreaPartId);var required=AreaParts.SingleOrDefault(p=>p.Id==edge.RequiredAreaPartId);
            Need(part!=null&&required!=null&&part!=required,"前置引用或自引用错误");
            Need(Regions.Where(r=>r.Enterable&&Rulers[r.Id]==part!.CastleId).Any(r=>Regions.Any(n=>n.Enterable&&Rulers[n.Id]==required!.CastleId&&Math.Abs(n.X-r.X)+Math.Abs(n.Y-r.Y)==1)),"前置统治区必须实际接壤（共边）");
        }
        var reachable=AreaParts.Where(p=>p.InitiallyUnlocked).Select(p=>p.Id).ToHashSet();
        bool changed;
        do{changed=false;foreach(var edge in AreaPartPrerequisites)if(reachable.Contains(edge.RequiredAreaPartId))changed|=reachable.Add(edge.AreaPartId);}while(changed);
        Need(RegionPrerequisites.Count>0||reachable.Count==AreaParts.Count,"存在无法从初始统治区解锁的孤立前置链");
        ValidateRegionPrerequisites();
    }
}

public sealed partial class WorldProgress
{
    /// <summary>多个前置按 OR 处理，必须攻克前置城堡；仅清空普通地牢（橙色）不算通关。</summary>
    public bool AreaPartUnlocked(Config c,string id)
    {
        var part=c.AreaParts.Single(p=>p.Id==id);
        var configured=c.RegionPrerequisites.Where(p=>c.Rulers[p.RegionId]==part.CastleId).ToArray();
        if(configured.Length>0)return configured.Any(p=>Cleared.Contains(p.RequiredDungeonId));
        return part.InitiallyUnlocked||c.AreaPartPrerequisites.Any(e=>e.AreaPartId==id&&Cleared.Contains(c.AreaParts.Single(p=>p.Id==e.RequiredAreaPartId).CastleId));
    }
    public HashSet<int> AccessibleRegions(Config c)
    {
        var castles=c.AreaParts.Where(p=>AreaPartUnlocked(c,p.Id)).Select(p=>p.CastleId).ToHashSet();
        return c.Regions.Where(r=>r.Enterable&&castles.Contains(c.Rulers[r.Id])).Select(r=>r.Id).ToHashSet();
    }
}
