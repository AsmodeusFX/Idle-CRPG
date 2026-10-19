using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    /// <summary>固定营地场景：建筑底面来自寻路障碍表；围栏唯一缺口和桥接道路共用出口坐标。</summary>
    private void DrawTownScene()
    {
        var town=config.Town!;var world=session!.World!;
        Vector2 P(float x,float y)=>Project(new V(x,y));
        Vector2[] Quad(float x,float y,float w,float h)=>new[]{P(x,y),P(x+w,y),P(x+w,y+h),P(x,y+h)};
        if(town.Theme!="camp")DrawChapterTown(town);
        else
        {
        for(int y=-8;y<(int)town.Height+8;y++)for(int x=-8;x<(int)town.Width+8;x++)
        {
            uint noise=unchecked((uint)(x*73856093^y*19349663));bool inside=x>=2&&x<town.Width-6&&y>=2&&y<town.Height-4;
            var color=new Color(inside?"354c36":"243d32").Lightened(noise%11*.003f);
            DrawColoredPolygon(Quad(x,y,1,1),color);
            var p=P(x+.3f,y+.6f);DrawLine(p,p+new Vector2(3,-2),color.Lightened(.12f),1);
            if(!inside&&x<town.Width-3&&noise%9==0)
            {
                Decoration(P(x+.5f,y+.5f),"grass",.7f,(int)noise);
            }
        }
        // 出口外溪流、桥及道路只作为环境；可走范围仍由TownScene判定。
        DrawColoredPolygon(Quad(town.Width-4,-8,3,town.Height+16),new Color("263e46"));
        for(int i=-6;i<town.Height+6;i+=2)DrawLine(P(town.Width-3.8f,i),P(town.Width-1.2f,i+.6f),new Color("47616a"),2);
        DrawLine(P(town.Width-6,town.Exit.Y),P(town.Width+5,town.Exit.Y),new Color("5d5341"),26,true);
        for(float x=town.Width-4.5f;x<town.Width+.3f;x+=.35f)DrawLine(P(x,town.Exit.Y-1),P(x,town.Exit.Y+1),new Color("a08a60"),5,true);
        // 主路与六个服务点的支路，由固定可行走路径铺设。
        foreach(var npc in town.Npcs)
        {
            var points=town.FindPath(town.Entry,npc.Position).Select(Project).ToArray();
            DrawPolyline(points,new Color("665e47"),14,true);
            for(int i=0;i<points.Length;i+=3)DrawCircle(points[i],2,new Color("81765b"));
        }
        float right=town.Width-6,bottom=town.Height-4;
        void Fence(V from,V to)
        {
            int count=(int)Math.Ceiling(V.Distance(from,to)*2);
            for(int i=0;i<=count;i++)
            {
                var p=Project(V.Lerp(from,to,i/(float)Math.Max(1,count)));DrawLine(p,p-new Vector2(0,20),new Color("847e61"),3);
            }
            var a=Project(from);var b=Project(to);DrawLine(a-new Vector2(0,7),b-new Vector2(0,7),new Color("655e48"),3);DrawLine(a-new Vector2(0,15),b-new Vector2(0,15),new Color("a09a76"),2);
        }
        Fence(new(2,2),new(right,2));Fence(new(2,2),new(2,bottom));Fence(new(2,bottom),new(right,bottom));
        Fence(new(right,2),new(right,town.Exit.Y-1));Fence(new(right,town.Exit.Y+1),new(right,bottom));
        foreach(var prop in town.Props.OrderBy(p=>p.X+p.Y))
        {
            var floor=Quad(prop.X,prop.Y,prop.Width,prop.Height);var center=P(prop.X+prop.Width/2,prop.Y+prop.Height/2);
            DrawColoredPolygon(floor,new Color("302d25"));
            if(prop.Kind=="tent")
            {
                var roof=floor.Select(p=>p-new Vector2(0,27)).ToArray();
                DrawColoredPolygon(new[]{floor[0],floor[1],roof[1],roof[0]},new Color("655f4c"));
                DrawColoredPolygon(new[]{floor[1],floor[2],roof[2],roof[1]},new Color("777361"));
                DrawColoredPolygon(new[]{roof[0],roof[1],roof[2],roof[3]},new Color("99927a"));
                DrawLine(roof[0],roof[2],new Color("c6bca0"),2);DrawLine(floor[1],roof[1],new Color("b6aa88"),3);
                DrawLine(center-new Vector2(0,8),center-new Vector2(0,25),new Color("242b23"),8);
            }
            else if(prop.Kind=="fire")
            {
                for(int i=0;i<8;i++){float angle=i*MathF.Tau/8;DrawCircle(center+new Vector2(MathF.Cos(angle)*12,MathF.Sin(angle)*6),4,new Color("6e7468"));}
                DrawColoredPolygon(new[]{center+new Vector2(-7,0),center+new Vector2(1,-23),center+new Vector2(8,0)},new Color("cf8a41"));DrawCircle(center-new Vector2(0,5),4,new Color("f0d18a"));
            }
            else
            {
                var top=floor.Select(p=>p-new Vector2(0,12)).ToArray();DrawColoredPolygon(top,new Color(prop.Kind=="forge"?"70756e":"8b7653"));
                DrawLine(floor[1],top[1],new Color("453d31"),7);DrawLine(floor[2],top[2],new Color("453d31"),7);
                if(prop.Kind=="forge"){DrawCircle(center-new Vector2(15,18),7,new Color("d18e49"));Text("锻炉",center-new Vector2(12,27),10,"e8c18a");}
                if(prop.Kind=="wagon"){DrawCircle(floor[1],6,new Color("272b23"));DrawCircle(floor[2],6,new Color("272b23"));}
                if(prop.Kind=="stash")Text("储物箱 · 待开放",center-new Vector2(30,22),10,"d6c493");
            }
        }
        }
        foreach(var npc in town.Npcs.OrderBy(n=>n.Position.X+n.Position.Y))
        {
            bool active=world.TownPhase==TownVisitPhase.Heal&&npc.HasRole("heal")||world.TownPhase==TownVisitPhase.Sell&&npc.HasRole("sell");
            var p=Project(npc.Position);DrawFighter(p,new Color(npc.HasRole("heal")?"b69fca":npc.HasRole("sell")?"d4a363":"9eac83"),-1,false,1,npc.Name);
            if(active)DrawArc(p,new Vector2(12,0).Length(),0,MathF.Tau,24,new Color("e7c978"),2,true);
            string role=string.Join(" / ",npc.Role.Split(';').Select(r=>r switch {"heal"=>"治疗","sell"=>"售卖杂物","mercenary"=>"队伍管理·待开放","gamble"=>"赌博·待开放","travel"=>"航行·待开放","magic"=>"法系商店·待开放","dialogue"=>"剧情·待开放",_=>"鉴定·待开放"}));
            Text(role,p+new Vector2(-20,14),10,active?"efd498":"a6b5a3");
        }
        if(world.TownReturnPortalVisible){DrawPortalRing(Project(config.TownPortal),.85f);Text("回城传送门",Project(config.TownPortal)+new Vector2(-25,20),11,"a8d9e4");}
        var pad=Project(config.TownWaypoint);DrawArc(pad,17,0,MathF.Tau,32,new Color("9ab6bd"),3,true);DrawArc(pad,10,0,MathF.Tau,24,new Color("537a88"),2,true);
        for(int i=0;i<4;i++){float a=i*MathF.Tau/4;DrawCircle(pad+new Vector2(MathF.Cos(a)*18,MathF.Sin(a)*9),3,new Color("a4c0c4"));}
        Text("固定传送阵",pad+new Vector2(-25,22),11,"a8d9e4");
        for(int i=session.Heroes.Count-1;i>=0;i--)
        {
            var hero=session.Heroes[i];DrawFighter(Project(VisualPosition(hero)),heroColors[i],i,false,hero.Hp/hero.MaxHp,i==0?"小队":"");
        }
        Text(town.ExitName,Project(town.Theme=="camp"?new V(town.Width-6,town.Exit.Y):town.Exit)+new Vector2(-30,29),11,"e7c978");
        Text(town.Name,new Vector2(48,165),20,"f4e1b9");Text(world.Status,new Vector2(48,190),14,"e0e8e7");
        Text(session.Gm.SkipSupplies?"跳过补给 · 直接前往出发点":"按需补给 · "+town.Npc("heal").Name+"治疗 → "+town.Npc("sell").Name+"售卖 → 出发",new Vector2(48,218),12,"a6b5a3");
    }
}
