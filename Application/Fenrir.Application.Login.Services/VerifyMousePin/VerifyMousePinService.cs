using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Application.Login.Domain.Pins;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Services.VerifyMousePin;

/// <summary>op15 CL_LOGIN_MOUSE_PASSWORD_SEND business logic: verifies the stored PIN against the client's input.</summary>
public sealed class VerifyMousePinService(IAccountPinRepository pins, IEventLogRepository eventLog)
    : IVerifyMousePinService
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

    public async ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken)
    {
        var eventCode = lockedOut
            ? AccountSecurityEventCodes.MousePinLockout
            : AccountSecurityEventCodes.MousePinMismatch;

        await eventLog.LogAsync(eventCode, EventLogCategory.AccountSecurity, accountId, null, null, null, null,
            null, null, null, null, (byte)failureCount, null, cancellationToken);
    }
}
