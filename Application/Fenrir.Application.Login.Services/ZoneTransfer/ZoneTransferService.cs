using System.Collections.Immutable;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.ZoneTransfer;

/// <summary>
///     op22 CL_DEMAND_ZONE_SERVER_INFO_SEND business logic — resolves the shard hosting the character's map and
///     mints the handover ticket; the handover identity lives server-side in the ticket row, never on the wire.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25login/S04_MyWork02.cpp:129-435, tail segment :324-426 (per-slot logout-zone/tribe
///         realignment + Life/Mana floor clamp + blacklisted-item purge, run on every occupied avatar slot at every
///         successful <c>LOGIN_SEND</c>) ; :1543-1622 (<c>DEMAND_ZONE_SERVER_INFO_SEND</c>, the legacy op22 handler
///         this class is the Fenrir counterpart of, consuming <c>aLogoutInfo[0]</c> at :1587 for zone routing).
///     </para>
///     <para>
///         Only the Life/Mana floor clamp (<see cref="AvatarVitalsFloor" />) is implemented here, deliberately
///         re-timed from "all 3 slots at every login" to "the one slot being transferred, at op22 request time" --
///         this class already loads the position/tribe/life/mana this clamp needs (<c>CharacterWorldEntryDto</c>),
///         and is the closest Fenrir timing/data-locality match per the accompanying behavior contract's own
///         placement finding.
///     </para>
///     <para>
///         Two sibling corrections from the same C++ tail segment are deliberately NOT implemented by this change:
///     </para>
///     <list type="bullet">
///         <item>
///             Logout-zone/tribe realignment (S04_MyWork02.cpp:330-356, <c>GetReturnBornInTownLocation</c> at
///             Server/Header/mapcheck.h:298-326) needs the static zone-number-&gt;owning-tribe table (column [0] of
///             <c>mZoneTribeInfo</c>, Server/Header/S18_MyZoneInfo.cpp:9-393 / accessor :417-424). That table's
///             other column (PvP flag) is already ported as
///             <c>Fenrir.Application.Game.Domain.Combat.ZonePvpZoneCatalog</c>,
///             but the owning-tribe column itself is not present anywhere in current Fenrir code, and the behavior
///             contract handed to this change only cites 3 illustrative zone ids (5, 10, 15) rather than the full
///             ~117-350-entry table -- porting it here would mean fabricating the other entries from memory, which
///             the project's legacy-parity rules forbid. Needs a follow-up <c>cpp-ts25-explorer</c>/
///             <c>legacy-behavior-translator</c> finding enumerating the full table (mirroring how
///             <c>ZonePvpZoneCatalog</c> was built for column [1]) before this can be safely implemented -- an
///             empty/placeholder table would silently relocate every character with an "unknown" logout zone home
///             on every login, which is worse than not implementing this at all.
///         </item>
///         <item>
///             Blacklisted-item purge (S04_MyWork02.cpp:361-418, item ids 1451/2268 per
///             Server/ts25login/S07_MyGame01.cpp:87-124) needs a full per-container item read Login has no
///             existing call site for (equip/inventory/trade/store/save-item), and the accompanying behavior
///             contract explicitly flags the per-avatar save-item container's Fenrir persistence analog as
///             unconfirmed -- both are called out in the contract itself as open architectural questions requiring
///             a fresh legacy re-check, not a guess made here.
///         </item>
///     </list>
/// </remarks>
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
            // Character vanished between the two reads above (concurrent delete elsewhere); bail before minting a ticket.
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

        // The directory row only proves shard {shard.ShardId} heartbeated within the last ~17s (15s SQL
        // freshness window + up to 2s of the repository's own read-cache reuse) -- not that it is still alive
        // right now. Without this probe, a shard that crashed inside that window would still get a ticket
        // minted for it, handing the client a dead Host:Port with no recovery (a genuine dead-end handoff).
        if (!await reachabilityProbe.IsReachableAsync(shard.Host, shard.Port, cancellationToken))
        {
            logger.LogWarning(
                "Zone transfer rejected: shard {ShardId} ({Host}:{Port}) failed a reachability probe for character {CharacterId} (account {AccountId}, MapId {MapId}); likely crashed within the directory staleness window -- evicting its row",
                shard.ShardId, shard.Host, shard.Port, character.CharacterId, accountId, character.MapId);
            // Evict immediately rather than letting every other concurrent/subsequent request racing this
            // same dead shard wait out the same ~17s window on its own -- see MarkUnreachableAsync's remarks.
            await directory.MarkUnreachableAsync(shard.ShardId, cancellationToken);
            return new ZoneTransferResult(ZoneTransferOutcome.ShardUnavailable, "", 0, 0);
        }

        await tickets.CreateAsync(accountId, summary.CharacterId, shard.ShardId, options.Value.TicketTtlSeconds,
            sessionToken, accountGrade, cancellationToken);

        logger.LogInformation(
            "Zone transfer ticket minted: account {AccountId} character {CharacterId} -> shard {ShardId} ({Host}:{Port}, MapId {MapId})",
            accountId, character.CharacterId, shard.ShardId, shard.Host, shard.Port, character.MapId);

        // Zone = the persisted MapId the character resumes on (same value AvatarInfoFactory writes to LogoutInfo[0]).
        return new ZoneTransferResult(ZoneTransferOutcome.Success, shard.Host, shard.Port, character.MapId);
    }

    /// <summary>
    ///     Applies <see cref="AvatarVitalsFloor" /> and persists only if it actually changed something -- the
    ///     common case (Life/Mana already at or above floor) never issues the write.
    /// </summary>
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

        // No live shard claims this map -- fail explicitly instead of guessing. Guessing (the previous
        // shards.FirstOrDefault() fallback) silently misrouted the character to a shard that does not host
        // their map, which the destination shard would then have had no sane way to honor.
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
