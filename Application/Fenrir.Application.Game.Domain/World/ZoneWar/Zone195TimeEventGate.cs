namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Mirrors legacy's <c>mGAME.isZone195TimeEvent</c> flag (Server/ts25zone/S07_MyGame01.cpp:260-317,429) --
///     the sole gate for two otherwise-live, <c>LNW33</c>-reachable reward branches: the Zone195 "Nok-San"
///     capture-commit time-bonus CP/hero-point grant (<c>Simulation.Zone195NokSanSystem</c>,
///     Server/ts25zone/S07_MyGame01.cpp:8494-8524) and the maps-195/196 branch of the cross-tribe PvP-kill
///     reward switch (<c>Combat.PvpKillExtendedRewardZones</c>, Server/ts25zone/S07_MyGame03.cpp:3017-3029).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this is a constant, not a computed or externally-wired value:</b> the flag's only setter in
///         the entire legacy codebase lives inside the free function <c>EventLogic()</c>
///         (Server/ts25zone/S07_MyGame01.cpp:260-317), which would set it <see langword="true" /> only for
///         server number 196, only on a Sunday, only while the wall-clock hour is 20 or 21
///         (<c>S07_MyGame01.cpp:274,294-305</c>). But <c>EventLogic()</c> itself has exactly two call sites in
///         the whole codebase and <b>both are commented out</b> (<c>S07_MyGame01.cpp:1679</c>,
///         <c>:1926</c>) -- confirmed by direct grep this session, no other call site exists anywhere.
///         <c>mGAME.isZone195TimeEvent</c> is otherwise set exactly once, to <see langword="false" />, at
///         process boot inside <c>MyGame::Init()</c> (<c>S07_MyGame01.cpp:429</c>) and never changes again for
///         the lifetime of the process. Both gated reward branches are therefore permanently dead in every real
///         production build (<c>ReleaseEU33</c>) despite being otherwise-live, <c>LNW33</c>-reachable code --
///         <c>LNW33</c> itself IS the real, live build-variant gate (confirmed this session: it is
///         <c>#define</c>'d only inside the <c>#ifdef M33</c> branch of <c>Server/Header/Protocol/DEFINE.h:21-51</c>,
///         and <c>M33</c> is active in the one real buildable configuration). The dead behavior is caused
///         entirely by <c>isZone195TimeEvent</c> always being false, never by the <c>LNW33</c> gate itself being
///         closed. Full citation trail: the <c>A9-a10-triggers</c> contract (wave10), Edge cases section.
///     </para>
///     <para>
///         <b>Do not wire this to a wall-clock computation, and do not treat it as "pending" future wiring.</b>
///         A prior pass modeled the PvP-kill-reward counterpart (
///         <c>PvpKillRewardZoneRuntimeState.Map195TimeEventActive</c>)
///         as a not-yet-wired live world-state toggle defaulting to <see langword="false" /> pending a future
///         wiring pass -- that default happens to already match this gate's permanent value, but the docs
///         inviting future wiring were stale: there is no live trigger to ever wire it to. A second consumer
///         (the Zone195 Nok-San capture-commit reward gate) instead actively re-computed a
///         Sunday-20:00-21:59-UTC wall-clock window and returned <see langword="true" /> during it -- that one
///         was an active bug, not a stale-but-safe default: it granted the capture-commit time-bonus reward
///         during a window the legacy source would never actually open. Both consumers should defer to
///         <see cref="IsOpen" /> instead of reconstructing the setter condition themselves.
///     </para>
/// </remarks>
public static class Zone195TimeEventGate
{
    /// <summary>
    ///     Always <see langword="false" />, mirroring <c>mGAME.isZone195TimeEvent</c>'s permanent boot-time
    ///     value in every live <c>ts25zone</c> build. See this class's own remarks for the full citation trail
    ///     establishing why no live trigger can ever flip it. Exposed as a property (not a raw literal at each
    ///     call site) so every consumer shares one documented, greppable source of truth, and so a future
    ///     reverse-engineering pass that DOES find a live out-of-band trigger (e.g. a GM console /
    ///     <c>ts25ztool</c> override -- explicitly not yet investigated, see the source contract's Open
    ///     questions) has exactly one place to update.
    /// </summary>
    public static bool IsOpen => false;
}
