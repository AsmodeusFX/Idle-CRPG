using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using V = System.Numerics.Vector2;

public partial class Main
{
    private WorldProgress? mapProgress;
    private int mapCleared=-1,mapExplored=-1;
    private string mapPlan="";
    private readonly Dictionary<int,RegionStatus> mapStatuses=new();
    private RegionStatus MapStatus(int region)
    {
        var world=session!.World!;var progress=world.Progress;
        string plan=string.Join(",",world.PendingCastles)+"/"+world.Target.Dungeon.Id+"/"+world.Stage;
        if(mapPlan!=plan||!ReferenceEquals(mapProgress,progress)||mapCleared!=progress.Cleared.Count||mapExplored!=progress.Explored.Count)
        {
            mapPlan=plan;mapProgress=progress;mapCleared=progress.Cleared.Count;mapExplored=progress.Explored.Count;mapStatuses.Clear();
            var rulers=new Dictionary<string,RegionStatus>();
            foreach(var r in config.Regions)
            {
                string castle=config.Rulers[r.Id];
                if(!rulers.TryGetValue(castle,out var state))
                {
                    state=world.RegionState(r.Id);
                    if(state==RegionStatus.Sweeping)state=RegionStatus.Unexplored;
                    rulers[castle]=state;
                }
                mapStatuses[r.Id]=state==RegionStatus.Unexplored&&progress.Explored.Contains(r.Id)?RegionStatus.Sweeping:state;
            }
        }
        return mapStatuses[region];
    }
    private void Text(string value,Vector2 position,int size=16,string color="c1d0d8") => DrawString(font,position,value,HorizontalAlignment.Left,-1,size,new Color(color));
    private static string TerrainName(string terrain) => terrain switch {"snow"=>"北境雪原","desert"=>"西部沙海","tropical"=>"南方雨林","coast"=>"东岸湖湾","grass"=>"中央原野",_=>throw new ArgumentOutOfRangeException(nameof(terrain))};
    private static Color TerrainColor(string terrain) => new(terrain switch {"snow"=>"bbcdd0","desert"=>"9b8052","tropical"=>"315b44","coast"=>"47747e","grass"=>"526b52",_=>throw new ArgumentOutOfRangeException(nameof(terrain))});
    private static string StatusName(RegionStatus status) => status switch {RegionStatus.Unexplored=>"未展开探索",RegionStatus.Sweeping=>"地牢扫荡中",RegionStatus.WaitingCastle=>"等待攻略城堡",RegionStatus.Occupied=>"已占领",RegionStatus.PlannedAttack=>"计划攻击",_=>throw new ArgumentOutOfRangeException(nameof(status))};
    private static Color StatusColor(RegionStatus status) => new(status switch {RegionStatus.Unexplored=>"141e27",RegionStatus.Sweeping=>"747e85",RegionStatus.WaitingCastle=>"ad5828",RegionStatus.Occupied=>"39764e",RegionStatus.PlannedAttack=>"ffe600",_=>throw new ArgumentOutOfRangeException(nameof(status))});
    private float MapTile => Math.Min(36,Math.Min(604f/config.WorldWidth,610f/config.WorldHeight));
    private Vector2 MapPoint(V point) => new Vector2(55,184)+new Vector2(point.X,point.Y)*MapTile;

    public override void _UnhandledInput(InputEvent input)
    {
        if(session==null||input is not InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left} mouse)return;
        var w=session.World!;
        bool map=viewMode==1;
        if(!map)return;
        // 城堡页按缩小后的视口绘制，点击使用同一逆变换。
        var mapMouse=(mouse.Position-SceneRect.Position-new Vector2(104,0))/.8f+new Vector2(24,148);
        var p=(mapMouse-new Vector2(55,184))/MapTile;
        if(p.X>=0&&p.Y>=0&&p.X<config.WorldWidth&&p.Y<config.WorldHeight)
        {selectedRegion=config.Regions.Single(r=>r.X==(int)p.X&&r.Y==(int)p.Y).Id;RefreshCastleButton();SaveNow();}
    }

    private void DrawWorld()
    {
        var world=session!.World!;
        Text("世界疆域",new Vector2(55,165),20,"e7d2a4");
        Text($"{config.WorldWidth} × {config.WorldHeight}  /  北境霜雪 · 南方雨林 · 西部沙海 · 东岸湖湾",new Vector2(193,163),13,"91a6b7");
        float tile=MapTile;
        foreach(var r in config.Regions)
        {
            var status=MapStatus(r.Id);
            var pos=MapPoint(new V(r.X,r.Y));
            DrawRect(new Rect2(pos,new Vector2(tile-2,tile-2)),StatusColor(status));
            if(status!=RegionStatus.Unexplored)
            {
                DrawRect(new Rect2(pos+new Vector2(2,2),new Vector2(tile-6,5)),TerrainColor(r.Terrain).Darkened(0.2f));
                var road=new Color(0.88f,0.79f,0.61f,0.3f);
                DrawLine(pos+new Vector2(0,tile*.5f),pos+new Vector2(tile-2,tile*.5f),road,2);
                DrawLine(pos+new Vector2(tile*.5f,0),pos+new Vector2(tile*.5f,tile-2),road,2);
                Decoration(pos+new Vector2(8,tile-7),r.Terrain,0.42f,r.Id);
                var village=config.Villages.Single(v=>v.RegionId==r.Id);
                DrawRect(new Rect2(pos+new Vector2(village.X,village.Y)*tile-new Vector2(2,2),new Vector2(4,4)),new Color("d6c7a4"));
            }
            if(config.Dungeons.Any(d=>d.RegionId==r.Id&&d.Castle))CastleIcon(pos+new Vector2(tile*.65f,tile*.36f),0.65f);
            if(r.Id==selectedRegion)DrawRect(new Rect2(pos-new Vector2(1,1),new Vector2(tile,tile)),new Color("e7d2a4"),false,2);
        }
        // 勾勒选中统治区的外边界，方便识别不规则辖区，不改变探索状态颜色。
        string selectedCastle=config.Rulers[selectedRegion<0?world.CurrentRegion:selectedRegion];
        var selectedCells=config.Regions.Where(r=>config.Rulers[r.Id]==selectedCastle).Select(r=>(r.X,r.Y)).ToHashSet();
        foreach(var cell in selectedCells)
        {
            var p=MapPoint(new V(cell.X,cell.Y));var color=new Color("d4b77d");
            if(!selectedCells.Contains((cell.X-1,cell.Y)))DrawLine(p,p+new Vector2(0,tile),color,2);
            if(!selectedCells.Contains((cell.X+1,cell.Y)))DrawLine(p+new Vector2(tile,0),p+new Vector2(tile,tile),color,2);
            if(!selectedCells.Contains((cell.X,cell.Y-1)))DrawLine(p,p+new Vector2(tile,0),color,2);
            if(!selectedCells.Contains((cell.X,cell.Y+1)))DrawLine(p+new Vector2(0,tile),p+new Vector2(tile,tile),color,2);
        }
        var route=new[]{world.Position}.Concat(world.Route).Select(MapPoint).ToArray();
        if(route.Length>1)DrawPolyline(route,new Color("71d9d0"),2,true);
        var marker=MapPoint(VisualWorldPosition);
        DrawCircle(marker,8,new Color("0b1019"));DrawCircle(marker,5,new Color("71e4d0"));
        DrawArc(marker,10,0,Mathf.Tau,24,new Color("71e4d0"),1,true);
        var current=config.Regions.Single(r=>r.Id==world.CurrentRegion);
        DrawRect(new Rect2(MapPoint(new V(current.X,current.Y)),new Vector2(tile-2,tile-2)),new Color("71e4d0"),false,2);

        int selected=selectedRegion<0?world.CurrentRegion:selectedRegion;
        var region=config.Regions.Single(r=>r.Id==selected);
        var part=config.AreaPart(selected);bool partUnlocked=world.Progress.AreaPartUnlocked(config,part.Id);
        var origin=new Vector2(680,185);
        DrawRect(new Rect2(origin,new Vector2(352,573)),new Color("111c26"));
        Text(region.Name,origin+new Vector2(22,34),23,"e7d2a4");
        Text($"区域 {region.Id:D3}  /  {TerrainName(region.Terrain)}",origin+new Vector2(22,62),15,"91a6b7");
        Text(partUnlocked?$"{part.Id} · {StatusName(MapStatus(selected))}":$"{part.Id} · 统治区未解锁",origin+new Vector2(22,96),19);
        var normals=config.Dungeons.Where(d=>d.RegionId==selected&&!d.Castle).OrderBy(d=>d.LevelMin).ToArray();
        Text($"地牢 {normals.Count(d=>world.Progress.Cleared.Contains(d.Id))}/{normals.Length} 已通关",origin+new Vector2(22,132));
        int line=0;
        foreach(var d in normals.Take(3))
            Text($"{(world.Progress.Cleared.Contains(d.Id)?"✓":"·")} {d.Name}  Lv.{d.LevelMin}",origin+new Vector2(22,160+line++*25),14);
        var ruler=config.Dungeons.Single(d=>d.Id==config.Rulers[selected]);
        int total=config.Dungeons.Count(d=>!d.Castle&&config.Rulers[d.RegionId]==ruler.Id);
        int complete=config.Dungeons.Count(d=>!d.Castle&&config.Rulers[d.RegionId]==ruler.Id&&world.Progress.Cleared.Contains(d.Id));
        Text("统治城堡 / "+ruler.Name,origin+new Vector2(22,244),15,"e7d2a4");
        Text($"统治区普通地牢 {complete}/{total}",origin+new Vector2(22,270),15);
        string prerequisites=string.Join(" / ",config.AreaPartPrerequisites.Where(e=>e.AreaPartId==part.Id).Select(e=>e.RequiredAreaPartId));
        Text(!partUnlocked?"前置任一："+prerequisites:world.Progress.Cleared.Contains(ruler.Id)?"统治区域已占领":world.Progress.CastleUnlocked(config,ruler.Id)?"城堡已解锁":"尚未满足城堡挑战条件",origin+new Vector2(22,295),14,"91a6b7");
        DrawLine(origin+new Vector2(22,315),origin+new Vector2(330,315),new Color("344651"));
        Text(world.Target.Farming?"当前任务 · 自动复刷":"当前任务 · 开拓远征",origin+new Vector2(22,346),17,"71e4d0");
        Text(world.Target.Dungeon.Name,origin+new Vector2(22,374),17);
        Text($"已探索 {world.Progress.Explored.Count}/{config.Regions.Count}  ·  占领 {mapStatuses.Values.Count(s=>s==RegionStatus.Occupied)}",origin+new Vector2(22,402),14,"91a6b7");
        for(int i=0;i<5;i++)
        {
            var state=(RegionStatus)i;var p=origin+new Vector2(22,427+i*27);
            DrawRect(new Rect2(p,new Vector2(14,14)),StatusColor(state));Text(StatusName(state),p+new Vector2(25,13),14);
        }
        Text("点击格子查看区域 · 青色圆点标记小队",new Vector2(55,792),14,"91a6b7");
    }

    private void CastleIcon(Vector2 p,float scale)
    {
        var c=new Color("e7ba71");
        DrawRect(new Rect2(p+new Vector2(-10,-4)*scale,new Vector2(20,13)*scale),c);
        foreach(int x in new[]{-10,-2,6})DrawRect(new Rect2(p+new Vector2(x,-9)*scale,new Vector2(4,7)*scale),c);
        DrawRect(new Rect2(p+new Vector2(-2,1)*scale,new Vector2(5,8)*scale),new Color("28313b"));
    }
    private void Decoration(Vector2 p,string terrain,float scale,int seedValue)
    {
        if(terrain=="desert")
        {
            DrawArc(p,12*scale,Mathf.Pi*1.1f,Mathf.Pi*1.9f,12,new Color("e0bd79"),2*scale,true);
            DrawLine(p+new Vector2(10,-3)*scale,p+new Vector2(10,-13)*scale,new Color("a7b074"),2*scale);return;
        }
        if(terrain=="coast")
        {
            DrawArc(p,12*scale,0,Mathf.Pi,12,new Color("92c7d4"),2*scale,true);
            DrawArc(p+new Vector2(6,4)*scale,10*scale,0,Mathf.Pi,12,new Color("6197b5"),2*scale,true);return;
        }
        DrawLine(p,p+new Vector2(0,-10)*scale,new Color("827558"),3*scale);
        var green=new Color(terrain=="snow"?"54766e":terrain=="tropical"?"70a66e":"7d9a69");
        if(terrain=="tropical")
        {
            for(int i=0;i<5;i++)DrawCircle(p+new Vector2((i%2*2-1)*7,-10-i/2*6)*scale,7*scale,green);
        }
        else
        {
            DrawColoredPolygon(new[]{p+new Vector2(0,-24)*scale,p+new Vector2(12,-4)*scale,p+new Vector2(-12,-4)*scale},green);
            if(terrain=="snow")DrawColoredPolygon(new[]{p+new Vector2(0,-24)*scale,p+new Vector2(5,-15)*scale,p+new Vector2(-5,-15)*scale},new Color("e0eded"));
        }
    }
    private void House(Vector2 p,string terrain)
    {
        DrawRect(new Rect2(p-new Vector2(20,24),new Vector2(40,28)),new Color("9b937e"));
        DrawColoredPolygon(new[]{p+new Vector2(-28,-24),p+new Vector2(0,-47),p+new Vector2(28,-24)},new Color(terrain=="snow"?"d2e2e4":"8e6051"));
        DrawRect(new Rect2(p-new Vector2(5,11),new Vector2(10,15)),new Color("35424c"));
        DrawRect(new Rect2(p+new Vector2(10,-19),new Vector2(6,7)),new Color("edca88"));
    }
}
