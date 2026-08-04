using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int PetBagInboxCapacity = 256;

    private readonly Channel<PetBagZoneCommand> _petBagInbox =
        Channel.CreateBounded<PetBagZoneCommand>(
            new BoundedChannelOptions(PetBagInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostPetBagCommand(in PetBagZoneCommand command)
    {
        return _petBagInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostPetBagCommandAndWaitAsync(PetBagZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostPetBagCommand(in withSignal))
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

    private void DrainPetBagCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _petBagInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyPetBagCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} pet-bag command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyPetBagCommand(in PetBagZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        foreach (var write in command.PetBagWrites)
            state.SetPetBagSlot(write.Slot, write.ItemId);

        if (command.InventoryContainer is { } snapshot)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);
    }
}
