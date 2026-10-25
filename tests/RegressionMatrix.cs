using System;
using System.Linq;
using System.Reflection;

public static class RegressionMatrix
{
    /// <summary>独立运行旧套件以收集规则迁移差异；任何失败仍令进程失败，不吞断言或跳过保护。</summary>
    public static void Run(string root)
    {
        int passed=0,failed=0;
        foreach(var type in typeof(RegressionMatrix).Assembly.GetTypes().Where(t=>t.IsAbstract&&t.IsSealed&&t.Name.EndsWith("Checks")&&t!=typeof(LegacyRunMigrationChecks)).OrderBy(t=>t.Name))
        {
            var method=type.GetMethod("Run",new[]{typeof(string)});if(method==null)continue;
            try{method.Invoke(null,new object[]{root});Console.WriteLine("MATRIX_PASS "+type.Name);passed++;}
            catch(TargetInvocationException e){Console.WriteLine("MATRIX_FAIL "+type.Name+" "+e.InnerException);failed++;}
        }
        Console.WriteLine($"REGRESSION_MATRIX passed={passed} failed={failed}");if(failed>0)throw new Exception("回归差异尚未关闭："+failed);
    }
}
