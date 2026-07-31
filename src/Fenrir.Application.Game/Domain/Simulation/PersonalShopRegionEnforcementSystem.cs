using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PersonalShopRegionEnforcementSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var state in zone.Players)
        {
            if (!state.PshopOpen || state.IsMovingZone)
                continue;

            if (PersonalShopRegionPolicy.IsWithinPermittedRegion(zone.MapId, state.Tribe, state.PosX, state.PosY,
                    state.PosZ))
                continue;

            (toDisconnect ??= []).Add(state);
        }

        if (toDisconnect is null)
            return;

        foreach (var state in toDisconnect)
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
    }
}
