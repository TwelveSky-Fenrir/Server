using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Inventory;

namespace Fenrir.Data.Inventory;

/// <inheritdoc cref="IPetBagRepository" />
public sealed record PetBagRepository(ICaeriusNetDbContext Db) : IPetBagRepository
{
    public async ValueTask DepositAsync(int characterId, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, byte petBagSlot, int petItemId, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterPetBag_Deposit", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("InventoryContainer", inventoryContainer, SqlDbType.TinyInt);

        if (inventoryItems.Count > 0)
            builder.AddTvpParameter("InventoryItems", inventoryItems);

        builder.AddParameter("PetBagSlot", petBagSlot, SqlDbType.TinyInt)
            .AddParameter("PetItemId", petItemId, SqlDbType.Int);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask WithdrawAsync(int characterId, byte petBagSlot, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterPetBag_Withdraw", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PetBagSlot", petBagSlot, SqlDbType.TinyInt)
            .AddParameter("InventoryContainer", inventoryContainer, SqlDbType.TinyInt);

        if (inventoryItems.Count > 0)
            builder.AddTvpParameter("InventoryItems", inventoryItems);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask RearrangeAsync(int characterId, byte sourceSlot, byte destinationSlot,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterPetBag_Rearrange", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("SourceSlot", sourceSlot, SqlDbType.TinyInt)
            .AddParameter("DestinationSlot", destinationSlot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
