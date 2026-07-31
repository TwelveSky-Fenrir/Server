using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class WorldEventUplink(
    ILogger<WorldEventUplink> logger,
    IRvrSiegeEventRelayQueue? relayQueue = null,
    IOptions<GameServerOptions>? gameOptions = null) : IWorldEventUplink
{
    public void Publish(int sort, ReadOnlySpan<byte> data)
    {
        if (relayQueue is null)
        {
            // La dependance est optionnelle : sans ce log, un evenement RvR cross-shard perdu ne leve pas
            // et ne se voit qu'au comportement du jeu.
            logger.LogError("World event sort {Sort} dropped: no IRvrSiegeEventRelayQueue is registered.", sort);
            return;
        }

        relayQueue.Enqueue(new RvrSiegeEventRelayEntry(gameOptions?.Value.ShardId ?? 0, sort, data.ToArray()));
    }
}
