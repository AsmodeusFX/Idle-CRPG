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
for(int tick=0;tick<200000&&!s.Won;tick++)
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
// 清房后先移动再读条，多个死者逐个复活，期间禁止事件结算和换房。
var rescue=new GameSession(c,1,new[]{"warrior","ranger","mage"});
var corpse=rescue.Heroes[0]; var second=rescue.Heroes[1]; var helper=rescue.Heroes[2];
corpse.Hp=0; corpse.Position=new Vector2(2,2); second.Hp=0; second.Position=new Vector2(-2,-2);
helper.Position=new Vector2(-2,2);
rescue.Floor.Rooms[0].Event="宝箱";
rescue.Tick(0.05f);
Check(!corpse.Alive && rescue.ReviveElapsed==0 && helper.Position.X>-2,"未移动或提前读条");
for(int i=0;i<500&&rescue.ReviveElapsed==0;i++)rescue.Tick(0.05f);
Check(rescue.ReviveElapsed>0&&Vector2.Distance(helper.Position,corpse.Position)<=c.ReviveRange,"远距离复活");
int channelTicks=(int)MathF.Ceiling(c.ReviveSeconds/0.05f);
for(int i=0;i<channelTicks-2;i++)rescue.Tick(0.05f);
Check(!corpse.Alive,"读条未完成就复活");
for(int i=0;i<5&&!corpse.Alive;i++)rescue.Tick(0.05f);
Check(corpse.Alive&&Math.Abs(corpse.Hp-corpse.MaxHp*c.ReviveHpRatio)<0.001f,"复活生命错误");
Check(!second.Alive&&rescue.Experience==0&&rescue.CurrentRoom==0,"救援期间结算事件或移动到下个房间");
for(int i=0;i<1000&&!second.Alive;i++)rescue.Tick(0.05f);
Check(second.Alive&&rescue.Experience==0,"第二位队友未获救");
rescue.Tick(0.05f);Check(rescue.Experience==8,"救援后未恢复事件结算");
rescue.Tick(0.05f);Check(rescue.Experience==8,"重复发放事件奖励");

// 活怪存在时禁止复活，清怪之后才允许救援。
var combat=new GameSession(c,3,new[]{"warrior","ranger","mage"});
for(int i=0;i<2000&&combat.CurrentRoom==0;i++)combat.Tick(0.05f);
Check(combat.CurrentRoom!=0,"没有进入战斗房间");
combat.Heroes[0].Hp=0;
foreach(var m in combat.Floor.Rooms[combat.CurrentRoom].Monsters){m.Attack=1;m.Hp=m.MaxHp=100000;}
for(int i=0;i<channelTicks+10;i++)combat.Tick(0.05f);
Check(!combat.Heroes[0].Alive&&combat.ReviveTarget==null,"战斗中复活");
foreach(var m in combat.Floor.Rooms[combat.CurrentRoom].Monsters)m.Hp=0;
for(int i=0;i<1000&&!combat.Heroes[0].Alive;i++)combat.Tick(0.05f);
Check(combat.Heroes[0].Alive,"清怪后未救援");

// 在后续楼层全灭：成长资源保留，所有层内容、事件、探索、移动与救援状态重置。
var retry=new GameSession(c,42,new[]{"warrior","ranger","mage"});
foreach(var h in retry.Heroes)h.Hp=h.MaxHp=100000;
for(int i=0;i<100000&&retry.FloorIndex==0;i++)retry.Tick(0.05f);
Check(retry.FloorIndex>0&&retry.Heroes[0].Level>1&&retry.Kills>0,"未构造跨层成长场景");
var saved=retry.Heroes.Select(h=>(h,h.Level,h.MaxHp,h.Attack,h.Defense)).ToArray();
int xp=retry.Experience,kills=retry.Kills;
string layout=string.Join(';',retry.Floors.SelectMany(f=>f.Rooms).Select(r=>$"{r.Id}:{r.X}:{r.Y}:{r.Boss}"));
retry.Heroes[0].Hp=0; retry.Tick(0.05f);Check(retry.ReviveTarget!=null,"没有进入救援");
foreach(var h in retry.Heroes){h.Hp=0;h.Timer=9;}
retry.Tick(0.05f);
Check(retry.Retries==1&&retry.FloorIndex==0&&retry.CurrentRoom==0&&!retry.Won,"未返回一层入口");
Check(retry.Experience==xp&&retry.Kills==kills,"累计资源丢失");
foreach(var h in saved)Check(retry.Heroes.Contains(h.h)&&h.h.Level==h.Level&&h.h.MaxHp==h.MaxHp&&h.h.Attack==h.Attack&&h.h.Defense==h.Defense&&h.h.Hp==h.MaxHp&&h.h.Timer==0,"成长或队员丢失/未恢复生命");
Check(retry.ReviveTarget==null&&retry.Rescuer==null&&retry.ReviveElapsed==0,"旧救援状态泄露");
Check(layout==string.Join(';',retry.Floors.SelectMany(f=>f.Rooms).Select(r=>$"{r.Id}:{r.X}:{r.Y}:{r.Boss}")),"全灭改变结构");
Check(retry.Floors.SelectMany(f=>f.Rooms).Count(r=>r.Revealed)==1,"旧探索未重置");
Check(retry.Floors.SelectMany(f=>f.Rooms).All(r=>r.Monsters.Count==0&&r.Event==""&&!r.EventUsed&&r.SnapshotLevel==0),"旧房间内容未重置");
Check(retry.Floors.SelectMany(f=>f.Passages).All(p=>!p.Revealed),"旧走廊未重置");
for(int i=0;i<2000&&retry.CurrentRoom==0;i++)retry.Tick(0.05f);
Check(retry.CurrentRoom!=0&&retry.Floor.Rooms[retry.CurrentRoom].SnapshotLevel==retry.Heroes[0].Level,"重挑战未重新按成长刷怪");
foreach(var h in retry.Heroes)h.Hp=0;retry.Tick(0.05f);Check(retry.Retries==2,"无法连续重挑战");
Console.WriteLine("PASS: 100 seeds, topology, deferred spawn, growth, victory, rescue movement/channel/multiple deaths, combat gating, wipe retry and resource retention");
