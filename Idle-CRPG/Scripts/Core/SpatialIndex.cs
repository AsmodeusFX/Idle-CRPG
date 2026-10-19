using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class Config
{
    private Dictionary<int,RegionSpec> regionIndex=new();
    private RegionSpec[,]? regionGrid;
    private HashSet<(int,int)> gateIndex=new();
    /// <summary>配置校验后构建只读空间索引；避免每个导航子格反复扫描全部世界格子。</summary>
    private void IndexWorld()
    {
        regionIndex=Regions.ToDictionary(r=>r.Id);regionGrid=new RegionSpec[WorldWidth,WorldHeight];
        foreach(var r in Regions)regionGrid[r.X,r.Y]=r;
        gateIndex=Gates.Select(g=>(Math.Min(g.From,g.To),Math.Max(g.From,g.To))).ToHashSet();
    }
    public RegionSpec RegionAt(int x,int y)=>regionGrid?[x,y]??Regions.Single(r=>r.X==x&&r.Y==y);
    public RegionSpec? RegionById(int id)=>regionIndex.GetValueOrDefault(id)??Regions.SingleOrDefault(r=>r.Id==id);
}
