using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op30, CZ_MAKE_SKILL_SEND -- 4 fragment-to-skill-book recipes (S04_MyWork02.cpp:5868-6017), delegated to
///     <see cref="ICraftSkillBookService" />. Recipes 0-2 are unconditional (no roll); recipe 3 (War God)
///     additionally rolls the granted skill via <c>SkillBookCraftResolver.ResolveWarGod</c>.
/// </summary>
public sealed class CraftSkillBookHandler(
    ICraftSkillBookService craftSkillBookService,
    ILogger<CraftSkillBookHandler> logger)
    : IAsyncPacketHandler<CraftSkillBookRequest>
{
    public async ValueTask HandleAsync(CraftSkillBookRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
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

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await craftSkillBookService.ResolveAsync(packet, zone, state, characterId,
                cancellationToken);

            if (result.Outcome != CraftSkillBookOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
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
