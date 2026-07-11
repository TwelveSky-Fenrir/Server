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
///     op88, CZ_MAKE_PET_SEND -- 6 pet-fusion recipes (S04_MyWork02.cpp:12125-12501, LNW33+__GOD__ build),
///     delegated to <see cref="ICraftPetService" />. See <c>CraftPetService</c>'s own remarks for the
///     shared <c>MakeNotice</c> "notable craft" announcement (relay sort 2000, this recipe family's own call
///     site at S04_MyWork02.cpp:12276) -- it is stood in for by a log line
///     (<c>CenterRelayNoticeLog.LogNotableCraft</c>), never a client-facing broadcast: a 2026-07-11
///     confirmation pass re-verified that no such broadcast is recoverable for this notice family (the
///     Center-side relay case is a permanently-empty stub with no default fallback in either process), so this
///     is correct terminal legacy-parity behavior, not a placeholder for future work.
/// </summary>
public sealed class CraftPetHandler(ICraftPetService craftPetService, ILogger<CraftPetHandler> logger)
    : IAsyncPacketHandler<CraftPetRequest>
{
    public async ValueTask HandleAsync(CraftPetRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: CraftPetRequest (op88) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, accountId,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(CraftPetRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, int accountId,
        CancellationToken cancellationToken)
    {
        CraftPetResult result;

        switch (packet.Sort)
        {
            case PetCraftRecipeCatalog.Recipe1Sort:
            case PetCraftRecipeCatalog.Recipe2Sort:
            case PetCraftRecipeCatalog.Recipe3Sort:
                result = await craftPetService.ResolveFourSlotRecipeAsync(packet, zone, state, characterId,
                    accountId, cancellationToken);
                break;
            case PetCraftRecipeCatalog.Recipe4Sort:
            case PetCraftRecipeCatalog.Recipe5Sort:
            case PetCraftRecipeCatalog.Recipe6Sort:
                result = await craftPetService.ResolveTwoSlotRecipeAsync(packet, zone, state, characterId,
                    accountId, cancellationToken);
                break;
            default:
                logger.LogWarning(
                    "Craft-pet request rejected for character {CharacterId}: invalid sort {Sort} -- aborting session",
                    characterId, packet.Sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        if (result.Outcome != CraftPetOutcome.Applied)
        {
            logger.LogWarning(
                "Craft-pet recipe rejected for character {CharacterId}: sort {Sort} -- aborting session",
                characterId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        logger.LogInformation(
            "Character {CharacterId} crafted pet: result item {ResultItemId}x{ResultQuantity} (recipe sort {Sort})",
            characterId, result.ResultItemId, result.ResultQuantity, packet.Sort);

        session.Send(new CraftPetResponse
        {
            Result = result.WireResult,
            Value = [result.ResultItemId, 0, 0, result.ResultQuantity, 0, result.Serial]
        });
    }
}
