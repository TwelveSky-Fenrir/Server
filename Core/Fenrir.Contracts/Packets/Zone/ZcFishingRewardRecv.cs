using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_FISHING_REWARD_RECV (ZONE.h:1183-1190) — unicast response to CZ 105, builder
///     <c>B_FISHING_REWARD_RECV(MyUser*, tResult, tItemIndex, tPage, tIndex, tXY)</c> with USEND baked in
///     (S05_MyTransfer.cpp:1494-1503). Legacy call-site quirk: the handler passes <c>tPosY</c> into the
///     <c>tXY</c> parameter (S04_MyWork02.cpp:13939/13942), not a packed X/Y pair — reproduced as-is.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingRewardRecv,
    ExpectedSize = 21)]
public readonly partial record struct ZcFishingRewardRecv : IOutgoingPacket
{
    /// <summary>1=item granted, 2=inventory full.</summary>
    public required int Result { get; init; }

    /// <summary>Item index of the fish/prize.</summary>
    public required int ItemIndex { get; init; }

    public required int Page { get; init; }
    public required int Index { get; init; }

    /// <summary>Legacy quirk: actually carries <c>tPosY</c>, not a packed X/Y pair.</summary>
    public required int XY { get; init; }
}
