namespace Fenrir.Application.Login.Abstractions.ChangeMousePin;

public enum ChangeMousePinOutcome
{
    NoPinConfigured,
    InvalidFormat,
    WrongPassword,
    StorageFailure,
    Success,

        Locked
}

public readonly record struct ChangeMousePinResult(ChangeMousePinOutcome Outcome);

public interface IChangeMousePinService
{
    public ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken);

        public ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken);
}
