using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_BANK_SEND (opcode 82). Sort 1 is view; sort 2 is a WITHDRAW (bank slot -&gt; player money) --
///     confirmed by a fresh full read of Server/ts25zone/S04_MyWork02.cpp:11560-11607 and
///     Server/ts25playuser/S04_MyWork02.cpp:269-377, resolving an earlier 3-way contradiction between this
///     file, <see cref="ITribeBankService" />/<see cref="TribeBankService" />, and
///     <see cref="TribeBankWithdrawService" /> over what sort 2 does. Legacy has no client-invocable deposit
///     path at all on this opcode (or anywhere else) -- deposits only ever happen via the automatic
///     10-minute server-internal tax-skim sweep (<c>TribeBankTaxSweepFlushHost</c>), never a packet. Any
///     gate failure disconnects (matches legacy's undifferentiated <c>Quit()</c>), never a graceful error
///     reply.
/// </summary>
public sealed class TribeBankHandler(
    ITribeBankService bankService,
    TribeBankWithdrawService withdrawService,
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
                // Sort 2 = withdraw (bank slot -> player money), not deposit -- see the class doc comment.
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
