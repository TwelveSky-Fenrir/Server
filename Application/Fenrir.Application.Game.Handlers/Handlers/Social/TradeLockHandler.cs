using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeLockHandler(
    ZoneRegistry zones,
    ITradeLockService tradeLockService,
    ILogger<TradeLockHandler> logger) : IAsyncPacketHandler<TradeLockRequest>
{
    public async ValueTask HandleAsync(TradeLockRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeLock: session {SessionId} character {CharacterId}", session.SessionId, characterId);

        var attempt = tradeLockService.TryLock(characterId);
        if (!attempt.Locked)
            return;

        var trade = attempt.Trade!;

        if (!zones.TryGetPlayerAndZone(trade.PlayerAId, out var playerA, out var zoneA) ||
            !zones.TryGetPlayerAndZone(trade.PlayerBId, out var playerB, out var zoneB))
            return;

        playerA.Session.Send(new TradeLockResponse { CheckMe = characterId == trade.PlayerAId ? 0 : 1 });
        playerB.Session.Send(new TradeLockResponse { CheckMe = characterId == trade.PlayerBId ? 0 : 1 });

        if (trade.SideA.MenuState < 2 || trade.SideB.MenuState < 2)
            return;

        var (first, second) = playerA.CharacterId < playerB.CharacterId ? (playerA, playerB) : (playerB, playerA);

        await first.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await second.EconomyActionLock.WaitAsync(cancellationToken);
            try
            {
                await tradeLockService.CommitAsync(trade, playerA, zoneA, playerB, zoneB, characterId,
                    cancellationToken);
            }
            finally
            {
                second.EconomyActionLock.Release();
            }
        }
        finally
        {
            first.EconomyActionLock.Release();
        }
    }
}
