using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IAccountPinRepository" /> — the seam the type doc of
///     <c>AccountPinRepository</c> calls out explicitly so the PIN handlers (ops 13/14/15) are unit-testable
///     without a SQL container. Single-account: every PIN handler only ever touches its own session's account.
/// </summary>
internal sealed class FakeAccountPinRepository : IAccountPinRepository
{
    private AccountPinDto? _stored;

    /// <summary>Throws from <see cref="SetAsync" /> to simulate the legacy UpdateMousePassword storage failure.</summary>
    public bool ThrowOnSet { get; set; }

    public int SetCallCount { get; private set; }

    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(_stored);
    }

    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct)
    {
        if (ThrowOnSet)
            throw new InvalidOperationException("Simulated auth.usp_AccountPin_Set failure.");

        SetCallCount++;
        _stored = new AccountPinDto(pinHash, pinSalt);
        return ValueTask.CompletedTask;
    }

    public static FakeAccountPinRepository WithNoPin()
    {
        return new FakeAccountPinRepository();
    }

    public static FakeAccountPinRepository WithPin(string pin)
    {
        var (hash, salt) = PasswordHasher.Hash(pin);
        return new FakeAccountPinRepository { _stored = new AccountPinDto(hash, salt) };
    }
}
