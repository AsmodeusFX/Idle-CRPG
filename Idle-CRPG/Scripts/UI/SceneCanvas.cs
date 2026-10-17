using Godot;
using System;
using V=System.Numerics.Vector2;

/// <summary>所有场景绘制置于独立裁切容器，不能覆盖标题、页签和右侧操作区。</summary>
public partial class SceneCanvas : Node2D
{
    public Main OwnerView=null!;
    public override void _Draw()=>OwnerView.RenderScene(this);
}
public partial class Main
{
    private static readonly Rect2 SceneRect=new(24,148,1040,672);
    private SceneCanvas sceneCanvas=null!;
    private CanvasItem painter=null!;
    private V cameraCenter;
    private float cameraZoom;
    private string cameraScene="";
    private bool cameraReady;
    private void BuildSceneCanvas()
    {
        var clip=new Control {Position=SceneRect.Position,Size=SceneRect.Size,ClipContents=true,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(clip);
        sceneCanvas=new SceneCanvas {OwnerView=this};clip.AddChild(sceneCanvas);
    }
    private void UpdateCamera(double dt)
    {
        if(session==null)return;
        var w=session.World!;V target;float targetZoom;string key;float rate;
        if(w.Stage==IdleCrpg.Core.CampaignStage.Dungeon)
        {
            var room=session.Floor.Rooms[session.CurrentRoom];bool locked=!session.MovingBetweenRooms;
            target=locked?room.Center:session.Leader.Position;
            targetZoom=Math.Min(config.CameraMaxZoom,Math.Min(930/(room.HalfWidth*2+room.HalfHeight*2),530/(room.HalfWidth+room.HalfHeight)));
            key="dungeon:"+w.DungeonVisits+":"+session.FloorIndex;
            rate=locked?config.CameraTransition:config.CameraFollow;
        }
        else {target=w.Position*config.WorldUnits;targetZoom=config.WorldZoom;key="outdoors";rate=config.CameraFollow;}
        if(!cameraReady||key!=cameraScene){cameraReady=true;cameraCenter=target;cameraZoom=targetZoom;cameraScene=key;}
        else
        {
            float blend=1-MathF.Exp(-rate*(float)Math.Min(dt,.1));cameraCenter=V.Lerp(cameraCenter,target,blend);cameraZoom=Mathf.Lerp(cameraZoom,targetZoom,blend);
        }
        zoom=cameraZoom;origin=new Vector2(544,495)-new Vector2((cameraCenter.X-cameraCenter.Y)*zoom,(cameraCenter.X+cameraCenter.Y)*zoom*.5f);
    }
    public void RenderScene(CanvasItem context)
    {
        painter=context;context.DrawSetTransform(-SceneRect.Position);
        PaintScene();
    }
    // 绘制转发保持所有几何/文字共用同一裁切坐标系。
    private new void DrawRect(Rect2 r,Color c,bool filled=true,float width=-1,bool antialias=false)=>painter.DrawRect(r,c,filled,width,antialias);
    private new void DrawLine(Vector2 a,Vector2 b,Color c,float width=-1,bool antialias=false)=>painter.DrawLine(a,b,c,width,antialias);
    private new void DrawCircle(Vector2 p,float radius,Color c)=>painter.DrawCircle(p,radius,c);
    private new void DrawArc(Vector2 p,float radius,float start,float end,int points,Color c,float width=-1,bool antialias=false)=>painter.DrawArc(p,radius,start,end,points,c,width,antialias);
    private void DrawColoredPolygon(Vector2[] points,Color c)=>painter.DrawColoredPolygon(points,c);
    private new void DrawPolyline(Vector2[] points,Color c,float width=-1,bool antialias=false)=>painter.DrawPolyline(points,c,width,antialias);
    private void DrawString(Font f,Vector2 p,string text,HorizontalAlignment align=HorizontalAlignment.Left,float width=-1,int size=16,Color? color=null)=>painter.DrawString(f,p,text,align,width,size,color);
}
