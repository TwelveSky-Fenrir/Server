using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Link;

internal sealed class CenterUplinkSession(
    long sessionId,
    SocketConnection connection,
    ILogger? logger = null)
    : ClientSession(sessionId, connection, FenrirServer.Center, connection.RemoteEndPoint, logger)
{
    public override bool IsOpcodeAllowed(byte opcode)
    {
        return true;
    }
}
