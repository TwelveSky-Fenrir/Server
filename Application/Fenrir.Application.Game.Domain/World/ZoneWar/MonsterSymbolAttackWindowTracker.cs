namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe "has the current monster-symbol holder been notified yet" latch for
///     <see cref="MonsterSymbolAttackWindowNotifySystem" /> -- the modern replacement for legacy's per-instance
///     <c>bAttackMonsterSymbol</c> flag plus its paired start-timestamp. Ephemeral only: unlike
///     <see cref="WorldState.WorldStateService" />, nothing here is persisted, matching the legacy field's own
///     in-memory-only, reset-on-process-restart lifetime -- there is no schema column for this anywhere in
///     <c>game.WorldState</c>.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:2622-2651 (<c>bAttackMonsterSymbol</c> and its paired start
///     timestamp, both zone-instance-local fields in the legacy's one-process-per-zone architecture).
///     <para>
///         Fenrir collapses every zone this shard hosts into one process, and only the single zone matching the
///         current holder's mapped instance (<see cref="GameServerOptions.MonsterSymbolAttackNotifyMapIds" />)
///         ever calls <see cref="ShouldNotifyNow" /> with a non-early-return path in
///         <see cref="MonsterSymbolAttackWindowNotifySystem" />, so tracking one shared latch here (rather than
///         one per <see cref="Zone" />) is behaviorally equivalent to the legacy's own one-flag-per-matching-
///         instance shape -- there is never more than one "current" instance to track at a time.
///     </para>
///     <para>
///         GAP -- not modeled: whether the legacy ever resets <c>bAttackMonsterSymbol</c> back to false while
///         the SAME tribe keeps holding the symbol (allowing a repeat notification within one holding period)
///         was not independently confirmed by the translated behavior contract this class implements. This
///         class only ever re-arms (resets <see cref="_notified" /> to false) when the HOLDER TRIBE itself
///         changes -- the conservative "at most once per holding period" reading the contract's own prose
///         states as the confirmed behavior ("whether this notification can fire more than once per
///         monster-symbol holding period... was not determined here").
///     </para>
/// </remarks>
public sealed class MonsterSymbolAttackWindowTracker
{
    private readonly Lock _lock = new();
    private int _elapsedLegacyTicksSinceCapture;
    private byte? _lastHolder;
    private bool _notified;

    /// <summary>
    ///     Advances the latch by <paramref name="legacyTicksElapsed" /> for <paramref name="holderTribe" />,
    ///     re-arming (elapsed reset to 0, notified reset to false) the instant the holder tribe itself changes
    ///     from the last call. Returns true exactly once per holding period, the first call where the
    ///     accumulated elapsed ticks reach <paramref name="delayLegacyTicks" /> -- every later call for the same
    ///     unchanged holder returns false.
    /// </summary>
    public bool ShouldNotifyNow(byte holderTribe, int legacyTicksElapsed, int delayLegacyTicks)
    {
        lock (_lock)
        {
            if (_lastHolder != holderTribe)
            {
                _lastHolder = holderTribe;
                _elapsedLegacyTicksSinceCapture = 0;
                _notified = false;
            }

            _elapsedLegacyTicksSinceCapture += legacyTicksElapsed;

            if (_notified || _elapsedLegacyTicksSinceCapture < delayLegacyTicks)
                return false;

            _notified = true;
            return true;
        }
    }
}
