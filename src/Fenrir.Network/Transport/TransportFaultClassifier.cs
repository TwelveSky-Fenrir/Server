using System.Net.Sockets;

namespace Fenrir.Network.Transport;

public static class TransportFaultClassifier
{
    public static bool IsExpectedDisconnect(Exception ex)
    {
        return ex switch
        {
            SocketException => true,
            ObjectDisposedException => true,
            IOException { InnerException: SocketException } => true,
            _ => false
        };
    }
}
