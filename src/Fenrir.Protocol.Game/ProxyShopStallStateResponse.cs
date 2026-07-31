using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ProxyShopStallState,
    ExpectedSize = 65)]
public readonly partial record struct ProxyShopStallStateResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required int UniqueNumber { get; init; }
    public required ProxyStateInfo ProxyObject { get; init; }
    [Reserved(2)] public required int CheckChangeActionState { get; init; }
}
