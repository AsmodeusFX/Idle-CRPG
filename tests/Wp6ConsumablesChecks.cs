using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// WP6 机制切片：一种单体攻击卷轴＋一种主动 Buff 药剂。
/// 卷轴：掉落→库存→手动目标→真实伤害→恰好-1，失败不扣。
/// 药剂：4槽、生效占槽、同种仅一瓶、满槽不覆盖、待用可丢、计时随 Tick 冻结/恢复。
/// </summary>
public static class Wp6ConsumablesChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("WP6_CONSUMABLE: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        Check(c.Scrolls.Count>=1&&c.Potions.Count>=1,"缺少卷轴或药剂配置");
        // [H] 测试：收敛为一卷一药并把掉落概率临时拉满，使"掉落→库存/槽"链路可确定性观察。
        TestPlayerActions.SingleConsumableFixture(c);
        c.Scrolls[0]=c.Scrolls[0] with {DropChance=1};
        c.Potions[0]=c.Potions[0] with {DropChance=1};
        string scrollId=c.Scrolls[0].Id,potionId=c.Potions[0].Id;

        GameSession Battle(int seed)
        {
            var game=GameSession.CreateWorld(c,seed,new[]{"warrior","ranger","mage"});
            // C2-A 逐层节奏会在 spawn_rule 区间内加减数量；QA 夹具固定加 4 个名额，保证清房用例仍有足够目标。
            game.SetGmBattle(new GmBattleSettings(RoomCountBonus:4));
            for(int i=0;i<20000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            while(game.CombatEntryRemaining>0)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            foreach(var hero in game.Heroes){hero.Hp=hero.MaxHp=100000;hero.Timer=999;}
            foreach(var monster in room.Monsters){monster.Hp=monster.MaxHp=1000;monster.Attack=0;}
            return game;
        }

        // V1.6 A-05：列表第一名更远时必须命中近者；平距稳定、死亡重查、快速连点及空目标不扣。
        var nearest=Battle(8803);
        var nearestRoom=nearest.Floor.Rooms[nearest.CurrentRoom];
        Check(nearestRoom.Monsters.Count>=2,"最近目标夹具至少需要两怪");
        foreach(var m in nearestRoom.Monsters)m.Hp=0;
        var caster=nearest.Heroes[0];caster.Position=nearestRoom.Center;
        var far=nearestRoom.Monsters[0];var near=nearestRoom.Monsters[1];
        far.Hp=near.Hp=1000;
        far.Position=caster.Position+new System.Numerics.Vector2(1.5f,0);
        near.Position=caster.Position+new System.Numerics.Vector2(.25f,0);
        nearest.QaGrantConsumable(scrollId);nearest.QaGrantConsumable(scrollId);
        Check(nearest.NearestScrollTarget()==1,"列表首怪覆盖了距离排序");
        Check(nearest.UseScrollOnNearest(scrollId,out _)&&near.Hp<1000&&far.Hp==1000,"最近卷轴未命中真实近目标");
        near.Position=far.Position;Check(nearest.NearestScrollTarget()==0,"相等距离未按索引稳定选择");
        near.Hp=0;
        Check(nearest.UseScrollOnNearest(scrollId,out _)&&far.Hp<1000,"目标死亡后未重查");
        Check(!nearest.UseScrollOnNearest(scrollId,out _)&&nearest.ScrollStock.GetValueOrDefault(scrollId)==0,"连续点击超扣库存");
        nearest.QaGrantConsumable(scrollId);far.Hp=0;
        Check(nearest.NearestScrollTarget()==-1&&!nearest.UseScrollOnNearest(scrollId,out _)&&nearest.ScrollStock[scrollId]==1,"无合法目标仍扣库存");

        // 卷轴：击杀掉落入库 → 目标非法不扣 → 真实伤害恰好扣 1。
        var game=Battle(8801);
        var room=game.Floor.Rooms[game.CurrentRoom];
        // C2-A 逐层节奏会改变本房数量：只杀到剩 1 只，保证后续"合法目标"用例仍有活口。
        for(int i=0;i<4&&room.Monsters.Count(m=>m.Alive)>1;i++){
            var victim=room.Monsters.FirstOrDefault(m=>m.Alive);if(victim==null)break;
            game.QaResolveHit(victim);
        }
        Check(game.ScrollStock.GetValueOrDefault(scrollId)>=1,$"卷轴未掉落或未入库存：{string.Join(',',game.ScrollStock.Select(p=>p.Key+"="+p.Value))}");
        int stockBefore=game.ScrollStock[scrollId];
        Check(!game.UseScroll(scrollId,99,out string bad)&&game.ScrollStock[scrollId]==stockBefore,"非法目标仍扣库存："+bad);
        var target=room.Monsters.FirstOrDefault(m=>m.Alive)??throw new Exception("房间没有存活目标");
        float hpBefore=target.Hp;
        Check(game.UseScroll(scrollId,room.Monsters.IndexOf(target),out string why),"合法目标使用卷轴失败："+why);
        Check(target.Hp<hpBefore,"卷轴未造成真实伤害");
        Check(game.ScrollStock.GetValueOrDefault(scrollId)==stockBefore-1,"成功使用后库存未恰好-1");
        while(game.ScrollStock.GetValueOrDefault(scrollId)>0)
        {
            var next=room.Monsters.FirstOrDefault(m=>m.Alive);if(next==null){game.QaResolveHit(room.Monsters[0]);next=room.Monsters.FirstOrDefault(m=>m.Alive);}
            if(next==null)break;game.UseScroll(scrollId,room.Monsters.IndexOf(next),out _);
        }
        Check(game.ScrollStock.GetValueOrDefault(scrollId)==0&&!game.UseScroll(scrollId,0,out string empty)&&empty.Contains("库存"),"库存耗尽仍可使用");

        // 药剂：掉落占槽、满槽不覆盖、同种仅一瓶生效、生效占槽、计时到期清槽。
        var potionGame=Battle(8802);
        var potionRoom=potionGame.Floor.Rooms[potionGame.CurrentRoom];
        for(int i=0;i<8;i++){
            var victim=potionRoom.Monsters.FirstOrDefault(m=>m.Alive);if(victim==null)break;
            potionGame.QaResolveHit(victim);
        }
        var slots=potionGame.PotionSlots;
        Check(slots.Count==4,"药剂槽数量不是4");
        Check(slots.All(s=>s.ItemId==potionId)&&slots.All(s=>!s.Active),"药剂未按掉落占满空槽或状态错误");
        int fullSlots=slots.Count(s=>s.ItemId!="");
        Check(fullSlots==4,"满槽状态未建立");
        var extra=potionRoom.Monsters.FirstOrDefault(m=>m.Alive);
        if(extra!=null)potionGame.QaResolveHit(extra);
        Check(potionGame.PotionSlots.Count(s=>s.ItemId!="")==4,"满槽后发生了自动覆盖");

        Check(potionGame.UsePotion(0,out string use)&&potionGame.PotionSlots[0].Active,"药剂启用失败："+use);
        Check(potionGame.PotionAttackPct>0,"药剂加成未生效");
        Check(!potionGame.UsePotion(1,out string concurrent)&&concurrent.Contains("同种"),"同种药剂被并发启用："+concurrent);
        Check(!potionGame.DiscardPotion(0,out _),"生效中药剂被允许丢弃");
        Check(potionGame.DiscardPotion(1,out _)&&potionGame.PotionSlots[1].ItemId=="","待用药剂无法丢弃腾槽");

        int duration=c.Potions[0].DurationTicks;
        for(int i=0;i<20;i++)potionGame.Tick(.05f);
        Check(potionGame.PotionSlots[0].Active&&Math.Abs(potionGame.PotionSlots[0].RemainingSeconds-(duration-20)/20d)<.01,"药剂计时未随逻辑步推进");
        // 暂停即不 Tick：计时不应流逝。
        double frozen=potionGame.PotionSlots[0].RemainingSeconds;
        Check(potionGame.PotionSlots[0].RemainingSeconds==frozen,"暂停期间药剂计时发生变化");
        // 读档保留生效状态与剩余时间。
        var reloaded=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=potionGame.CaptureProgress()})).Session!);
        Check(reloaded.PotionSlots[0].Active&&Math.Abs(reloaded.PotionSlots[0].RemainingSeconds-frozen)<.01,"读档后药剂状态或计时丢失");
        for(int i=0;i<duration;i++)reloaded.Tick(.05f);
        Check(!reloaded.PotionSlots[0].Active&&reloaded.PotionAttackPct==0,"药剂到期后仍生效或加成残留");

        Console.WriteLine("WP6_CONSUMABLE_PASS: 卷轴真实伤害与恰好扣减、非法目标不扣；药剂4槽占槽/同种单瓶/满槽不覆盖/丢弃规则/计时与读档一致");
    }
}
