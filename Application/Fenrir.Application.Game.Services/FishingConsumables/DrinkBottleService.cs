using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.FishingConsumables;

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
