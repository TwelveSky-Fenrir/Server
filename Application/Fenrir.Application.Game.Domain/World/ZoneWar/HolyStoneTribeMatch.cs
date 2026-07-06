namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The "does this tribe already hold (or is allied with the holder of) the Holy Stone" test shared by the
///     Zone038 possession-war contest gate and the Zone039 territory-access eviction sweep -- both need the
///     exact same tribe-or-declared-ally comparison against <see cref="WorldState.WorldRvrState.Zone038WinTribe" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3114-3117 (the tribe-or-declared-ally "already holds the
///     Stone"/"matches the holder" helper both Zone038 and Zone039 call). The ally lookup itself is
///     <see cref="WorldState.WorldStateService.GetAllyOf" />, already resolved against the OWNER's tribe by the
///     caller -- same calling convention as <see cref="Progression.TowerFriendlyFireGate" />, which shares this
///     exact "resolve the owner's ally once, pass it in" shape for the identical class of legacy bug (never
///     resolve the ally of the character's OWN tribe and compare that back against itself).
/// </remarks>
public static class HolyStoneTribeMatch
{
    /// <param name="tribe">The character's own tribe (0-3).</param>
    /// <param name="holderTribe">
    ///     The Holy Stone's current holder tribe, or null if no tribe currently holds it (nobody has ever
    ///     captured it, or a fresh boot before the first capture) -- nothing matches a null holder.
    /// </param>
    /// <param name="allyOfHolderTribe">
    ///     <see cref="WorldState.WorldStateService.GetAllyOf" /> already resolved against
    ///     <paramref name="holderTribe" /> -- never against <paramref name="tribe" />.
    /// </param>
    public static bool Matches(byte tribe, byte? holderTribe, byte? allyOfHolderTribe)
    {
        if (holderTribe is not { } holder)
            return false;

        return tribe == holder || tribe == allyOfHolderTribe;
    }
}
