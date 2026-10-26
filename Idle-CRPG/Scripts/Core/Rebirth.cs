using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>离线结算结果：只含被批准的三类资源，且记录实际结算秒数，便于审计与幂等。</summary>
public sealed record OfflineSettlement(int Seconds,long Experience,long Kp,long Gold);

public sealed partial class GameSession
{
    private bool retired;
    /// <summary>本轮已正式重生：旧 Run 不允许再写盘，防止把已清零的资产写回。</summary>
    public bool Retired=>retired;

    /// <summary>正式重生门槛：必须完成第 4 章主线（当前 5 章配置中的第 4 章主目标）。</summary>
    public bool CanRebirth(out string reason)
    {
        if(Config.ChapterMaps.Count<4){reason="当前版本没有第4章";return false;}
        string mainPoi=Config.ChapterMaps[3].MainClearPoi;
        if(World==null||string.IsNullOrWhiteSpace(mainPoi)){
            // 无世界或配置缺主目标时，退化为"已完成第4章全部地牢"的判定。
            var chapter=Config.ChapterMaps[3];
            if(World==null||chapter.Dungeons.Any(d=>!World.Progress.Cleared.Contains(d.Id))){reason="需先完成第4章主线";return false;}
            reason="";return true;
        }
        if(!World.Progress.Cleared.Contains(mainPoi)){reason="需先完成第4章主线";return false;}
        reason="";return true;
    }

    /// <summary>
    /// 正式重生：只结算已批准的永久事实（通关事实与轮次序号），Run 的英雄/装备/XP/KP/金币/星图/
    /// 卷轴/药剂/地图全部由新会话重建而清零；放弃本轮不产生这些事实。
    /// </summary>
    public bool Rebirth(out AccountState account,out string reason)
    {
        account=Account.Copy();
        if(retired){reason="本轮已结束，请直接开始新轮次";return false;}
        if(!CanRebirth(out reason))return false;
        Account.RunOrdinal=checked(Account.RunOrdinal+1);
        if(!Account.AchievementFacts.Contains("ch4-cleared"))Account.AchievementFacts.Add("ch4-cleared");
        string fact=$"rebirth:{Account.RunOrdinal}";
        if(!Account.AchievementFacts.Contains(fact))Account.AchievementFacts.Add(fact);
        Note($"正式重生：第{Account.RunOrdinal}次，账号权益保留、本轮资产清零");
        retired=true;account=Account.Copy();reason="";return true;
    }

    /// <summary>
    /// 有限离线结算：以存档时间为锚点、按配置上限封顶，只结算 XP/KP/金币；
    /// 不自动升级、换装、学天赋、买星图/KP、攻城、使用卷轴药剂，也不推进药剂计时。
    /// </summary>
    public static OfflineSettlement SettleOffline(Config config,SessionSnapshot snapshot,long nowUnix)
    {
        long saved=snapshot.SavedAtUnix;
        if(saved<=0||nowUnix<=saved)return new(0,0,0,0);
        long seconds=Math.Clamp(nowUnix-saved,0,config.Talents.Rule("offline_cap_seconds"));
        if(seconds<60)return new(0,0,0,0);
        double hours=seconds/3600d;
        // C1-B F03：离线修行只作用于合法离线结算渠道；纯在线不计入。
        var offlineStars=new StarProgression(config.Stars,snapshot.Stars);
        double offlineScale=1+offlineStars.Configuration.Nodes.Values.Where(n=>n.Attribute=="offline_pct").Sum(n=>n.ValuePerLevel*offlineStars.Level(n.Id));
        long xp=(long)Math.Floor(config.Talents.Rule("offline_xp_per_hour")*hours*offlineScale);
        long kp=(long)Math.Floor(config.Talents.Rule("offline_kp_per_hour")*hours*offlineScale);
        long gold=(long)Math.Floor(config.Talents.Rule("offline_gold_per_hour")*hours*offlineScale);
        snapshot.PublicExperience=checked(snapshot.PublicExperience+xp);
        snapshot.Kp=checked(snapshot.Kp+kp);snapshot.KpEarned=checked(snapshot.KpEarned+kp);
        snapshot.Gold=checked(snapshot.Gold+gold);
        snapshot.SavedAtUnix=nowUnix;
        return new((int)seconds,xp,kp,gold);
    }
}
