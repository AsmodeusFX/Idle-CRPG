using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public record DifficultyDefinition(string Id,string Name);
public record ChapterDefinition(string Id,string Name,string Terrain,int Width,int Height,string Directory);

public sealed partial class Config
{
    public IReadOnlyList<DifficultyDefinition> Difficulties { get; private set; }=Array.Empty<DifficultyDefinition>();
    public IReadOnlyList<ChapterDefinition> Chapters { get; private set; }=Array.Empty<ChapterDefinition>();
    public IReadOnlyList<Config> ChapterMaps { get; private set; }=Array.Empty<Config>();
    public int ChapterIndex { get; private set; }

    /// <summary>章节地图独立加载；共享战斗定义，地图实例表和统治区引用严格局限本章。</summary>
    private void LoadChapters(string directory,bool legacyFixture)
    {
        Difficulties=Read(Path.Combine(directory,"world/difficulty.csv")).Select(r=>new DifficultyDefinition(r["id"],r["name"])).ToArray();
        Chapters=Read(Path.Combine(directory,legacyFixture?"legacy/chapter.csv":"world/chapter.csv")).Select(r=>new ChapterDefinition(r["id"],r["name"],r["terrain"],Integer(r,"width"),Integer(r,"height"),r["directory"])).ToArray();
        if(!Difficulties.Select(d=>d.Id).SequenceEqual(new[]{"normal","nightmare","hell"})||!Chapters.Select(c=>c.Id).SequenceEqual(new[]{"act1","act2","act3","act4","act5"}))throw new InvalidDataException("难度和章节稳定ID及顺序不得变更；显示名称可配置");
        if(Difficulties.Count!=3||Chapters.Count!=5||Difficulties.Select(d=>d.Id).Distinct().Count()!=3||Chapters.Select(c=>c.Id).Distinct().Count()!=5||Difficulties.Any(d=>string.IsNullOrWhiteSpace(d.Id)||string.IsNullOrWhiteSpace(d.Name))||Chapters.Any(c=>string.IsNullOrWhiteSpace(c.Id)||string.IsNullOrWhiteSpace(c.Name)||c.Width<1||c.Width>64||c.Height<1||c.Height>64||!new[]{"mixed","grass","desert","tropical","coast","snow","hell"}.Contains(c.Terrain)))throw new InvalidDataException("必须配置3档难度及5个有效独立章节");
        if(legacyFixture)Chapters=Chapters.Select((c,i)=>i==0?c with {Width=WorldWidth,Height=WorldHeight,Directory="."}:c).ToArray();
        if(Chapters.Select(c=>Path.GetFullPath(Path.Combine(directory,c.Directory))).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=5)throw new InvalidDataException("每章必须有独立地图目录");
        var maps=new List<Config>();
        for(int i=0;i<Chapters.Count;i++)
        {
            var def=Chapters[i];
            if(i==0&&legacyFixture){maps.Add(this);continue;}
            string mapRoot=Path.GetFullPath(Path.Combine(directory,def.Directory));
            if(!mapRoot.StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("章节目录不得离开config");
            var map=(Config)MemberwiseClone();map.ChapterIndex=i;map.LoadWorld(mapRoot,directory);map.LoadEncounterPlans(mapRoot);map.LoadSceneConfig(mapRoot,directory);
            // 区域投放池按章加载；缺失表示该章仍用全局怪物表（legacy 夹具与旧图不受影响）。
            map.LoadMonsterPools(mapRoot);map.ValidateMonsterPools();
            if(!legacyFixture){map.LoadTown(directory,i);map.LoadWaypoints(mapRoot);}
            map.Spawn=map.Spawn with {Levels=MonsterLevels};
            if(map.Dungeons.Any(d=>d.LevelMax+(long)Spawn.Level-1>MaxMonsterLevel))throw new InvalidDataException("章节怪物等级超过等级表");
            maps.Add(map);
        }
        for(int i=0;i<maps.Count;i++)
        {
            var map=maps[i];var def=Chapters[i];
            if(map.WorldWidth!=def.Width||map.WorldHeight!=def.Height||def.Terrain!="mixed"&&map.Regions.Any(r=>r.Enterable&&r.Terrain!=def.Terrain))throw new InvalidDataException("章节地图尺寸/地形与章节配置不一致");
            map.ChapterMaps=maps;
        }
        ChapterMaps=maps;
    }
}
