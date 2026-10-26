using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>章节内容行为：只偏移索敌策略与附加技能，不新建 AI 分支或第二份动作实现。</summary>
public record ActMonsterBehavior(string MonsterId,MonsterTargetPolicy Policy,string Skill);

/// <summary>精英词缀：按难度给出出现率与权重；BuffId 为空表示只改索敌策略（猎手）。</summary>
public record EliteAffixRule(string Difficulty,string AffixId,string BuffId,MonsterTargetPolicy Policy,int Weight,int ChancePct);

/// <summary>五章内容层：行为偏移、精英词缀与 Boss 技能包。均为原型内容，不改变等级/奖励/难度权威。</summary>
public sealed partial class Config
{
    public Dictionary<string,ActMonsterBehavior> ActBehaviors { get; private set; }=new(StringComparer.Ordinal);
    public List<EliteAffixRule> EliteAffixes { get; private set; }=new();
    public Dictionary<string,string> BossSkills { get; private set; }=new(StringComparer.Ordinal);

    /// <summary>三个内容表都可缺失；缺失表示该内容未投放，不静默造默认行为。</summary>
    private void LoadMonsterContent(string directory)
    {
        ActBehaviors=new(StringComparer.Ordinal);EliteAffixes=new();BossSkills=new(StringComparer.Ordinal);

        string path=Path.Combine(directory,"monster/act_behaviors.csv");
        if(File.Exists(path))
        {
            var table=CsvTable.Load(path,File.ReadAllText(path),"monster_id","policy","skill_id");
            foreach(var r in table.Rows)
            {
                var policy=ParseMonsterPolicy(r.Required("policy"),path);
                string skill=r["skill_id"];
                if(!ActMonsters.Any(m=>m.Id==r.Id)&&!Monsters.Any(m=>m.Id==r.Id))
                    throw r.Error("monster_id","行为引用了未知怪物");
                if(skill!=""&&!Skills.Skills.ContainsKey(skill))
                    throw r.Error("skill_id","技能引用无效");
                ActBehaviors.Add(r.Id,new ActMonsterBehavior(r.Id,policy,skill));
            }
        }

        path=Path.Combine(directory,"monster/elite.csv");
        if(File.Exists(path))
        {
            // 同一难度多行，首列不唯一，按无键记录读取。
            var records=CsvTable.ReadRecords(path,File.ReadAllText(path));
            string[] expected={"difficulty","affix_id","buff_id","policy","weight","chance_pct"};
            if(records.Count==0||!records[0].Cells.SequenceEqual(expected,StringComparer.Ordinal))
                throw new InvalidDataException("精英词缀："+path+" 表头必须为 difficulty,affix_id,buff_id,policy,weight,chance_pct");
            for(int i=1;i<records.Count;i++)
            {
                var (line,cells)=records[i];
                if(cells.Length!=expected.Length)throw new InvalidDataException($"精英词缀：{path}:{line} 列数错误");
                string difficulty=cells[0].Trim(),affix=cells[1].Trim(),buff=cells[2].Trim();
                if(difficulty is not ("normal" or "nightmare" or "hell"))throw new InvalidDataException($"精英词缀：{path}:{line} 难度非法");
                if(affix=="")throw new InvalidDataException($"精英词缀：{path}:{line} 词缀ID为空");
                if(buff!=""&&!Skills.Buffs.ContainsKey(buff))throw new InvalidDataException($"精英词缀：{path}:{line} Buff引用无效");
                var policy=ParseMonsterPolicy(cells[3]==""?"Ordinary":cells[3],path);
                if(!int.TryParse(cells[4],out int weight)||weight<1||weight>1000)throw new InvalidDataException($"精英词缀：{path}:{line} 权重非法");
                if(!int.TryParse(cells[5],out int chance)||chance<0||chance>100)throw new InvalidDataException($"精英词缀：{path}:{line} 出现率非法");
                if(buff==""&&policy==MonsterTargetPolicy.Ordinary)throw new InvalidDataException($"精英词缀：{path}:{line} 词缀没有任何效果");
                EliteAffixes.Add(new EliteAffixRule(difficulty,affix,buff,policy,weight,chance));
            }
            foreach(var group in EliteAffixes.GroupBy(a=>a.Difficulty))
                if(group.Select(a=>a.ChancePct).Distinct().Count()!=1)throw new InvalidDataException("精英词缀：同一难度的出现率必须一致");
        }

        path=Path.Combine(directory,"monster/boss_skill.csv");
        if(File.Exists(path))
        {
            var table=CsvTable.Load(path,File.ReadAllText(path),"poi_id","skill_id");
            foreach(var r in table.Rows)
            {
                string skill=r.Required("skill_id");
                if(!Skills.Skills.ContainsKey(skill))throw r.Error("skill_id","Boss技能引用无效");
                BossSkills.Add(r.Id,skill);
            }
        }
    }

    private static MonsterTargetPolicy ParseMonsterPolicy(string value,string file)=>
        Enum.TryParse<MonsterTargetPolicy>(value,true,out var policy)&&Enum.IsDefined(policy)
            ?policy:throw new InvalidDataException($"怪物策略非法：{file} → {value}");

    /// <summary>生成时套用行为与 Boss 技能包；只读本表，不改变等级、奖励或房间预算。</summary>
    public void ApplyMonsterContent(Actor monster,bool boss,string poiId)
    {
        if(ActBehaviors.TryGetValue(monster.SpecId,out var behavior))
        {
            monster.TargetPolicy=behavior.Policy;
            if(behavior.Skill!="")monster.SkillIds=monster.SkillIds.Append(behavior.Skill).Distinct().ToArray();
        }
        if(boss&&poiId!=""&&BossSkills.TryGetValue(poiId,out var bossSkill))
            monster.SkillIds=monster.SkillIds.Append(bossSkill).Distinct().ToArray();
    }
}

public sealed partial class GameSession
{
    /// <summary>困难度名称与配置表对齐；难度索引来自会话，不再新增第二套难度字段。</summary>
    private static string DifficultyName(int difficulty)=>difficulty switch {0=>"normal",1=>"nightmare",2=>"hell",_=>throw new ArgumentOutOfRangeException(nameof(difficulty))};

    /// <summary>每房最多一个精英：先按难度出现率掷骰，再按权重选词缀，最后在存活非Boss单位中确定目标。
    /// 只在世界远征（有实际地牢）时启用，单地牢模拟与 legacy 夹具不掷骰，保证旧回归稳定。</summary>
    public bool TryApplyElite(List<Actor> monsters,Random rng,int difficulty,int chanceBias=0)
    {
        var rules=Config.EliteAffixes.Where(a=>a.Difficulty==DifficultyName(difficulty)).ToArray();
        if(rules.Length==0)return false;
        // 逐层语气只调整"本房是否出现精英"的概率，上限仍由 elite.csv 的词缀与难度决定。
        int chance=Math.Clamp(rules[0].ChancePct+chanceBias,0,100);
        if(rng.Next(100)>=chance)return false;
        int total=rules.Sum(a=>a.Weight),roll=rng.Next(total),accumulated=0;
        var picked=rules[^1];
        foreach(var rule in rules){accumulated+=rule.Weight;if(roll<accumulated){picked=rule;break;}}
        var candidates=monsters.Where(m=>m.Alive&&!m.Boss).ToArray();
        if(candidates.Length==0)return false;
        var target=candidates[rng.Next(candidates.Length)];
        if(picked.Policy!=MonsterTargetPolicy.Ordinary)target.TargetPolicy=picked.Policy;
        // 精英身份：合法掷骰一旦命中即成为该实例的权威标记（不是UI文案），并分配一次性收据令牌。
        target.Elite=true;
        target.EliteToken=$"E{FloorIndex}-{CurrentRoom}-{CombatId(target)}-{picked.AffixId}";
        if(picked.BuffId!="")ApplyBuff(picked.BuffId,CombatId(target),CombatId(target),"elite_affix",1,null);
        return true;
    }
}
