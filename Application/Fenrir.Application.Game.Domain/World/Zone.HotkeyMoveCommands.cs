using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Hotkeys;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Already-validated-and-SQL-durable self-mutation mirror for the CZ_PROCESS_DATA_SEND hotkey-bind family
///     (tSort 204/205/211/253/214/216) -- posted by
///     <c>Fenrir.Application.Game.Services.Hotkeys.HotkeyActionService</c> after
///     <c>Fenrir.Application.Game.Domain.Hotkeys.HotkeyActionResolver</c> decided the move and
///     <c>ICharacterRepository</c> already persisted it. See <see cref="HotkeyMoveZoneCommand" />'s own
///     remarks for the exact shape and why this is a dedicated channel rather than folded into
///     <c>_inventoryInbox</c>/<c>_hotkeySlotInbox</c> (<c>Zone.EconomyMirrors.cs</c>/
///     <c>Zone.CosmeticMirrors.cs</c>).
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Bounded capacity for <see cref="_hotkeyMoveInbox" /> -- also the basis for
    ///     <see cref="HotkeyMoveInboxDrainCapPerTick" />.
    /// </summary>
    private const int HotkeyMoveInboxCapacity = 512;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_hotkeyMoveInbox" /> -- same "half of this channel's own bounded
    ///     capacity" convention as <see cref="InboxDrainCapPerTick" /> (see that constant's own remarks for the
    ///     full rationale).
    /// </summary>
    private const int HotkeyMoveInboxDrainCapPerTick = HotkeyMoveInboxCapacity / 2;

    private readonly Channel<HotkeyMoveZoneCommand> _hotkeyMoveInbox =
        Channel.CreateBounded<HotkeyMoveZoneCommand>(
            new BoundedChannelOptions(HotkeyMoveInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostHotkeyMoveCommand(in HotkeyMoveZoneCommand command)
    {
        return _hotkeyMoveInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Posts <paramref name="command" /> (its <see cref="HotkeyMoveZoneCommand.Applied" /> is overwritten --
    ///     leave it default) and waits until this zone's tick has actually mirrored it, not merely accepted it.
    ///     Callers must already hold <see cref="PlayerRuntimeState.EconomyActionLock" /> -- same contract as
    ///     <c>Zone.PostInventoryCommandAndWaitAsync</c> (<c>Zone.EconomyMirrors.cs</c>). A timeout still returns
    ///     true: the SQL write is already durable, a timed-out mirror just stays stale until next world entry.
    /// </summary>
    public async Task<bool> PostHotkeyMoveCommandAndWaitAsync(HotkeyMoveZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostHotkeyMoveCommand(in withSignal))
            return false; // inbox full -- caller logs this; nothing to wait for.

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    private void DrainHotkeyMoveCommands()
    {
        var processed = 0;
        while (processed < HotkeyMoveInboxDrainCapPerTick && _hotkeyMoveInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyHotkeyMoveCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                // Same containment posture as DrainInbox: one bad hotkey-move command must never take the
                // whole tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} hotkey-move command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= HotkeyMoveInboxDrainCapPerTick)
            LogDrainCapEngaged(_hotkeyMoveInbox.Reader, "hotkey-move", HotkeyMoveInboxDrainCapPerTick);
    }

    /// <summary>
    ///     No validation/I/O here on purpose -- already decided and persisted by the posting service before
    ///     reaching the inbox. A no-op if the character already left this zone by the time the tick drains
    ///     this: their SQL write is already durable, so there is nothing left to mirror.
    /// </summary>
    private void ApplyHotkeyMoveCommand(in HotkeyMoveZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        foreach (var write in command.HotkeyWrites)
            state.SetHotkeySlot(write.Page, write.Index, write.Slot);

        if (command.InventoryContainer is { } snapshot)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);
    }
}
