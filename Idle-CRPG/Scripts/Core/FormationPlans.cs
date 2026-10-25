using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace IdleCrpg.Core;
public sealed partial class GameSession
{
    private readonly List<Actor> reserveHeroes=new();
    private string[] pendingFormation=Array.Empty<string>();
    private int formationCapacityFloor;
    /// <summary>退下英雄保留永久成长与技能状态，不参与当前房间战斗。</summary>
    public IReadOnlyList<Actor> ReserveHeroes=>reserveHeroes.AsReadOnly();
    /// <summary>唯一待执行编队的有序职业列表；空列表表示没有计划。</summary>
    public IReadOnlyList<string> PendingFormation=>Array.AsReadOnly(pendingFormation);
    private int FormationCapacity(StarProgression stars)=>4;
    /// <summary>编队只是计划，确认时不修改正在战斗的成员；新计划替换尚未执行的旧编队计划。</summary>
    public bool RequestFormation(IEnumerable<string> ids,out string reason)
    {
        reason="本轮队伍已锁定；更换队伍请放弃本轮后重新选队";return false;
    }
    private bool ValidFormation(string[] ids,int capacity)=>ids.Length>=1&&ids.Length<=capacity&&ids.Distinct().Count()==ids.Length&&ids.All(id=>Config.Heroes.Any(h=>h.Id==id)&&(HeroUnlocked(id)||Heroes.Concat(reserveHeroes).Any(h=>h.SpecId==id)));
    /// <summary>撤销未执行的阵容变更；已经开始的回城表演照常完成。</summary>
    public void CancelFormation()
    {if(pendingFormation.Length==0)return;pendingFormation=Array.Empty<string>();Note("已取消待执行的编队计划");}
    /// <summary>编队状态提示只描述当前计划，UI不能通过显示状态直接变更成员。</summary>
    public string FormationStatus=>pendingFormation.Length==0?"":World?.Stage==CampaignStage.Dungeon?"等待当前房间清理、救援与拾取完成":World?.PortalPhase!=PortalPhase.None?"正在传送，回城后前往凯恩":World?.Stage==CampaignStage.Village?"前往凯恩 · 到达后变更编队":"编队计划 · 正在返回城镇";
    private Actor CreateFormationHero(string id)
    {
        var hero=Actor.From(Config.Heroes.Single(h=>h.Id==id));Config.ApplyCombatProfile(hero);
        // 首次加入从1级、0经验起步；候补直接复用原Actor保留个人成长。
        var growth=Config.HeroLevels[hero.Level];
        hero.Attack+=growth.AttackBonus+(float)starAttack;hero.MaxHp+=growth.HpBonus+(float)starHp;hero.Hp=hero.MaxHp;GrantStarterSkill(hero);return hero;
    }
    /// <summary>唯一执行入口：全队必须实际到达编队NPC范围并完成城镇服务读条。</summary>
    internal void ApplyPendingFormation()
    {
        if(pendingFormation.Length==0)return;
        var town=Config.Town??throw new InvalidOperationException("缺少编队城镇");var npc=town.Npc("formation");
        if(World?.Stage!=CampaignStage.Village||World.PortalPhase!=PortalPhase.None||World.TownPhase!=TownVisitPhase.Formation||World.TradeElapsed+.00001f<town.ServiceSeconds||Heroes.Any(h=>Vector2.Distance(h.Position,npc.Position)>town.InteractionRange))throw new InvalidOperationException("必须到达凯恩并完成编队交互");
        if(!ValidFormation(pendingFormation,PartyCapacity))throw new InvalidOperationException("编队计划已失效");
        ClearSummons();
        var pool=Heroes.Concat(reserveHeroes).ToDictionary(h=>h.HeroInstanceId);var next=pendingFormation.Select(id=>pool.TryGetValue(id,out var hero)?hero:CreateFormationHero(id)).ToArray();
        reserveHeroes.Clear();reserveHeroes.AddRange(pool.Values.Where(h=>!pendingFormation.Contains(h.SpecId)));
        Heroes.Clear();Heroes.AddRange(next);foreach(var h in Heroes){h.Position=npc.Position;h.Timer=0;}
        pendingFormation=Array.Empty<string>();ResetSkillRoom();roomEntry.Clear();ResetRescue();destination=-1;phase=0;regroup=0;
        Note(npc.Name+"：编队变更完成 · "+string.Join("、",Heroes.Select(h=>h.Name))+"；完成补给后继续远征");
    }
    private void ValidateFormationCapacity(StarProgression next)
    {if(Heroes.Count>FormationCapacity(next)||pendingFormation.Length>FormationCapacity(next))throw new InvalidOperationException("星图变更不能使当前或计划编队超过已解锁席位");}
    private static void ValidateFormationSnapshot(Config c,SessionSnapshot s)
    {
        if(s.ReserveHeroes==null||s.PendingFormation==null||s.ReserveHeroes.Count!=0||s.PendingFormation.Length!=0||s.Heroes.Count is <1 or >4)throw new InvalidDataException("本轮阵容锁定或实例引用无效");
    }
}
public sealed partial class WorldCampaign
{
    /// <summary>清房后主动回城不记未完成地牢通关，不发额外奖励；已批准的城堡重新排回队首。</summary>
    internal void ReturnForFormation(GameSession game,bool fromDungeon)
    {
        if(Target.Dungeon.Castle&&!Progress.Cleared.Contains(Target.Dungeon.Id)&&!pendingCastles.Contains(Target.Dungeon.Id))pendingCastles.Insert(0,Target.Dungeon.Id);
        Village=config.ReturnVillage(CurrentRegion);Stage=CampaignStage.TravelVillage;
        BuildRoute(Village.RegionId,WorldPoint(Village.RegionId,Village.X,Village.Y));
        if(fromDungeon)BeginPortal(true,origin:game.Heroes[0].Position);
        Status=fromDungeon?"编队计划 · 打开回城传送门":"编队计划 · 返回城镇前往凯恩";game.Note(Status);
    }
}
