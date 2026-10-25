using System;
using System.IO;
using IdleCrpg.Core;

public static class ExecutionSafetyChecks
{
    public static void Run(string root)
    {
        string qa=QaOutput.NewDirectory(root,"wp0-protection");
        bool Reject(string path){try{QaOutput.NewDirectory(root,"test",path);return false;}catch(InvalidOperationException){return true;}}
        if(!Reject(Path.Combine(root,"docs/data",ParameterAudit.ArchiveFolder))||!Reject(Path.Combine(root,"Idle-CRPG/config"))||!Reject(qa))throw new Exception("QA输出保护失败");
        string copy=Path.Combine(qa,"config");
        foreach(string relative in new[]{StarConfiguration.ContentPath,StarConfiguration.LayoutPath,StarConfiguration.PlanPath})
        {string target=Path.Combine(copy,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(root,"Idle-CRPG/config",relative),target);}
        var store=new StarEditorStore(copy);var draft=store.Load();store.Save(draft,new StarProgressSave(Array.Empty<StarRankSave>()));
        Console.WriteLine("WP0_SAFETY_PASS: archived/source/existing output rejected, QA star editor save validated");
    }
}
