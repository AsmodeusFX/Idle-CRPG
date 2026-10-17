using System;
using System.Linq;
using System.IO;
using System.Numerics;
using IdleCrpg.Core;

static void Check(bool value,string message) {if(!value)throw new Exception(message);}
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
string Shape(int seed)=>string.Join(";",Dungeon.Generate(seed,c).SelectMany(f=>f.Rooms.Select(r=>$"{r.Id}:{r.X},{r.Y}:{string.Join(',',r.Neighbors)}")));
Check(Shape(42)==Shape(42),"种子不可重现");
Check(Shape(42)!=Shape(43),"种子没有差异");
for(int seed=0;seed<100;seed++)
{
    foreach(var f in Dungeon.Generate(seed,c))
    {
        Check(f.Passages.Count==f.Rooms.Count-1,"不是树");
        Check(f.Rooms.Select(r=>(r.X,r.Y)).Distinct().Count()==f.Rooms.Count,"房间重叠");
        Check(f.Rooms.All(r=>!r.Generated&&!r.Revealed&&r.Monsters.Count==0),"预生成内容");
        Check(f.BossRoom.Depth==f.Rooms.Max(r=>r.Depth),"BOSS 不在最远端");
        Check(f.Passages.All(p=>Vector2.Distance(f.Rooms[p.A].Center,f.Rooms[p.B].Center)==12),"走廊连接非法");
    }
}
var s=new GameSession(c,42,new[]{"warrior","ranger","mage"});
Check(s.Floor.Rooms.Count(r=>r.Revealed)==1,"提前揭示");
Check(s.Floor.Rooms.Skip(1).All(r=>!r.Generated),"提前刷怪");
// 用高生命验证完整探索与层间门禁，保留普通攻击和成长过程。
foreach(var h in s.Heroes){h.Hp=h.MaxHp=100000;}
var identities=s.Heroes.ToArray();
int previousFloor=0; bool sawGrowth=false,sawDeferred=false;
for(int tick=0;tick<200000&&!s.Won&&!s.Lost;tick++)
{
    var snapshots=s.Floors.SelectMany(f=>f.Rooms).Where(r=>r.Generated).ToDictionary(r=>r,r=>(r.SnapshotLevel,r.Monsters.Count));
    s.Tick(0.05f);
    foreach(var r in s.Floors.SelectMany(f=>f.Rooms).Where(r=>r.Generated&&!snapshots.ContainsKey(r)&&r.Id!=0))
    {
        Check(r.Revealed,"刷怪未揭示");
        Check(r.Neighbors.Any(n=>s.Heroes.Any(h=>h.Alive&&Vector2.Distance(h.Position,r.Center+Vector2.Normalize(s.Floor.Rooms[n].Center-r.Center)*3.5f)<=0.321f)),"未触门就生成内容");
    }
    foreach(var kv in snapshots)Check(kv.Key.SnapshotLevel==kv.Value.SnapshotLevel&&kv.Key.Monsters.Count==kv.Value.Count,"回访刷新");
    if(s.FloorIndex!=previousFloor){Check(s.Floors[previousFloor].Clear,"未清层进入下一层");previousFloor=s.FloorIndex;}
    sawGrowth|=s.Heroes[0].Level>1;
    sawDeferred|=s.Floors.SelectMany(f=>f.Rooms).Any(r=>r.SnapshotLevel>1);
}
Check(s.Won,"完整地牢没有通关: "+s.Status);
Check(s.Floors.All(f=>f.Clear&&f.Rooms.All(r=>r.Revealed)),"没有清空全部房间");
Check(identities.SequenceEqual(s.Heroes),"队伍成员改变");
Check(sawGrowth&&sawDeferred,"成长没有作用于后续内容");
var lost=new GameSession(c,1,new[]{"warrior","ranger","mage"});foreach(var h in lost.Heroes)h.Hp=0;lost.Tick(0.05f);Check(lost.Lost,"全灭未失败");
Console.WriteLine("PASS: 100 seeds, topology, deferred spawn, exploration, growth, revisits, floor gating, victory, defeat");
