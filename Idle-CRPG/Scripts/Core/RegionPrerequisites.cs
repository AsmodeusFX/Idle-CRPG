using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record RegionPrerequisite(int RegionId,string RequiredDungeonId);
public sealed partial class Config
{
    public List<RegionPrerequisite> RegionPrerequisites=new();
    private void LoadRegionPrerequisites(string directory)
    {
        string path=Path.Combine(directory,"world/region_prerequisite.csv");
        RegionPrerequisites=CsvTable.Load(path,File.ReadAllText(path),"id","region_id","required_dungeon_id").Rows.Select(r=>new RegionPrerequisite(r.Integer("region_id"),r.Required("required_dungeon_id"))).ToList();
    }
    /// <summary>格子前置按所属区域的 unlock_mode 聚合；显式配置覆盖旧门禁，不由相邻、探索或已占领推导开放。</summary>
    private void ValidateRegionPrerequisites()
    {
        if(RegionPrerequisites.Distinct().Count()!=RegionPrerequisites.Count||RegionPrerequisites.Any(p=>!Regions.Any(r=>r.Id==p.RegionId&&r.Enterable&&!IsTown(r.Id))||!Dungeons.Any(d=>d.Id==p.RequiredDungeonId)))throw new InvalidDataException("地块前置重复或引用无效");
        if(RegionPrerequisites.Any(p=>AreaPart(p.RegionId).InitiallyUnlocked||Rulers[p.RegionId]==Rulers[Dungeons.Single(d=>d.Id==p.RequiredDungeonId).RegionId]))throw new InvalidDataException("初始区域不得设置门禁，前置地牢不得属于自身统治区域");
        var progress=new WorldProgress();var reached=new HashSet<int>();
        bool changed;
        do
        {
            var allowed=progress.AccessibleRegions(this);var queue=new Queue<int>();queue.Enqueue(StartRegionId);
            while(queue.Count>0)
            {
                int id=queue.Dequeue();if(!allowed.Contains(id)||!reached.Add(id))continue;
                foreach(var n in ExpeditionPlanner.Neighbors(this,id))queue.Enqueue(n.Id);
            }
            int before=progress.Cleared.Count;
            progress.Cleared.UnionWith(Dungeons.Where(d=>reached.Contains(d.RegionId)).Select(d=>d.Id));changed=progress.Cleared.Count>before;
            // 每轮重新遍历已开放图，允许新增门禁解除后从旧路径继续扩展。
            if(changed)reached.Clear();
        }while(changed);
        if(progress.Cleared.Count!=Dungeons.Count)throw new InvalidDataException("前置地牢门禁或不可进入地块造成地图推进不可达");
    }
}
