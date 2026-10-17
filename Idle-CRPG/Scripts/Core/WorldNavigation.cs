using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public record WorldObstacle(string Id,string Kind,float X,float Y,float Width,float Height);
/// <summary>统一世界坐标的细网格 A*；障碍按整格相交阻挡，路径线段不会穿过水面或岩石。</summary>
public static class WorldNavigation
{
    public const int CellsPerRegion=8;
    public static bool Walkable(Config c,int x,int y)
    {
        if(x<0||y<0||x>=c.WorldWidth*CellsPerRegion||y>=c.WorldHeight*CellsPerRegion)return false;
        float left=x/(float)CellsPerRegion,top=y/(float)CellsPerRegion,size=1f/CellsPerRegion;
        return !c.Obstacles.Any(o=>left<o.X+o.Width-.00001f&&left+size>o.X+.00001f&&top<o.Y+o.Height-.00001f&&top+size>o.Y+.00001f);
    }
    public static bool Walkable(Config c,Vector2 p)=>Walkable(c,(int)(p.X*CellsPerRegion),(int)(p.Y*CellsPerRegion));
    public static List<Vector2> FindPath(Config c,Vector2 from,Vector2 to,ISet<int>? explored=null,ISet<int>? allowedRegions=null)
    {
        int width=c.WorldWidth*CellsPerRegion;
        int Id(Vector2 p)=>(int)(p.Y*CellsPerRegion)*width+(int)(p.X*CellsPerRegion);
        int start=Id(from),goal=Id(to);
        var regions=c.Regions.ToDictionary(r=>(r.X,r.Y),r=>r.Id);
        if(allowedRegions!=null&&(!allowedRegions.Contains(regions[((int)from.X,(int)from.Y)])||!allowedRegions.Contains(regions[((int)to.X,(int)to.Y)])))throw new InvalidOperationException("路径端点属于未解锁统治区");
        if(!Walkable(c,from)||!Walkable(c,to))throw new InvalidDataException("世界路径端点被障碍阻挡");
        var open=new PriorityQueue<int,int>();var cost=new Dictionary<int,int>{{start,0}};var parent=new Dictionary<int,int>();open.Enqueue(start,0);
        bool Allowed(int x,int y)
        {
            if(!Walkable(c,x,y))return false;
            if(allowedRegions!=null&&!allowedRegions.Contains(regions[(x/CellsPerRegion,y/CellsPerRegion)]))return false;
            int gx=(int)to.X,gy=(int)to.Y;
            return explored==null||explored.Contains(regions[(x/CellsPerRegion,y/CellsPerRegion)])||(x/CellsPerRegion==gx&&y/CellsPerRegion==gy);
        }
        while(open.Count>0)
        {
            int id=open.Dequeue();if(id==goal)break;int x=id%width,y=id/width;
            foreach(var (dx,dy) in new[]{(1,0),(0,1),(-1,0),(0,-1)})
            {
                int nx=x+dx,ny=y+dy;if(!Allowed(nx,ny))continue;int next=ny*width+nx,newCost=cost[id]+1;
                if(cost.TryGetValue(next,out int old)&&old<=newCost)continue;
                cost[next]=newCost;parent[next]=id;open.Enqueue(next,newCost+Math.Abs(nx-goal%width)+Math.Abs(ny-goal/width));
            }
        }
        if(start!=goal&&!parent.ContainsKey(goal))throw new InvalidOperationException("世界目标无可达路径");
        var path=new List<Vector2>();for(int id=goal;id!=start;id=parent[id])path.Add(new Vector2((id%width+.5f)/CellsPerRegion,(id/width+.5f)/CellsPerRegion));
        path.Reverse();
        // 先到所在格中心再走格边，禁止起点直接斜穿相邻阻挡格。
        if(start!=goal)path.Insert(0,new Vector2((start%width+.5f)/CellsPerRegion,(start/width+.5f)/CellsPerRegion));
        path.Add(to);return path;
    }
}
public sealed partial class Config
{
    public List<WorldObstacle> Obstacles=new();
    public float CameraFollow,CameraTransition,CameraMaxZoom,WorldZoom,WorldUnits;
    private void LoadSceneConfig(string directory)
    {
        Obstacles=Read(Path.Combine(directory,"world/obstacle.csv")).Select(r=>new WorldObstacle(r["id"],r["kind"],Number(r,"x"),Number(r,"y"),Number(r,"width"),Number(r,"height"))).ToList();
        var rows=Read(Path.Combine(directory,"system/camera.csv"));if(rows.Count!=1)throw new InvalidDataException("camera.csv 必须一行");var s=rows[0];
        CameraFollow=Number(s,"follow_rate");CameraTransition=Number(s,"transition_rate");CameraMaxZoom=Number(s,"dungeon_max_zoom");WorldZoom=Number(s,"world_zoom");WorldUnits=Number(s,"world_units");
        if(CameraFollow<=0||CameraTransition<=0||CameraMaxZoom<=0||WorldZoom<=0||WorldUnits<12||Obstacles.Select(o=>o.Id).Distinct().Count()!=Obstacles.Count||Obstacles.Any(o=>o.Kind is not ("water" or "rock")||o.X<0||o.Y<0||o.Width<=0||o.Height<=0||o.X+o.Width>WorldWidth||o.Y+o.Height>WorldHeight))throw new InvalidDataException("场景/障碍配置非法");
        foreach(var r in Regions)
        {
            foreach(var p in Dungeons.Where(d=>d.RegionId==r.Id).Select(d=>new Vector2(r.X+d.X,r.Y+d.Y)).Concat(Villages.Where(v=>v.RegionId==r.Id).Select(v=>new Vector2(r.X+v.X,r.Y+v.Y))))
                if(!WorldNavigation.Walkable(this,p))throw new InvalidDataException("地牢或村庄入口被障碍阻挡");
            var village=Villages.Single(v=>v.RegionId==r.Id);var npc=Npcs.Single(n=>n.Id==village.NpcId);
            var npcPosition=new Vector2(r.X+village.X,r.Y+village.Y)+npc.Position/WorldUnits;
            if((int)npcPosition.X!=r.X||(int)npcPosition.Y!=r.Y||!WorldNavigation.Walkable(this,npcPosition))throw new InvalidDataException("NPC 必须位于村庄所属区域可行走位置");
        }
    }
}
