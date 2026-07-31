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

        if (sender.IsMuted)
        {
            logger.LogInformation("Character {CharacterId} party chat dropped: caller is muted", sender.CharacterId);
            return false;
        }

        var response = new PartyChatResponse { AvatarName = sender.Name, Content = content, Link = EmptyLink };

        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var recipient))
                recipient.Session.Send(response);

        logger.LogDebug("Party chat: character {CharacterId} broadcast to {RecipientCount} members",
            sender.CharacterId, members.Count);

        return true;
    }
}
