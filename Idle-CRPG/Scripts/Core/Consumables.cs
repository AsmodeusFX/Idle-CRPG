using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>
/// 卷轴原型：五种快捷品类共用一套"施放→统一伤害管线→恰好扣 1"的消费路径。
/// <para>
/// <c>Effect</c>：<c>damage</c> 单体、<c>chain</c> 多目标连锁（不超过 chain_targets 个不同敌人）、
/// <c>aoe</c> 目标点固定半径单次脉冲、<c>slow</c> 单体伤害+有限时减速、<c>protect</c> 全队短期防护。
/// <c>Target</c>：<c>enemy</c> 需要合法敌人；<c>party</c> 需要战斗中的队伍（不要求点选敌人）。
/// 伤害/半径/连锁数均为 [H] 原型，留待 C3 调整。
/// </para>
/// </summary>
public sealed record ScrollSpec(string Id,string Name,string Effect,double DamagePct,DamageType Element,float Radius,int ChainTargets,string BuffId,string Target,double DropChance)
{
    public bool TargetsParty=>Target=="party";
}
/// <summary>
/// 主动药剂原型：生效期间仍占槽，到期解除并清空原槽；暂停/关闭不流逝。
/// <c>Effect</c>：<c>attack</c> 队伍攻击、<c>reduce_damage</c> 队伍受到伤害减免、
/// <c>kill_gold</c> 仅活跃 Tick 的合法击杀金币、<c>swift</c> 仅非战斗行进速度。
/// </summary>
public sealed record PotionSpec(string Id,string Name,string Effect,int DurationTicks,double Value,double DropChance);

public sealed record PotionSlotSave(string ItemId,bool Active,int Remaining);
/// <summary>UI 只读视图：不暴露可变状态。</summary>
public sealed record PotionSlotView(string ItemId,string Name,bool Active,double RemainingSeconds,string Effect="");

public sealed partial class Config
{
    public List<ScrollSpec> Scrolls=new();
    public List<PotionSpec> Potions=new();
    public ScrollSpec ScrollOf(string id)=>Scrolls.Single(s=>s.Id==id);
    public PotionSpec PotionOf(string id)=>Potions.Single(p=>p.Id==id);
    public void LoadConsumables(string root)
    {
        Scrolls.Clear();Potions.Clear();
        var scrolls=CsvTable.Load("item/scroll.csv",File.ReadAllText(Path.Combine(root,"item/scroll.csv")),"id","name","effect","damage_pct","element","radius","chain_targets","buff_id","target","drop_chance");
        foreach(var r in scrolls.Rows)
        {
            var spec=new ScrollSpec(r.Id,r.Required("name"),r.Required("effect"),r.Number("damage_pct"),r.Enum<DamageType>("element"),
                (float)r.Number("radius"),r.Integer("chain_targets"),r["buff_id"],r.Required("target"),r.Number("drop_chance"));
            if(spec.Effect is not ("damage" or "chain" or "aoe" or "slow" or "protect"))throw r.Error("effect","卷轴效果仅支持 damage/chain/aoe/slow/protect");
            if(spec.Target is not ("enemy" or "party"))throw r.Error("target","卷轴目标仅支持 enemy/party");
            if(spec.TargetsParty!= (spec.Effect=="protect"))throw r.Error("target","只有 protect 卷轴指向队伍，其余必须指向敌人");
            if(spec.DropChance is <0 or >1||!double.IsFinite(spec.DropChance))throw r.Error("drop_chance","掉率必须在0～1之间");
            if(spec.Effect=="protect"){if(spec.DamagePct!=0)throw r.Error("damage_pct","防护卷轴不造成伤害");}
            else if(!(spec.DamagePct>0))throw r.Error("damage_pct","伤害类卷轴系数必须为正");
            if(spec.Effect=="chain"&&spec.ChainTargets is <2 or >12)throw r.Error("chain_targets","连锁卷轴目标数必须在2～12之间");
            if(spec.Effect!="chain"&&spec.ChainTargets!=0)throw r.Error("chain_targets","非连锁卷轴不得配置连锁目标数");
            if(spec.Effect=="aoe"&&!(spec.Radius>0))throw r.Error("radius","范围卷轴必须有正半径");
            if(spec.Effect!="aoe"&&spec.Radius!=0)throw r.Error("radius","非范围卷轴不得配置半径");
            if(spec.Effect is "slow" or "protect"&&spec.BuffId=="")throw r.Error("buff_id","减速/防护卷轴必须配置 Buff");
            if(spec.BuffId!=""&&!Skills.Buffs.ContainsKey(spec.BuffId))throw r.Error("buff_id","卷轴 Buff 引用无效");
            Scrolls.Add(spec);
        }
        var potions=CsvTable.Load("item/potion.csv",File.ReadAllText(Path.Combine(root,"item/potion.csv")),"id","name","effect","duration_ticks","value","drop_chance");
        foreach(var r in potions.Rows)
        {
            var spec=new PotionSpec(r.Id,r.Required("name"),r.Required("effect"),r.Integer("duration_ticks"),r.Number("value"),r.Number("drop_chance"));
            if(spec.Effect is not ("attack" or "reduce_damage" or "kill_gold" or "swift"))throw r.Error("effect","药剂效果仅支持 attack/reduce_damage/kill_gold/swift");
            if(spec.DurationTicks<1||!(spec.Value>0)||spec.DropChance is <0 or >1||!double.IsFinite(spec.Value))throw r.Error("value","药剂时长/强度/掉率无效");
            Potions.Add(spec);
        }
        if(Scrolls.Count==0)throw new InvalidDataException("卷轴定义无效");
        if(Potions.Count==0)throw new InvalidDataException("药剂定义无效");
        if(Scrolls.Count>5)throw new InvalidDataException("卷轴快捷栏固定5槽，卷轴定义不得超过5种");
    }
}

public sealed partial class GameSession
{
    /// <summary>卷轴库存（本轮资产，按物品 ID 计数）。</summary>
    private readonly Dictionary<string,int> scrollStock=new();
    /// <summary>药剂槽固定 4 个（UI 预留 8 槽布局）；每槽最多一瓶，生效中仍占槽。</summary>
    private const int PotionSlotCount=4;
    private readonly List<(string ItemId,bool Active,int Remaining)> potionSlots=Enumerable.Range(0,PotionSlotCount).Select(_=>("",false,0)).ToList();

    public IReadOnlyDictionary<string,int> ScrollStock=>scrollStock;
    public IReadOnlyList<PotionSlotView> PotionSlots=>potionSlots.Select(s=>new PotionSlotView(s.ItemId,s.ItemId==""?"":Config.PotionOf(s.ItemId).Name,s.Active,s.Remaining/20d,s.ItemId==""?"":Config.PotionOf(s.ItemId).Effect)).ToArray();
    /// <summary>生效中药剂的队伍攻击百分比加成；同类药剂同时只有一瓶生效。</summary>
    public double PotionAttackPct=>PotionEffectSum("attack");
    /// <summary>生效中药剂的队伍受伤减免（0–0.9 截断）；只作用于承伤，不改 MaxHp/当前HP 语义。</summary>
    public double PotionDamageReductionPct=>Math.Clamp(PotionEffectSum("reduce_damage"),0,0.9);
    /// <summary>生效中药剂的合法击杀金币加成（只在拾取入账点应用一次）。</summary>
    public double PotionKillGoldPct=>PotionEffectSum("kill_gold");
    /// <summary>生效中药剂的非战斗行进速度加成（不影响战斗 Tick）。</summary>
    public double PotionSwiftPct=>PotionEffectSum("swift");
    /// <summary>按效果种类求和并叠加星图"药剂效力"；同一效果只统计生效槽。</summary>
    private double PotionEffectSum(string effect)
        =>potionSlots.Where(s=>s.Active).Sum(s=>Config.PotionOf(s.ItemId).Effect==effect?Config.PotionOf(s.ItemId).Value:0)*(1+StarPotionEffectPct);

    /// <summary>
    /// 击杀掉落消耗品：优先按"实际怪级"的等级表（概率 + 品类准入，C2-B）抽取；
    /// 旧组合缺表时回退到各品类自身的原型掉率。卷轴进库存，药剂进第一个空槽，满槽不自动覆盖。
    /// </summary>
    internal void TryDropConsumable(Actor monster)
    {
        var rule=Config.DropRuleOfLevel(monster.Level);
        if(rule!=null)
        {
            if(lootRandom.NextDouble()<rule.ScrollChance)
            {
                string id=rule.ScrollIds[lootRandom.Next(0,rule.ScrollIds.Length)];
                int before=scrollStock.GetValueOrDefault(id);scrollStock[id]=before+1;
                AuditAsset("monster_drop","receive","scroll",id,1,before.ToString(),(before+1).ToString(),origin:monster.SpecId);
                MarkChanged();
            }
            if(lootRandom.NextDouble()<rule.PotionChance)
            {
                string id=rule.PotionIds[lootRandom.Next(0,rule.PotionIds.Length)];
                int slotIndex=potionSlots.FindIndex(s=>s.ItemId=="");
                // 满槽不覆盖：直接放弃本次掉落（不静默替换玩家已有的药剂）。
                if(slotIndex>=0)
                {
                    potionSlots[slotIndex]=(id,false,0);
                    AuditAsset("monster_drop","receive","potion_slot",id,1,$"slot:{slotIndex}:empty",$"slot:{slotIndex}:{id}:pending",origin:monster.SpecId);
                    MarkChanged();
                }
            }
            return;
        }
        if(Config.Scrolls.Count>0)
        {
            var scroll=Config.Scrolls[lootRandom.Next(0,Config.Scrolls.Count)];
            if(lootRandom.NextDouble()<scroll.DropChance){int before=scrollStock.GetValueOrDefault(scroll.Id);scrollStock[scroll.Id]=before+1;AuditAsset("monster_drop","receive","scroll",scroll.Id,1,before.ToString(),(before+1).ToString(),origin:monster.SpecId);MarkChanged();}
        }
        if(Config.Potions.Count==0)return;
        var potion=Config.Potions[lootRandom.Next(0,Config.Potions.Count)];
        if(lootRandom.NextDouble()>=potion.DropChance)return;
        int empty=potionSlots.FindIndex(s=>s.ItemId=="");
        // 满槽不覆盖：直接放弃本次掉落（不静默替换玩家已有的药剂）。
        if(empty<0)return;
        potionSlots[empty]=(potion.Id,false,0);AuditAsset("monster_drop","receive","potion_slot",potion.Id,1,$"slot:{empty}:empty",$"slot:{empty}:{potion.Id}:pending",origin:monster.SpecId);MarkChanged();
    }

    /// <summary>仅测试/自动化：直接发放消耗品，用于构造 UI 与边界场景；不开放为玩家入口。</summary>
    internal void QaGrantConsumable(string id)
    {
        if(Config.Scrolls.Any(s=>s.Id==id)){int before=scrollStock.GetValueOrDefault(id);scrollStock[id]=before+1;AuditAsset("qa_fixture","grant","scroll",id,1,before.ToString(),(before+1).ToString());MarkChanged();return;}
        var potion=Config.PotionOf(id);int empty=potionSlots.FindIndex(s=>s.ItemId=="");
        if(empty<0)return;potionSlots[empty]=(potion.Id,false,0);AuditAsset("qa_fixture","grant","potion_slot",id,1,$"slot:{empty}:empty",$"slot:{empty}:{id}:pending");MarkChanged();
    }
    /// <summary>沿用卷轴的首名存活英雄作为作用方；距离相同时按房间怪物索引稳定选择。</summary>
    public int NearestScrollTarget()
    {
        if(Floors.Count==0||CurrentRoom<0||CurrentRoom>=Floor.Rooms.Count)return -1;
        var caster=Heroes.FirstOrDefault(h=>h.Alive);
        if(caster==null||!InCombatRoom(caster))return -1;
        return Floor.Rooms[CurrentRoom].Monsters.Select((actor,index)=>(actor,index))
            .Where(p=>p.actor.Alive&&InCombatRoom(p.actor))
            .OrderBy(p=>System.Numerics.Vector2.DistanceSquared(caster.Position,p.actor.Position))
            .ThenBy(p=>p.index).Select(p=>p.index).DefaultIfEmpty(-1).First();
    }
    /// <summary>快捷栏原子查询当前合法最近目标，再复用唯一卷轴消费与伤害命令。</summary>
    public bool UseScrollOnNearest(string itemId,out string reason)
    {
        var spec=Config.ScrollOf(itemId);
        if(spec.TargetsParty)return UseScrollOnParty(itemId,out reason);
        return UseScroll(itemId,NearestScrollTarget(),out reason);
    }

    /// <summary>防护卷轴：要求队伍与敌人在同一战斗房（战斗中手动施加，不点选敌人）。</summary>
    public bool CanUsePartyScroll(string itemId)
    {
        if(!scrollStock.TryGetValue(itemId,out int count)||count<=0)return false;
        if(Floors.Count==0||CurrentRoom<0||CurrentRoom>=Floor.Rooms.Count)return false;
        var room=Floor.Rooms[CurrentRoom];
        return Heroes.Any(h=>h.Alive&&InCombatRoom(h))&&room.Monsters.Any(m=>m.Alive&&InCombatRoom(m));
    }

    /// <summary>对全队施加短期防护 Buff；库存恰好 -1，非法现场/无库存拒绝且不扣。</summary>
    public bool UseScrollOnParty(string itemId,out string reason)
    {
        var spec=Config.ScrollOf(itemId);
        if(!spec.TargetsParty){reason="该卷轴需要选择敌人";return false;}
        if(!scrollStock.TryGetValue(itemId,out int count)||count<=0){reason="卷轴库存不足";return false;}
        if(!CanUsePartyScroll(itemId)){reason="防护卷轴只能在战斗中对队伍使用";return false;}
        var targets=Heroes.Where(h=>h.Alive&&InCombatRoom(h)).ToArray();
        int owner=CombatId(targets[0]);
        foreach(var hero in targets)ApplyBuff(spec.BuffId,owner,CombatId(hero),itemId,1,null);
        int current=scrollStock.GetValueOrDefault(itemId);scrollStock[itemId]=current-1;if(current-1==0)scrollStock.Remove(itemId);
        AuditAsset("player_command","use","scroll",itemId,-1,current.ToString(),(current-1).ToString(),targets[0].HeroInstanceId,"target:party");
        Note($"使用{spec.Name}，全队获得短期防护");
        MarkChanged();reason="";return true;
    }

    /// <summary>
    /// 对当前房间的一个怪物使用卷轴：目标非法/无库存拒绝且不扣；成功造成真实伤害或施加真实状态后恰好扣 1。
    /// 所有伤害仍由统一伤害管线结算，不新增第二条伤害或奖励路径。
    /// </summary>
    public bool UseScroll(string itemId,int monsterIndex,out string reason)
    {
        var spec=Config.ScrollOf(itemId);
        if(spec.TargetsParty)return UseScrollOnParty(itemId,out reason);
        if(!scrollStock.TryGetValue(itemId,out int count)||count<=0){reason="卷轴库存不足";return false;}
        if(Floors.Count==0||CurrentRoom<0||CurrentRoom>=Floor.Rooms.Count){reason="当前没有战斗房间";return false;}
        var room=Floor.Rooms[CurrentRoom];
        if(monsterIndex<0||monsterIndex>=room.Monsters.Count||!room.Monsters[monsterIndex].Alive){reason="目标已失效，请选择当前存活的敌人";return false;}
        var attacker=Heroes.FirstOrDefault(h=>h.Alive);if(attacker==null){reason="没有可施放卷轴的英雄";return false;}
        var primary=room.Monsters[monsterIndex];
        // C1-B S01：卷轴威力只放大已持有的合法卷轴伤害，不生成第二个伤害源。
        double coefficient=spec.DamagePct*(1+StarScrollDamagePct);
        int hits=0;double damage=0;
        switch(spec.Effect)
        {
            case "damage":
            {
                var result=SettleDamage(attacker,primary,itemId,coefficient,spec.Element,true);
                if(result!=null){hits++;damage+=result.FinalDamage;}
                break;
            }
            case "chain":
            {
                // 只对不同的存活目标生效；目标不足时不虚构弹跳次数。
                var chain=room.Monsters.Select((actor,index)=>(actor,index))
                    .Where(p=>p.actor.Alive&&InCombatRoom(p.actor))
                    .OrderBy(p=>System.Numerics.Vector2.DistanceSquared(primary.Position,p.actor.Position))
                    .ThenBy(p=>p.index).Take(spec.ChainTargets).ToArray();
                for(int i=0;i<chain.Length;i++)
                {
                    var result=SettleDamage(attacker,chain[i].actor,itemId,coefficient*(i==0?1:Math.Pow(.8,i)),spec.Element,true);
                    if(result!=null){hits++;damage+=result.FinalDamage;}
                }
                break;
            }
            case "aoe":
            {
                var targets=room.Monsters.Where(m=>m.Alive&&InCombatRoom(m)&&System.Numerics.Vector2.Distance(m.Position,primary.Position)<=spec.Radius).ToArray();
                foreach(var target in targets)
                {
                    var result=SettleDamage(attacker,target,itemId,coefficient,spec.Element,true);
                    if(result!=null){hits++;damage+=result.FinalDamage;}
                }
                break;
            }
            case "slow":
            {
                var result=SettleDamage(attacker,primary,itemId,coefficient,spec.Element,true);
                if(result!=null)hits++;
                if(primary.Alive)ApplyBuff(spec.BuffId,CombatId(attacker),CombatId(primary),itemId,1,null);
                hits++;
                break;
            }
            default:reason="未实现的卷轴效果";return false;
        }
        if(hits==0){reason="卷轴未能造成伤害或状态";return false;}
        // 击杀卷轴目标可能在同一伤害结算中再掉卷轴；从最新库存扣一张，不能覆盖掉落收入。
        int current=scrollStock.GetValueOrDefault(itemId);scrollStock[itemId]=current-1;if(current-1==0)scrollStock.Remove(itemId);
        AuditAsset("player_command","use","scroll",itemId,-1,current.ToString(),(current-1).ToString(),attacker.HeroInstanceId,$"target:{monsterIndex}");
        Note(spec.Effect switch
        {
            "chain"=>$"使用{spec.Name}，连锁命中 {hits} 名敌人，合计 {damage:0} 伤害",
            "aoe"=>$"使用{spec.Name}，范围内 {hits} 名敌人，合计 {damage:0} 伤害",
            "slow"=>$"使用{spec.Name}，对 {primary.Name} 造成伤害并减速",
            _=>$"使用{spec.Name}，对 {primary.Name} 造成 {damage:0} 伤害"
        });
        MarkChanged();reason="";return true;
    }

    /// <summary>使用某一槽的药剂：槽内为空/已生效/同种已有生效瓶时拒绝；生效期间仍占槽。</summary>
    public bool UsePotion(int slot,out string reason)
    {
        if(slot<0||slot>=potionSlots.Count||potionSlots[slot].ItemId==""){reason="该槽没有药剂";return false;}
        var (itemId,active,_)=potionSlots[slot];
        if(active){reason="该药剂正在生效中";return false;}
        if(potionSlots.Any(s=>s.Active&&s.ItemId==itemId)){reason="同种药剂已有生效瓶，请等待其结束后再使用";return false;}
        // C1-B P01：药剂时效在"使用瞬间"按星图延长，读档按剩余Tick保留，不重复加成。
        int duration=(int)Math.Ceiling(Config.PotionOf(itemId).DurationTicks*(1+StarPotionDurationPct));
        potionSlots[slot]=(itemId,true,duration);
        AuditAsset("player_command","activate","potion_slot",itemId,0,$"slot:{slot}:{itemId}:pending",$"slot:{slot}:{itemId}:active:{duration}");
        Note($"饮用药剂：{Config.PotionOf(itemId).Name}");MarkChanged();reason="";return true;
    }
    /// <summary>丢弃待用药剂以腾槽；生效中默认不可丢弃（避免"腾槽保 Buff"），待决策前保持禁用并说明原因。</summary>
    public bool DiscardPotion(int slot,out string reason)
    {
        if(slot<0||slot>=potionSlots.Count||potionSlots[slot].ItemId==""){reason="该槽没有药剂";return false;}
        if(potionSlots[slot].Active){reason="生效中不可丢弃：产品尚未裁决是否允许提前结束效果";return false;}
        string discarded=potionSlots[slot].ItemId;potionSlots[slot]=("",false,0);AuditAsset("player_command","discard","potion_slot",discarded,-1,$"slot:{slot}:{discarded}:pending",$"slot:{slot}:empty");MarkChanged();reason="";return true;
    }
    /// <summary>按逻辑步推进药剂计时；暂停/关闭期间不 Tick，因此自动冻结；到期清空原槽。</summary>
    internal void AdvanceConsumables()
    {
        for(int i=0;i<potionSlots.Count;i++)
        {
            var (itemId,active,remaining)=potionSlots[i];
            if(!active)continue;
            remaining--;
            if(remaining<=0){potionSlots[i]=("",false,0);AuditAsset("runtime","expire","potion_slot",itemId,-1,$"slot:{i}:{itemId}:active:1",$"slot:{i}:empty");Note($"药剂效果结束：{Config.PotionOf(itemId).Name}");MarkChanged();}
            else potionSlots[i]=(itemId,true,remaining);
        }
    }
    internal List<PotionSlotSave> CapturePotions()=>potionSlots.Select(s=>new PotionSlotSave(s.ItemId,s.Active,s.Remaining)).ToList();
    internal Dictionary<string,int> CaptureScrolls()=>new(scrollStock);
    internal void RestoreConsumables(IEnumerable<KeyValuePair<string,int>> scrolls,IEnumerable<PotionSlotSave> potions)
    {
        scrollStock.Clear();
        foreach(var pair in scrolls){if(pair.Value<=0)continue;Config.ScrollOf(pair.Key);scrollStock[pair.Key]=pair.Value;}
        var slots=potions.ToArray();
        // 旧档/内存构造的快照没有消耗品字段：重建为全空槽，而不是让读档失败。
        if(slots.Length==0){for(int i=0;i<PotionSlotCount;i++)potionSlots[i]=("",false,0);return;}
        if(slots.Length!=PotionSlotCount)throw new InvalidDataException("药剂槽数量不匹配");
        int activeSameType=0;string? activeType=null;
        for(int i=0;i<slots.Length;i++)
        {
            var save=slots[i];
            if(save.ItemId==""&&(save.Active||save.Remaining!=0))throw new InvalidDataException("空槽残留药剂状态");
            if(save.ItemId==""){potionSlots[i]=("",false,0);continue;}
            var spec=Config.PotionOf(save.ItemId);
            if(save.Active)
            {
                if(save.Remaining<1||save.Remaining>spec.DurationTicks)throw new InvalidDataException("药剂剩余时间越界");
                if(activeType==save.ItemId&&activeSameType>=1)throw new InvalidDataException("同种药剂同时生效超过一瓶");
                activeType=save.ItemId;activeSameType++;
            }
            else if(save.Remaining!=0)throw new InvalidDataException("未生效药剂不应有剩余时间");
            potionSlots[i]=(save.ItemId,save.Active,save.Remaining);
        }
    }
    internal static void ValidateConsumables(Config config,IEnumerable<KeyValuePair<string,int>>? scrolls,IEnumerable<PotionSlotSave>? potions)
    {
        if(scrolls!=null)foreach(var pair in scrolls)
        {
            if(pair.Value<=0||config.Scrolls.All(s=>s.Id!=pair.Key))throw new InvalidDataException("卷轴库存无效："+pair.Key);
        }
        if(potions!=null)
        {
            var slots=potions.ToArray();
            // 旧档/内存构造的快照没有消耗品字段：按"全空槽"处理，不判非法。
            if(slots.Length==0)return;
            if(slots.Length!=PotionSlotCount)throw new InvalidDataException("药剂槽数量不匹配");
            var active=new HashSet<string>();
            foreach(var slot in slots)
            {
                if(slot.ItemId==""&&(slot.Active||slot.Remaining!=0))throw new InvalidDataException("空槽残留药剂状态");
                if(slot.ItemId=="")continue;
                var spec=config.Potions.SingleOrDefault(p=>p.Id==slot.ItemId)??throw new InvalidDataException("药剂定义不存在");
                if(slot.Active&&(slot.Remaining<1||slot.Remaining>spec.DurationTicks))throw new InvalidDataException("药剂剩余时间越界");
                if(slot.Active&&!active.Add(slot.ItemId))throw new InvalidDataException("同种药剂同时生效超过一瓶");
                if(!slot.Active&&slot.Remaining!=0)throw new InvalidDataException("未生效药剂不应有剩余时间");
            }
        }
    }
}
