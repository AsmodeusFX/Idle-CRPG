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
            kpUnlock!.EmitSignal(BaseButton.SignalName.Pressed);Need(session.Kp==100&&session.MonsterLevelMax==1&&growthFeedback!.Text.Contains("AR不足"),"评级不足的UI拒绝");
            await Picture("kp-rating-rejected");
            upgradeCards[session.Heroes[0].HeroInstanceId].EmitSignal(BaseButton.SignalName.Pressed);
            Need(session.Heroes[0].Level==2&&session.Heroes.Skip(1).All(h=>h.Level==1)&&session.PublicExperience<xp,"指定英雄升级UI");
            await Picture("specified-hero-upgraded");
            foreach(var hero in session.Heroes.Skip(1))upgradeCards[hero.HeroInstanceId].EmitSignal(BaseButton.SignalName.Pressed);
            kpUnlock!.EmitSignal(BaseButton.SignalName.Pressed);Need(session.MonsterLevelMax==2&&session.Kp==90,"KP点击购买");
            await Picture("lmax-purchased");
            // WP5 装备卡：六部位各一件候选 → 出现"一键换装（6件）"，点击后实际穿戴并入档。
            foreach(var spec in config.EquipmentBases)session.QaOfferEquipment(session.Heroes[0].HeroInstanceId,spec.Id,3,"rare");
            Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var batchCard=uiLayer.FindChild("BatchEquip",true,false) as Button;Need(batchCard!=null&&!batchCard.Disabled,"批量换装卡缺失或被禁用");
            // 装备卡位于右栏滚动区底部：拍照前滚到底，让证据图真实可见。
            interactionScroll.ScrollVertical=(int)interactionScroll.GetVScrollBar().MaxValue;
            await Picture("equipment-batch");
            batchCard!.EmitSignal(BaseButton.SignalName.Pressed);
            Need(session.EquipmentTotals(session.Heroes[0]).Attack+session.EquipmentTotals(session.Heroes[0]).Hp>0,"批量换装未生效");
            await Picture("equipment-equipped");
            // WP6：卷轴库存显示与药剂槽点击（生效占槽、空槽灰态）。
            session.QaGrantConsumable(config.Scrolls[0].Id);
            session.QaGrantConsumable(config.Potions[0].Id);
            Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Need(gameHud.FindChild("ScrollQuickBar",true,false)!=null,"卷轴快捷栏缺失");
            var potionSlot=gameHud.FindChild("PotionSlot_0",true,false) as Button;Need(potionSlot!=null&&!potionSlot.Disabled,"药剂槽不可点");
            await Picture("consumables-slots");
            potionSlot!.EmitSignal(BaseButton.SignalName.Pressed);
            Need(session.PotionSlots[0].Active&&session.PotionAttackPct>0,"药剂点击未生效");
            await Picture("potions-active");
            // 右栏排序：英雄升级 → 装备 → KP购买 → 城堡（此处断言装备按钮在 KP 之前）。
            var equipNode=uiLayer.FindChild("BatchEquip",true,false);var kpNode=uiLayer.FindChild("BuyMonsterLevelMaximum",true,false);
            Need(equipNode!=null&&kpNode!=null&&equipNode.GetIndex()<kpNode.GetIndex(),"右栏顺序错误：装备应在KP购买之前");

            // 英雄页·装备页签：六部位详情 + 背包列表 + "穿戴更好装备"。
            session.QaOfferEquipment(session.Heroes[0].HeroInstanceId,config.EquipmentBases[0].Id,9,"legendary");
            Refresh();SelectStarView(4);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var tabs=heroPage.FindChild("装备",true,false)?.GetParent() as TabContainer;
            Need(tabs!=null,"英雄页缺少页签容器");
            tabs!.CurrentTab=tabs.GetTabIdxFromControl(heroPage.FindChild("装备",true,false) as Control);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var wearBest=heroPage.FindChild("WearBestEquipment",true,false) as Button;
            Need(wearBest!=null&&heroPage.FindChild("EquippedSlots",true,false)!=null&&heroPage.FindChild("BagItems",true,false)!=null,"装备页签结构缺失");
            await Picture("hero-equipment-bag");
            wearBest!.EmitSignal(BaseButton.SignalName.Pressed);
            Need(session.WornEquipment(session.Heroes[0],config.EquipmentBases[0].Slot)!=null,"穿戴更好装备未生效");
            await Picture("hero-equipment-worn");
            SelectStarView(0);
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
