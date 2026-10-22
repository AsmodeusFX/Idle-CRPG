using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>跑图领队与门前先锋的隔离检查：只使用内存会话，不写玩家存档。</summary>
public static class DungeonTravelChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("TRAVEL: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        c.FloorsMin=c.FloorsMax=1;c.RoomsMin=c.RoomsMax=3;

        GameSession New(int seed,params string[] ids)
        {
            var game=new GameSession(c,seed,ids);
            foreach(var hero in game.Heroes)
            {
                hero.Hp=hero.MaxHp=100000;hero.Attack=100000;hero.Timer=999;
                hero.SkillIds=Array.Empty<string>();
            }
            return game;
        }
        (Room From,Room To,Vector2 Exit,Vector2 Entry) Route(GameSession game)
        {
            var snapshot=game.Capture();var from=game.Floor.Rooms[game.CurrentRoom];var to=game.Floor.Rooms[snapshot.Destination];
            return (from,to,from.DoorTowards(to),to.DoorTowards(from));
        }

        // 1. 高速游侠领队，慢速近战战士在门前等待后独自开门并率先进入。
        var lead=New(501,"warrior","ranger","mage");
        lead.Heroes[0].Speed=3f;lead.Heroes[1].Speed=6f;lead.Heroes[2].Speed=3f;
        lead.Tick(.05f);
        Need(lead.MovingBetweenRooms,"未进入跑图状态");
        Need(ReferenceEquals(lead.TravelLeader,lead.Heroes[1]),"没有选择实际速度最高的游侠作为领队");
        int fromRoom=lead.CurrentRoom;var route=Route(lead);
        Actor? opener=null;
        for(int i=0;i<2000&&lead.CurrentRoom==fromRoom;i++)
        {
            lead.Tick(.05f);
            if(lead.DoorOpener!=null)
            {
                opener=lead.DoorOpener;
                // 门前等待是可保存的关键流程状态：读档必须沿用同一先锋，不能重新挑选。
                var saved=lead.Capture();
                var resumed=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=saved})).Session!);
                Need(resumed.DoorOpener!=null&&resumed.DoorOpener.SpecId==opener.SpecId,"读档丢失门前开门先锋");
            }
        }
        Need(lead.CurrentRoom==route.To.Id,"目标房间没有开启");
        Need(ReferenceEquals(opener,lead.Heroes[0]),"慢速近战战士没有成为开门先锋");
        Need(Vector2.Distance(lead.Heroes[0].Position,route.Entry)<=.35f,"先锋没有率先抵达门口");
        Need(Vector2.Distance(lead.Heroes[1].Position,route.Entry)>.35f,"远程领队抢在先锋前进入房间");

        // 2. 四名远程英雄没有近战前排时，由高速领队直接开门。
        var ranged=New(502,"ranger","mage","priest");
        ranged.Heroes[0].Speed=6f;ranged.Heroes[1].Speed=3f;ranged.Heroes[2].Speed=3f;
        ranged.Tick(.05f);
        Need(ReferenceEquals(ranged.TravelLeader,ranged.Heroes[0]),"最快远程没有成为领队");
        int rangedFrom=ranged.CurrentRoom;var rangedRoute=Route(ranged);var rangedLeader=ranged.TravelLeader!;bool sawLeader=false;
        for(int i=0;i<2000&&ranged.CurrentRoom==rangedFrom;i++)
        {
            ranged.Tick(.05f);
            if(ranged.DoorOpener!=null){sawLeader=true;Need(ReferenceEquals(ranged.DoorOpener,rangedLeader),"无近战先锋时没有由领队直接开门");}
        }
        Need(sawLeader&&ranged.CurrentRoom==rangedRoute.To.Id,"无近战队伍门前停滞");
        Need(Vector2.Distance(rangedLeader.Position,rangedRoute.Entry)<=.35f,"领队没有亲自开门进入");
        Need(ranged.Heroes.Where(h=>!ReferenceEquals(h,rangedLeader)).All(h=>Vector2.Distance(h.Position,rangedRoute.Entry)>.35f),"远程队员因领队身份获得额外前排站位");

        // 3. 全远程英雄带近战骷髅时，由骷髅担任开门先锋。
        var necro=New(503,"necromancer","ranger","mage");
        necro.Heroes[0].SkillIds=new[]{"nec_raise_skeleton"};
        necro.Heroes[0].Speed=3.6f;necro.Heroes[1].Speed=6f;necro.Heroes[2].Speed=3f;
        Need(necro.Heroes.All(h=>h.Ranged),"骷髅用例前提：英雄必须全部为远程");
        int summonFrom=necro.CurrentRoom;
        for(int i=0;i<200&&!necro.Summons.Any(s=>s.Alive);i++){Need(necro.CurrentRoom==summonFrom,"骷髅生成前已进入下一房间");necro.Tick(.05f);}
        Need(necro.Summons.Any(s=>s.Alive),"脱战骷髅没有生成");
        var summonRoute=Route(necro);Actor? summonOpener=null;
        for(int i=0;i<3000&&necro.CurrentRoom==summonFrom;i++)
        {
            necro.Tick(.05f);
            if(necro.DoorOpener!=null)summonOpener=necro.DoorOpener;
        }
        Need(necro.CurrentRoom==summonRoute.To.Id,"骷髅先锋未能开门");
        Need(summonOpener!=null&&summonOpener.SummonDefinitionId=="nec_skeleton"&&ReferenceEquals(summonOpener,necro.Summons.First(s=>s.SummonDefinitionId=="nec_skeleton"&&s.Alive)),"近战骷髅没有成为开门先锋");
        Need(Vector2.Distance(summonOpener!.Position,summonRoute.Entry)<=.35f,"骷髅没有率先抵达门口");

        // 4. 多名近战时只在领队到位后选择最近的一名，不改选其他近战。
        var nearest=New(504,"warrior","priest","ranger");
        nearest.Heroes[0].Speed=0.5f;nearest.Heroes[1].Speed=3.7f;nearest.Heroes[2].Speed=6f;
        nearest.Tick(.05f);
        for(int i=0;i<2000&&nearest.Capture().Phase==0;i++)nearest.Tick(.05f);
        var nearRoute=Route(nearest);float waitOffset=c.Talents.Rule("dungeon_door_wait_offset_milli")/1000f;
        var waitPoint=nearRoute.Entry-Vector2.Normalize(nearRoute.To.Center-nearRoute.From.Center)*waitOffset;
        // 战士放在等待位，祭司落后两单位；只应选择更近的战士，且等待期间不改选。
        nearest.Heroes[0].Position=waitPoint;
        nearest.Heroes[1].Position=waitPoint-new Vector2(2,0);
        nearest.Heroes[2].Position=waitPoint;
        nearest.Tick(.05f);
        Need(ReferenceEquals(nearest.DoorOpener,nearest.Heroes[0]),"多名近战没有选择离门最近者");
        nearest.Tick(.05f);nearest.Tick(.05f);
        Need(ReferenceEquals(nearest.DoorOpener,nearest.Heroes[0]),"门前等待期间反复更换先锋");

        // 5. 领队只在有效速度变化后更新，微小变化不触发抖动。
        var speed=New(505,"warrior","ranger","mage");
        speed.Heroes[0].Speed=9f;speed.Heroes[1].Speed=4.2f;speed.Heroes[2].Speed=3.6f;
        speed.Tick(.05f);
        Need(ReferenceEquals(speed.TravelLeader,speed.Heroes[0]),"初始最快战士未成为领队");
        speed.Heroes[1].Speed=10f;speed.Tick(.05f);
        Need(ReferenceEquals(speed.TravelLeader,speed.Heroes[1]),"速度变化后领队未更新为游侠");
        speed.Heroes[1].Speed=10.001f;speed.Heroes[0].Speed=9.999f;speed.Tick(.05f);
        Need(ReferenceEquals(speed.TravelLeader,speed.Heroes[1]),"微小速度变化导致领队反复切换");
        speed.Heroes[0].Speed=10.02f;speed.Tick(.05f);
        Need(ReferenceEquals(speed.TravelLeader,speed.Heroes[0]),"有效速度变化后领队未重新选择");

        // 6. 先锋异常无法到位时超时解除等待，由领队直接开门，不会无限卡门。
        var stuck=New(506,"warrior","ranger","mage");
        stuck.Heroes[0].Speed=0f;stuck.Heroes[1].Speed=6f;stuck.Heroes[2].Speed=3f;
        stuck.Tick(.05f);
        int stuckFrom=stuck.CurrentRoom;var stuckRoute=Route(stuck);bool sawPioneer=false;bool switched=false;
        for(int i=0;i<400;i++)
        {
            stuck.Tick(.05f);
            if(ReferenceEquals(stuck.DoorOpener,stuck.Heroes[0]))sawPioneer=true;
            if(stuck.CurrentRoom!=stuckFrom){switched=true;break;}
        }
        Need(sawPioneer&&switched,"先锋卡住时门前等待没有超时解除");
        Need(Vector2.Distance(stuck.Heroes[1].Position,stuckRoute.Entry)<=.35f,"超时后领队没有直接开门");

        Console.WriteLine("PASS: dynamic fastest travel leader, slow melee door pioneer, all-ranged direct open, melee summon pioneer, nearest-only wait, speed-change reselection without jitter, and stuck-pioneer timeout fallback");
    }
}
