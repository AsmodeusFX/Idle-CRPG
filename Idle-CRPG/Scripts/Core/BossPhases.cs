using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>Boss 阶段触发方式。四种都必须由真实战斗/地图状态驱动，不提供"手点机关"入口。</summary>
public enum BossPhaseTrigger
{
    /// <summary>进入战斗、警觉窗口结束后的第一次判定（安达利尔/都瑞尔的开场形态）。</summary>
    FightStart,
    /// <summary>Boss 本体生命百分比阈值（Boss 战斗状态切换）。</summary>
    HpPct,
    /// <summary>遭遇序列：房间里所有已激活敌人死亡后推进到下一段（议会→墨菲斯托）。</summary>
    Sequence,
    /// <summary>地图阶段：沿 P0 原有楼层推进（混沌避难所六层），进入该层 Boss 房即生效。</summary>
    FloorStage
}

/// <summary>
/// 一条 Boss 阶段定义。只描述"在什么状态切换、切换后多出哪些技能/Buff/随从"，
/// 不携带怪物等级、刷怪数量、伤害或奖励的第二权威。
/// <para>
/// <c>Role</c>：<c>add</c> 阶段随从；<c>successor</c> 阶段接替的新 Boss（遭遇序列专用，全表唯一）。
/// <c>EscortCount</c> 是阶段召援上限：加载期预留槽位，激活一次后不再补员。
/// </para>
/// </summary>
public sealed record BossPhase(
    string Id,int Chapter,string PoiId,int Phase,BossPhaseTrigger Trigger,int Threshold,string Scope,
    string Name,string SkillId,string BuffId,string EscortTemplate,int EscortCount,string Role,string Telegraph)
{
    public bool HasEscort => EscortTemplate!="";
    public bool IsSuccessor => Role=="successor";
    /// <summary>该阶段是否只在存在 Boss 本体时生效。</summary>
    public bool NeedsBoss => Scope=="boss";
    public string TableKey => $"{Chapter}:{PoiId}";
}

public sealed partial class Config
{
    /// <summary>
    /// key = <c>章:POI</c> → 按阶段号升序的阶段表。
    /// POI ID 只在章内唯一（多个章都有 A0x-C01），所以主键必须带章号；没有该键时完全没有阶段行为。
    /// </summary>
    public Dictionary<string,List<BossPhase>> BossPhases { get; private set; }=new(StringComparer.Ordinal);

    private static string BossPhaseTableKey(int chapter,string poiId)=>$"{chapter}:{poiId}";

    public IReadOnlyList<BossPhase> BossPhasesOf(int chapter,string poiId)
        =>BossPhases.TryGetValue(BossPhaseTableKey(chapter,poiId),out var list)?list:Array.Empty<BossPhase>();

    public BossPhase? BossPhaseOf(int chapter,string poiId,int phase)
        =>BossPhases.TryGetValue(BossPhaseTableKey(chapter,poiId),out var list)?list.FirstOrDefault(p=>p.Phase==phase):null;

    /// <summary>读取 Boss 阶段表；文件缺失表示本轮不投放阶段能力，四章末 Boss 保持原行为。</summary>
    private void LoadBossPhases(string directory)
    {
        BossPhases=new(StringComparer.Ordinal);
        string path=Path.Combine(directory,"monster/boss_phase.csv");
        if(!File.Exists(path))return;
        var table=CsvTable.Load(path,File.ReadAllText(path),"id","chapter","poi_id","phase","trigger","threshold","scope","name","skill_id","buff_id","escort_template","escort_count","role","telegraph");
        foreach(var r in table.Rows)
        {
            int chapter=r.Integer("chapter");
            if(chapter is <1 or >5)throw r.Error("chapter","章号必须在1～5之间");
            string poi=r.Required("poi_id");
            int phase=r.Integer("phase");
            var trigger=r.Required("trigger") switch
            {
                "fight_start"=>BossPhaseTrigger.FightStart,
                "hp_pct"=>BossPhaseTrigger.HpPct,
                "sequence"=>BossPhaseTrigger.Sequence,
                "floor_stage"=>BossPhaseTrigger.FloorStage,
                _=>throw r.Error("trigger","阶段触发方式仅支持 fight_start/hp_pct/sequence/floor_stage")
            };
            int threshold=r.Integer("threshold");
            string scope=r.Required("scope"),name=r.Required("name"),skill=r["skill_id"],buff=r["buff_id"];
            string template=r["escort_template"],telegraph=r.Required("telegraph");
            string role=r["role"];
            int count=r.Integer("escort_count");
            if(scope is not ("boss" or "room"))throw r.Error("scope","阶段作用域仅支持 boss/room");
            if(scope=="room"&&trigger!=BossPhaseTrigger.FloorStage)throw r.Error("scope","只有地图阶段可以只按楼层推进（room）");
            if(skill!=""&&!Skills.Skills.ContainsKey(skill))throw r.Error("skill_id","阶段技能引用无效");
            if(buff!=""&&!Skills.Buffs.ContainsKey(buff))throw r.Error("buff_id","阶段Buff引用无效");
            if(template!=""&&FindMonsterSpec(template)==null)throw r.Error("escort_template","阶段随从/接替身份引用无效");
            if(count is <0 or >6)throw r.Error("escort_count","阶段召援上限必须在0～6之间");
            if(role is not ("" or "add" or "successor"))throw r.Error("role","阶段角色仅支持 add/successor");
            if(role=="add"&&(template==""||count<1))throw r.Error("escort_template","阶段随从必须声明身份与1～6个名额");
            if(role==""&&template!="")throw r.Error("role","声明了随从身份就必须声明 role=add 或 successor");
            if(role!="add"&&template==""&&count!=0)throw r.Error("escort_count","没有随从身份时名额必须为0");
            switch(trigger)
            {
                case BossPhaseTrigger.FightStart:if(threshold!=0)throw r.Error("threshold","战斗开始阶段阈值必须为0");break;
                case BossPhaseTrigger.HpPct:if(threshold is <1 or >95)throw r.Error("threshold","血量阈值必须在1～95之间");break;
                case BossPhaseTrigger.Sequence:if(threshold!=0)throw r.Error("threshold","遭遇序列阶段阈值必须为0");break;
                case BossPhaseTrigger.FloorStage:if(threshold is <1 or >20)throw r.Error("threshold","地图阶段阈值必须是1～20的楼层号");break;
            }
            var definition=new BossPhase(r.Id,chapter,poi,phase,trigger,threshold,scope,name,skill,buff,template,count,role,telegraph);
            string key=BossPhaseTableKey(chapter,poi);
            if(!BossPhases.TryGetValue(key,out var list))BossPhases[key]=list=new();
            if(list.Any(p=>p.Phase==phase))throw r.Error("phase","同一章同一POI阶段号重复");
            list.Add(definition);
        }
        foreach(var pair in BossPhases)ValidateBossPhaseChain(pair.Key,pair.Value);
    }

    /// <summary>阶段必须连续、阈值单调、接替 Boss 唯一且位于最后一段；不满足即为非法内容。</summary>
    private static void ValidateBossPhaseChain(string key,List<BossPhase> list)
    {
        list.Sort((a,b)=>a.Phase.CompareTo(b.Phase));
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException($"Boss阶段：{key} {why}");}
        for(int i=0;i<list.Count;i++)Need(list[i].Phase==i+1,$"阶段号必须从1连续，缺第{i+1}段");
        var fightStarts=list.Where(p=>p.Trigger==BossPhaseTrigger.FightStart).ToArray();
        Need(fightStarts.Length<=1,"最多允许一段战斗开始阶段");
        Need(fightStarts.Length==0||fightStarts[0].Phase==1,"战斗开始阶段必须是第1段");
        var hp=list.Where(p=>p.Trigger==BossPhaseTrigger.HpPct).ToArray();
        for(int i=1;i<hp.Length;i++)Need(hp[i].Threshold<hp[i-1].Threshold,"血量阈值必须逐段降低");
        var floors=list.Where(p=>p.Trigger==BossPhaseTrigger.FloorStage).ToArray();
        for(int i=1;i<floors.Length;i++)Need(floors[i].Threshold>floors[i-1].Threshold,"地图阶段楼层必须逐段提高");
        var successors=list.Where(p=>p.IsSuccessor).ToArray();
        Need(successors.Length<=1,"同一POI最多允许一个接替Boss");
        Need(successors.Length==0||successors[0].Phase==list[^1].Phase,"接替Boss必须位于最后一段");
        Need(successors.All(p=>p.Trigger==BossPhaseTrigger.Sequence),"接替Boss只允许由遭遇序列触发");
        Need(list.Sum(p=>p.EscortCount)<=10,"同一POI阶段召援总名额不得超过10");
    }

    /// <summary>章节装载完成后才能确认 POI 与楼层存在；缺失即报错，不静默忽略阶段配置。</summary>
    private void ValidateBossPhases()
    {
        foreach(var pair in BossPhases)
        {
            var first=pair.Value[0];
            var map=ChapterMaps[first.Chapter-1];
            if(!map.Dungeons.Any(d=>d.Id==first.PoiId))throw new InvalidDataException($"Boss阶段：第{first.Chapter}章不存在 POI {first.PoiId}");
            int maxFloor=map.FloorPlans.Where(p=>p.Poi==first.PoiId).Select(p=>p.Index).DefaultIfEmpty(0).Max();
            if(maxFloor==0)throw new InvalidDataException("Boss阶段：POI 缺少楼层规划 "+first.PoiId);
            foreach(var phase in pair.Value)
                if(phase.Trigger==BossPhaseTrigger.FloorStage&&phase.Threshold>maxFloor)
                    throw new InvalidDataException($"Boss阶段：{first.PoiId} 第{phase.Phase}段楼层 {phase.Threshold} 超出 {maxFloor}");
        }
    }
}
