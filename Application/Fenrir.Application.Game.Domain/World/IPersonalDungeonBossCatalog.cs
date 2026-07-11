namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Resolves which boss monster <c>Zone.TryEnterZone241PersonalInstance</c> should summon for a given
///     rebirth tier.
/// </summary>
/// <remarks>
///     Legacy hardcodes THREE divergent tier/server-&gt;boss tables inline across three call sites (a
///     zone-enter handler table, a server-325-330-only registration-validation table, and a tick-driven
///     summon-step table), confirmed by the A4-missing-bosses contract to race against each other on entry --
///     see <see cref="PersonalDungeonBossTables" />'s own class remarks for the concrete ids and the confirmed
///     race resolution. <see cref="Fenrir.Application.Game.Domain.World.Zone" /> only ever calls the one
///     method below; <see cref="Zone241RebirthTierBossCatalog" /> is the concrete implementation, backed by
///     the tick-driven summon-step table (<see cref="PersonalDungeonBossTables.ResolveCatalogE" />) since that
///     is the table the confirmed race resolves to in practice, not the zone-enter-handler table this
///     interface's method was originally modeled after -- see that class's own remarks for why.
/// </remarks>
public interface IPersonalDungeonBossCatalog
{
    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId);
}

/// <summary>
///     Default <see cref="IPersonalDungeonBossCatalog" /> wired into every <see cref="Zone" />: no tier ever
///     resolves, so every Zone-241 entry attempt fails the summon step and is refused (see
///     <c>Zone.TryEnterZone241PersonalInstance</c>'s <c>SummonFailed</c> outcome) until a real catalog is
///     supplied. <see cref="Zone241RebirthTierBossCatalog" /> is now available with real, cited ids -- see its
///     own remarks -- but is not yet assigned to any production <see cref="Zone" /> instance; see this
///     cluster's wiring report for the exact change needed.
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
