using System.Collections.Immutable;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.ZoneTransfer;

public sealed class ZoneTransferService(
    ICharacterRepository characters,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    ISessionTicketRepository tickets,
    IShardReachabilityProbe reachabilityProbe,
    IOptions<LoginServerOptions> options,
    ILogger<ZoneTransferService> logger) : IZoneTransferService
{
    public async ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost,
        Guid sessionToken, short accountGrade, CancellationToken cancellationToken)
    {
        logger.LogInformation("Zone transfer requested: account {AccountId} slot {AvatarPost}", accountId,
            avatarPost);

        var summaries = await characters.GetByAccountAsync(accountId, cancellationToken);
        var summary = summaries.FirstOrDefault(c => c.Slot == avatarPost);
        if (summary is null)
        {
            logger.LogWarning(
                "Zone transfer rejected: account {AccountId} slot {AvatarPost} holds no character", accountId,
                avatarPost);
            return new ZoneTransferResult(ZoneTransferOutcome.CharacterNotFound, "", 0, 0);
        }

        var character = await characters.GetForWorldEntryAsync(summary.CharacterId, cancellationToken);
        if (character is null)
        {
            logger.LogWarning(
                "Zone transfer rejected: character {CharacterId} (account {AccountId} slot {AvatarPost}) vanished between reads",
                summary.CharacterId, accountId, avatarPost);
            return new ZoneTransferResult(ZoneTransferOutcome.CharacterNotFound, "", 0, 0);
        }

        await ClampVitalsFloorIfNeededAsync(character, cancellationToken);

        var shards = await directory.GetDirectoryAsync(cancellationToken);
        var shard = await ResolveShardForMapAsync(shards, character.MapId, character.CharacterId, cancellationToken);
        if (shard is null)
        {
            logger.LogWarning(
                "Zone transfer rejected: no shard available for character {CharacterId} (account {AccountId}, MapId {MapId})",
                character.CharacterId, accountId, character.MapId);
            return new ZoneTransferResult(ZoneTransferOutcome.ShardUnavailable, "", 0, 0);
        }

        if (!await reachabilityProbe.IsReachableAsync(shard.Host, shard.Port, cancellationToken))
        {
            logger.LogWarning(
                "Zone transfer rejected: shard {ShardId} ({Host}:{Port}) failed a reachability probe for character {CharacterId} (account {AccountId}, MapId {MapId}); likely crashed within the directory staleness window -- evicting its row",
                shard.ShardId, shard.Host, shard.Port, character.CharacterId, accountId, character.MapId);
            await directory.MarkUnreachableAsync(shard.ShardId, cancellationToken);
            return new ZoneTransferResult(ZoneTransferOutcome.ShardUnavailable, "", 0, 0);
        }

        await tickets.CreateAsync(accountId, summary.CharacterId, shard.ShardId, options.Value.TicketTtlSeconds,
            sessionToken, accountGrade, cancellationToken);

        logger.LogInformation(
            "Zone transfer ticket minted: account {AccountId} character {CharacterId} -> shard {ShardId} ({Host}:{Port}, MapId {MapId})",
            accountId, character.CharacterId, shard.ShardId, shard.Host, shard.Port, character.MapId);

        return new ZoneTransferResult(ZoneTransferOutcome.Success, shard.Host, shard.Port, character.MapId);
    }

        private async ValueTask ClampVitalsFloorIfNeededAsync(CharacterWorldEntryDto character,
        CancellationToken cancellationToken)
    {
        var (life, mana) = AvatarVitalsFloor.Clamp(character.Life, character.Mana);
        if (life == character.Life && mana == character.Mana)
            return;

        await characters.ClampVitalsFloorAsync(character.CharacterId, character.FlushSequence + 1, life, mana,
            cancellationToken);
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

        if (shards.IsEmpty)
            logger.LogWarning(
                "No live shard is currently registered in runtime.GameServerDirectory; cannot route character {CharacterId} to MapId {MapId}",
                characterId, mapId);
        else
            logger.LogWarning(
                "Live shards exist but none of them claims MapId {MapId} in admin.ShardMapAssignments; cannot route character {CharacterId}",
                mapId, characterId);
        return null;
    }
}
