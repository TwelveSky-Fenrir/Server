using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum ZoneHandshakeOutcome
{
    /// <summary>
    ///     The session ticket the LoginServer minted for this AccountId (ADR-0005) is absent, expired, or
    ///     scoped to a different shard than the one it is being consumed against -- the closest structural
    ///     analog to legacy's <c>RegisterUserForZone_00</c> failure (account not currently tracked as logged
    ///     in at all, or tracked but not in the "told to enter a zone" state;
    ///     Server/ts25playuser/S07_MyGame01.cpp:1049-1060). Legacy answers that failure with a silent
    ///     <c>Quit()</c> and zero response bytes (Server/ts25zone/S04_MyWork02.cpp:728-733) -- never the
    ///     explicit response it reserves for a quota-full/server-state-gate rejection (see
    ///     <see cref="QuotaFull" />). Resolved product-decision boundary: this outcome now carries the same
    ///     silent-disconnect posture forward, rather than being grouped with <see cref="QuotaFull" />'s
    ///     explicit, open-connection response -- no response packet, the connection is simply dropped.
    /// </summary>
    Rejected,

    Accepted,

    /// <summary>
    ///     The ticket itself was valid (right account, right shard) but <c>usp_AccountSession_TransitionToGame</c>
    ///     refused it -- the Login-side <c>runtime.AccountSessions</c> row no longer matches the session token this
    ///     ticket carried (a newer login already claimed the account). Handled identically to a hijack attempt: no
    ///     response packet, the connection is simply dropped.
    /// </summary>
    SessionSuperseded,

    /// <summary>
    ///     The account/session index embedded in the request's identifier text falls outside the server's valid
    ///     index range, or the declared tribe falls outside the valid range for whichever tribe-quota group this
    ///     shard enforces (<see cref="TribeQuotaGroup" />). Treated as a protocol violation,
    ///     same posture as an unreachable repeat of this request on an already-registered connection (that case
    ///     never reaches the service at all -- <c>SessionStateGate</c> already drops it): no response packet, the
    ///     connection is simply dropped.
    /// </summary>
    ProtocolViolation,

    /// <summary>
    ///     The declared tribe's currently-temp-registered population already meets this shard's configured
    ///     tribe-quota threshold. Mirrors legacy's own quota-full/server-state-gate branches
    ///     (Server/ts25zone/S04_MyWork02.cpp:630-635, :678-683) -- the one failure class on this packet legacy
    ///     answers with an explicit response instead of a silent disconnect: a generic Result=1 rejection
    ///     response is sent and the connection stays open, retry-able.
    /// </summary>
    QuotaFull
}

public readonly record struct ZoneHandshakeResult(
    ZoneHandshakeOutcome Outcome,
    int AccountId = 0,
    int CharacterId = 0,
    Guid SessionToken = default,
    short AccountGrade = 0);

/// <summary>
///     Business logic for op11, the first packet after ZC_CONNECT_OK_RECV: gates entry on this shard's
///     tribe-population quota (if any), then consumes the single-use session ticket the LoginServer minted for
///     this AccountId (ADR-0005) -- see <c>ZoneHandshakeHandler</c>'s remarks.
/// </summary>
public interface IZoneHandshakeService
{
    /// <param name="obfuscatedId">The wire-obfuscated "MG"+accountId identifier text.</param>
    /// <param name="declaredTribe">
    ///     The tribe the connection declares it wants to enter as -- gated against the current
    ///     population, but not necessarily the tribe later recorded (see <see cref="TribeQuotaRegistry" />'s own remarks).
    /// </param>
    /// <param name="session">
    ///     The connection attempting to register -- recorded on success so its slot can be counted and later
    ///     released.
    /// </param>
    public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
        ZoneClientSession session, CancellationToken cancellationToken);
}
