using System;
namespace IdleCrpg.Core;
/// <summary>一至五人的出生横向队形固定在已校验的 ±0.35 单位范围；三人旧队形保持不变。</summary>
public static class PartyFormation
{
    public static float SideOffset(int index,int count)
    {
        if(count<1||count>5||index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));
        return count==1?0:(index-(count-1)*.5f)*(.7f/(count-1));
    }
}
