using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmExpGrantService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:959-1005 (case 503 full handler body: tier gate at :961-965, inline
///     <c>GMEXP_DATA{type,exp}</c> at :966-971, mode-0 branch and the huge-magnitude shortcut at :972-986,
///     mode-1 branch at :987-990, dead mode-3 block commented at :991-1001, unconditional <c>tResult = 0</c> at
///     :1003) ; Server/ts25zone/S07_MyGame03.cpp:161-322 (<c>MyUtil::ProcessForExperience</c>, the shared
///     leveling/experience routine both branches deposit into -- see <see cref="Zone" />'s own
///     <c>ApplyGmSelfExperienceGrantCommand</c>/<c>ApplyGmCharacterExperienceGrant</c> for where the actual
///     read-decide-mutate work happens, since it must run on the target zone's own tick thread).
/// </summary>
public sealed class GmExpGrantService(
    IEventLogRepository eventLog,
    ILogger<GmExpGrantService> logger) : IGmExpGrantService
{
    private const int Sort = 503;

    /// <summary>Unconditional once the tier gate passes -- see the source behavior contract's own Outputs/Error semantics.</summary>
    private const int AcceptedResult = 0;

    /// <summary>game.EventLog.Outcome for this audit row -- always success, mirroring <see cref="AcceptedResult" />.</summary>
    private const byte SuccessOutcome = 1;

    public async ValueTask HandleAsync(GmExpGrantPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier grant-experience-to-self command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!await zone.PostGmSelfExperienceGrantCommandAndWaitAsync(
                new GmSelfExperienceGrantZoneCommand(state.CharacterId, packet.Type, packet.Exp), cancellationToken))
            logger.LogError(
                "Zone {MapId} GM-experience inbox full: dropped grant-experience-to-self mirror for character {CharacterId} (mode {Mode}, magnitude {Magnitude}) -- reporting accepted regardless, matching legacy's own unconditional success result",
                zone.MapId, state.CharacterId, packet.Type, packet.Exp);

        await eventLog.LogAsync(GmActionEventCodes.ExpGrant, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, SuccessOutcome,
            $"Mode={packet.Type};Magnitude={packet.Exp}", cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} applied the Elevated-tier grant-experience-to-self command (mode {Mode}, magnitude {Magnitude})",
            state.CharacterId, packet.Type, packet.Exp);

        zoneSession.Send(new GenericActionResponse { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}
