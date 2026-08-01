using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class MuteRefreshPollHost(
    ZoneRegistry zones,
    IMuteRepository mutes,
    IOptions<GameServerOptions> options,
    ILogger<MuteRefreshPollHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.MutePollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Mute refresh poll failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        var tracked = new Dictionary<int, (Zone Zone, bool WasMuted)>();
        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            tracked[player.CharacterId] = (zone, player.IsMuted);

        if (tracked.Count == 0)
            return;

        var mutedIds = await mutes.GetActiveCharacterIdsAsync(tracked.Keys, ct).ConfigureAwait(false);
        var mutedSet = mutedIds.IsEmpty ? [] : mutedIds.ToHashSet();

        foreach (var (characterId, (zone, wasMuted)) in tracked)
        {
            var isMuted = mutedSet.Contains(characterId);
            if (isMuted == wasMuted)
                continue;

            if (!zone.Post(ZoneCommand.SetMuted(characterId, isMuted)))
                logger.LogWarning(
                    "Zone {MapId} inbox full: dropped mute-state change for character {CharacterId}",
                    zone.MapId, characterId);
        }
    }
}
