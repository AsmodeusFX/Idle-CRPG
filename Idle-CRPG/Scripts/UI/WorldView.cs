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
        var world=MapWorld!;var progress=world.Progress;
        string plan=string.Join(",",world.PendingCastles)+"/"+world.Target.Dungeon.Id+"/"+world.Stage;
        if(mapPlan!=plan||!ReferenceEquals(mapProgress,progress)||mapCleared!=progress.Cleared.Count||mapExplored!=progress.Explored.Count)
        {
            mapPlan=plan;mapProgress=progress;mapCleared=progress.Cleared.Count;mapExplored=progress.Explored.Count;mapStatuses.Clear();
            var rulers=new Dictionary<string,RegionStatus>();
            foreach(var r in MapConfig.Regions)
            {
                if(MapConfig.IsTown(r.Id)||!r.Enterable){mapStatuses[r.Id]=RegionStatus.Unexplored;continue;}
                string castle=MapConfig.Rulers[r.Id];
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
    private static string TerrainName(string terrain) => terrain switch {"snow"=>"北境雪原","desert"=>"西部沙海","tropical"=>"南方雨林","coast"=>"东岸湖湾","grass"=>"中央原野","hell"=>"地狱荒原","mountain"=>"山脉","ocean"=>"海洋",_=>throw new ArgumentOutOfRangeException(nameof(terrain))};
    private static Color TerrainColor(string terrain) => new(terrain switch {"snow"=>"bbcdd0","desert"=>"9b8052","tropical"=>"315b44","coast"=>"47747e","grass"=>"526b52","hell"=>"784538","mountain"=>"65727d","ocean"=>"234d69",_=>throw new ArgumentOutOfRangeException(nameof(terrain))});
    private static string StatusName(RegionStatus status) => status switch {RegionStatus.Unexplored=>"未展开探索",RegionStatus.Sweeping=>"地牢扫荡中",RegionStatus.WaitingCastle=>"等待攻略城堡",RegionStatus.Occupied=>"已占领",RegionStatus.PlannedAttack=>"计划攻击",_=>throw new ArgumentOutOfRangeException(nameof(status))};
    private static Color StatusColor(RegionStatus status) => new(status switch {RegionStatus.Unexplored=>"141e27",RegionStatus.Sweeping=>"747e85",RegionStatus.WaitingCastle=>"ad5828",RegionStatus.Occupied=>"39764e",RegionStatus.PlannedAttack=>"ffe600",_=>throw new ArgumentOutOfRangeException(nameof(status))});
    private float MapTile => Math.Min(MapConfig.PoiProfiles.Count>0?84:46,Math.Min(920f/MapConfig.WorldWidth,680f/MapConfig.WorldHeight));
    private Vector2 MapOrigin => new(32+(940-MapConfig.WorldWidth*MapTile)/2,148+(680-MapConfig.WorldHeight*MapTile)/2);
    private Vector2 MapPoint(V point) => MapOrigin+new Vector2(point.X,point.Y)*MapTile;

    public override void _UnhandledInput(InputEvent input)
    {
        if(session==null||input is not InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left} mouse)return;
        var w=MapWorld!;
        bool map=viewMode==1;
        if(!map)return;
        var p=(mouse.Position-MapOrigin)/MapTile;
        if(p.X>=0&&p.Y>=0&&p.X<MapConfig.WorldWidth&&p.Y<MapConfig.WorldHeight)
        {SelectMapTile(MapConfig.Regions.Single(r=>r.X==(int)p.X&&r.Y==(int)p.Y).Id);}
    }

    private void DrawWorld()
    {
        var world=MapWorld!;
        float tile=MapTile;
        foreach(var r in MapConfig.Regions)
        {
            var pos=MapPoint(new V(r.X,r.Y));
            if(MapConfig.IsTown(r.Id)){DrawRect(new Rect2(pos,new Vector2(tile-2,tile-2)),new Color("8d7845"));House(pos+new Vector2(tile*.5f,tile*.65f),"grass");Text(r.Name,pos+new Vector2(5,tile-8),12,"f4e1b9");continue;}
            if(!r.Enterable)
            {
                DrawRect(new Rect2(pos,new Vector2(tile-2,tile-2)),TerrainColor(r.Terrain).Darkened(.4f));
                Decoration(pos+new Vector2(tile*.5f,tile*.65f),r.Terrain,tile/40,r.Id);
                DrawLine(pos+new Vector2(4,4),pos+new Vector2(tile-6,tile-6),new Color("6b7f8b"),1);continue;
            }
            var status=MapStatus(r.Id);
            var fill=StatusColor(status);
            if(MapConfig.AreaStyles.TryGetValue(MapConfig.Rulers[r.Id],out var style)&&status==RegionStatus.Unexplored)fill=new Color(style.Color).Darkened(.35f);
            DrawRect(new Rect2(pos,new Vector2(tile-2,tile-2)),fill);
            if(status!=RegionStatus.Unexplored)
            {
                DrawRect(new Rect2(pos+new Vector2(2,2),new Vector2(tile-6,5)),TerrainColor(r.Terrain).Darkened(0.2f));
                var road=new Color(0.88f,0.79f,0.61f,0.3f);
                DrawLine(pos+new Vector2(0,tile*.5f),pos+new Vector2(tile-2,tile*.5f),road,2);
                DrawLine(pos+new Vector2(tile*.5f,0),pos+new Vector2(tile*.5f,tile-2),road,2);
                Decoration(pos+new Vector2(8,tile-7),r.Terrain,0.42f,r.Id);
                var village=MapConfig.Villages.FirstOrDefault(v=>v.RegionId==r.Id);
                if(village!=null)
                DrawRect(new Rect2(pos+new Vector2(village.X,village.Y)*tile-new Vector2(2,2),new Vector2(4,4)),new Color("d6c7a4"));
            }
            if(MapConfig.Dungeons.Any(d=>d.RegionId==r.Id&&d.Castle))CastleIcon(pos+new Vector2(tile*.65f,tile*.36f),0.65f);
            if(style!=null)
            {
                Text("区域 "+style.Index,pos+new Vector2(7,tile*.28f),14,"f1ede2");
                var poi=MapConfig.Dungeons.FirstOrDefault(d=>d.RegionId==r.Id);
                string label=poi?.Name??"空地";if(label.Length>6)label=label[..5]+"…";
                Text(label,pos+new Vector2(5,tile*.58f),12,"f1ede2");
                Text($"{r.X+1}-{r.Y+1}",pos+new Vector2(5,tile-9),11);
                if(poi is {Castle:false})DrawLandmark(pos+new Vector2(tile-14,25),false,.35f);
            }
            var waypoint=MapConfig.Waypoints.FirstOrDefault(w=>w.Region==r.Id);
            if(waypoint!=null)
            {
                bool active=world.Progress.WaypointUnlocked(MapConfig,waypoint);
                DrawArc(pos+new Vector2(tile-14,tile-14),6,0,MathF.Tau,20,new Color(active?"68d9ef":"71837f"),2,true);
            }
        }
        if(MapConfig.Gates.Count>0)DrawGateBoundaries(tile);
        // 勾勒选中统治区的外边界，方便识别不规则辖区，不改变探索状态颜色。
        MapConfig.Rulers.TryGetValue(selectedRegion<0?world.CurrentRegion:selectedRegion,out string? selectedCastle);
        var selectedCells=MapConfig.Regions.Where(r=>r.Enterable&&!MapConfig.IsTown(r.Id)&&MapConfig.Rulers[r.Id]==selectedCastle).Select(r=>(r.X,r.Y)).ToHashSet();
        foreach(var cell in selectedCells)
        {
            var p=MapPoint(new V(cell.X,cell.Y));var color=SelectedDominionColor;
            if(!selectedCells.Contains((cell.X-1,cell.Y)))DrawLine(p,p+new Vector2(0,tile),color,2);
            if(!selectedCells.Contains((cell.X+1,cell.Y)))DrawLine(p+new Vector2(tile,0),p+new Vector2(tile,tile),color,2);
            if(!selectedCells.Contains((cell.X,cell.Y-1)))DrawLine(p,p+new Vector2(tile,0),color,2);
            if(!selectedCells.Contains((cell.X,cell.Y+1)))DrawLine(p+new Vector2(0,tile),p+new Vector2(tile,tile),color,2);
        }
        var route=new[]{world.Position}.Concat(world.Route).Select(MapPoint).ToArray();
        if(ViewingCurrentMap&&route.Length>1)DrawPolyline(route,new Color("71d9d0"),2,true);
        if(ViewingCurrentMap)
        {
        var marker=MapPoint(VisualWorldPosition);
        DrawCircle(marker,8,new Color("0b1019"));DrawCircle(marker,5,new Color("71e4d0"));
        DrawArc(marker,10,0,Mathf.Tau,24,new Color("71e4d0"),1,true);
        var current=MapConfig.Regions.Single(r=>r.Id==world.CurrentRegion);
        DrawRect(new Rect2(MapPoint(new V(current.X,current.Y)),new Vector2(tile-2,tile-2)),new Color("71e4d0"),false,2);
        }
        // 选中格内缩绘制并置顶，和金色辖区外轮廓、青色玩家位置分别辨识。
        var selected=MapConfig.Regions.Single(r=>r.Id==(selectedRegion<0?world.CurrentRegion:selectedRegion));
        DrawRect(new Rect2(MapPoint(new V(selected.X,selected.Y))+new Vector2(3,3),new Vector2(tile-8,tile-8)),SelectedTileColor,false,3);

        for(int i=0;i<5;i++)
        {
            var state=(RegionStatus)i;var p=new Vector2(40+i*181,856);
            DrawRect(new Rect2(p,new Vector2(12,12)),StatusColor(state));Text(StatusName(state),p+new Vector2(20,12),13);
        }
    }

    private void DrawGateBoundaries(float tile)
    {
        foreach(var cell in MapConfig.Regions.Where(r=>r.Enterable))foreach(var delta in new[]{new V(1,0),new V(0,1)})
        {
            var next=MapConfig.Regions.FirstOrDefault(r=>r.X==cell.X+delta.X&&r.Y==cell.Y+delta.Y&&r.Enterable);
            if(next==null||MapConfig.Rulers.GetValueOrDefault(cell.Id)==MapConfig.Rulers.GetValueOrDefault(next.Id))continue;
            var p=MapPoint(new V(cell.X,cell.Y));var a=p+(delta.X>0?new Vector2(tile,0):new Vector2(0,tile));var b=a+(delta.X>0?new Vector2(0,tile):new Vector2(tile,0));
            DrawLine(a,b,new Color("050a10"),4);
            if(MapConfig.CanCross(cell.Id,next.Id)){var mid=(a+b)/2;var along=(b-a).Normalized()*9;DrawLine(mid-along,mid+along,new Color("e7c77b"),4);}
        }
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
        if(terrain=="ocean"){DrawArc(p,12*scale,0,Mathf.Pi,12,new Color("72b3d1"),2*scale,true);DrawArc(p+new Vector2(4,-7)*scale,10*scale,0,Mathf.Pi,12,new Color("4e87ad"),2*scale,true);return;}
        if(terrain=="mountain"){DrawColoredPolygon(new[]{p+new Vector2(-15,4)*scale,p+new Vector2(0,-21)*scale,p+new Vector2(15,4)*scale},new Color("8d969b"));DrawLine(p,p+new Vector2(0,-15)*scale,new Color("c4ced2"),2*scale);return;}
        if(terrain=="hell")
        {
            DrawColoredPolygon(new[]{p+new Vector2(-11,0)*scale,p+new Vector2(-3,-19)*scale,p+new Vector2(4,-8)*scale,p+new Vector2(10,0)*scale},new Color("3e3031"));
            DrawLine(p+new Vector2(-8,3)*scale,p+new Vector2(8,-2)*scale,new Color("e79446"),2*scale);return;
        }
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
