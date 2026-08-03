using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class PartyChatService(ZoneRegistry zones, PartyRegistry parties, ILogger<PartyChatService> logger)
    : IPartyChatService
{
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public bool TrySendChat(PlayerRuntimeState sender, string content)
    {
        var members = parties.GetMembers(sender.CharacterId);
        if (members.Count == 0)
        {
            logger.LogDebug("Party chat dropped: character {CharacterId} is not in a party", sender.CharacterId);
            return false;
        }

        var response = new PartyChatResponse { AvatarName = sender.Name, Content = content, Link = EmptyLink };

        var recipientCount = 0;
        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var recipient) && !recipient.IsMovingZone)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        logger.LogDebug(
            "Party chat: character {CharacterId} broadcast to {RecipientCount} same-shard members ({TotalMembers} total)",
            sender.CharacterId, recipientCount, members.Count);

        return true;
    }
}
