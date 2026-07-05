using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op30, CZ_MAKE_SKILL_SEND -- 4 fragment-to-skill-book recipes (S04_MyWork02.cpp:5868-6017), delegated to
///     <see cref="ICraftSkillBookService" />. Recipes 0-2 are unconditional (no roll); recipe 3 (War God)
///     additionally rolls the granted skill via <c>SkillBookCraftResolver.ResolveWarGod</c>.
/// </summary>
public sealed class CraftSkillBookHandler(ICraftSkillBookService craftSkillBookService)
    : IAsyncPacketHandler<CraftSkillBookRequest>
{
    public async ValueTask HandleAsync(CraftSkillBookRequest packet, IPacketSession session,
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
