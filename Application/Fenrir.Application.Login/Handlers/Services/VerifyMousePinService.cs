using Fenrir.Application.Login.Pins;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Handlers.Services;

public enum VerifyMousePinOutcome
{
    NoPinConfigured,
    InvalidFormat,
    WrongPassword,
    Success
}

public readonly record struct VerifyMousePinResult(VerifyMousePinOutcome Outcome);

public interface IVerifyMousePinService
{
    ValueTask<VerifyMousePinResult> VerifyMousePinAsync(int accountId, string mousePasswordInput,
        CancellationToken cancellationToken);
}

/// <summary>op15 CL_LOGIN_MOUSE_PASSWORD_SEND business logic: verifies the stored PIN against the client's input.</summary>
public sealed class VerifyMousePinService(IAccountPinRepository pins) : IVerifyMousePinService
{
    public async ValueTask<VerifyMousePinResult> VerifyMousePinAsync(int accountId, string mousePasswordInput,
        CancellationToken cancellationToken)
    {
        // No stored PIN => caller must create one first (op13).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
            return new VerifyMousePinResult(VerifyMousePinOutcome.NoPinConfigured);

        if (!MousePinFormat.IsValid(mousePasswordInput))
            return new VerifyMousePinResult(VerifyMousePinOutcome.InvalidFormat);

        if (!PasswordHasher.Verify(mousePasswordInput, storedPin.PinHash, storedPin.PinSalt))
            return new VerifyMousePinResult(VerifyMousePinOutcome.WrongPassword);

        return new VerifyMousePinResult(VerifyMousePinOutcome.Success);
    }
}
