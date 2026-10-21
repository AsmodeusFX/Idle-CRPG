using Godot;
using System;
using V=System.Numerics.Vector2;

/// <summary>所有场景绘制置于独立裁切容器，不能覆盖标题、页签和右侧操作区。</summary>
public partial class SceneCanvas : Node2D
{
    public Main OwnerView=null!;
    public bool TownBackdrop;
    public override void _Draw(){if(TownBackdrop)OwnerView.RenderTownBackdrop(this);else OwnerView.RenderScene(this);}
}
public partial class Main
{
    private static readonly Vector2 CreationOffset=new(176,90);
    private Rect2 SceneRect=>session==null?new Rect2(24,92,1392,784):new Rect2(24,92,1040,558);
    private SceneCanvas sceneCanvas=null!;
    private SceneCanvas townBackdropCanvas=null!;
    private IdleCrpg.Core.TownScene? drawnTown;
    private Vector2 drawnTownOrigin;
    private float drawnTownZoom;
    private CanvasItem painter=null!;
    private V cameraCenter;
    private float cameraZoom;
    private string cameraScene="";
    private bool cameraReady;
    private V cameraVelocity;
    private float zoomVelocity;
    private void BuildSceneCanvas()
    {
        var clip=new Control {Position=SceneRect.Position,Size=SceneRect.Size,ClipContents=true,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(clip);
        townBackdropCanvas=new SceneCanvas {OwnerView=this,TownBackdrop=true,Visible=false};clip.AddChild(townBackdropCanvas);
        sceneCanvas=new SceneCanvas {OwnerView=this};clip.AddChild(sceneCanvas);
    }
    /// <summary>城镇静态绘制仅在场景、镜头投影或页面可见性变化时失效；角色移动不重建地面。</summary>
    private void UpdateTownBackdrop()
    {
        bool visible=session?.World?.InTownScene==true&&config.Town!=null&&sceneCanvas.Visible;
        bool redraw=visible&&(!townBackdropCanvas.Visible||!ReferenceEquals(drawnTown,config.Town)||drawnTownOrigin!=origin||drawnTownZoom!=zoom);
        townBackdropCanvas.Visible=visible;
        if(!redraw)return;
        drawnTown=config.Town;drawnTownOrigin=origin;drawnTownZoom=zoom;townBackdropCanvas.QueueRedraw();
    }
    public void RenderTownBackdrop(CanvasItem context)
    {
        if(session?.World?.InTownScene!=true||config.Town==null)return;
        long profileStart=System.Diagnostics.Stopwatch.GetTimestamp();
        painter=context;context.DrawSetTransform(-SceneRect.Position);DrawTownBackdrop();
        if(performanceRun)drawTimes.Add(Milliseconds(profileStart));
    }
    private void UpdateCamera(double dt)
    {
        if(session==null)return;
        var w=session.World!;V target;float targetZoom;string key;float rate;
        if(w.InDungeonScene)
        {
            var room=session.Floor.Rooms[session.CurrentRoom];bool locked=!session.MovingBetweenRooms;
            target=locked?room.Center:VisualPosition(session.Leader);
            targetZoom=Math.Min(config.CameraMaxZoom,Math.Min(930/(room.HalfWidth*2+room.HalfHeight*2),(SceneRect.Size.Y-150)/(room.HalfWidth+room.HalfHeight)));
            key="dungeon:"+w.DungeonVisits+":"+session.FloorIndex;
            rate=locked?config.CameraTransition:config.CameraFollow;
        }
        else if(w.InTownScene&&config.Town is {} town)
        {
            target=new V(town.Width*.5f,town.Height*.5f);targetZoom=Math.Min(930/(town.Width+town.Height),(SceneRect.Size.Y-100)/((town.Width+town.Height)*.5f));
            key="town:"+town.Id;rate=config.CameraTransition;
        }
        else {target=VisualWorldPosition*config.WorldUnits;targetZoom=config.WorldZoom;key="outdoors";rate=config.CameraFollow;}
        if(!cameraReady||key!=cameraScene){cameraReady=true;cameraCenter=target;cameraZoom=targetZoom;cameraScene=key;cameraVelocity=V.Zero;zoomVelocity=0;}
        else
        {
            // 临界阻尼保留进入房间瞬间的运动速度，使跟随→房间锚点的轨迹连续且不回弹。
            float step=(float)Math.Min(dt,.1),omega=rate,decay=MathF.Exp(-omega*step);
            var offset=cameraCenter-target;var impulse=cameraVelocity+omega*offset;
            cameraCenter=target+(offset+impulse*step)*decay;
            cameraVelocity=(cameraVelocity-omega*impulse*step)*decay;
            float z=cameraZoom-targetZoom,j=zoomVelocity+omega*z;
            cameraZoom=targetZoom+(z+j*step)*decay;zoomVelocity=(zoomVelocity-omega*j*step)*decay;
        }
        zoom=cameraZoom;origin=SceneRect.GetCenter()+new Vector2(0,28)-new Vector2((cameraCenter.X-cameraCenter.Y)*zoom,(cameraCenter.X+cameraCenter.Y)*zoom*.5f);
    }
    public void RenderScene(CanvasItem context)
    {
        long profileStart=System.Diagnostics.Stopwatch.GetTimestamp();
        painter=context;context.DrawSetTransform(-SceneRect.Position);
        PaintScene();
        if(performanceRun)drawTimes.Add(Milliseconds(profileStart));
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
