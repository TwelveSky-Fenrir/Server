using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Result: 0=moved to chest, 1=no gift at index, 2=chest full (28 slots), 101=storage failure (rolled back).
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ClaimGift,
    ExpectedSize = 5)]
public readonly partial record struct ClaimGiftResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
