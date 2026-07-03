using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PARTY_MAKE_INFO (ZONE.h:810-818) — full party roster (MAX 5, slot 1 = leader), emitted by ProcessForRelay case
///     108 (S04_MyWork04.cpp:118-143) to every member across all zones.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyRoster, ExpectedSize = 70)]
public readonly partial record struct PartyRosterResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedString(13)] public required string AvatarName01 { get; init; }
    [FixedString(13)] public required string AvatarName02 { get; init; }
    [FixedString(13)] public required string AvatarName03 { get; init; }
    [FixedString(13)] public required string AvatarName04 { get; init; }
    [FixedString(13)] public required string AvatarName05 { get; init; }
}
