using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmBlockAvatarService" />'s own remarks for the full behavior contract. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1487-1515 (gate, target lookup, ban dispatch, target disconnect, the
///     return-vs-break control-flow divergence versus sibling case 518 KICK) ; :1510 (mGAMELOG.GL_632_GM_BLOCK,
///     defined non-dead at Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:629, declared
///     Server/ts25zone/H06_MyUpperCom.h:303 -- a real, non-dead audit-log call written before the ban, the same
///     pattern as sibling case 518 KICK's own GL_631_GM_KICK call at S04_MyWork04.cpp:1482) ; :2121-2122 (shared
///     PROCESS_DATA_SEND/RECV reply step reached only by sub-commands that fall through, which sends this on
///     opcode 23 -- legacy has no command-specific reply message for GM-BLOCK) ;
///     Server/Header/Protocol/ZONE.h:462-468, :1437-1438 (the opcode-23 ZC_PROCESS_DATA_RECV shape:
///     Result+Sort+Data+RuneValue) ; Server/Header/Protocol/DEFINE.h:377-381 (Data is 130 bytes under the
///     production build, giving 143 bytes total header included) ;
///     Server/ts25zone/S07_MyGame03.cpp:4422-4451 (SearchAvatar: process-local, ready-state-only, first-match,
///     excludes the caller's own index by default) ; Server/Header/datetime.h:195-218 (ReturnAddDate: "today
///     plus 365*30 days," a concrete far-future date, never a null/forever sentinel).
/// </summary>
public sealed class GmBlockAvatarService(
    ZoneRegistry zones,
    IBanRepository bans,
    IEventLogRepository eventLog,
    ILogger<GmBlockAvatarService> logger) : IGmBlockAvatarService
{
    private const int ResultTargetNotFound = 1; // legacy tResult's own default-initialized value (S04_MyWork04.cpp:305)

    // Legacy PROCESS_DATA_SEND sub-command selector for [GM]-BLOCK (S04_MyWork04.cpp:1487) -- echoed back
    // verbatim as GenericActionResponse.Sort on the "not found" reply, exactly like every sibling sub-command
    // that falls through to the shared epilogue.
    private const int GmBlockSort = 519;

    // Legacy echoes back the same 130-byte opaque tData buffer it received (S04_MyWork04.cpp:2121-2122). This
    // service only receives the decoded GmBlockAvatarPayload (AvatarName), not GenericActionHandler's own raw
    // 130-byte tData buffer that payload was read out of, so there is no opaque payload here to echo verbatim
    // -- a zero-filled buffer is sent instead. The client does not derive anything from Data's content on this
    // failure path; only the total wire size (143 bytes) has to match what an unmodified client's dispatch
    // table expects at opcode 23.
    private static readonly byte[] EmptyGenericActionData = new byte[130];

    // Legacy: ReturnAddDate(0, 365*30) -- "today plus 10,950 days," a concrete future date, not NULL/forever
    // (Database/Tables/admin/Bans.sql's own null-vs-concrete-date convention).
    private static readonly TimeSpan BlockDuration = TimeSpan.FromDays(365 * 30);

    public async ValueTask HandleAsync(GmBlockAvatarPayload packet, ZoneClientSession zoneSession,
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
            // Legacy's real reply shape for this outcome: the shared opcode-23 PROCESS_DATA_RECV envelope,
            // never a command-specific message (S04_MyWork04.cpp:2121-2122).
            zoneSession.Send(new GenericActionResponse
            {
                Result = ResultTargetNotFound, Sort = GmBlockSort, Data = EmptyGenericActionData, RuneValue = 0
            });
            return;
        }

        // Legacy sends both uUserIdx (account) and uCharIdx (character) in the same dispatch
        // (Server/Header/CSQLAvatar.cpp:743-754) -- usp_Ban_Create accepts both in one call, matching that shape.
        var targetAccountId = ((ZoneClientSession)target.Session).AccountId;

        await bans.CreateAsync(targetAccountId, target.CharacterId, BanReason.GmManualBlock,
            DateTime.UtcNow.Add(BlockDuration), cancellationToken);

        // Legacy writes its GL_632_GM_BLOCK audit-log call immediately after issuing the ban
        // (S04_MyWork04.cpp:1510) -- same placement, same GmAction category, as the sibling GL_631_GM_KICK call
        // GmBasicCommandService.HandleKickAsync makes for case 518.
        await eventLog.LogAsync(GmActionEventCodes.Block, EventLogCategory.GmAction, zoneSession.AccountId,
            callerCharacterId, targetAccountId, target.CharacterId, null, null, null, null, null, 1,
            $"TargetName={target.Name}", cancellationToken);

        logger.LogWarning(
            "GM character {GmCharacterId} blocked avatar {TargetCharacterId} ({TargetName})",
            callerCharacterId, target.CharacterId, target.Name);

        ((ZoneClientSession)target.Session).Abort(DisconnectReason.Banned);

        // No response to the caller: legacy's case 519 returns immediately after the ban, bypassing the shared
        // epilogue every sibling sub-command (e.g. case 518 KICK) reaches via break -- silence is the "it
        // worked" signal on this path. Do not add a success ack here to "complete" the shape.
    }
}
