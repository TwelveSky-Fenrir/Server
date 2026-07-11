using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Hotkeys;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

        private const int HotkeyMoveInboxCapacity = 512;

        private const int HotkeyMoveInboxDrainCapPerTick = HotkeyMoveInboxCapacity / 2;

    private readonly Channel<HotkeyMoveZoneCommand> _hotkeyMoveInbox =
        Channel.CreateBounded<HotkeyMoveZoneCommand>(
            new BoundedChannelOptions(HotkeyMoveInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostHotkeyMoveCommand(in HotkeyMoveZoneCommand command)
    {
        return _hotkeyMoveInbox.Writer.TryWrite(command);
    }

        public async Task<bool> PostHotkeyMoveCommandAndWaitAsync(HotkeyMoveZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostHotkeyMoveCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
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
                logger.LogError(ex, "Zone {MapId} hotkey-move command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= HotkeyMoveInboxDrainCapPerTick)
            LogDrainCapEngaged(_hotkeyMoveInbox.Reader, "hotkey-move", HotkeyMoveInboxDrainCapPerTick);
    }

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
