namespace Fenrir.Application.Game.Domain.Gm;

/// <summary>
///     Process-wide state for the "YangGok" (Yang/Gok) PvP-kill currency-drop event, toggled by the GM
///     LocalChat sub-command <c>ygdrop</c> (a legacy general-chat command word, not a dedicated opcode --
///     Server/ts25zone/S04_MyWork02.cpp:7808-7849). Legacy keeps two independent globals: the enable flag
///     (<c>gYangGokPvPDropEvent</c>) and the drop-rate percentage (<c>gYangGokPvPDropRate</c>,
///     S04_MyWork02.cpp:7819-7820). Both are modeled here on one shard-wide singleton because the (not yet
///     modeled) PvP-kill drop consumer reads them together.
///     <para>
///         Intended as a DI singleton so the single writer (the inline LocalChat GM-command path in
///         <c>Fenrir.Application.Game.Services.Chat.LocalChatService</c>) and every future reader (a
///         tick-thread PvP-kill drop roll) share one instance. Fields are <c>volatile</c>: writes come from
///         the inline chat handler thread, reads from zone tick threads. The two fields are independent
///         toggles, so no cross-field atomicity is required -- and a reader that observes the flag enabled
///         always also observes a rate already written, because <see cref="Enable" /> stores the rate before
///         raising the flag.
///     </para>
///     <para>
///         <b>Consumer gap (flagged, not silently assumed):</b> the actual PvP-kill currency drop that reads
///         this flag/rate is a separate subsystem not modeled in Fenrir yet. This type only owns the toggle
///         and its current value; wiring the drop roll to read it is a downstream hook.
///     </para>
/// </summary>
public sealed class YangGokPvpDropEventState
{
    /// <summary>
    ///     The drop-rate percentage <c>ygdrop on</c> installs -- Server/ts25zone/S04_MyWork02.cpp:7820
    ///     (<c>gYangGokPvPDropRate = 35</c>). A cited legacy literal, not a Fenrir product default.
    /// </summary>
    public const int EnabledDropRatePercent = 35;

    private volatile int _dropRatePercent;

    private volatile bool _enabled;

    /// <summary>Whether the event is currently active (legacy <c>gYangGokPvPDropEvent</c>).</summary>
    public bool Enabled => _enabled;

    /// <summary>
    ///     The active drop-rate percentage (legacy <c>gYangGokPvPDropRate</c>). Meaningful only while
    ///     <see cref="Enabled" />; retained (not reset) across a <see cref="Disable" />, matching legacy's own
    ///     <c>off</c> branch which never touches the rate.
    /// </summary>
    public int DropRatePercent => _dropRatePercent;

    /// <summary>
    ///     <c>ygdrop on</c>: installs the rate first, then raises the flag, so a concurrent reader never sees
    ///     the flag set against a stale rate (S04_MyWork02.cpp:7819-7820).
    /// </summary>
    public void Enable()
    {
        _dropRatePercent = EnabledDropRatePercent;
        _enabled = true;
    }

    /// <summary>
    ///     <c>ygdrop off</c>: clears only the flag; the rate is deliberately left as-is, matching legacy's own
    ///     <c>off</c> branch which touches only <c>gYangGokPvPDropEvent</c> (S04_MyWork02.cpp:7828).
    /// </summary>
    public void Disable()
    {
        _enabled = false;
    }
}
