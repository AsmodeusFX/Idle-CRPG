using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>
/// 按"被击杀怪物的实际等级"（MonsterLevel 快照，仍在 Run 全局区间内）决定的掉落规则。
/// <para>
/// 这里只提供概率与准入清单，不定义怪物等级、掉落数量或奖励账本：
/// 等级仍来自 Run/生成快照，金币/XP/KP 仍走既有收据，装备数值仍由 <see cref="Config.EquipmentValue"/> 唯一计算。
/// </para>
/// </summary>
public sealed record LevelDropRule(int Level,double EquipmentChance,double[] QualityFractions,double ScrollChance,double PotionChance,string[] ScrollIds,string[] PotionIds);

public sealed partial class Config
{
    /// <summary>31 个实际怪级对应的掉落分布；旧组合（无该表）时为空，走原型整体概率。</summary>
    public List<LevelDropRule> DropTable=new();
    /// <summary>四品质在等级表里的固定列序，与 equipment_quality.csv 的 ID 集合必须一致。</summary>
    public static readonly string[] QualityFractionsOrder={"common","uncommon","rare","legendary"};

    /// <summary>取该实际等级的行；等级超界时夹到表内，不新增等级权威。</summary>
    public LevelDropRule? DropRuleOfLevel(int level)
    {
        if(DropTable.Count==0)return null;
        int clamped=Math.Clamp(level,1,DropTable[^1].Level);
        return DropTable.FirstOrDefault(r=>r.Level==clamped)??DropTable[^1];
    }

    public void LoadDropTable(string root)
    {
        DropTable.Clear();
        string path=Path.Combine(root,"item/drop_table.csv");
        if(!File.Exists(path))return;
        var table=CsvTable.Load("item/drop_table.csv",File.ReadAllText(path),"monster_level","equipment_chance","common","uncommon","rare","legendary","scroll_chance","potion_chance","scroll_ids","potion_ids");
        var qualities=QualityFractionsOrder;
        if(!EquipmentQualities.Select(q=>q.Id).OrderBy(x=>x).SequenceEqual(qualities.OrderBy(x=>x)))throw new InvalidDataException("掉落等级表的品质列与装备品质ID不一致");
        foreach(var r in table.Rows)
        {
            int level=r.Integer("monster_level");
            if(level!=DropTable.Count+1)throw r.Error("monster_level","掉落等级必须从1连续递增（不跳号、不重复）");
            if(level>MaxMonsterLevel)throw r.Error("monster_level","掉落等级超出属性表尾上限");
            double[] fractions=qualities.Select(q=>r.Number(q)).ToArray();
            if(fractions.Any(f=>f<0||f>1||!double.IsFinite(f)))throw r.Error("common","品质概率必须在0～1之间");
            if(Math.Abs(fractions.Sum()-1)>0.001)throw r.Error("common","四品质概率之和必须为1");
            double equipment=r.Number("equipment_chance"),scroll=r.Number("scroll_chance"),potion=r.Number("potion_chance");
            if(equipment is <0 or >1||scroll is <0 or >1||potion is <0 or >1)throw r.Error("equipment_chance","掉落概率必须在0～1之间");
            string[] scrollIds=Split(r["scroll_ids"]),potionIds=Split(r["potion_ids"]);
            if(scrollIds.Length==0)throw r.Error("scroll_ids","每个等级至少要有一个合法卷轴品类");
            if(potionIds.Length==0)throw r.Error("potion_ids","每个等级至少要有一个合法药剂品类");
            foreach(string id in scrollIds)if(Scrolls.All(s=>s.Id!=id))throw r.Error("scroll_ids","卷轴品类不存在："+id);
            foreach(string id in potionIds)if(Potions.All(p=>p.Id!=id))throw r.Error("potion_ids","药剂品类不存在："+id);
            DropTable.Add(new(level,equipment,fractions,scroll,potion,scrollIds,potionIds));
        }
        // 只要求"从1连续"，不要求覆盖属性表尾：超出表尾的实际怪级按最高行处理（不新增等级权威）。
        if(DropTable.Count>0&&DropTable[^1].Level<1)throw new InvalidDataException("掉落等级表为空");
        static string[] Split(string value)=>value.Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
    }
}
