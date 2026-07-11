using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Inventory;

public interface IPetBagRepository
{

        public ValueTask DepositAsync(int characterId, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, byte petBagSlot, int petItemId, CancellationToken ct);

        public ValueTask WithdrawAsync(int characterId, byte petBagSlot, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, CancellationToken ct);

        public ValueTask RearrangeAsync(int characterId, byte sourceSlot, byte destinationSlot, CancellationToken ct);
}
