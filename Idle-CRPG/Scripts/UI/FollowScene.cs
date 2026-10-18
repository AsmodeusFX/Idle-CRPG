using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    private readonly Dictionary<string,List<V>> roads=new();
    private void RefreshCastleButton()=>RefreshCastleInteractions();
    private void DrawFighter(Vector2 p,Color color,int role,bool boss,float hp,string name)
    {
        float scale=Math.Clamp(zoom/50,.8f,1.25f)*(boss?1.3f:1);
        Vector2 P(float x,float y)=>p+new Vector2(x,y)*scale;
        DrawCircle(P(0,1),10*scale,new Color(0,0,0,.35f));
        DrawColoredPolygon(new[]{P(-7,-26),P(7,-26),P(11,-3),P(-10,-3)},color.Darkened(.25f));
        DrawLine(P(-4,-8),P(-5,0),new Color("172532"),5*scale);DrawLine(P(4,-8),P(5,0),new Color("172532"),5*scale);
        DrawRect(new Rect2(P(-6,-24),new Vector2(12,15)*scale),color);
        DrawCircle(P(0,-31),5*scale,role>=0?new Color("e7c29a"):color.Lightened(.3f));
        DrawLine(P(-6,-30),P(6,-30),color.Lightened(.2f),3*scale);
        DrawLine(P(7,-20),P(15,-10),color.Lightened(.1f),3*scale);
        DrawLine(P(15,-10),P(role==0?18:17,role==0?-35:-32),role==0?new Color("d9e1dc"):new Color("c2a76c"),3*scale);
        if(role==0)DrawCircle(P(-8,-15),5*scale,color.Darkened(.4f));
        if(role==2)DrawCircle(P(17,-33),3*scale,new Color("9aaeff"));
        DrawRect(new Rect2(P(-17,-46),new Vector2(34,4)*scale),new Color("111820"));
        DrawRect(new Rect2(P(-17,-46),new Vector2(34*Math.Clamp(hp,0,1),4)*scale),color);
        Text(name,P(-18,-53),12);
    }
    private V WorldLocation(int id,float x,float y){var r=config.Regions.Single(r=>r.Id==id);return new V(r.X+x,r.Y+y);}
    private Vector2 OutdoorPoint(V p)=>Project(p*config.WorldUnits);
    private bool OnScreen(Vector2 p,float margin=130)=>p.X>SceneRect.Position.X-margin&&p.X<SceneRect.End.X+margin&&p.Y>SceneRect.Position.Y-margin&&p.Y<SceneRect.End.Y+margin;
    private List<V> Road(string id,V from,V to)
    {
        if(!roads.TryGetValue(id,out var path)){path=new[]{from}.Concat(WorldNavigation.FindPath(config,from,to)).ToList();roads[id]=path;}return path;
    }
    private void DrawVillageOrTravel()
    {
        if(session!.World!.InTownScene&&config.Town!=null){DrawTownScene();return;}
        var w=session!.World!;V center=cameraCenter/config.WorldUnits;
        int cells=WorldNavigation.CellsPerRegion;
        int x0=Math.Max(0,(int)((center.X-1.6f)*cells)),x1=Math.Min(config.WorldWidth*cells-1,(int)((center.X+1.6f)*cells));
        int y0=Math.Max(0,(int)((center.Y-1.6f)*cells)),y1=Math.Min(config.WorldHeight*cells-1,(int)((center.Y+1.6f)*cells));
        var decorations=new List<(V Position,string Terrain,int Seed)>();
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
        {
            V a=new(x/(float)cells,y/(float)cells),b=a+new V(1f/cells);var p=OutdoorPoint((a+b)*.5f);if(!OnScreen(p,110))continue;
            var r=config.Regions.Single(r=>r.X==x/cells&&r.Y==y/cells);uint noise=unchecked((uint)(x*73856093^y*19349663));
            var color=TerrainColor(r.Terrain).Lightened((noise%13)/400f);
            var corners=new[]{OutdoorPoint(a),OutdoorPoint(new V(b.X,a.Y)),OutdoorPoint(b),OutdoorPoint(new V(a.X,b.Y))};DrawColoredPolygon(corners,color);
            if(!r.Enterable){Decoration(p,r.Terrain,.45f,(int)noise);continue;}
            for(int k=0;k<3;k++){var q=p+new Vector2((int)(noise>>(k*3))%25-12,(int)(noise>>(k*4))%13-6);DrawLine(q,q+new Vector2(3,-1),color.Darkened(.15f));}
            float fx=(x+.5f)/cells,fy=(y+.5f)/cells;
            bool nearRoad=Math.Abs(fx-MathF.Floor(fx)-.5f)<.09f||Math.Abs(fy-MathF.Floor(fy)-.5f)<.09f;
            if(!nearRoad&&noise%5<=1&&WorldNavigation.Walkable(config,x,y))
                for(int i=0;i<4;i++)decorations.Add(((a+b)*.5f+new V((i%2-.5f)*.035f,(i/2-.5f)*.035f),r.Terrain,(int)noise+i));
        }
        var nearby=config.Regions.Where(r=>r.Enterable&&Math.Abs(r.X+.5f-center.X)<1.7f&&Math.Abs(r.Y+.5f-center.Y)<1.7f).ToArray();
        foreach(var r in nearby)
        {
            V c=new(r.X+.5f,r.Y+.5f);
            var east=config.Regions.FirstOrDefault(n=>n.X==r.X+1&&n.Y==r.Y);var south=config.Regions.FirstOrDefault(n=>n.X==r.X&&n.Y==r.Y+1);
            if(east!=null&&config.CanCross(r.Id,east.Id))DrawLine(OutdoorPoint(c),OutdoorPoint(c+new V(1,0)),new Color("847a66"),18,true);
            if(south!=null&&config.CanCross(r.Id,south.Id))DrawLine(OutdoorPoint(c),OutdoorPoint(c+new V(0,1)),new Color("847a66"),18,true);
            var village=config.Villages.FirstOrDefault(v=>v.RegionId==r.Id);if(village==null)continue;V v=WorldLocation(r.Id,village.X,village.Y);
            DrawPolyline(Road(village.Id,v,c).Select(OutdoorPoint).ToArray(),new Color("928773"),15,true);
            foreach(var d in config.Dungeons.Where(d=>d.RegionId==r.Id))DrawPolyline(Road(d.Id,v,WorldLocation(r.Id,d.X,d.Y)).Select(OutdoorPoint).ToArray(),new Color("928773"),11,true);
        }
        foreach(var o in config.Obstacles)
        {
            var p=OutdoorPoint(new V(o.X+o.Width*.5f,o.Y+o.Height*.5f));if(!OnScreen(p,140))continue;
            var pts=new[]{OutdoorPoint(new V(o.X,o.Y)),OutdoorPoint(new V(o.X+o.Width,o.Y)),OutdoorPoint(new V(o.X+o.Width,o.Y+o.Height)),OutdoorPoint(new V(o.X,o.Y+o.Height))};
            DrawColoredPolygon(pts,new Color(o.Kind=="water"?"4d859a":"64717b"));DrawPolyline(pts.Append(pts[0]).ToArray(),new Color(o.Kind=="water"?"9ac7c4":"9aa5a7"),3,true);
            if(o.Kind=="water")for(int i=0;i<4;i++)DrawLine(p+new Vector2(-22+i*7,i*4),p+new Vector2(-5+i*7,i*4),new Color("a5d5da"),2);
            else DrawColoredPolygon(new[]{p+new Vector2(-22,6),p+new Vector2(-5,-30),p+new Vector2(23,7)},new Color("8d9ba2"));
        }
        foreach(var item in decorations.OrderBy(d=>d.Position.X+d.Position.Y))Decoration(OutdoorPoint(item.Position),item.Terrain,.9f,item.Seed);
        foreach(var r in nearby)
        {
            var village=config.Villages.FirstOrDefault(v=>v.RegionId==r.Id);if(village==null)continue;V anchor=WorldLocation(r.Id,village.X,village.Y);var p=OutdoorPoint(anchor);
            House(p-new Vector2(10,35),r.Terrain);Text(village.Name,p+new Vector2(-35,-88),14,"f4e1b9");
            var npc=config.Npcs.Single(n=>n.Id==village.NpcId);var n=OutdoorPoint(anchor+npc.Position/config.WorldUnits);
            if(config.Town==null)DrawFighter(n,new Color("b89761"),-1,false,1,npc.Name);
        }
        V worldLeader=VisualWorldPosition;
        // 地牢造型独立于村庄存在；第一章只有营地格有村庄，不能把其余入口放在村庄循环里。
        foreach(var dungeon in config.Dungeons.Where(d=>nearby.Any(r=>r.Id==d.RegionId)))
        {
            var point=OutdoorPoint(WorldLocation(dungeon.RegionId,dungeon.X,dungeon.Y));
            DrawLandmark(point,dungeon.Castle,1);Text(dungeon.Name,point+new Vector2(-36,-64),13,"e7d2a4");
        }
        foreach(var waypoint in config.Waypoints.Where(p=>nearby.Any(r=>r.Id==p.Region)))
        {
            bool unlocked=w.Progress.WaypointUnlocked(config,waypoint);
            var point=OutdoorPoint(config.WaypointPoint(waypoint));DrawPortalRing(point,unlocked?1:.4f,unlocked?"68cce8":"6b817d");Text(unlocked?"传送点":"传送点 · 未解锁",point+new Vector2(-20,19),10,"a8c7cd");
        }
        foreach(int i in new[]{2,1,0})
        {
            V position=worldLeader+new V(-i*.04f,i*.04f);
            if(w.Stage==CampaignStage.Village)position=WorldLocation(w.Village.RegionId,w.Village.X,w.Village.Y)+VisualPosition(session.Heroes[i])/config.WorldUnits+new V(-i*.025f,i*.025f);
            DrawFighter(OutdoorPoint(position),heroColors[i],i,false,session.Heroes[i].Hp/session.Heroes[i].MaxHp,session.Heroes[i].Name);
        }
        Text(w.Stage==CampaignStage.Village?w.Village.Name:"行旅 · "+config.Regions.Single(r=>r.Id==w.CurrentRegion).Name,new Vector2(48,165),20,"f4e1b9");
        Text(w.Status,new Vector2(48,190),14,"e0e8e7");
        if(w.Stage==CampaignStage.Village){DrawRect(new Rect2(360,620,370,7),new Color("23323e"));DrawRect(new Rect2(360,620,370*w.TradeElapsed/config.TradeSeconds,7),new Color("65d9cb"));}
    }
}
