using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class MountBoxUseItemHandler(ILogger<MountBoxUseItemHandler> logger) : IUseItemHandler
{

        public const int ItemId = 635;

    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Character {CharacterId} used mount box (635) but the eight mount reward ids are not yet cataloged: box left unconsumed, result 1 (see MountBoxUseItemHandler TODO)",
            context.CharacterId);
        return ValueTask.FromResult(UseItemResponses.Fail(context.Page, context.Index));
    }
}
