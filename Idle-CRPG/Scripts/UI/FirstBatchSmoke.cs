using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
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
            File.WriteAllText(Path.Combine(output,"screenshots.csv"),"name,window_width,window_height,viewport_width,viewport_height,png_width,png_height\n");
            async System.Threading.Tasks.Task Picture(string name)
            {
                Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var shot=GetViewport().GetTexture().GetImage();var error=shot.SavePng(Path.Combine(output,name+".png"));Need(error==Error.Ok,"截图写入失败");
                var viewport=GetViewport().GetVisibleRect().Size;var window=GetWindow().Size;
                File.AppendAllText(Path.Combine(output,"screenshots.csv"),$"{name},{window.X},{window.Y},{viewport.X},{viewport.Y},{shot.GetWidth()},{shot.GetHeight()}\n");
            }
            async System.Threading.Tasks.Task Click(Button button)
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var point=button.GetGlobalTransformWithCanvas()*(button.Size/2);
                GetViewport().PushInput(new InputEventMouseMotion {Position=point,GlobalPosition=point},true);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                GetViewport().PushInput(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true},true);
                GetViewport().PushInput(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false},true);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
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
            interactionScroll.ScrollVertical=(int)kpUnlock.Position.Y;
            await Picture("lmax-purchased");
            Need(kpUnlock.Text.Contains("AR≥")&&kpUnlock.Text.Contains("DR≥")&&!kpUnlock.Text.Contains("最低角色等级"),"Lmax评级文案错误");
            Need(kpRetire!.Disabled&&kpRetire.Text.Contains("退休当前最低怪物等级 1"),"Lmin退休文案/禁用错误");
            // WP5 装备卡：六部位各一件候选 → 出现"一键换装（6件）"，点击后实际穿戴并入档。
            foreach(var spec in config.EquipmentBases)session.QaOfferEquipment(session.Heroes[0].HeroInstanceId,spec.Id,3,"rare");
            Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var batchCard=uiLayer.FindChild("BatchEquip",true,false) as Button;Need(batchCard!=null&&!batchCard.Disabled,"批量换装卡缺失或被禁用");
            // 装备卡位于右栏滚动区底部：拍照前滚到底，让证据图真实可见。
            interactionScroll.ScrollVertical=(int)batchCard!.Position.Y;
            await Picture("equipment-batch");
            await Click(batchCard!);
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
            await Click(wearBest!);
            Need(session.WornEquipment(session.Heroes[0],config.EquipmentBases[0].Slot)!=null,"穿戴更好装备未生效");
            await Picture("hero-equipment-worn");
            SelectStarView(0);
            // 固定4+T真实控件状态：页面选择、学习、免费局部重置及同职资格共享。
            // C1-A-1 槽位重排：战吼为 S3（原 S2 改为旋风斩），真实控件断言跟随新顺位。
            selectedHero=session.Heroes[0].HeroInstanceId;selectedSkillRegion=3;SelectStarView(4);talentStamp="";RefreshTalentPage();
            tabs.CurrentTab=tabs.GetTabIdxFromControl(talentContent);
            await Picture("fixed-skills-s3-unlearned-t-locked");
            var s2=talentRegionNodes.GetChildren().OfType<Button>().Single(b=>b.Name=="LearnTalent_barb_taunt");
            await Click(s2);Need(session.Heroes[0].SkillIds.Contains("barb_taunt"),"鼠标按下/松开未学S3");
            await Picture("fixed-skills-s3-learned");
            await Click(talentRegionReset);Need(!session.Heroes[0].SkillIds.Contains("barb_taunt")&&session.Heroes[0].SkillIds.Contains("barb_bash"),"真实局部洗点未生效");
            await Picture("fixed-skills-local-reset");
            // C1-A-2：区域2除技能卡外还必须出现技能级修饰器卡（BAR-S202…），且未学前置时按钮为不可学状态。
            selectedSkillRegion=2;talentStamp="";RefreshTalentPage();
            var regionNodes=talentRegionNodes.GetChildren().OfType<Button>().ToArray();
            Need(regionNodes.Any(b=>b.Name=="LearnTalent_BAR-S202")&&regionNodes.Any(b=>b.Name=="LearnTalent_BAR-S206"),"区域2未渲染技能修饰器节点");
            Need(regionNodes.Count(b=>b.Name.ToString().StartsWith("LearnTalent_"))>=7,"区域2节点数量不足（应为技能节点+修饰器+触发源）");
            await Picture("talent-region2-modifiers");
            // T区域：职业特性根+6条T路线节点，全部为C1-A新增内容，用于真实界面取证。
            selectedSkillRegion=5;talentStamp="";RefreshTalentPage();
            var traitNodes=talentRegionNodes.GetChildren().OfType<Button>().Select(b=>b.Name.ToString()).ToArray();
            Need(traitNodes.Contains("LearnTalent_BAR-T02")&&traitNodes.Contains("LearnTalent_BAR-T07"),"T区域未渲染7个特性节点："+string.Join(",",traitNodes));
            await Picture("talent-region5-traits");
            session.GrantGmResources(gold:10000);session.BuyStar("s_root",out _);
            SelectStarView(3);await Picture("star-retired-slots-absent");
            BuyStarNode("s_trait_warrior");Need(session.ClassTraitUnlocked("warrior"),"星图职业资格购买未生效");
            await Picture("star-class-trait-purchased");
            foreach(int member in new[]{0,1})
            {
                selectedHero=session.Heroes[member].HeroInstanceId;selectedSkillRegion=5;SelectStarView(4);talentStamp="";RefreshTalentPage();
                await Picture("trait-shared-hero-"+(member+1));
            }
            selectedHero=session.Heroes[0].HeroInstanceId;talentStamp="";RefreshTalentPage();
            await Click(talentRegionNodes.GetChildren().OfType<Button>().Single(b=>b.Name=="LearnTalent_warrior_trait"));
            Need(session.Heroes[0].Talents.GetValueOrDefault("warrior_trait")==1&&!session.Heroes[1].Talents.ContainsKey("warrior_trait"),"T学习串实例");
            await Picture("trait-invested-only-first-instance");SelectStarView(0);
            // V1.6：真实Godot渲染多窗口尺寸；只验证本轮已修正界面，不冒充4+1验收。
            foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1920,1080)})
            {
                GetWindow().Size=size;
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                interactionScroll.ScrollVertical=(int)interactionScroll.GetVScrollBar().MaxValue;
                await Picture($"layout-{size.X}x{size.Y}");
            }
            GetWindow().Size=new Vector2I(1440,900);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Need(gameHud.FindChild("ScrollQuickBar",true,false)!=null&&heroHealth.All(h=>h!=null)&&hudSkillBars.All(b=>b!=null),"七分区缺失");
            SaveNow(true);var stored=saveStore.Load(config)!;Need(stored.Run!=null&&stored.Run.MonsterLevelMax==2&&stored.Run.Kp==90,"真实存档接线");
            uiLayer.GetNode<Button>("GmMenuButton").EmitSignal(BaseButton.SignalName.Pressed);Need(gmRoot.Visible,"GM入口丢失");ToggleGm();
            uiLayer.GetNode<Button>("SettingsButton").EmitSignal(BaseButton.SignalName.Pressed);Need(settingsRoot.Visible,"设置入口丢失");await Picture("settings-abandon-and-clear");ToggleSettings();
            pause.EmitSignal(BaseButton.SignalName.Pressed);Need(!paused,"暂停入口丢失");pause.EmitSignal(BaseButton.SignalName.Pressed);
            // 独立战斗夹具：近者位于列表第二名；真正触发槽按钮并观察真实扣减与伤害。
            session=GameSession.CreateWorld(config,8803,new[]{"warrior","ranger","mage"});
            for(int tick=0;tick<20000&&session.CurrentRoom==0;tick++)session.Tick(.05f);
            while(session.CombatEntryRemaining>0)session.Tick(.05f);
            var battleRoom=session.Floor.Rooms[session.CurrentRoom];
            Need(battleRoom.Monsters.Count>=2,"卷轴双目标夹具不足");
            foreach(var monster in battleRoom.Monsters)monster.Hp=0;
            var caster=session.Heroes[0];caster.Position=battleRoom.Center;
            var far=battleRoom.Monsters[0];var near=battleRoom.Monsters[1];far.Hp=near.Hp=10000;
            far.Position=caster.Position+new System.Numerics.Vector2(1.5f,0);
            near.Position=caster.Position+new System.Numerics.Vector2(.25f,0);
            session.QaGrantConsumable(config.Scrolls[0].Id);Refresh();
            Need(!scrollSlotButtons[0].Disabled,"有合法目标及库存的卷轴槽被禁用");
            await Picture("scroll-nearest-before");
            await Click(scrollSlotButtons[0]);
            Need(near.Hp<10000&&far.Hp==10000&&session.ScrollStock.GetValueOrDefault(config.Scrolls[0].Id)==0,"槽位未命中近者或未扣一张");
            float after=near.Hp;
            scrollSlotButtons[0].EmitSignal(BaseButton.SignalName.Pressed);
            Need(near.Hp==after&&session.ScrollStock.GetValueOrDefault(config.Scrolls[0].Id)==0,"快速重复槽位触发超扣");
            await Picture("scroll-nearest-after");
            session.GrantGmResources(experience:1000);session.UpgradeHero(caster.HeroInstanceId,out _);session.LearnTalent(caster.HeroInstanceId,"barb_taunt",out _);
            foreach(var hero in session.Heroes){hero.Hp=hero.MaxHp=100000;hero.Timer=999;}
            for(int tick=0;tick<40;tick++)session.Tick(.05f);
            Need(session.Capture().SkillState.Buffs.Any(b=>b.Id=="barb_taunt_buff"&&b.Owner==0),"实机S2未产生真实Buff");
            await Picture("fixed-s2-effect-and-hud-cooldown");
            session.ResetTalentRegion(caster.HeroInstanceId,2,out _);await Picture("fixed-s2-effect-removed-s1-retained");
            // 0/1/4/5件真实卡片矩阵，五项显示不限制实际穿戴数量。
            foreach(int count in new[]{0,1,4,5})
            {
                session=GameSession.CreateNewWorld(config,1700+count,new[]{"warrior","warrior"});
                foreach(var spec in config.EquipmentBases.Take(count))session.QaOfferEquipment(session.Heroes[0].HeroInstanceId,spec.Id,3,"rare");
                Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);interactionScroll.ScrollVertical=0;
                Need(count<5?equipCards.Count==count:equipBatch!.Visible,"0/1/4/5件卡片合批门槛");
                await Picture("equipment-count-"+count);
                if(count==5){interactionScroll.ScrollVertical=(int)equipBatch!.Position.Y;await Click(equipBatch);Need(session.EquipmentTotals(session.Heroes[0]).Attack+session.EquipmentTotals(session.Heroes[0]).Hp>0,"五项合批真实点击未生效");}
            }
            for(int slot=0;slot<4;slot++)session!.QaGrantConsumable(config.Potions[0].Id);
            await Picture("potion-four-pending");
            await Click((Button)gameHud.FindChild("PotionSlot_0",true,false)!);Need(session!.PotionSlots[0].Active&&session.PotionSlots.Count==4,"药剂真实点击/占槽错误");
            await Picture("potion-four-slots-one-active");
            var priorFixture=session;
            session=GameSession.CreateWorld(config,1706,new[]{"priest","priest","priest","priest"});
            for(int tick=0;tick<20000&&session.CurrentRoom==0;tick++)session.Tick(.05f);
            while(session.CombatEntryRemaining>0)session.Tick(.05f);
            session.GrantGmResources(experience:10000);var auraRoom=session.Floor.Rooms[session.CurrentRoom];
            // C1-A-1：持续光环移到职业特性T（守护光环），四同职各自投入T即产生四份独立来源。
            foreach(var priest in session.Heroes){session.UpgradeHero(priest.HeroInstanceId,out _);session.LearnTalent(priest.HeroInstanceId,"priest_trait",out _);priest.Position=auraRoom.Center;priest.Hp=priest.MaxHp=100000;priest.Attack=1;priest.Timer=999;}
            foreach(var monster in auraRoom.Monsters){monster.Position=auraRoom.Center+new System.Numerics.Vector2(1,0);monster.Hp=monster.MaxHp=100000;monster.Attack=0;monster.Timer=999;}
            for(int tick=0;tick<60;tick++)session.Tick(.05f);
            Need(session.Capture().SkillState.Buffs.Where(b=>b.Id=="pal_guard_buff"&&b.Target==0).Select(b=>b.Owner).Distinct().Count()==4,"真实窗口四同职多来源Buff缺失");
            await Picture("four-same-class-buff-sources-and-cooldowns");session=priorFixture;
            // 64位长数值/长中文名为明确QA压力夹具；完整余额通过tooltip可访问。
            var longValues=session.CaptureProgress();longValues.Gold=long.MaxValue-1000;longValues.PublicExperience=long.MaxValue-1000;longValues.Kp=long.MaxValue-1000;longValues.KpEarned=long.MaxValue-1000;longValues.KpSpent=0;
            session=GameSession.Restore(config,longValues);session.Heroes[0].Name="测试用超长中文英雄名称与连续成长说明";
            foreach(var size in new[]{new Vector2I(1440,900),new Vector2I(1280,720)})
            {
                GetWindow().Size=size;await Picture($"long-values-{size.X}x{size.Y}");
                for(int i=0;i<currencies.Length;i++)Need(currencies[i].Size.X<=72.1f,"长数字越过货币格");
            }
            GetWindow().Size=new Vector2I(1440,900);
            GD.Print("FIRST_BATCH_UI_PASS output="+output+" window="+GetWindow().Size+" renderer="+DisplayServer.GetName()+" explicit_QA_fixture=true");QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
}
