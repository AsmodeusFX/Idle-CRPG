using Godot;
using IdleCrpg.Core;
using System;
using System.IO;
using System.Linq;

public partial class Main
{
    /// <summary>仅自动化隔离会话的已授权前置；无正式玩家入口，不开放Lmin。</summary>
    private void SeedQaLmax(int maximum)
    {
        if(!automation||session==null)throw new InvalidOperationException("仅QA隔离会话允许设置测试前置");
        var state=session.CaptureProgress();state.MonsterLevelMax=maximum;session=GameSession.Restore(config,state);
    }
    /// <summary>隔离QA存档的真实Godot控件流程；只用明确测试余额，不将夹具冒充正式随机现场。</summary>
    private async void RunFirstBatchSmoke()
    {
        try
        {
            void Need(bool ok,string why){if(!ok)throw new Exception("FIRST_BATCH_UI: "+why);}
            string output=QaOutput.NewDirectory(ProjectSettings.GlobalizePath("res://.."),"first-batch-ui");
            async System.Threading.Tasks.Task Picture(string name)
            {
                Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var error=GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output,name+".png"));Need(error==Error.Ok,"截图写入失败");
            }
            foreach(int count in new[]{1,3,4})
            {
                for(int i=0;i<partyCounts.Length;i++)partyCounts[i].Value=i==0?count:0;
                await Picture("setup-"+count);
            }
            foreach(var count in partyCounts)count.Value=0;start.EmitSignal(BaseButton.SignalName.Pressed);Need(session==null,"零人开局未拒绝");
            partyCounts[0].Value=4;start.EmitSignal(BaseButton.SignalName.Pressed);paused=true;
            Need(session?.Heroes.Count==4&&session.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==4,"四同职创队");
            await Picture("locked-team");
            session!.GrantGmResources(experience:10000);
            var fixture=session.CaptureProgress();fixture.Kp=100;fixture.KpEarned=100;session=GameSession.Restore(config,fixture);Refresh();
            long xp=session.PublicExperience;
            kpPurchase!.EmitSignal(BaseButton.SignalName.Pressed);Need(session.Kp==100&&session.MonsterLevelMax==1&&growthFeedback!.Text.Contains("AR不足"),"评级不足的UI拒绝");
            await Picture("kp-rating-rejected");
            upgradeCards[session.Heroes[0].HeroInstanceId].EmitSignal(BaseButton.SignalName.Pressed);
            Need(session.Heroes[0].Level==2&&session.Heroes.Skip(1).All(h=>h.Level==1)&&session.PublicExperience<xp,"指定英雄升级UI");
            await Picture("specified-hero-upgraded");
            foreach(var hero in session.Heroes.Skip(1))upgradeCards[hero.HeroInstanceId].EmitSignal(BaseButton.SignalName.Pressed);
            kpPurchase!.EmitSignal(BaseButton.SignalName.Pressed);Need(session.MonsterLevelMax==2&&session.Kp==90,"KP点击购买");
            await Picture("lmax-purchased");
            Need(gameHud.FindChild("ScrollQuickBar",true,false)!=null&&heroHealth.All(h=>h!=null)&&hudSkillBars.All(b=>b!=null),"七分区缺失");
            SaveNow(true);var stored=saveStore.Load(config)!;Need(stored.Run!=null&&stored.Run.MonsterLevelMax==2&&stored.Run.Kp==90,"真实存档接线");
            uiLayer.GetNode<Button>("GmMenuButton").EmitSignal(BaseButton.SignalName.Pressed);Need(gmRoot.Visible,"GM入口丢失");ToggleGm();
            uiLayer.GetNode<Button>("SettingsButton").EmitSignal(BaseButton.SignalName.Pressed);Need(settingsRoot.Visible,"设置入口丢失");await Picture("settings-abandon-and-clear");ToggleSettings();
            pause.EmitSignal(BaseButton.SignalName.Pressed);Need(!paused,"暂停入口丢失");pause.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print("FIRST_BATCH_UI_PASS output="+output+" window="+GetWindow().Size+" renderer="+DisplayServer.GetName()+" explicit_QA_fixture=true");QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
}
