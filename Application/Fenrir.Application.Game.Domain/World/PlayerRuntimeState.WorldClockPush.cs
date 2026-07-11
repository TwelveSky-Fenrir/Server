namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy per-session throttle-state integer (Server/ts25zone/H07_MyGame.h:909, inherited by the
    ///     avatar/connected-user types at H07_MyGame.h:912/H03_MyUser.h:5) driving
    ///     <see cref="Simulation.WorldClockPushSystem" />'s self-throttled "current time" push
    ///     (<c>MyUtil::SendTime</c>, S07_MyGame03.cpp:9685-9714) -- see that system's own remarks for the full
    ///     four-state machine. Values: 0 = unset, 1 = rearm-pending, 2 = blocked, 3 = just-sent (no symbolic
    ///     names exist in the legacy source; these are the four integer literals the state machine itself ever
    ///     writes -- A6-sendtime-push contract, Inputs section). Defaults to 0 (unset) for a freshly
    ///     (re)registered avatar: the legacy forced/registration call path resets this same field to 0
    ///     immediately before its own unconditional send (contract Preconditions/Side effects), and the
    ///     field's value before that first registration was never independently traced this session (contract
    ///     Edge case 6, an open question) -- defaulting the backing CLR field to 0 here reproduces the
    ///     post-registration state regardless of how that open question resolves, since a fresh session's very
    ///     first per-tick evaluation behaves identically either way.
    /// </summary>
    public int WorldClockPushThrottleState { get; set; }
}
