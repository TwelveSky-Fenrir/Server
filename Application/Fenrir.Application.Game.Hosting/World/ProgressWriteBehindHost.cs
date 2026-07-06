using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Builds and persists <see cref="CharacterProgressTvp" /> rows for the Vitals/Progression side of a
///     drained character write-behind batch -- the flush reader that Blocker #1 of the 2026-07-05 audit found
///     missing (combat HP loss, monster-kill XP, level-ups, quest/CP progress were marked dirty via
///     <see cref="DirtyFlags.Vitals" />/<see cref="DirtyFlags.Progression" /> but never actually flushed to
///     <c>game.Characters</c>, which is also the root cause of the tSort 16/17 tribe-scroll CP-duplication
///     exploit: <c>TribeActionService</c>'s CP debit never survived a relog).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this is not its own independently-timed <see cref="Microsoft.Extensions.Hosting.BackgroundService" />
///         / <see cref="IWriteBehindFlusher" />, despite that being the more literal reading of "add a new write-behind
///         host":</b> <see cref="DirtyTracker{TKey}" /> is registered exactly once as a process-wide singleton
///         (<c>DomainServiceCollectionExtensions.AddGameDomain</c>) and is the SAME instance every Vitals/Progression
///         call site (combat, death/XP-loss, revive, skill cast, quest/pet/rebirth/CP mirrors) and
///         <c>Zone.PlayerLifecycle.HandleMove</c>'s Position mark both feed. <see cref="DirtyTracker{TKey}.DrainAll" />
///         is all-or-nothing with no flag-partitioned variant, so two independently-timed
///         <see cref="WriteBehindFlusher{TKey}" /> loops draining it would race: whichever fires first empties the
///         WHOLE dictionary (every character, every flag) and the other finds nothing left to do that round --
///         not merely delayed, silently dropped, since nothing re-marks the entry. Worse, both
///         <c>usp_Character_PersistBatch</c> (position) and <c>usp_Character_PersistProgressBatch</c> (this class)
///         share one per-character <c>FlushSequence</c> guard column; if both procs were ever called with the
///         identical CURRENT <see cref="PlayerRuntimeState.FlushSequence" /> value in the same real-time window
///         (e.g. a player who moved AND fought within the same ~5s poll interval), whichever proc call lands
///         second would fail its own "FlushSequence &gt; stored" idempotence guard and silently no-op --
///         reproducing a differently-shaped variant of the exact bug this class exists to close. And
///         <c>GameConnectionHost</c>'s disconnect path holds exactly one <see cref="IWriteBehindFlusher" />
///         and calls <see cref="IWriteBehindFlusher.RequestImmediateFlush" /> once, expecting that single call to
///         durably capture the disconnecting player's pending state -- two independent flushers racing each
///         other is precisely the wrong time for that race to matter most.
///     </para>
///     <para>
///         The safe design is one coordinated drain: <see cref="PositionWriteBehindHost" /> remains the sole
///         owner of the one <see cref="WriteBehindFlusher{TKey}" /> loop and the one <see cref="IWriteBehindFlusher" />
///         registration (disconnect flush keeps working with zero changes to <c>GameConnectionHost</c>), and calls
///         <see cref="FlushAsync" /> from its own callback on every drained batch, before building position rows.
///         Within that single batch, this class always "wins" the shared <c>FlushSequence</c> slot for a character
///         that has Vitals/Progression flags: it persists using the CURRENT value, and <see cref="FlushAsync" />'s
///         return value tells the caller which character ids must defer (re-mark) their Position row instead of
///         colliding on that same now-already-advanced value. Position is the safe side to
///         defer -- it was already documented as best-effort/eventually-consistent (a stale position self-heals on
///         the player's next move, or is caught by the disconnect flush); Progression is the exploit/durability
///         -critical side and must never lose its turn.
///     </para>
/// </remarks>
public sealed class ProgressWriteBehindHost(ZoneRegistry zones, ICharacterRepository characters)
{
    private const DirtyFlags ProgressFlags = DirtyFlags.Vitals | DirtyFlags.Progression;

    /// <summary>
    ///     Builds a <see cref="CharacterProgressTvp" /> row (reading CURRENT <see cref="PlayerRuntimeState" />,
    ///     same "dirty tracker only holds flags, never values" posture as <see cref="PositionWriteBehindHost" />)
    ///     for every dirty entry carrying <see cref="DirtyFlags.Vitals" /> and/or <see cref="DirtyFlags.Progression" />,
    ///     persists them in one batch, and returns which character ids it claimed the shared FlushSequence slot
    ///     for this cycle.
    /// </summary>
    /// <remarks>
    ///     A character absent from every zone (logged out, or mid-handoff) is correctly dropped here, same
    ///     documented behavior as <see cref="PositionWriteBehindHost" />: their last state was already flushed by
    ///     the disconnect path's immediate flush, and a handoff re-marks them dirty on arrival.
    /// </remarks>
    public async ValueTask<IReadOnlySet<int>> FlushAsync(IReadOnlyDictionary<int, DirtyFlags> dirty,
        CancellationToken ct)
    {
        var rows = new List<CharacterProgressTvp>(dirty.Count);
        var claimed = new HashSet<int>();

        foreach (var (characterId, flags) in dirty)
        {
            if ((flags & ProgressFlags) == 0)
                continue;

            if (!zones.TryGetPlayer(characterId, out var state))
                continue;

            rows.Add(new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
                state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit,
                state.StatStr, state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints,
                state.ContributionPoints, state.Exp2, state.RebirthCount));

            claimed.Add(characterId);
        }

        // Any failure propagates uncaught: PositionWriteBehindHost's single WriteBehindFlusher callback wraps
        // both this call and its own position persist in the SAME try, so its shared onFlushError logs once
        // and re-merges the WHOLE original batch (position + progress flags together) -- never a partial
        // silent drop, same all-or-nothing retry semantics WriteBehindFlusher already guarantees.
        await characters.PersistProgressAsync(rows, ct).ConfigureAwait(false);

        return claimed;
    }
}
