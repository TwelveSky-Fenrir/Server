using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmBlockAvatarService" />'s own remarks for the full behavior contract. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1487-1515 (gate, target lookup, ban dispatch, target disconnect, the
///     return-vs-break control-flow divergence versus sibling case 518 KICK) ;
///     Server/ts25zone/S07_MyGame03.cpp:4422-4451 (SearchAvatar: process-local, ready-state-only, first-match,
///     excludes the caller's own index by default) ; Server/Header/datetime.h:195-218 (ReturnAddDate: "today
///     plus 365*30 days," a concrete far-future date, never a null/forever sentinel).
/// </summary>
public sealed class GmBlockAvatarService(
    ZoneRegistry zones,
    IBanRepository bans,
    ILogger<GmBlockAvatarService> logger) : IGmBlockAvatarService
{
    private const int ResultTargetNotFound = 1; // legacy tResult's own default-initialized value (S04_MyWork04.cpp:305)

    // Legacy: ReturnAddDate(0, 365*30) -- "today plus 10,950 days," a concrete future date, not NULL/forever
    // (Database/Tables/admin/Bans.sql's own null-vs-concrete-date convention).
    private static readonly TimeSpan BlockDuration = TimeSpan.FromDays(365 * 30);

    public async ValueTask HandleAsync(GmBlockAvatarRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        // uUserSort < 1 gate (Server/ts25zone/S04_MyWork04.cpp:1489): unauthorized caller is disconnected
        // outright, no reply of any kind -- matches this project's "malformed/unauthorized input -> disconnect"
        // treatment elsewhere, not a graceful error reply. The secondary uAuthInfo.BlockFlag==0 check in legacy
        // is dead code (its body is commented out) and is deliberately not ported.
        if (!zoneSession.IsGm)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var callerCharacterId = zoneSession.CharacterId!.Value;

        // Process-local (this GameServer's own hosted maps), case-insensitive, first match wins -- mirrors
        // legacy's SearchAvatar. Self-targeting collapses to "not found" (SearchAvatar's own default
        // tWithSameIndex=FALSE excludes the caller's own index, Server/ts25zone/H07_MyGame.h:599). Whether this
        // should extend across shards is an explicit, open design question this pass does not resolve.
        var found = zones.TryGetPlayerAndZoneByName(packet.AvatarName, out var target, out _);
        if (!found || target!.CharacterId == callerCharacterId)
        {
            zoneSession.Send(new GmBlockAvatarResponse { Result = ResultTargetNotFound });
            return;
        }

        // Legacy sends both uUserIdx (account) and uCharIdx (character) in the same dispatch
        // (Server/Header/CSQLAvatar.cpp:743-754) -- usp_Ban_Create accepts both in one call, matching that shape.
        var targetAccountId = ((ZoneClientSession)target.Session).AccountId;

        await bans.CreateAsync(targetAccountId, target.CharacterId, BanReason.GmManualBlock,
            DateTime.UtcNow.Add(BlockDuration), cancellationToken);

        logger.LogWarning(
            "GM character {GmCharacterId} blocked avatar {TargetCharacterId} ({TargetName})",
            callerCharacterId, target.CharacterId, target.Name);

        ((ZoneClientSession)target.Session).Abort(DisconnectReason.Banned);

        // No response to the caller: legacy's case 519 returns immediately after the ban, bypassing the shared
        // epilogue every sibling sub-command (e.g. case 518 KICK) reaches via break -- silence is the "it
        // worked" signal on this path. Do not add a success ack here to "complete" the shape.
    }
}
