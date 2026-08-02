using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:9455-9480 (PARTY_CHAT_SEND, relay sort 105) relayed by
///     Server/ts25center/S04_MyWork02.cpp:1260-1261,1307-1333 (unconditional fan-out to every connected
///     zone process, itself included) and delivered per-zone by Server/ts25zone/S04_MyWork04.cpp:19-24,
///     66-79 via a party-name string match against each zone's own locally-connected sessions, skipping
///     not-ready/mid-transfer ones. Fenrir instead reuses PartyRegistry's per-shard membership list, so
///     this only reaches recipients hosted by the sender's own shard process today -- true cross-shard
///     fan-out needs a DB-relay row carrying party name + avatar name + content, which neither
///     runtime.GuildTribeBroadcastRelay (keyed by GuildId/Tribe, no free string column) nor
///     runtime.PartyResyncRelay (no Content column) currently has room for.
/// </remarks>
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
