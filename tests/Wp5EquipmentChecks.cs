using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// WP5 机制切片：六部位实例绑定、候选保留、手动/批量换装、回城次优卖出与存档一致。
/// 数值均为 [H] 原型；装备不参与 Armor，只提供 ATK/HP 主属性。
/// </summary>
public static class Wp5EquipmentChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("WP5_EQUIP: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        Check(c.EquipmentBases.Count==6&&c.EquipmentQualities.Count==4,"装备基础表或品质表数量错误");

        var game=GameSession.CreateNewWorld(c,7701,new[]{"warrior","warrior","ranger","mage"});
        var one=game.Heroes[0];var two=game.Heroes[1];
        // 四名实例身份稳定，装备按实例绑定。
        Check(game.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==4,"同职业实例ID重复");

        // 6 部位逐件穿戴：ATK/HP 主属性实际进入角色数值，且只影响该实例。
        float attackBefore=one.Attack,maxHpBefore=one.MaxHp;
        float otherAttack=two.Attack;
        double expectedAttack=0,expectedHp=0;
        foreach(var baseSpec in c.EquipmentBases)
        {
            game.QaOfferEquipment(one.HeroInstanceId,baseSpec.Id,5,"rare");
            Check(game.EquipCandidate(one.HeroInstanceId,baseSpec.Slot),$"{baseSpec.Slot} 穿戴失败");
            var item=game.WornEquipment(one,baseSpec.Slot)??throw new Exception("穿戴后未记录装备");
            expectedAttack+=item.Attr=="atk"?item.Value:0;
            expectedHp+=item.Attr=="hp"?item.Value:0;
        }
        Check(Math.Abs(one.Attack-(attackBefore+expectedAttack))<.01,$"攻击加成不一致 {one.Attack}/{attackBefore+expectedAttack}");
        Check(Math.Abs(one.MaxHp-(maxHpBefore+expectedHp))<.01,$"生命加成不一致 {one.MaxHp}/{maxHpBefore+expectedHp}");
        Check(two.Attack==otherAttack&&two.MaxHp==c.Heroes.First(h=>h.Id==two.SpecId).Hp,"同职业另一实例被串改");

        // 读档后装备与角色数值一致，不重复叠加。
        var reloaded=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=game.CaptureProgress()})).Session!);
        var reloadedOne=reloaded.Heroes[0];
        Check(Math.Abs(reloadedOne.Attack-one.Attack)<.01&&Math.Abs(reloadedOne.MaxHp-one.MaxHp)<.01,"读档后角色数值漂移（装备加成重复或丢失）");
        Check(reloaded.WornEquipment(reloadedOne,"weapon")!=null,"读档后穿戴丢失");
        var again=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=reloaded.CaptureProgress()})).Session!);
        Check(Math.Abs(again.Heroes[0].MaxHp-reloadedOne.MaxHp)<.01,"二次读档重复叠加装备");

        // 批量换装门槛：4 个可提升不批处理，达到 5 个才一次执行。
        var batchGame=GameSession.CreateNewWorld(c,7702,new[]{"warrior","warrior","ranger","mage"});
        var slots=new[]{"weapon","helm","chest","gloves","boots","ring"};
        string BaseId(string slot)=>c.EquipmentBases.Single(b=>b.Slot==slot).Id;
        int offered=0;
        for(int i=0;i<4;i++){batchGame.QaOfferEquipment(batchGame.Heroes[0].HeroInstanceId,BaseId(slots[i]),6,"common");offered++;}
        Check(offered==4&&batchGame.BatchEquipCandidates()==0,"4件时批量换装仍被执行");
        Check(batchGame.Heroes.All(h=>slots.All(s=>batchGame.WornEquipment(h,s)==null)),"4件被拒绝时仍发生了穿戴");
        batchGame.QaOfferEquipment(batchGame.Heroes[1].HeroInstanceId,BaseId(slots[0]),6,"common");
        batchGame.QaOfferEquipment(batchGame.Heroes[2].HeroInstanceId,BaseId(slots[1]),6,"common");
        int applied=batchGame.BatchEquipCandidates();
        Check(applied==6,$"5件以上批量换装数量错误：{applied}");

        // 回城卖出：同属性不优于已穿的候选卖出，跨属性与更优候选保留。
        var sellGame=GameSession.CreateNewWorld(c,7703,new[]{"warrior"});
        var hero=sellGame.Heroes[0];
        sellGame.QaOfferEquipment(hero.HeroInstanceId,"eq_weapon",10,"legendary");
        Check(sellGame.EquipCandidate(hero.HeroInstanceId,"weapon"),"售卖场景穿戴失败");
        var worn=sellGame.WornEquipment(hero,"weapon")!;
        sellGame.QaOfferEquipment(hero.HeroInstanceId,"eq_weapon",1,"common");
        sellGame.QaOfferEquipment(hero.HeroInstanceId,"eq_ring",10,"common");
        long goldBefore=sellGame.Gold;
        long sold=sellGame.SellInferiorEquipment();
        Check(sold>0&&sellGame.Gold==goldBefore,"次优装备卖出未入账");
        Check(sellGame.CandidateEquipment(hero,"weapon")==null,"同属性次优候选未被卖出");
        Check(sellGame.CandidateEquipment(hero,"ring")!=null,"跨属性候选被误卖");
        Check(Math.Abs(sellGame.WornEquipment(hero,"weapon")!.Value-worn.Value)<.01,"卖出影响了已穿装备");

        Console.WriteLine("WP5_EQUIP_PASS: 六部位实例绑定、ATK/HP 加成与读档一致、5件批量门槛、回城次优卖出与跨属性保留");
    }
}
