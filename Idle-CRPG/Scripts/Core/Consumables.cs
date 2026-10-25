using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>单体攻击卷轴：真实伤害由统一伤害管线结算，库存恰好 -1；失败不扣。</summary>
public sealed record ScrollSpec(string Id,string Name,double DamagePct,double DropChance);
/// <summary>主动药剂：生效期间仍占槽，到期解除并清空原槽；暂停/关闭不流逝。</summary>
public sealed record PotionSpec(string Id,string Name,int DurationTicks,double AttackPct,double DropChance);

public sealed record PotionSlotSave(string ItemId,bool Active,int Remaining);
/// <summary>UI 只读视图：不暴露可变状态。</summary>
public sealed record PotionSlotView(string ItemId,string Name,bool Active,double RemainingSeconds);

public sealed partial class Config
{
    public List<ScrollSpec> Scrolls=new();
    public List<PotionSpec> Potions=new();
    public ScrollSpec ScrollOf(string id)=>Scrolls.Single(s=>s.Id==id);
    public PotionSpec PotionOf(string id)=>Potions.Single(p=>p.Id==id);
    public void LoadConsumables(string root)
    {
        Scrolls.Clear();Potions.Clear();
        var scrolls=CsvTable.Load("item/scroll.csv",File.ReadAllText(Path.Combine(root,"item/scroll.csv")),"id","name","damage_pct","drop_chance");
        foreach(var r in scrolls.Rows)Scrolls.Add(new(r.Id,r.Required("name"),r.Number("damage_pct"),r.Number("drop_chance")));
        var potions=CsvTable.Load("item/potion.csv",File.ReadAllText(Path.Combine(root,"item/potion.csv")),"id","name","duration_ticks","attack_pct","drop_chance");
        foreach(var r in potions.Rows)Potions.Add(new(r.Id,r.Required("name"),r.Integer("duration_ticks"),r.Number("attack_pct"),r.Number("drop_chance")));
        if(Scrolls.Count==0||Scrolls.Any(s=>!(s.DamagePct>0)||s.DropChance is <0 or >1))throw new InvalidDataException("卷轴定义无效");
        if(Potions.Count==0||Potions.Any(p=>p.DurationTicks<1||p.AttackPct<=0||p.DropChance is <0 or >1))throw new InvalidDataException("药剂定义无效");
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
    public IReadOnlyList<PotionSlotView> PotionSlots=>potionSlots.Select(s=>new PotionSlotView(s.ItemId,s.ItemId==""?"":Config.PotionOf(s.ItemId).Name,s.Active,s.Remaining/20d)).ToArray();
    /// <summary>生效中药剂的队伍攻击百分比加成；同类药剂同时只有一瓶生效。</summary>
    public double PotionAttackPct=>potionSlots.Where(s=>s.Active).Sum(s=>Config.PotionOf(s.ItemId).AttackPct);

    /// <summary>击杀掉落消耗品：[H] 概率来自配置；卷轴进库存，药剂进第一个空槽，满槽不自动覆盖。</summary>
    internal void TryDropConsumable(Actor monster)
    {
        if(Config.Scrolls.Count>0)
        {
            var scroll=Config.Scrolls[lootRandom.Next(0,Config.Scrolls.Count)];
            if(lootRandom.NextDouble()<scroll.DropChance){scrollStock[scroll.Id]=scrollStock.GetValueOrDefault(scroll.Id)+1;MarkChanged();}
        }
        if(Config.Potions.Count==0)return;
        var potion=Config.Potions[lootRandom.Next(0,Config.Potions.Count)];
        if(lootRandom.NextDouble()>=potion.DropChance)return;
        int empty=potionSlots.FindIndex(s=>s.ItemId=="");
        // 满槽不覆盖：直接放弃本次掉落（不静默替换玩家已有的药剂）。
        if(empty<0)return;
        potionSlots[empty]=(potion.Id,false,0);MarkChanged();
    }

    /// <summary>仅测试/自动化：直接发放消耗品，用于构造 UI 与边界场景；不开放为玩家入口。</summary>
    internal void QaGrantConsumable(string id)
    {
        if(Config.Scrolls.Any(s=>s.Id==id)){scrollStock[id]=scrollStock.GetValueOrDefault(id)+1;MarkChanged();return;}
        var potion=Config.PotionOf(id);int empty=potionSlots.FindIndex(s=>s.ItemId=="");
        if(empty<0)return;potionSlots[empty]=(potion.Id,false,0);MarkChanged();
    }
    /// <summary>对当前房间的一个怪物使用卷轴：目标非法/无库存拒绝且不扣；成功造成真实伤害后恰好扣 1。</summary>
    public bool UseScroll(string itemId,int monsterIndex,out string reason)
    {
        if(!scrollStock.TryGetValue(itemId,out int count)||count<=0){reason="卷轴库存不足";return false;}
        if(Floors.Count==0||CurrentRoom<0||CurrentRoom>=Floor.Rooms.Count){reason="当前没有战斗房间";return false;}
        var room=Floor.Rooms[CurrentRoom];
        if(monsterIndex<0||monsterIndex>=room.Monsters.Count||!room.Monsters[monsterIndex].Alive){reason="目标已失效，请选择当前存活的敌人";return false;}
        var attacker=Heroes.FirstOrDefault(h=>h.Alive);if(attacker==null){reason="没有可施放卷轴的英雄";return false;}
        var spec=Config.ScrollOf(itemId);
        var damage=SettleDamage(attacker,room.Monsters[monsterIndex],itemId,spec.DamagePct,DamageType.Physical,true);
        if(damage==null){reason="卷轴未能造成伤害";return false;}
        scrollStock[itemId]=count-1;if(count-1==0)scrollStock.Remove(itemId);
        Note($"使用{spec.Name}，对 {room.Monsters[monsterIndex].Name} 造成 {damage.FinalDamage:0} 伤害");
        MarkChanged();reason="";return true;
    }

    /// <summary>使用某一槽的药剂：槽内为空/已生效/同种已有生效瓶时拒绝；生效期间仍占槽。</summary>
    public bool UsePotion(int slot,out string reason)
    {
        if(slot<0||slot>=potionSlots.Count||potionSlots[slot].ItemId==""){reason="该槽没有药剂";return false;}
        var (itemId,active,_)=potionSlots[slot];
        if(active){reason="该药剂正在生效中";return false;}
        if(potionSlots.Any(s=>s.Active&&s.ItemId==itemId)){reason="同种药剂已有生效瓶，请等待其结束后再使用";return false;}
        potionSlots[slot]=(itemId,true,Config.PotionOf(itemId).DurationTicks);
        Note($"饮用药剂：{Config.PotionOf(itemId).Name}");MarkChanged();reason="";return true;
    }
    /// <summary>丢弃待用药剂以腾槽；生效中默认不可丢弃（避免"腾槽保 Buff"），待决策前保持禁用并说明原因。</summary>
    public bool DiscardPotion(int slot,out string reason)
    {
        if(slot<0||slot>=potionSlots.Count||potionSlots[slot].ItemId==""){reason="该槽没有药剂";return false;}
        if(potionSlots[slot].Active){reason="生效中不可丢弃：产品尚未裁决是否允许提前结束效果";return false;}
        potionSlots[slot]=("",false,0);MarkChanged();reason="";return true;
    }
    /// <summary>按逻辑步推进药剂计时；暂停/关闭期间不 Tick，因此自动冻结；到期清空原槽。</summary>
    internal void AdvanceConsumables()
    {
        for(int i=0;i<potionSlots.Count;i++)
        {
            var (itemId,active,remaining)=potionSlots[i];
            if(!active)continue;
            remaining--;
            if(remaining<=0){potionSlots[i]=("",false,0);Note($"药剂效果结束：{Config.PotionOf(itemId).Name}");MarkChanged();}
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
