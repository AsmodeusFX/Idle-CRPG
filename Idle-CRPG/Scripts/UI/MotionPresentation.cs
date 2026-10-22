using IdleCrpg.Core;
using System.Collections.Generic;
using System.Numerics;
using System;
using System.Linq;

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
    private readonly Dictionary<Actor,Vector2> combatOffsets=new();
    private GameSession? visualOwner;
    private Room? visualRoom;
    /// <summary>围攻偏移只改变绘制，按本房实例ID稳定取位；目标变化平滑过渡，禁止写回Actor.Position。</summary>
    public void UpdateCombatVisuals(GameSession game,double dt)
    {
        var room=game.Floors.Count>0&&(game.World==null||game.World.InDungeonScene)?game.Floor.Rooms[game.CurrentRoom]:null;
        if(!ReferenceEquals(visualOwner,game)||!ReferenceEquals(visualRoom,room)){combatOffsets.Clear();visualOwner=game;visualRoom=room;}
        if(room==null)return;
        var locks=game.MonsterTargets.ToDictionary(l=>l.Owner);
        float blend=1-MathF.Exp(-12*(float)Math.Clamp(dt,0,.1));
        for(int i=0;i<room.Monsters.Count;i++)
        {
            var actor=room.Monsters[i];Vector2 goal=Vector2.Zero;
            if(actor.Alive&&locks.TryGetValue(game.Heroes.Count+i,out var target)&&target.Kind!=MonsterLockKind.Ranged)
            {
                // 怪物锁定索引包含召唤物；绘制偏移必须按友方总表取位，不能只索引Heroes。
                var friendly=target.Target>=0&&target.Target<game.Heroes.Count?game.Heroes[target.Target]
                    :target.Target>=game.Heroes.Count&&target.Target-game.Heroes.Count<game.Summons.Count?game.Summons[target.Target-game.Heroes.Count]:null;
                if(friendly==null)continue;
                float distance=Vector2.Distance(actor.Position,friendly.Position);
                float proximity=Math.Clamp((actor.Range+2-distance)/2,0,1);
                float angle=i*2.399963f+target.Target*.7f;
                goal=new Vector2(MathF.Cos(angle)*(18+i%3*3),MathF.Sin(angle)*(13+i%4*3))*proximity;
            }
            combatOffsets[actor]=Vector2.Lerp(combatOffsets.GetValueOrDefault(actor),goal,blend);
        }
    }
    public Vector2 CombatVisualOffset(Actor actor)=>combatOffsets.GetValueOrDefault(actor);
    public void BeforeTick(GameSession game)
    {
        owner=game;stage=game.World?.Stage;floor=game.Floors.Count>0?game.Floor:null;
        world=game.World;worldPosition=game.World?.Position??default;previous.Clear();outdoorParty.Clear();
        for(int i=0;i<game.Heroes.Count;i++)outdoorParty.Add(game.World?.OutdoorPartyPosition(i)??default);
        foreach(var h in game.Heroes)previous[h]=h.Position;
        // 召唤物同样使用固定步前后位置插值；缺这一步会让它们每逻辑步冻结再跳变。
        foreach(var s in game.Summons)previous[s]=s.Position;
        if(floor!=null)foreach(var room in floor.Rooms)foreach(var m in room.Monsters)previous[m]=m.Position;
    }
    private bool Compatible(GameSession game)=>ReferenceEquals(owner,game)&&ReferenceEquals(world,game.World)&&stage==game.World?.Stage&&ReferenceEquals(floor,game.Floors.Count>0?game.Floor:null);
    public Vector2 Position(GameSession game,Actor actor,float alpha)=>
        // 脱队闪现是瞬时位移，插值会画成横穿地图的滑行，因此本步直接取新坐标。
        Compatible(game)&&!game.SummonBlinkedThisTick(actor)&&previous.TryGetValue(actor,out var p)?Vector2.Lerp(p,actor.Position,alpha):actor.Position;
    public Vector2 OutdoorPartyPosition(GameSession game,int index,float alpha)=>Compatible(game)&&index<outdoorParty.Count&&Vector2.Distance(worldPosition,game.World!.Position)<game.Config.TravelSpeed*.11f?Vector2.Lerp(outdoorParty[index],game.World.OutdoorPartyPosition(index),alpha):game.World!.OutdoorPartyPosition(index);
    public Vector2 WorldPosition(GameSession game,float alpha)=>Compatible(game)?Vector2.Lerp(worldPosition,game.World!.Position,alpha):game.World!.Position;
}
