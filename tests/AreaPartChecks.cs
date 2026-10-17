using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class AreaPartChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("AREAPART_CHECK: "+message);}
        Config Load()=>Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var c=Load();var initial=c.AreaPart(c.StartRegionId);var p=new WorldProgress();
        Check(c.AreaParts.Count==35&&c.Dungeons.Single(d=>d.Id==initial.CastleId).RegionId==c.StartRegionId,"自然城堡布局数量或初始城堡错误");
        Check(c.AreaParts.All(a=>c.Dungeons.Count(d=>!d.Castle&&c.Rulers[d.RegionId]==a.CastleId) is >=3 and <=10),"每统治区普通地牢数量不在 3–10");
        Check(c.AreaParts.Count(a=>{var cells=c.Regions.Where(r=>c.Rulers[r.Id]==a.CastleId).ToArray();return (cells.Max(r=>r.X)-cells.Min(r=>r.X)+1)*(cells.Max(r=>r.Y)-cells.Min(r=>r.Y)+1)>cells.Length;})>17,"统治区域仍过于方正");
        Check(c.AreaParts.Count(a=>p.AreaPartUnlocked(c,a.Id))==1,"初始开放了多个统治区");
        var multi=c.AreaPartPrerequisites.GroupBy(e=>e.AreaPartId).First(g=>g.Count()>1).ToArray();
        foreach(var edge in multi)
        {
            var progress=new WorldProgress();progress.Cleared.Add(c.AreaParts.Single(a=>a.Id==edge.RequiredAreaPartId).CastleId);
            Check(progress.AreaPartUnlocked(c,edge.AreaPartId),"多选前置没有按 OR 判定");
        }
        foreach(var d in c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]==initial.CastleId))p.Cleared.Add(d.Id);
        Check(c.AreaParts.Count(a=>p.AreaPartUnlocked(c,a.Id))==1,"橙色状态提前解锁邻区");
        for(int i=0;i<4;i++)Check(ExpeditionPlanner.Select(c,p,1,i%2==0).Farming,"等待手动攻城时越权开拓");
        p.Cleared.Add(initial.CastleId);
        var expected=c.AreaPartPrerequisites.Where(e=>e.RequiredAreaPartId==initial.Id).Select(e=>e.AreaPartId).Append(initial.Id).ToHashSet();
        Check(c.AreaParts.Where(a=>p.AreaPartUnlocked(c,a.Id)).Select(a=>a.Id).ToHashSet().SetEquals(expected),"通关没有精确解锁相邻配置统治区");
        Check(expected.SetEquals(new[]{"ap1","ap2","ap3","ap4"}),"初始区没有按示意开放 2、3、4 三条分支");
        var locked=c.AreaParts.First(a=>!p.AreaPartUnlocked(c,a.Id));var target=c.Dungeons.First(d=>d.Castle&&d.Id==locked.CastleId);var r=c.Regions.Single(r=>r.Id==target.RegionId);
        bool refused=false;try{WorldNavigation.FindPath(c,new Vector2(.25f,.5f),new Vector2(r.X+target.X,r.Y+target.Y),allowedRegions:p.AccessibleRegions(c));}catch(InvalidOperationException){refused=true;}
        Check(refused,"寻路允许进入锁定统治区");
        c.FloorsMin=c.FloorsMax=1;c.RoomsMin=c.RoomsMax=3;
        var game=GameSession.CreateWorld(c,31,new[]{"warrior","ranger","mage"});var world=game.World!;
        foreach(var h in game.Heroes){h.Hp=h.MaxHp=1000000;h.Attack=100000;}
        for(int i=0;i<100000&&!world.Progress.CastleUnlocked(c,initial.CastleId);i++)game.Tick(.05f);
        Check(world.Progress.CastleUnlocked(c,initial.CastleId),"没有扫荡完整初始统治区");
        int visits=world.DungeonVisits;long gold=game.Gold;
        for(int i=0;i<10000;i++)
        {
            game.Tick(.05f);
            Check(c.AreaPart(world.CurrentRegion).Id==initial.Id&&c.AreaPart(world.Target.Dungeon.RegionId).Id==initial.Id&&!world.Target.Dungeon.Castle,"未点击城堡就推进到新统治区");
        }
        Check(world.DungeonVisits>visits&&game.Gold>gold,"等待攻城时停止挂机积累");
        Check(game.RequestCastleChallenge(initial.CastleId),"无法主动挑战初始城堡");
        for(int i=0;i<100000&&!world.Progress.Cleared.Contains(initial.CastleId);i++)game.Tick(.05f);
        Check(world.Progress.Cleared.Contains(initial.CastleId)&&world.Stage==CampaignStage.TravelVillage,"攻城后没有先返村");
        Check(c.AreaParts.Where(a=>world.Progress.AreaPartUnlocked(c,a.Id)).Select(a=>a.Id).ToHashSet().SetEquals(expected),"实际攻城没有解锁对应前置");
        var legacy=game.CaptureProgress();legacy.World!.AreaPartAccessVersion=0;legacy.World.Position=new Vector2(r.X+target.X,r.Y+target.Y);legacy.World.Explored.Add(r.Id);
        var migrated=GameSession.Restore(c,legacy);
        Check(migrated.Gold==game.Gold&&migrated.Heroes[0].Level==game.Heroes[0].Level&&migrated.World!.Stage==CampaignStage.Village&&migrated.World.CurrentRegion==c.StartRegionId,"旧档迁移丢资源或继续越界");
        var oldLayout=game.CaptureProgress();oldLayout.World!.LayoutVersion=0;oldLayout.World.AreaPartAccessVersion=0;oldLayout.World.Cleared.Add("d1_2");oldLayout.World.TargetId="d1_2";oldLayout.ActiveDungeonId="d1_2";
        var layoutCopy=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new SaveDocument {Session=oldLayout})).Session!);
        Check(layoutCopy.Gold==game.Gold&&layoutCopy.Heroes[0].Level==game.Heroes[0].Level&&layoutCopy.World!.Stage==CampaignStage.Village,"自然布局迁移丢失成长/资源");
        void Reject(Action<Config> change,string message)
        {
            var bad=Load();change(bad);bool rejected=false;try{bad.ValidateWorld();}catch(InvalidDataException){rejected=true;}Check(rejected,message);
        }
        Reject(b=>b.AreaPartPrerequisites.Add(new(initial.Id,initial.Id)),"未拒绝自引用");
        Reject(b=>b.AreaPartPrerequisites.Add(new("missing",initial.Id)),"未拒绝失效引用");
        Reject(b=>b.AreaPartPrerequisites.Add(new(locked.Id,initial.Id)),"未拒绝非接壤前置");
        Reject(b=>b.AreaPartPrerequisites.Add(b.AreaPartPrerequisites[0]),"未拒绝重复边");
        Reject(b=>b.AreaPartPrerequisites.RemoveAll(e=>e.AreaPartId==locked.Id),"未拒绝孤立前置链");
        Console.WriteLine("PASS: areapart OR prerequisites, manual castle progression gate, continuous farming, route restrictions, legacy migration and CSV validation");
    }
}
