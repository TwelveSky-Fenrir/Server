using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmCallPvpService(
    ZoneRegistry zones,
    IEventLogRepository eventLog,
    ILogger<GmCallPvpService> logger,
    DuelRegistry? duelRegistry = null) : IGmCallPvpService
{
    private const int Sort = 599;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1;

    private static readonly (float X, float Y, float Z) DuelSlot1Coordinate = (-232f, 36f, 2f);
    private static readonly (float X, float Y, float Z) DuelSlot2Coordinate = (232f, 36f, 2f);

    public async ValueTask HandleAsync(GmCallPvpPayload packet, byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM-CALLPVP command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.DuelSlot is not (1 or 2))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Character {CharacterId} GM-CALLPVP rejected: invalid duel slot {DuelSlot}",
                    zoneSession.CharacterId, packet.DuelSlot);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = Sort, Data = data, RuneValue = 0 });
            return;
        }

        var destination = packet.DuelSlot == 1 ? DuelSlot1Coordinate : DuelSlot2Coordinate;

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });

        var callerCharacterId = zoneSession.CharacterId!.Value;
        var callerAccountId = zoneSession.AccountId;

        foreach (var zone in zones.Zones)
        foreach (var candidate in zone.Players)
        {
            if (!string.Equals(candidate.Name, packet.TargetName, StringComparison.Ordinal))
                continue;

            if (candidate.IsMovingZone)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug(
                        "GM-CALLPVP: candidate character {CandidateCharacterId} ({CandidateName}) skipped -- mid zone-transfer",
                        candidate.CharacterId, candidate.Name);
                continue;
            }

            if (duelRegistry is not null &&
                (duelRegistry.IsNegotiating(candidate.CharacterId) ||
                 duelRegistry.TryGetActiveDuel(candidate.CharacterId, out _)))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug(
                        "GM-CALLPVP: candidate character {CandidateCharacterId} ({CandidateName}) skipped -- already engaged in a duel-related state",
                        candidate.CharacterId, candidate.Name);
                continue;
            }

            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(candidate.CharacterId, TeleportTo: destination,
                        NeighborActionBroadcast: true, ResetAfkTick: true), cancellationToken))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped GM-CALLPVP relocation mirror for character {CharacterId}",
                    zone.MapId, candidate.CharacterId);


            await eventLog.LogAsync(GmDuelAndInventoryActionEventCodes.CallPvpRelocate, EventLogCategory.GmAction,
                callerAccountId, callerCharacterId, ((IZoneSession)candidate.Session).AccountId,
                candidate.CharacterId, null, null, null, null, null, 1,
                $"DuelSlot={packet.DuelSlot};TargetName={candidate.Name}", cancellationToken);

            logger.LogInformation(
                "Character {CharacterId} applied the Basic-tier GM-CALLPVP command against character {TargetCharacterId} ({TargetName}), duel slot {DuelSlot}",
                callerCharacterId, candidate.CharacterId, candidate.Name, packet.DuelSlot);
        }
    }
}
