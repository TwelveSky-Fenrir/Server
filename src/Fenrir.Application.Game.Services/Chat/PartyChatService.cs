using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class PartyChatService(
    ZoneRegistry zones,
    PartyRegistry parties,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyChatService> logger)
    : IPartyChatService
{
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public bool TrySendChat(PlayerRuntimeState sender, string content)
    {
        if (sender.IsMuted)
        {
            logger.LogInformation("Character {CharacterId} party chat dropped: caller is muted", sender.CharacterId);
            return false;
        }

        var members = parties.GetMembers(sender.CharacterId);
        if (members.Count == 0)
        {
            logger.LogDebug("Party chat dropped: character {CharacterId} is not in a party", sender.CharacterId);
            return false;
        }

        var response = new PartyChatResponse { AvatarName = sender.Name, Content = content, Link = EmptyLink };

        var localRecipientCount = 0;
        var queuedRecipientCount = 0;
        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var recipient))
            {
                if (!recipient.IsMovingZone)
                {
                    recipient.Session.Send(response);
                    localRecipientCount++;
                }
            }
            else
            {
                relay.Enqueue(new GuildTribeBroadcastRelayEntry(
                    GuildTribeBroadcastKind.PartyChat,
                    options.Value.ShardId,
                    memberId,
                    null,
                    0,
                    sender.Name,
                    content,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
                {
                    SourceCharacterId = sender.CharacterId
                });
                queuedRecipientCount++;
            }

        logger.LogDebug(
            "Party chat: character {CharacterId} broadcast to {LocalRecipientCount} same-shard members and queued " +
            "{QueuedRecipientCount} remote members ({TotalMembers} total)",
            sender.CharacterId, localRecipientCount, queuedRecipientCount, members.Count);

        return true;
    }
}
