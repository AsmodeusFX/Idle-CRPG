using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>每职业固定4技能的机制容量；空 skill_id 表示该槽内容尚未投放，必须在界面与Review中明示缺口。</summary>
public sealed record SkillSlotSpec(string Hero,int Slot,string SkillId);

public sealed partial class Config
{
    private readonly List<SkillSlotSpec> skillSlots=new();
    public IReadOnlyList<SkillSlotSpec> SkillSlots=>skillSlots;
    /// <summary>某职业四槽的技能定义；空串代表未投放，不做隐式替代。</summary>
    public string[] ClassSkillSlots(string hero)=>Enumerable.Range(1,4).Select(slot=>skillSlots.SingleOrDefault(s=>s.Hero==hero&&s.Slot==slot)?.SkillId??"").ToArray();
    /// <summary>已投放技能数（用于 Review 与界面口径，防止空槽被当成已完成内容）。</summary>
    public int DeployedClassSkills(string hero)=>ClassSkillSlots(hero).Count(id=>id!="");
    private void LoadSkillSlots(string root,Config config)
    {
        var table=CsvTable.Load("skill/skill_slot.csv",File.ReadAllText(Path.Combine(root,"skill/skill_slot.csv")),"id","hero","slot","skill_id");
        foreach(var row in table.Rows)
        {
            string hero=row.Required("hero");int slot=row.Integer("slot");string skill=row["skill_id"];
            if(!config.Heroes.Any(h=>h.Id==hero)||slot is <1 or >4)throw new InvalidDataException("技能槽职业或槽位无效："+hero+"/"+slot);
            if(skill!=""&&(!config.Skills.Skills.TryGetValue(skill,out var definition)||definition.Kind!="ActiveSkill"))throw new InvalidDataException("技能槽引用不是主动技能："+skill);
            if(skill!=""&&(!config.Talents.Nodes.TryGetValue(skill,out var node)||node.Hero!=hero||node.Kind!="active"))throw new InvalidDataException("技能槽必须引用本职业可学习主动："+skill);
            if(skillSlots.Any(s=>s.Hero==hero&&s.Slot==slot))throw new InvalidDataException("技能槽重复："+hero+"/"+slot);
            skillSlots.Add(new(hero,slot,skill));
        }
        foreach(var hero in config.Heroes)if(Enumerable.Range(1,4).Any(slot=>!skillSlots.Any(s=>s.Hero==hero.Id&&s.Slot==slot)))throw new InvalidDataException("每职业必须声明4个技能槽："+hero.Id);
    }
}
