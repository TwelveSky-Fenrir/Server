using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Result: 0=ok, 101=shop already open, 102=proxy active server-side, 103=IPC failure.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.OpenShopStall,
    ExpectedSize = 1237)]
public readonly partial record struct OpenShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
