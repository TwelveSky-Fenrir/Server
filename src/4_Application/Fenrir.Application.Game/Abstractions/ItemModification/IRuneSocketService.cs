using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum RuneSocketOutcome
{
    Rejected,
    InventoryFull,
    Applied
}

public readonly record struct RuneInsertResult(RuneSocketOutcome Outcome);

public readonly record struct RuneRemoveResult(
    RuneSocketOutcome Outcome,
    byte Page,
    byte Index,
    int ItemIndex,
    ItemStack? GrantedItem = null);

public interface IRuneSocketService
{
    public ValueTask<RuneInsertResult> InsertAsync(RuneSocketRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    public ValueTask<RuneRemoveResult> RemoveAsync(RuneSocketRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
