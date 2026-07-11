using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmMaxStatService(IEventLogRepository eventLog, ILogger<GmMaxStatService> logger)
    : IGmMaxStatService
{
    private const byte AppliedOutcome = 1;

    private const int MaxStatValue = 10000;

    private const int MaxSkillPoints = 3000;

    public async ValueTask HandleAsync(ZoneClientSession zoneSession, PlayerRuntimeState state, Zone zone,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Admin))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Admin-tier MAX stat-cheat without sufficient privilege -- forcing logout, no reply",
                zoneSession.CharacterId);
            zoneSession.Abort(DisconnectReason.GmCommandLogout);
            return;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, StatVit: MaxStatValue, StatStr: MaxStatValue,
                    StatInt: MaxStatValue, StatDex: MaxStatValue, SkillPoints: MaxSkillPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped MAX stat-cheat mirror for character {CharacterId} -- forcing logout regardless, matching legacy's own unconditional disconnect",
                zone.MapId, state.CharacterId);

        await eventLog.LogAsync(GmActionEventCodes.MaxStatCheat, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, AppliedOutcome,
            $"VIT=STR=INT=DEX={MaxStatValue};SkillPoints={MaxSkillPoints}", cancellationToken);

        logger.LogWarning(
            "Character {CharacterId} applied the Admin-tier MAX stat-cheat (VIT/STR/INT/DEX -> {MaxStatValue}, SkillPoints -> {MaxSkillPoints}) -- forcing logout, no reply",
            state.CharacterId, MaxStatValue, MaxSkillPoints);
        zoneSession.Abort(DisconnectReason.GmCommandLogout);
    }
}
