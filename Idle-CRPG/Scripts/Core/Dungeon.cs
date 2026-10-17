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
    public Vector2 DoorTowards(Room other)
    {
        var d=Vector2.Normalize(other.Center-Center);return Center+d*(Math.Abs(d.X)>.5f?HalfWidth:HalfHeight);
    }
    public bool Boss, Revealed, Generated, EventUsed;
    public int SnapshotLevel;
    public string Event = "";
    public List<int> Neighbors = new();
    public List<Actor> Monsters = new();
    public bool Clear => Generated && Monsters.All(m => !m.Alive);
}

public sealed class Passage
{
    public int A, B;
    public bool Revealed;
}

public sealed class Floor
{
    public List<Room> Rooms = new();
    public List<Passage> Passages = new();
    public bool Clear => Rooms.All(r => r.Clear);
    public Room BossRoom => Rooms.Single(r => r.Boss);
}

public static class Dungeon
{
    /// <summary>只生成拓扑。坐标网格保证房间与走廊互不重叠。</summary>
    public static List<Floor> Generate(int seed, Config config)
    {
        var rng = new Random(seed);
        var floors = new List<Floor>();
        int count = rng.Next(config.FloorsMin, config.FloorsMax + 1);
        for (int f = 0; f < count; f++)
        {
            var floor = new Floor();
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
