using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

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

    public LegacyQuarantine Copy()
    {
        var copy=new LegacyQuarantine{Version=Version,Notes=new(Notes)};
        foreach(var hero in Heroes)copy.Heroes.Add(hero.Copy());
        return copy;
    }
    public void Validate()
    {
        if(Version!=1)throw new InvalidDataException("迁移隔离区版本无效");
        if(Heroes==null||Notes==null||Notes.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("迁移隔离区字段为空");
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
