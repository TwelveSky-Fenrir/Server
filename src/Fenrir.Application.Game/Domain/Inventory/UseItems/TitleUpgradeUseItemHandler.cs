using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class TitleUpgradeUseItemHandler(ILogger<TitleUpgradeUseItemHandler> logger) : IUseItemHandler
{
    public const int ItemId = 891;

    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Character {CharacterId} op23 title-upgrade (891, \"God of Fist\"): dead legacy feature (WUSE_ITEM_891 " +
            "never #define'd, rank 13 unreachable) — no-op, no consumption, no state change",
            context.CharacterId);
        return ValueTask.FromResult(UseItemResponses.Fail(context.Page, context.Index));
    }
}
