using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftSkillBookHandler(
    ICraftSkillBookService craftSkillBookService,
    ILogger<CraftSkillBookHandler> logger)
    : IAsyncPacketHandler<CraftSkillBookRequest>
{
    public async ValueTask HandleAsync(CraftSkillBookRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: CraftSkillBookRequest received, sort {Sort}",
                zoneSession.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: CraftSkillBookRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await craftSkillBookService.ResolveAsync(packet, zone, state, characterId,
                cancellationToken);

            if (result.Outcome != CraftSkillBookOutcome.Applied)
            {
                session.Send(new CraftSkillBookResponse { Result = 1, Value = [0, 0, 0, 0, 0, 0] });
                return;
            }

            session.Send(new CraftSkillBookResponse
            {
                Result = 0, Value = [result.ResultItemId, 0, 0, 0, 0, result.Serial]
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
