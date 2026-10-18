using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public sealed partial class Config
{
    /// <summary>GM 单次发放量、倍率档位及数量步长均为原型调试配置，不是正式成长数值。</summary>
    public int GmGoldGrant { get; private set; }
    public int GmExperienceGrant { get; private set; }
    public float[] GmScaleSteps { get; private set; }=Array.Empty<float>();
    public int GmCountStep { get; private set; }
    public int GmCountLimit { get; private set; }
    private void LoadGm(string directory)
    {
        var rows=Read(Path.Combine(directory,"gm/gm.csv"));
        if(rows.Count!=1)throw new InvalidDataException("GM 配置必须恰有一行");
        var r=rows[0];GmGoldGrant=Integer(r,"gold_grant");GmExperienceGrant=Integer(r,"experience_grant");
        GmCountStep=Integer(r,"count_step");GmCountLimit=Integer(r,"count_limit");
        if(!r.TryGetValue("monster_scale_steps",out var steps))throw new InvalidDataException("GM 配置缺少倍率档位");
        GmScaleSteps=steps.Split(';').Select(value=>float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)?number:float.NaN).ToArray();
        if(GmGoldGrant is < 1 or > 1000000||GmExperienceGrant is < 1 or > 1000000||GmCountStep<1||GmCountStep>GmCountLimit||GmCountLimit>999||GmScaleSteps.Length<2||!GmScaleSteps.Contains(1)||GmScaleSteps.Any(v=>!float.IsFinite(v)||v<.5f||v>50)||GmScaleSteps.Zip(GmScaleSteps.Skip(1)).Any(pair=>pair.First>=pair.Second))
            throw new InvalidDataException("GM 发放量、数量步长/范围或倍率档位无效");
    }
}
