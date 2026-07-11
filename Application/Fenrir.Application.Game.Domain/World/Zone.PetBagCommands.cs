using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Inventory;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Already-validated-and-SQL-durable self-mutation mirror for the CZ_PROCESS_DATA_SEND pet-bag family
///     (tSort 254 deposit, 255 withdraw, 256 rearrange) -- posted by
///     <c>Fenrir.Application.Game.Services.Inventory.PetBagActionService</c> after
///     <c>Fenrir.Application.Game.Domain.Inventory.PetBagItemTransferPolicy</c> decided the move. See
///     <see cref="PetBagZoneCommand" />'s own remarks for the exact shape.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Bounded capacity for <see cref="_petBagInbox" /> -- also the basis for
    ///     <see cref="PetBagInboxDrainCapPerTick" />.
    /// </summary>
    private const int PetBagInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_petBagInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int PetBagInboxDrainCapPerTick = PetBagInboxCapacity / 2;

    private readonly Channel<PetBagZoneCommand> _petBagInbox =
        Channel.CreateBounded<PetBagZoneCommand>(
            new BoundedChannelOptions(PetBagInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostPetBagCommand(in PetBagZoneCommand command)
    {
        return _petBagInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <c>Zone.PostInventoryCommandAndWaitAsync</c> (<c>Zone.EconomyMirrors.cs</c>) --
    ///     callers must already hold <see cref="PlayerRuntimeState.EconomyActionLock" />.
    /// </summary>
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

    private void DrainPetBagCommands()
    {
        var processed = 0;
        while (processed < PetBagInboxDrainCapPerTick && _petBagInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= PetBagInboxDrainCapPerTick)
            LogDrainCapEngaged(_petBagInbox.Reader, "pet-bag", PetBagInboxDrainCapPerTick);
    }

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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
