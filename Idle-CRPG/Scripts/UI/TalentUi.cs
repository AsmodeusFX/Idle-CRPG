using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>技能按钮共用扇形冷却遮罩；进度来自逻辑秒数，不自行推进战斗。</summary>
public partial class TalentSlotButton : InteractionButton
{
    public Func<double>? Remaining;
    public double Duration=1;
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        double remaining=Remaining?.Invoke()??0;if(remaining<=0)return;
        float angle=(float)(Math.Clamp(remaining/Math.Max(Duration,.001),0,1)*Math.Tau);
        var center=Size/2;int count=Math.Max(2,(int)(angle*16));
        var points=new Vector2[count+2];points[0]=center;
        for(int i=0;i<=count;i++){var ray=Vector2.FromAngle(-Mathf.Pi/2+angle*i/count);float length=Math.Min(center.X/Math.Max(.0001f,Math.Abs(ray.X)),center.Y/Math.Max(.0001f,Math.Abs(ray.Y)));points[i+1]=center+ray*length;}
        DrawColoredPolygon(points,new Color(0,0,0,.62f));
        DrawString(ThemeDB.FallbackFont,new Vector2(3,Size.Y-3),remaining.ToString("0.0"),HorizontalAlignment.Left,-1,11,Colors.White);
    }
}
public partial class Main
{
    private Control talentContent=null!,talentEditor=null!;
    private Label talentHeading=null!,talentMessage=null!,talentEditorHint=null!;
    private HBoxContainer talentTrees=null!,talentBar=null!,talentEditorSlots=null!;
    private VBoxContainer talentList=null!;
    private string talentStamp="",editingTalentHero="";
    private string talentScrollHero="";
    private GameSession? hudTalentSession;
    private readonly List<string> talentDraft=new();
    private int selectedTalentSlot;
    private readonly HBoxContainer[] hudSkillBars=new HBoxContainer[5];
    private Actor? SelectedTalentHero=>session?.Heroes.FirstOrDefault(h=>h.HeroInstanceId==selectedHero)??session?.Heroes.FirstOrDefault();
    private void BuildTalentContent(Control parent)
    {
        talentContent=new Control {Name="技能",CustomMinimumSize=new Vector2(1000,600)};parent.AddChild(talentContent);
        talentHeading=HudText(talentContent,"",new Vector2(12,10),16);
        talentMessage=HudText(talentContent,"点击学习；拖动节点查看说明并装配，空白处拖动浏览",new Vector2(12,35),12);
        var reset=new Button {Text="重置天赋",Position=new Vector2(850,6),Size=new Vector2(130,30)};talentContent.AddChild(reset);
        reset.Pressed+=()=>{var h=SelectedTalentHero;if(h==null||session==null)return;var dialog=new ConfirmationDialog {Title="重置天赋",DialogText=$"重置{h.Name}全部天赋，返还{session.TalentInvestment(h)}点。\n费用：{session.TalentResetCost(h)}金币；需星图解锁且身处城镇。"};uiLayer.AddChild(dialog);dialog.Confirmed+=()=>{session.ResetTalents(h.HeroInstanceId,out var reason);talentMessage.Text=reason==""?"天赋已重置":reason;talentStamp="";SaveNow();};dialog.PopupCentered(new Vector2I(520,180));};
        talentTrees=new HBoxContainer {Position=new Vector2(8,65),Size=new Vector2(990,453)};talentTrees.AddThemeConstantOverride("separation",8);talentContent.AddChild(talentTrees);
        var bar=HudPanel(talentContent,new Vector2(8,530),new Vector2(990,66));HudText(bar,"主动技能",new Vector2(8,7),13);
        talentBar=new HBoxContainer {Position=new Vector2(110,10)};bar.AddChild(talentBar);
        var edit=new Button {Text="技能编辑",Position=new Vector2(860,12),Size=new Vector2(116,40)};bar.AddChild(edit);edit.Pressed+=()=>{if(SelectedTalentHero is { } h)OpenTalentEditor(h.HeroInstanceId);};
    }
    private void BuildTalentEditor()
    {
        talentEditor=new Control {Size=new Vector2(1440,900),Visible=false};uiLayer.AddChild(talentEditor);
        talentEditor.AddChild(new ColorRect {Color=new Color(0,0,0,.78f),Size=new Vector2(1440,900)});
        var panel=HudPanel(talentEditor,new Vector2(250,170),new Vector2(940,570));
        HudText(panel,"主动技能装配",new Vector2(20,14),22);
        talentEditorHint=HudText(panel,"",new Vector2(20,46),14);
        talentEditorSlots=new HBoxContainer {Position=new Vector2(20,90)};talentEditorSlots.AddThemeConstantOverride("separation",10);panel.AddChild(talentEditorSlots);
        var scroll=new DragScrollContainer {Position=new Vector2(20,175),Size=new Vector2(900,305)};panel.AddChild(scroll);
        talentList=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Pass};scroll.AddChild(talentList);
        var confirm=new Button {Text="确认装配",Position=new Vector2(610,510),Size=new Vector2(145,40)};panel.AddChild(confirm);
        confirm.Pressed+=()=>{if(session==null)return;if(session.SetTalentLoadout(editingTalentHero,talentDraft,out string reason)){talentEditor.Hide();talentStamp="";Refresh();SaveNow();}else talentEditorHint.Text=reason;};
        var cancel=new Button {Text="取消",Position=new Vector2(770,510),Size=new Vector2(145,40)};panel.AddChild(cancel);cancel.Pressed+=()=>talentEditor.Hide();
        var clear=new Button {Text="清空选中槽",Position=new Vector2(20,510),Size=new Vector2(150,40)};panel.AddChild(clear);clear.Pressed+=()=>{if(selectedTalentSlot>=0&&selectedTalentSlot<talentDraft.Count)talentDraft.RemoveAt(selectedTalentSlot);selectedTalentSlot=NextTalentSlot();RefreshTalentEditor();};
    }
    private static void ClearTalentChildren(Node parent)
    {foreach(var c in parent.GetChildren()){parent.RemoveChild(c);c.QueueFree();}}
    private TalentSlotButton TalentSlot(Actor h,string id,Vector2 size)
    {
        var n=config.Talents.Nodes.GetValueOrDefault(id);
        var b=new TalentSlotButton {Text=n?.Name??(id==""?"+":id),CustomMinimumSize=size,ClipContents=true,Duration=id==""?1:config.Skills.Skills[id].Cooldown/20d};b.AddThemeFontSizeOverride("font_size",size.Y<35?10:13);
        if(id!=""&&session!=null){b.Remaining=()=>session?.Heroes.Contains(h)==true?session.SkillCooldown(h,id):0;b.TooltipText=TalentTooltip(h,id);b.Details=()=>TalentTooltip(h,id);b.DragData=()=>InteractionButton.Payload("talent",h.HeroInstanceId,id);}
        return b;
    }
    private static string TalentDamageName(DamageType type)=>type switch {DamageType.Physical=>"物理",DamageType.Fire=>"火焰",DamageType.Cold=>"冰霜",DamageType.Lightning=>"闪电",DamageType.Poison=>"毒素",_=>type.ToString()};
    private string TalentTooltip(Actor h,string id)
    {
        if(session==null||!config.Talents.Nodes.TryGetValue(id,out var n))return id;
        int rank=h.Talents.GetValueOrDefault(id),effective=session.TalentLevel(h,id);
        string unit=n.Effect is "shout" or "weaken" or "crit_rate"?"百分比":n.Kind=="active"?"攻击倍率":"固定属性";
        double current=session.TalentValue(h,n);string amount=unit=="百分比"?(current*100).ToString("0.##")+"%":current.ToString("0.##");
        string next=rank<n.MaxRank?"\n下一级基础效果："+(n.Value+n.PerRank*rank).ToString("0.##")+(unit=="百分比"?"（比例）":""):"\n已达可学习最高等级";
        session.CanLearnTalent(h,id,out string reason);
        return $"{n.Name} · {(n.Kind=="active"?"主动 / 自动施放":"被动 / 学习即生效")}\n{n.TreeName} · 第{n.Row+1}排\n{n.Description}\n学习等级 {rank}/{n.MaxRank} · 额外 +{effective-rank} · 生效等级 {effective}\n当前效果：{amount} {unit}{next}\n本系投入 {session.TreeInvestment(h,n.Tree)} / 要求 {config.Talents.Required(n)}\n前置：{(n.Prerequisite==""?"无":config.Talents.Nodes[n.Prerequisite].Name+" "+n.RequiredRank+"级")}\n{(n.Kind=="active"?$"伤害类型 {TalentDamageName(n.DamageType)} · 冷却 {n.Cooldown:0.#}秒 · 距离 {n.Range:0.#} · 半径 {n.Radius:0.#}\n":"")}{(n.BonusAllowed?"额外等级不计入投入，不解锁未学习节点":$"最高等级为{n.MaxRank}，不享受装备技能加成")}\n{reason}";
    }
    private void RefreshTalentPage()
    {
        if(GetViewport().GuiIsDragging())return;
        var h=SelectedTalentHero;
        string stamp=h==null?"none":$"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(session!)}:{h.SpecId}:{h.Level}:{session!.ActiveSkillSlots}:{string.Join(';',h.Talents)}:{string.Join(';',h.TalentBonuses)}:{string.Join(';',h.SkillIds)}:{session.Stars.Level("s_respec")}";
        if(stamp==talentStamp)return;talentStamp=stamp;
        var offsets=talentScrollHero==h?.SpecId?talentTrees.GetChildren().OfType<VBoxContainer>().Select(c=>c.GetChildren().OfType<ScrollContainer>().First().ScrollVertical).ToArray():Array.Empty<int>();
        talentScrollHero=h?.SpecId??"";ClearTalentChildren(talentTrees);ClearTalentChildren(talentBar);
        if(h==null||session==null){talentHeading.Text="请先创建英雄";return;}
        talentHeading.Text=$"{h.Name} · 可用天赋点 {session.TalentPoints(h)}";
        for(int tree=0;tree<3;tree++)
        {
            int index=tree;var nodes=config.Talents.Nodes.Values.Where(n=>n.Hero==h.SpecId&&n.Tree==index).ToArray();
            var column=new VBoxContainer {CustomMinimumSize=new Vector2(322,453)};talentTrees.AddChild(column);
            column.AddChild(UiLabel($"{nodes.FirstOrDefault()?.TreeName??$"天赋系 {tree+1}"} · 已投入 {session.TreeInvestment(h,tree)}",16));
            var scroll=new DragScrollContainer {CustomMinimumSize=new Vector2(322,425),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};column.AddChild(scroll);
            var canvas=HudPanel(scroll,Vector2.Zero,new Vector2(306,Math.Max(415,(nodes.Select(n=>n.Row).DefaultIfEmpty(0).Max()+1)*112)));canvas.CustomMinimumSize=canvas.Size;canvas.MouseFilter=Control.MouseFilterEnum.Pass;
            if(nodes.Length==0)HudText(canvas,"暂无技能",new Vector2(20,24),16);
            foreach(var n in nodes.Where(n=>n.Prerequisite!=""))
            {
                var parent=config.Talents.Nodes[n.Prerequisite];var line=new Line2D {Width=2,DefaultColor=new Color("716342"),Points=new[]{new Vector2(51+parent.Col*100,75+parent.Row*112),new Vector2(51+n.Col*100,20+n.Row*112)}};canvas.AddChild(line);
            }
            foreach(var n in nodes)
            {
                bool can=session.CanLearnTalent(h,n.Id,out _);int rank=h.Talents.GetValueOrDefault(n.Id);
                var button=new InteractionButton {Text=$"{n.Name}\n{rank}/{n.MaxRank}\n{(n.Kind=="active"?"主动":"被动")}",Position=new Vector2(5+n.Col*100,15+n.Row*112),Size=new Vector2(96,74),TooltipText=TalentTooltip(h,n.Id)};button.AddThemeFontSizeOverride("font_size",12);button.Modulate=can?new Color("e7ba71"):rank>0?new Color("94d8c2"):new Color("75808c");canvas.AddChild(button);
                button.Details=()=>TalentTooltip(h,n.Id);button.DragData=()=>InteractionButton.Payload("talent",h.HeroInstanceId,n.Id);
                button.Pressed+=()=>{bool equipped=h.SkillIds.Contains(n.Id);session.LearnTalent(h.HeroInstanceId,n.Id,out var reason);talentMessage.Text=reason!=""?reason:"已学习 "+n.Name+(!equipped&&h.SkillIds.Contains(n.Id)?" · 已自动装配到空槽":"");talentStamp="";RefreshTalentPage();Refresh();SaveNow();};
            }
            if(nodes.Length==0)HudText(canvas,"该职业天赋尚未配置",new Vector2(35,35),14);
            if(tree<offsets.Length)scroll.SetDeferred("scroll_vertical",offsets[tree]);
        }
        for(int i=0;i<config.Talents.Rule("active_skill_max_slots");i++){int index=i;var slot=TalentSlot(h,h.SkillIds.ElementAtOrDefault(i)??"",new Vector2(135,43));if(i>=session.ActiveSkillSlots){slot.Text="星图解锁";slot.Disabled=true;}talentBar.AddChild(slot);ConfigureTalentDrop(slot,h,index,false);slot.Pressed+=()=>OpenTalentEditor(h.HeroInstanceId,index);}
    }
    private void OpenTalentEditor(string heroId,int? slot=null)
    {
        if(session==null)return;var selected=session.Heroes.SingleOrDefault(h=>h.HeroInstanceId==heroId)??(session.Heroes.Count(h=>h.SpecId==heroId)==1?session.Heroes.Single(h=>h.SpecId==heroId):null);if(selected==null)return;editingTalentHero=selected.HeroInstanceId;talentDraft.Clear();talentDraft.AddRange(selected.SkillIds);selectedTalentSlot=slot??NextTalentSlot();RefreshTalentEditor();talentEditor.Show();talentEditor.MoveToFront();
    }
    private void RefreshTalentEditor()
    {
        if(session==null)return;var h=session.Heroes.FirstOrDefault(h=>h.HeroInstanceId==editingTalentHero);if(h==null){talentEditor.Hide();return;}
        var scroll=(ScrollContainer)talentList.GetParent();int offset=scroll.ScrollVertical;ClearTalentChildren(talentEditorSlots);ClearTalentChildren(talentList);
        talentEditorHint.Text=$"{h.Name} · 点击技能优先填空；满槽先选替换位置，或拖到目标槽。确认后生效。";
        for(int i=0;i<config.Talents.Rule("active_skill_max_slots");i++)
        {
            int index=i;var b=TalentSlot(h,talentDraft.ElementAtOrDefault(i)??"",new Vector2(170,65));if(i>=session.ActiveSkillSlots){b.Text="星图解锁";b.Disabled=true;}else if(index==selectedTalentSlot)b.Modulate=new Color("e7ba71");talentEditorSlots.AddChild(b);ConfigureTalentDrop(b,h,index,true);b.Pressed+=()=>{selectedTalentSlot=index;RefreshTalentEditor();};
        }
        foreach(var n in config.Talents.Nodes.Values.Where(n=>n.Hero==h.SpecId&&n.Kind=="active").OrderByDescending(n=>h.Talents.ContainsKey(n.Id)).ThenBy(n=>n.Tree).ThenBy(n=>n.Row))
        {
            bool learned=h.Talents.ContainsKey(n.Id);var b=new InteractionButton {Text=$"{n.Name} · {n.TreeName} · {(learned?$"等级 {session.TalentLevel(h,n.Id)}":$"未学习：前往{n.TreeName}第{n.Row+1}排解锁")}",CustomMinimumSize=new Vector2(865,48),TooltipText=TalentTooltip(h,n.Id)};b.Details=()=>TalentTooltip(h,n.Id);b.DragData=()=>InteractionButton.Payload("talent",h.HeroInstanceId,n.Id);talentList.AddChild(b);if(talentDraft.Contains(n.Id))b.Text+=" · 已装配";if(!learned)b.Modulate=new Color("77818b");
            b.Pressed+=()=>{if(!learned){talentEditorHint.Text=$"请在{n.TreeName}第{n.Row+1}排学习{n.Name}。";return;}PutTalentDraft(n.Id,null);};
        }
        scroll.SetDeferred("scroll_vertical",offset);
    }
    private void RefreshHudSkills()
    {
        if(!ReferenceEquals(hudTalentSession,session)){hudTalentSession=session;foreach(var b in hudSkillBars)if(b!=null)b.SetMeta("stamp","refresh");}
        if(talentEditor!=null&&talentEditor.Visible&&(session==null||!session.Heroes.Any(h=>h.HeroInstanceId==editingTalentHero)))talentEditor.Hide();
        for(int i=0;i<5;i++)
        {
            var bar=hudSkillBars[i];if(bar==null)continue;var h=session?.Heroes.ElementAtOrDefault(i);
            string stamp=h==null?"":h.SpecId+":"+session!.ActiveSkillSlots+":"+string.Join(';',h.SkillIds);
            if(bar.GetMeta("stamp","").AsString()==stamp)continue;bar.SetMeta("stamp",stamp);ClearTalentChildren(bar);if(h==null)continue;
            for(int j=0;j<config.Talents.Rule("active_skill_max_slots");j++){int index=j;var slot=TalentSlot(h,h.SkillIds.ElementAtOrDefault(j)??"",new Vector2(61,28));if(j>=session!.ActiveSkillSlots){slot.Text="锁";slot.Disabled=true;}bar.AddChild(slot);ConfigureTalentDrop(slot,h,index,false);slot.Pressed+=()=>OpenTalentEditor(h.HeroInstanceId,index);}
            var edit=new Button {Text="编辑",CustomMinimumSize=new Vector2(48,28)};edit.AddThemeFontSizeOverride("font_size",11);bar.AddChild(edit);edit.Pressed+=()=>OpenTalentEditor(h.HeroInstanceId);
        }
    }
    private int NextTalentSlot()=>session!=null&&talentDraft.Count<session.ActiveSkillSlots?talentDraft.Count:-1;
    private bool ReadTalentDrop(Variant data,Actor h,out string id)
    {
        return InteractionButton.ReadPayload(data,"talent",h.HeroInstanceId,out id)&&session?.Heroes.Contains(h)==true&&config.Talents.Nodes.TryGetValue(id,out var n)&&n.Hero==h.SpecId&&n.Kind=="active"&&h.Talents.ContainsKey(id);
    }
    /// <summary>点击优先填空；显式拖到占用槽时交换或替换，草稿不重复，不修改运行状态。</summary>
    private void PutTalentDraft(string id,int? target)
    {
        if(session==null)return;
        int old=talentDraft.IndexOf(id);
        if(!target.HasValue&&old>=0){selectedTalentSlot=old;RefreshTalentEditor();talentEditorHint.Text="该技能已装配，已定位对应槽位。";return;}
        int slot=target??(talentDraft.Count<session.ActiveSkillSlots?talentDraft.Count:selectedTalentSlot);
        if(slot<0||slot>=session.ActiveSkillSlots){talentEditorHint.Text="槽位已满，请先选择替换位置，或将技能拖到目标槽位。";return;}
        slot=Math.Min(slot,talentDraft.Count);
        if(old>=0)
        {
            if(slot==talentDraft.Count){talentDraft.RemoveAt(old);talentDraft.Add(id);}
            else (talentDraft[old],talentDraft[slot])=(talentDraft[slot],talentDraft[old]);
        }
        else if(slot==talentDraft.Count)talentDraft.Add(id);else talentDraft[slot]=id;
        selectedTalentSlot=NextTalentSlot();RefreshTalentEditor();talentEditorHint.Text="装配草稿已更新；确认后生效，战斗中新换入技能需完整冷却。";
    }
    private void ConfigureTalentDrop(InteractionButton button,Actor h,int index,bool draft)
    {
        button.AcceptDrop=data=>session!=null&&index<session.ActiveSkillSlots&&ReadTalentDrop(data,h,out _);
        button.ReceiveDrop=data=>
        {
            if(!ReadTalentDrop(data,h,out var id)||session==null)return;
            if(draft){PutTalentDraft(id,index);return;}
            var next=h.SkillIds.ToList();int slot=Math.Min(index,next.Count),old=next.IndexOf(id);
            if(old>=0)
            {
                if(slot==next.Count){next.RemoveAt(old);next.Add(id);}else (next[old],next[slot])=(next[slot],next[old]);
            }
            else if(slot==next.Count)next.Add(id);else next[slot]=id;
            if(next.SequenceEqual(h.SkillIds))return;
            session.SetTalentLoadout(h.HeroInstanceId,next,out string reason);talentMessage.Text=reason==""?"已装配 "+config.Talents.Nodes[id].Name:reason;
            talentStamp="";RefreshTalentPage();Refresh();SaveNow();
        };
    }
}
