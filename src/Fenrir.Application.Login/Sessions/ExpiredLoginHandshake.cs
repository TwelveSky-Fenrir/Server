namespace Fenrir.Application.Login.Sessions;

public readonly record struct ExpiredLoginHandshake(LoginClientSession Session, DateTimeOffset AcceptedUtc);
