namespace Fenrir.Security.Abstractions;

public interface IFloodKickSink
{
    public int KickByRemoteAddress(string ipAddress);
}
