using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyResyncRelayHandler(
    ZoneRegistry zones,
    PartyRegistry parties,
    Lazy<IPartyResyncRelayQueue> relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyResyncRelayHandler> logger) : IPartyResyncRelayHandler
{
    public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct)
    {
        if (row.Sort != (byte)PartyResyncRelaySort.Request)
        {
            logger.LogDebug(
                "Relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) is a result leg with no " +
                "local UI-apply wired yet; no-op",
                row.RelayId, row.Sort, row.PartyName);
            return ValueTask.CompletedTask;
        }

        if (!zones.TryGetPlayerByName(row.PartyName, out var leaderCandidate) ||
            !parties.IsLeader(leaderCandidate.CharacterId))
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for party {PartyName} (subject {SourceCharacterId}) " +
                "is not hosted on shard {ShardId}",
                row.RelayId, row.PartyName, row.SourceCharacterId, options.Value.ShardId);
            return ValueTask.CompletedTask;
        }

        var roster = parties.GetMembers(leaderCandidate.CharacterId);
        if (roster.Count == 0)
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for party {PartyName} raced a disband on shard " +
                "{ShardId} between the leader check and the roster fetch; treated as not hosted",
                row.RelayId, row.PartyName, options.Value.ShardId);
            return ValueTask.CompletedTask;
        }

        relay.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfo,
            options.Value.ShardId,
            row.SourceCharacterId,
            row.PartyName,
            row.AvatarName));

        logger.LogDebug(
            "Relayed party-resync request {RelayId} for party {PartyName} confirmed on shard {ShardId} " +
            "({MemberCount} members); result republished for subject {SourceCharacterId}",
            row.RelayId, row.PartyName, options.Value.ShardId, roster.Count, row.SourceCharacterId);

        return ValueTask.CompletedTask;
    }
}
