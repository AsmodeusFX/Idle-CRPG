using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class Main
{
    /// <summary>
    /// QA 隔离存档：写入一份与真实旧档同形的超额档（5 名在阵、无实例ID、无候补），
    /// 只在 automation 目录下生成，用来复现 C-LEGACY-5 的迁移选择框真实控件现场。
    /// </summary>
    private void SeedLegacyMigrationFixture()
    {
        if(!automation)throw new InvalidOperationException("仅QA隔离会话允许写入旧档夹具");
        var world=GameSession.CreateWorld(config,20261017,new[]{"ranger","warrior","mage","priest"});
        var snapshot=world.CaptureProgress();
        snapshot.RunVersion=0;
        snapshot.RunId="";
        foreach(var hero in snapshot.Heroes){hero.HeroInstanceId="";hero.Level=49;}
        // 旧版本允许第5名职业在阵：按旧档原样追加，实例ID留空模拟未迁移前状态。
        var fifth=snapshot.Heroes[3].Copy();fifth.SpecId="necromancer";fifth.Level=49;fifth.HeroInstanceId="";
        snapshot.Heroes.Add(fifth);
        snapshot.ReserveHeroes.Clear();
        snapshot.PendingFormation=Array.Empty<string>();
        saveStore.WriteEncoded(SaveStore.Encode(new(){Session=snapshot}));
    }

    /// <summary>
    /// 旧档迁移选择框的真实控件回归：弹窗必须完整落在视口内（按钮可点）、默认勾选可用、
    /// 取消后创角入口不锁死、再次点“开始游戏”能重开选择框、确认后按选择恢复本轮。
    /// </summary>
    private async void RunLegacyMigrationSmoke()
    {
        try
        {
            void Need(bool ok,string why){if(!ok)throw new Exception("LEGACY_MIGRATION_UI: "+why);}
            string output=QaOutput.NewDirectory(ProjectSettings.GlobalizePath("res://.."),"legacy-migration");
            async System.Threading.Tasks.Task Shot(string name)
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var error=GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output,name+".png"));
                Need(error==Error.Ok,"截图写入失败");
            }
            async System.Threading.Tasks.Task Wait()
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            await Wait();
            var viewport=GetViewport().GetVisibleRect().Size;
            var dialog=legacyDialog;
            Need(dialog!=null&&dialog.Visible,"旧档迁移选择框没有出现");
            var ok=dialog!.GetOkButton();
            var cancel=dialog.GetCancelButton();
            var rect=new Rect2(dialog.Position,dialog.Size);
            Need(rect.Position.Y>=0&&rect.End.Y<=viewport.Y&&rect.End.X<=viewport.X&&rect.Size.X<=viewport.X,"迁移框超出视口："+rect+" viewport="+viewport);
            Need(ok.IsVisibleInTree()&&cancel.IsVisibleInTree(),"迁移框确认/取消按钮不可见");
            // 嵌入式子窗口内控件的矩形以窗口左上角为原点，按钮必须整体落在窗口高度内才可能点到。
            Need(ok.GetGlobalRect().End.Y<=dialog.Size.Y+1,"确认按钮落在窗口可视区之外："+ok.GetGlobalRect());
            Need(!ok.Disabled,"默认未勾选英雄：确认按钮不可用");
            var boxes=CheckBoxes(dialog);
            Need(boxes.Count==5,"候选英雄数量与旧档不一致："+boxes.Count);
            Need(boxes.Count(b=>b.ButtonPressed)==4,"默认勾选数量不是4："+boxes.Count(b=>b.ButtonPressed));
            await Shot("legacy-migration-dialog");

            // 取消：旧档不改，创角入口必须仍可用，并可从“开始游戏”重开选择框。
            cancel.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait();
            Need(start.Visible&&!start.Disabled,"取消迁移后开始游戏入口被锁死");
            start.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait();
            Need(legacyDialog!=null&&legacyDialog.Visible,"点开始游戏没有重新打开迁移选择");
            dialog=legacyDialog!;ok=dialog.GetOkButton();boxes=CheckBoxes(dialog);

            // 上限：勾满5名时确认必须禁用，回到4名恢复。
            boxes[4].ButtonPressed=true;await Wait();
            Need(ok.Disabled,"超过4名仍可确认迁移");
            boxes[4].ButtonPressed=false;await Wait();
            Need(!ok.Disabled,"回到4名后确认按钮仍被禁用");

            // 失败路径：迁移事务报错时弹窗必须留在原地并说明原因，不能静默消失让玩家无出口。
            var run=pendingLegacy!.Document.Run!;
            var originals=run.Heroes.ToList();
            run.Heroes.Clear();
            ok.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait();
            Need(legacyDialog!=null&&legacyDialog.Visible,"迁移失败后选择框消失，玩家无法重试");
            Need(help.Text.Contains("迁移失败"),"迁移失败没有给出原因");
            run.Heroes.AddRange(originals);
            await Wait();

            ok.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait();
            Need(session!=null&&session.Heroes.Count==4,"确认迁移后没有按选择恢复本轮队伍");
            Need(saveStore.Legacy.Heroes.Count==1,"冻结隔离区数量不是1："+saveStore.Legacy.Heroes.Count);
            Need(!start.Visible,"迁移成功后创角入口仍显示");
            Need(saveStore.Load(config)?.Session!=null,"迁移结果没有落盘");
            await Shot("legacy-migration-confirmed");
            GD.Print($"LEGACY_MIGRATION_UI_PASS party={session!.Heroes.Count} frozen={saveStore.Legacy.Heroes.Count} dialog={rect} output={output}");
            QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }

    /// <summary>迁移选择框里的候选英雄勾选框（按容器命名定位，避免误取 AcceptDialog 内部控件）。</summary>
    private static List<CheckBox> CheckBoxes(Window dialog)
    {
        var columns=dialog.FindChild("LegacyColumns",true,false) as Control;
        return columns==null?new List<CheckBox>():columns.GetChildren().OfType<CheckBox>().ToList();
    }
}
