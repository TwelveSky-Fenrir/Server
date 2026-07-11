using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeAccountPinRepository : IAccountPinRepository
{
    public bool ThrowOnSet { get; set; }

    public int SetCallCount { get; private set; }

    public int RecordAttemptCallCount { get; private set; }

    public AccountPinDto? Stored { get; private set; }

    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(Stored);
    }

    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct)
    {
        if (ThrowOnSet)
            throw new InvalidOperationException("Simulated auth.usp_AccountPin_Set failure.");

        SetCallCount++;
        Stored = new AccountPinDto(pinHash, pinSalt);
        return ValueTask.CompletedTask;
    }

    public ValueTask RecordAttemptAsync(int accountId, bool success, CancellationToken ct)
    {
        RecordAttemptCallCount++;

        if (Stored is null)
            return ValueTask.CompletedTask;

        if (success)
        {
            Stored = Stored with { FailedAttempts = 0, LockedUntilUtc = null };
            return ValueTask.CompletedTask;
        }

        var failedAttempts = Stored.FailedAttempts + 1;
        var lockedUntil = failedAttempts switch
        {
            >= 10 => DateTime.UtcNow.AddMinutes(15),
            >= 5 => DateTime.UtcNow.AddMinutes(1),
            _ => Stored.LockedUntilUtc
        };
        Stored = Stored with { FailedAttempts = failedAttempts, LockedUntilUtc = lockedUntil };
        return ValueTask.CompletedTask;
    }

    public static FakeAccountPinRepository WithNoPin()
    {
        return new FakeAccountPinRepository();
    }

    public static FakeAccountPinRepository WithPin(string pin)
    {
        var (hash, salt) = PasswordHasher.Hash(pin);
        return new FakeAccountPinRepository { Stored = new AccountPinDto(hash, salt) };
    }

    public static FakeAccountPinRepository WithLockedPin(string pin, DateTime lockedUntilUtc)
    {
        var (hash, salt) = PasswordHasher.Hash(pin);
        return new FakeAccountPinRepository
        {
            Stored = new AccountPinDto(hash, salt, 5, lockedUntilUtc)
        };
    }
}
