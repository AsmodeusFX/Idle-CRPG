using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>冷却展示只读取Core，不自行推进技能。</summary>
public partial class TalentSlotButton : InteractionButton
{
    public Func<double>? Remaining;
    public double Duration=1;
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        double remaining=Remaining?.Invoke()??0;if(remaining<=0)return;
        float angle=(float)(Math.Clamp(remaining/Math.Max(Duration,.001),0,1)*Math.Tau);
        var center=Size/2;int count=Math.Max(2,(int)(angle*16));var points=new Vector2[count+2];points[0]=center;
        for(int i=0;i<=count;i++){var ray=Vector2.FromAngle(-Mathf.Pi/2+angle*i/count);float length=Math.Min(center.X/Math.Max(.0001f,Math.Abs(ray.X)),center.Y/Math.Max(.0001f,Math.Abs(ray.Y)));points[i+1]=center+ray*length;}
        DrawColoredPolygon(points,new Color(0,0,0,.62f));DrawString(ThemeDB.FallbackFont,new Vector2(3,Size.Y-3),remaining.ToString("0.0"),HorizontalAlignment.Left,-1,11,Colors.White);
    }
}
public partial class Main
{
    private Control talentContent=null!,talentEditor=null!;
    private Label talentHeading=null!,talentMessage=null!;
    private HBoxContainer talentTrees=null!,talentBar=null!;
    private VBoxContainer talentRegionNodes=null!;
    private Button talentRegionReset=null!,legacyTalentReturn=null!;
    private string talentStamp="";
    private int selectedSkillRegion=1;
    private GameSession? hudTalentSession;
    private readonly HBoxContainer[] hudSkillBars=new HBoxContainer[5];
    private Actor? SelectedTalentHero=>session?.Heroes.FirstOrDefault(h=>h.HeroInstanceId==selectedHero)??session?.Heroes.FirstOrDefault();
    private void BuildTalentContent(Control parent)
    {
        talentContent=new Control {Name="技能",CustomMinimumSize=new Vector2(1000,600)};parent.AddChild(talentContent);
        talentHeading=HudText(talentContent,"",new Vector2(12,8),16);
        talentHeading.ClipText=true;talentHeading.Size=new Vector2(710,24);
        talentMessage=HudText(talentContent,"选择S1～S4或T区域；学习后自动施放，无需装配",new Vector2(12,35),12);
        legacyTalentReturn=new Button {Name="ReturnLegacyTalentPoints",Position=new Vector2(730,5),Size=new Vector2(260,28)};talentContent.AddChild(legacyTalentReturn);
        legacyTalentReturn.Pressed+=()=>{var hero=SelectedTalentHero;if(hero==null||session==null)return;session.ReturnRetiredTalentPoints(hero.HeroInstanceId,out string reason);talentMessage.Text=reason==""?"旧投入已免费返还；原始记录保留":reason;talentStamp="";Refresh();SaveNow();};
        talentBar=new HBoxContainer {Position=new Vector2(8,65),Size=new Vector2(984,126)};talentBar.AddThemeConstantOverride("separation",8);talentContent.AddChild(talentBar);
        talentTrees=new HBoxContainer {Position=new Vector2(8,202),Size=new Vector2(984,40)};talentContent.AddChild(talentTrees);
        for(int region=1;region<=5;region++)
        {
            int captured=region;var button=new Button {Name="SelectSkillRegion"+region,Text=region==5?"T · 职业特性":$"S{region} · 天赋",CustomMinimumSize=new Vector2(155,34)};talentTrees.AddChild(button);
            button.Pressed+=()=>{selectedSkillRegion=captured;talentStamp="";RefreshTalentPage();};
        }
        talentRegionReset=new Button {Name="ResetCurrentSkillRegion",Text="免费重置本区域",Position=new Vector2(804,247),Size=new Vector2(186,32)};talentContent.AddChild(talentRegionReset);
        talentRegionReset.Pressed+=()=>{var hero=SelectedTalentHero;if(hero==null||session==null)return;session.ResetTalentRegion(hero.HeroInstanceId,selectedSkillRegion,out string reason);talentMessage.Text=reason==""?"当前区域已重置，S1基础攻击保留":reason;talentStamp="";Refresh();SaveNow();};
        var scroll=new DragScrollContainer {Position=new Vector2(8,287),Size=new Vector2(984,305),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};talentContent.AddChild(scroll);
        talentRegionNodes=new VBoxContainer {Name="CurrentSkillTalentNodes",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(talentRegionNodes);
    }
    // 兼容旧生命周期Hide调用；不创建或展示任何技能装配控件。
    private void BuildTalentEditor(){talentEditor=new Control {Visible=false};uiLayer.AddChild(talentEditor);}
    private static void ClearTalentChildren(Node parent){foreach(var node in parent.GetChildren()){parent.RemoveChild(node);node.QueueFree();}}
    private TalentSlotButton TalentSlot(Actor hero,string id,Vector2 size)
    {
        var node=config.Talents.Nodes.GetValueOrDefault(id);
        var button=new TalentSlotButton {Text=node?.Name??"未投放",CustomMinimumSize=size,ClipContents=true,ClipText=true,Duration=id==""?1:config.Skills.Skills[id].Cooldown/20d};button.AddThemeFontSizeOverride("font_size",size.Y<35?10:13);
        if(id!=""&&session!=null){button.Remaining=()=>session?.Heroes.Contains(hero)==true?session.SkillCooldown(hero,id):0;button.TooltipText=TalentTooltip(hero,id);button.Details=()=>TalentTooltip(hero,id);}
        return button;
    }
    private string TalentTooltip(Actor hero,string id)
    {
        if(session==null||!config.Talents.Nodes.TryGetValue(id,out var node))return "尚未投放";
        session.CanLearnTalent(hero,id,out string reason);
        string mode=node.Kind=="active"?"主动 / 学会后自动施放":"被动 / 学习即生效";
        return $"{node.Name} · {mode}\n{node.Description}\n学习等级 {hero.Talents.GetValueOrDefault(id)}/{node.MaxRank} · 生效等级 {session.TalentLevel(hero,id)}\n学习花费：1点 · 可用 {session.TalentPoints(hero)}\n互斥分支：{(node.ExclusiveGroup==""?"无":node.ExclusiveGroup)}\n冷却 {node.Cooldown:0.#}秒 · 距离 {node.Range:0.#}\n{reason}";
    }
    private void RefreshTalentPage()
    {
        var hero=SelectedTalentHero;
        string stamp=hero==null?"none":$"{hero.HeroInstanceId}:{hero.Level}:{selectedSkillRegion}:{string.Join(';',hero.Talents)}:{string.Join(';',hero.RetiredTalents)}:{string.Join(';',session!.Stars.Capture().Ranks.Select(p=>p.Id+":"+p.Level))}";
        if(stamp==talentStamp)return;talentStamp=stamp;ClearTalentChildren(talentBar);ClearTalentChildren(talentRegionNodes);
        if(hero==null||session==null){talentHeading.Text="请先创建英雄";legacyTalentReturn.Hide();return;}
        talentHeading.Text=$"{hero.Name} #{session.Heroes.IndexOf(hero)+1} · {hero.SpecId} · Lv.{hero.Level} · 天赋点 {session.TalentPoints(hero)}";
        int legacy=session.RetiredTalentPoints(hero);legacyTalentReturn.Visible=legacy>0;legacyTalentReturn.Text=$"免费取回旧技能投入（{legacy}点）";
        talentTrees.GetChild<Button>(4).Text=session.ClassTraitUnlocked(hero.SpecId)?"T · 特性（资格已购）":"T · 特性（星图锁定）";
        var skills=config.ClassSkillSlots(hero.SpecId);
        for(int i=0;i<4;i++)
        {
            int region=i+1;string id=skills[i];var button=TalentSlot(hero,id,new Vector2(240,126));button.Name="FixedSkill"+region;
            bool learned=id!=""&&hero.Talents.GetValueOrDefault(id)>0;string status=id==""?"未投放":region==1?"开局已学":learned?"已学 · 自动施放":session.CanLearnTalent(hero,id,out _)?"可学习":"前置/点数未满足";
            button.Text=$"S{region} · {(id==""?"未投放":config.Talents.Nodes[id].Name)}\n{status}\n"+(id==""?"内容尚未部署":$"Rank {hero.Talents.GetValueOrDefault(id)}/{config.Talents.Nodes[id].MaxRank}\n{config.Talents.Nodes[id].Description}");
            button.Pressed+=()=>{selectedSkillRegion=region;talentStamp="";RefreshTalentPage();};talentBar.AddChild(button);
        }
        bool traitLocked=selectedSkillRegion==5&&!session.ClassTraitUnlocked(hero.SpecId);
        var heading=UiLabel(selectedSkillRegion==5?traitLocked?"T已锁定：需要在星图购买该职业特性资格":"T资格已解锁 · 同职业各实例分别投点":$"S{selectedSkillRegion}区域 · 已投入 {session.RegionInvestment(hero,selectedSkillRegion)}点",14);heading.AutowrapMode=TextServer.AutowrapMode.WordSmart;talentRegionNodes.AddChild(heading);
        var nodes=config.Talents.Nodes.Values.Where(node=>node.Hero==hero.SpecId&&config.TalentRegion(node)==selectedSkillRegion).OrderBy(node=>node.Kind=="active"?0:1).ThenBy(node=>node.Row).ThenBy(node=>node.Col).ToArray();
        foreach(var node in nodes)
        {
            bool can=session.CanLearnTalent(hero,node.Id,out string reason);string id=node.Id;
            var button=new InteractionButton {Name="LearnTalent_"+id,Text=$"{node.Name} · {hero.Talents.GetValueOrDefault(id)}/{node.MaxRank} · 1天赋点\n{(can?"点击学习："+node.Description:reason)}",CustomMinimumSize=new Vector2(950,58),Disabled=!can,TooltipText=TalentTooltip(hero,id)};
            button.Details=()=>TalentTooltip(hero,id);talentRegionNodes.AddChild(button);
            button.Pressed+=()=>{session.LearnTalent(hero.HeroInstanceId,id,out string why);talentMessage.Text=why==""?"已学习，按定义自动生效":why;talentStamp="";Refresh();SaveNow();};
        }
        if(nodes.Length==0)talentRegionNodes.AddChild(UiLabel("此技能区域尚未投放内容",14));
        talentRegionReset.Disabled=session.RegionInvestment(hero,selectedSkillRegion)==0;
    }
    private void RefreshHudSkills()
    {
        if(!ReferenceEquals(hudTalentSession,session)){hudTalentSession=session;foreach(var bar in hudSkillBars)if(bar!=null)bar.SetMeta("stamp","refresh");}
        for(int i=0;i<hudSkillBars.Length;i++)
        {
            var bar=hudSkillBars[i];if(bar==null)continue;var hero=session?.Heroes.ElementAtOrDefault(i);
            string stamp=hero==null?"":hero.HeroInstanceId+":"+string.Join(';',session!.LearnedClassSkills(hero));
            if(bar.GetMeta("stamp","").AsString()==stamp)continue;bar.SetMeta("stamp",stamp);ClearTalentChildren(bar);if(hero==null)continue;
            foreach(string id in config.ClassSkillSlots(hero.SpecId))
            {
                bool learned=id!=""&&hero.Talents.GetValueOrDefault(id)>0;var button=TalentSlot(hero,id,new Vector2(76,28));
                if(!learned){button.Text=id==""?"未投放":"未学习";button.Remaining=null;button.Modulate=new Color("77818b");}
                button.Pressed+=()=>{selectedHero=hero.HeroInstanceId;SelectStarView(4);talentStamp="";RefreshTalentPage();};bar.AddChild(button);
            }
        }
    }
}
