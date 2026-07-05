using System.Threading.Channels;
using Fenrir.Application.Game.Social.Chat;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Raw local/shout/tribe chat sends that need this zone's tick-owned <see cref="_grid" />/
    ///     <see cref="_players" /> to resolve their audience -- every other chat channel (whisper/party/guild/
    ///     world/notices) fans out directly via <c>ZoneRegistry</c>/<see cref="Players" /> without touching the tick thread.
    /// </summary>
    private readonly Channel<ChatZoneCommand> _chatInbox =
        Channel.CreateBounded<ChatZoneCommand>(
            new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostChatCommand(in ChatZoneCommand command)
    {
        return _chatInbox.Writer.TryWrite(command);
    }

    private void DrainChatCommands()
    {
        while (_chatInbox.Reader.TryRead(out var command))
            try
            {
                ApplyChatCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} chat command from character {CharacterId} failed", MapId,
                    command.SenderCharacterId);
            }
    }

    /// <summary>
    ///     Resolves Local/Shout/Tribe chat's audience on this zone's own tick thread. Local = AOI-neighbor
    ///     broadcast filtered by the sender's own tribe (alliance not modeled); Shout/Tribe = whole-zone, Tribe
    ///     additionally filtered by tribe. The sender always receives its own echo.
    /// </summary>
    private void ApplyChatCommand(in ChatZoneCommand command)
    {
        if (!_players.TryGetValue(command.SenderCharacterId, out var sender))
            return; // sender disconnected/handed off between post and drain -- benign

        switch (command.Kind)
        {
            case ChatBroadcastKind.Local:
            {
                var response = new LocalChatResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                foreach (var id in _grid.Neighbors(sender.CurrentCell))
                    if (_players.TryGetValue(id, out var recipient) && recipient.Tribe == sender.Tribe)
                        recipient.Session.Send(response);
                break;
            }
            case ChatBroadcastKind.Shout:
            {
                var response = new ShoutResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                foreach (var recipient in _players.Values)
                    recipient.Session.Send(response);
                break;
            }
            case ChatBroadcastKind.Tribe:
            {
                var response = new TribeChatResponse
                    { AvatarName = sender.Name, Content = command.Content, Link = command.Link };
                foreach (var recipient in _players.Values)
                    if (recipient.Tribe == sender.Tribe)
                        recipient.Session.Send(response);
                break;
            }
        }
    }
}
