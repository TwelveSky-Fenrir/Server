using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Tribes;

public sealed class TribeAnnouncementScrollService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<TribeAnnouncementScrollService> logger) : ITribeAnnouncementScrollService
{
    private const byte ActiveSourceWireRole = 1;

    public bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session,
        string content)
    {
        if (sender.IsMuted)
        {
            logger.LogDebug(
                "Character {CharacterId} tribe-notify-scroll broadcast rejected: sender is muted",
                characterId);
            return false;
        }

        if (sender.TribeNotifyScrollCount < 1)
        {
            logger.LogDebug(
                "Character {CharacterId} tribe-notify-scroll broadcast rejected: no scroll charges remaining",
                characterId);
            return false;
        }

        var newCount = sender.TribeNotifyScrollCount - 1;

        session.Send(new AvatarStatUpdateResponse { Sort = 69, Value = newCount, Value2 = 0 });

        var response = new TribeAnnouncementScrollResponse
            { TribeRole = ActiveSourceWireRole, AvatarName = sender.Name, Content = content };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.TribeAnnouncementScroll,
            options.Value.ShardId,
            null,
            sender.Tribe,
            ActiveSourceWireRole,
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

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: newCount));

        logger.LogInformation(
            "Character {CharacterId} used a tribe-notify scroll to broadcast to tribe {Tribe} ({RecipientCount} same-shard recipients, {RemainingCharges} charges left)",
            characterId, sender.Tribe, recipientCount, newCount);

        return true;
    }
}
