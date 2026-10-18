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
        Check(c.WorldWidth==8&&c.WorldHeight==8&&c.Regions.Count(r=>r.Enterable)==49&&c.Regions.Count(r=>!r.Enterable)==15,"网格数量");
        Check(c.Dungeons.Count==41&&c.Dungeons.Count(d=>d.Castle)==8&&c.Gates.Count==8,"POI或关口数量");
        Check(c.Villages.Count==1&&c.StartRegionId==36&&c.IsTown(36),"单一营地");
        var game=GameSession.CreateWorld(c,123,new[]{"warrior","ranger","mage"});
        Check(game.World!.CurrentRegion==36,"营地出生");
        Check(c.FloorPlans.Count==207&&c.PoiChallenges.Count==123,"逐层和难度数据缺失");
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
            w.Stage=CampaignStage.Village;w.Position=new Vector2(r.X+v.X,r.Y+v.Y)+npc.Position/map.WorldUnits;w.VillageId=v.Id;w.Route.Clear();w.VillagePaths.Clear();w.TradeElapsed=map.TradeSeconds-.025f;s.Floors.Clear();s.ActiveDungeonId="";s.SkillState=new();
            foreach(var h in s.Heroes){h.Position=npc.Position;h.Hp=h.MaxHp;}
            var result=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=s})).Session!);result.Tick(.05f);return result;
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
        var normal=GameSession.CreateWorld(c,13,new[]{"warrior","ranger","mage"});normal.World!.Progress.Cleared.UnionWith(c.Dungeons.Where(d=>!c.Optional(d.Id)&&c.Challenge(d.Id,0).Challenge<7).Select(d=>d.Id));
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
        Check(run.LatestMapIndex==1&&run.ChapterIndex==1&&run.MapProgress(0,0).Cleared.Count==37&&andarielSpawns==1,"主线真实通关或唯一安达利尔/支线非阻断失败");
        Check(run.RequestBranchChallenge(0,0,"A03-C01"),"旧章支线不能授权");run=Trade(run);Check(run.ActiveBranch!=null&&run.ChapterIndex==0&&run.LatestMapIndex==1,"支线未传送到原章");
        run=Copy(run);run.SetGmBattle(new(Invincible:true));
        for(int i=0;i<200000&&run.ActiveBranch!=null;i++)run.Tick(.05f);
        Check(run.ActiveBranch==null&&run.ChapterIndex==1&&run.LatestMapIndex==1&&run.MapProgress(0,0).Cleared.Contains("A03-C01"),"支线未真实完成并返回最新章");
        var recovered=Copy(run);Check(recovered.MapProgress(0,0).Cleared.Count==41,"跨章支线进度读档丢失");
        var store=new SaveStore(Path.Combine(root,"artifacts/qa/act1-reset/progress.json"));store.Reset();File.WriteAllText(store.PathName,"{bad");File.WriteAllText(store.PathName+".bak","{bad");Check(store.Load(c)?.Session==null&&store.RecoveryReset&&store.LastWarning!=null,"损坏主备份未提示自动重置");store.Reset();
        Console.WriteLine("PASS: production Act1 8x8 topology, 41 POIs, 8 gates and single central town");
        Console.WriteLine("PASS: V0.5 207 floor budgets, AND/OR, manual castles, 2-failure recovery, level-based retry, actual 37-POI main clear, cross-chapter branch return, unique Andariel and reset notice");
    }
}
