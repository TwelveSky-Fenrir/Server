using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeBankHandler(
    ITribeBankService bankService,
    ITribeBankWithdrawService withdrawService,
    ILogger<TribeBankHandler>? logger = null)
    : IAsyncPacketHandler<TribeBankRequest>
{
    public async ValueTask HandleAsync(TribeBankRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_BANK_SEND received (character {CharacterId}, sort {Sort}, value {Value})",
            session.SessionId, zoneSession.CharacterId, packet.Sort, packet.Value);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = packet.Sort switch
            {
                1 => await bankService.ViewAsync(zoneSession, state, cancellationToken),
                2 => await withdrawService.WithdrawAsync(packet.Value, state, characterId, cancellationToken),
                _ => TribeBankResult.Aborted
            };

            if (!result.Success)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new TribeBankResponse
                { Result = 0, Sort = result.Sort, TribeBankInfo = result.TribeBankInfo!, Money = result.Money });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
