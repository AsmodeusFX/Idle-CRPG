using System;
using System.IO;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>旧主城坐标不可直接套用新布局；仅迁移本地现场，保留售卖阶段、背包及所有世界进度。</summary>
    private static bool MigrateTownLayout(Config config,SessionSnapshot snapshot)
    {
        // 暂挂主线已在出发边界，只有本地缓存路径需要丢弃；当前队员仍属于正在执行的支线。
        var suspended=snapshot.SuspendedWorld;
        if(suspended?.TownLayoutVersion<0||snapshot.World?.TownLayoutVersion<0)throw new InvalidDataException("主城布局版本不可为负数");
        if(suspended!=null&&snapshot.LatestMapIndex>=0&&snapshot.LatestMapIndex<15)
        {
            int version=config.ChapterMaps[snapshot.LatestMapIndex%5].Town?.LayoutVersion??0;
            if(suspended.TownLayoutVersion<version){suspended.VillagePaths.Clear();suspended.TownLayoutVersion=version;}
        }
        var world=snapshot.World;var town=config.Town;
        if(world==null||town==null||world.TownLayoutVersion>=town.LayoutVersion)return false;
        world.VillagePaths.Clear();
        bool village=world.Stage==CampaignStage.Village;
        bool departing=world.PortalPhase!=PortalPhase.None&&!world.PortalReturning&&world.PortalPhase!=PortalPhase.Arriving;
        if(village||departing)
        {
            var anchor=village?config.TownPortal:config.TownWaypoint;
            for(int i=0;i<snapshot.Heroes.Count;i++)snapshot.Heroes[i].Position=anchor+new Vector2(0,PartyFormation.SideOffset(i,snapshot.Heroes.Count));
            world.VillagePaths.Clear();world.TradeElapsed=0;
            if(departing)world.PortalOrigin=config.TownWaypoint;
        }
        world.TownLayoutVersion=town.LayoutVersion;
        return village||departing;
    }
}
