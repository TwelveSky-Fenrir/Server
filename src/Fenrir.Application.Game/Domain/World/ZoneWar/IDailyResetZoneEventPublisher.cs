namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IDailyResetZoneEventPublisher
{
    public ValueTask PublishAsync(CancellationToken ct);
}
