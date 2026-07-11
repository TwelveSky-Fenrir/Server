using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Stats;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

        private const int GmExperienceInboxCapacity = 64;

        private const int GmExperienceInboxDrainCapPerTick = GmExperienceInboxCapacity / 2;

        private const long GmMaxExperience = 2_000_000_000;

    private readonly Channel<GmSelfExperienceGrantZoneCommand> _gmExperienceInbox =
        Channel.CreateBounded<GmSelfExperienceGrantZoneCommand>(
            new BoundedChannelOptions(GmExperienceInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostGmSelfExperienceGrantCommand(in GmSelfExperienceGrantZoneCommand command)
    {
        return _gmExperienceInbox.Writer.TryWrite(command);
    }

        public async Task<bool> PostGmSelfExperienceGrantCommandAndWaitAsync(GmSelfExperienceGrantZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostGmSelfExperienceGrantCommand(in withSignal))
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

    private void DrainGmExperienceCommands()
    {
        var processed = 0;
        while (processed < GmExperienceInboxDrainCapPerTick && _gmExperienceInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyGmSelfExperienceGrantCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} GM-experience command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= GmExperienceInboxDrainCapPerTick)
            LogDrainCapEngaged(_gmExperienceInbox.Reader, "gm-experience", GmExperienceInboxDrainCapPerTick);
    }

        private void ApplyGmSelfExperienceGrantCommand(in GmSelfExperienceGrantZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state) || state.IsMovingZone)
            return;

        switch (command.Mode)
        {
            case 0:
                ApplyGmCharacterExperienceGrant(state, command.Magnitude);
                break;

            case 1:
                if (command.Magnitude >= 1)
                    CreditPetGrowth(state, command.Magnitude);
                break;

        }
    }

        private void ApplyGmCharacterExperienceGrant(PlayerRuntimeState state, int magnitude)
    {
        if (state.Experience >= GmMaxExperience)
            return;

        if (magnitude >= GmMaxExperience)
        {
            var maxLevelExperience = worldData.LevelsByLevel.TryGetValue(LevelProgressionCalculator.MaxLevel,
                out var maxLevelRow)
                ? maxLevelRow.ExpRangeMin
                : GmMaxExperience;

            var toCap = maxLevelExperience - state.Experience;
            if (toCap > 0)
                ApplyCharacterExperienceGain(state, (int)Math.Min(toCap, int.MaxValue));

            if (state.Level2 == 0)
            {
                var toMax = GmMaxExperience - state.Experience;
                if (toMax > 0)
                    ApplyCharacterExperienceGain(state, (int)Math.Min(toMax, int.MaxValue));
            }

            return;
        }

        var remainingCapacity = GmMaxExperience - state.Experience;
        var clampedGain = magnitude < remainingCapacity ? magnitude : (int)remainingCapacity;
        ApplyCharacterExperienceGain(state, clampedGain);
    }
}
