using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldChat, ExpectedSize = 79)]
public readonly record struct WorldChatResponse : IOutgoingPacket
{
    /// <summary>Same semantics as <see cref="TribeAnnouncementScrollResponse.TribeRole" />: sender's tribe number, not a role.</summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
