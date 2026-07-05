using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Fishing;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_FISHING_RESULT_SEND (opcode 104) -- same zone-52 gating as <see cref="FishingLineHandler" />. Sort
///     1=poll bite (silent no-op unless step==3 and &gt;=1 minute elapsed since cast -- no reply at all on
///     gate failure, matching the legacy exactly), 2=recast, 3=client-forced step (0..5, else disconnect).
///     Reaching step 4/5 here always broadcasts action Sort=93 to self + AOI neighbors (mirrors <c>Broadcast22</c>
///     + explicit self <c>USEND</c>).
/// </summary>
public sealed class FishingProgressHandler : IInlinePacketHandler<FishingProgressRequest>
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

        switch (packet.Sort)
        {
            case 1:
                PollBite(session, zone, state, characterId);
                return;
            case 2:
                Recast(session, zone, state, characterId);
                return;
            case 3:
                if (packet.FishingStep is < 0 or > 5)
                {
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                Reply(session, zone, state, characterId, 3, packet.FishingStep, null);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    private static void PollBite(IPacketSession session, Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0 || state.FishingStep != 3 || state.FishingCastAtUtc is not { } castAt ||
            DateTime.UtcNow - castAt < TimeSpan.FromMinutes(1))
            return;

        var step = FishingRewardResolver.RollBite(SystemRandomSource.Instance) ? 4 : 5;
        Reply(session, zone, state, characterId, 1, step, null);
    }

    private static void Recast(IPacketSession session, Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0)
        {
            Reply(session, zone, state, characterId, 2, state.FishingStep, null);
            return;
        }

        Reply(session, zone, state, characterId, 2, 2, DateTime.UtcNow);
    }

    private static void Reply(IPacketSession session, Zone zone, PlayerRuntimeState state, int characterId,
        int resultSort, int newStep, DateTime? castAt)
    {
        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = resultSort,
            FishingState = state.FishingState, FishingStep = newStep
        });

        var caught = newStep is 4 or 5;
        zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, newStep,
            state.CatchingFish || caught, caught, caught ? 93 : null, castAt));
    }
}
