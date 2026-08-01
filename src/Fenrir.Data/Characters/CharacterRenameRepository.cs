using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record CharacterRenameRepository(ICaeriusNetDbContext Db) : ICharacterRenameRepository
{
    private const int RenameScrollItemId = 1133;

    public async ValueTask<int> RenameAndConsumeItemAsync(int accountId, byte slot, string newName,
        byte itemContainer, byte itemSlot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Rename", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("NewName", newName, SqlDbType.NVarChar)
            .AddParameter("ItemContainer", itemContainer, SqlDbType.TinyInt)
            .AddParameter("ItemSlot", itemSlot, SqlDbType.TinyInt)
            .AddParameter("RequiredItemId", RenameScrollItemId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
