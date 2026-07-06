namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Resolves which boss monster <c>Zone.TryEnterZone241PersonalInstance</c> should summon for a given
///     rebirth tier.
/// </summary>
/// <remarks>
///     Legacy hardcodes two DIVERGENT tier-&gt;boss tables inline (one in the zone-enter handler, one in the
///     tick-driven summon step), disagreeing at tiers 4 and 5 (Server/ts25zone/S04_MyWork02.cpp:1167-1183 vs.
///     Server/ts25zone/S07_MyGame01.cpp:11819-11861). Neither table's concrete monster ids were captured by
///     the behavior contract this was implemented from, and inventing specific legacy monster ids without a
///     citation would violate the "no legacy parity from memory" rule -- so this interface exists to hold the
///     table's SHAPE only. <see cref="Fenrir.Application.Game.Domain.World.Zone" /> only ever calls the one
///     method below (the zone-enter-handler table's Fenrir equivalent); the tick-driven step's own divergent
///     copy has no call site in this codebase since Fenrir's tick loop never re-summons on its own (see
///     <c>Zone.DungeonInstance.cs</c>'s remarks) -- a future contract citing concrete monster ids per tier for
///     both tables is needed before this can be wired to real game data.
/// </remarks>
public interface IPersonalDungeonBossCatalog
{
    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId);
}

/// <summary>
///     Default <see cref="IPersonalDungeonBossCatalog" /> wired into every <see cref="Zone" />: no tier ever
///     resolves, so every Zone-241 entry attempt fails the summon step and is refused (see
///     <c>Zone.TryEnterZone241PersonalInstance</c>'s <c>SummonFailed</c> outcome) until a real catalog is
///     supplied (e.g. by <c>Fenrir.Application.Game.Hosting</c> once the tier tables above are captured).
/// </summary>
public sealed class NullPersonalDungeonBossCatalog : IPersonalDungeonBossCatalog
{
    public static readonly NullPersonalDungeonBossCatalog Instance = new();

    private NullPersonalDungeonBossCatalog()
    {
    }

    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId)
    {
        monsterId = 0;
        return false;
    }
}
