using Fenrir.Data.Characters;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for ICharacterRenameRepository: drives every game.usp_Character_Rename outcome
// (0/2/102, plus a simulated engine error) without a SQL container.
internal sealed class FakeCharacterRenameRepository : ICharacterRenameRepository
{
    private readonly Exception? _fault;
    private readonly int _result;

    private FakeCharacterRenameRepository(int result, Exception? fault)
    {
        _result = result;
        _fault = fault;
    }

    public (int AccountId, byte Slot, string NewName)? LastCall { get; private set; }

    public ValueTask<int> RenameAsync(int accountId, byte slot, string newName, CancellationToken ct)
    {
        LastCall = (accountId, slot, newName);
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
