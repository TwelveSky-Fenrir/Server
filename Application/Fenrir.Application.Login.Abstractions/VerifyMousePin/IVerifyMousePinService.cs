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

    /// <summary>
    ///     Records a game.EventLog AccountSecurity row for one rejected mouse-PIN attempt. The session-scoped
    ///     consecutive-failure counter (legacy <c>mSecondLoginTryNum</c>) lives on the caller's
    ///     LoginClientSession, not in this service, so the caller supplies the count it just observed;
    ///     <paramref name="lockedOut" /> is true only for the specific attempt that crossed
    ///     VerifyMousePinHandler.MaxPinFailures and is about to disconnect the session. This is a new Fenrir
    ///     observability addition, not a reproduction of any legacy audit trail -- see
    ///     Fenrir.Data.Abstractions.Game.IEventLogRepository's own remarks ("no durable audit-log subsystem
    ///     exists" in the legacy client-observable behavior).
    /// </summary>
    public ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken);
}
