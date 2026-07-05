using Fenrir.Application.Login.Pins;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Handlers.Services;

public enum ChangeMousePinOutcome
{
    NoPinConfigured,
    InvalidFormat,
    WrongPassword,
    StorageFailure,
    Success
}

public readonly record struct ChangeMousePinResult(ChangeMousePinOutcome Outcome);

public interface IChangeMousePinService
{
    ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken);
}

/// <summary>
///     op14 CL_CHANGE_MOUSE_PASSWORD_SEND business logic: verifies the current PIN then stores the new one.
/// </summary>
public sealed class ChangeMousePinService(IAccountPinRepository pins) : IChangeMousePinService
{
    public async ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken)
    {
        // No stored PIN => caller must create one first (op13).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
            return new ChangeMousePinResult(ChangeMousePinOutcome.NoPinConfigured);

        if (!MousePinFormat.IsValid(currentPin) || !MousePinFormat.IsValid(newPin))
            return new ChangeMousePinResult(ChangeMousePinOutcome.InvalidFormat);

        if (!PasswordHasher.Verify(currentPin, storedPin.PinHash, storedPin.PinSalt))
            return new ChangeMousePinResult(ChangeMousePinOutcome.WrongPassword);

        try
        {
            var (hash, salt) = PasswordHasher.Hash(newPin);
            await pins.SetAsync(accountId, hash, salt, cancellationToken);
        }
        catch (Exception)
        {
            // Legacy: storage failure replies 2 without disconnecting (S04_MyWork02.cpp l.525-530).
            return new ChangeMousePinResult(ChangeMousePinOutcome.StorageFailure);
        }

        return new ChangeMousePinResult(ChangeMousePinOutcome.Success);
    }
}
