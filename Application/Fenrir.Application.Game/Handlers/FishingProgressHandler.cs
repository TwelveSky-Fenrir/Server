using Fenrir.Application.Game.Handlers.FishingConsumables.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_FISHING_RESULT_SEND (opcode 104) -- same zone-52 gating as <see cref="FishingLineHandler" />. Sort
///     1=poll bite (silent no-op unless step==3 and &gt;=1 minute elapsed since cast -- no reply at all on
///     gate failure, matching the legacy exactly), 2=recast, 3=client-forced step (0..5, else disconnect).
///     Reaching step 4/5 here always broadcasts action Sort=93 to self + AOI neighbors (mirrors <c>Broadcast22</c>
///     + explicit self <c>USEND</c>).
/// </summary>
public sealed class FishingProgressHandler(IFishingProgressService fishingProgressService)
    : IInlinePacketHandler<FishingProgressRequest>
{
    public void Handle(in FishingProgressRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
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
                break;
            case 3:
                if (packet.FishingStep is < 0 or > 5)
                {
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                result = fishingProgressService.ForceStep(zone, state, characterId, packet.FishingStep);
                break;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        if (result is not { } value)
            return;

        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = value.ResultSort,
            FishingState = value.FishingState, FishingStep = value.FishingStep
        });
    }
}
