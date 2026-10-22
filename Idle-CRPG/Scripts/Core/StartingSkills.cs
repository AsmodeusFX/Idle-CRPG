using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>初始技能消耗1级获得的技能点，与其他手动投入一起计算，可洗掉。</summary>
    public int TalentInvestment(Actor h)=>h.Talents.Values.Sum();
    /// <summary>职业及泛用真实弹道的只读绘制坐标，包含隔离QA多重箭，不伪造视觉碰撞。</summary>
    public IEnumerable<(System.Numerics.Vector2 Previous,System.Numerics.Vector2 Position,DamageType Type)> TalentProjectiles=>skillState.Flights.Where(f=>f.Active&&(Config.Talents.Nodes.ContainsKey(f.Skill)||f.Form=="generic"))
        .Select(f=>(f.Previous,f.Position,Config.Talents.Nodes.TryGetValue(f.Skill,out var node)?node.DamageType:SkillConfig.Effects[SkillConfig.Effects[f.GenericEffect].Impact].DamageType));
    /// <summary>新英雄和旧正式存档各初始化一次。旧角色无空余点则保留既有投入；洗点后不自动补回。</summary>
    private bool GrantStarterSkill(Actor h)
    {
        if(h.StartingSkillGranted)return false;
        h.StartingSkillGranted=true;
        string id=Config.Talents.StartingSkills[h.SpecId];
        if(!h.Talents.ContainsKey(id)&&TalentPoints(h)<=0)return true;
        h.Talents[id]=Math.Max(1,h.Talents.GetValueOrDefault(id));
        if(h.SkillIds.Length<ActiveSkillSlots&&!h.SkillIds.Contains(id))h.SkillIds=h.SkillIds.Append(id).ToArray();
        return true;
    }
}
