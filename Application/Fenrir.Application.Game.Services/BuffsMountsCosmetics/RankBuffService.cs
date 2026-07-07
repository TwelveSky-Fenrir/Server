using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="IRankBuffService" />
public sealed class RankBuffService : IRankBuffService
{
    /// <summary>
    ///     ReturnSymbolNumNoMon under a no-alliance, no-capture-event default world state -- see RankBuffResolver's
    ///     remarks.
    /// </summary>
    private const int DefaultStoneCount = 1;

    /// <remarks>
    ///     Deliberately does not read <c>state.Stats?.MaxLife</c>/<c>MaxMana</c> here: this method runs on the
    ///     session thread, and the character's true max-life/max-mana at the moment the posted command is
    ///     actually drained (on the zone's tick thread) can differ if a concurrent buff expiry, gear change, or
    ///     level-up moves the cap in between. <see cref="AvatarBuffZoneCommand.HealToMax" /> only requests the
    ///     heal; <see cref="Zone.ApplyAvatarBuffCommand" /> derives the actual Life/Mana values from the
    ///     tick-owned state at apply time. See the items-skills-fenrir-self-critique finding this fixes.
    /// </remarks>
    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort)
    {
        var resolved = RankBuffResolver.Resolve(sort, DefaultStoneCount);
        if (!resolved.Succeeded)
            return new RankBuffResult(false);

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(characterId, RankBuffType: sort, HealToMax: true));

        return new RankBuffResult(true);
    }
}
