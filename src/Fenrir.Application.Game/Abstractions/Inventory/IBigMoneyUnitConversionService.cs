using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Inventory;

public interface IBigMoneyUnitConversionService
{
    public ValueTask<GenericActionResult> ConvertAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, CancellationToken cancellationToken);
}
