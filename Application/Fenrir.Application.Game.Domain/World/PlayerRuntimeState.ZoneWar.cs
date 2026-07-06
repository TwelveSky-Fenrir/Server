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
}
