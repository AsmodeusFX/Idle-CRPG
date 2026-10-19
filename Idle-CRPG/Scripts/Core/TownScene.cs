using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public record TownNpc(string Id,string Name,string Role,Vector2 Position,string Description);
public record TownProp(string Id,string Kind,float X,float Y,float Width,float Height,bool Blocks);
/// <summary>固定城镇局部场景，与世界格子坐标隔离；目前唯一东侧出入口，后续可扩展入口表。</summary>
public sealed class TownScene
{
    public string Id="",Name="";
    public float Width,Height,InteractionRange,ServiceSeconds;
    public Vector2 Entry,Exit;
    public List<TownNpc> Npcs=new();
    public List<TownProp> Props=new();
    private readonly Dictionary<Vector2,IReadOnlyList<Vector2>> entrancePaths=new();
    public TownNpc Npc(string role)=>Npcs.Single(n=>n.Role==role);
    public bool Walkable(Vector2 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&
        ((p.X>=2&&p.X<=Width-6&&p.Y>=2&&p.Y<=Height-4)||(p.X>=Width-6&&p.X<=Exit.X&&Math.Abs(p.Y-Exit.Y)<=1))&&
        !Props.Any(o=>o.Blocks&&p.X>=o.X-.2f&&p.X<=o.X+o.Width+.2f&&p.Y>=o.Y-.2f&&p.Y<=o.Y+o.Height+.2f);
    /// <summary>城镇半格广度优先寻路；绘制与移动共用物件矩形，禁止穿帐篷、围栏或从非入口离场。</summary>
    public IReadOnlyList<Vector2> FindPath(Vector2 from,Vector2 to)
    {
        if(from==Entry&&entrancePaths.TryGetValue(to,out var cached))return cached;
        if(!Walkable(from)||!Walkable(to))throw new InvalidOperationException("城镇路径端点不可走");
        (int X,int Y) Cell(Vector2 p)=>((int)MathF.Round(p.X*2),(int)MathF.Round(p.Y*2));
        Vector2 Point((int X,int Y) p)=>new(p.X*.5f,p.Y*.5f);
        var start=Cell(from);var end=Cell(to);var queue=new Queue<(int X,int Y)>();var parents=new Dictionary<(int X,int Y),(int X,int Y)>();queue.Enqueue(start);parents[start]=start;
        while(queue.Count>0)
        {
            var p=queue.Dequeue();if(p==end)break;
            foreach(var n in new[]{(p.X+1,p.Y),(p.X-1,p.Y),(p.X,p.Y+1),(p.X,p.Y-1)})
                if(!parents.ContainsKey(n)&&Walkable(Point(n))){parents[n]=p;queue.Enqueue(n);}
        }
        if(!parents.ContainsKey(end))throw new InvalidOperationException("城镇服务不可达");
        var result=new List<Vector2>{to};var current=end;while(current!=start){result.Add(Point(current));current=parents[current];}result.Add(from);result.Reverse();var path=result.AsReadOnly();if(from==Entry)entrancePaths[to]=path;return path;
    }
}
public sealed partial class Config
{
    public TownScene? Town {get;private set;}
    private void LoadTown(string directory,int chapter)
    {
        string root=Path.Combine(directory,"village");
        var portal=Read(Path.Combine(root,"portal_rule.csv")).Single();
        PortalSeconds=Number(portal,"phase_seconds");TownPortal=new(Number(portal,"town_x"),Number(portal,"town_y"));
        TownWaypoint=new(Number(portal,"waypoint_x"),Number(portal,"waypoint_y"));
        PortalRange=Number(portal,"use_range");
        if(PortalRange<=0||PortalRange>1)throw new InvalidDataException("传送门使用距离必须为0～1");
        if(PortalSeconds<=0||PortalSeconds>10)throw new InvalidDataException("传送阶段时间必须为0～10秒");
        var row=Read(Path.Combine(root,"town_scene.csv")).Single(r=>r["chapter_id"]=="act"+(chapter+1));
        Town=new(){Id=row["id"],Name=row["name"],Width=Number(row,"width"),Height=Number(row,"height"),Entry=new(Number(row,"entry_x"),Number(row,"entry_y")),Exit=new(Number(row,"exit_x"),Number(row,"exit_y")),InteractionRange=Number(row,"interaction_range"),ServiceSeconds=Number(row,"service_seconds")};
        Town.Npcs=Read(Path.Combine(root,"town_npc.csv")).Where(r=>r["scene_id"]==Town.Id).Select(r=>new TownNpc(r["id"],r["name"],r["role"],new(Number(r,"x"),Number(r,"y")),r["description"])).ToList();
        Town.Props=Read(Path.Combine(root,"town_prop.csv")).Where(r=>r["scene_id"]==Town.Id).Select(r=>new TownProp(r["id"],r["kind"],Number(r,"x"),Number(r,"y"),Number(r,"width"),Number(r,"height"),Boolean(r,"blocks"))).ToList();
        if(Town.Width<16||Town.Width>64||Town.Height<16||Town.Height>64||Town.InteractionRange<=0||Town.InteractionRange>1||Town.ServiceSeconds<=0||Town.Npcs.Count!=6||Town.Npcs.Select(n=>n.Role).Distinct().Count()!=6||new[]{"heal","mercenary","sell","gamble","travel","identify"}.Any(role=>!Town.Npcs.Any(n=>n.Role==role))||Town.Npcs.Select(n=>n.Id).Distinct().Count()!=6||Town.Props.Any(p=>p.Width<=0||p.Height<=0||p.X<0||p.Y<0||p.X+p.Width>Town.Width||p.Y+p.Height>Town.Height)||Town.Exit.X!=Town.Width-4||Town.Entry.X!=Town.Width-5||Town.Entry.Y!=Town.Exit.Y||Town.Exit.Y<4||Town.Exit.Y>Town.Height-6)throw new InvalidDataException("固定城镇尺寸、唯一出口或六职能NPC配置非法");
        Town.FindPath(Town.Entry,Town.Exit);
        foreach(var npc in Town.Npcs){if(string.IsNullOrWhiteSpace(npc.Name))throw new InvalidDataException("城镇NPC名称为空");Town.FindPath(Town.Entry,npc.Position);}
    }
}

public enum TownVisitPhase { Heal, Sell, Exit }
public sealed partial class WorldCampaign
{
    public TownVisitPhase TownPhase {get;private set;}
    public int TownVersion {get;private set;}=1;
    internal void SpawnTown(GameSession game,TownVisitPhase phase)
    {
        TownPhase=phase;TradeElapsed=0;villagePaths.Clear();
        for(int i=0;i<game.Heroes.Count;i++)game.Heroes[i].Position=config.Town!.Entry+new Vector2(0,(i-1)*.35f);
    }
    /// <summary>新开局及传送到新章节从营地门内出发；无需凭空经历一次交易。</summary>
    internal void StartTownDeparture(GameSession game)
    {
        if(config.Town==null)return;
        Stage=CampaignStage.Village;route.Clear();SpawnTown(game,TownVisitPhase.Exit);Status=config.Town.Name+(HasTownPortal?" · 前往固定传送阵":" · 前往唯一出口");
    }
    private void TickTown(GameSession game,float dt)
    {
        var town=config.Town!;var target=TownPhase==TownVisitPhase.Exit?(HasTownPortal?config.TownWaypoint:town.Exit):town.Npc(TownPhase==TownVisitPhase.Heal?"heal":"sell").Position;
        bool arrived=true;
        foreach(var hero in game.Heroes)
        {
            if(Vector2.Distance(hero.Position,target)<=town.InteractionRange)continue;
            arrived=false;
            if(!villagePaths.TryGetValue(hero.SpecId,out var path)){path=new(town.FindPath(hero.Position,target));villagePaths[hero.SpecId]=path;}
            while(path.Count>0&&Vector2.DistanceSquared(path.Peek(),hero.Position)<.0001f)path.Dequeue();
            GameSession.Move(hero,path.Count>0?path.Peek():target,dt,config.VillageSpeed);
        }
        if(!arrived){TradeElapsed=0;Status="前往 "+(TownPhase==TownVisitPhase.Exit?(HasTownPortal?"固定传送阵":"营地出口"):town.Npc(TownPhase==TownVisitPhase.Heal?"heal":"sell").Name);return;}
        if(TownPhase==TownVisitPhase.Exit)
        {
            // 全队到达唯一出口后才切回世界场景，世界位置始终是村庄锚点。
            if(!game.AfterVillageTrade())
            {
                Plan(Target.Farming);
                if(ClosestWaypoint() is {} waypoint)BeginPortal(false,waypoint.Id);
            }
            game.MarkChanged();return;
        }
        TradeElapsed=Math.Min(town.ServiceSeconds,TradeElapsed+dt);
        Status=(TownPhase==TownVisitPhase.Heal?town.Npc("heal").Name+" · 治疗":town.Npc("sell").Name+" · 售卖杂物")+$" {TradeElapsed:0.0}/{town.ServiceSeconds:0.0}秒";
        if(TradeElapsed+.00001f<town.ServiceSeconds)return;
        if(TownPhase==TownVisitPhase.Heal)
        {
            foreach(var h in game.Heroes){h.Hp=h.MaxHp;h.Timer=0;}
            TownPhase=TownVisitPhase.Sell;game.Note(town.Npc("heal").Name+"：全队治疗完成");
        }
        else
        {
            long sold=game.SellJunk();Trades++;TownPhase=TownVisitPhase.Exit;game.Note(town.Npc("sell").Name+$"：杂物售出 {sold} 金币，前往营地出口");
        }
        TradeElapsed=0;villagePaths.Clear();game.MarkChanged();
    }
}
