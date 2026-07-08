using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op131, CZ_MAKE_ITEM2_SEND -- only tSort==2 is reachable, an early <c>if (tSort != 2) Quit()</c> guard
///     makes tSort 0/1/3 dead code (S04_MyWork02.cpp:14902-14906). Upgrades an already-Legendary pet (item
///     world.Items.Sort 31/32) into a further-evolved Legendary/Guardian pet for 10,000 CP + 2 catalyst stones
///     (delegated to <see cref="ICraftLegendaryPetService" />). The server-wide "notable craft" announcement
///     (<c>MakeNotice</c>) is not reproduced -- see <see cref="CraftPetHandler" />'s own remarks.
/// </summary>
public sealed class CraftLegendaryPetHandler(
    ICraftLegendaryPetService craftLegendaryPetService,
    ILogger<CraftLegendaryPetHandler> logger)
    : IAsyncPacketHandler<CraftLegendaryPetRequest>
{
    public async ValueTask HandleAsync(CraftLegendaryPetRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: CraftLegendaryPetRequest (op131) received for character {CharacterId}",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item/CP-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await craftLegendaryPetService.ResolveAsync(packet, zone, state, characterId, accountId,
                cancellationToken);

            if (result.Outcome != CraftLegendaryPetOutcome.Applied)
            {
                logger.LogWarning(
                    "Craft-legendary-pet rejected for character {CharacterId} -- aborting session", characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            logger.LogInformation(
                "Character {CharacterId} crafted legendary pet: result item {ResultItemId}", characterId,
                result.ResultItemId);

            session.Send(new CraftLegendaryPetResponse
            {
                Result = LegendaryPetCraftCatalog.WireResult,
                Value = [result.ResultItemId, 0, 0, 0, 0, result.Serial],
                Padding = 0
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
