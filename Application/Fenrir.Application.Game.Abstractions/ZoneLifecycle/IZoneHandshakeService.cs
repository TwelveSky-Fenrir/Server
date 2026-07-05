namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum ZoneHandshakeOutcome
{
    Rejected,
    Accepted
}

public readonly record struct ZoneHandshakeResult(ZoneHandshakeOutcome Outcome, int AccountId = 0, int CharacterId = 0);

/// <summary>
///     Business logic for op11, the first packet after ZC_CONNECT_OK_RECV: consumes the single-use session
///     ticket the LoginServer minted for this AccountId (ADR-0005) -- see <c>ZoneHandshakeHandler</c>'s remarks.
/// </summary>
public interface IZoneHandshakeService
{
    public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, CancellationToken cancellationToken);
}
