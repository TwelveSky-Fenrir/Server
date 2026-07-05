using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

/// <summary>
///     Business logic for CZ_PARTY_CHAT_SEND (opcode 68): party-membership gate plus the cross-zone fan-out to
///     every current party member. The outgoing link is always zeroed -- the legacy decodes the incoming item
///     link but never relays it, so it is discarded here, same as before extraction.
/// </summary>
public interface IPartyChatService
{
    /// <summary>
    ///     Broadcasts <paramref name="content" /> to every member of <paramref name="sender" />'s current party.
    ///     A partyless sender is silently ignored -- returns false.
    /// </summary>
    bool TrySendChat(PlayerRuntimeState sender, string content);
}

public sealed class PartyChatService(ZoneRegistry zones, PartyRegistry parties) : IPartyChatService
{
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public bool TrySendChat(PlayerRuntimeState sender, string content)
    {
        var members = parties.GetMembers(sender.CharacterId);
        if (members.Count == 0)
            return false;

        var response = new PartyChatResponse { AvatarName = sender.Name, Content = content, Link = EmptyLink };

        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var recipient))
                recipient.Session.Send(response);

        return true;
    }
}
