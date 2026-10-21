using IdleCrpg.Core;
using System.Collections.Generic;
using System.Numerics;

/// <summary>只读表现插值：固定逻辑前后位置之间绘制，不回写模型或改变交互、伤害判定。</summary>
public sealed class MotionPresentation
{
    private readonly Dictionary<Actor,Vector2> previous=new();
    private GameSession? owner;
    private Floor? floor;
    private CampaignStage? stage;
    private Vector2 worldPosition;
    private WorldCampaign? world;
    private readonly List<Vector2> outdoorParty=new();
    public void BeforeTick(GameSession game)
    {
        owner=game;stage=game.World?.Stage;floor=game.Floors.Count>0?game.Floor:null;
        world=game.World;worldPosition=game.World?.Position??default;previous.Clear();outdoorParty.Clear();
        for(int i=0;i<game.Heroes.Count;i++)outdoorParty.Add(game.World?.OutdoorPartyPosition(i)??default);
        foreach(var h in game.Heroes)previous[h]=h.Position;
        if(floor!=null)foreach(var room in floor.Rooms)foreach(var m in room.Monsters)previous[m]=m.Position;
    }
    private bool Compatible(GameSession game)=>ReferenceEquals(owner,game)&&ReferenceEquals(world,game.World)&&stage==game.World?.Stage&&ReferenceEquals(floor,game.Floors.Count>0?game.Floor:null);
    public Vector2 Position(GameSession game,Actor actor,float alpha)=>Compatible(game)&&previous.TryGetValue(actor,out var p)?Vector2.Lerp(p,actor.Position,alpha):actor.Position;
    public Vector2 OutdoorPartyPosition(GameSession game,int index,float alpha)=>Compatible(game)&&index<outdoorParty.Count&&Vector2.Distance(worldPosition,game.World!.Position)<game.Config.TravelSpeed*.11f?Vector2.Lerp(outdoorParty[index],game.World.OutdoorPartyPosition(index),alpha):game.World!.OutdoorPartyPosition(index);
    public Vector2 WorldPosition(GameSession game,float alpha)=>Compatible(game)?Vector2.Lerp(worldPosition,game.World!.Position,alpha):game.World!.Position;
}
