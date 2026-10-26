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
            foreach(var relative in new[]{"monster/act_behaviors.csv","monster/elite.csv","monster/boss_skill.csv"})
            {var path=Path.Combine(directory,relative);if(File.Exists(path))File.Delete(path);}
            foreach(var act in new[]{"act1","act2","act3","act4","act5"})
            {var path=Path.Combine(directory,"chapters",act,"monster/region_pool.csv");if(File.Exists(path))File.Delete(path);}
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
