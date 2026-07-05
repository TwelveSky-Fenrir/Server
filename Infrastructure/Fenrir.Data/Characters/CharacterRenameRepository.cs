using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

/// <summary>Singleton facade over game.usp_Character_Rename -- procs only, no SqlDbType leaks past this type.</summary>
public sealed record CharacterRenameRepository(ICaeriusNetDbContext Db) : ICharacterRenameRepository
{
    public async ValueTask<int> RenameAsync(int accountId, byte slot, string newName, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Rename", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("NewName", newName, SqlDbType.NVarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
