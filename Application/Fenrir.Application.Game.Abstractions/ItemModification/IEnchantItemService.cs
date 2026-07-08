using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum EnchantItemOutcome
{
    Rejected,
    Applied
}

public readonly record struct EnchantItemResult(EnchantItemOutcome Outcome, int ResultCode, int Cost, int NewEnchant);

public interface IEnchantItemService
{
    public ValueTask<EnchantItemResult> EnchantAsync(EnchantItemRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
