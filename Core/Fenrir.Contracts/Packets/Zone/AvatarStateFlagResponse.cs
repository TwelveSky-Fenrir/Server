using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_AVATAR_CHANGE_INFO_1 (ZONE.h:417-425, 24-byte payload, 6 ints, no padding) — broadcasts a third-party
///     avatar state change; the semantics live in <c>tSort</c> (e.g. 7 = duel state, 10 = pet) rather than in the
///     wire shape. AOI broadcast (concerns a visible third party). Not to be confused with ZC 22 (state of SELF,
///     different layout).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStateFlag, ExpectedSize = 25)]
public readonly partial record struct AvatarStateFlagResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int Sort { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
    public required int Value03 { get; init; }
}
