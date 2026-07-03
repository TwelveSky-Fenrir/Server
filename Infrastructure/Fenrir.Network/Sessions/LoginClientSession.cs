using System.IO.Pipelines;
using System.Net;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Sessions;

/// <summary>
///     Login-flow session: <c>Connected → VersionOk → Authenticated → CharSelect → HandoverIssued</c>
///     (Docs/protocol/M1_Legacy_Wire_Contract.md §1, §8.1 of the architecture reference).
/// </summary>
public sealed class LoginClientSession(long sessionId, IDuplexPipe transport, IPEndPoint? remoteEndPoint = null)
    : ClientSession(sessionId, transport, FenrirServer.Login, remoteEndPoint)
{
    public LoginSessionState State { get; private set; } = LoginSessionState.Connected;

    /// <summary>
    ///     Set by <see cref="MarkAuthenticated" /> — the DB identity (= legacy <c>uUserIdx</c>, ADR-0005) this connection
    ///     now speaks for. Null until authentication succeeds.
    /// </summary>
    public int? AccountId { get; private set; }

    /// <summary>
    ///     Legacy <c>mSecondLoginTryNum</c>: consecutive mouse-PIN mismatches on this connection. The third one
    ///     disconnects (S04_MyWork02.cpp l.517-522/l.567-573); reset when the PIN gate re-arms.
    /// </summary>
    public int PinFailureCount { get; private set; }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return SessionStateGate.Allows(State, opcode);
    }

    public void MarkVersionOk()
    {
        State = LoginSessionState.VersionOk;
    }

    public void MarkAuthenticated(int accountId)
    {
        AccountId = accountId;
        State = LoginSessionState.Authenticated;
    }

    /// <summary>
    ///     Legacy <c>mSecondLoginSort = 1</c> right after a successful LOGIN_SEND when P2ndPassword=1: the mouse-PIN
    ///     gate closes until op 13/14/15 opens it. Also resets the mismatch counter, like the legacy
    ///     <c>mSecondLoginTryNum = 0</c> on login success (S04_MyWork02.cpp l.307).
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
