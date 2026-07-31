using Fenrir.Application.Game.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmGrantMoneyService
{
    public ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, CancellationToken cancellationToken);
}
