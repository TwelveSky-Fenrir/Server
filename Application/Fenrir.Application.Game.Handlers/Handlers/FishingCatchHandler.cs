using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_FISHING_REWARD_SEND (opcode 105) -- same zone-52 gating as <see cref="FishingLineHandler" />.
///     Silently ignored unless currently in the "catch" state (step 4/5, <c>CatchingFish</c>). Step 4 rolls a
///     koi item and grants it (Result=2/inventory-full resets fishing to idle with no further reply, matching
///     the legacy's early return); step 5 is a pure miss -- no item, straight to the echoed progress reply.
///     Legacy quirk (kept, not "fixed" per D8): a successful/miss catch does NOT reset FishingState/FishingStep,
///     so a repeated CZ_FISHING_REWARD_SEND while still in step 4 re-rolls and re-grants another item.
/// </summary>
public sealed class FishingCatchHandler(IFishingCatchService fishingCatchService)
    : IAsyncPacketHandler<FishingCatchRequest>
{
    public async ValueTask HandleAsync(FishingCatchRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (state.FishingState == 0 || !state.CatchingFish)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await fishingCatchService.ResolveAndApplyAsync(zone, state, characterId, session, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
