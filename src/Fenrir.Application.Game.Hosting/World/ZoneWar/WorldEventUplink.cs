using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Cluster.Client.Link;
using Fenrir.Protocol.Center;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class WorldEventUplink(
    ILogger<WorldEventUplink> logger,
    IRvrSiegeEventRelayQueue? relayQueue = null,
    IOptions<GameServerOptions>? gameOptions = null,
    Lazy<ICenterLink>? centerLink = null) : IWorldEventUplink
{
    public void Publish(int sort, ReadOnlySpan<byte> data)
    {
        if (gameOptions?.Value.WorldStateAuthority == WorldStateAuthorityMode.Center)
        {
            if (centerLink is null)
            {
                // Les deux dependances etant optionnelles, l'ancien `centerLink?.Value.Send(...)` jetait
                // l'evenement sans un mot. Un evenement RvR inter-shards perdu ne leve pas et ne se voit
                // qu'au comportement du jeu.
                logger.LogError(
                    "World event sort {Sort} dropped: WorldStateAuthority is Center but no ICenterLink is " +
                    "registered -- cross-shard RvR state will diverge silently until this is fixed.", sort);
                return;
            }

            centerLink.Value.Send(new WorldEventOutbound { Sort = sort, Data = data.ToArray() });
            return;
        }

        if (relayQueue is null)
        {
            logger.LogError(
                "World event sort {Sort} dropped: no IRvrSiegeEventRelayQueue is registered and " +
                "WorldStateAuthority is not Center.", sort);
            return;
        }

        relayQueue.Enqueue(new RvrSiegeEventRelayEntry(gameOptions?.Value.ShardId ?? 0, sort, data.ToArray()));
    }
}
