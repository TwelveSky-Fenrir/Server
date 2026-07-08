namespace Fenrir.Network.Serialization.Wire;

public enum FenrirDirection : byte
{
    /// <summary>Client → server. <c>CLIENT_PACKET</c> header, 9 bytes.</summary>
    Incoming,

    /// <summary>Server → client. <c>DEFALUT_PACKET</c> header, 1 byte.</summary>
    Outgoing
}
