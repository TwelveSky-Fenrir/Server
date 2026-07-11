using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class HoisundoCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (ResolveBroadcastSort(zone.MapId) is not { } sort)
            return;

        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var state in zone.Players)
        {
            if (state.IsMovingZone)
                continue;

            state.HoisundoAccrualTicks += legacyTicksElapsed;
            if (state.HoisundoAccrualTicks < SimulationClock.PlayTimeAccrualLegacyTicks)
                continue;

            state.HoisundoAccrualTicks -= SimulationClock.PlayTimeAccrualLegacyTicks;

            if (state.HoisundoTimeRemaining > 0)
            {
                state.HoisundoTimeRemaining--;
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = sort, Value = state.HoisundoTimeRemaining, Value2 = 0 });
            }

            if (state.HoisundoTimeRemaining < 1)
                (toDisconnect ??= []).Add(state);
        }

        if (toDisconnect is null)
            return;

        foreach (var state in toDisconnect)
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.TimedZoneExpired);
    }

    private static int? ResolveBroadcastSort(short mapId)
    {
        return mapId switch
        {
            234 => 33,
            235 => 34,
            236 => 35,
            237 => 36,
            238 => 37,
            239 => 38,
            240 => 39,
            _ => null
        };
    }
}
