namespace Fenrir.Core.Wire;

public static class WireHeaderSizes
{
    public const int ClientPacketSize = 9;

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
