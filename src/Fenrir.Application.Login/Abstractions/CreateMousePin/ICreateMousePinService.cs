namespace Fenrir.Application.Login.Abstractions.CreateMousePin;

public enum CreateMousePinOutcome
{
    InvalidFormat,
    AlreadyExists,
    StorageFailure,
    Success
}

public readonly record struct CreateMousePinResult(CreateMousePinOutcome Outcome);

public interface ICreateMousePinService
{
    public ValueTask<CreateMousePinResult> CreateMousePinAsync(int accountId, string mousePassword,
        CancellationToken cancellationToken);
}
