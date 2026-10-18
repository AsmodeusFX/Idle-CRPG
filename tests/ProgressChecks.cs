using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using IdleCrpg.Core;

public static class ProgressChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("PROGRESS_CHECK: "+message);}
        var c=Config.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        var g=new GameSession(c,42,new[]{"warrior","ranger","mage"});
        foreach(var h in g.Heroes){h.Hp=h.MaxHp=100000;h.Attack=100000;h.Range=100;}
        for(int i=0;i<10000;i++)
        {
            if(!g.MovingBetweenRooms&&g.CurrentRoom!=0&&g.Floor.Rooms[g.CurrentRoom].Monsters.Count(m=>m.Alive)>=2)break;
            g.Tick(.05f);
        }
        var room=g.Floor.Rooms[g.CurrentRoom];Check(room.Monsters.Count(m=>m.Alive)>=2,"未构造战斗");
        int count=room.Monsters.Count;long revision=g.Revision,gold=g.Gold;int xp=g.Experience,kills=g.Kills;
        foreach(var h in g.Heroes)h.Timer=999;
        g.Heroes[0].Timer=0;g.Tick(.05f);
        Check(room.Loot.Count==1&&room.Monsters.Any(m=>m.Alive)&&g.Gold==gold&&g.Kills==kills+1,"战斗中提前发放实物掉落或没有即时记录击杀");
        Check(g.Revision>revision&&(g.Experience!=xp||g.Heroes[0].Level>1),"击杀经验没有即时结算或触发存档");
        xp=g.Experience;kills=g.Kills;
        long beforeHit=g.Revision;
        foreach(var h in g.Heroes)h.Timer=999;
        g.Tick(.05f);Check(g.Revision==beforeHit,"普通战斗计时触发存档");
        var compact=g.CaptureProgress();
        Check(compact.Floors.All(f=>f.Rooms.All(r=>r.Monsters.Count==0)),"正式快照保留战斗现场");
        string directory=Path.Combine(root,"artifacts","progress-checks",Guid.NewGuid().ToString("N"));
        var store=new SaveStore(Path.Combine(directory,"progress.json"));store.Save(g);
        var raw=JsonSerializer.Deserialize<SaveDocument>(File.ReadAllText(store.PathName),SaveStore.JsonOptions)!;
        Check(raw.Version==4&&raw.Session!.RoomCheckpoint&&raw.Session.Floors.All(f=>f.Rooms.All(r=>r.Monsters.Count==0)),"磁盘版本错误或仍保存怪物现场");
        var resumed=GameSession.Restore(c,store.Load(c)!.Session!);room=resumed.Floor.Rooms[resumed.CurrentRoom];
        Check(room.Monsters.Count==count&&room.Monsters.All(m=>m.Hp==m.MaxHp&&m.Timer==0)&&room.Loot.Count==0,"没有从进房重开");
        Check(resumed.Gold==gold&&resumed.Experience==xp&&resumed.Kills==kills&&resumed.Heroes.All(h=>h.Timer==0),"重开丢失累计成长或保留攻击冷却");
        foreach(var h in resumed.Heroes)h.Timer=0;
        for(int i=0;i<1000&&!room.Clear;i++)resumed.Tick(.05f);
        Check(room.Clear&&room.Loot.Count==count&&room.Loot.All(l=>!l.Collected),"清房前领取奖励");
        for(int i=0;i<3000&&!room.Loot.Any(l=>l.Collected);i++)
        {
            var loot=room.Loot.First(l=>!l.Collected);bool near=resumed.Heroes.Any(h=>Vector2.Distance(h.Position,loot.Position)<=.35f);
            resumed.Tick(.05f);if(loot.Collected)Check(near,"远程拾取");
        }
        Check(room.Loot.Any(l=>l.Collected),"没有走近拾取");
        store.Save(resumed);var copy=GameSession.Restore(c,store.Load(c)!.Session!);
        void FinishLoot(GameSession game)
        {
            var r=game.Floor.Rooms[game.CurrentRoom];
            for(int i=0;i<10000&&r.Loot.Any(l=>!l.Collected);i++)game.Tick(.05f);
            Check(r.Loot.All(l=>l.Collected),"拾取停滞");
        }
        FinishLoot(resumed);FinishLoot(copy);
        Check(resumed.Gold==copy.Gold&&resumed.Kills==copy.Kills&&resumed.Experience==copy.Experience&&resumed.Heroes[0].Level==copy.Heroes[0].Level&&resumed.Inventory.OrderBy(p=>p.Key).SequenceEqual(copy.Inventory.OrderBy(p=>p.Key)),"部分拾取后读档重复或丢失奖励");
        var legacy=new SaveDocument {Version=1,Session=resumed.Capture()};
        Check(GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(legacy)).Session!).Gold==resumed.Gold,"旧版存档不兼容");
        var presentation=new MotionPresentation();var h0=resumed.Heroes[0];var original=h0.Position;
        presentation.BeforeTick(resumed);h0.Position+=new Vector2(1,0);
        Check(presentation.Position(resumed,h0,.25f)==original+new Vector2(.25f,0)&&presentation.Position(resumed,h0,.75f)==original+new Vector2(.75f,0),"逐帧插值不连续");
        Check(h0.Position==original+new Vector2(1,0),"表现修改逻辑坐标");
        Console.WriteLine("PASS: key-state saves, compact room restart, post-combat spatial loot, exactly-once rewards, legacy saves, render interpolation");
    }
}
