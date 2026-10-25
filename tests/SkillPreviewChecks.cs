using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

public static class SkillPreviewChecks
{
    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));int cases=0;
        // M7 的 15 个 skill_XX 保持退役；48 个稳定天赋节点（含未绑定 skill_id 的旧主动）仍是正式内容。
        if(config.Skills.Skills.Keys.Any(id=>id.StartsWith("skill_")))throw new Exception("退役 skill_XX 仍混入正式配置/预览");
        if(config.Talents.Nodes.Count!=66||config.Talents.Nodes.Values.Count(n=>n.Kind=="active")!=39)throw new Exception("正式节点/主动数量错误");
        // 只在QA目录验证：引用已删 skill_XX 且主备份均不兼容时自动重置，设置不受影响。
        {
            var directory=Path.Combine(root,"artifacts/qa/retired-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var file=Path.Combine(directory,"progress.json");var settings=Path.Combine(directory,"settings.json");File.WriteAllText(settings,"keep-settings");
            var snapshot=GameSession.CreateWorld(config,12,new[]{"warrior"}).CaptureProgress();
            snapshot.Heroes[0].SkillIds=new[]{"skill_01"};
            string text=SaveStore.Encode(new(){Session=snapshot});File.WriteAllText(file,text);File.WriteAllText(file+".bak",text);
            var store=new SaveStore(file);bool refused=false;try{store.Load(config);}catch(MigrationRequiredException){refused=true;}
            if(!refused||store.RecoveryReset||File.ReadAllText(file)!=text||File.ReadAllText(file+".bak")!=text||File.ReadAllText(settings)!="keep-settings")throw new Exception("不兼容账号必须保留主备份并明确提示，不能自动清档");
        }
        var compatible=GameSession.CreateWorld(config,13,new[]{"warrior"}).CaptureProgress();
        if(SaveStore.Decode(config,SaveStore.Encode(new(){Session=compatible})).Session==null)throw new Exception("兼容进度被误重置");
        foreach(var node in config.Talents.Nodes.Values.Where(n=>n.Kind=="active"))
        foreach(int rank in new[]{1,node.MaxRank})
        {
            var game=GameSession.CreateSkillPreview(config,node.Hero,node.Id,rank,config.Monsters[0].Id,12,1);
            if(game.TalentLevel(game.Heroes[0],node.Id)!=rank||game.Heroes[0].SkillIds.Single()!=node.Id)throw new Exception("试场技能等级/装配错误："+node.Id);
            bool cast=false;
            for(int i=0;i<400;i++)
            {
                game.TickSkillPreview(.05f);
                cast|=game.VisualEvents.Any(e=>e.Skill==node.Id);
            }
            if(!cast)throw new Exception("试场技能未产生表现："+node.Id+" Lv."+rank+" corpses="+game.Capture().SkillState.Corpses.Count+" hero="+game.Heroes[0].Position+" events="+string.Join(',',game.VisualEvents.Select(e=>e.Skill)));
            if(game.World!.Stage!=CampaignStage.Dungeon||game.Floors.Count!=1||game.Floor.Rooms.Count!=1)throw new Exception("试场离开独立房间");
            cases++;
        }
        foreach(var monster in config.Monsters)
        {
            var n=config.Talents.Nodes.Values.First(n=>n.Kind=="active");
            var game=GameSession.CreateSkillPreview(config,n.Hero,n.Id,1,monster.Id,40,config.MaxMonsterLevel);
            if(game.Floor.Rooms[0].Monsters.Count!=40||game.Floor.Rooms[0].Monsters.Any(m=>m.SpecId!=monster.Id||m.Level!=config.MaxMonsterLevel))throw new Exception("试场怪物参数错误");
        }
        Console.WriteLine($"SKILL_PREVIEW_OK: {cases} skill/rank cases, all monster templates, isolated room");
    }
}
