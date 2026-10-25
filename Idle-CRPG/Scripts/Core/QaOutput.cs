using System;
using System.IO;

namespace IdleCrpg.Core;

/// <summary>自动工具只写工作区artifacts/qa；拒绝历史归档、源配置及已存在的输出，避免覆盖取证。</summary>
public static class QaOutput
{
    public static string NewDirectory(string workspace,string purpose,string? requested=null)
    {
        string qa=Path.GetFullPath(Path.Combine(workspace,"artifacts","qa"));
        string target=Path.GetFullPath(requested??Path.Combine(qa,purpose+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+Guid.NewGuid().ToString("N")));
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(!target.StartsWith(qa+Path.DirectorySeparatorChar,comparison))throw new InvalidOperationException("自动输出必须使用artifacts/qa下的新目录，禁止覆盖历史归档或生产配置");
        // 不允许通过已有目录连接/符号链接逃出QA根。
        for(string? current=target;current!=null;current=Path.GetDirectoryName(current))
            if(Directory.Exists(current)&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("QA输出路径不能经过目录链接");
        if(Directory.Exists(target)||File.Exists(target))throw new InvalidOperationException("输出目标已经存在，请使用新的QA目录");
        Directory.CreateDirectory(target);return target;
    }
}
