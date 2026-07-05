using Fenrir.Application.Game.Consumables;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op129, CZ_BOTTLE_STATE_SEND -- consumes one charge of the selected bottle slot via
///     <see cref="BottleResolver.ResolveDrink" />, healing Life to <see cref="PlayerRuntimeState.MaxLife" />.
///     Purely in-memory (no SQL): both the read and the tick-owned mirror are safe to do straight from this
///     inline handler.
/// </summary>
/// <remarks>
///     The mount-blocks-drinking gate (!aAnimalNumber || aAnimalAbsorbState) always passes today -- Fenrir has
///     no Mount/AnimalAbsorb state yet (Batch F), see <see cref="BottleResolver.ResolveDrink" />'s remarks.
///     Stat recompute (SetBasicAbilityFromEquip) is skipped: no stat input in this codebase depends on bottle
///     state, so recomputing today would be a pure no-op.
/// </remarks>
public sealed class DrinkBottleHandler : IInlinePacketHandler<DrinkBottleRequest>
{
    public void Handle(in DrinkBottleRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var resolved = BottleResolver.ResolveDrink(state.BottleSlots, packet.Sort, packet.Value);

        switch (resolved.Outcome)
        {
            case BottleResolver.DrinkOutcome.Silent:
                return;
            case BottleResolver.DrinkOutcome.Rejected:
                session.Send(new DrunkStateResponse { Sort = packet.Sort, Result = 1, BottleIndex = 0 });
                return;
        }

        session.Send(new DrunkStateResponse { Sort = 0, Result = 0, BottleIndex = packet.Value });
        session.Send(new AvatarStatUpdateResponse
        {
            Sort = 112, Value = BottleResolver.DrunkDurationTicks, Value2 = 0
        });

        zone.PostDrinkBottleCommand(new DrinkBottleZoneCommand(characterId, packet.Value, resolved.NewCount,
            state.MaxLife));
    }
}
