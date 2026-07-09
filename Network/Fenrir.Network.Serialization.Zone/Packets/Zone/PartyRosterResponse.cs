using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Slot 1 = leader; roster capped at 5 members.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyRoster, ExpectedSize = 70)]
public readonly record struct PartyRosterResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedString(13)] public required string AvatarName01 { get; init; }
    [FixedString(13)] public required string AvatarName02 { get; init; }
    [FixedString(13)] public required string AvatarName03 { get; init; }
    [FixedString(13)] public required string AvatarName04 { get; init; }
    [FixedString(13)] public required string AvatarName05 { get; init; }
}
