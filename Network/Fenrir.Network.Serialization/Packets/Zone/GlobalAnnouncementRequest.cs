using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Handled by <c>Fenrir.Application.Game.Handlers.Handlers.Chat.GlobalAnnouncementHandler</c>, delegating
///     to <c>Fenrir.Application.Game.Abstractions.Chat.IGlobalAnnouncementService</c>. Silently dropped --
///     no reply, no disconnect -- unless the sender meets <c>GmCommandTier.Basic</c> (legacy's
///     <c>uUserSort &gt;= 1</c>).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GlobalAnnouncement, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct GlobalAnnouncementRequest : IIncomingPacket<GlobalAnnouncementRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
