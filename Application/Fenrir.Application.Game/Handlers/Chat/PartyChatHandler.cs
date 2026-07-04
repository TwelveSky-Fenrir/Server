using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_PARTY_CHAT_SEND (opcode 68). Empty content ⇒ Quit(); no party ⇒ silent return. TRAP,
///     preserved verbatim (see <see cref="PartyChatRequest.Link" />'s own remarks): the item link is DEAD
///     server-side in the legacy -- the outgoing ZC_PARTY_CHAT_RECV ALWAYS carries a zeroed link, even
///     though the incoming wire field is decoded (Fenrir decodes it too, then deliberately discards it).
///     Fan-out to every party member across every zone.
/// </summary>
public sealed class PartyChatHandler(ZoneRegistry zones, PartyRegistry parties) : IInlinePacketHandler<PartyChatRequest>
{
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public void Handle(in PartyChatRequest packet, IPacketSession session)
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

        var members = parties.GetMembers(characterId);
        if (members.Count == 0)
            return; // "pas de groupe" -- silent return, not a Quit

        var response = new PartyChatResponse
            { AvatarName = sender.Name, Content = packet.Content, Link = EmptyLink };

        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var recipient))
                recipient.Session.Send(response);
    }
}
