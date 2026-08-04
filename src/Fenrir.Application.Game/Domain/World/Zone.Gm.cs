using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Core.Packets.Shared;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int GmExperienceInboxCapacity = 64;

    private const long GmMaxExperience = 2_000_000_000;

    private const int GmZone124PartyPullInboxCapacity = 16;

    private const int GmMoveCoordinateDataTag = 2;

    private const int GmMoveCoordinateDataSize = 100;

    private readonly Channel<GmSelfExperienceGrantZoneCommand> _gmExperienceInbox =
        Channel.CreateBounded<GmSelfExperienceGrantZoneCommand>(
            new BoundedChannelOptions(GmExperienceInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<GmZone124PartyPullZoneCommand> _gmZone124PartyPullInbox =
        Channel.CreateBounded<GmZone124PartyPullZoneCommand>(
            new BoundedChannelOptions(GmZone124PartyPullInboxCapacity)
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

    private void DrainGmExperienceCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _gmExperienceInbox.Reader.TryRead(out var command); processed++)
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

            var toMax = GmMaxExperience - state.Experience;
            if (toMax > 0)
                ApplyCharacterExperienceGain(state, (int)Math.Min(toMax, int.MaxValue));

            return;
        }

        var remainingCapacity = GmMaxExperience - state.Experience;
        var clampedGain = magnitude < remainingCapacity ? magnitude : (int)remainingCapacity;
        ApplyCharacterExperienceGain(state, clampedGain);
    }

    public bool PostGmZone124PartyPullCommand(in GmZone124PartyPullZoneCommand command)
    {
        return _gmZone124PartyPullInbox.Writer.TryWrite(command);
    }

    public async Task<IReadOnlyList<PlayerRuntimeState>> PostGmZone124PartyPullCommandAndWaitAsync(
        GmZone124PartyPullZoneCommand command, CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied =
            new TaskCompletionSource<IReadOnlyList<PlayerRuntimeState>>(TaskCreationOptions
                .RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostGmZone124PartyPullCommand(in withSignal))
            return [];

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return [];
        }
    }

    private void DrainGmZone124PartyPullCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _gmZone124PartyPullInbox.Reader.TryRead(out var command); processed++)
            try
            {
                var pulled = ApplyGmZone124PartyPullCommand(in command);
                command.Applied?.TrySetResult(pulled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} GM zone-124 party pull command failed", MapId);
                command.Applied?.TrySetException(ex);
            }
    }

    private List<PlayerRuntimeState> ApplyGmZone124PartyPullCommand(in GmZone124PartyPullZoneCommand command)
    {
        var pulled = new List<PlayerRuntimeState>();

        foreach (var candidate in _players.Values)
        {
            if (candidate.IsMovingZone)
                continue;

            var isSelectedMember = command.TargetPartyName.Length == 0
                ? candidate.CharacterId == command.TargetCharacterId
                : string.Equals(ResolveGmZone124CandidatePartyName(candidate), command.TargetPartyName,
                    StringComparison.Ordinal);

            if (!isSelectedMember)
                continue;

            _duelRegistry.ForceClearOnZoneEntry(candidate.CharacterId);
            candidate.CanUseConsumables = true;

            candidate.PosX = command.DestinationX;
            candidate.PosY = command.DestinationY;
            candidate.PosZ = command.DestinationZ;

            var newCell = _grid.CellOf(candidate.PosX, candidate.PosZ);
            _grid.Move(candidate.CharacterId, candidate.CurrentCell, newCell, candidate.PosX, candidate.PosY,
                candidate.PosZ);
            candidate.CurrentCell = newCell;

            dirtyTracker.MarkDirty(candidate.CharacterId, DirtyFlags.Position);

            var gmData = new byte[GmMoveCoordinateDataSize];
            new GmMoveCoordinatePayload { Location = [candidate.PosX, candidate.PosY, candidate.PosZ] }
                .Write(gmData);
            candidate.Session.Send(new GmCommandResponse { Sort = GmMoveCoordinateDataTag, GmData = gmData });

            pulled.Add(candidate);
        }

        return pulled;
    }

    private string ResolveGmZone124CandidatePartyName(PlayerRuntimeState candidate)
    {
        return PartyIdentityResolver.ResolveCurrentPartyName(_partyRegistry, candidate.CharacterId, candidate.Name,
            memberId => _players.TryGetValue(memberId, out var member) ? member?.Name : null);
    }
}
