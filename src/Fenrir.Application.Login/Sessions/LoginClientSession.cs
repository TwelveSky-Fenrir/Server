using System.IO.Pipelines;
using System.Net;
using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Sessions;

public sealed class LoginClientSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Login, remoteEndPoint, logger)
{
    private const int PreAuthenticationPending = 0;
    private const int PreAuthenticationComplete = 1;
    private const int PreAuthenticationExpired = 2;

    private int _preAuthenticationStatus;

    public LoginSessionState State { get; private set; } = LoginSessionState.Connected;

    public bool IsPreAuthentication => Volatile.Read(ref _preAuthenticationStatus) == PreAuthenticationPending;

    public int? AccountId { get; private set; }

    public short AccountGrade { get; private set; }

    public int PinFailureCount { get; private set; }

    public Guid? AccountSessionToken { get; private set; }

    public GiftSlotBoard GiftSlots { get; } = new();

    public override bool IsOpcodeAllowed(byte opcode)
    {
        if (!LoginSessionStateGate.Allows(State, opcode))
            return false;

        return !ChangeMasterStateGate.AppliesTo(opcode) || ChangeMasterStateGate.Allows(State);
    }

    public void MarkVersionOk()
    {
        var previous = State;
        State = LoginSessionState.VersionOk;
        LogSessionStateChanged(previous, State);
    }

    public void MarkAuthenticated(int accountId, short accountGrade = 0)
    {
        if (Interlocked.CompareExchange(ref _preAuthenticationStatus, PreAuthenticationComplete,
                PreAuthenticationPending) != PreAuthenticationPending)
            return;

        var previous = State;
        AccountId = accountId;
        AccountGrade = accountGrade;
        State = LoginSessionState.Authenticated;
        LogSessionStateChanged(previous, State);
    }

    public bool TryExpirePreAuthentication()
    {
        if (Interlocked.CompareExchange(ref _preAuthenticationStatus, PreAuthenticationExpired,
                PreAuthenticationPending) != PreAuthenticationPending)
            return false;

        Abort(Core.Abstractions.DisconnectReason.IdleTimeout);
        return true;
    }

    public void MarkAccountSessionToken(Guid token)
    {
        AccountSessionToken = token;
    }

    public void MarkPinRequired()
    {
        var previous = State;
        PinFailureCount = 0;
        State = LoginSessionState.PinRequired;
        LogSessionStateChanged(previous, State);
    }

    public int RegisterPinFailure()
    {
        return ++PinFailureCount;
    }

    public void MarkCharSelect()
    {
        var previous = State;
        State = LoginSessionState.CharSelect;
        LogSessionStateChanged(previous, State);
    }

    public void MarkHandoverIssued()
    {
        var previous = State;
        State = LoginSessionState.HandoverIssued;
        LogSessionStateChanged(previous, State);
    }
}
