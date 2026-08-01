using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class TribeSymbolDamageModifierSystem(
    WorldStateService worldState,
    TribeSymbolCombatModifiers modifiers) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var ally = worldState.GetAllyOf(tribeId);
            var ownSlotOwner = worldState.GetTribeSymbolOwner(tribeId);
            var ownsOwnSlot = IsControlledByTribeOrAlly(ownSlotOwner, tribeId, ally);

            modifiers.SetDamageDownPenalty(tribeId,
                ownsOwnSlot ? 0f : TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);

            modifiers.SetDamageUpBonusIncrementCount(tribeId,
                ComputeDamageUpBonusIncrementCount(tribeId, ally, ownsOwnSlot));
        }
    }

    private int ComputeDamageUpBonusIncrementCount(byte tribeId, byte? ally, bool ownsOwnSlot)
    {
        if (!ownsOwnSlot)
            return 0;

        var count = 0;

        for (byte otherSlot = 0; otherSlot < WorldStateService.TribeCount; otherSlot++)
        {
            if (otherSlot == tribeId)
                continue;
            if (IsControlledByTribeOrAlly(worldState.GetTribeSymbolOwner(otherSlot), tribeId, ally))
                count++;
        }

        if (worldState.World.MonsterSymbol is { } monsterSymbolOwner &&
            IsControlledByTribeOrAlly(monsterSymbolOwner, tribeId, ally))
            count++;

        if (count > 0)
            return count;

        return IsStrictlyLowestEligibleTribeByCombinedPoints(tribeId) ? 1 : 0;
    }

    private static bool IsControlledByTribeOrAlly(byte ownerTribeId, byte tribeId, byte? ally)
    {
        return ownerTribeId == tribeId || (ally is { } allyId && ownerTribeId == allyId);
    }

    private bool IsStrictlyLowestEligibleTribeByCombinedPoints(byte tribeId)
    {
        var combined = new int[WorldStateService.TribeCount];
        for (byte i = 0; i < WorldStateService.TribeCount; i++)
        {
            var points = worldState.GetTribe(i).Points;
            if (worldState.GetAllyOf(i) is { } allyOfI)
                points += worldState.GetTribe(allyOfI).Points;
            combined[i] = points;
        }

        var ownCombined = combined[tribeId];
        if (ownCombined < TribeSymbolCombatModifiers.SmallTribeAdvantagePointFloor)
            return false;

        for (byte i = 0; i < WorldStateService.TribeCount; i++)
        {
            if (i == tribeId)
                continue;
            if (combined[i] < TribeSymbolCombatModifiers.SmallTribeAdvantagePointFloor)
                continue;
            if (combined[i] <= ownCombined)
                return false;
        }

        return true;
    }
}
