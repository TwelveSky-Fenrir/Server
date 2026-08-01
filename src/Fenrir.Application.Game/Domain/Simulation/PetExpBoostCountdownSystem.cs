using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PetExpBoostCountdownSystem : ISimulationSystem
{
    private const int DoublePetExpTimeStatSort = 44;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed);
    }

    private static void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.PetExpX2TimeAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.PetExpX2TimeAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.PetExpX2TimeAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        if (state.PetExpX2Time < 1)
            return;

        var petItemId = state.Inventory.GetContainer(ContainerMatrix.Equipment)
            .TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        if (DoublePetExpTimerGate.IsAtFreezeThreshold(petItemId, state.PetGrowth))
            return;

        state.PetExpX2Time = Math.Max(0, state.PetExpX2Time - minutesElapsed);

        // Server/ts25zone/S07_MyGame04.cpp:950 -- unicast au seul porteur, jamais aux voisins.
        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = DoublePetExpTimeStatSort, Value = state.PetExpX2Time, Value2 = 0 });
    }
}
