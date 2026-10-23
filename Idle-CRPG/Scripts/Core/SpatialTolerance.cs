using System;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>
/// 房间边界与位移到达的共享几何阈值。用途不同、数值可以不同，但必须**成对配套**，
/// 不允许出现“位移系统认为已到达、房间系统认为尚未进入”的空档（AUD-001/AUD-002）。
///
/// 约定：
/// - <see cref="MoveArrival"/>：一次位移的到达判定。移动系统在该距离内不再前进；
///   为了让几何目标可信，<c>Move</c> 在该距离内直接吸附到目标点（最多移动 1mm）。
/// - <see cref="RoomBoundary"/>：房间归属容差。必须 **不小于** <see cref="MoveArrival"/>，
///   否则站在门线上的单位会同时不满足“已到达别处”和“已进入房间”，形成无法自愈的死区。
/// - <see cref="EntryTrigger"/>：切房触发距离，只表示“队首足够靠近门点就开始换房”，
///   不表示已经入房，因此不与上面两个阈值比较。
/// - <see cref="InteractionRange"/>：楼梯集合与战利品拾取等交互距离，语义是“交互半径”，
///   既不是到达判定也不是房间归属，保持独立取值。
/// </summary>
public static class SpatialTolerance
{
    /// <summary>位移到达阈值：剩余距离不超过该值时直接吸附目标，不再逐帧逼近。</summary>
    public const float MoveArrival=0.001f;
    /// <summary>房间归属容差：单位到房间中心在“半尺寸 + 该容差”内即视为已进入该房间。</summary>
    public const float RoomBoundary=0.01f;
    /// <summary>切房触发距离：队首（或开门先锋）到门点在该距离内开始切换到目标房间。</summary>
    public const float EntryTrigger=0.32f;
    /// <summary>交互距离：走到楼梯/战利品等目标该距离内即可交互，与房间归属判定无关。</summary>
    public const float InteractionRange=0.35f;

    /// <summary>房间归属判定：四边各留 <see cref="RoomBoundary"/> 容差，站在门线上的单位归入房内。</summary>
    public static bool InsideRoom(Room room,Vector2 position)
    {
        var delta=Vector2.Abs(position-room.Center);
        return delta.X<=room.HalfWidth+RoomBoundary&&delta.Y<=room.HalfHeight+RoomBoundary;
    }

    /// <summary>是否贴在房间边界带内（门外一小段）。用于确认“从门线直接走向房内落点”不会切墙。</summary>
    public static bool NearRoomEdge(Room room,Vector2 position)
    {
        var delta=Vector2.Abs(position-room.Center);
        float band=RoomBoundary*2f;
        return delta.X<=room.HalfWidth+band&&delta.Y<=room.HalfHeight+band;
    }
}
