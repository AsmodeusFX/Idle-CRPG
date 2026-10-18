using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private static readonly Rect2 GlobalPageRect=new(24,92,1392,784);
    private Control castlePage=null!;
    private CastleMapCanvas castleMap=null!;
    private readonly Dictionary<string,Button> castleRows=new();
    private readonly Dictionary<string,Label> castleStates=new();

    /// <summary>城堡是独立全局页面：左侧列出全部城堡，右侧地图与列表共享进度及选择。</summary>
    private void BuildWorldPage()
    {
        castlePage=HudPanel(uiLayer,GlobalPageRect.Position,GlobalPageRect.Size);castlePage.Visible=false;castlePage.ClipContents=true;castlePage.MouseFilter=Control.MouseFilterEnum.Ignore;
        castleMap=new CastleMapCanvas {OwnerView=this,Size=GlobalPageRect.Size,MouseFilter=Control.MouseFilterEnum.Ignore};castlePage.AddChild(castleMap);
        HudPanel(castlePage,Vector2.Zero,new Vector2(428,784));
        HudText(castlePage,"城堡名称",new Vector2(12,9),17);HudText(castlePage,"攻略状态",new Vector2(250,9),17);
        var scroll=new ScrollContainer {Position=new Vector2(8,42),Size=new Vector2(412,730),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};castlePage.AddChild(scroll);
        var list=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};list.AddThemeConstantOverride("separation",3);scroll.AddChild(list);
        foreach(var castle in config.Dungeons.Where(d=>d.Castle))
        {
            var row=new Button {Name="CastleRow_"+castle.Id,CustomMinimumSize=new Vector2(0,29),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};list.AddChild(row);
            var name=UiLabel(castle.Name,14);name.Position=new Vector2(8,4);name.Size=new Vector2(224,22);name.ClipText=true;name.MouseFilter=Control.MouseFilterEnum.Ignore;row.AddChild(name);
            var status=UiLabel("未开始",14);status.Position=new Vector2(242,3);status.CustomMinimumSize=new Vector2(140,23);status.HorizontalAlignment=HorizontalAlignment.Center;status.MouseFilter=Control.MouseFilterEnum.Ignore;row.AddChild(status);
            castleRows[castle.Id]=row;castleStates[castle.Id]=status;
            int region=castle.RegionId;row.Pressed+=()=>{selectedRegion=region;RefreshWorldPage();SaveNow();};
        }
    }

    private void RefreshWorldPage()
    {
        var world=session?.World;
        foreach(var castle in config.Dungeons.Where(d=>d.Castle))
        {
            var state=castleStates[castle.Id];var row=castleRows[castle.Id];
            if(world==null){state.Text="未开始";row.Disabled=true;continue;}
            row.Disabled=false;
            var status=world.RegionState(castle.RegionId);
            bool unlocked=world.Progress.AreaPartUnlocked(config,config.AreaPart(castle.RegionId).Id);
            var normal=config.Dungeons.Where(d=>!d.Castle&&config.Rulers[d.RegionId]==castle.Id).ToArray();
            int cleared=normal.Count(d=>world.Progress.Cleared.Contains(d.Id));
            state.Text=status==RegionStatus.Occupied?"已占领":status==RegionStatus.PlannedAttack?"计划攻击":!unlocked?"未解锁":status==RegionStatus.WaitingCastle?"等待攻略":$"地牢 {cleared} / {normal.Length}";
            state.AddThemeStyleboxOverride("normal",new StyleBoxFlat {BgColor=StatusColor(status)});
            state.AddThemeColorOverride("font_color",status==RegionStatus.PlannedAttack?new Color("242000"):Colors.White);
            row.Modulate=config.Rulers[selectedRegion<0?world.CurrentRegion:selectedRegion]==castle.Id?new Color("e7d2a4"):Colors.White;
            row.TooltipText=$"{castle.Name} · Lv.{castle.LevelMin}\n{state.Text} · 统治区普通地牢 {cleared}/{normal.Length}\n点击定位统治区；挑战请返回游戏交互区安排。";
        }
        castleMap.QueueRedraw();
    }

    /// <summary>全局地图使用设计坐标绘制，鼠标直接按相同原点和格宽命中。</summary>
    public void RenderCastleMap(CanvasItem context)
    {
        painter=context;context.DrawSetTransform(-GlobalPageRect.Position);
        DrawRect(GlobalPageRect,new Color("080d15"));
        if(session!=null)DrawWorld();
        else Text("请先在游戏页组建小队，开启世界远征。",new Vector2(650,420),20);
    }
}

public partial class CastleMapCanvas : Control
{
    public Main OwnerView=null!;
    public override void _Draw()=>OwnerView.RenderCastleMap(this);
}
