using Fenrir.Application.Game.Consumables;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.FishingConsumables.Services;

/// <summary>
///     Business logic for <c>DrinkBottleHandler</c> (op129, CZ_BOTTLE_STATE_SEND): consumes one charge of the
///     selected bottle slot via <see cref="BottleResolver.ResolveDrink" /> and mirrors the new count to the zone.
/// </summary>
public interface IDrinkBottleService
{
    DrinkBottleResult Drink(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value);
}

public sealed class DrinkBottleService : IDrinkBottleService
{
    public DrinkBottleResult Drink(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value)
    {
        var resolved = BottleResolver.ResolveDrink(state.BottleSlots, sort, value);

        switch (resolved.Outcome)
        {
            case BottleResolver.DrinkOutcome.Silent:
                return new DrinkBottleResult(DrinkBottleOutcome.Silent, 0, 0);
            case BottleResolver.DrinkOutcome.Rejected:
                return new DrinkBottleResult(DrinkBottleOutcome.Rejected, 0, 0);
        }

        zone.PostDrinkBottleCommand(new DrinkBottleZoneCommand(characterId, value, resolved.NewCount, state.MaxLife));

        return new DrinkBottleResult(DrinkBottleOutcome.Success, value, BottleResolver.DrunkDurationTicks);
    }
}

public enum DrinkBottleOutcome
{
    Silent,
    Rejected,
    Success
}

public sealed record DrinkBottleResult(DrinkBottleOutcome Outcome, int BottleIndex, int DrunkDurationTicks);
