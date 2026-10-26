using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>
/// Boss 阶段运行时（S15-C2-A / A2）：唯一消费者。
/// <para>
/// 输入只有 <c>boss_phase.csv</c>；输出只有三件事——把阶段技能追加到 Boss 的 SkillIds、
/// 激活预留的阶段随从/接替 Boss、施加一个短期阶段 Buff。等级取 Boss 本体快照（仍在 Run.[Lmin,Lmax] 内），
/// 伤害与奖励仍走既有 DamagePipeline / 击杀收据，因此不产生第二套刷怪或收益权威。
/// </para>
/// <para>
/// 进度以 <c>poi:floor:room → 已生效阶段号</c> 保存：同一房间同一段只生效一次，
/// 读档重开房间时按存档进度重放（不重复生成 Boss、不重复发放奖励）。
/// </para>
/// </summary>
public sealed partial class GameSession
{
    private readonly Dictionary<string,int> bossPhaseProgress=new(StringComparer.Ordinal);

    /// <summary>只读阶段进度，供存档捕获与出处账本使用；key 为 <c>poi:floor:room</c>。</summary>
    public IReadOnlyDictionary<string,int> BossPhaseState=>bossPhaseProgress;

    /// <summary>当前房间已经进入的阶段名；没有阶段或尚未切换时为空串。UI 只读。</summary>
    public string BossPhaseLabel
    {
        get
        {
            if(Floors.Count==0||ActiveDungeon==null)return "";
            string key=$"{ActiveDungeon.Id}:{FloorIndex}:{CurrentRoom}";
            if(!bossPhaseProgress.TryGetValue(key,out int applied)||applied<=0)return "";
            var phase=Config.BossPhaseOf(Config.ChapterIndex+1,ActiveDungeon.Id,applied);
            return phase==null?"":$"{phase.Name}（第{applied}段）";
        }
    }

    private static string BossPhaseKey(string poi,int floor,int room)=>$"{poi}:{floor}:{room}";

    /// <summary>进入新副本时清空本轮的阶段进度：复刷是新的合法遭遇，但仍按新房间重新走一遍阶段。</summary>
    private void ResetBossPhases()=>bossPhaseProgress.Clear();

    /// <summary>
    /// 进房生成/读档重建时预留阶段槽位：随从与接替 Boss 以 0 血待命，激活一次后永不复用。
    /// 预留数量完全来自配置（每段≤6、每 POI≤10），不按房间预算或玩家等级推算。
    /// </summary>
    internal static void PrepareBossPhaseActors(Config config,Room room,string poiId,int floor,int roomId,int fallbackLevel)
    {
        if(room==null||!room.Generated||string.IsNullOrEmpty(poiId))return;
        var phases=config.BossPhasesOf(config.ChapterIndex+1,poiId);
        if(phases.Count==0)return;
        // 幂等：同一房间实例只预留一次，读档重建的 Monsters 已被清空，因此会重新预留且顺序一致（收据索引稳定）。
        if(room.Monsters.Any(m=>m.BossPhaseId!=""))return;
        var boss=room.Monsters.FirstOrDefault(m=>m.Boss);
        var anchor=boss?.Position??room.Center;
        int level=boss?.Level??fallbackLevel;
        foreach(var phase in phases)
        {
            if(!phase.HasEscort||phase.EscortCount<1)continue;
            var spec=config.FindMonsterSpec(phase.EscortTemplate);
            if(spec==null)continue;
            for(int i=0;i<phase.EscortCount;i++)
            {
                if(room.Monsters.Count>=1000)break;
                // 接替 Boss 走 Boss 预算（生命/攻击倍率与额外 XP），阶段随从按普通单位结算。
                var actor=config.Spawn.Create(spec,Math.Max(1,level),phase.IsSuccessor,new());
                config.ApplyCombatProfile(actor);
                actor.Name=phase.Name;
                actor.BossPhaseId=$"{poiId}:{phase.Phase}";
                actor.BossPhaseDormant=true;
                actor.Hp=0;
                actor.Position=EscortPoint(room,anchor,i);
                room.Monsters.Add(actor);
            }
        }
        room.PhasePending=phases.Any(p=>p.IsSuccessor);
    }

    /// <summary>阶段随从落点只做确定性避让，不参与寻路或索敌；都在房间边界内。</summary>
    private static Vector2 EscortPoint(Room room,Vector2 anchor,int index)
    {
        var offset=new Vector2(((index%3)-1)*.8f,(index/3)*.9f-.4f);
        return Vector2.Clamp(anchor+offset,room.Center-room.HalfSize+new Vector2(.4f),room.Center+room.HalfSize-new Vector2(.4f));
    }

    /// <summary>捕获阶段进度为存档字符串（<c>poi:floor:room:phase</c>），按 key 排序保证固定种子与字节稳定。</summary>
    internal List<string> CaptureBossPhases()
        =>bossPhaseProgress.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>$"{p.Key}:{p.Value}").ToList();

    /// <summary>
    /// 读档恢复阶段进度：只恢复当前房间（其它房间的阶段只可能属于已清房或未进入，重新进入时会重新判定）。
    /// 恢复时按顺序重放已生效阶段，保证 Boss 技能、随从与 Buff 与存档前一致。
    /// </summary>
    internal void RestoreBossPhases(IReadOnlyList<string> entries)
    {
        bossPhaseProgress.Clear();
        if(entries==null||Floors.Count==0||ActiveDungeon==null)return;
        string current=BossPhaseKey(ActiveDungeon.Id,FloorIndex,CurrentRoom);
        int applied=0;
        foreach(string entry in entries)
        {
            if(string.IsNullOrWhiteSpace(entry))continue;
            var parts=entry.Split(':');
            if(parts.Length!=4||parts[0]=="")continue;
            if(!int.TryParse(parts[1],out _)||!int.TryParse(parts[2],out _)||!int.TryParse(parts[3],out int phase))continue;
            string key=$"{parts[0]}:{parts[1]}:{parts[2]}";
            if(key!=current)continue;
            applied=Math.Max(applied,phase);
        }
        if(applied<=0)return;
        var room=Floor.Rooms[CurrentRoom];
        room.PhasePending=Config.BossPhasesOf(Config.ChapterIndex+1,ActiveDungeon.Id).Any(p=>p.IsSuccessor);
        for(int phase=1;phase<=applied;phase++)
        {
            var definition=Config.BossPhaseOf(Config.ChapterIndex+1,ActiveDungeon.Id,phase);
            if(definition==null)break;
            ApplyBossPhase(definition,room,Floor,current,restore:true);
            bossPhaseProgress[current]=phase;
        }
    }

    /// <summary>
    /// 单一阶段推进入口：每帧最多推进一段，保证四段阈值/楼层/序列按顺序可见，且同段不会重复生效。
    /// </summary>
    private void AdvanceBossPhases(Room room)
    {
        var poi=ActiveDungeon?.Id;
        if(string.IsNullOrEmpty(poi))return;
        var phases=Config.BossPhasesOf(Config.ChapterIndex+1,poi);
        if(phases.Count==0)return;
        string key=BossPhaseKey(poi,FloorIndex,room.Id);
        int applied=bossPhaseProgress.GetValueOrDefault(key,0);
        if(applied>=phases.Count)return;
        var next=phases[applied];
        if(!BossPhaseTriggerMet(next,room))return;
        ApplyBossPhase(next,room,Floor,key,restore:false);
        bossPhaseProgress[key]=next.Phase;
        MarkChanged();
    }

    private bool BossPhaseTriggerMet(BossPhase phase,Room room)
    {
        var boss=ActiveBossInRoom(room);
        // 接替阶段的前提正好相反：原 Boss 已倒下、预留的接替者尚待命。
        if(phase.IsSuccessor)
            return room.Monsters.Any(m=>m.BossPhaseDormant&&m.BossPhaseId==$"{phase.PoiId}:{phase.Phase}")&&room.Monsters.All(m=>m.BossPhaseDormant||!m.Alive);
        if(phase.NeedsBoss&&boss==null)return false;
        return phase.Trigger switch
        {
            BossPhaseTrigger.FightStart=>CombatEntryRemaining<=0,
            BossPhaseTrigger.HpPct=>boss!=null&&boss.Hp*100f<=phase.Threshold*boss.MaxHp,
            // 遭遇序列：房间里所有已激活单位死亡（待命阶段的预留槽不算敌人，因此不会卡住推进）。
            BossPhaseTrigger.Sequence=>room.Monsters.All(m=>m.BossPhaseDormant||!m.Alive),
            BossPhaseTrigger.FloorStage=>FloorIndex+1>=phase.Threshold,
            _=>false
        };
    }

    /// <summary>当前房内真正的 Boss 本体：待命阶段的预留接替者不算，死亡后也不算。</summary>
    private static Actor? ActiveBossInRoom(Room room)=>room.Monsters.FirstOrDefault(m=>m.Boss&&!m.BossPhaseDormant&&m.Alive);

    /// <summary>
    /// 生效一段阶段：激活预留槽 → 接替者成为新 Boss → 追加技能/施加短期 Buff → 预警文案与表现事件。
    /// 不扣血、不发奖励、不改等级；奖励仍由该单位被合法击杀时的既有收据路径产生。
    /// </summary>
    private void ApplyBossPhase(BossPhase phase,Room room,Floor floor,string key,bool restore)
    {
        if(room==null||!room.Generated)return;
        string tag=$"{phase.PoiId}:{phase.Phase}";
        int activated=0;
        foreach(var actor in room.Monsters.Where(m=>m.BossPhaseDormant&&m.BossPhaseId==tag).ToArray())
        {
            actor.BossPhaseDormant=false;
            actor.Hp=actor.MaxHp;
            if(phase.IsSuccessor){actor.Boss=true;actor.Name=phase.Name;}
            floor.ActualSpawned++;
            activated++;
        }
        if(phase.IsSuccessor)room.PhasePending=false;

        Actor? owner=phase.IsSuccessor
            ?room.Monsters.FirstOrDefault(m=>m.Boss&&!m.BossPhaseDormant&&m.Alive)
            :ActiveBossInRoom(room);
        if(owner!=null)
        {
            if(phase.SkillId!=""&&!owner.SkillIds.Contains(phase.SkillId))
                owner.SkillIds=owner.SkillIds.Append(phase.SkillId).ToArray();
            if(phase.BuffId!="")
                ApplyBuff(phase.BuffId,CombatId(owner),CombatId(owner),"boss_phase_"+phase.Phase,1,null);
        }
        if(restore)return;

        string label=Config.PoiProfiles.TryGetValue(phase.PoiId,out var poi)&&poi.Boss!=""?poi.Boss:phase.PoiId;
        Note($"{label} · 第{phase.Phase}段「{phase.Name}」：{phase.Telegraph}");
        int ownerId=owner==null?-1:CombatId(owner);
        var at=owner?.Position??room.Center;
        if(activated>0)PushVisual(CombatVisualKind.Summon,phase.SkillId,phase.Id,ownerId,ownerId,at,at);
        else if(phase.SkillId!="")PushVisual(CombatVisualKind.Cast,phase.SkillId,phase.Id,ownerId,ownerId,at,at);
    }
}
