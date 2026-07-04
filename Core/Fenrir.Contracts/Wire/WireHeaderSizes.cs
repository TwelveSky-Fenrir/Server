namespace Fenrir.Contracts.Wire;

/// <summary>Legacy header sizes. §2.1.</summary>
public static class WireHeaderSizes
{
    /// <summary><c>CLIENT_PACKET</c>: int tPacket1 + int tPacket2 + BYTE tProtocol.</summary>
    public const int ClientPacketSize = 9;

    /// <summary><c>DEFALUT_PACKET</c> (sic): BYTE tProtocol.</summary>
    public const int DefaultPacketSize = 1;

    public static int SizeFor(FenrirDirection direction)
    {
        return direction switch
        {
            FenrirDirection.Incoming => ClientPacketSize,
            FenrirDirection.Outgoing => DefaultPacketSize,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }
}
