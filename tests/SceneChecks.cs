using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SceneChecks
{
    public static void Run(string root)
    {
        void Check(bool value,string message){if(!value)throw new Exception("SCENE_CHECK: "+message);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var floor=Dungeon.Generate(42,c)[0];
        Check(floor.Rooms.Select(r=>(r.HalfWidth,r.HalfHeight)).Distinct().Count()>1,"房间大小没有变化");
        foreach(var room in floor.Rooms)foreach(int n in room.Neighbors)
        {
            var door=room.DoorTowards(floor.Rooms[n]);var relative=door-room.Center;
            Check(Math.Abs(relative.X)==room.HalfWidth||Math.Abs(relative.Y)==room.HalfHeight,"门不在房间边缘");
        }
        Vector2 from=new(.25f,.5f),to=new(.7f,.25f);
        var path=new[]{from}.Concat(WorldNavigation.FindPath(c,from,to)).ToArray();
        for(int i=1;i<path.Length;i++)for(int j=0;j<=20;j++)
            Check(WorldNavigation.Walkable(c,Vector2.Lerp(path[i-1],path[i],j/20f)),"路径穿过障碍");
        Check(path.Length>2&&path[^1]==to,"绕行路径未到目标");
        var game=GameSession.CreateWorld(c,77,new[]{"warrior","ranger","mage"});var w=game.World!;
        var oldSave=SaveStore.Decode(c,SaveStore.Encode(new SaveDocument {Session=game.Capture()}));
        oldSave.Session!.World!.NavigationVersion=0;oldSave.Session.World.Route=new(){to};
        var oldRestored=GameSession.Restore(c,oldSave.Session);
        Check(oldRestored.World!.Route.Count>1&&oldRestored.World.Route.Last()==to,"旧版路径没有迁移到避障网格");
        Check(!game.RequestCastleChallenge("c1"),"未解锁城堡可点击挑战");
        for(int i=0;i<3000&&w.Stage!=CampaignStage.Dungeon;i++)game.Tick(.05f);
        foreach(var region in c.Rulers.Where(p=>p.Value=="c1"))w.Progress.Explored.Add(region.Key);
        foreach(var d in c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]=="c1"))w.Progress.Cleared.Add(d.Id);
        Check(!ExpeditionPlanner.Select(c,w.Progress,1,false).Dungeon.Castle,"未点击就自动挑战城堡");
        string current=w.Target.Dungeon.Id;Check(game.RequestCastleChallenge("c1")&&w.PendingCastle=="c1"&&w.Target.Dungeon.Id==current&&w.Stage==CampaignStage.Dungeon,"战斗中城堡请求跳过当前地牢");
        string json=SaveStore.Encode(new SaveDocument {Session=game.Capture()});var resumed=GameSession.Restore(c,SaveStore.Decode(c,json).Session!);
        Check(resumed.World!.PendingCastle=="c1","排队城堡请求未保存");
        foreach(var h in resumed.Heroes){h.Hp=h.MaxHp=100000;h.Attack=100000;}
        for(int i=0;i<30000&&resumed.World.Trades==0;i++)resumed.Tick(.05f);
        Check(resumed.World.Trades==1&&resumed.World.Target.Dungeon.Id=="c1"&&resumed.World.Stage==CampaignStage.TravelDungeon,"交易后未执行排队城堡挑战");
        var migrated=SaveStore.Decode(c,json);migrated.Session!.World!.NavigationVersion=0;
        GameSession.Restore(c,migrated.Session);
        Console.WriteLine("PASS: variable room geometry, door positions, obstacle routing, manual castle gate, queued challenge persistence and post-trade dispatch");
    }
}
