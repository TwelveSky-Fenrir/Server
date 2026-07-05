using Fenrir.Application.Game.Fishing;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_FISHING_STATE_SEND (opcode 103) -- zone 52 only (S04_MyWork01.cpp:115-122): any other zone would
///     never have registered this opcode in the legacy, so it disconnects here too. Sort 1=cast (gated by a
///     mesh check under the caster's own position), 2=reel; anything else disconnects.
/// </summary>
public sealed class FishingLineHandler : IInlinePacketHandler<FishingLineRequest>
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

        switch (packet.Sort)
        {
            case 1:
                Cast(session, zone, state, characterId);
                return;
            case 2:
                Reel(session, zone, state, characterId);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    private static void Cast(IPacketSession session, Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (!FishingCastResolver.HasWaterAtCurrentPosition(zone.Geometry, state.PosX, state.PosY, state.PosZ))
        {
            session.Send(new FishingLineResponse
            {
                ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = 0, FishingState = 0,
                FishingStep = 0
            });
            zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, state.FishingStep,
                false, false, null));
            return;
        }

        session.Send(new FishingLineResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = 1, FishingState = 1,
            FishingStep = 2
        });
        zone.PostFishingCommand(new FishingZoneCommand(characterId, 1, 2, false, true, 92, DateTime.UtcNow));
    }

    private static void Reel(IPacketSession session, Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0)
        {
            session.Send(new FishingLineResponse
            {
                ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = 0, FishingState = 0,
                FishingStep = 0
            });
            zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, state.FishingStep,
                false, false, null));
            return;
        }

        session.Send(new FishingLineResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = 2, FishingState = 0,
            FishingStep = 0
        });
        zone.PostFishingCommand(new FishingZoneCommand(characterId, 0, 0, false, false, null));
    }
}
