using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_CHAT_SEND (opcode 38, contracts/02_chat_notices.md). Empty content ⇒ anti-fuzzing
///     Quit(); a muted sender is silently dropped (no reply -- report 06 §1.7's own cached-flag posture,
///     mirrors the legacy's synchronous mute check with no distinct wire error). The GM inline-command
///     interception ("where"/"ygdrop"/"lab"/"boss" when <c>uUserSort &gt; 0</c>) is NOT modeled -- no
///     GM-rank concept exists in <see cref="PlayerRuntimeState" /> yet (open issue, same gap
///     <c>GlobalAnnouncementHandler</c> documents).
/// </summary>
public sealed class LocalChatHandler : IInlinePacketHandler<LocalChatRequest>
{
    public void Handle(in LocalChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null || state.IsMuted)
            return;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = characterId,
            Kind = ChatBroadcastKind.Local,
            Content = packet.Content,
            Link = packet.Link
        });
    }
}
