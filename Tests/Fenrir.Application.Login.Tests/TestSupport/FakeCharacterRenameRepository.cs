using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for ICharacterRenameRepository: drives every game.usp_Character_Rename outcome
// (0/2/102/-1, plus a simulated engine error) without a SQL container.
internal sealed class FakeCharacterRenameRepository : ICharacterRenameRepository
{
    private readonly Exception? _fault;
    private readonly int _result;

    private FakeCharacterRenameRepository(int result, Exception? fault)
    {
        _result = result;
        _fault = fault;
    }

    public (int AccountId, byte Slot, string NewName, byte ItemContainer, byte ItemSlot)? LastCall
    {
        get;
        private set;
    }

    public ValueTask<int> RenameAndConsumeItemAsync(int accountId, byte slot, string newName, byte itemContainer,
        byte itemSlot, CancellationToken ct)
    {
        LastCall = (accountId, slot, newName, itemContainer, itemSlot);
        return _fault is not null
            ? throw _fault
            : ValueTask.FromResult(_result);
    }

    public static FakeCharacterRenameRepository ReturningResult(int result)
    {
        return new FakeCharacterRenameRepository(result, null);
    }

    public static FakeCharacterRenameRepository Throwing(Exception fault)
    {
        return new FakeCharacterRenameRepository(0, fault);
    }
}
