using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed class Room
{
    public int Id, X, Y, Depth;
    public float HalfWidth=3.5f,HalfHeight=3.5f;
    public Vector2 Center => new(X * 12, Y * 12);
    public Vector2 HalfSize => new(HalfWidth,HalfHeight);
    /// <summary>事件显示和交互共用位置；适应小房间尺寸。</summary>
    public Vector2 EventPosition => Center+Vector2.Min(new Vector2(1.7f),HalfSize-new Vector2(.6f));
    public Vector2 DoorTowards(Room other)
    {
        var d=Vector2.Normalize(other.Center-Center);return Center+d*(Math.Abs(d.X)>.5f?HalfWidth:HalfHeight);
    }
    public bool Boss, Revealed, Generated, EventUsed;
    public int SnapshotLevel;
    public string Event = "";
    public List<int> Neighbors = new();
    public List<Actor> Monsters = new();
    public bool ContentCleared;
    public List<MonsterOrigin> Origins=new();
    public List<RoomLoot> Loot=new();
    /// <summary>
    /// 遭遇序列里仍有未登场的接替 Boss 时必须保持"未清空"：否则议会成员全部倒下的一瞬间房间就会结算，
    /// 墨菲斯托永远没有登场机会。该标记由 BossPhaseRuntime 维护，不进入存档。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool PhasePending;
    public bool Clear => Generated && (ContentCleared || (!PhasePending && Monsters.All(m => !m.Alive)));
}

/// <summary>进房生成快照，只保存重建所需信息，不保存怪物交战中的生命、冷却和移动。</summary>
public record MonsterOrigin(string SpecId,int Level,bool Boss,Vector2 Position,SpawnModifiers Modifiers);
/// <summary>战利品在击杀时确定，清房且救援完成后走近拾取，领取标志与资源同事务保存。</summary>
public sealed class RoomLoot
{
    public Vector2 Position;
    public bool Collected;
    public long Gold;
    public List<InventoryEntry> Items=new();
}

public sealed class Passage
{
    public int A, B;
    public bool Revealed;
}

public sealed class Floor
{
    public int Seed,ActualSpawned,ActualKillFacts;
    public long ElapsedTicks;
    public long BattleTicks,WalkTicks;
    public List<Room> Rooms = new();
    public List<Passage> Passages = new();
    public bool Clear => Rooms.All(r => r.Clear);
    public Room BossRoom => Rooms.Single(r => r.Boss);
    /// <summary>楼梯依附墙壁，优先使用无门的后墙；四边都有门时错开门中心。</summary>
    public Vector2 StairPosition(Room room)
    {
        var directions=new[]{new Vector2(0,-1),new Vector2(-1,0),new Vector2(1,0),new Vector2(0,1)};
        foreach(var d in directions)
            if(!room.Neighbors.Any(n=>Vector2.Dot(Vector2.Normalize(Rooms[n].Center-room.Center),d)>.9f))
                return room.Center+d*room.HalfSize;
        return room.Center+new Vector2(-room.HalfWidth,1.3f);
    }
}

public static class Dungeon
{
    /// <summary>只生成拓扑。坐标网格保证房间与走廊互不重叠。</summary>
    public static List<Floor> Generate(int seed, Config config,int? floorCount=null)
    {
        var rng = new Random(seed);
        var floors = new List<Floor>();
        int count = floorCount??rng.Next(config.FloorsMin, config.FloorsMax + 1);
        if(count<1||count>20)throw new ArgumentOutOfRangeException(nameof(floorCount));
        for (int f = 0; f < count; f++)
        {
            var floor = new Floor {Seed=seed};
            floor.Rooms.Add(new Room { Id = 0 });
            int rooms = rng.Next(config.RoomsMin, config.RoomsMax + 1);
            while (floor.Rooms.Count < rooms)
            {
                var parent = floor.Rooms[rng.Next(floor.Rooms.Count)];
                var d = new[] { (1,0), (-1,0), (0,1), (0,-1) }[rng.Next(4)];
                int x = parent.X + d.Item1, y = parent.Y + d.Item2;
                if (floor.Rooms.Any(r => r.X == x && r.Y == y)) continue;
                var room = new Room { Id = floor.Rooms.Count, X = x, Y = y, Depth = parent.Depth + 1 };
                parent.Neighbors.Add(room.Id); room.Neighbors.Add(parent.Id);
                floor.Passages.Add(new Passage { A = parent.Id, B = room.Id });
                floor.Rooms.Add(room);
            }
            floor.Rooms.OrderByDescending(r => r.Depth).ThenByDescending(r => r.Id).First().Boss = true;
            foreach(var room in floor.Rooms)
            {
                room.HalfWidth=rng.Next(config.RoomWidthMin,config.RoomWidthMax+1)*.5f;
                room.HalfHeight=rng.Next(config.RoomHeightMin,config.RoomHeightMax+1)*.5f;
            }
            floors.Add(floor);
        }
        return floors;
    }
}
