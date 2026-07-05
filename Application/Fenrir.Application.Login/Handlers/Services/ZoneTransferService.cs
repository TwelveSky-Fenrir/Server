using System.Collections.Immutable;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Handlers.Services;

public enum ZoneTransferOutcome
{
    CharacterNotFound,
    ShardUnavailable,
    Success
}

public readonly record struct ZoneTransferResult(ZoneTransferOutcome Outcome, string Ip, int Port, short Zone);

public interface IZoneTransferService
{
    ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken);
}

/// <summary>
///     op22 CL_DEMAND_ZONE_SERVER_INFO_SEND business logic — resolves the shard hosting the character's map and
///     mints the handover ticket; the handover identity lives server-side in the ticket row, never on the wire.
/// </summary>
public sealed class ZoneTransferService(
    ICharacterRepository characters,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    ISessionTicketRepository tickets,
    IOptions<LoginServerOptions> options,
    ILogger<ZoneTransferService> logger) : IZoneTransferService
{
    public async ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken)
    {
        var summaries = await characters.GetByAccountAsync(accountId, cancellationToken);
        var summary = summaries.FirstOrDefault(c => c.Slot == avatarPost);
        if (summary is null)
            return new ZoneTransferResult(ZoneTransferOutcome.CharacterNotFound, "", 0, 0);

        var character = await characters.GetForWorldEntryAsync(summary.CharacterId, cancellationToken);
        if (character is null)
            // Character vanished between the two reads above (concurrent delete elsewhere); bail before minting a ticket.
            return new ZoneTransferResult(ZoneTransferOutcome.CharacterNotFound, "", 0, 0);

        var shards = await directory.GetDirectoryAsync(cancellationToken);
        var shard = await ResolveShardForMapAsync(shards, character.MapId, character.CharacterId, cancellationToken);
        if (shard is null)
            return new ZoneTransferResult(ZoneTransferOutcome.ShardUnavailable, "", 0, 0);

        await tickets.CreateAsync(accountId, summary.CharacterId, shard.ShardId, options.Value.TicketTtlSeconds,
            cancellationToken);

        // Zone = the persisted MapId the character resumes on (same value AvatarInfoFactory writes to LogoutInfo[0]).
        return new ZoneTransferResult(ZoneTransferOutcome.Success, shard.Host, shard.Port, character.MapId);
    }

    private async ValueTask<ShardDirectoryEntryDto?> ResolveShardForMapAsync(
        ImmutableArray<ShardDirectoryEntryDto> shards, short mapId, int characterId, CancellationToken ct)
    {
        foreach (var candidate in shards)
        {
            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(candidate.ShardId, ct);
            if (hostedMaps.Contains(mapId))
                return candidate;
        }

        logger.LogWarning(
            "No shard in admin.ShardMapAssignments hosts MapId {MapId} for character {CharacterId}; falling back to first live shard",
            mapId, characterId);
        return shards.FirstOrDefault();
    }
}
