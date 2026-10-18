using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class TerrainGateChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("TERRAIN_GATE_CHECK: "+why);}
        Config Map()=>Config.Load(Path.Combine(root,"Idle-CRPG/config")).ChapterMaps[3];
        var c=Map();var blocked=c.Regions.Where(r=>!r.Enterable).ToArray();
        Check(blocked.Length>0&&blocked.Any(r=>r.Terrain=="mountain")&&blocked.Any(r=>r.Terrain=="ocean"),"缺少阻断地形实例");
        foreach(var r in blocked)
        {
            Check(!c.Rulers.ContainsKey(r.Id)&&!c.Dungeons.Any(d=>d.RegionId==r.Id)&&!c.Villages.Any(v=>v.RegionId==r.Id),"阻断格有游戏内容");
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)Check(!WorldNavigation.Walkable(c,r.X*8+x,r.Y*8+y),"细网格阻断失效");
        }
        var p=new WorldProgress();p.Explored.UnionWith(c.Regions.Select(r=>r.Id));p.Cleared.Add("c2");
        Check(!p.AreaPartUnlocked(c,"ap2")&&!p.AccessibleRegions(c).Contains(25),"探索/自身城堡绕过显式前置");
        p.Cleared.Clear();p.Cleared.Add("d2");Check(!p.AreaPartUnlocked(c,"ap2"),"无关地牢解锁");
        c.RegionPrerequisites.Add(new(26,"d1"));c.ValidateWorld();p.Cleared.Clear();p.Cleared.Add("d1");
        Check(p.AreaPartUnlocked(c,"ap2")&&c.Regions.Where(r=>r.Enterable&&c.Rulers[r.Id]=="c2").All(r=>p.AccessibleRegions(c).Contains(r.Id)),"普通地牢OR前置未开放整个区域");
        p.Cleared.Clear();p.Cleared.Add("c1");Check(p.AreaPartUnlocked(c,"ap2"),"要塞OR前置未开放");
        var from=new Vector2(.25f,.25f);var to=new Vector2(7.65f,5.65f);
        var path=WorldNavigation.FindPath(c,from,to,allowedRegions:p.AccessibleRegions(c));
        Check(path.All(point=>WorldNavigation.Walkable(c,point)),"寻路穿过阻断格");
        bool rejected=false;try{WorldNavigation.FindPath(c,from,new Vector2(7.5f,.5f));}catch(InvalidDataException){rejected=true;}Check(rejected,"路径端点可进入山脉");
        c.RegionPrerequisites=new(){new(25,"missing")};rejected=false;try{c.ValidateWorld();}catch(InvalidDataException){rejected=true;}Check(rejected,"失效前置引用未拒绝");
        c=Map();c.Villages.Add(c.Villages[0] with {Id="bad",RegionId=blocked[0].Id});rejected=false;try{c.ValidateWorld();}catch(InvalidDataException){rejected=true;}Check(rejected,"阻断格村庄未拒绝");
        c=Map();var wall=c.Regions.Where(r=>r.Y==2).Select(r=>r.Id).ToHashSet();
        c.Regions=c.Regions.Select(r=>wall.Contains(r.Id)?r with {Enterable=false,Terrain="mountain"}:r).ToList();c.Dungeons.RemoveAll(d=>wall.Contains(d.RegionId));c.Villages.RemoveAll(v=>wall.Contains(v.RegionId));foreach(int id in wall)c.Rulers.Remove(id);
        rejected=false;try{c.ValidateWorld();}catch(InvalidDataException){rejected=true;}Check(rejected,"山脉隔断导致不可推进仍允许配置");
        c=Map();var old=GameSession.CreateWorld(c,887,new[]{"warrior","ranger","mage"}).CaptureProgress();old.ChapterIndex=3;old.Gold=987;
        old.CompletedMaps=c.ChapterMaps.Take(3).Select((map,index)=>new ChapterProgressSave{Chapter=index,Explored=map.Regions.Where(r=>r.Enterable).Select(r=>r.Id).ToList(),Cleared=map.Dungeons.Select(d=>d.Id).ToList()}).ToList();
        old.World!.TerrainVersion=0;old.World.Position=new Vector2(7.5f,.5f);old.World.Explored.Add(8);
        var migrated=GameSession.Restore(c,old);
        Check(migrated.Gold==987&&migrated.World!.Stage==CampaignStage.Village&&migrated.World.CurrentRegion==c.StartRegionId&&!migrated.World.Progress.Explored.Contains(8),"旧档落在新增山脉时未安全迁移或丢失资源");
        var invalid=migrated.CaptureProgress();invalid.World!.Position=new Vector2(7.5f,.5f);rejected=false;
        try{GameSession.Restore(c,invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"新版本存档允许落在阻断格");
        Console.WriteLine("PASS: blocked mountain/ocean cells, no content/ownership, micro-grid routing, explicit dungeon/fortress OR gates, whole-area unlock and invalid/unreachable map rejection");
    }
}
