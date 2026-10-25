using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using IdleCrpg.Core;

/// <summary>
/// C-LEGACY-5 冻结候补迁移：超额旧档必须由玩家选满 4 名，其余进入只读迁移隔离区；
/// 未选择前不改写原档，重复执行不重复冻结，放弃/重生不销毁冻结数据，只有"清除全部进度"会清除。
/// </summary>
public static class LegacySelectionChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("LEGACY_SELECTION: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        string directory=QaOutput.NewDirectory(root,"legacy-selection");string path=Path.Combine(directory,"progress.json");
        var store=new SaveStore(path);

        // 构造旧档：5 名在阵 + 1 名候补，且是尚未分配实例ID的旧格式。
        var seed=GameSession.CreateNewWorld(c,4242,new[]{"warrior","ranger","mage","warrior"});
        var snapshot=seed.CaptureProgress();
        snapshot.RunVersion=0;snapshot.RunId="";foreach(var hero in snapshot.Heroes)hero.HeroInstanceId="";
        var fifth=snapshot.Heroes[0].Copy();fifth.Name="旧档第五人";fifth.Level=7;fifth.HeroInstanceId="";
        snapshot.Heroes.Add(fifth);
        var reserve=snapshot.Heroes[1].Copy();reserve.Name="旧档候补";reserve.Level=9;reserve.HeroInstanceId="";
        snapshot.ReserveHeroes.Add(reserve);
        snapshot.PendingFormation=Array.Empty<string>();
        string legacyText=SaveStore.Encode(new(){Session=snapshot});
        store.WriteEncoded(legacyText);
        string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        // 未选择前：必须抛出需要选择，并且原档一字节不动。
        LegacySelectionRequiredException? pending=null;
        try{store.Load(c);}catch(LegacySelectionRequiredException e){pending=e;}
        Check(pending!=null,"超额旧档没有要求玩家选择");
        Check(pending!.Candidates.Count==5,$"候选人数应为5，实际 {pending.Candidates.Count}");
        Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))==before,"未选择前改写了原档");

        // 每次迁移尝试都从同一份旧档文本重新解码，模拟"重试/重复进入迁移界面"。
        SaveDocument DecodeLegacy()
        {
            try{SaveStore.Decode(c,legacyText);}catch(LegacySelectionRequiredException e){return e.Document;}
            throw new Exception("旧档未再要求选择");
        }

        // 选择 4 名：其余入隔离区；等级与职业保留，实例ID可追溯。
        int[] keep={0,1,2,4};
        var session=store.LoadWithLegacySelection(c,DecodeLegacy(),keep);
        Check(session.Heroes.Count==4&&session.Heroes.All(h=>!string.IsNullOrWhiteSpace(h.HeroInstanceId)),"保留队伍人数或实例ID错误");
        // 显示名由职业配置重建，身份以实例ID/职业/等级为准。
        Check(session.Heroes[3].Level==snapshot.Heroes[4].Level&&session.Heroes[3].SpecId==snapshot.Heroes[4].SpecId,$"选择结果与原始英雄不一致 keep={string.Join(',',keep)} got={string.Join('|',session.Heroes.Select(h=>h.SpecId+":"+h.Level))}");
        var frozen=store.Legacy;
        Check(frozen.Heroes.Count==2,$"隔离区应冻结2名（1在阵+1候补），实际 {frozen.Heroes.Count}");
        Check(frozen.Heroes.Any(h=>h.Level==9)&&frozen.Heroes.Any(h=>h.SpecId=="warrior"),$"冻结英雄等级或职业未保留 levels={string.Join(',',frozen.Heroes.Select(h=>h.SpecId+":"+h.Level))}");
        Check(frozen.Heroes.All(h=>!string.IsNullOrWhiteSpace(h.HeroInstanceId)&&h.HeroInstanceId.StartsWith("legacy-")),"冻结英雄缺少可追溯实例ID");
        Check(frozen.Notes.Count>0,"缺少迁移说明");

        // 读档幂等：再次读取不再要求选择，也不会重复冻结。
        var reloaded=store.Load(c);
        Check(reloaded?.Session!=null,"迁移后读档失败");
        Check(store.Legacy.Heroes.Count==2,"读档后冻结数量漂移");
        store.LoadWithLegacySelection(c,DecodeLegacy(),keep);
        Check(store.Legacy.Heroes.Count==2,"重复执行迁移重复冻结同一批英雄");

        // 换选（只留2名）：按来源槽位追加冻结，不与已冻结重复。
        var two=store.LoadWithLegacySelection(c,DecodeLegacy(),new[]{0,1});
        Check(two.Heroes.Count==2,"只保留2名时队伍人数错误");
        Check(store.Legacy.Heroes.Count==4,$"换选后应累计冻结4名（同槽位不重复），实际 {store.Legacy.Heroes.Count}");

        // 放弃本轮 + 新开一轮：隔离区必须保留（不得借放弃/重生悄悄销毁）。
        var account=two.AbandonRun();
        var next=GameSession.StartRun(c,99,new[]{"warrior"},account);
        store.Save(next);
        Check(store.Load(c)?.Session!=null,"放弃后新档读取失败");
        Check(store.Legacy.Heroes.Count==4,"放弃本轮后冻结数据被销毁");

        // 清除全部进度：唯一允许清除隔离区的入口。
        store.Reset();
        var cleared=store.Load(c);
        Check(cleared?.Session==null,"清除全部进度后仍残留本轮进度");
        Check(store.Legacy.Heroes.Count==0,"清除全部进度未清除迁移隔离区");

        Console.WriteLine("LEGACY_SELECTION_PASS: 5+1 旧档按玩家选择保留4名、冻结其余、未选择不改档、重跑幂等、放弃/重生保留、全清清除");
    }
}
