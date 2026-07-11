using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmSetPvpPointService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1755-1769 (full case 598 body: the uUserSort &gt;= 1 gate, the duel-slot
///     validation, and the single success-indicator assignment that is the entirety of this command's live
///     effect) ; Server/ts25zone/S04_MyWork04.cpp:303-305 (the dispatcher's own default-failure result value,
///     grounding the "abandoned" outcome for an invalid duel slot) ; Server/Header/Protocol/STRUCT.h:1285-1289
///     (payload shape: duel-slot then point-value, the latter never read anywhere in the read source tree).
/// </summary>
public sealed class GmSetPvpPointService(ILogger<GmSetPvpPointService> logger) : IGmSetPvpPointService
{
    private const int Sort = 598;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1; // legacy tResult's own default-initialized/rejected value

    public ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM-SETPVPPOINT command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        if (packet.DuelSlot is not (1 or 2))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Character {CharacterId} GM-SETPVPPOINT rejected: invalid duel slot {DuelSlot}",
                    zoneSession.CharacterId, packet.DuelSlot);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = Sort, Data = data, RuneValue = 0 });
            return ValueTask.CompletedTask;
        }

        // Confirmed functional no-op once validated -- see IGmSetPvpPointService's own remarks and
        // GmSetPvpPointPayload.PointValue's own remarks. No character field, world state, or persisted value of
        // any kind is read or written here; do not add a mutation this contract does not describe.
        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
        return ValueTask.CompletedTask;
    }
}
