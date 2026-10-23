using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>P1-5 切片C2/C3：怪物行为偏移、精英词缀与Boss技能包的隔离检查。</summary>
public static class ActMonsterContentChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("ACT_CONTENT: "+why);}
        string configRoot=Path.Combine(root,"Idle-CRPG/config");
        var c=LegacySkillConfig.Load(configRoot);

        // 一、行为层：M05 范围、M06 猎手、M08 支援各有对应偏移，其余身份不凭空获得技能。
        Need(c.ActBehaviors.Count>0,"没有加载任何行为偏移");
        foreach(var behavior in c.ActBehaviors.Values)
        {
            var monster=c.ActMonsters.SingleOrDefault(m=>m.Id==behavior.MonsterId);
            Need(monster!=null,"行为引用了不存在的身份："+behavior.MonsterId);
            if(behavior.Skill!="")Need(c.Skills.Skills.ContainsKey(behavior.Skill),"行为技能不存在："+behavior.Skill);
        }
        var hunter=c.ActBehaviors.Values.FirstOrDefault(b=>b.Policy==MonsterTargetPolicy.BacklinePriority);
        Need(hunter!=null,"缺少猎手策略身份");
        var hunterActor=Actor.From(c.ActMonsters.Single(m=>m.Id==hunter!.MonsterId));
        c.ApplyCombatProfile(hunterActor);c.ApplyMonsterContent(hunterActor,false,"");
        Need(hunterActor.TargetPolicy==MonsterTargetPolicy.BacklinePriority,"猎手身份未套用后排优先策略");

        var caster=c.ActBehaviors.Values.FirstOrDefault(b=>b.Skill=="mob_area_pulse");
        Need(caster!=null,"缺少范围攻击身份");
        var casterActor=Actor.From(c.ActMonsters.Single(m=>m.Id==caster!.MonsterId));
        c.ApplyCombatProfile(casterActor);c.ApplyMonsterContent(casterActor,false,"");
        Need(casterActor.SkillIds.Contains("mob_area_pulse"),"范围身份未获得范围技能");
        var support=c.ActBehaviors.Values.FirstOrDefault(b=>b.Skill=="mob_support");
        Need(support!=null,"缺少支援身份");

        // 二、Boss 技能包：已命名Castle必须能取到技能，且只加给 Boss 本体。
        Need(c.BossSkills.Count>0,"没有加载Boss技能包");
        var poi=c.BossSkills.Keys.First();
        var bossActor=Actor.From(c.Monsters[0]);c.ApplyCombatProfile(bossActor);
        c.ApplyMonsterContent(bossActor,true,poi);
        Need(bossActor.SkillIds.Contains(c.BossSkills[poi]),"Boss未获得技能包");
        var normalActor=Actor.From(c.Monsters[0]);c.ApplyCombatProfile(normalActor);
        c.ApplyMonsterContent(normalActor,false,poi);
        Need(!normalActor.SkillIds.Contains(c.BossSkills[poi]),"普通怪错误获得Boss技能");

        // 三、精英词缀：三难度都有规则，出现率一致，且绝不落在 Boss 身上。
        foreach(var difficulty in new[]{"normal","nightmare","hell"})
        {
            var rules=c.EliteAffixes.Where(a=>a.Difficulty==difficulty).ToArray();
            Need(rules.Length>0,"缺少难度词缀："+difficulty);
            Need(rules.Select(a=>a.ChancePct).Distinct().Count()==1,"同难度出现率不一致："+difficulty);
            Need(rules.All(a=>a.BuffId!=""||a.Policy!=MonsterTargetPolicy.Ordinary),"词缀没有任何效果："+difficulty);
        }
        var rollConfig=LegacySkillConfig.Load(configRoot);
        var rollGame=GameSession.CreateWorld(rollConfig,7,new[]{"warrior","ranger","mage"});
        for(int i=0;i<20000&&(rollGame.World!.Stage!=CampaignStage.Dungeon||rollGame.CurrentRoom==0);i++)rollGame.Tick(.05f);
        Need(rollGame.World!.Stage==CampaignStage.Dungeon,"精英检查未进入地牢");
        var room=rollGame.Floor.Rooms[rollGame.CurrentRoom];
        // 精英只作用于存活非Boss单位；Boss身上不得出现精英Buff。
        var bossInRoom=room.Monsters.Where(m=>m.Boss).ToArray();
        foreach(var boss in bossInRoom)
            Need(!rollGame.Capture().SkillState.Buffs.Any(b=>b.Target==room.Monsters.IndexOf(boss)&&b.Id.StartsWith("elite_")),"Boss被施加了精英词缀");

        // 掷骰：同种子可复现；成功一次即验证目标真实获得Buff或策略（同一房间内直接调用，避免反复建会话）。
        var eliteRoom=rollGame.Floor.Rooms[rollGame.CurrentRoom];
        var elitePool=eliteRoom.Monsters.Where(m=>m.Alive&&!m.Boss).ToList();
        Need(elitePool.Count>0,"精英检查房间没有可作用目标");
        bool sawElite=false;
        for(int seed=0;seed<400&&!sawElite;seed++)
        {
            var beforePolicies=elitePool.Select(m=>m.TargetPolicy).ToArray();
            int beforeBuffs=rollGame.Capture().SkillState.Buffs.Count;
            bool applied=rollGame.TryApplyElite(elitePool,new Random(seed),0);
            if(!applied)continue;
            sawElite=true;
            int afterBuffs=rollGame.Capture().SkillState.Buffs.Count;
            bool policyChanged=elitePool.Where((m,i)=>m.TargetPolicy!=beforePolicies[i]).Any();
            Need(afterBuffs>beforeBuffs||policyChanged,"精英词缀没有产生任何效果");
        }
        Need(sawElite,"400次掷骰内没有任何精英生成");
        bool sameRoll=rollGame.TryApplyElite(elitePool,new Random(99),0)==rollGame.TryApplyElite(elitePool,new Random(99),0);
        Need(sameRoll,"同种子掷骰结果不稳定");

        // 四、配置拒绝：未知行为技能、无效果词缀、未知Boss技能都必须加载失败。
        string qa=Path.Combine(root,"artifacts/qa/act-content-"+Guid.NewGuid().ToString("N"));
        foreach(var file in Directory.EnumerateFiles(configRoot,"*.csv",SearchOption.AllDirectories))
        {string target=Path.Combine(qa,Path.GetRelativePath(configRoot,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
        void Reject(string relative,string text,string why)
        {
            string path=Path.Combine(qa,relative),original=File.ReadAllText(path);
            File.WriteAllText(path,text);bool failed=false;
            try{Config.Load(qa);}catch(Exception error)when(error is InvalidDataException or CsvConfigurationException){failed=true;}
            File.WriteAllText(path,original);
            Need(failed,why);
        }
        string behaviorPath=Path.Combine(qa,"monster/act_behaviors.csv"),behaviorText=File.ReadAllText(behaviorPath);
        Reject("monster/act_behaviors.csv",behaviorText.Replace("mob_area_pulse","mob_not_exists"),"未知行为技能未拒绝");
        string elitePath=Path.Combine(qa,"monster/elite.csv"),eliteText=File.ReadAllText(elitePath);
        Reject("monster/elite.csv",eliteText.Replace("elite_hardened",""),"无效果词缀未拒绝");
        string bossSkillPath=Path.Combine(qa,"monster/boss_skill.csv"),bossText=File.ReadAllText(bossSkillPath);
        Reject("monster/boss_skill.csv",bossText.Replace("mob_heavy","mob_not_exists"),"未知Boss技能未拒绝");

        Console.WriteLine("PASS: act behaviors (area/hunter/support), boss skill packages limited to bosses, three-difficulty elite affixes with consistent chance and no boss affix, deterministic elite rolls with real effect, and rejection of unknown behavior/boss skills and effect-less affixes");
    }
}
