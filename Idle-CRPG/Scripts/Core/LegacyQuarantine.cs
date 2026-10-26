using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>跨Run只读保存原技能字段和旧投入返还状态，不构成可上阵英雄。</summary>
public sealed record LegacySkillArchive(string RunId,string HeroInstanceId,string ClassId,string[] Loadout,RetiredTalent[] Investments,string RawSkillData="");
/// <summary>旧星图购买等级原样保留；不据当前配置价格推断历史实付金额或自动退款。</summary>
public sealed record LegacyStarArchive(string RunId,StarRankSave[] Purchases);

/// <summary>
/// 迁移隔离区（C-LEGACY-5）：旧档超出 4 席或含候补时，历史英雄按原样只读冻结保存。
/// 它不是 Account 永久英雄，不上阵、不参与战斗/天赋/装备，不能作为普通候补调换，也不授予任何玩法权益。
/// 只有"清除全部进度"经明确确认后才清除；放弃本轮/正式重生不得静默销毁。
/// </summary>
public sealed class LegacyQuarantine
{
    public int Version=1;
    /// <summary>冻结英雄的原始数据（等级、经验、天赋、装备等按旧档原样保留）。</summary>
    public List<Actor> Heroes=new();
    /// <summary>迁移说明：来源、冻结原因与去向，供 UI 原样展示。</summary>
    public List<string> Notes=new();
    public List<LegacySkillArchive> Skills=new();
    public List<LegacyStarArchive> Stars=new();
    /// <summary>只归档历史价值，不跨Run发点或退款；同Run/实例更新返还标记而不重复新增。</summary>
    public void Absorb(SessionSnapshot state)
    {
        foreach(var hero in state.Heroes.Concat(state.ReserveHeroes).Where(h=>h.RetiredTalents.Count>0||h.LegacySkillIds.Length>0))
        {
            int index=Skills.FindIndex(p=>p.RunId==state.RunId&&p.HeroInstanceId==hero.HeroInstanceId);
            var record=new LegacySkillArchive(state.RunId,hero.HeroInstanceId,hero.SpecId,hero.LegacySkillIds.ToArray(),hero.RetiredTalents.ToArray(),hero.LegacySkillData);
            if(index<0)Skills.Add(record);else Skills[index]=record;
        }
        // 保留原始购买ID和等级；退役记录仍存在Run.Stars，无任何新增金币。
        var purchases=state.Stars.Ranks.Where(p=>p.Id.StartsWith("s_skill_",StringComparison.Ordinal)||p.Id.StartsWith("s_party_",StringComparison.Ordinal)||p.Id is "s_respec" or "s_respec_discount").ToArray();
        if(purchases.Length>0&&!Stars.Any(p=>p.RunId==state.RunId))Stars.Add(new(state.RunId,purchases));
    }

    public LegacyQuarantine Copy()
    {
        var copy=new LegacyQuarantine{Version=Version,Notes=new(Notes),Skills=Skills.Select(s=>s with {Loadout=s.Loadout.ToArray(),Investments=s.Investments.ToArray()}).ToList(),Stars=Stars.Select(s=>s with {Purchases=s.Purchases.ToArray()}).ToList()};
        foreach(var hero in Heroes)copy.Heroes.Add(hero.Copy());
        return copy;
    }
    public void Validate()
    {
        if(Version!=1)throw new InvalidDataException("迁移隔离区版本无效");
        if(Heroes==null||Notes==null||Notes.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("迁移隔离区字段为空");
        if(Skills==null||Stars==null||Skills.Any(s=>string.IsNullOrWhiteSpace(s.RunId)||string.IsNullOrWhiteSpace(s.HeroInstanceId)||s.Loadout==null||s.Investments==null||s.Investments.Any(p=>p.Rank<1))||Skills.Select(s=>(s.RunId,s.HeroInstanceId)).Distinct().Count()!=Skills.Count||Stars.Any(s=>string.IsNullOrWhiteSpace(s.RunId)||s.Purchases==null||s.Purchases.Any(p=>p.Level<1))||Stars.Select(s=>s.RunId).Distinct().Count()!=Stars.Count)throw new InvalidDataException("旧技能/星图隔离记录无效");
        var ids=Heroes.Select(h=>h.HeroInstanceId).ToArray();
        if(ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct().Count()!=ids.Length)throw new InvalidDataException("迁移隔离英雄实例ID重复或缺失");
    }
}

/// <summary>
/// 旧档含超额英雄或候补，需要玩家先明确选择本轮最多 4 名英雄；不自动裁剪、不删除任何人。
/// 继承 MigrationRequiredException，使既有"需要用户介入"的调用方保持同一处理分支。
/// </summary>
public sealed class LegacySelectionRequiredException : MigrationRequiredException
{
    public SaveDocument Document {get;}
    public IReadOnlyList<(int Index,string SpecId,string Name,int Level)> Candidates {get;}
    public LegacySelectionRequiredException(SaveDocument document,string message):base(message)
    {
        Document=document;
        Candidates=(document.Run?.Heroes??new()).Select((h,i)=>(i,h.SpecId,h.Name,h.Level)).ToArray();
    }
}
