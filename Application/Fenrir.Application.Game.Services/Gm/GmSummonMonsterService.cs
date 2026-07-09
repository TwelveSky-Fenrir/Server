using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmSummonMonsterService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1133-1145 (case 506 full handler: payload cast to
///     <c>GM_MONCALL_RECV</c> at :1135, tier gate at :1136-1140, spawn call at :1141, gamelog call at :1142,
///     unconditional success at :1143) ; Server/Header/Protocol/STRUCT.h:1270-1273 (the physically-shared,
///     four-way-aliased <c>{int tValue;}</c> struct this command's own payload shape derives from -- see
///     <see cref="GmSummonMonsterPayload" />'s own remarks for why it is nonetheless modeled as its own
///     dedicated type) ; Server/ts25zone/H10_MySummon.h:38, Server/ts25zone/S10_MySummon.cpp:1254
///     (<c>SummonMonsterForSpecial(tMonsterNumber, tMonsterLocation, tCheckExistMonster, tUserIndex=-1)</c> --
///     no duplicate-of-same-template check requested, no owner bound -- see <see cref="Zone" />'s own
///     <c>SpawnGmSummonedMonster</c> for how these two properties fall out of Fenrir's own slot-keyed monster
///     pool without needing to be reproduced explicitly) ;
///     Server/ts25zone/H06_MyUpperCom.h:298, Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:599-603
///     (<c>GL_628_GM_MONCALL</c> audit-log call: log-sort 628, GM account id, avatar name, literal
///     <c>(MONSTER)</c> tag, monster template id, current zone number -- <see cref="IEventLogRepository.LogAsync" />
///     has no dedicated remote-log-sort/free-text-tag parameters, so this method packs the closest available
///     fields into the free-text <c>payload</c> column, same "best-effort inference, not an asserted
///     byte-exact citation" posture <see cref="GmCreateItemService" />'s own remarks already document for its
///     own audit-log call).
/// </summary>
/// <remarks>
///     Known functional overlap with the separately-modeled <c>boss &lt;id&gt;</c> chat command
///     (<c>Fenrir.Application.Game.Domain.Social.Chat.LocalChatGmCommandParser</c>, currently a deliberate
///     no-op pending its own spawn-monster-by-id subsystem -- see
///     <c>Fenrir.Application.Game.Services.Chat.LocalChatService</c>'s own remarks) -- flagged, not resolved,
///     per the source behavior contract's own "must not be collapsed into
///     one" instruction: this command performs no id validation and writes a durable audit-log entry;
///     <c>boss</c> would reject an id below 1 and broadcast a server-wide notice, and writes no audit-log entry.
///     The two must remain independently invokable, distinct code paths.
/// </remarks>
public sealed class GmSummonMonsterService(
    IEventLogRepository eventLog,
    ILogger<GmSummonMonsterService> logger) : IGmSummonMonsterService
{
    private const int Sort = 506;

    /// <summary>Unconditional once the tier gate passes -- no validation of the requested template id occurs.</summary>
    private const int AcceptedResult = 0;

    /// <summary>game.EventLog.Outcome for this audit row -- always success, mirroring <see cref="AcceptedResult" />.</summary>
    private const byte SuccessOutcome = 1;

    public async ValueTask HandleAsync(GmSummonMonsterPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier summon-monster command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, GmSummonMonsterTemplateId: packet.Value),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped summon-monster mirror for character {CharacterId} (monster {MonsterId}) -- reporting accepted regardless, matching legacy's own unconditional success result",
                zone.MapId, state.CharacterId, packet.Value);

        await eventLog.LogAsync(GmActionEventCodes.SummonMonster, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, SuccessOutcome,
            $"MonsterId={packet.Value};MapId={zone.MapId}", cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} applied the Elevated-tier summon-monster command (monster {MonsterId}, map {MapId})",
            state.CharacterId, packet.Value, zone.MapId);

        zoneSession.Send(new GenericActionResponse { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}
