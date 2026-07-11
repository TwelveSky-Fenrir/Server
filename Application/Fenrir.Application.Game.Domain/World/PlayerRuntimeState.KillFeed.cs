namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy per-player running kill counter (<c>H07_MyGame.h:905</c>) -- the ranking key
    ///     <see cref="Combat.KillFeedLeaderboard" /> sorts its top-3 by. Incremented by exactly one on every
    ///     qualifying enemy-tribe kill this character lands while the killing zone's own war state is active
    ///     (<see cref="World.Zone.RecordEnemyKillForFeed" />), and -- critically -- NEVER reset by
    ///     <see cref="Combat.KillFeedLeaderboard.Clear" /> at battle open/reset/close: a returning player
    ///     carries prior-battle kills into a later battle's leaderboard, exactly matching the source behavior
    ///     contract's own "Ranking key is a session total, not a per-battle total" edge case.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Réf. C++ : Server/ts25zone/H07_MyGame.h:905 (field) ; Server/ts25zone/S03_MyUser.cpp:428 (reset only at
    ///         player (re)initialization).
    ///     </para>
    ///     <para>
    ///         <b>Known wiring gap:</b> legacy resets this only when the player's own connection-state object
    ///         is (re)initialized -- i.e. never across an in-process zone transfer. Fenrir's own
    ///         <see cref="World.Zone.HandleEnter" /> constructs a brand-new <see cref="PlayerRuntimeState" />
    ///         on EVERY zone entry, including an in-process transfer (see <c>PlayerEnterData</c>'s own "must
    ///         travel here or it silently resets" contract in <c>ZoneCommand.cs</c>) -- so byte-exact "only
    ///         resets at true (re)initialization, never at a zone transfer" parity additionally requires this
    ///         value to be threaded through <c>PlayerEnterData</c>/<c>ZoneTransfer.cs</c>, neither of which
    ///         this file may edit. Until that wiring lands, this counter silently resets to 0 on every zone
    ///         transfer, not just on a fresh login -- see the C15 kill-feed-ranking wiring manifest.
    ///     </para>
    /// </remarks>
    public int SessionKillCount { get; set; }
}
