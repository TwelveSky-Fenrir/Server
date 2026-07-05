using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

/// <summary>
///     Business logic for <c>DrinkBottleHandler</c> (op129, CZ_BOTTLE_STATE_SEND): consumes one charge of the
///     selected bottle slot via <see cref="BottleResolver.ResolveDrink" /> and
///     mirrors the new count to the zone.
/// </summary>
public interface IDrinkBottleService
{
    public DrinkBottleResult Drink(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value);
}

public enum DrinkBottleOutcome
{
    Silent,
    Rejected,
    Success
}

public sealed record DrinkBottleResult(DrinkBottleOutcome Outcome, int BottleIndex, int DrunkDurationTicks);
