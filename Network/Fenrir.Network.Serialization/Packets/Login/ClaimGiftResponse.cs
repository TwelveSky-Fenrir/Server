using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Result: 0=moved to chest, 1=no gift at index (stale index or lost a claim race against another request),
// 2=chest full (28 slots), 101=unexpected persistence failure (rolled back, safe to retry). See
// Fenrir.Application.Login.Abstractions.ClaimGift.ClaimGiftOutcome for the C# side of this mapping.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ClaimGift,
    ExpectedSize = 5)]
public readonly partial record struct ClaimGiftResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
