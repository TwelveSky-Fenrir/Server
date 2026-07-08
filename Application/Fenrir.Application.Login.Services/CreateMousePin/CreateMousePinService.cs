using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Application.Login.Domain.Pins;
using Fenrir.Data.Security;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.CreateMousePin;

/// <summary>op13 CL_CREATE_MOUSE_PASSWORD_SEND business logic: first-time PIN creation, stored hashed.</summary>
public sealed class CreateMousePinService(IAccountPinRepository pins, ILogger<CreateMousePinService> logger)
    : ICreateMousePinService
{
    public async ValueTask<CreateMousePinResult> CreateMousePinAsync(int accountId, string mousePassword,
        CancellationToken cancellationToken)
    {
        if (!MousePinFormat.IsValid(mousePassword))
            return new CreateMousePinResult(CreateMousePinOutcome.InvalidFormat);

        // Legacy: creating over an existing PIN is a protocol violation (client should send op15/op14 instead).
        if (await pins.GetAsync(accountId, cancellationToken) is not null)
            return new CreateMousePinResult(CreateMousePinOutcome.AlreadyExists);

        try
        {
            var (hash, salt) = PasswordHasher.Hash(mousePassword);
            await pins.SetAsync(accountId, hash, salt, cancellationToken);
        }
        catch (Exception ex)
        {
            // Legacy: storage failure is a silent Quit(), no reply (S04_MyWork02.cpp l.476-479). Previously
            // swallowed with no trace at all -- logged here (the only place the exception itself is still in
            // scope) so a real PIN-storage failure is diagnosable instead of vanishing silently.
            logger.LogWarning(ex, "PIN creation storage failed for account {AccountId}", accountId);
            return new CreateMousePinResult(CreateMousePinOutcome.StorageFailure);
        }

        // Success is announced by CreateMousePinHandler (matching DeleteAvatarService/CreateAvatarService's
        // own posture: this service layer only logs the failure/exception path it alone can see).
        return new CreateMousePinResult(CreateMousePinOutcome.Success);
    }
}
