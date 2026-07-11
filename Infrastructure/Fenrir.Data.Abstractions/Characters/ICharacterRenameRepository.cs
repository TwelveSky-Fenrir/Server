namespace Fenrir.Data.Abstractions.Characters;

public interface ICharacterRenameRepository
{
    public ValueTask<int> RenameAndConsumeItemAsync(int accountId, byte slot, string newName, byte itemContainer,
        byte itemSlot, CancellationToken ct);
}
