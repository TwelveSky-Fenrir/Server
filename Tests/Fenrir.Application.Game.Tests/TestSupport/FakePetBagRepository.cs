using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Inventory;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>In-memory stand-in for the new (wave7/C8) <see cref="IPetBagRepository" /> -- records the last call only.</summary>
internal sealed class FakePetBagRepository : IPetBagRepository
{
    public (int CharacterId, byte InventoryContainer, IReadOnlyList<CharacterItemSlotTvp> InventoryItems,
        byte PetBagSlot, int PetItemId)? LastDeposit { get; private set; }

    public (int CharacterId, byte PetBagSlot, byte InventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> InventoryItems)? LastWithdraw { get; private set; }

    public (int CharacterId, byte SourceSlot, byte DestinationSlot)? LastRearrange { get; private set; }

    public ValueTask DepositAsync(int characterId, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, byte petBagSlot, int petItemId, CancellationToken ct)
    {
        LastDeposit = (characterId, inventoryContainer, inventoryItems, petBagSlot, petItemId);
        return ValueTask.CompletedTask;
    }

    public ValueTask WithdrawAsync(int characterId, byte petBagSlot, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, CancellationToken ct)
    {
        LastWithdraw = (characterId, petBagSlot, inventoryContainer, inventoryItems);
        return ValueTask.CompletedTask;
    }

    public ValueTask RearrangeAsync(int characterId, byte sourceSlot, byte destinationSlot, CancellationToken ct)
    {
        LastRearrange = (characterId, sourceSlot, destinationSlot);
        return ValueTask.CompletedTask;
    }
}
