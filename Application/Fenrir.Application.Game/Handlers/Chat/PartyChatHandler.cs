using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_PARTY_CHAT_SEND (opcode 68). The outgoing link is always zeroed -- the legacy decodes the
///     incoming item link but never relays it, so it is decoded here and then deliberately discarded.
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
            return;

        var response = new PartyChatResponse
            { AvatarName = sender.Name, Content = packet.Content, Link = EmptyLink };

        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var recipient))
                recipient.Session.Send(response);
    }
}
