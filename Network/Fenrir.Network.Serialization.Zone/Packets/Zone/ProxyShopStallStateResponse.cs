using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// UniqueNumber is a plain int (not DWORD) on the wire.
// CheckChangeActionState: 1 = spawn/update, 0 = periodic re-broadcast (no animation replay).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ProxyShopStallState,
    ExpectedSize = 65)]
public readonly record struct ProxyShopStallStateResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required int UniqueNumber { get; init; }
    public required ProxyStateInfo ProxyObject { get; init; }
    [Reserved(2)] public required int CheckChangeActionState { get; init; }
}
