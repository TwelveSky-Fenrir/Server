using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_COSTUME_STATE_SEND (op90). Sort 1-5 (Select/no-op/Equip/Remove/ReturnToInventory) match the legacy
///     switch exactly -- see <see cref="CostumeStateResolver" />'s remarks for why Select/Equip/Remove/
///     ReturnToInventorySuccess never actually fire against today's always-empty wardrobe.
/// </summary>
public sealed class CostumeStateHandler(ICostumeStateService service) : IAsyncPacketHandler<CostumeStateRequest>
{
    public async ValueTask HandleAsync(CostumeStateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ApplyAsync(zone, state, characterId, accountId, packet.Sort, packet.Value,
                cancellationToken);

            switch (result.Outcome)
            {
                case CostumeStateOutcome.NoReply:
                    return;

                case CostumeStateOutcome.Disconnect:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;

                case CostumeStateOutcome.Reply:
                    session.Send(new CostumeStateResponse
                    {
                        Result = result.ResultCode, Sort = packet.Sort, Value = packet.Value, Page = result.Page,
                        PosX = result.PosX, PosY = result.PosY, ItemIndex = result.ItemIndex, CostumeDate = 0
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
