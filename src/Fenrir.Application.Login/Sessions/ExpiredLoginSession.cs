namespace Fenrir.Application.Login.Sessions;

public readonly record struct ExpiredLoginSession(LoginClientSession Session, DateTimeOffset IdleSinceUtc);
