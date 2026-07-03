using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Characters;

/// <summary>
///     Rename surface for CL_CHANGE_AVATAR_NAME_SEND (op 19), deliberately split from
///     <see cref="CharacterRepository" /> (a parallel workstream owns that file) and expressed as an interface so
///     <c>RenameAvatarHandler</c> is unit-testable without a SQL container.
/// </summary>
public interface ICharacterRenameRepository
{
    /// <summary>
    ///     game.usp_Character_Rename's scalar result — the LEGACY mDB.ChangeCharacterName codes, forwarded verbatim
    ///     to the client: 0 = renamed, 2 = name already taken (incl. "new == current"), 102 = no character at that
    ///     (account, slot). Engine errors surface as exceptions (the caller maps them to the legacy 101).
    /// </summary>
    public ValueTask<int> RenameAsync(int accountId, byte slot, string newName, CancellationToken ct);
}

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
