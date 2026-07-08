using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Application.Login.Domain.Pins;
using Fenrir.Data.Security;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.ChangeMousePin;

/// <summary>
///     op14 CL_CHANGE_MOUSE_PASSWORD_SEND business logic: verifies the current PIN then stores the new one.
/// </summary>
public sealed class ChangeMousePinService(IAccountPinRepository pins, ILogger<ChangeMousePinService> logger)
    : IChangeMousePinService
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
        catch (Exception ex)
        {
            // Legacy: storage failure replies 2 without disconnecting (S04_MyWork02.cpp l.525-530). Previously
            // swallowed with no trace at all -- logged here (the only place the exception itself is still in
            // scope) so a real PIN-storage failure is diagnosable instead of vanishing silently.
            logger.LogWarning(ex, "PIN change storage failed for account {AccountId}", accountId);
            return new ChangeMousePinResult(ChangeMousePinOutcome.StorageFailure);
        }

        // Success is announced by ChangeMousePinHandler (matching DeleteAvatarService/CreateAvatarService's
        // own posture: this service layer only logs the failure/exception path it alone can see).
        return new ChangeMousePinResult(ChangeMousePinOutcome.Success);
    }
}
