using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class DrinkBottleHandler(IDrinkBottleService drinkBottleService, ILogger<DrinkBottleHandler> logger)
    : IInlinePacketHandler<DrinkBottleRequest>
{
    public void Handle(in DrinkBottleRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: DrinkBottleRequest (op129) received for character {CharacterId}, sort {Sort} value {Value}",
            session.SessionId, characterId, packet.Sort, packet.Value);

        var result = drinkBottleService.Drink(zone, state, characterId, packet.Sort, packet.Value);

        switch (result.Outcome)
        {
            case DrinkBottleOutcome.Silent:
                return;
            case DrinkBottleOutcome.Rejected:
                logger.LogInformation(
                    "Drink-bottle rejected for character {CharacterId}: sort {Sort} value {Value}", characterId,
                    packet.Sort, packet.Value);
                session.Send(new DrunkStateResponse { Sort = packet.Sort, Result = 1, BottleIndex = 0 });
                return;
        }

        logger.LogInformation("Character {CharacterId} drank bottle slot {BottleIndex}", characterId,
            result.BottleIndex);

        session.Send(new DrunkStateResponse { Sort = 0, Result = 0, BottleIndex = result.BottleIndex });
        session.Send(new AvatarStatUpdateResponse
        {
            Sort = 112, Value = result.DrunkDurationTicks, Value2 = 0
        });
    }
}
