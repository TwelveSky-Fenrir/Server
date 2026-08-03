using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftLegendaryPetHandler(
    ICraftLegendaryPetService craftLegendaryPetService,
    ILogger<CraftLegendaryPetHandler> logger)
    : IAsyncPacketHandler<CraftLegendaryPetRequest>
{
    public async ValueTask HandleAsync(CraftLegendaryPetRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: CraftLegendaryPetRequest (op131) received for character {CharacterId}",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await craftLegendaryPetService.ResolveAsync(packet, zone, state, characterId, accountId,
                cancellationToken);

            if (result.Outcome != CraftLegendaryPetOutcome.Applied)
            {
                logger.LogInformation(
                    "Craft-legendary-pet rejected for character {CharacterId} -- disconnecting", characterId);
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
