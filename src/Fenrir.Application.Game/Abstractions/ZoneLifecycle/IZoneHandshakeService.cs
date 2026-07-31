using Fenrir.Application.Game.Sessions;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum ZoneHandshakeOutcome
{
    Rejected,

    Accepted,

    SessionSuperseded,

    ProtocolViolation,

    QuotaFull
}

public readonly record struct ZoneHandshakeResult(
    ZoneHandshakeOutcome Outcome,
    int AccountId = 0,
    int CharacterId = 0,
    Guid SessionToken = default,
    short AccountGrade = 0,
    short TargetMapId = 0);

public interface IZoneHandshakeService
{
    public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
        ZoneClientSession session, CancellationToken cancellationToken);
}
