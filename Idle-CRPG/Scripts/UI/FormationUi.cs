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
    private string formationStamp="";
    private Button formationPlanCard=null!;
    private void BuildFormationUi()
    {
        formationRoot=new Control {Name="FormationDialog",Size=new Vector2(1440,900),Visible=false};uiLayer.AddChild(formationRoot);
        formationRoot.AddChild(new ColorRect {Size=new Vector2(1440,900),Color=new Color(0,0,0,.65f)});
        var panel=HudPanel(formationRoot,new Vector2(248,154),new Vector2(944,560));
        HudText(panel,"远征编队",new Vector2(20,14),23);
        formationRevoke=new Button {Text="撤销待执行计划",Position=new Vector2(740,14),Size=new Vector2(184,30)};panel.AddChild(formationRevoke);formationRevoke.Pressed+=()=>{session?.CancelFormation();SaveNow();Refresh();RefreshFormationDraft();};
        HudText(panel,"先选左侧席位，再选右侧英雄。确认后加入计划，到凯恩处生效。",new Vector2(20,47),14);
        formationSeats=new VBoxContainer {Position=new Vector2(20,85),Size=new Vector2(206,380)};formationSeats.AddThemeConstantOverride("separation",10);panel.AddChild(formationSeats);
        var area=HudPanel(panel,new Vector2(242,85),new Vector2(682,380));
        var scroll=new ScrollContainer {Position=new Vector2(10,10),Size=new Vector2(662,360),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};area.AddChild(scroll);
        formationCandidates=new GridContainer {Columns=5,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};formationCandidates.AddThemeConstantOverride("h_separation",8);formationCandidates.AddThemeConstantOverride("v_separation",10);scroll.AddChild(formationCandidates);
        formationHint=HudText(panel,"",new Vector2(20,476),14);formationHint.Size=new Vector2(904,24);formationHint.ClipText=true;
        formationCurrent=HudText(panel,"",new Vector2(20,505),13);formationCurrent.Size=new Vector2(440,40);formationCurrent.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        formationRemove=new Button {Text="移出所选席位",Position=new Vector2(465,512),Size=new Vector2(138,32)};panel.AddChild(formationRemove);
        formationRemove.Pressed+=()=>{formationDraft[formationSelected]="";RefreshFormationDraft();};
        var cancel=new Button {Text="取消",Position=new Vector2(612,512),Size=new Vector2(100,32)};cancel.Pressed+=()=>formationRoot.Hide();panel.AddChild(cancel);
        formationConfirm=new Button {Text="确认编队计划",Position=new Vector2(724,512),Size=new Vector2(200,32)};formationConfirm.Pressed+=ConfirmFormation;panel.AddChild(formationConfirm);
        formationPlanCard=new Button {Name="FormationPlanCard",CustomMinimumSize=new Vector2(0,118),Alignment=HorizontalAlignment.Left,ClipText=true,Visible=false};formationPlanCard.AddThemeFontSizeOverride("font_size",14);interactionList.AddChild(formationPlanCard);formationPlanCard.Pressed+=()=>OpenFormation(0);
    }
    private void OpenFormation(int slot)
    {
        if(session==null){heroNotice.DialogText="请先开始远征。";heroNotice.PopupCentered();return;}
        var ids=session.PendingFormation.Count>0?session.PendingFormation.ToArray():session.Heroes.Select(h=>h.SpecId).ToArray();
        for(int i=0;i<5;i++)formationDraft[i]=i<ids.Length?ids[i]:"";
        formationSelected=Math.Clamp(slot,0,4);formationRoot.Show();formationRoot.MoveToFront();RefreshFormationDraft();
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
            var b=new Button {Name="FormationSeat"+i,Text=$"席位 {i+1}{(formationSelected==i?" · 已选":"")}\n"+(locked?"未解锁 · 前往星图扩编":id==""?"+  已解锁 · 席位空缺":name+" · 计划上阵"),CustomMinimumSize=new Vector2(206,66)};
            if(formationSelected==i)b.AddThemeStyleboxOverride("normal",new StyleBoxFlat {BgColor=new Color("253947"),BorderColor=new Color("d5b773"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2});b.Pressed+=()=>{formationSelected=slot;RefreshFormationDraft();};formationSeats.AddChild(b);
        }
        foreach(var spec in config.Heroes)
        {
            string id=spec.Id;bool unlocked=session.HeroUnlocked(id)||session.Heroes.Concat(session.ReserveHeroes).Any(h=>h.SpecId==id);bool planned=formationDraft.Contains(id);bool active=session.Heroes.Any(h=>h.SpecId==id);
            string state=!unlocked?$"未解锁\n累计击杀 {session.Kills}/{config.HeroUnlockKills[id]}":planned?(active?"已上阵\n草稿保留":"已解锁\n计划上阵"):active?"已上阵\n草稿已移出":"已解锁\n待命";
            var button=new Button {Name="FormationHero_"+id,Text=spec.Name+"\n\n"+state,CustomMinimumSize=new Vector2(124,120),Disabled=!unlocked,TooltipText=!unlocked?$"累计击杀达到 {config.HeroUnlockKills[id]} 后解锁":"点击安排到所选席位"};
            button.AddThemeFontSizeOverride("font_size",13);button.Pressed+=()=>ChooseFormationHero(id);formationCandidates.AddChild(button);
        }
        formationRemove.Disabled=formationSelected>=cap||string.IsNullOrEmpty(formationDraft[formationSelected]);
        formationConfirm.Disabled=!formationDraft.Any(id=>!string.IsNullOrEmpty(id));
        formationHint.Text=formationSelected>=cap?"此席位尚未解锁：请先在星图购买远征扩编节点。":"草稿不会立即改变队伍；清房、救援和拾取完成后回城，走近凯恩变更编队。";
        formationCurrent.Text=$"已解锁席位 {cap}/5 · 草稿 {formationDraft.Count(id=>!string.IsNullOrEmpty(id))} 人"+(session.PendingFormation.Count>0?"\n已有编队计划，确认将更新该计划。":"");
        formationStamp=$"{cap}:{session.Kills}";
    }
    private void ChooseFormationHero(string id)
    {
        if(session==null||formationSelected>=session.PartyCapacity){formationHint.Text="请先选择一个已解锁席位。";return;}
        for(int i=0;i<5;i++)if(formationDraft[i]==id)formationDraft[i]="";
        formationDraft[formationSelected]=id;
        int next=Array.FindIndex(formationDraft,0,session.PartyCapacity,x=>string.IsNullOrEmpty(x));if(next>=0)formationSelected=next;
        RefreshFormationDraft();
    }
    private void ConfirmFormation()
    {
        if(session==null)return;
        var ids=formationDraft.Where(id=>!string.IsNullOrEmpty(id)).ToArray();
        if(!session.RequestFormation(ids,out var reason)){formationHint.Text=reason;return;}
        formationRoot.Hide();SaveNow();Refresh();PlaySound("sfx_reward");heroNotice.Title="编队计划";heroNotice.DialogText="编队计划已加入。\n完成当前房间、救援与拾取后回城，走近凯恩变更队伍。\n可在游戏页右侧计划列表查看或调整。";heroNotice.PopupCentered(new Vector2I(560,190));
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
        {if(session==null)formationRoot.Hide();else if(formationStamp!=$"{session.PartyCapacity}:{session.Kills}")RefreshFormationDraft();}
    }
}
