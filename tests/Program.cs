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
int previousFloor=0; bool sawGrowth=false;
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
}
Check(s.Won,"完整地牢没有通关: "+s.Status);
Check(s.Floors.All(f=>f.Clear&&f.Rooms.All(r=>r.Revealed)),"没有清空全部房间");
Check(identities.SequenceEqual(s.Heroes),"队伍成员改变");
Check(sawGrowth&&s.Floors.SelectMany(f=>f.Rooms).Where(r=>r.Id!=0).All(r=>r.SnapshotLevel==1&&r.Monsters.Count>=1&&r.Monsters.Count<=5),"初始刷怪没有独立于英雄等级");
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
Check(retry.CurrentRoom!=0&&retry.Floor.Rooms[retry.CurrentRoom].SnapshotLevel==1,"重挑战未使用独立刷怪配置");
foreach(var h in retry.Heroes)h.Hp=0;retry.Tick(0.05f);Check(retry.Retries==2,"无法连续重挑战");
// 科技参数在首次进入时读取，不能追涨已生成怪物。
var tech=new GameSession(c,9,new[]{"warrior","ranger","mage"});foreach(var h in tech.Heroes)h.Hp=h.MaxHp=100000;
for(int i=0;i<2000&&tech.CurrentRoom==0;i++)tech.Tick(.05f);
var oldRoom=tech.Floor.Rooms[tech.CurrentRoom];var oldMonster=oldRoom.Monsters[0];float oldHp=oldMonster.MaxHp;
tech.SetSpawnModifiers(new SpawnModifiers(1,2,3,1.5f,2));
for(int i=0;i<20000&&tech.Floor.Rooms[tech.CurrentRoom]==oldRoom;i++)tech.Tick(.05f);
Check(oldMonster.MaxHp==oldHp&&oldMonster.Level==1,"科技追涨已生成怪物");
var upgraded=tech.Floor.Rooms[tech.CurrentRoom];
Check(upgraded.SnapshotLevel==4&&upgraded.Monsters.Count>=2&&upgraded.Monsters.Count<=7&&upgraded.Monsters.All(m=>m.Level==4),"独立科技增量未应用");
Check(upgraded.Monsters.All(m=>m.RewardXp>=8&&m.ValueMultiplier>=3.5f),"等级未增加经验/物品价值");
bool rejected=false;try{tech.SetSpawnModifiers(new SpawnModifiers(20,0));}catch(InvalidDataException){rejected=true;}Check(rejected,"非法刷怪区间未拒绝");
Check(c.Regions.Count==256&&c.Villages.Count==256&&c.Rulers.Count==256,"世界配置覆盖错误");
var progress=new WorldProgress();progress.Explored.Add(1);
Check(progress.Status(c,1)==RegionStatus.Sweeping&&progress.Status(c,2)==RegionStatus.Unexplored,"初始颜色错误");
string ruler=c.Rulers[1];
foreach(var d in c.Dungeons.Where(d=>d.RegionId==1&&!d.Castle))progress.Cleared.Add(d.Id);
Check(progress.Status(c,1)==RegionStatus.WaitingCastle&&!progress.CastleUnlocked(c,ruler),"本区清空就解锁城堡");
foreach(var d in c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]==ruler))progress.Cleared.Add(d.Id);
Check(progress.CastleUnlocked(c,ruler),"统治范围清空未解锁城堡");
progress.Cleared.Add(ruler);
Check(c.Rulers.Where(p=>p.Value==ruler).All(p=>progress.Status(c,p.Key)==RegionStatus.Occupied),"占领未辐射/绿色优先级错误");

// 所有邻接区已橙而城堡未解锁时，必须选择橙区最高等级复刷。
var blocked=new WorldProgress();
foreach(var r in ExpeditionPlanner.Neighbors(c,1).Select(r=>r.Id).Append(1))
{
    blocked.Explored.Add(r);foreach(var d in c.Dungeons.Where(d=>d.RegionId==r&&!d.Castle))blocked.Cleared.Add(d.Id);
}
var farm=ExpeditionPlanner.Select(c,blocked,1,false);
Check(farm.Farming&&blocked.Status(c,farm.Dungeon.RegionId)==RegionStatus.WaitingCastle&&farm.Dungeon.LevelMin==c.Dungeons.Where(d=>!d.Castle&&blocked.Status(c,d.RegionId)==RegionStatus.WaitingCastle).Max(d=>d.LevelMin),"橙区最高等级复刷错误");
Check(!ExpeditionPlanner.Select(c,blocked,1,true).Farming,"复刷后没有继续回溯开拓");
var allCleared=new WorldProgress();foreach(var d in c.Dungeons)allCleared.Cleared.Add(d.Id);foreach(var r in c.Regions)allCleared.Explored.Add(r.Id);
Check(ExpeditionPlanner.Select(c,allCleared,1,true).Farming,"世界完成后挂机停止");

// 使用小地牢、高战力完整验证世界→地牢→返村→NPC读条→售卖→再出发及城堡解锁。
var worldConfig=Config.Load(Path.Combine(root,"Idle-CRPG/config"));worldConfig.FloorsMin=worldConfig.FloorsMax=1;worldConfig.RoomsMin=worldConfig.RoomsMax=3;
var campaign=GameSession.CreateWorld(worldConfig,25,new[]{"warrior","ranger","mage"});
var w=campaign.World!;
Check(w.CurrentRegion==1&&w.Stage==CampaignStage.TravelDungeon&&w.Target.Dungeon.Id=="d1_1"&&campaign.Floors.Count==0,"未从初始村庄最低等级地牢出发");
foreach(var h in campaign.Heroes){h.MaxHp=h.Hp=1000000;h.Attack=100000;}
var team=campaign.Heroes.ToArray();bool sawVillage=false,sawLoot=false,sawTrade=false,sawCastle=false;
var lastPosition=w.Position;int oldVisits=0;var lastStage=w.Stage;
for(int tick=0;tick<1500000&&w.Progress.Cleared.Count<worldConfig.Dungeons.Count;tick++)
{
    long beforeGold=campaign.Gold;
    long pendingSale=campaign.Inventory.Sum(entry=>entry.Key.Price*entry.Value);
    if(w.Stage==CampaignStage.Village)
    {
        sawVillage=true;Check(campaign.Inventory.Count>0||sawTrade,"怪物未掉落杂物");
    }
    campaign.Tick(.05f);
    if(lastStage is CampaignStage.TravelDungeon or CampaignStage.TravelVillage)
        Check(Vector2.Distance(lastPosition,w.Position)<=worldConfig.TravelSpeed*.05f+.0001f,"世界移动瞬移");
    if(w.DungeonVisits>oldVisits)
    {
        Check(w.DungeonVisits==w.Trades+1,"跳过村庄交易进入下一地牢");
        if(w.Target.Dungeon.Castle)Check(w.Progress.CastleUnlocked(worldConfig,w.Target.Dungeon.Id),"提前挑战城堡");
        oldVisits=w.DungeonVisits;
    }
    if(campaign.Inventory.Count>0)sawLoot=true;
    if(lastStage==CampaignStage.Village&&w.Stage==CampaignStage.TravelDungeon)
    {
        sawTrade=true;Check(campaign.Inventory.Count==0&&campaign.Gold==beforeGold+pendingSale&&campaign.Heroes.All(h=>h.Hp==h.MaxHp),"交易未按锁定价格售卖/补给");
    }
    sawCastle=w.Progress.Cleared.Any(id=>worldConfig.Dungeons.Single(d=>d.Id==id).Castle);
    lastPosition=w.Position;lastStage=w.Stage;
}
Check(sawVillage&&sawLoot&&sawTrade&&sawCastle&&campaign.Gold>0,"自动远征闭环或城堡占领未完成");
Check(w.Progress.Cleared.Count==worldConfig.Dungeons.Count&&worldConfig.Regions.All(r=>w.Progress.Status(worldConfig,r.Id)==RegionStatus.Occupied),"全世界推进停滞/占领不完整");
Check(team.SequenceEqual(campaign.Heroes),"世界切换队伍被替换");
// 掉落保留：正在世界地牢中全灭，金币、背包、世界进度都不变。
for(int i=0;i<10000&&w.Stage!=CampaignStage.Dungeon;i++)campaign.Tick(.05f);
for(int i=0;i<10000&&(w.Stage!=CampaignStage.Dungeon||campaign.Inventory.Count==0);i++)campaign.Tick(.05f);
Check(w.Stage==CampaignStage.Dungeon&&campaign.Inventory.Count>0,"未构造有掉落的全灭场景");
long keptGold=campaign.Gold;var keptItems=campaign.Inventory.ToArray();int keptProgress=w.Progress.Cleared.Count,keptRetries=campaign.Retries;
foreach(var h in campaign.Heroes)h.Hp=0;campaign.Tick(.05f);
Check(campaign.Retries==keptRetries+1&&campaign.Gold==keptGold&&campaign.Inventory.SequenceEqual(keptItems)&&w.Progress.Cleared.Count==keptProgress&&w.Stage==CampaignStage.Dungeon,"全灭丢失金币/物品/世界进度");
Check(w.Target.Farming,"世界全部完成后未持续复刷");
void RejectConfig(Action<Config> mutate,string message)
{
    var invalid=Config.Load(Path.Combine(root,"Idle-CRPG/config"));mutate(invalid);bool failed=false;
    try{invalid.ValidateWorld();}catch(InvalidDataException){failed=true;}Check(failed,message);
}
RejectConfig(config=>config.Villages.RemoveAt(0),"缺村庄没有拒绝");
RejectConfig(config=>config.Rulers[1]="missing","无效城堡引用没有拒绝");
RejectConfig(config=>config.Regions[1]=config.Regions[0] with {Id=2},"重复坐标没有拒绝");
RejectConfig(config=>config.Drops[0]=config.Drops[0] with {Chance=1.5f},"非法掉率没有拒绝");
RejectConfig(config=>config.Items[0]=config.Items[0] with {TypeId="missing"},"无效物品类型没有拒绝");
Console.WriteLine("PASS: dungeon regression, revival, 256-region world configuration, spawn snapshots/modifiers, loot, travel, automatic trade, castle dominion, occupation, farming, resource retention");
