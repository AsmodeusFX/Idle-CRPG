using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private Label gmHpValue=null!,gmAttackValue=null!,gmCountValue=null!,gmStatus=null!;
    private Button gmInvincible=null!,gmInstantClear=null!,gmSkipSupplies=null!;
    private readonly List<Button> gmSessionButtons=new();
    private readonly Dictionary<string,Button> gmButtons=new();

    private static StyleBoxFlat GmStyle(string color,int radius=6)=>new() {
        BgColor=new Color(color),BorderColor=new Color("2c4655"),
        BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,
        CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius
    };
    private Panel GmPanelBox(Node parent,Vector2 position,Vector2 size,string color)
    {var p=new Panel {Position=position,Size=size};p.AddThemeStyleboxOverride("panel",GmStyle(color));parent.AddChild(p);return p;}
    private Button GmButton(Node parent,string id,string text,Vector2 position,Vector2 size,Action command,bool needsSession=true)
    {
        var b=new Button {Name=id,Text=text,Position=position,Size=size};parent.AddChild(b);gmButtons[id]=b;
        if(needsSession)gmSessionButtons.Add(b);
        b.Pressed+=()=>
        {
            if(needsSession&&session==null)return;
            long revision=session?.Revision??0;
            command();RefreshGm();Refresh();PlaySound("sfx_click");
            if(session!=null&&session.Revision!=revision)SaveNow();
        };
        return b;
    }
    private float StepGmScale(float value,int direction)
    {
        int index=Array.FindIndex(config.GmScaleSteps,x=>x>=value);
        return config.GmScaleSteps[Math.Clamp(index+direction,0,config.GmScaleSteps.Length-1)];
    }

    /// <summary>参考 Idle-Sword GM 面板：上半区只调整当局，下半区进入编辑器；不修改参考工程。</summary>
    private void BuildGmMenu()
    {
        gmRoot=new Control {Size=new Vector2(1440,900),Visible=false,MouseFilter=Control.MouseFilterEnum.Stop};uiLayer.AddChild(gmRoot);
        var theme=new Theme();
        theme.SetStylebox("normal","Button",GmStyle("172b3b"));theme.SetStylebox("hover","Button",GmStyle("294759"));
        theme.SetStylebox("pressed","Button",GmStyle("395c59"));theme.SetStylebox("disabled","Button",GmStyle("15232d"));
        theme.SetColor("font_color","Button",new Color("e3e5d6"));theme.SetColor("font_disabled_color","Button",new Color("677d87"));
        theme.SetColor("font_color","Label",new Color("96acae"));theme.SetFontSize("font_size","Button",16);gmRoot.Theme=theme;
        gmRoot.AddChild(new ColorRect {Size=new Vector2(1440,900),Color=new Color(0,0,0,.62f)});
        var panel=GmPanelBox(gmRoot,new Vector2(420,108),new Vector2(600,728),"172b3b");
        var title=HudText(panel,"GM · 调试",new Vector2(26,18),24);title.AddThemeColorOverride("font_color",new Color("d9bc82"));
        GmButton(panel,"GmClose","关闭",new Vector2(496,18),new Vector2(78,34),()=>gmRoot.Hide(),false);

        var commands=GmPanelBox(panel,new Vector2(18,68),new Vector2(564,428),"101f2d");
        var heading=HudText(commands,"GM 指令 · 当局状态",new Vector2(16,10),17);heading.AddThemeColorOverride("font_color",new Color("8cc4b1"));
        HudText(commands,"资源",new Vector2(16,52),15);
        GmButton(commands,"GmGold",$"金币 +{config.GmGoldGrant}",new Vector2(112,44),new Vector2(136,36),()=>session!.GrantGmResources(gold:config.GmGoldGrant));
        GmButton(commands,"GmExperience",$"经验 +{config.GmExperienceGrant}",new Vector2(258,44),new Vector2(136,36),()=>session!.GrantGmResources(experience:config.GmExperienceGrant));
        GmButton(commands,"GmKills",$"击杀 +{config.GmKillGrant}",new Vector2(404,44),new Vector2(144,36),()=>session!.GrantGmResources(kills:config.GmKillGrant));

        HudText(commands,"怪物血量",new Vector2(16,104),15);
        GmButton(commands,"GmHpMinus","血量 −",new Vector2(112,96),new Vector2(100,36),()=>session!.SetGmBattle(session.Gm with {MonsterHp=StepGmScale(session.Gm.MonsterHp,-1)}));
        gmHpValue=HudText(commands,"×1",new Vector2(230,103),18);
        GmButton(commands,"GmHpPlus","血量 +",new Vector2(448,96),new Vector2(100,36),()=>session!.SetGmBattle(session.Gm with {MonsterHp=StepGmScale(session.Gm.MonsterHp,1)}));

        HudText(commands,"怪物攻击",new Vector2(16,156),15);
        GmButton(commands,"GmAttackMinus","攻击 −",new Vector2(112,148),new Vector2(100,36),()=>session!.SetGmBattle(session.Gm with {MonsterAttack=StepGmScale(session.Gm.MonsterAttack,-1)}));
        gmAttackValue=HudText(commands,"×1",new Vector2(230,155),18);
        GmButton(commands,"GmAttackPlus","攻击 +",new Vector2(448,148),new Vector2(100,36),()=>session!.SetGmBattle(session.Gm with {MonsterAttack=StepGmScale(session.Gm.MonsterAttack,1)}));

        HudText(commands,"小队",new Vector2(16,208),15);
        gmInvincible=GmButton(commands,"GmInvincible","全队无敌：关",new Vector2(112,200),new Vector2(200,36),()=>session!.SetGmBattle(session.Gm with {Invincible=!session.Gm.Invincible}));
        GmButton(commands,"GmResetBattle","重置战斗调试",new Vector2(324,200),new Vector2(224,36),()=>session!.SetGmBattle(new()));

        HudText(commands,"房间怪物",new Vector2(16,260),15);
        GmButton(commands,"GmCountMinus","数量 −",new Vector2(112,252),new Vector2(100,36),()=>session!.SetGmBattle(session.Gm with {RoomCountBonus=Math.Max(-config.GmCountLimit,session.Gm.RoomCountBonus-config.GmCountStep)}));
        gmCountValue=HudText(commands,"+0 只",new Vector2(230,259),18);
        GmButton(commands,"GmCountPlus","数量 +",new Vector2(448,252),new Vector2(100,36),()=>session!.SetGmBattle(session.Gm with {RoomCountBonus=Math.Min(config.GmCountLimit,session.Gm.RoomCountBonus+config.GmCountStep)}));

        HudText(commands,"关卡测试",new Vector2(16,312),15);
        gmInstantClear=GmButton(commands,"GmInstantClear","瞬间通关：关",new Vector2(112,304),new Vector2(200,36),()=>session!.SetGmBattle(session.Gm with {InstantDungeonClear=!session.Gm.InstantDungeonClear}));
        GmButton(commands,"GmClearChapter","通关本章",new Vector2(324,304),new Vector2(224,36),()=>session!.CompleteGmChapter());
        gmInstantClear.TooltipText="仅对下一次及后续进入生效；普通地牢和已授权要塞均适用。写入真实通关进度，无击杀、经验或掉落奖励；仍需回城，补给可由开关跳过；不绕过要塞手动授权。读档关闭。";
        gmButtons["GmClearChapter"].TooltipText="GM测试例外：解锁并通关当前执行章节全部主支线内容，放弃当前地牢，回城补给后进入下一章；不发战斗奖励，通关进度会保存。";
        gmSkipSupplies=GmButton(commands,"GmSkipSupplies","跳过补给：关",new Vector2(112,356),new Vector2(200,36),()=>session!.SetGmBattle(session.Gm with {SkipSupplies=!session.Gm.SkipSupplies}));
        gmSkipSupplies.TooltipText="回城跳过治疗和售卖，走近固定传送阵出发；无可用传送点时走城门。不恢复生命、不出售物品。读档或重置调试关闭。";
        var tools=GmPanelBox(panel,new Vector2(18,510),new Vector2(564,112),"101f2d");
        var toolsTitle=HudText(tools,"编辑器入口",new Vector2(16,10),17);toolsTitle.AddThemeColorOverride("font_color",new Color("8cc4b1"));
        HudText(tools,"配置工具",new Vector2(16,58),15);
        GmButton(tools,"StarEditorEntry","星图节点编辑器",new Vector2(112,48),new Vector2(200,38),()=>{gmRoot.Hide();viewMode=3;if(!_editingStars)ToggleStarEditor();else RefreshStarPage();TickStarPanels();},false);
        var future=new Button {Text="关卡编辑器 · 待实现",Position=new Vector2(324,48),Size=new Vector2(224,38),Disabled=true};tools.AddChild(future);
        GmButton(panel,"SkillPreviewEntry","技能预览 · 战斗试场",new Vector2(130,628),new Vector2(424,36),OpenSkillPreview,false);
        gmStatus=HudText(panel,"",new Vector2(28,674),13);gmStatus.Size=new Vector2(544,50);gmStatus.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        gmButtons["GmHpPlus"].TooltipText=gmButtons["GmHpMinus"].TooltipText="有效血量倍率：承伤除以倍率，保持当前生命比例，立即影响场上及后续怪物。";
        gmButtons["GmAttackPlus"].TooltipText=gmButtons["GmAttackMinus"].TooltipText="怪物攻击在扣除队员防御前乘以倍率，立即生效。";
        gmButtons["GmCountPlus"].TooltipText=gmButtons["GmCountMinus"].TooltipText="在正常随机数量上增减，最终限制为 1–1000 只；仅影响后续首次生成的房间。";
        RefreshGm();
    }
    private void RefreshGm()
    {
        foreach(var button in gmSessionButtons)button.Disabled=session==null;
        var settings=session?.Gm??new GmBattleSettings();
        gmHpValue.Text=$"×{settings.MonsterHp:0.#}";gmAttackValue.Text=$"×{settings.MonsterAttack:0.#}";
        gmCountValue.Text=$"{settings.RoomCountBonus:+0;-0;+0} 只";gmInvincible.Text="全队无敌："+(settings.Invincible?"开":"关");
        gmSkipSupplies.Text="跳过补给："+(settings.SkipSupplies?"开":"关");
        gmInstantClear.Text="瞬间通关："+(settings.InstantDungeonClear?"开":"关");
        if(session!=null)
        {
            gmButtons["GmHpMinus"].Disabled=settings.MonsterHp<=config.GmScaleSteps[0];gmButtons["GmHpPlus"].Disabled=settings.MonsterHp>=config.GmScaleSteps[^1];
            gmButtons["GmAttackMinus"].Disabled=settings.MonsterAttack<=config.GmScaleSteps[0];gmButtons["GmAttackPlus"].Disabled=settings.MonsterAttack>=config.GmScaleSteps[^1];
            gmButtons["GmCountMinus"].Disabled=settings.RoomCountBonus<=-config.GmCountLimit;gmButtons["GmCountPlus"].Disabled=settings.RoomCountBonus>=config.GmCountLimit;
        }
        gmStatus.Text=(session==null?"请先在游戏页开始远征，再使用当局指令。\n":"瞬间通关从下次进入生效，无战斗奖励；通关进度正常保存。\n")+"所有调试开关读档恢复默认；资源和成长正常保存。";
    }

    /// <summary>真实 GM 按钮只在 QA 会话中执行，验证命令生效及当局/工具两区分离。</summary>
    private void RunGmSmoke()
    {
        var original=session;
        int originalSpeed=speed;double originalAccumulator=accumulator;
        try
        {
            // 使用与逐帧运行相同的时钟，验证城镇寻路在三档倍速下均推进相应固定步数。
            foreach(int rate in new[]{1,2,4})
            {
                session=GameSession.CreateWorld(config,234,new[]{"warrior","ranger","mage"});
                var expected=GameSession.CreateWorld(config,234,new[]{"warrior","ranger","mage"});
                speed=rate;accumulator=0;
                for(int frame=0;frame<8;frame++)AdvanceSimulation(.125);
                for(int tick=0;tick<(int)Math.Round((rate-accumulator)/.05);tick++)expected.Tick(.05f);
                if(session.World!.Stage!=expected.World!.Stage||session.Heroes.Where((h,i)=>System.Numerics.Vector2.Distance(h.Position,expected.Heroes[i].Position)>.0001f).Any())throw new Exception("城镇倍速时钟未按固定步推进");
            }
            session=GameSession.CreateWorld(config,234,new[]{"warrior","ranger","mage"});
            ToggleGm();long before=session.Gold;int beforeKills=session.Kills;
            gmButtons["GmGold"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmExperience"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmKills"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmInvincible"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmHpPlus"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmAttackMinus"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmCountPlus"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmInstantClear"].EmitSignal(Button.SignalName.Pressed);
            gmButtons["GmSkipSupplies"].EmitSignal(Button.SignalName.Pressed);
            if(!session.Gm.SkipSupplies||!gmSkipSupplies.Text.EndsWith("开"))throw new Exception("跳过补给按钮状态不同步");
            if(!session.Gm.InstantDungeonClear||!gmInstantClear.Text.EndsWith("开"))throw new Exception("进入瞬间通关按钮状态不同步");
            if(!gmRoot.Visible||session.Gold!=before+10000||session.Kills!=beforeKills+10000||session.Heroes[0].Level<=1||!session.Gm.Invincible||session.Gm.MonsterHp!=2||session.Gm.MonsterAttack!=.5f||session.Gm.RoomCountBonus!=1)throw new Exception("GM 按钮未生效或意外关闭面板");
            if(gmButtons["GmGold"].GetParent()==gmButtons["StarEditorEntry"].GetParent())throw new Exception("GM 指令与编辑器没有分区");
            gmButtons["GmResetBattle"].EmitSignal(Button.SignalName.Pressed);
            if(session.Gm!=new GmBattleSettings()||session.Gold!=before+10000||session.Kills!=beforeKills+10000)throw new Exception("重置战斗调试清除了资源或没有重置开关");
            gmButtons["GmClearChapter"].EmitSignal(Button.SignalName.Pressed);
            if(session.World!.Progress.Cleared.Count!=session.Config.Dungeons.Count||session.World.Stage!=CampaignStage.Village)throw new Exception("通关本章按钮未清空当前章节或未返回城镇");
            session=null;RefreshGm();
            RefreshHud();
            if(!partySetup.Visible||interactionHeading.Visible||interactionScroll.Visible)throw new Exception("创角仍与远征交互重叠");
            if(gmSessionButtons.Any(b=>!b.Disabled)||gmButtons["StarEditorEntry"].Disabled)throw new Exception("未开局时 GM 命令/编辑器可用性错误");
            GD.Print("GM_SMOKE_OK: town 1/2/4 speed clock, skip-supplies toggle/reset, actual command buttons, exclusive party setup and editor access");
        }
        catch(Exception error){GD.PushError(error.ToString());QuitSafely(1);}
        finally{session=original;speed=originalSpeed;accumulator=originalAccumulator;gmRoot.Hide();RefreshGm();Refresh();}
    }
}
