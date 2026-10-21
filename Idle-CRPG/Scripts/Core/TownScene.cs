using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public record TownNpc(string Id,string Name,string Role,Vector2 Position,string Description)
{
    /// <summary>一位角色可提供多种职能；分号分隔配置，不按名字绑定服务。</summary>
    public bool HasRole(string role)=>Role.Split(';').Contains(role);
}
public record TownProp(string Id,string Kind,float X,float Y,float Width,float Height,bool Blocks);
public record TownZone(string Id,string Kind,float X,float Y,float Width,float Height)
{
    public bool Contains(Vector2 p)=>p.X>=X&&p.Y>=Y&&p.X<=X+Width&&p.Y<=Y+Height;
}
/// <summary>固定城镇局部场景，与世界格子坐标隔离；地板并集及建筑占地共同决定可走范围。</summary>
public sealed class TownScene
{
    public string Id="",Name="";
    public string Theme="camp",ExitName="唯一出入口";
    public int LayoutVersion;
    public float Width,Height,InteractionRange,ServiceSeconds;
    public Vector2 Entry,Exit;
    public List<TownNpc> Npcs=new();
    public List<TownProp> Props=new();
    public List<TownZone> Zones=new();
    private readonly Dictionary<Vector2,IReadOnlyList<Vector2>> entrancePaths=new();
    private static readonly (int X,int Y)[] PathDirections={(1,0),(-1,0),(0,1),(0,-1)};
    public TownNpc Npc(string role)=>Npcs.Single(n=>n.HasRole(role));
    /// <summary>共用地板和建筑占地；寻路热路径不创建捕获坐标的委托或临时集合。</summary>
    public bool Walkable(Vector2 p)
    {
        if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))return false;
        bool inside=false;
        if(Zones.Count==0)inside=(p.X>=2&&p.X<=Width-6&&p.Y>=2&&p.Y<=Height-4)||(p.X>=Width-6&&p.X<=Exit.X&&Math.Abs(p.Y-Exit.Y)<=1);
        else foreach(var zone in Zones)if(zone.Contains(p)){inside=true;break;}
        if(!inside)return false;
        foreach(var o in Props)
            if(o.Blocks&&p.X>=o.X-.2f&&p.X<=o.X+o.Width+.2f&&p.Y>=o.Y-.2f&&p.Y<=o.Y+o.Height+.2f)return false;
        return true;
    }
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
            foreach(var direction in PathDirections)
            {
                var n=(p.X+direction.X,p.Y+direction.Y);
                if(!parents.ContainsKey(n)&&Walkable(Point(n))){parents[n]=p;queue.Enqueue(n);}
            }
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
        PortalSeconds=Number(portal,"phase_seconds");
        PortalRange=Number(portal,"use_range");
        if(PortalRange<=0||PortalRange>1)throw new InvalidDataException("传送门使用距离必须为0～1");
        if(PortalSeconds<=0||PortalSeconds>10)throw new InvalidDataException("传送阶段时间必须为0～10秒");
        var row=Read(Path.Combine(root,"town_scene.csv")).Single(r=>r["chapter_id"]=="act"+(chapter+1));
        Town=new(){Id=row["id"],Name=row["name"],Theme=row["theme"],LayoutVersion=Integer(row,"layout_version"),ExitName=row["exit_name"],Width=Number(row,"width"),Height=Number(row,"height"),Entry=new(Number(row,"entry_x"),Number(row,"entry_y")),Exit=new(Number(row,"exit_x"),Number(row,"exit_y")),InteractionRange=Number(row,"interaction_range"),ServiceSeconds=Number(row,"service_seconds")};
        TownPortal=new(Number(row,"return_x"),Number(row,"return_y"));TownWaypoint=new(Number(row,"waypoint_x"),Number(row,"waypoint_y"));
        Town.Zones=Read(Path.Combine(root,"town_zone.csv")).Where(r=>r["scene_id"]==Town.Id).Select(r=>new TownZone(r["id"],r["kind"],Number(r,"x"),Number(r,"y"),Number(r,"width"),Number(r,"height"))).ToList();
        Town.Npcs=Read(Path.Combine(root,"town_npc.csv")).Where(r=>r["scene_id"]==Town.Id).Select(r=>new TownNpc(r["id"],r["name"],r["role"],new(Number(r,"x"),Number(r,"y")),r["description"])).ToList();
        Town.Props=Read(Path.Combine(root,"town_prop.csv")).Where(r=>r["scene_id"]==Town.Id).Select(r=>new TownProp(r["id"],r["kind"],Number(r,"x"),Number(r,"y"),Number(r,"width"),Number(r,"height"),Boolean(r,"blocks"))).ToList();
        string[] roles={"heal","mercenary","sell","gamble","travel","identify","magic","dialogue","formation"};
        if(Town.Width<16||Town.Width>64||Town.Height<16||Town.Height>64||Town.InteractionRange<=0||Town.InteractionRange>1||Town.ServiceSeconds<=0||Town.LayoutVersion<0||!new[]{"camp","sand","docks","sanctum","snow"}.Contains(Town.Theme)||string.IsNullOrWhiteSpace(Town.ExitName)||Town.Npcs.Count<4||Town.Npcs.Select(n=>n.Id).Distinct().Count()!=Town.Npcs.Count||Town.Npcs.Any(n=>n.Role.Split(';').Distinct().Count()!=n.Role.Split(';').Length||n.Role.Split(';').Any(r=>!roles.Contains(r)))||roles.Take(6).Any(role=>Town.Npcs.Count(n=>n.HasRole(role))>1)||new[]{"heal","mercenary","sell","gamble","identify","formation"}.Any(role=>Town.Npcs.Count(n=>n.HasRole(role))!=1)||Town.Props.Select(p=>p.Id).Distinct().Count()!=Town.Props.Count||Town.Props.Any(p=>p.Width<=0||p.Height<=0||p.X<0||p.Y<0||p.X+p.Width>Town.Width||p.Y+p.Height>Town.Height)||Town.Zones.Select(z=>z.Id).Distinct().Count()!=Town.Zones.Count||Town.Zones.Any(z=>z.Width<1||z.Height<1||z.X<0||z.Y<0||z.X+z.Width>Town.Width||z.Y+z.Height>Town.Height||!new[]{"stone","wood","bridge","stairs","sand","snow"}.Contains(z.Kind))||Town.Theme!="camp"&&Town.Zones.Count==0)throw new InvalidDataException("固定城镇尺寸、职能映射或场景几何配置非法");
        Town.FindPath(Town.Entry,Town.Exit);
        if(Vector2.Distance(TownPortal,TownWaypoint)<=1)throw new InvalidDataException("回城落点与固定出发阵必须分离");
        string[] propKinds={"tent","wagon","forge","fire","stash","palace","sand_house","market","tower","water","pier","ship","sewer","hut","ruin","shrine","palm","column","angel","holy_fire","keep","snow_house","sealed_gate"};
        if(Town.Props.Any(p=>!propKinds.Contains(p.Kind)))throw new InvalidDataException("未知城镇建筑绘制类型");
        foreach(var anchor in new[]{Town.Entry,TownPortal,TownWaypoint})
            for(int i=-1;i<=1;i++)Town.FindPath(Town.Entry,anchor+new Vector2(0,i*.35f));
        foreach(var npc in Town.Npcs){if(string.IsNullOrWhiteSpace(npc.Name))throw new InvalidDataException("城镇NPC名称为空");Town.FindPath(Town.Entry,npc.Position);}
    }
}

public enum TownVisitPhase { Heal, Sell, Exit, Formation }
public sealed partial class WorldCampaign
{
    public TownVisitPhase TownPhase {get;private set;}
    public int TownVersion {get;private set;}=1;
    internal void SpawnTown(GameSession game,TownVisitPhase phase)
    {
        TownPhase=phase;TradeElapsed=0;villagePaths.Clear();
        for(int i=0;i<game.Heroes.Count;i++)game.Heroes[i].Position=config.Town!.Entry+new Vector2(0,PartyFormation.SideOffset(i,game.Heroes.Count));
    }
    /// <summary>新开局及传送到新章节从营地门内出发；无需凭空经历一次交易。</summary>
    internal void StartTownDeparture(GameSession game)
    {
        if(config.Town==null)return;
        Stage=CampaignStage.Village;route.Clear();SpawnTown(game,TownVisitPhase.Exit);Status=config.Town.Name+(HasTownPortal?" · 前往固定传送阵":" · 前往唯一出口");
    }
    private void TickTown(GameSession game,float dt)
    {
        // 每步重新核对需求，兼容读档和在补给途中切换 GM；新增 NPC/MP 功能时须扩展此流程。
        var next=TownPhase;
        if(game.PendingFormation.Count>0)next=TownVisitPhase.Formation;else if(next==TownVisitPhase.Formation)next=TownVisitPhase.Heal;else if(game.Gm.SkipSupplies)next=TownVisitPhase.Exit;
        else
        {
            if(next==TownVisitPhase.Heal&&!game.NeedsTownHealing)next=TownVisitPhase.Sell;
            if(next==TownVisitPhase.Sell&&!game.HasAutoSellItems)next=TownVisitPhase.Exit;
        }
        if(next!=TownPhase){TownPhase=next;TradeElapsed=0;villagePaths.Clear();game.MarkChanged();}
        var town=config.Town!;var target=TownPhase==TownVisitPhase.Exit?(HasTownPortal?config.TownWaypoint:town.Exit):town.Npc(TownPhase==TownVisitPhase.Formation?"formation":TownPhase==TownVisitPhase.Heal?"heal":"sell").Position;
        bool arrived=true;
        foreach(var hero in game.Heroes)
        {
            if(Vector2.Distance(hero.Position,target)<=town.InteractionRange)continue;
            arrived=false;
            if(!villagePaths.TryGetValue(hero.SpecId,out var path)){path=new(town.FindPath(hero.Position,target));villagePaths[hero.SpecId]=path;}
            // 消耗完整步长的距离预算；倍速由 UI 增加固定步数量，不能在此重复乘倍速。
            float remaining=dt*config.VillageSpeed;
            while(path.Count>0&&remaining>0)
            {
                var point=path.Peek();float distance=Vector2.Distance(hero.Position,point);
                if(distance<=remaining){hero.Position=point;path.Dequeue();remaining-=distance;}
                else {hero.Position+=Vector2.Normalize(point-hero.Position)*remaining;remaining=0;}
            }
        }
        if(!arrived){TradeElapsed=0;Status="前往 "+(TownPhase==TownVisitPhase.Exit?(HasTownPortal?"固定传送阵":"营地出口"):town.Npc(TownPhase==TownVisitPhase.Formation?"formation":TownPhase==TownVisitPhase.Heal?"heal":"sell").Name);return;}
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
        Status=(TownPhase==TownVisitPhase.Heal?town.Npc("heal").Name+" · 治疗":TownPhase==TownVisitPhase.Formation?town.Npc("formation").Name+" · 变更编队":town.Npc("sell").Name+" · 售卖杂物")+$" {TradeElapsed:0.0}/{town.ServiceSeconds:0.0}秒";
        if(TradeElapsed+.00001f<town.ServiceSeconds)return;
        if(TownPhase==TownVisitPhase.Formation)
        {game.ApplyPendingFormation();TownPhase=TownVisitPhase.Heal;TradeElapsed=0;villagePaths.Clear();return;}
        if(TownPhase==TownVisitPhase.Heal)
        {
            game.RestoreTownHealth();
            TownPhase=TownVisitPhase.Sell;game.Note(town.Npc("heal").Name+"：全队治疗完成");
        }
        else
        {
            long sold=game.SellJunk();Trades++;TownPhase=TownVisitPhase.Exit;game.Note(town.Npc("sell").Name+$"：杂物售出 {sold} 金币，前往营地出口");
        }
        TradeElapsed=0;villagePaths.Clear();game.MarkChanged();
    }
}
