using System.Linq;
using IdleCrpg.Core;
/// <summary>测试只读现场投影，不改战斗状态。</summary>
public static class SimulationDiagnostic {
 public static string Describe(GameSession g)=>g.EngagementDiagnostics()+"\n"+string.Join(";",g.Heroes.Select(h=>$"{h.SpecId} L{h.Level} xp={h.Experience}/{h.ExperienceFraction} HP={h.Hp}/{h.MaxHp} ATK={h.Attack} talents={string.Join('/',h.Talents)} skills={string.Join('/',h.SkillIds)}"))+"\n"+(g.Floors.Count==0?"none":$"room={g.CurrentRoom} moving={g.MovingBetweenRooms} hits={g.HitCount};"+string.Join(";",g.Floor.Rooms[g.CurrentRoom].Monsters.Select(m=>$"{m.SpecId} L{m.Level} HP={m.Hp} rate={g.Config.MonsterExperiencePercent(g.Heroes[0].Level,m.Level)}")));
}
