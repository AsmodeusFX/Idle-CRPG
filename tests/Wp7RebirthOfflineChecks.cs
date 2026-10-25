using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// WP7 机制切片：正式重生门槛与事务、放弃本轮区分、有限离线结算与幂等封顶、药剂时间冻结。
/// 账号奖励仅记录 QA/H 事实，不发放正式成就点，不开放第五席/商店。
/// </summary>
public static class Wp7RebirthOfflineChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("WP7_REBIRTH: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var game=GameSession.CreateNewWorld(c,9901,new[]{"warrior"});
        game.GrantGmResources(gold:500,experience:5000);
        game.UpgradeHero(game.Heroes[0].HeroInstanceId,out _);

        // 未通关第4章：拒绝重生；放弃本轮不产生账号事实。
        Check(!game.CanRebirth(out string locked)&&locked.Contains("第4章"),"未通关时未拒绝重生："+locked);
        var abandonAccount=game.AbandonRun();
        Check(!abandonAccount.AchievementFacts.Any(f=>f.StartsWith("rebirth:")),"放弃本轮产生了重生事实");

        // 标记第4章主线完成后：正式重生只结算永久事实，Run 资产由新轮次重建。
        string mainPoi=c.ChapterMaps[3].MainClearPoi;
        game.World!.Progress.Cleared.Add(mainPoi);
        Check(game.CanRebirth(out _),"通关后仍无法重生");
        long ordinalBefore=game.Account.RunOrdinal;
        Check(game.Rebirth(out AccountState account,out string why),$"重生失败：{why}");
        Check(game.Retired,"重生后旧轮次未标记为退役");
        Check(account.RunOrdinal==ordinalBefore+1&&account.AchievementFacts.Contains("ch4-cleared")&&account.AchievementFacts.Any(f=>f.StartsWith("rebirth:")),"账号权益或结算事实未记录");
        Check(account.AchievementPoints==0,"测试阶段擅自发放了正式成就点");
        var next=GameSession.StartRun(c,9902,new[]{"ranger"},account);
        Check(next.PublicExperience==0&&next.Kp==0&&next.Gold==0&&next.EquipmentTotals(next.Heroes[0]).Attack==0&&next.PotionSlots.All(s=>s.ItemId=="")&&next.ScrollStock.Count==0,"新轮次未清零本轮资产");
        Check(next.Account.RunOrdinal>=account.RunOrdinal&&account.AchievementFacts.All(f=>next.Account.AchievementFacts.Contains(f)),"新轮次丢失账号权益");
        Check(next.Stars.Level("s_root")==0&&next.Heroes[0].Level==1,"新轮次继承了星图或等级");

        // 有限离线结算：封顶、只结算三类资源、不自动升级、幂等。
        var offline=GameSession.CreateNewWorld(c,9903,new[]{"warrior"});
        var snapshot=offline.CaptureProgress();
        snapshot.SavedAtUnix=1_000_000;
        int levelBefore=snapshot.Heroes[0].Level;
        var half=GameSession.SettleOffline(c,snapshot,1_000_000+1800);
        Check(half.Seconds==1800&&half.Experience==30&&half.Kp==0&&half.Gold==15,$"半小时结算不正确：{half}");
        Check(snapshot.Heroes[0].Level==levelBefore,"离线结算自动升级了英雄");
        var repeat=GameSession.SettleOffline(c,snapshot,1_000_000+1800);
        Check(repeat.Seconds==0&&repeat.Experience==0,"同一时间段被重复结算");
        var capped=GameSession.SettleOffline(c,snapshot,1_000_000+1800+36000);
        Check(capped.Seconds==7200,$"离线时长未按上限封顶：{capped.Seconds}");

        // 读档接线：旧时间戳 → 正常结算一次并回写，再次读取不再重复。
        string directory=QaOutput.NewDirectory(root,"offline-settlement");string path=System.IO.Path.Combine(directory,"progress.json");
        var store=new SaveStore(path);
        var saved=offline.CaptureProgress();saved.SavedAtUnix=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-1800;
        store.WriteEncoded(SaveStore.Encode(new(){Session=saved,SavedAtUnix=saved.SavedAtUnix}));
        Check(store.Load(c)?.Session!=null,"离线结算后读档失败");
        Check(store.LastOffline is {Seconds:>=1800},"读档未触发离线结算");
        var settled=store.LastOffline!;
        store.Load(c);
        Check(store.LastOffline is null||store.LastOffline.Seconds==0,"同一存档重复结算离线收益");
        Check(settled.Experience>0&&settled.Gold>0,"离线结算未发放XP或金币");

        // 药剂在未 Tick 期间冻结（离线不做逻辑步推进）。
        var potionGame=GameSession.CreateNewWorld(c,9904,new[]{"warrior"});
        potionGame.QaGrantConsumable(c.Potions[0].Id);
        Check(potionGame.UsePotion(0,out _),"药剂启用失败");
        double before=potionGame.PotionSlots[0].RemainingSeconds;
        var potionSnap=potionGame.CaptureProgress();potionSnap.SavedAtUnix=1_000_000;
        GameSession.SettleOffline(c,potionSnap,1_000_000+7200);
        Check(GameSession.Restore(c,potionSnap).PotionSlots[0].RemainingSeconds==before,"离线结算推进了药剂计时");

        Console.WriteLine("WP7_REBIRTH_PASS: 章节门槛/重生事务与账号保留、放弃不产生事实、离线封顶与幂等、药剂冻结");
    }
}
