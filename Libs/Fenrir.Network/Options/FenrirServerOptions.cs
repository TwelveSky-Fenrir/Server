using System.Net;

namespace Fenrir.Network.Options;

/// <summary>Represents a configuration for the transport layer.</summary>
public class FenrirServerOptions
{
    // TODO: Rename this to bind address?
    // TODO: What about Nat Translation?
    
    /// <summary>Gets the ip address.</summary>
    public required string IpAddress { get; set; }

    /// <summary>Gets the port.</summary>
    public ushort Port { get; set; }

    /// <summary>Gets the number of max connections accepted by the server.</summary>
    public ushort MaxConnections { get; set; }

    /// <summary>Gets the number of max connections by ip address accepted by the server.</summary>
    public ushort MaxConnectionsByIpAddress { get; set; }

    /// <summary>Determines whether the server log messages.</summary>
    public bool EnableLogging { get; set; }

    /// <summary>Determines whether the server use the keep alive feature.</summary>
    public bool EnableKeepAlive { get; set; }

    /// <summary>Gets the keep alive interval.</summary>
    public int KeepAliveInterval { get; set; }
    
    public int RecvBuffersize { get; set; }

    internal IPEndPoint GetRemoteEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(IpAddress), Port);
    }
}