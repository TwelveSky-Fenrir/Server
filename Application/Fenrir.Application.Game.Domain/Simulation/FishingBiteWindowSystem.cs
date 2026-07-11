using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class FishingBiteWindowSystem : ISimulationSystem
{

        public const short FishingZoneNumber = 52;

        private const int BiteWindowArmedResultSort = 3;

    private static readonly TimeSpan BiteWindowDelay = TimeSpan.FromMinutes(1);

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (zone.MapId != FishingZoneNumber)
            return;

        var now = DateTime.UtcNow;

        foreach (var state in zone.Players)
        {
            if (state.FishingState == 0 || state.FishingStep != 2 || state.FishingCastAtUtc is not { } castAt ||
                now - castAt < BiteWindowDelay)
                continue;

            state.FishingStep = 3;

            state.Session.Send(new FishingProgressResponse
            {
                ServerIndex = state.CharacterId,
                UniqueNumber = state.UniqueNumber,
                Result = BiteWindowArmedResultSort,
                FishingState = state.FishingState,
                FishingStep = state.FishingStep
            });
        }
    }
}
