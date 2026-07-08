using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="IPlaytimeBuffService" />
/// <remarks>
///     Every call unconditionally clobbers <see cref="PlayerRuntimeState.PlayTime2" /> to
///     <see cref="PlaytimeBuffResolver.PlayTimeClobberValue" />, valid sort or not -- reproducing op97's
///     second legacy defect (see <see cref="PlaytimeBuffResolver" />'s remarks) rather than silently
///     preserving the tick-accumulated counter. This is a deliberate legacy-parity choice, not an oversight:
///     the project's D8 posture is to reproduce non-UB/non-crash legacy quirks as-is and flag any deviation
///     explicitly, and this one is purely a data clobber (a stale/misleading session HUD stat), not a crash
///     or memory-safety bug worth "fixing" out from under legacy parity.
/// </remarks>
public sealed class PlaytimeBuffService : IPlaytimeBuffService
{
    public PlaytimeBuffResult Apply(Zone zone, int characterId, int sort)
    {
        var resolved = PlaytimeBuffResolver.Resolve(sort);

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(
            characterId,
            resolved.Applied ? resolved.NewStateTimeEffect : null,
            PlayTime2: PlaytimeBuffResolver.PlayTimeClobberValue));

        return new PlaytimeBuffResult(resolved.Value);
    }
}
