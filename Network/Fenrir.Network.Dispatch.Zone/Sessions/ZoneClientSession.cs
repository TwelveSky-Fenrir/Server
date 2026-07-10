using Fenrir.Network.Serialization.Wire;
using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Zone.Sessions;

/// <summary>
///     The three fixed <c>uUserSort</c> thresholds <c>ts25zone</c>'s GM command switch gates against
///     (Server/ts25zone/S04_MyWork04.cpp) — not a graduated scale, exactly these three literals. The large
///     majority of GM commands (BLOCK, KICK, HIDE, SHOW, the two distinct MOVE commands, DIE, TRIBE,
///     EQUIP/UNEQUIP, FIND, CALL, NCHAT/YCHAT, TRIBEBANK, LEVEL, the STR/DEX/VIT/INT-edit command, ...) gate
///     at <see cref="Basic" />; a smaller set (grant-experience-to-self, grant-money, zone-wide-FFA-start,
///     summon-monster/"moncall") gate at <see cref="Elevated" />; the most dangerous handful (spawn-item, the
///     MAX stat-cheat, grant-pet-experience) gate at <see cref="Admin" />. This enum only names the three
///     threshold values — which command uses which tier is a per-command decision made where that command is
///     implemented, not here.
/// </summary>
public enum GmCommandTier : short
{
    Basic = 1,
    Elevated = 10,
    Admin = 100
}

/// <summary>
///     Zone-flow session: <c>Connected → TicketConsumed → Registering → InWorld</c>.
/// </summary>
public sealed class ZoneClientSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Zone, remoteEndPoint, logger)
{
    public ZoneSessionState State { get; private set; } = ZoneSessionState.Connected;

    /// <summary>Set by <see cref="MarkTicketConsumed" /> — the account the single-use session ticket resolved to.</summary>
    public int? AccountId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the character the ticket committed to at login.
    /// </summary>
    public int? CharacterId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the token carried in the consumed
    ///     <c>runtime.SessionTickets</c> row, threaded from the Login-side claim through
    ///     <c>usp_AccountSession_TransitionToGame</c>. Null until the ticket is consumed.
    /// </summary>
    public Guid? AccountSessionToken { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the account-grade fact carried in the consumed ticket,
    ///     originally <see cref="LoginClientSession.AccountGrade" /> at Login-side authentication (legacy
    ///     <c>uUserSort</c>). Zero (the default) means not elevated; never re-queried per action.
    /// </summary>
    public short AccountGrade { get; private set; }

    /// <summary>
    ///     Legacy's <c>uUserSort &lt; 1</c> elevation gate (Server/ts25zone/S04_MyWork04.cpp:1489 and identical
    ///     siblings at case 518/520/521) -- a strict binary gate, not a graduated permission: any positive grade
    ///     is treated as fully elevated. Equivalent to <c>MeetsGmTier(GmCommandTier.Basic)</c> -- the lowest of
    ///     the three thresholds ts25zone's GM command switch actually uses (see <see cref="GmCommandTier" />).
    ///     Correct for every Basic-tier GM concern implemented in Fenrir today (GM-BLOCK case 519, the Whisper
    ///     elevated-flag stamp, and LocalChat's outer any-GM-tier guard plus its own "where"/"kill200"/"?clear"
    ///     sub-commands -- see <c>Fenrir.Application.Game.Services.Chat.LocalChatService</c>), but it must NOT
    ///     be reused as-is for a future command whose legacy case gates at Elevated (&lt; 10) or Admin
    ///     (&lt; 100) -- LocalChat's own "ygdrop"/"lab"/"boss" sub-commands are exactly such a case and call
    ///     <see cref="MeetsGmTier" /> with that command's own tier instead.
    /// </summary>
    public bool IsGm => MeetsGmTier(GmCommandTier.Basic);

    // Re-pointed by the source zone's tick on each in-process map transfer. Unsynchronized: a reference
    // write is atomic and a stale read is benign — a command posted to the old zone just finds nothing there and is dropped.
    public IZoneActor? CurrentZone { get; set; }

    /// <summary>
    ///     Set by <see cref="MarkCrossShardTransferPending" /> — this connection is closing on purpose because a
    ///     Game(sourceShard)->Game(destinationShard) cross-shard zone transfer already minted a fresh
    ///     <c>runtime.SessionTickets</c> row reusing this connection's own <see cref="AccountSessionToken" />
    ///     (<c>ZoneMoveService.HandleCrossShardAsync</c>). Mirrors <see cref="LoginClientSession.MarkHandoverIssued" />'s
    ///     role for the Login-&gt;Game handoff, but deliberately implemented as an ancillary flag rather than a
    ///     fifth <see cref="ZoneSessionState" /> value: <see cref="IsOpcodeAllowed" /> (via <c>SessionStateGate</c>,
    ///     generated from the wire-protocol model) only ever reads <see cref="State" />, never this flag, and
    ///     nothing about opcode gating needs to change for this connection to close cleanly. Never changes
    ///     <see cref="State" /> — an ancillary fact, not a state transition, so no log here (same posture as
    ///     <see cref="LoginClientSession.MarkAccountSessionToken" />). Must only ever be set on the one code path
    ///     that actually mints a cross-shard ticket — never speculatively, since the connection host uses this
    ///     flag to skip its own <c>runtime.AccountSessions</c> teardown on disconnect, and a session that is
    ///     genuinely abandoned (not mid-handoff) must still have that row cleared.
    /// </summary>
    public bool IsCrossShardTransferPending { get; private set; }

    /// <summary>
    ///     Legacy's per-command <c>uUserSort &gt;= tier</c> gate (the same scalar backs all three thresholds
    ///     in <see cref="GmCommandTier" />). Every future GM-command handler must call this with the exact
    ///     tier its own legacy case uses — <see cref="GmCommandTier.Basic" />, <see cref="GmCommandTier.Elevated" />,
    ///     or <see cref="GmCommandTier.Admin" /> — rather than assume a single elevated-or-not flag suffices
    ///     for every command; see <see cref="IsGm" />'s own remarks for why that shortcut is only valid for
    ///     the Basic tier.
    /// </summary>
    public bool MeetsGmTier(GmCommandTier tier)
    {
        return AccountGrade >= (short)tier;
    }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return ZoneSessionStateGate.Allows(State, opcode);
    }

    // sessionToken/accountGrade are optional so every existing call site that never dealt with cross-process
    // duplicate-login tracking or GM elevation keeps compiling unchanged; ZoneHandshakeHandler (the one
    // production caller that consumes a real runtime.SessionTickets row) always supplies both.
    public void MarkTicketConsumed(int accountId, int characterId, Guid? sessionToken = null, short accountGrade = 0)
    {
        var previous = State;
        AccountId = accountId;
        CharacterId = characterId;
        AccountSessionToken = sessionToken;
        AccountGrade = accountGrade;
        State = ZoneSessionState.TicketConsumed;
        LogSessionStateChanged(previous, State);
    }

    public void MarkRegistering()
    {
        var previous = State;
        State = ZoneSessionState.Registering;
        LogSessionStateChanged(previous, State);
    }

    public void MarkInWorld()
    {
        var previous = State;
        State = ZoneSessionState.InWorld;
        LogSessionStateChanged(previous, State);
    }

    /// <summary>
    ///     Records that this connection is closing on purpose because a cross-shard zone-transfer ticket was
    ///     just minted for it — see <see cref="IsCrossShardTransferPending" />'s own remarks for the full
    ///     rationale and the exact single caller this must stay scoped to.
    /// </summary>
    public void MarkCrossShardTransferPending()
    {
        IsCrossShardTransferPending = true;
    }
}
