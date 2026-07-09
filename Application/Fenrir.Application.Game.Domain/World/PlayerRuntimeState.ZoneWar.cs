using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy <c>mBugHSBPos</c> -- FIX_HSB_POS_BUG's consecutive-qualifying-tick counter for the
    ///     anti-camping forced-return-to-auto-zone check (see <see cref="AntiCampingForcedReturnSystem" />).
    ///     Persists across ticks while this character keeps being counted as "near" a guarded point; reset to
    ///     zero the instant a tick's evaluation finds it not in range, and initialized to zero on world entry
    ///     -- the same reset the legacy applies at per-connection state construction.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/H07_MyGame.h:1039 (field declaration, compiled in only under
    ///     <c>FIX_HSB_POS_BUG</c>) ; Server/ts25zone/S07_MyGame04.cpp:124-126 (zeroed on connection-state
    ///     reset).
    /// </remarks>
    public int AntiCampingProximityCounter { get; set; }

    /// <summary>
    ///     Legacy <c>mAFKTick</c> (<c>USE_REGULAR_WAR_AFK_TICK</c>) -- <see cref="ZoneWar.RegularWarAfkTickSystem" />'s
    ///     per-avatar accumulator, counting legacy ticks continuously spent on a map currently enforcing this
    ///     mechanic (a Zone049 Regular War instance mid-active-battle, or any Zone195 Nok-San instance at all).
    ///     Reset to zero whenever this avatar's own zone stops enforcing the mechanic -- see
    ///     <see cref="ZoneWar.RegularWarAfkTickSystem" />'s own remarks for why this reset-on-clear rule is a
    ///     documented engineering choice, not an independently confirmed legacy fact.
    /// </summary>
    public int AfkTick { get; set; }
}
