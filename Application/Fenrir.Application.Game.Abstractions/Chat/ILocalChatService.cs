using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_GENERAL_CHAT_SEND (opcode 38): the mute gate, the embedded GM command sub-parser
///     (<see cref="LocalChatGmCommandParser" />), and posting the AOI-scoped broadcast onto the sender's own
///     zone tick for everything that isn't a recognized GM command.
/// </summary>
public interface ILocalChatService
{
    /// <summary>
    ///     A muted sender is silently ignored -- returns false. Otherwise, if <paramref name="zoneSession" />
    ///     holds any GM tier and <paramref name="content" /> matches one of LocalChat's six embedded GM
    ///     commands, the command is intercepted (never posted as ordinary chat) -- see
    ///     <see cref="Fenrir.Application.Game.Services.Chat.LocalChatService" />'s own remarks for which
    ///     commands are actually implemented. Otherwise posts <paramref name="content" />/<paramref name="link" />
    ///     as a <see cref="ChatBroadcastKind.Local" /> command onto <paramref name="zone" />'s tick.
    /// </summary>
    public bool TryPostChat(Zone zone, ZoneClientSession zoneSession, PlayerRuntimeState sender, string content,
        ItemLinkInfo link);
}
