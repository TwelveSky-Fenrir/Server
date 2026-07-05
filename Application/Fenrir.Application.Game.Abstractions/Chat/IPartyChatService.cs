using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

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
    public bool TrySendChat(PlayerRuntimeState sender, string content);
}
