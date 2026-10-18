using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    private void DrawLandmark(Vector2 p,bool fortress,float scale)
    {
        Vector2 P(float x,float y)=>p+new Vector2(x,y)*scale;
        if(!fortress)
        {
            DrawColoredPolygon(new[]{P(-24,1),P(-19,-23),P(-6,-39),P(9,-35),P(25,-10),P(23,3)},new Color("81867a"));
            DrawColoredPolygon(new[]{P(-13,1),P(-12,-16),P(-5,-25),P(7,-24),P(13,-12),P(13,1)},new Color("152126"));
            DrawLine(P(-19,-23),P(-6,-39),new Color("bdbaa1"),4*scale);DrawLine(P(9,-35),P(25,-10),new Color("555e57"),4*scale);
            for(int i=0;i<4;i++)DrawLine(P(-14-i*2,2+i*4),P(14+i*2,2+i*4),new Color(i%2==0?"a59a7b":"706c59"),4*scale);
        }
        else
        {
            DrawColoredPolygon(new[]{P(-23,1),P(-23,-27),P(20,-27),P(25,2)},new Color("5c6e74"));
            foreach(int x in new[]{-24,20})
            {
                DrawRect(new Rect2(P(x-7,-42),new Vector2(14,47)*scale),new Color(x<0?"93a0a1":"718286"));
                DrawColoredPolygon(new[]{P(x-10,-42),P(x,-55),P(x+10,-42)},new Color("a85c48"));
                for(int j=0;j<5;j++)DrawLine(P(x-7,-35+j*8),P(x+7,-35+j*8),new Color("4c6067"),1*scale);
            }
            DrawRect(new Rect2(P(-7,-18),new Vector2(14,23)*scale),new Color("18272c"));
            DrawLine(P(1,-28),P(1,-66),new Color("c8b789"),2*scale);DrawColoredPolygon(new[]{P(2,-65),P(21,-59),P(2,-53)},new Color("d09b56"));
        }
    }
    private void DrawPortalRing(Vector2 p,float amount,string color="68cce8")
    {
        float width=15*Math.Max(.1f,amount),height=26*Math.Max(.1f,amount);var c=new Color(color);
        var points=Enumerable.Range(0,33).Select(i=>p+new Vector2(MathF.Cos(i*MathF.Tau/32)*width,MathF.Sin(i*MathF.Tau/32)*height-25)).ToArray();
        DrawColoredPolygon(points,new Color(c.R*.2f,c.G*.2f,c.B*.3f,.8f));DrawPolyline(points,c,3,true);
        DrawArc(p-new Vector2(0,25),7*Math.Max(.1f,amount),0,MathF.Tau,24,c.Lightened(.2f),2,true);
    }
    /// <summary>表演严格读取逻辑传送阶段：打开、进入收束、目标地点展开；动画不触发传送或奖励。</summary>
    private void DrawPortalPerformance()
    {
        if(session?.World is not {} world||world.PortalPhase==PortalPhase.None)return;
        Vector2 p;
        if(world.InTownScene)p=Project(world.PortalReturning?config.TownPortal:config.TownWaypoint);
        else if(world.InDungeonScene)p=Project(world.PortalOrigin);
        else p=OutdoorPoint(VisualWorldPosition);
        float t=world.PortalElapsed/world.PortalDuration;
        DrawPortalRing(p,world.PortalPhase==PortalPhase.Using?1-t:Math.Max(.1f,t));
        for(int i=0;i<8;i++)
        {
            float angle=(i/8f+t)*MathF.Tau;DrawCircle(p+new Vector2(MathF.Cos(angle)*30,MathF.Sin(angle)*18-25),2,new Color("b6f0f4"));
        }
        if(world.PortalPhase==PortalPhase.Using)DrawRect(SceneRect,new Color(.16f,.46f,.57f,t*.35f));
    }
}
