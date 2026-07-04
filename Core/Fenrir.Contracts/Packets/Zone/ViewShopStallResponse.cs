using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     Result: 0=ok, 1=target not found, 2=stall not open. On error, PshopInfo holds the REQUESTER's own stall — must
///     be ignored.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ViewShopStall,
    ExpectedSize = 1237)]
public readonly partial record struct ViewShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
