using Fenrir.Security.Credentials;
using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Domain.Login.Pins;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.CreateMousePin;

public sealed class CreateMousePinService(IAccountPinRepository pins, ILogger<CreateMousePinService> logger)
    : ICreateMousePinService
{
    public async ValueTask<CreateMousePinResult> CreateMousePinAsync(int accountId, string mousePassword,
        CancellationToken cancellationToken)
    {
        if (await pins.GetAsync(accountId, cancellationToken) is not null)
            return new CreateMousePinResult(CreateMousePinOutcome.AlreadyExists);

        if (!MousePinFormat.IsValid(mousePassword))
            return new CreateMousePinResult(CreateMousePinOutcome.InvalidFormat);

        try
        {
            var (hash, salt) = PasswordHasher.Hash(mousePassword);
            await pins.SetAsync(accountId, hash, salt, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PIN creation storage failed for account {AccountId}", accountId);
            return new CreateMousePinResult(CreateMousePinOutcome.StorageFailure);
        }

        return new CreateMousePinResult(CreateMousePinOutcome.Success);
    }
}
