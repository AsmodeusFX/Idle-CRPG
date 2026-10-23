using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleCrpg.Core;

/// <summary>仅测试使用的不可变场景；未知字段和版本拒绝加载，所有玩家动作默认关闭。</summary>
public sealed record SimulationConfig
{
 public bool PartyOnlyStars {get;init;} public int SchemaVersion {get;init;}=1;
 public string ScenarioId {get;init;}="custom";
 public int Seed {get;init;}=20261023;
 public int DurationMinutes {get;init;}=240;
 public int SampleIntervalMinutes {get;init;}=30;
 public int ActionCheckIntervalSeconds {get;init;}=60;
 public int CastleRetryCooldownSeconds {get;init;}=60;
 public Actions PlayerActions {get;init;}=new();
 public sealed record Actions {
 public bool AutoCastleChallenge {get;init;}
 public bool AutoHeroGrowth {get;init;}
 public bool AutoStarmapPurchase {get;init;}
 public bool AutoPartyManagement {get;init;}
 }
 public static SimulationConfig Load(string path) {
 var c=JsonSerializer.Deserialize<SimulationConfig>(File.ReadAllText(path).TrimStart('\uFEFF'),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new ArgumentException("空配置");
 if(c.SchemaVersion!=1||c.DurationMinutes<=0||c.SampleIntervalMinutes<=0||c.ActionCheckIntervalSeconds<=0||c.CastleRetryCooldownSeconds<=0||c.PlayerActions==null||string.IsNullOrWhiteSpace(c.ScenarioId)||c.ScenarioId.Any(ch=>!char.IsLetterOrDigit(ch)&&ch!='_'))throw new ArgumentException("非法模拟配置/未知版本");return c;
 }
}
/// <summary>只推进正式会话和合法玩家命令；时间完全来自完成的20Hz Tick，不写存档。</summary>
public static class SimulationRunner {
 public static void Run(string root,string path) {
 var c=SimulationConfig.Load(path);var game=GameSession.CreateWorld(LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config")),c.Seed,new[]{"warrior"});
 var dir=Path.Combine(root,"artifacts/simulation",c.ScenarioId+"_"+c.Seed);Directory.CreateDirectory(dir);
 var events=new List<string>{"sim_time,event_type,target,result,reason"};var snapshots=new List<string>{"sim_time,scenario,seed,chapter,region,map,stage,heroes,gold,kills,retries,party,star_nodes"};
 string Q(object? x)=>"\""+(x?.ToString()??"unavailable").Replace("\"","\"\"")+"\"";
 long tick=0;void Event(string type,string target,string result,string reason)=>events.Add(string.Join(',',(tick/20d).ToString(System.Globalization.CultureInfo.InvariantCulture),Q(type),Q(target),Q(result),Q(reason)));
 void Sample()=>snapshots.Add(string.Join(',',tick/20,c.ScenarioId,c.Seed,game.ChapterIndex+1,game.World?.CurrentRegion,Q(game.World?.Target.Dungeon.Id),game.World?.Stage,Q(string.Join('|',game.Heroes.Select(h=>$"{h.SpecId}:{h.Level}:{h.Experience}"))),game.Gold,game.Kills,game.Retries,game.Heroes.Count,game.Stars.Capture().Ranks.Count()));
 var bought=new HashSet<string>();var learned=new Dictionary<string,List<string>>();var available=new HashSet<string>();var cleared=new HashSet<string>();string previous="";int chapter=game.ChapterIndex;
 Sample();long nextCastle=0;
 for(tick=1;tick<=c.DurationMinutes*1200L;tick++) {
 game.Tick(.05f);
 var w=game.World!;
 string state=$"{game.ChapterIndex}:{w.Target.Dungeon.Id}:{w.Stage}:{game.Retries}:"+string.Join('|',game.Heroes.Select(h=>$"{h.SpecId}:{h.Level}"));
 if(state!=previous){Event("state",w.Target.Dungeon.Id,"observed",state);previous=state;}
 if(chapter!=game.ChapterIndex){Event("chapter_unlock",(game.ChapterIndex+1).ToString(),"observed","");chapter=game.ChapterIndex;available.Clear();cleared.Clear();}
 foreach(var d in game.Config.Dungeons.Where(d=>d.Castle)) {
 if(w.Progress.Cleared.Contains(d.Id)){if(cleared.Add(d.Id))Event("castle_result",d.Id,"victory","formal_progress");continue;}
 if(w.Progress.CastleUnlocked(game.Config,d.Id)&&available.Add(d.Id))Event("castle_available",d.Id,"available","formal_gate");
 }
 if(tick%(c.ActionCheckIntervalSeconds*20L)==0) {
 var a=c.PlayerActions;var growthEvents=new List<string>();EconomySimChecks.Grow(c.ScenarioId,game,bought,learned,tick/1200d,growthEvents,a.AutoStarmapPurchase&&!c.PartyOnlyStars,a.AutoHeroGrowth,a.AutoPartyManagement); if(c.PartyOnlyStars){foreach(var id in new[]{"s_root","s_party_1","s_party_2"})if(!bought.Contains(id)&&game.BuyStar(id,out _)){bought.Add(id);Event("required_star",id,"purchased","formation_only");}}
 foreach(var e in growthEvents)Event("player_growth",e,"observed","formal_command");
 if(a.AutoCastleChallenge&&tick>=nextCastle) {
 nextCastle=tick+c.CastleRetryCooldownSeconds*20L;
 var d=game.Config.Dungeons.FirstOrDefault(d=>d.Castle&&!w.Progress.Cleared.Contains(d.Id)&&w.Progress.CastleUnlocked(game.Config,d.Id)&&!w.PendingCastles.Contains(d.Id)&&!(w.Target.Dungeon.Id==d.Id));
 if(d!=null)Event("castle_click",d.Id,game.RequestCastleChallenge(d.Id)?"accepted":"rejected","formal_command");else Event("castle_blocked","","blocked","no_eligible_castle");
 }
 }
 if(tick%(c.SampleIntervalMinutes*1200L)==0||tick==c.DurationMinutes*1200L||tick<=36000&&tick%1200==0)Sample(); if(tick%6000==0)Event("diagnostic",game.Status,"observed",SimulationDiagnostic.Describe(game));
 }
 File.WriteAllLines(Path.Combine(dir,"events.csv"),events);File.WriteAllLines(Path.Combine(dir,"snapshots.csv"),snapshots);
 File.WriteAllText(Path.Combine(dir,"summary.md"),"# "+c.ScenarioId+"\n\n```json\n"+File.ReadAllText(path)+"\n```\n\n"+string.Join("\n",snapshots)+"\n\n详细收支、有效经验归零原因、逐次死亡及挑战失败计数：unavailable；当前仅记录真实状态和重试累计，不推测原因。\n");
 Console.WriteLine("PASS simulation "+dir);
 }
}

