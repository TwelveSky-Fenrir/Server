using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_STELLAR_STATE_SEND (op153). Sort 1-5 (Select/no-op/Equip/Remove/ReturnToInventory) match the legacy
///     switch exactly -- see <see cref="StellarCores.StellarCoreStateResolver" />'s remarks for why Select/Equip/
///     Remove/ReturnToInventorySuccess never actually fire against today's always-empty wardrobe. Same shape as
///     <see cref="CostumeStateHandler" />.
/// </summary>
public sealed class StellarCoreStateHandler(IStellarCoreStateService service)
    : IAsyncPacketHandler<StellarCoreStateRequest>
{
    public async ValueTask HandleAsync(StellarCoreStateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ApplyAsync(zone, state, characterId, packet.Sort, packet.Value,
                cancellationToken);

            switch (result.Outcome)
            {
                case StellarCoreStateOutcome.NoReply:
                    return;

                case StellarCoreStateOutcome.Disconnect:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;

                case StellarCoreStateOutcome.Reply:
                    session.Send(new StellarCoreStateResponse
                    {
                        Result = result.ResultCode, Sort = packet.Sort, Value = packet.Value, Page = result.Page,
                        PosX = result.PosX, PosY = result.PosY, ItemIndex = result.ItemIndex
                    });
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
