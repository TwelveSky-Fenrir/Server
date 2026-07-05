using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Result: 0=ok, 101=shop already open, 102=proxy active server-side, 103=IPC failure.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.OpenShopStall,
    ExpectedSize = 1237)]
public readonly partial record struct OpenShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
