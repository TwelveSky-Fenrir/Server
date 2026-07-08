using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_FISHING_RESULT_SEND (opcode 104) -- same zone-52 gating as <see cref="FishingLineHandler" />. Sort
///     1=poll bite (silent no-op unless step==3 and &gt;=1 minute elapsed since cast -- no reply at all on
///     gate failure, matching the legacy exactly), 2=recast, 3=client-forced step (0..5, else disconnect).
///     Reaching step 4/5 here always broadcasts action Sort=93 to self + AOI neighbors (mirrors <c>Broadcast22</c>
///     + explicit self <c>USEND</c>).
/// </summary>
public sealed class FishingProgressHandler(
    IFishingProgressService fishingProgressService,
    ILogger<FishingProgressHandler> logger)
    : IInlinePacketHandler<FishingProgressRequest>
{
    public void Handle(in FishingProgressRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Sort=1 (poll bite) is a client-driven poll while waiting for a bite, so this entry log must not
        // format/box unconditionally on every poll -- gate behind IsEnabled like ContinueSkillUseHandler's
        // own per-tick pulse.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: FishingProgressRequest (op104) received for character {CharacterId}, sort {Sort}",
                session.SessionId, characterId, packet.Sort);

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            logger.LogWarning(
                "Fishing-progress request rejected for character {CharacterId}: map {MapId} is not the fishing zone -- aborting session",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        FishingProgressResult? result;
        switch (packet.Sort)
        {
            case 1:
                result = fishingProgressService.PollBite(zone, state, characterId);
                break;
            case 2:
                result = fishingProgressService.Recast(zone, state, characterId);
                logger.LogInformation("Character {CharacterId} recast fishing line", characterId);
                break;
            case 3:
                if (packet.FishingStep is < 0 or > 5)
                {
                    logger.LogWarning(
                        "Fishing-progress request rejected for character {CharacterId}: invalid step {FishingStep} -- aborting session",
                        characterId, packet.FishingStep);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                result = fishingProgressService.ForceStep(zone, state, characterId, packet.FishingStep);
                break;
            default:
                logger.LogWarning(
                    "Fishing-progress request rejected for character {CharacterId}: invalid sort {Sort} -- aborting session",
                    characterId, packet.Sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        if (result is not { } value)
            return;

        if (value.FishingStep is 4 or 5)
            logger.LogInformation("Character {CharacterId} fishing bite at step {FishingStep}", characterId,
                value.FishingStep);

        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = value.ResultSort,
            FishingState = value.FishingState, FishingStep = value.FishingStep
        });
    }
}
