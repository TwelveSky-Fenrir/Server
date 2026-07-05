using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_GUILD_CHAT_SEND (opcode 77): guildless/muted gating plus the process-wide fan-out
///     to every member of the sender's guild. Unlike party chat, the item link is genuinely relayed here.
/// </summary>
public interface IGuildChatService
{
    /// <summary>
    ///     Broadcasts <paramref name="content" />/<paramref name="link" /> to every online member of
    ///     <paramref name="sender" />'s guild. A guildless or muted sender is silently ignored -- returns false.
    /// </summary>
    public bool TrySendChat(PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
