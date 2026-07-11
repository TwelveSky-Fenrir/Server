using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmGrantMoneyService
{
    public ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, CancellationToken cancellationToken);
}
