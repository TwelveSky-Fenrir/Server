using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Sort=1: attached name is your new student; Sort=2: attached name is your new master.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorStart, ExpectedSize = 18)]
public readonly partial record struct MentorStartResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
