using System.IO;
using System.Net.Sockets;

namespace Fenrir.Network.Transport;

// SocketConnection.ReceiveLoopAsync/SendLoopAsync (this project) catch every exception from their own
// Socket.*Async calls and complete their pipe with it as the failure -- the next PipeReader.ReadAsync on
// that pipe then rethrows the exact same exception (System.IO.Pipelines' own contract for a faulted
// writer). A peer that closes abruptly (app closed, crash, network drop, NAT/firewall reset) surfaces here
// as an ordinary SocketException/IOException, not a server-side bug -- see this type's own call sites
// (GameConnectionHost/LoginConnectionHost's OnAcceptedAsync) for why that distinction matters for logging.
public static class TransportFaultClassifier
{
    public static bool IsExpectedDisconnect(Exception ex) => ex switch
    {
        SocketException => true,
        ObjectDisposedException => true,
        IOException { InnerException: SocketException } => true,
        _ => false
    };
}
