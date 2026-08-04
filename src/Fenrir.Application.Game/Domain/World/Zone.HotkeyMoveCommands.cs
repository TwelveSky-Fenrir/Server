using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Simulation;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int HotkeyMoveInboxCapacity = 512;

    private readonly Channel<HotkeyMoveZoneCommand> _hotkeyMoveInbox =
        Channel.CreateBounded<HotkeyMoveZoneCommand>(
            new BoundedChannelOptions(HotkeyMoveInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public bool PostHotkeyMoveCommand(in HotkeyMoveZoneCommand command)
    {
        if (_hotkeyMoveInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Hotkey-move inbox is full."));
        return false;
    }

    public async Task<ZoneCommandResult> PostHotkeyMoveCommandAndWaitForResultAsync(HotkeyMoveZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostHotkeyMoveCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Hotkey-move inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Hotkey-move command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Hotkey-move command wait was cancelled.");
        }
    }

    public async Task<bool> PostHotkeyMoveCommandAndWaitAsync(HotkeyMoveZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        return (await PostHotkeyMoveCommandAndWaitForResultAsync(command, ct, timeout).ConfigureAwait(false)).Kind ==
               ZoneCommandResultKind.Applied;
    }

    private void DrainHotkeyMoveCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _hotkeyMoveInbox.Reader.TryRead(out var command); processed++)
            try
            {
                command.Applied?.TrySetResult(ApplyHotkeyMoveCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Hotkey-move command could not be applied."));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hotkey-move command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplyHotkeyMoveCommand(in HotkeyMoveZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        foreach (var write in command.HotkeyWrites)
            state.SetHotkeySlot(write.Page, write.Index, write.Slot);

        if (command.InventoryContainer is { } snapshot)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

        return true;
    }
}
