using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    private TownScene? townRoadScene;
    private V[][] townRoads=Array.Empty<V[]>();
    /// <summary>章节城镇的地板、栈桥、阶梯和建筑均读取同一配置几何；水面与深渊不生成可走地板。</summary>
    private void DrawChapterTown(TownScene town)
    {
        Vector2 P(float x,float y)=>Project(new V(x,y));
        Vector2[] Q(float x,float y,float w,float h)=>new[]{P(x,y),P(x+w,y),P(x+w,y+h),P(x,y+h)};
        bool sand=town.Theme=="sand",docks=town.Theme=="docks",holy=town.Theme=="sanctum";
        string backdrop=sand?"645438":docks?"183c3d":holy?"161724":"344653";
        DrawRect(SceneRect,new Color(backdrop));
        var bounds=TownBackdropBounds();
        for(int y=bounds.Top;y<bounds.Bottom;y++)for(int x=bounds.Left;x<bounds.Right;x++)
        {
            uint noise=unchecked((uint)(x*73856093^y*19349663));var p=P(x+.3f,y+.5f);
            if(!SceneRect.Grow(45).HasPoint(p))continue;
            if(docks&&noise%4==0)DrawLine(p,p+new Vector2(12,0),new Color("356769"),1);
            if(sand&&noise%5==0)DrawLine(p,p+new Vector2(8,1),new Color("806c46"),1);
            if(holy&&noise%9==0)DrawCircle(p,1,new Color("5b5467"));
            if(!sand&&!docks&&!holy&&noise%5==0)DrawLine(p,p+new Vector2(4,1),new Color("9cbbc4"),1);
            if((docks||!sand&&!holy)&&noise%19==0&&!town.Zones.Any(z=>z.Contains(new V(x,y))))
            {
                if(docks)
                {
                    DrawLine(p,p-new Vector2(0,22),new Color("75684c"),3);
                    for(int i=0;i<5;i++){float angle=i*MathF.Tau/5;DrawLine(p-new Vector2(0,22),p-new Vector2(0,22)+new Vector2(MathF.Cos(angle)*15,MathF.Sin(angle)*6),new Color("447b58"),4);}
                }
                else Decoration(p,"snow",.8f,(int)noise);
            }
        }
        foreach(var zone in town.Zones)
        {
            string floor=zone.Kind switch {"wood"=>"665e43","bridge"=>"8c7952","stairs"=>"a09b86","snow"=>"8d9fa7","sand"=>"a78c58",_=>holy?"686779":"9b8d6a"};
            DrawColoredPolygon(Q(zone.X,zone.Y,zone.Width,zone.Height),new Color(floor));
            for(float y=zone.Y;y<zone.Y+zone.Height;y+=1)
                DrawLine(P(zone.X,y),P(zone.X+zone.Width,y),new Color(floor).Darkened(.12f),1);
            if(zone.Kind is not ("bridge" or "wood" or "stairs"))
                for(float x=zone.X;x<zone.X+zone.Width;x+=2)DrawLine(P(x,zone.Y),P(x,zone.Y+zone.Height),new Color(floor).Darkened(.08f),1);
            // 仅为平台外缘绘制栏杆/城墙，矩形重叠处保留真实的连通缺口。
            void Edge(V a,V b,V outside)
            {
                int count=(int)Math.Ceiling(V.Distance(a,b));
                for(int i=0;i<count;i++)
                {
                    var v=V.Lerp(a,b,(i+.5f)/count);
                    if(town.Zones.Any(z=>z.Contains(v+outside*.25f)))continue;
                    if(V.Distance(v,town.Exit)<1.8f)continue;
                    var left=Project(V.Lerp(a,b,i/(float)count));var right=Project(V.Lerp(a,b,(i+1f)/count));
                    float height=docks?7:holy?10:18;
                    DrawColoredPolygon(new[]{left,right,right-new Vector2(0,height),left-new Vector2(0,height)},new Color(sand?"b49b6b":docks?"473e2c":holy?"8c8495":"5f717c"));
                    DrawLine(left-new Vector2(0,height),right-new Vector2(0,height),new Color(sand?"e2c98f":holy?"b5acbf":"c0cbd0"),2);
                }
            }
            Edge(new(zone.X,zone.Y),new(zone.X+zone.Width,zone.Y),new(0,-1));
            Edge(new(zone.X,zone.Y+zone.Height),new(zone.X+zone.Width,zone.Y+zone.Height),new(0,1));
            Edge(new(zone.X,zone.Y),new(zone.X,zone.Y+zone.Height),new(-1,0));
            Edge(new(zone.X+zone.Width,zone.Y),new(zone.X+zone.Width,zone.Y+zone.Height),new(1,0));
        }
        if(!docks)
        {
            if(!ReferenceEquals(townRoadScene,town))
            {
                townRoadScene=town;townRoads=town.Npcs.Select(n=>town.FindPath(config.TownPortal,n.Position).ToArray()).ToArray();
            }
            foreach(var road in townRoads)
            {
                var path=road.Select(Project).ToArray();
                DrawPolyline(path,new Color(sand?"bba16e":holy?"8b8295":"b0bfc2"),sand?10:7,true);
            }
        }
        foreach(var prop in town.Props.OrderBy(p=>p.X+p.Y))DrawChapterTownProp(prop,town.Theme);
        var exit=Project(town.Exit);
        DrawArc(exit,14,MathF.PI,MathF.Tau,16,new Color("e5c181"),3,true);
        if(town.Theme=="snow")
        {
            DrawLine(exit+new Vector2(-21,6),exit+new Vector2(-21,-30),new Color("a8b9c0"),8);
            DrawLine(exit+new Vector2(21,6),exit+new Vector2(21,-30),new Color("a8b9c0"),8);
        }
    }

    private void DrawChapterTownProp(TownProp prop,string theme)
    {
        Vector2 P(float x,float y)=>Project(new V(x,y));
        var floor=new[]{P(prop.X,prop.Y),P(prop.X+prop.Width,prop.Y),P(prop.X+prop.Width,prop.Y+prop.Height),P(prop.X,prop.Y+prop.Height)};
        var center=P(prop.X+prop.Width/2,prop.Y+prop.Height/2);
        if(prop.Kind=="water")
        {
            DrawColoredPolygon(floor,new Color("24566b"));
            for(float y=prop.Y+.5f;y<prop.Y+prop.Height;y++)DrawLine(P(prop.X,y),P(prop.X+prop.Width,y),new Color("467c88"),1);
            return;
        }
        if(prop.Kind=="palm")
        {
            DrawLine(center,center-new Vector2(0,37),new Color("81704e"),5);
            for(int i=0;i<7;i++){float angle=i*MathF.Tau/7;DrawLine(center-new Vector2(0,37),center-new Vector2(0,37)+new Vector2(MathF.Cos(angle)*22,MathF.Sin(angle)*9),new Color("4b845d"),5);}
            return;
        }
        if(prop.Kind is "fire" or "holy_fire")
        {
            DrawColoredPolygon(floor,new Color("5b5355"));DrawCircle(center,8,new Color("8e7b64"));
            DrawColoredPolygon(new[]{center+new Vector2(-7,0),center+new Vector2(0,-25),center+new Vector2(8,0)},new Color(prop.Kind=="holy_fire"?"94d3df":"e3a35b"));
            DrawLine(center,center-new Vector2(0,15),new Color("fff0bf"),3);return;
        }
        if(prop.Kind is "sewer" or "sealed_gate")
        {
            DrawColoredPolygon(floor,new Color("222a2a"));
            for(int i=-2;i<=2;i++)DrawLine(center+new Vector2(i*4,-7),center+new Vector2(i*4,7),new Color("8c8c7f"),2);
            Text(prop.Kind=="sewer"?"下水道·地标":"封闭门廊",center+new Vector2(-25,17),10,"c5b99b");return;
        }
        if(prop.Kind=="ship")
        {
            DrawColoredPolygon(floor,new Color("503c2a"));DrawPolyline(new[]{floor[0],floor[1],floor[2],floor[3],floor[0]},new Color("af9562"),3,true);
            DrawLine(center,center-new Vector2(0,55),new Color("ad9465"),3);
            DrawColoredPolygon(new[]{center-new Vector2(0,50),center+new Vector2(23,-22),center-new Vector2(0,17)},new Color("d4c6a4"));return;
        }
        if(prop.Kind is "column" or "angel")
        {
            DrawColoredPolygon(floor,new Color("90869b"));DrawLine(center,center-new Vector2(0,48),new Color("b5aec3"),prop.Kind=="angel"?10:7);
            if(prop.Kind=="angel")
            {
                for(int sign=-1;sign<=1;sign+=2)DrawPolyline(new[]{center+new Vector2(0,-38),center+new Vector2(sign*25,-56),center+new Vector2(sign*30,-25)},new Color("c6dbeb"),3,true);
                Text("圣殿",center+new Vector2(-10,-62),11,"d8d8e3");
            }
            return;
        }
        float height=prop.Kind switch {"palace"=>42,"keep"=>38,"tower"=>43,"ruin"=>35,"forge"=>13,"stash"=>10,"pier"=>2,_=>25};
        string wall=theme=="sand"?"ad8b55":theme=="docks"?"625c43":theme=="sanctum"?"777085":"657580";
        var top=floor.Select(p=>p-new Vector2(0,height)).ToArray();
        DrawColoredPolygon(new[]{floor[0],floor[1],top[1],top[0]},new Color(wall).Darkened(.18f));
        DrawColoredPolygon(new[]{floor[1],floor[2],top[2],top[1]},new Color(wall));
        DrawColoredPolygon(top,new Color(theme=="snow"?"ccd6d8":theme=="sand"?"d4b97f":"8b8060"));
        DrawLine(top[0],top[2],new Color(theme=="snow"?"e9eef0":"dec99b"),2);
        if(prop.Kind=="palace")
        {
            var dome=center-new Vector2(0,height+8);DrawCircle(dome,25,new Color("cfb77c"));DrawArc(dome,25,MathF.PI,MathF.Tau,24,new Color("f0d9a0"),2,true);
            DrawLine(dome-new Vector2(0,25),dome-new Vector2(0,36),new Color("e8ca7c"),2);Text("皇宫",dome+new Vector2(-14,-43),12,"f4dda6");
        }
        if(prop.Kind=="forge"){DrawCircle(center-new Vector2(0,12),7,new Color("e9a450"));DrawLine(center+new Vector2(7,-12),center+new Vector2(13,-35),new Color("373c3e"),8);}
        if(prop.Kind=="market")DrawColoredPolygon(top.Select((p,i)=>p+new Vector2(0,i%2==0?-6:0)).ToArray(),new Color("a9644a"));
        if(prop.Kind=="ruin"){DrawLine(top[0],top[1]+new Vector2(0,9),new Color("4f6357"),4);}
        string label=prop.Kind switch {"stash"=>"储物箱·待开放","keep"=>"城寨","tower"=>"法师塔","shrine"=>"灵修台",_=>""};
        if(label!="")Text(label,center+new Vector2(-20,-height-6),10,"ddd5bb");
    }
}
