using System.Collections.Immutable;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Domain.Login;
using Fenrir.Domain.Login.Avatars;
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
    // ShardUnavailable carries the real endpoint and the real zone number on purpose: the legacy answers
    // result 1 with ip/port/zone filled in, not zeroed (Server/ts25login/S04_MyWork02.cpp:1590).
    public async ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost,
        Guid sessionToken, short accountGrade, CancellationToken cancellationToken)
    {
        logger.LogInformation("Zone transfer requested: account {AccountId} slot {AvatarPost}", accountId,
            avatarPost);

        var summaries = await characters.GetByAccountAsync(accountId, cancellationToken);
        var summary = summaries.FirstOrDefault(c => c.Slot == avatarPost);
        if (summary is null || summary.Name.Length == 0)
        {
            logger.LogWarning(
                "Zone transfer rejected: account {AccountId} slot {AvatarPost} holds no character", accountId,
                avatarPost);
            return new ZoneTransferResult(ZoneTransferOutcome.SlotEmpty, "", 0, 0);
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

        var (healedMapId, _, _, _) = LogoutZoneSelfHeal.Apply(character.Tribe, character.MapId, character.PosX,
            character.PosY, character.PosZ);

        var shards = await directory.GetDirectoryAsync(cancellationToken);
        var shard = await ResolveShardForMapAsync(shards, healedMapId, character.CharacterId, cancellationToken);
        if (shard is null)
        {
            logger.LogWarning(
                "Zone transfer rejected: no shard available for character {CharacterId} (account {AccountId}, MapId {MapId})",
                character.CharacterId, accountId, healedMapId);
            return new ZoneTransferResult(ZoneTransferOutcome.ShardUnavailable, "", 0, healedMapId);
        }

        var zonePort = options.Value.ZoneBasePort + healedMapId;

        if (!await reachabilityProbe.IsReachableAsync(shard.Host, zonePort, cancellationToken))
        {
            logger.LogWarning(
                "Zone transfer rejected: zone endpoint {Host}:{Port} (MapId {MapId}) failed a reachability probe for " +
                "character {CharacterId} (account {AccountId}); that zone's listener is not accepting on shard {ShardId}",
                shard.Host, zonePort, healedMapId, character.CharacterId, accountId, shard.ShardId);
            return new ZoneTransferResult(ZoneTransferOutcome.ShardUnavailable, shard.Host, zonePort, healedMapId);
        }

        await tickets.CreateAsync(accountId, summary.CharacterId, shard.ShardId, options.Value.TicketTtlSeconds,
            sessionToken, accountGrade, healedMapId, cancellationToken);

        logger.LogInformation(
            "Zone transfer ticket minted: account {AccountId} character {CharacterId} -> zone {MapId} at {Host}:{Port} (shard {ShardId})",
            accountId, character.CharacterId, healedMapId, shard.Host, zonePort, shard.ShardId);

        return new ZoneTransferResult(ZoneTransferOutcome.Success, shard.Host, zonePort, healedMapId);
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
                "No live shard is currently registered in runtime.GameServerDirectory; cannot route character {CharacterId} " +
                "to MapId {MapId}. => Aucun GameServer n'a heartbeaté dans la DB que LIT ce LoginServer : soit le " +
                "GameServer ne tourne pas, soit il pointe une AUTRE base (connection string), soit son heartbeat échoue " +
                "(voir son log). Le GameServer confirme son inscription par 'registered in runtime.GameServerDirectory as shard N'.",
                characterId, mapId);
        else
            logger.LogWarning(
                "Live shards exist ([{Shards}]) but none of them claims MapId {MapId} in admin.ShardMapAssignments; " +
                "cannot route character {CharacterId}. => La map cible n'est assignée à aucun shard vivant (seed " +
                "admin.ShardMapAssignments / migration 027).",
                string.Join(", ", shards.Select(s => $"shard{s.ShardId}@{s.Host}:{s.Port}")), mapId, characterId);
        return null;
    }
}
