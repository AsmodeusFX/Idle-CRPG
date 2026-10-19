using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class Act1Checks
{
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        void Check(bool ok,string why){if(!ok)throw new Exception("ACT1_CHECK: "+why);}
        Check(c.WorldWidth==8&&c.WorldHeight==8&&c.Regions.Count(r=>r.Enterable)==50&&c.Regions.Count(r=>!r.Enterable)==14,"网格数量");
        Check(c.Dungeons.Count==41&&c.Dungeons.Count(d=>d.Castle)==8&&c.Gates.Count==9,"POI或关口数量");
        Check(c.Villages.Count==1&&c.StartRegionId==36&&c.IsTown(36),"单一营地");
        var game=GameSession.CreateWorld(c,123,new[]{"warrior","ranger","mage"});
        Check(game.World!.CurrentRegion==36,"营地出生");
        Check(!game.World.TownReturnPortalVisible&&c.TownPortal!=c.TownWaypoint,"首次进城出现回城门或两处传送位置重合");
        // 固定场景共用局部导航/绘制配置；服务阶段必须保存，售卖后读档不重复到账。
        Check(c.ChapterMaps.Select(m=>m.Town?.Npcs.Count).SequenceEqual(new int?[]{6,7,7,4,5}),"各章固定主城NPC数量与合并职能不符");
        var town=c.Town!;Check(!town.Walkable(new(1,10))&&!town.Walkable(new(23,5))&&town.Walkable(town.Exit),"围栏存在非出口通路");
        var townSave=game.CaptureProgress();townSave.World!.TownPhase=TownVisitPhase.Heal;
        foreach(var h in townSave.Heroes){h.Hp=h.MaxHp/2;h.Position=town.Entry;}
        var junk=c.Items.First(i=>c.ItemTypes.Single(t=>t.Id==i.TypeId).Kind=="junk");townSave.Inventory.Add(new(junk.Id,5,3));
        GameSession RestoreTown(SessionSnapshot snapshot)=>GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=snapshot})).Session!);
        var visit=RestoreTown(townSave);
        // 按需服务和 GM 例外均复用正式回城阶段；不允许偷偷恢复或卖掉物品。
        var healthy=RestoreTown(townSave);
        foreach(var h in healthy.Heroes)h.Hp=h.MaxHp;
        healthy.Tick(.05f);
        Check(healthy.World!.TownPhase==TownVisitPhase.Sell&&healthy.Inventory.Count>0,"满血携带杂物未跳过治疗");
        var empty=game.CaptureProgress();empty.World!.TownPhase=TownVisitPhase.Heal;
        var noServices=RestoreTown(empty);noServices.Tick(.05f);
        Check(noServices.World!.TownPhase==TownVisitPhase.Exit,"满血空背包未跳过两项服务");
        var injured=RestoreTown(empty);injured.Heroes[0].Hp/=2;injured.Tick(.05f);
        Check(injured.World!.TownPhase==TownVisitPhase.Heal,"受伤空背包错误跳过治疗");
        for(int i=0;i<5000&&injured.World.TownPhase==TownVisitPhase.Heal;i++)injured.Tick(.05f);
        injured.Tick(.05f);Check(injured.World.TownPhase==TownVisitPhase.Exit,"治疗后空背包未跳过售卖");
        var bypass=RestoreTown(townSave);bypass.Tick(.05f);
        bypass.SetGmBattle(new(SkipSupplies:true));bypass.Tick(.05f);
        Check(bypass.World!.TownPhase==TownVisitPhase.Exit&&bypass.Heroes[0].Hp==bypass.Heroes[0].MaxHp/2&&bypass.Inventory.Values.Sum()==3&&bypass.Gold==0,"跳过补给没有中止寻路或伪造了补给");
        var reloaded=GameSession.Restore(c,bypass.CaptureProgress());Check(!reloaded.Gm.SkipSupplies,"跳过补给开关被存档");
        for(int i=0;i<5000&&bypass.World.Stage==CampaignStage.Village;i++)bypass.Tick(.05f);
        Check(bypass.World.Stage==CampaignStage.TravelDungeon&&bypass.Inventory.Values.Sum()==3&&bypass.Gold==0,"跳过补给未继续出发或售卖了物品");
        for(int i=0;i<5000&&visit.World!.TownPhase==TownVisitPhase.Heal;i++)
        {visit.Tick(.05f);Check(visit.Heroes.All(h=>town.Walkable(h.Position)),"营地移动穿过建筑或围栏");}
        Check(visit.World!.TownPhase==TownVisitPhase.Sell&&visit.Heroes.All(h=>h.Hp==h.MaxHp&&Vector2.Distance(h.Position,town.Npc("heal").Position)<=town.InteractionRange)&&visit.Inventory.Values.Sum()==3&&visit.Gold==0,"未先走近治疗或治疗时提前售卖");
        visit=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=visit.CaptureProgress()})).Session!);
        for(int i=0;i<5000&&visit.World!.TownPhase!=TownVisitPhase.Exit;i++)visit.Tick(.05f);
        Check(visit.World!.TownPhase==TownVisitPhase.Exit&&visit.Gold==15&&visit.Inventory.Count==0&&visit.World.Trades==1&&visit.Heroes.All(h=>Vector2.Distance(h.Position,town.Npc("sell").Position)<=town.InteractionRange),"未走近铁匠或售卖结果不正确");
        visit=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=visit.CaptureProgress()})).Session!);
        for(int i=0;i<5000&&visit.World!.Stage==CampaignStage.Village;i++)visit.Tick(.05f);
        Check(visit.World!.Stage==CampaignStage.TravelDungeon&&visit.Gold==15&&visit.World.Trades==1&&visit.Heroes.All(h=>Vector2.Distance(h.Position,town.Exit)<=town.InteractionRange),"未通过唯一出口或读档重复售卖");
        Check(c.FloorPlans.Count==207&&c.PoiChallenges.Count==123,"逐层和难度数据缺失");
        var ordering=new WorldProgress();ordering.Cleared.UnionWith(new[]{"A01-C01","A02-C01"});var opened=ordering.AccessibleRegions(c);
        var equalGroup=c.Dungeons.Where(d=>!d.Castle&&opened.Contains(d.RegionId)).GroupBy(d=>c.Challenge(d.Id,0).Challenge).OrderBy(g=>g.Key).First(g=>g.Count()>1);
        ordering.Cleared.UnionWith(c.Dungeons.Where(d=>!d.Castle&&c.Challenge(d.Id,0).Challenge<equalGroup.Key).Select(d=>d.Id));var choices=new System.Collections.Generic.HashSet<string>();
        var selectionRandom=new PersistentRandom(123731u);
        for(int i=0;i<40;i++){var pick=ExpeditionPlanner.Select(c,ordering,36,false,0,selectionRandom);Check(c.Challenge(pick.Dungeon.Id,0).Challenge==equalGroup.Key,"未优先选择全部主支线的最低挑战等级");choices.Add(pick.Dungeon.Id);}
        Check(choices.Count>1,"同挑战等级没有随机选择");
        var portalGame=GameSession.CreateWorld(c,731,new[]{"warrior","ranger","mage"});var wp=c.Waypoints.Single(w=>w.Castle=="A01-C01");
        Check(c.Waypoints.Count==8&&!portalGame.World!.Progress.WaypointUnlocked(c,wp),"传送点数量或初始解锁");
        portalGame.World!.Progress.Cleared.UnionWith(c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]=="A01-C01").Select(d=>d.Id));
        Check(portalGame.World.Progress.WaypointUnlocked(c,wp),"地块全清未解锁传送点");
        var extra=c.Dungeons.First(d=>d.RegionId==wp.Region) with {Id="qa_extra"};c.Dungeons.Add(extra);Check(!portalGame.World.Progress.WaypointUnlocked(c,wp),"多地牢地块未全清却解锁");c.Dungeons.Remove(extra);
        var seenPortals=new System.Collections.Generic.HashSet<string>();
        portalGame.SetGmBattle(new(InstantDungeonClear:true));bool returned=false;
        for(int i=0;i<20000&&!returned;i++)
        {
            portalGame.Tick(.05f);var w=portalGame.World!;
            if(w.PortalPhase!=PortalPhase.None)
            {
                string phase=(w.PortalReturning?"return_":"out_")+w.PortalPhase;
                if(seenPortals.Add(phase))
                {
                    var round=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=portalGame.CaptureProgress()})).Session!);
                    Check(round.World!.PortalPhase==w.PortalPhase&&round.World.PortalReturning==w.PortalReturning,"传送动画阶段读档丢失");portalGame=round;portalGame.SetGmBattle(new(InstantDungeonClear:true));
                }
            }
            returned=seenPortals.Contains("return_Arriving")&&w.PortalPhase==PortalPhase.None;
        }
        Check(returned&&seenPortals.Count==6&&portalGame.World!.Stage==CampaignStage.Village&&portalGame.World.TownReturnPortalVisible&&portalGame.Heroes.All(h=>Vector2.Distance(h.Position,c.TownPortal)<.5f),"往返传送未经历三阶段或未抵达城镇传送点");
        var portalRestored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=portalGame.CaptureProgress()})).Session!);Check(portalRestored.World!.TownReturnPortalVisible,"首次回城后常驻传送门未保存");
        Check(c.FloorPlans.Where(p=>p.Difficulty==0).Sum(p=>p.TargetKills)==2517&&c.FloorPlans.Where(p=>p.Difficulty==1).Sum(p=>p.TargetKills)==2601&&c.FloorPlans.Where(p=>p.Difficulty==2).Sum(p=>p.TargetKills)==3439,"V0.5规划预算汇总");
        Check(Config.KillBudget(5,3,1,1,2,0,0)==18,"预算算例");
        var progress=new WorldProgress();Check(!progress.AreaPartUnlocked(c,"ap2"),"提前开放");progress.Cleared.Add("A01-C01");progress.Cleared.Add("A02-C01");
        Check(progress.AreaPartUnlocked(c,"ap3")&&progress.AreaPartUnlocked(c,"ap4"),"双分支解锁");
        Check(!c.CanCross(20,21)&&c.CanCross(12,13),"非Gate跨区边界许可");
        c.RegionPrerequisites.Add(new(4,"A01-C01"));int index=c.AreaParts.FindIndex(a=>a.Id=="ap4");
        var either=new WorldProgress();either.Cleared.Add("A01-C01");Check(either.AreaPartUnlocked(c,"ap4"),"OR第一项单独满足未解锁");either.Cleared.Clear();either.Cleared.Add("A02-C01");Check(either.AreaPartUnlocked(c,"ap4"),"OR第二项单独满足未解锁");either.Cleared.Clear();Check(!either.AreaPartUnlocked(c,"ap4"),"OR无条件满足却解锁");
        c.AreaParts[index]=c.AreaParts[index] with {UnlockMode="all"};
        var one=new WorldProgress();one.Cleared.Add("A01-C01");Check(!one.AreaPartUnlocked(c,"ap4"),"AND被当作OR");one.Cleared.Add("A02-C01");Check(one.AreaPartUnlocked(c,"ap4"),"AND全部条件满足仍未开放");
        c.ValidateWorld();
        c.AreaParts[index]=c.AreaParts[index] with {UnlockMode="invalid"};bool invalidMode=false;try{c.ValidateWorld();}catch(InvalidDataException){invalidMode=true;}Check(invalidMode,"非法unlock_mode未拒绝");
        c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        GameSession Copy(GameSession g)=>GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
        GameSession Trade(GameSession g)
        {
            var s=g.CaptureProgress();var map=g.Config;var w=s.World!;var v=map.ReturnVillage(map.StartRegionId);var r=map.Regions.Single(r=>r.Id==v.RegionId);var npc=map.Npcs.Single(n=>n.Id==v.NpcId);
            w.Stage=CampaignStage.Village;w.PortalPhase=PortalPhase.None;w.PortalElapsed=0;w.PortalWaypoint="";w.Position=new Vector2(r.X+v.X,r.Y+v.Y);w.VillageId=v.Id;w.Route.Clear();w.VillagePaths.Clear();w.TownPhase=TownVisitPhase.Sell;w.TradeElapsed=(map.Town?.ServiceSeconds??map.TradeSeconds)-.025f;s.Floors.Clear();s.ActiveDungeonId="";s.SkillState=new();
            foreach(var h in s.Heroes){h.Position=map.Town?.Npc("sell").Position??npc.Position;h.Hp=h.MaxHp;}
            var result=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=s})).Session!);for(int i=0;i<3000&&result.World!.Stage==CampaignStage.Village;i++)result.Tick(.05f);return result;
        }
        void Enter(GameSession g,string id)
        {
            for(int i=0;i<30000&&g.World!.Stage!=CampaignStage.Dungeon;i++)g.Tick(.05f);
            Check(g.World!.Stage==CampaignStage.Dungeon&&g.World.Target.Dungeon.Id==id,"未进入预期挑战："+id);
        }
        void Wipe(GameSession g){foreach(var h in g.Heroes)h.Hp=0;g.Tick(.05f);Check(g.World!.Stage==CampaignStage.Village,"全灭未返村");}
        var retry=GameSession.CreateWorld(c,12,new[]{"warrior","ranger","mage"});retry.World!.Progress.Cleared.UnionWith(c.Dungeons.Where(d=>!d.Castle&&c.Rulers[d.RegionId]=="A01-C01").Select(d=>d.Id));
        Check(retry.RequestCastleChallenge("A01-C01"),"手动要塞请求");Enter(retry,"A01-C01");Wipe(retry);retry=Trade(retry);Enter(retry,"A01-C01");Wipe(retry);retry=Trade(retry);
        Check(retry.World!.Target.Farming&&c.Challenge(retry.World.Target.Dungeon.Id,0).Challenge==1&&retry.World.IsCastleRetry("A01-C01"),"2次失败未匹配等级回刷/刷新要塞重试");
        retry=Copy(retry);foreach(var h in retry.Heroes)h.Level=2;retry=Trade(retry);
        Check(!retry.World!.Target.Dungeon.Castle&&c.Challenge(retry.World.Target.Dungeon.Id,0).Challenge==2,"升级后自动重试了要塞或未选择最高匹配复刷");
        Check(retry.RequestCastleChallenge("A01-C01")&&!retry.World.IsCastleRetry("A01-C01"),"再次挑战无法由玩家授权");
        var normal=GameSession.CreateWorld(c,13,new[]{"warrior","ranger","mage"});normal.World!.Progress.Cleared.UnionWith(c.Dungeons.Where(d=>c.Challenge(d.Id,0).Challenge<7).Select(d=>d.Id));
        normal=Trade(normal);Check(normal.World!.Target.Dungeon.Id=="A04-D04","普通地牢全局ChallengeLevel排序");Enter(normal,"A04-D04");Wipe(normal);normal=Trade(normal);Enter(normal,"A04-D04");Wipe(normal);normal=Trade(normal);
        Check(normal.World!.Target.Farming,"普通地牢失败未回刷");foreach(var h in normal.Heroes)h.Level=2;normal=Trade(normal);Check(normal.World!.Target.Dungeon.Id=="A04-D04"&&!normal.World.Target.Farming,"小队等级变化未重试失败普通地牢");
        // 实际运行完整主线：测试只授权要塞，所有首通来自战斗与出口，绝不按规划预算结算。
        var run=GameSession.CreateWorld(c,71,new[]{"warrior","ranger","mage"});foreach(var h in run.Heroes){h.Hp=h.MaxHp=1000000;h.Attack=100000;}
        run.SetGmBattle(new(Invincible:true));int andarielSpawns=0;var seen=new System.Collections.Generic.HashSet<Actor>();
        for(int tick=0;tick<800000&&run.LatestMapIndex==0;tick++)
        {
            if(tick%50==0)
            {
                var ready=c.Dungeons.FirstOrDefault(d=>d.Castle&&!c.Optional(d.Id)&&!run.World!.Progress.Cleared.Contains(d.Id)&&run.World.Progress.CastleUnlocked(c,d.Id));
                if(ready!=null)run.RequestCastleChallenge(ready.Id);
            }
            run.Tick(.05f);
            if(run.ActiveDungeon?.Id=="A08-C01")foreach(var monster in run.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters).Where(m=>m.Name.Contains("安达利尔")))if(seen.Add(monster))andarielSpawns++;
            Check(!run.World!.Progress.Cleared.Contains("A03-C01"),"支线被自动攻略");
        }
        Check(run.LatestMapIndex==1&&run.ChapterIndex==1&&run.MapProgress(0,0).Cleared.Count==40&&andarielSpawns==1,"主线真实通关或唯一安达利尔/支线非阻断失败");
        Check(run.RequestBranchChallenge(0,0,"A03-C01"),"旧章支线不能授权");run=Trade(run);Check(run.ActiveBranch!=null&&run.ChapterIndex==0&&run.LatestMapIndex==1,"支线未传送到原章");
        var oldBranch=run.CaptureProgress();oldBranch.SuspendedWorld!.TownLayoutVersion=0;
        oldBranch.SuspendedWorld.VillagePaths[run.Heroes[0].SpecId]=new(){new(23,10),new(18,5)};
        var branchPositions=run.Heroes.Select(h=>h.Position).ToArray();
        run=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=oldBranch})).Session!);
        Check(run.CaptureProgress().SuspendedWorld!.TownLayoutVersion==1&&run.CaptureProgress().SuspendedWorld!.VillagePaths.Count==0&&run.Heroes.Select(h=>h.Position).SequenceEqual(branchPositions),"暂挂主线布局迁移丢失当前支线位置");
        run.SetGmBattle(new(Invincible:true));
        for(int i=0;i<200000&&run.ActiveBranch!=null;i++)run.Tick(.05f);
        Check(run.ActiveBranch==null&&run.ChapterIndex==1&&run.LatestMapIndex==1&&run.MapProgress(0,0).Cleared.Contains("A03-C01"),"支线未真实完成并返回最新章");
        var recovered=Copy(run);Check(recovered.MapProgress(0,0).Cleared.Count==41,"跨章支线进度读档丢失");
        var store=new SaveStore(Path.Combine(root,"artifacts/qa/act1-reset/progress.json"));store.Reset();File.WriteAllText(store.PathName,"{bad");File.WriteAllText(store.PathName+".bak","{bad");Check(store.Load(c)?.Session==null&&store.RecoveryReset&&store.LastWarning!=null,"损坏主备份未提示自动重置");store.Reset();
        Console.WriteLine("PASS: production Act1 8x8 topology, 41 POIs, 8 gates and single central town");
        Console.WriteLine("PASS: V0.5 207 floor budgets, AND/OR, manual castles, 2-failure recovery, level-based retry, actual 40-POI main plus optional dungeon clear, cross-chapter branch return, unique Andariel, town heal/sell/exit, portal stages and reset notice");
    }
}
