using Fenrir.Application.Game.Handlers.FishingConsumables.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_FISHING_STATE_SEND (opcode 103) -- zone 52 only (S04_MyWork01.cpp:115-122): any other zone would
///     never have registered this opcode in the legacy, so it disconnects here too. Sort 1=cast (gated by a
///     mesh check under the caster's own position), 2=reel; anything else disconnects.
/// </summary>
public sealed class FishingLineHandler(IFishingLineService fishingLineService) : IInlinePacketHandler<FishingLineRequest>
{
    public const short FishingZoneNumber = 52;

    public void Handle(in FishingLineRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        FishingLineResult result;
        switch (packet.Sort)
        {
            case 1:
                result = fishingLineService.Cast(zone, state, characterId);
                break;
            case 2:
                result = fishingLineService.Reel(zone, state, characterId);
                break;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        session.Send(new FishingLineResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = result.Result,
            FishingState = result.FishingState, FishingStep = result.FishingStep
        });
    }
}
