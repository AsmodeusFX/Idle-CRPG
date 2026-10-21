using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class WorldCampaign
{
    private readonly List<Vector2> outdoorTrail=new();
    private float followGather;
    private float FollowSpacing=>config.Talents.Rule("world_follow_spacing_milli")/1000f;
    private bool TrailCurrent=>outdoorTrail.Count>0&&Vector2.DistanceSquared(outdoorTrail[0],Position)<.00000001f;
    /// <summary>队员沿排头实际走过的折线路径采样；没有足够历史时留在安全起点，不伪造穿墙偏移。</summary>
    public Vector2 OutdoorPartyPosition(int index)
    {
        if(index<=0||!TrailCurrent)return Position;
        float distance=Math.Max(0,index*FollowSpacing-followGather);
        for(int i=1;i<outdoorTrail.Count;i++)
        {
            float length=Vector2.Distance(outdoorTrail[i-1],outdoorTrail[i]);
            if(length>=distance&&length>0)return Vector2.Lerp(outdoorTrail[i-1],outdoorTrail[i],distance/length);
            distance-=length;
        }
        return outdoorTrail[^1];
    }
    private void RecordOutdoorStep(Vector2 previous)
    {
        if(Vector2.DistanceSquared(previous,Position)<.0000000001f)return;
        if(outdoorTrail.Count==0||Vector2.DistanceSquared(outdoorTrail[0],previous)>.00000001f){outdoorTrail.Clear();outdoorTrail.Add(previous);followGather=0;}
        outdoorTrail.Insert(0,Position);followGather=Math.Max(0,followGather-Vector2.Distance(previous,Position));
        // 共线合并避免缓慢行走产生大量历史点；拐点保留，队尾不能切弯穿越障碍。
        if(outdoorTrail.Count>=3)
        {
            var a=outdoorTrail[0]-outdoorTrail[1];var b=outdoorTrail[1]-outdoorTrail[2];
            if(a.LengthSquared()>0&&b.LengthSquared()>0&&Vector2.Dot(Vector2.Normalize(a),Vector2.Normalize(b))>.999999f)outdoorTrail.RemoveAt(1);
        }
        float remaining=FollowSpacing*5+config.TravelSpeed*.1f;
        for(int i=1;i<outdoorTrail.Count;i++)
        {
            float length=Vector2.Distance(outdoorTrail[i-1],outdoorTrail[i]);
            if(length>remaining){outdoorTrail[i]=Vector2.Lerp(outdoorTrail[i-1],outdoorTrail[i],remaining/length);outdoorTrail.RemoveRange(i+1,outdoorTrail.Count-i-1);break;}remaining-=length;
        }
    }
    private void ValidateOutdoorTrail(WorldSnapshot s)
    {
        if(s.OutdoorTrail==null||s.OutdoorTrail.Count>1024||s.OutdoorTrail.Any(p=>!WorldNavigation.Walkable(config,p))||!float.IsFinite(s.FollowGather)||s.FollowGather<0||s.FollowGather>FollowSpacing*5+config.TravelSpeed*.11f)throw new InvalidDataException("室外队列轨迹或集合进度无效");
        if(s.OutdoorTrail.Count>0&&Vector2.DistanceSquared(s.OutdoorTrail[0],s.Position)>.00000001f)throw new InvalidDataException("室外跟随轨迹不连接排头");
        for(int i=1;i<s.OutdoorTrail.Count;i++)
        {
            int samples=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(s.OutdoorTrail[i-1],s.OutdoorTrail[i])*WorldNavigation.CellsPerRegion*4));
            for(int j=1;j<=samples;j++)if(!WorldNavigation.Walkable(config,Vector2.Lerp(s.OutdoorTrail[i-1],s.OutdoorTrail[i],j/(float)samples)))throw new InvalidDataException("室外队列轨迹穿过障碍");
        }
    }
}
