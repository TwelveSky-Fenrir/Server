namespace Fenrir.Security.Abstractions;

public interface IFloodKickSink
{

        int KickByRemoteAddress(string ipAddress);
}
