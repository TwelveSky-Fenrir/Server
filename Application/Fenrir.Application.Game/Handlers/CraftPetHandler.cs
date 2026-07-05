using Fenrir.Application.Game.Crafting;
using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op88, CZ_MAKE_PET_SEND -- 6 pet-fusion recipes (S04_MyWork02.cpp:12125-12501, LNW33+__GOD__ build),
///     delegated to <see cref="ICraftPetService" />. The server-wide "notable craft" announcement
///     (<c>MakeNotice</c> -&gt; Center broadcast) has no single-process equivalent in Fenrir and is not
///     reproduced here, matching the precedent set for other cross-server notices (e.g. TribeBank's audit
///     trail).
/// </summary>
public sealed class CraftPetHandler(ICraftPetService craftPetService)
    : IAsyncPacketHandler<CraftPetRequest>
{
    public async ValueTask HandleAsync(CraftPetRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(CraftPetRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        CraftPetResult result;

        switch (packet.Sort)
        {
            case PetCraftRecipeCatalog.Recipe1Sort:
            case PetCraftRecipeCatalog.Recipe2Sort:
            case PetCraftRecipeCatalog.Recipe3Sort:
                result = await craftPetService.ResolveFourSlotRecipeAsync(packet, zone, state, characterId,
                    cancellationToken);
                break;
            case PetCraftRecipeCatalog.Recipe4Sort:
            case PetCraftRecipeCatalog.Recipe5Sort:
            case PetCraftRecipeCatalog.Recipe6Sort:
                result = await craftPetService.ResolveTwoSlotRecipeAsync(packet, zone, state, characterId,
                    cancellationToken);
                break;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        if (result.Outcome != CraftPetOutcome.Applied)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new CraftPetResponse
        {
            Result = result.WireResult,
            Value = [result.ResultItemId, 0, 0, result.ResultQuantity, 0, result.Serial]
        });
    }
}
