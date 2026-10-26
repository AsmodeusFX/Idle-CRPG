using System;
using System.IO;
using IdleCrpg.Core;

/// <summary>仅显式旧回归组合归档技能；正式加载器永远不读取 TEST_ONLY 基线。</summary>
public static class LegacySkillConfig
{
    public static string DirectoryFor(string source)=>Copy(source,true);
    /// <summary>正式配置副本不叠加退役基线；Load 必须用它验证生产表，避免旧回归掩盖误删。</summary>
    private static string ProductionDirectoryFor(string source)=>Copy(source,false);
    private static string Copy(string source,bool mergeRetiredBaseline)
    {
        // QA 校验会修改源目录，同一路径也必须重新复制，不能复用已加载的配置副本。
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
        if(!string.Equals(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar),Path.Combine(root,"Idle-CRPG/config").Replace('/',Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))return source;
        var directory=Path.Combine(root,"artifacts/qa/"+(mergeRetiredBaseline?"legacy-skills-":"production-skills-")+Guid.NewGuid().ToString("N"));
        foreach(var file in Directory.GetFiles(source,"*.csv",SearchOption.AllDirectories))
        {var target=Path.Combine(directory,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
        if(mergeRetiredBaseline)foreach(var file in Directory.GetFiles(Path.Combine(root,"tests/fixtures/retired_skill_baseline_2026_10_22/skill"),"*.csv"))File.Copy(file,Path.Combine(directory,"skill",Path.GetFileName(file)),true);
        if(mergeRetiredBaseline)
        {
            // 旧技能基线不含2026-10-23新增的怪物技能，章节内容层表在旧组合里没有来源，
            // 这里显式移除而不是放宽生产校验：正式 Load 仍会拒绝未知技能引用。
            // A2 的 Boss 阶段表同样引用新增阶段技能，因此一并移除。
            foreach(var relative in new[]{"monster/act_behaviors.csv","monster/elite.csv","monster/boss_skill.csv","monster/boss_phase.csv"})
            {var path=Path.Combine(directory,relative);if(File.Exists(path))File.Delete(path);}
            foreach(var act in new[]{"act1","act2","act3","act4","act5"})
            {var path=Path.Combine(directory,"chapters",act,"monster/region_pool.csv");if(File.Exists(path))File.Delete(path);}
            // 旧技能基线没有 C2-B 新增的卷轴/药剂 Buff：把消耗品表收敛回旧组合里存在的一卷一药，
            // 不放宽生产校验（正式 Load 仍要求 Buff 引用有效）。
            File.WriteAllText(Path.Combine(directory,"item/scroll.csv"),
                "id,name,effect,damage_pct,element,radius,chain_targets,buff_id,target,drop_chance\n"+
                "scroll_bolt,闪电卷轴,damage,1.8,Physical,0,0,,enemy,0.12\n");
            File.WriteAllText(Path.Combine(directory,"item/potion.csv"),
                "id,name,effect,duration_ticks,value,drop_chance\n"+
                "potion_might,力量药剂,attack,600,0.25,0.12\n");
            // 旧技能基线也没有 C2-B 的 31 级掉落表：移除后用原型整体概率，保持旧组合自洽。
            var dropTable=Path.Combine(directory,"item/drop_table.csv");if(File.Exists(dropTable))File.Delete(dropTable);
        }
        return directory;
    }
    private static Config Historical(Config config)
    {
        foreach(var map in config.ChapterMaps){map.LegacySkillRuntime=true;map.Stars.AllowRetiredPurchases=true;}
        return config;
    }
    public static Config LoadLegacy(string source)=>Historical(Config.LoadLegacy(DirectoryFor(source)));
    public static Config Load(string source)=>Historical(Config.Load(ProductionDirectoryFor(source)));
}
