namespace Fenrir.Application.Login.Abstractions.VerifyMousePin;

public enum VerifyMousePinOutcome
{
    NoPinConfigured,
    InvalidFormat,
    WrongPassword,
    Success,

        Locked
}

public readonly record struct VerifyMousePinResult(VerifyMousePinOutcome Outcome);

public interface IVerifyMousePinService
{
    public ValueTask<VerifyMousePinResult> VerifyMousePinAsync(int accountId, string mousePasswordInput,
        CancellationToken cancellationToken);

        public ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken);
}
