using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TRADE_MENU_SEND (opcode 51) -- 2-notch confirm: first call locks (menu 0→1), second confirms
///     (1→2). At menu==2 on both sides, commits atomically
///     (<see cref="Fenrir.Data.Abstractions.Characters.ICharacterRepository.ExecuteTradeAsync" />) and mirrors each side's
///     new
///     container back to their own zone. An overflow aborts the whole commit -- no partial state.
/// </summary>
/// <remarks>
///     Both players' <see cref="PlayerRuntimeState.EconomyActionLock" /> are acquired in a fixed order
///     (smaller CharacterId first) to rule out lock-ordering deadlock.
/// </remarks>
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
