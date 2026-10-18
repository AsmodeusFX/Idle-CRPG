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
    private Label selectedTileTitle=null!,selectedTileInfo=null!;
    private VBoxContainer selectedTileDungeons=null!;
    private readonly Dictionary<string,Label> selectedDungeonLabels=new();
    private int detailRegion=-1;
    private ScrollContainer castleListScroll=null!;
    private static readonly Color SelectedTileColor=new("80b8ff");
    private static readonly Color SelectedDominionColor=new("d4b77d");

    /// <summary>城堡全局页：地图居左、章节选择在地图上方，详情和城堡清单位于右栏。</summary>
    private void BuildWorldPage()
    {
        castlePage=HudPanel(uiLayer,GlobalPageRect.Position,GlobalPageRect.Size);castlePage.Visible=false;castlePage.ClipContents=true;castlePage.MouseFilter=Control.MouseFilterEnum.Ignore;
        castleMap=new CastleMapCanvas {OwnerView=this,Size=GlobalPageRect.Size,MouseFilter=Control.MouseFilterEnum.Ignore};castlePage.AddChild(castleMap);
        var sidebar=new Control {Position=new Vector2(964,48),Size=new Vector2(428,736)};castlePage.AddChild(sidebar);
        BuildChapterControls();
        HudPanel(sidebar,Vector2.Zero,new Vector2(428,250));
        selectedTileTitle=HudText(sidebar,"选中地块",new Vector2(12,9),19);
        selectedTileTitle.AddThemeColorOverride("font_color",SelectedTileColor);
        selectedTileInfo=HudText(sidebar,"请先开启世界远征。",new Vector2(12,43),14);
        var detailScroll=new ScrollContainer {Position=new Vector2(12,146),Size=new Vector2(404,94),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};sidebar.AddChild(detailScroll);
        selectedTileDungeons=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};detailScroll.AddChild(selectedTileDungeons);
        HudPanel(sidebar,new Vector2(0,262),new Vector2(428,474));
        HudText(sidebar,"城堡名称",new Vector2(12,271),17);HudText(sidebar,"攻略状态",new Vector2(250,271),17);
        var scroll=new ScrollContainer {Position=new Vector2(8,304),Size=new Vector2(412,420),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};sidebar.AddChild(scroll);castleListScroll=scroll;
        castleList=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};castleList.AddThemeConstantOverride("separation",3);scroll.AddChild(castleList);
        RebuildCastleList();
    }
    private void RebuildCastleList()
    {
        foreach(var child in castleList.GetChildren()){castleList.RemoveChild(child);child.QueueFree();}
        castleRows.Clear();castleStates.Clear();detailRegion=-1;
        listMap=MapConfig;
        foreach(var castle in MapConfig.Dungeons.Where(d=>d.Castle))
        {
            var row=new Button {Name="CastleRow_"+castle.Id,CustomMinimumSize=new Vector2(0,29),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};castleList.AddChild(row);
            var name=UiLabel(castle.Name,14);name.Position=new Vector2(8,4);name.Size=new Vector2(224,22);name.ClipText=true;name.MouseFilter=Control.MouseFilterEnum.Ignore;row.AddChild(name);
            var status=UiLabel("未开始",14);status.Position=new Vector2(242,3);status.CustomMinimumSize=new Vector2(140,23);status.HorizontalAlignment=HorizontalAlignment.Center;status.MouseFilter=Control.MouseFilterEnum.Ignore;row.AddChild(status);
            castleRows[castle.Id]=row;castleStates[castle.Id]=status;
            int region=castle.RegionId;row.Pressed+=()=>SelectMapTile(region);
        }
    }

    private void RefreshWorldPage()
    {
        RefreshChapterControls();
        if(!ReferenceEquals(listMap,MapConfig))RebuildCastleList();
        var world=MapWorld;
        if(world!=null&&(!MapConfig.Regions.Any(r=>r.Id==selectedRegion)))selectedRegion=world.CurrentRegion;
        RefreshSelectedTile();
        foreach(var castle in MapConfig.Dungeons.Where(d=>d.Castle))
        {
            var state=castleStates[castle.Id];var row=castleRows[castle.Id];
            if(world==null){state.Text="未开始";row.Disabled=true;continue;}
            row.Disabled=false;
            var status=world.RegionState(castle.RegionId);
            bool unlocked=world.Progress.AreaPartUnlocked(MapConfig,MapConfig.AreaPart(castle.RegionId).Id);
            var normal=MapConfig.Dungeons.Where(d=>!d.Castle&&MapConfig.Rulers[d.RegionId]==castle.Id).ToArray();
            int cleared=normal.Count(d=>world.Progress.Cleared.Contains(d.Id));
            state.Text=status==RegionStatus.Occupied?"已占领":status==RegionStatus.PlannedAttack?"计划攻击":!unlocked?"未解锁":status==RegionStatus.WaitingCastle?"等待攻略":$"地牢 {cleared} / {normal.Length}";
            state.AddThemeStyleboxOverride("normal",new StyleBoxFlat {BgColor=StatusColor(status)});
            state.AddThemeColorOverride("font_color",status==RegionStatus.PlannedAttack?new Color("242000"):Colors.White);
            bool selected=MapConfig.Rulers[selectedRegion]==castle.Id;
            row.Modulate=Colors.White;
            var selectedStyle=new StyleBoxFlat {BgColor=new Color("20364b"),BorderColor=SelectedTileColor,BorderWidthLeft=3,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1};
            if(selected){row.AddThemeStyleboxOverride("normal",selectedStyle);row.AddThemeStyleboxOverride("hover",selectedStyle);}
            else {row.RemoveThemeStyleboxOverride("normal");row.RemoveThemeStyleboxOverride("hover");}
            row.TooltipText=$"{castle.Name} · Lv.{castle.LevelMin}\n{state.Text} · 统治区普通地牢 {cleared}/{normal.Length}\n点击定位统治区；挑战请返回游戏交互区安排。";
        }
        castleMap.QueueRedraw();
    }

    /// <summary>只改变查看目标；格子详情、城堡清单和统治区边界共同刷新，不安排远征。</summary>
    private void SelectMapTile(int region)
    {
        selectedRegion=region;RefreshWorldPage();
        if(castleRows.TryGetValue(MapConfig.Rulers[region],out var row))castleListScroll.EnsureControlVisible(row);
        SaveNow();
    }

    private void RefreshSelectedTile()
    {
        var world=MapWorld;
        if(world==null){selectedTileTitle.Text="选中地块";selectedTileInfo.Text="请先开启世界远征。";return;}
        var region=MapConfig.Regions.Single(r=>r.Id==selectedRegion);
        var castle=MapConfig.Dungeons.Single(d=>d.Id==MapConfig.Rulers[region.Id]);
        var dungeons=MapConfig.Dungeons.Where(d=>d.RegionId==region.Id).ToArray();
        int cleared=dungeons.Count(d=>world.Progress.Cleared.Contains(d.Id));
        bool unlocked=world.Progress.AreaPartUnlocked(MapConfig,MapConfig.AreaPart(region.Id).Id);
        selectedTileTitle.Text=$"{region.Name} · 地块 {region.Id:000}";
        selectedTileInfo.Text=$"{TerrainName(region.Terrain)} · 坐标 ({region.X+1}, {region.Y+1})\n所属城堡：{castle.Name}\n{(unlocked?StatusName(world.RegionState(region.Id)):"统治区域未解锁")} · 本格地牢 {cleared}/{dungeons.Length}\n蓝色框：选中地块  金色线：所属统治区域";
        if(detailRegion!=region.Id)
        {
            detailRegion=region.Id;selectedDungeonLabels.Clear();
            foreach(var child in selectedTileDungeons.GetChildren()){selectedTileDungeons.RemoveChild(child);child.QueueFree();}
            if(dungeons.Length==0)selectedTileDungeons.AddChild(UiLabel("本格没有地牢",14));
            foreach(var dungeon in dungeons)
            {
                var label=UiLabel("",14);selectedTileDungeons.AddChild(label);selectedDungeonLabels[dungeon.Id]=label;
            }
        }
        foreach(var dungeon in dungeons)selectedDungeonLabels[dungeon.Id].Text=$"{(world.Progress.Cleared.Contains(dungeon.Id)?"✓":"·")} {dungeon.Name}  Lv.{dungeon.LevelMin}  {(world.Progress.Cleared.Contains(dungeon.Id)?"已通关":"未通关")}";
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
