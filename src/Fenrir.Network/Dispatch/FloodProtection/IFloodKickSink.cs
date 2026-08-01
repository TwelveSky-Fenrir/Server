namespace Fenrir.Network.Dispatch.FloodProtection;

public interface IFloodKickSink
{
    public int KickByRemoteAddress(string ipAddress);
}
