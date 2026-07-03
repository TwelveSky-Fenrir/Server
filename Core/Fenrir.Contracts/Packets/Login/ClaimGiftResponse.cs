using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_WANT_GIFT_RECV (LOGIN.h l.203-207): result of a gift claim (login protocol report §4.21, V1 path).
///     0 = moved to the shared chest, 1 = no gift at that index, 2 = chest full (28 slots), 101 = storage
///     failure (rolled back). No XOR.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ClaimGift,
    ExpectedSize = 5)]
public readonly partial record struct ClaimGiftResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
