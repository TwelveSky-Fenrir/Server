using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     See <see cref="ITribeAnnouncementScrollService" />. Same-shard delivery is synchronous, via
///     <see cref="ZoneRegistry" />, exactly as before. Cross-shard delivery (a tribe member connected to a map
///     hosted by a DIFFERENT live shard) is handed off to <see cref="IGuildTribeBroadcastRelayQueue" /> -- see
///     that interface and <c>GuildTribeBroadcastRelayHost</c>'s own remarks for the full cluster-wide fan-out
///     design (Fenrir's SQL-mediated stand-in for legacy's <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> relay
///     uplink).
/// </summary>
public sealed class TribeAnnouncementScrollService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<TribeAnnouncementScrollService> logger) : ITribeAnnouncementScrollService
{
    public bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session,
        string content)
    {
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
            { TribeRole = sender.Tribe, AvatarName = sender.Name, Content = content };

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
            // Wire-field quirk: this opcode's "role" field position actually carries the sender's raw tribe
            // number, not a role (see TribeAnnouncementScrollResponse's own docstring) -- mirrored exactly
            // here so GuildTribeBroadcastRelayHost's delivery loop needs no opcode-specific special case.
            sender.Tribe,
            sender.Name,
            content,
            false,
            null,
            null,
            null,
            null,
            null,
            null));

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: newCount));

        logger.LogInformation(
            "Character {CharacterId} used a tribe-notify scroll to broadcast to tribe {Tribe} ({RecipientCount} same-shard recipients, {RemainingCharges} charges left)",
            characterId, sender.Tribe, recipientCount, newCount);

        return true;
    }
}
