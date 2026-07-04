using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_WORLD_CHAT_SEND (opcode 152). Level &lt; 10 ⇒ anti-fuzzing Quit() (anti-spam-bot gate, verified);
///     muted ⇒ silent drop. Relay tSort=115 -&gt; ZC_WORLD_CHAT_RECV (opcode 192) to EVERY ready player of
///     EVERY zone, unfiltered (contracts/02_chat_notices.md) -- collapsed here into a direct process-wide
///     fan-out (<see cref="ChatRouter" />'s own remarks on the relay-to-direct-fanout topology change).
///     The wire's <c>TribeRole</c> field for this opcode is actually the sender's TRIBE NUMBER, not a role
///     (contract's own documented "piège") -- passed through as <see cref="PlayerRuntimeState.Tribe" />
///     verbatim, with no +1/other offset (no evidence found for one -- see this task's open issues).
/// </summary>
public sealed class WorldChatHandler(ZoneRegistry zones) : IInlinePacketHandler<WorldChatRequest>
{
    private const int MinimumLevel = 10;

    public void Handle(in WorldChatRequest packet, IPacketSession session)
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
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        if (sender.Level < MinimumLevel)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (sender.IsMuted)
            return;

        var response = new WorldChatResponse
        {
            TribeRole = sender.Tribe,
            AvatarName = sender.Name,
            Content = packet.Content
        };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            recipient.Session.Send(response);
    }
}
