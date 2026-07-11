using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

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
