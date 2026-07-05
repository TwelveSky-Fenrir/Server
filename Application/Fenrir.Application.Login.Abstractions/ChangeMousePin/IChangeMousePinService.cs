namespace Fenrir.Application.Login.Abstractions.ChangeMousePin;

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
    public ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken);
}
