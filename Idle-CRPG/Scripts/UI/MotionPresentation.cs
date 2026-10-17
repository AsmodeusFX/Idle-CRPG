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
    public void BeforeTick(GameSession game)
    {
        owner=game;stage=game.World?.Stage;floor=game.Floors.Count>0?game.Floor:null;
        worldPosition=game.World?.Position??default;previous.Clear();
        foreach(var h in game.Heroes)previous[h]=h.Position;
        if(floor!=null)foreach(var room in floor.Rooms)foreach(var m in room.Monsters)previous[m]=m.Position;
    }
    private bool Compatible(GameSession game)=>ReferenceEquals(owner,game)&&stage==game.World?.Stage&&ReferenceEquals(floor,game.Floors.Count>0?game.Floor:null);
    public Vector2 Position(GameSession game,Actor actor,float alpha)=>Compatible(game)&&previous.TryGetValue(actor,out var p)?Vector2.Lerp(p,actor.Position,alpha):actor.Position;
    public Vector2 WorldPosition(GameSession game,float alpha)=>Compatible(game)?Vector2.Lerp(worldPosition,game.World!.Position,alpha):game.World!.Position;
}
