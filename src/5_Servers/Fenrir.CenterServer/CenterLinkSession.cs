using System.IO.Pipelines;
using System.Net;
using Fenrir.Cluster;
using Fenrir.Cluster.Wire;
using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer;

internal sealed class CenterLinkSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Center, remoteEndPoint, logger)
{

        public CenterSessionState State { get; private set; } = CenterSessionState.Connected;

        public override bool IsOpcodeAllowed(byte opcode)
    {
        return CenterSessionStateGate.Allows(State, opcode);
    }

        public void MarkAuthenticated()
    {
        State = CenterSessionState.Authenticated;
    }
}
