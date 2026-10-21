using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
public partial class Main
{
    private Control formationRoot=null!;
    private VBoxContainer formationSeats=null!;
    private GridContainer formationCandidates=null!;
    private Label formationHint=null!,formationCurrent=null!;
    private Button formationConfirm=null!,formationRemove=null!,formationRevoke=null!;
    private readonly string[] formationDraft=new string[5];
    private int formationSelected;
    private bool formationSeatChosen;
    private string formationStamp="";
    private Button formationPlanCard=null!;
    private void BuildFormationUi()
    {
        formationRoot=new Control {Name="FormationDialog",Size=new Vector2(1440,900),Visible=false};uiLayer.AddChild(formationRoot);
        formationRoot.AddChild(new ColorRect {Size=new Vector2(1440,900),Color=new Color(0,0,0,.65f)});
        var panel=HudPanel(formationRoot,new Vector2(248,154),new Vector2(944,560));
        HudText(panel,"远征编队",new Vector2(20,14),23);
        formationRevoke=new Button {Text="撤销待执行计划",Position=new Vector2(740,14),Size=new Vector2(184,30)};panel.AddChild(formationRevoke);formationRevoke.Pressed+=()=>{session?.CancelFormation();SaveNow();Refresh();RefreshFormationDraft();};
        HudText(panel,"点击英雄优先填空；选席位替换，或拖动英雄交换。确认后到凯恩处生效。",new Vector2(20,47),14);
        formationSeats=new VBoxContainer {Position=new Vector2(20,85),Size=new Vector2(206,380)};formationSeats.AddThemeConstantOverride("separation",10);panel.AddChild(formationSeats);
        var area=HudPanel(panel,new Vector2(242,85),new Vector2(682,380));
        var scroll=new DragScrollContainer {Position=new Vector2(10,10),Size=new Vector2(662,360),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};area.AddChild(scroll);
        formationCandidates=new GridContainer {Columns=5,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};formationCandidates.AddThemeConstantOverride("h_separation",8);formationCandidates.AddThemeConstantOverride("v_separation",10);scroll.AddChild(formationCandidates);
        formationHint=HudText(panel,"",new Vector2(20,476),14);formationHint.Size=new Vector2(904,24);formationHint.ClipText=true;
        formationCurrent=HudText(panel,"",new Vector2(20,505),13);formationCurrent.Size=new Vector2(440,40);formationCurrent.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        formationRemove=new Button {Text="移出所选席位",Position=new Vector2(465,512),Size=new Vector2(138,32)};panel.AddChild(formationRemove);
        formationRemove.Pressed+=()=>{if(formationDraft.Count(id=>!string.IsNullOrEmpty(id))<=1){formationHint.Text="至少保留一名英雄。";return;}formationDraft[formationSelected]="";RefreshFormationDraft();};
        var cancel=new Button {Text="取消",Position=new Vector2(612,512),Size=new Vector2(100,32)};cancel.Pressed+=()=>formationRoot.Hide();panel.AddChild(cancel);
        formationConfirm=new Button {Text="确认编队计划",Position=new Vector2(724,512),Size=new Vector2(200,32)};formationConfirm.Pressed+=ConfirmFormation;panel.AddChild(formationConfirm);
        formationPlanCard=new Button {Name="FormationPlanCard",CustomMinimumSize=new Vector2(0,118),Alignment=HorizontalAlignment.Left,ClipText=true,Visible=false};formationPlanCard.AddThemeFontSizeOverride("font_size",14);interactionList.AddChild(formationPlanCard);formationPlanCard.Pressed+=()=>OpenFormation(0);
    }
    private void OpenFormation(int slot)
    {
        if(session==null){heroNotice.DialogText="请先开始远征。";heroNotice.PopupCentered();return;}
        var ids=session.PendingFormation.Count>0?session.PendingFormation.ToArray():session.Heroes.Select(h=>h.SpecId).ToArray();
        for(int i=0;i<5;i++)formationDraft[i]=i<ids.Length?ids[i]:"";
        formationSeatChosen=false;formationSelected=Math.Clamp(slot,0,4);formationRoot.Show();formationRoot.MoveToFront();RefreshFormationDraft();
    }
    private void RefreshFormationDraft()
    {
        foreach(var child in formationSeats.GetChildren()){formationSeats.RemoveChild(child);child.QueueFree();}
        foreach(var child in formationCandidates.GetChildren()){formationCandidates.RemoveChild(child);child.QueueFree();}
        if(session==null){formationRoot.Hide();return;}
        int cap=session.PartyCapacity;formationRevoke.Disabled=session.PendingFormation.Count==0;
        for(int i=0;i<5;i++)
        {
            int slot=i;bool locked=i>=cap;string id=formationDraft[i]??"";
            var name=id==""?"":config.Heroes.Single(h=>h.Id==id).Name;
            var b=new InteractionButton {Name="FormationSeat"+i,Text=$"席位 {i+1}{(formationSelected==i?" · 已选":"")}\n"+(locked?"未解锁 · 前往星图扩编":id==""?"+  已解锁 · 席位空缺":name+" · 计划上阵"),CustomMinimumSize=new Vector2(206,66)};
            if(formationSelected==i)b.AddThemeStyleboxOverride("normal",new StyleBoxFlat {BgColor=new Color("253947"),BorderColor=new Color("d5b773"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2});b.Pressed+=()=>{formationSelected=slot;formationSeatChosen=true;RefreshFormationDraft();};
            if(id!=""){b.Details=()=>FormationHeroDetails(id);b.DragData=()=>InteractionButton.Payload("hero","formation",id);}
            b.AcceptDrop=data=>slot<session.PartyCapacity&&ReadFormationDrop(data,out _);b.ReceiveDrop=data=>{if(ReadFormationDrop(data,out var heroId))ChooseFormationHero(heroId,slot);};formationSeats.AddChild(b);
        }
        foreach(var spec in config.Heroes)
        {
            string id=spec.Id;bool unlocked=session.HeroUnlocked(id)||session.Heroes.Concat(session.ReserveHeroes).Any(h=>h.SpecId==id);bool planned=formationDraft.Contains(id);bool active=session.Heroes.Any(h=>h.SpecId==id);
            string state=!unlocked?$"未解锁\n累计击杀 {session.Kills}/{config.HeroUnlockKills[id]}":planned?(active?"已上阵\n草稿保留":"已解锁\n计划上阵"):active?"已上阵\n草稿已移出":"已解锁\n待命";
            var button=new InteractionButton {Name="FormationHero_"+id,Text=spec.Name+"\n\n"+state,CustomMinimumSize=new Vector2(124,120),Disabled=!unlocked,TooltipText=!unlocked?$"累计击杀达到 {config.HeroUnlockKills[id]} 后解锁":"点击安排到所选席位"};
            button.Details=()=>FormationHeroDetails(id);if(unlocked)button.DragData=()=>InteractionButton.Payload("hero","formation",id);button.AddThemeFontSizeOverride("font_size",13);button.Pressed+=()=>ChooseFormationHero(id);formationCandidates.AddChild(button);
        }
        formationRemove.Disabled=formationSelected>=cap||string.IsNullOrEmpty(formationDraft[formationSelected])||formationDraft.Count(id=>!string.IsNullOrEmpty(id))<=1;
        var chosen=formationDraft.Where(id=>!string.IsNullOrEmpty(id)).ToArray();
        formationConfirm.Disabled=chosen.Length==0||chosen.SequenceEqual(session.Heroes.Select(h=>h.SpecId))||chosen.SequenceEqual(session.PendingFormation);
        formationHint.Text=formationSelected>=cap?"此席位尚未解锁：请先在星图购买远征扩编节点。":"草稿不会立即改变队伍；清房、救援和拾取完成后回城，走近凯恩变更编队。";
        formationCurrent.Text=$"已解锁席位 {cap}/5 · 草稿 {formationDraft.Count(id=>!string.IsNullOrEmpty(id))} 人"+(session.PendingFormation.Count>0?"\n已有编队计划，确认将更新该计划。":"");
        formationStamp=$"{cap}:{session.Kills}";
    }
    private void ChooseFormationHero(string id,int? target=null)
    {
        if(session==null||!HeroCanJoinDraft(id)){formationHint.Text="英雄尚未解锁。";return;}
        int old=Array.IndexOf(formationDraft,id),empty=Array.FindIndex(formationDraft,0,session.PartyCapacity,string.IsNullOrEmpty);
        if(!target.HasValue&&!formationSeatChosen&&old>=0){formationSelected=old;RefreshFormationDraft();formationHint.Text="该英雄已在草稿中，已定位席位；拖到其他席位可交换位置。";return;}
        int slot=target??(formationSeatChosen?formationSelected:empty);
        if(slot<0||slot>=session.PartyCapacity){formationHint.Text="席位已满或尚未解锁，请先选择替换席位或拖动到目标席位。";return;}
        if(old>=0)(formationDraft[old],formationDraft[slot])=(formationDraft[slot],formationDraft[old]);
        else formationDraft[slot]=id;
        formationSeatChosen=false;int next=Array.FindIndex(formationDraft,0,session.PartyCapacity,string.IsNullOrEmpty);formationSelected=next>=0?next:slot;
        RefreshFormationDraft();formationHint.Text="编队草稿已更新；确认加入计划后，回城在凯恩处生效。";
    }
    private bool HeroCanJoinDraft(string id)=>session!=null&&config.Heroes.Any(h=>h.Id==id)&&(session.HeroUnlocked(id)||session.Heroes.Concat(session.ReserveHeroes).Any(h=>h.SpecId==id));
    private bool ReadFormationDrop(Variant data,out string id)=>InteractionButton.ReadPayload(data,"hero","formation",out id)&&HeroCanJoinDraft(id);
    private string FormationHeroDetails(string id)
    {
        var spec=config.Heroes.Single(h=>h.Id==id);var h=session?.Heroes.Concat(session.ReserveHeroes).FirstOrDefault(h=>h.SpecId==id);
        return $"{spec.Name}\n{(h==null?"尚未上阵":$"等级 {h.Level} · 经验 {HeroExperienceText(h)}\nHP {h.Hp:0}/{h.MaxHp:0}\n已学习天赋 {h.Talents.Count} 个 · 已装配主动 {h.SkillIds.Length} 个")}\n{(HeroCanJoinDraft(id)?"已解锁 · 点击优先填空，拖到席位交换或替换":$"未解锁 · 累计击杀 {session?.Kills??0}/{config.HeroUnlockKills[id]}")}\n确认只安排计划；完成当前房间、救援与拾取后回城，到凯恩处变更。";
    }

    private void ConfirmFormation()
    {
        if(session==null)return;
        var ids=formationDraft.Where(id=>!string.IsNullOrEmpty(id)).ToArray();
        if(!session.RequestFormation(ids,out var reason)){formationHint.Text=reason;return;}
        formationRoot.Hide();SaveNow();Refresh();PlaySound("sfx_reward");
    }
    private void RefreshFormationPlan()
    {
        if(formationPlanCard==null)return;
        bool planned=session?.PendingFormation.Count>0;interactionHeading.Text=planned?"计划列表 / 远征交互":"远征交互";formationPlanCard.Visible=planned;
        if(planned)
        {
            formationPlanCard.Text="编队计划 · 已安排\n"+string.Join("、",session!.PendingFormation.Select(id=>config.Heroes.Single(h=>h.Id==id).Name))+"\n"+session.FormationStatus+"\n点击调整计划";
            formationPlanCard.TooltipText=formationPlanCard.Text;interactionList.MoveChild(formationPlanCard,0);interactionEmpty.Hide();
        }
        if(formationRoot.Visible)
        {if(session==null)formationRoot.Hide();else if(!GetViewport().GuiIsDragging()&&formationStamp!=$"{session.PartyCapacity}:{session.Kills}")RefreshFormationDraft();}
    }
}
