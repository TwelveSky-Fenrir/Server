using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Login.Sessions;

public sealed class LoginClientSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Login, remoteEndPoint, logger)
{
    public LoginSessionState State { get; private set; } = LoginSessionState.Connected;

        public int? AccountId { get; private set; }

        public short AccountGrade { get; private set; }

        public int PinFailureCount { get; private set; }

        public Guid? AccountSessionToken { get; private set; }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return LoginSessionStateGate.Allows(State, opcode);
    }

    public void MarkVersionOk()
    {
        var previous = State;
        State = LoginSessionState.VersionOk;
        LogSessionStateChanged(previous, State);
    }

    public void MarkAuthenticated(int accountId, short accountGrade = 0)
    {
        var previous = State;
        AccountId = accountId;
        AccountGrade = accountGrade;
        State = LoginSessionState.Authenticated;
        LogSessionStateChanged(previous, State);
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
