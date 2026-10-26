using System;
using System.Collections.Generic;

namespace IdleCrpg.Core;

/// <summary>诊断流水：使用Run/序号标识事件；物品的before/after为唯一英雄部位或药剂槽的完整状态。</summary>
public sealed record AssetEvent(string EventId,string RunId,long LogicTick,string Source,string Action,string HeroInstanceId,string AssetType,string DefinitionId,long Delta,string Before,string After,string Origin);
public sealed partial class GameSession
{
    private long assetEventSequence;
    private long assetAuditTick;
    private readonly Queue<AssetEvent> assetEvents=new();
    internal event Action<AssetEvent>? AssetRecorded;
    /// <summary>有界只读诊断缓存，完整机器流水由采样器及时导出，不把无限事件列表写入正式档。</summary>
    public AssetEvent[] AssetEvents=>assetEvents.ToArray();
    private void AuditAsset(string source,string action,string asset,string id,long delta,string before,string after,string hero="",string origin="")
    {
        var entry=new AssetEvent($"{RunId}:asset:{++assetEventSequence}",RunId,assetAuditTick,source,action,hero,asset,id,delta,before,after,origin);
        assetEvents.Enqueue(entry);while(assetEvents.Count>8192)assetEvents.Dequeue();
        AssetRecorded?.Invoke(entry);
    }
    private void AuditCurrency(string source,string action,string asset,long before,long after,string hero="",string origin="")=>AuditAsset(source,action,asset,"",after-before,before.ToString(System.Globalization.CultureInfo.InvariantCulture),after.ToString(System.Globalization.CultureInfo.InvariantCulture),hero,origin);
    private void AuditEquipment(string source,string action,Actor hero,string slot,EquipmentItem? before,EquipmentItem? after)
    {
        string State(EquipmentItem? item)=>item==null?"empty":System.Text.Json.JsonSerializer.Serialize(item);
        AuditAsset(source,action,"equipment_slot",after?.BaseId??before?.BaseId??"",(after==null?0:1)-(before==null?0:1),State(before),State(after),hero.HeroInstanceId,slot);
    }
}
