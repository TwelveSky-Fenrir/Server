using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Same layout is reused elsewhere with different semantics; here TribeRole genuinely is a role.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAnnouncement, ExpectedSize = 79)]
public readonly partial record struct TribeAnnouncementResponse : IOutgoingPacket
{
    /// <summary>
    ///     Sender's real role: 1 = tribe master, 2 = vice-master, 3 = elected tribe-council member seated
    ///     via the tribe-vote mechanism (all three are eligible to post; only role 0, not carried by this
    ///     packet, is excluded upstream).
    /// </summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
