namespace Fenrir.Data.Abstractions.Characters;

// Split from CharacterRepository (parallel workstream); interface so RenameAvatarHandler is unit-testable without SQL.
public interface ICharacterRenameRepository
{
    /// <summary>
    ///     Legacy mDB.ChangeCharacterName codes, forwarded verbatim: 0 renamed, 2 name taken, 102 no character at that
    ///     slot. Exceptions map to legacy 101 by the caller.
    /// </summary>
    public ValueTask<int> RenameAsync(int accountId, byte slot, string newName, CancellationToken ct);
}
