using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmGrantMoneyService
{
    public ValueTask HandleAsync(byte[] data, IZoneSession zoneSession, CancellationToken cancellationToken);
}
