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

        // V1.6 A-09：沿用绝对当前HP不变、上限下降截断；不引入等比治疗规则。
        foreach(float health in new[]{0f,1f,25f,float.MaxValue})
        {
            var hpGame=GameSession.CreateNewWorld(c,7710,new[]{"warrior","warrior"});
            var subject=hpGame.Heroes[0];var untouched=hpGame.Heroes[1];
            float baseline=subject.MaxHp;subject.Hp=Math.Min(health,baseline);
            float beforeHp=subject.Hp,otherHp=untouched.Hp;
            var hpSpec=c.EquipmentBases.First(b=>b.Attr=="hp");
            hpGame.QaOfferEquipment(subject.HeroInstanceId,hpSpec.Id,10,"legendary");
            Check(hpGame.EquipCandidate(subject.HeroInstanceId,hpSpec.Slot),"HP装备未穿上");
            Check(subject.MaxHp>baseline&&subject.Hp==beforeHp,"提高生命上限意外治疗或复活");
            hpGame.QaOfferEquipment(subject.HeroInstanceId,hpSpec.Id,1,"common");
            // 旧件留在候选：先穿低件，允许显式选择较低HP，不自动治疗。
            Check(hpGame.EquipCandidate(subject.HeroInstanceId,hpSpec.Slot),"HP装备未换下");
            Check(subject.Hp>=0&&subject.Hp<=subject.MaxHp&&subject.Hp==beforeHp,"降低HP装备破坏当前HP边界");
            var saved=hpGame.CaptureProgress();
            for(int read=0;read<2;read++)
            {
                hpGame=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=saved})).Session!);
                Check(hpGame.Heroes[0].Hp==beforeHp&&hpGame.Heroes[0].MaxHp==subject.MaxHp,"HP装备重读漂移");
                saved=hpGame.CaptureProgress();
            }
            Check(untouched.Hp==otherHp&&untouched.EquipmentHp==0,"HP装备串同职实例");
        }
        var clampGame=GameSession.CreateNewWorld(c,7711,new[]{"warrior"});
        var clampHero=clampGame.Heroes[0];var clampSpec=c.EquipmentBases.First(b=>b.Attr=="hp");
        clampGame.QaOfferEquipment(clampHero.HeroInstanceId,clampSpec.Id,10,"legendary");
        Check(clampGame.EquipCandidate(clampHero.HeroInstanceId,clampSpec.Slot),"生命截断夹具未穿戴");
        clampHero.Hp=clampHero.MaxHp;
        clampGame.QaOfferEquipment(clampHero.HeroInstanceId,clampSpec.Id,1,"common");
        Check(clampGame.EquipCandidate(clampHero.HeroInstanceId,clampSpec.Slot)&&clampHero.Hp==clampHero.MaxHp,"上限下降未截断溢出HP");
        var rescue=new GameSession(c,7712,new[]{"warrior","warrior"});var down=rescue.Heroes[0];float unmodified=down.MaxHp;
        var hpBase=c.EquipmentBases.First(b=>b.Attr=="hp");
        rescue.QaOfferEquipment(down.HeroInstanceId,hpBase.Id,10,"legendary");Check(rescue.EquipCandidate(down.HeroInstanceId,hpBase.Slot),"救援HP装备夹具");
        down.Hp=0;
        Check(rescue.QaUnequipEquipment(down.HeroInstanceId,hpBase.Slot)&&down.Hp==0&&down.MaxHp==unmodified,"阵亡卸装错误复活或残留生命");
        Check(rescue.EquipCandidate(down.HeroInstanceId,hpBase.Slot)&&down.Hp==0,"阵亡穿HP装备错误治疗");
        var safeRoom=rescue.Floor.Rooms[rescue.CurrentRoom];safeRoom.Monsters.Clear();safeRoom.ContentCleared=true;
        foreach(var member in rescue.Heroes)member.Position=safeRoom.Center;
        for(int tick=0;tick<1000&&!down.Alive;tick++)rescue.Tick(.05f);
        Check(down.Alive&&Math.Abs(down.Hp-down.MaxHp*c.ReviveHpRatio)<.01,"真实救援未按装备后的生命上限复活");
        Check(rescue.QaUnequipEquipment(down.HeroInstanceId,hpBase.Slot)&&down.Hp>=0&&down.Hp<=down.MaxHp,"复活后卸装HP越界");
        var crossConfig=Config.Load(Path.Combine(root,"Idle-CRPG/config"));var weapon=crossConfig.EquipmentBases.First(b=>b.Slot=="weapon");
        crossConfig.EquipmentBases.Add(weapon with {Id="QA_HP_WEAPON",Attr="hp"});
        var cross=GameSession.CreateNewWorld(crossConfig,7713,new[]{"warrior"});var crossHero=cross.Heroes[0];
        cross.QaOfferEquipment(crossHero.HeroInstanceId,weapon.Id,1,"common");Check(cross.EquipCandidate(crossHero.HeroInstanceId,"weapon"),"跨属性原件未穿戴");
        cross.QaOfferEquipment(crossHero.HeroInstanceId,"QA_HP_WEAPON",10,"legendary");
        Check(cross.EquipBestFor(crossHero)==0&&cross.EquipAllReplaceable()==0&&cross.SellInferiorEquipment()==0&&cross.CandidateEquipment(crossHero,"weapon")?.Attr=="hp","跨ATK/HP候选未被自动穿戴/出售保护");
        Console.WriteLine("WP5_EQUIP_PASS: 六部位实例绑定、ATK/HP 加成与读档一致、5件批量门槛、回城次优卖出与跨属性保留、HP边界/两次重读");
    }
}
