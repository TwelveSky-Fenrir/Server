namespace Fenrir.Application.Login.Abstractions.VerifyMousePin;

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
    public ValueTask<VerifyMousePinResult> VerifyMousePinAsync(int accountId, string mousePasswordInput,
        CancellationToken cancellationToken);
}
