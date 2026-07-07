using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Which ack the handler must send (or whether it must disconnect instead) for a resolved CZ_USE_HOTKEY_ITEM_SEND
///     (op22) request.
/// </summary>
public enum UseHotkeyItemOutcome
{
    /// <summary>Malformed input or corrupted/stale state -- the handler must abort the session; no ack is sent.</summary>
    Disconnect,

    /// <summary>A legitimate business-rule rejection -- the handler sends a Result=1 ack, nothing else.</summary>
    RejectedClean,

    /// <summary>
    ///     Accepted -- the handler sends a Result=0 ack. Any life/mana/hotkey-slot mutation this caused has already been
    ///     posted to the zone tick.
    /// </summary>
    Success
}

/// <summary>Business logic for CZ_USE_HOTKEY_ITEM_SEND (op22) -- see <c>UseHotkeyItemHandler</c>'s remarks.</summary>
public interface IUseHotkeyItemService
{
    /// <summary>
    ///     Resolves and, on <see cref="UseHotkeyItemOutcome.Success" />, durably persists + zone-mirrors the
    ///     activation of whatever is bound at (<paramref name="page" />, <paramref name="index" />) in this
    ///     character's hotkey table -- see <c>HotkeyItemConsumptionResolver</c> for the full rule.
    /// </summary>
    /// <param name="page">Raw wire value, not yet range-validated -- 0-2 once validated.</param>
    /// <param name="index">Raw wire value, not yet range-validated -- 0-13 once validated.</param>
    public ValueTask<UseHotkeyItemOutcome> UseAsync(Zone zone, PlayerRuntimeState state, int characterId, int page,
        int index, CancellationToken cancellationToken);
}
