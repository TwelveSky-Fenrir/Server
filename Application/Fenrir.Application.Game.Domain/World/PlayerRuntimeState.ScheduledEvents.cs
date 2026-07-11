namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy <c>bHSBStoneRewardCheck</c> -- this character's one-time-per-HSB-window reward-eligibility
    ///     flag for killing a tribe-symbol-guard monster (reward items 1448/723/1073,
    ///     <c>Server/ts25zone/S07_MyGame02.cpp:2686-2702</c>). <see langword="false" /> = eligible (not yet
    ///     claimed this window); the flag flips to <see langword="true" /> when the reward is actually granted
    ///     (a combat-reward feature outside this file's scope, not yet wired) and is reset back to
    ///     <see langword="false" /> by <see cref="ZoneWar.HsbRewardFlagResetReactor" /> on case 4000. Defaults
    ///     to <see langword="false" /> (eligible) on world entry -- legacy persists this per-character in the
    ///     DB (<c>Server/Header/CSQLAvatar.cpp:730</c>); Fenrir has no backing column yet, so this is
    ///     in-memory-only and resets on zone (re)entry, the same "not yet persisted" posture as
    ///     <see cref="WarPoint" />.
    /// </summary>
    public bool HsbStoneRewardClaimed { get; set; }
}
