using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Sessions;

/// <summary>
///     Login-flow session: <c>Connected → VersionOk → Authenticated → CharSelect → HandoverIssued</c>.
/// </summary>
public sealed class LoginClientSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Login, remoteEndPoint, logger)
{
    public LoginSessionState State { get; private set; } = LoginSessionState.Connected;

    /// <summary>
    ///     Set by <see cref="MarkAuthenticated" /> — the DB identity (legacy <c>uUserIdx</c>). Null until authenticated.
    /// </summary>
    public int? AccountId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkAuthenticated" /> — the account-grade fact (legacy <c>uUserSort</c>,
    ///     Server/ts25login/S08_MyDB.cpp:244-245), loaded once at authentication and never re-queried per action.
    ///     Zero (the default) means not elevated. Carried into the zone-transfer ticket
    ///     (<c>ZoneTransferHandler</c>) so the Zone session inherits it too.
    /// </summary>
    public short AccountGrade { get; private set; }

    /// <summary>
    ///     Legacy <c>mSecondLoginTryNum</c>: consecutive mouse-PIN mismatches; the third disconnects.
    /// </summary>
    public int PinFailureCount { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkAccountSessionToken" /> — the token <c>runtime.AccountSessions</c> minted
    ///     for this login epoch. Carried into the zone-transfer ticket so the game-side handshake can prove
    ///     it's completing the same login, not a hijack of a newer one. Null until authenticated.
    /// </summary>
    public Guid? AccountSessionToken { get; private set; }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return SessionStateGate.Allows(State, opcode);
    }

    public void MarkVersionOk()
    {
        State = LoginSessionState.VersionOk;
    }

    // accountGrade is optional so every existing call site that never dealt with GM elevation keeps
    // compiling unchanged; LoginService's success branch always supplies the real value.
    public void MarkAuthenticated(int accountId, short accountGrade = 0)
    {
        AccountId = accountId;
        AccountGrade = accountGrade;
        State = LoginSessionState.Authenticated;
    }

    /// <summary>Records the token <c>usp_AccountSession_ClaimOrSignalKick</c> minted for this login epoch.</summary>
    public void MarkAccountSessionToken(Guid token)
    {
        AccountSessionToken = token;
    }

    /// <summary>
    ///     Legacy <c>mSecondLoginSort = 1</c> after LOGIN_SEND with P2ndPassword=1: PIN gate closes until op 13/14/15.
    ///     Also resets the mismatch counter.
    /// </summary>
    public void MarkPinRequired()
    {
        PinFailureCount = 0;
        State = LoginSessionState.PinRequired;
    }

    /// <summary>Returns the new consecutive-mismatch count (legacy <c>++mSecondLoginTryNum</c>).</summary>
    public int RegisterPinFailure()
    {
        return ++PinFailureCount;
    }

    public void MarkCharSelect()
    {
        State = LoginSessionState.CharSelect;
    }

    public void MarkHandoverIssued()
    {
        State = LoginSessionState.HandoverIssued;
    }
}
