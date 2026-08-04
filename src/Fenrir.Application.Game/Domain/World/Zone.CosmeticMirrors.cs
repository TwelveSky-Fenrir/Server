using System.Buffers;
using System.Collections.Immutable;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int HeroRankPointStatSort = 904;

    private const int DrunkDurationStatSort = 112;

    private const int AutoBuffInboxCapacity = 256;

    private const int AvatarBuffInboxCapacity = 256;

    private const int BottleInboxCapacity = 256;

    private const int HotkeySlotInboxCapacity = 256;

    private const int CostumeInboxCapacity = 256;

    private const int FishingInboxCapacity = 512;

    private const int HeroRankingInboxCapacity = 256;

    private const int HeroRankingRolloverInboxCapacity = 4;

    private const int MountInboxCapacity = 256;

    private const int PshopInboxCapacity = 256;

    private const int RuneInboxCapacity = 256;

    private const int StellarCoreInboxCapacity = 256;

    private const int CharacterMpStatSort = 11;

    private const int PetActivityStatSort = 12;

    private const int MountActivityExpStatSort = 71;

    private const int MountAbsorbAuraSort = 7997;

    private const int MountAbsorbAuraDataLength = 130;

    private const int AvatarNameLength = 13;

    private const int MountRideTimeStatSort = 26;

    private const int MountAbsorbTimeStatSort = 78;

    private const int MountAbsorbStateStatSort = 79;

    private readonly Channel<AutoBuffZoneCommand> _autoBuffInbox =
        Channel.CreateBounded<AutoBuffZoneCommand>(
            new BoundedChannelOptions(AutoBuffInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly List<int> _autoBuffNeighborScratch = [];

    private readonly Channel<AvatarBuffZoneCommand> _avatarBuffInbox =
        Channel.CreateBounded<AvatarBuffZoneCommand>(
            new BoundedChannelOptions(AvatarBuffInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _avatarStateFlagNeighborScratch = [];

    private readonly Channel<DrinkBottleZoneCommand> _bottleInbox =
        Channel.CreateBounded<DrinkBottleZoneCommand>(
            new BoundedChannelOptions(BottleInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _costumeFullActionNeighborScratch = [];

    private readonly Channel<CostumeZoneCommand> _costumeInbox =
        Channel.CreateBounded<CostumeZoneCommand>(
            new BoundedChannelOptions(CostumeInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<FishingZoneCommand> _fishingInbox =
        Channel.CreateBounded<FishingZoneCommand>(
            new BoundedChannelOptions(FishingInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly List<int> _fishingNeighborScratch = [];

    private readonly Channel<HeroRankingQueryZoneCommand> _heroRankingInbox =
        Channel.CreateBounded<HeroRankingQueryZoneCommand>(
            new BoundedChannelOptions(HeroRankingInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<HeroRankingRolloverZoneCommand> _heroRankingRolloverInbox =
        Channel.CreateBounded<HeroRankingRolloverZoneCommand>(
            new BoundedChannelOptions(HeroRankingRolloverInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<HotkeySlotMirrorZoneCommand> _hotkeySlotInbox =
        Channel.CreateBounded<HotkeySlotMirrorZoneCommand>(
            new BoundedChannelOptions(HotkeySlotInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<MountZoneCommand> _mountInbox =
        Channel.CreateBounded<MountZoneCommand>(
            new BoundedChannelOptions(MountInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _pshopCloseFullActionNeighborScratch = [];

    private readonly Channel<PshopZoneCommand> _pshopInbox =
        Channel.CreateBounded<PshopZoneCommand>(
            new BoundedChannelOptions(PshopInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<RuneSocketZoneCommand> _runeInbox =
        Channel.CreateBounded<RuneSocketZoneCommand>(
            new BoundedChannelOptions(RuneInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<StellarCoreZoneCommand> _stellarCoreInbox =
        Channel.CreateBounded<StellarCoreZoneCommand>(
            new BoundedChannelOptions(StellarCoreInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostPshopCommand(in PshopZoneCommand command)
    {
        return _pshopInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostPshopCommandAndWaitAsync(PshopZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostPshopCommand(in withSignal))
            return false;

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }

        return true;
    }

    public bool PostDrinkBottleCommand(in DrinkBottleZoneCommand command)
    {
        return _bottleInbox.Writer.TryWrite(command);
    }

    public bool PostHotkeySlotMirrorCommand(in HotkeySlotMirrorZoneCommand command)
    {
        return _hotkeySlotInbox.Writer.TryWrite(command);
    }

    public bool PostHeroRankingQueryCommand(in HeroRankingQueryZoneCommand command)
    {
        return _heroRankingInbox.Writer.TryWrite(command);
    }

    public bool PostHeroRankingRolloverReset()
    {
        return _heroRankingRolloverInbox.Writer.TryWrite(default);
    }

    public bool PostFishingCommand(in FishingZoneCommand command)
    {
        if (_fishingInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Fishing inbox is full."));
        return false;
    }

    public async Task<ZoneCommandResult> PostFishingCommandAndWaitForResultAsync(FishingZoneCommand command,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostFishingCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Fishing inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Fishing command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Fishing command wait was cancelled.");
        }
    }

    public bool PostMountCommand(in MountZoneCommand command)
    {
        return _mountInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostMountCommandAndWaitAsync(MountZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostMountCommand(in withSignal))
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

    public bool PostCostumeCommand(in CostumeZoneCommand command)
    {
        return _costumeInbox.Writer.TryWrite(command);
    }

    public bool PostStellarCoreCommand(in StellarCoreZoneCommand command)
    {
        return _stellarCoreInbox.Writer.TryWrite(command);
    }

    public bool PostAvatarBuffCommand(in AvatarBuffZoneCommand command)
    {
        return _avatarBuffInbox.Writer.TryWrite(command);
    }

    public bool PostRuneSocketCommand(in RuneSocketZoneCommand command)
    {
        if (_runeInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Rune-socket inbox is full."));
        return false;
    }

    public async Task<bool> PostRuneSocketCommandAndWaitAsync(RuneSocketZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        return (await PostRuneSocketCommandAndWaitForResultAsync(command, ct, timeout).ConfigureAwait(false)).Kind ==
               ZoneCommandResultKind.Applied;
    }

    public async Task<ZoneCommandResult> PostRuneSocketCommandAndWaitForResultAsync(
        RuneSocketZoneCommand command, CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostRuneSocketCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Rune-socket inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Rune-socket command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Rune-socket command wait was cancelled.");
        }
    }

    public bool PostAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        if (_autoBuffInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Auto-buff inbox is full."));
        return false;
    }

    public async Task<ZoneCommandResult> PostAutoBuffCommandAndWaitForResultAsync(AutoBuffZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostAutoBuffCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Auto-buff inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Auto-buff command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Auto-buff command wait was cancelled.");
        }
    }

    private void DrainDrinkBottleCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _bottleInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyDrinkBottleCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} drink-bottle command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyDrinkBottleCommand(in DrinkBottleZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var itemId = command.NewItemId ?? state.BottleSlots[command.BottleIndex].ItemId;
        state.BottleSlots = state.BottleSlots.SetItem(command.BottleIndex, (itemId, command.RemainingCount));

        state.Life = command.NewLife;

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (command.DrunkTicksRemaining is { } ticksRemaining)
            state.DrunkBottleTicksRemaining = ticksRemaining;

        if (command.DrunkActiveIndex is { } activeIndex)
            state.DrunkBottleIndex = activeIndex;

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    public void ExpireDrunkBottleEffect(PlayerRuntimeState state)
    {
        if (state.DrunkBottleTicksRemaining <= 0)
            return;

        state.DrunkBottleTicksRemaining = 0;

        RecomputeAndPublish(state);

        state.Session.Send(new AvatarStatUpdateResponse { Sort = DrunkDurationStatSort, Value = 0, Value2 = 0 });
    }

    private void DrainHotkeySlotMirrorCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _hotkeySlotInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyHotkeySlotMirrorCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hotkey-slot mirror command for character {CharacterId} failed",
                    MapId, command.CharacterId);
            }
    }

    private void ApplyHotkeySlotMirrorCommand(in HotkeySlotMirrorZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.SetHotkeySlot(command.Page, command.Index, command.NewSlot);

        if (!command.BuffWrites.IsDefaultOrEmpty)
        {
            ApplyBuffWrites(state, command.BuffWrites, command.RecomputeStats);
            ApplyHotkeyAntiCheatMarker(state, command.MarkerKind, command.MarkerValue);
        }

        if (command.PetActivityGain is { } petActivityGain)
        {
            state.PetActivity = (byte)Math.Clamp(state.PetActivity + petActivityGain, 0,
                MountActivityExpCodec.MaxActivity);
            PetItemState.SynchronizeEquippedState(state.Inventory, state.PetGrowth, state.PetActivity);
            RecomputeDerivedStats(state);
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = PetActivityStatSort, Value = state.PetActivity, Value2 = 0 });
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (command.MountActivityGain is { } mountActivityGain)
        {
            var animalInfo = MountAnimalInfo.Resolve(state.AnimalIndex, state.MountActivity,
                state.MountAccumulatedExp);
            var newMountActivity = MountActivityExpCodec.FeedActivity(animalInfo.Activity, mountActivityGain);
            state.MountActivity = state.MountActivity.SetItem(animalInfo.Slot, newMountActivity);
            RecomputeDerivedStats(state);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = MountActivityExpStatSort,
                Value = MountActivityExpCodec.Pack(newMountActivity, animalInfo.Exp),
                Value2 = 0
            });
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (command.LifeGain is null && command.ManaGain is null)
            return;

        if (command.LifeGain is { } lifeGain)
        {
            var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
            state.Life = Math.Clamp(state.Life + lifeGain, 0, maxLife);
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = CharacterHpStatSort, Value = state.Life, Value2 = 0 });
        }

        if (command.ManaGain is { } manaGain)
        {
            var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
            state.Mana = Math.Clamp(state.Mana + manaGain, 0, maxMana);
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = CharacterMpStatSort, Value = state.Mana, Value2 = 0 });
        }

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    private void ApplyHotkeyAntiCheatMarker(PlayerRuntimeState state,
        HotkeyItemConsumptionResolver.AntiCheatMarkerKind markerKind, int markerValue)
    {
        var currentTick = (int)_clock.TotalMilliseconds;

        switch (markerKind)
        {
            case HotkeyItemConsumptionResolver.AntiCheatMarkerKind.DarkAttack:
                state.DarkAttackKind = markerValue;
                state.DarkAttackUseTick = currentTick;
                state.DarkAttackActiveTick = currentTick;
                break;
            case HotkeyItemConsumptionResolver.AntiCheatMarkerKind.HitRate:
                state.HitRateKind = markerValue;
                state.HitRateTick = currentTick;
                break;
            case HotkeyItemConsumptionResolver.AntiCheatMarkerKind.DodgeRate:
                state.DodgeRateKind = markerValue;
                state.DodgeRateTick = currentTick;
                break;
        }
    }

    private void DrainHeroRankingQueryCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _heroRankingInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyHeroRankingQueryCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hero-ranking query command for character {CharacterId} failed",
                    MapId, command.CharacterId);
            }
    }

    private void ApplyHeroRankingQueryCommand(in HeroRankingQueryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.Previous)
            state.LastHeroRankingPreviousQueryAtZoneClock = command.QueriedAtZoneClock;
        else
            state.LastHeroRankingCurrentQueryAtZoneClock = command.QueriedAtZoneClock;
    }

    private void DrainHeroRankingRolloverCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _heroRankingRolloverInbox.Reader.TryRead(out _); processed++)
            try
            {
                ApplyHeroRankingRolloverReset();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hero-ranking rollover reset failed", MapId);
            }
    }

    private void ApplyHeroRankingRolloverReset()
    {
        foreach (var state in _players.Values)
        {
            if (state.HeroRankPoints <= 0)
                continue;

            state.HeroRankPoints = 0;
            state.Session.Send(new AvatarStatUpdateResponse { Sort = HeroRankPointStatSort, Value = 0, Value2 = 0 });
        }
    }

    private void DrainFishingCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _fishingInbox.Reader.TryRead(out var command); processed++)
            try
            {
                var result = ApplyFishingCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Fishing command could not be applied.");
                command.Applied?.TrySetResult(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} fishing command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplyFishingCommand(in FishingZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        if (command.ApplyState)
        {
            state.FishingState = command.NewFishingState;
            state.FishingStep = command.NewFishingStep;
            state.CatchingFish = command.CatchingFish;
            state.FishingBiteWasHit = command.BiteWasHit;

            if (command.CastAtUtc is { } castAt)
                state.FishingCastAtUtc = castAt;
        }

        if (command.ActionSort is { } sort)
            state.ActionSort = sort;

        if (!command.Broadcast)
            return true;

        var fishingPet = PetActionFieldsOf(state);
        var action = new ActionInfo
        {
            Type = 0,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = fishingPet.PetLocation,
            PetTargetLocation = fishingPet.PetTargetLocation,
            PetFront = fishingPet.PetFront,
            PetSort = fishingPet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var fisherId = command.CharacterId;
        _fishingNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_fishingNeighborScratch, state.CurrentCell, fisherId, state.PosX, state.PosY,
            state.PosZ);
        _fishingNeighborScratch.Add(fisherId);
        BroadcastAvatarAction(_fishingNeighborScratch, state, action);
        return true;
    }

    private void DrainMountCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _mountInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyMountCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} mount command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyMountCommand(in MountZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;
        var garageChanged = false;

        if (command.AnimalIndex is { } animalIndex)
        {
            state.AnimalIndex = animalIndex;
            garageChanged = true;
        }

        if (command.AnimalNumber is { } animalNumber)
        {
            state.AnimalNumber = animalNumber;
            changed = true;
        }

        if (command.AnimalAbsorbState is { } animalAbsorbState)
        {
            state.AnimalAbsorbState = animalAbsorbState;
            changed = true;
        }

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (command.DeleteGarageSlot is { } deleteGarageSlot)
        {
            garageChanged = true;
            state.MountGarage = state.MountGarage.SetItem(deleteGarageSlot, 0);
            state.MountActivity = state.MountActivity.SetItem(deleteGarageSlot, 0);
            state.MountAccumulatedExp = state.MountAccumulatedExp.SetItem(deleteGarageSlot, 0);
            state.MountRolledAttributeTotal = state.MountRolledAttributeTotal.SetItem(deleteGarageSlot, 0);
            for (var statSlotIndex = 0; statSlotIndex < MountStateResolver.StatSlotCount; statSlotIndex++)
                state.MountRolledAttributes = state.MountRolledAttributes.SetItem(
                    MountStateResolver.AttributeIndex(deleteGarageSlot, statSlotIndex), 0);
        }

        if (command.RolledAttributeGarageSlot is { } rolledGarageSlot &&
            command.RolledAttributeNewPower is { } rolledNewPower)
        {
            garageChanged = true;
            var baseIndex = rolledGarageSlot * MountStateResolver.StatSlotCount;
            var rolled = state.MountRolledAttributes;
            for (var statSlotIndex = 0; statSlotIndex < MountStateResolver.StatSlotCount; statSlotIndex++)
                rolled = rolled.SetItem(baseIndex + statSlotIndex,
                    MountPowerCodec.DigitAtPlace(rolledNewPower, MountPowerCodec.DigitCount - 1 - statSlotIndex));
            state.MountRolledAttributes = rolled;
            state.MountRolledAttributeTotal =
                state.MountRolledAttributeTotal.SetItem(rolledGarageSlot, MountPowerCodec.DigitSum(rolledNewPower));
        }

        if (command.ResetAccumulatedExpGarageSlot is { } expResetSlot)
        {
            garageChanged = true;
            state.MountAccumulatedExp = state.MountAccumulatedExp.SetItem(expResetSlot, 0);
        }

        if (command.MountExpSlot is { } expSlot && command.MountExpNewValue is { } expNewValue)
        {
            garageChanged = true;
            var activity = MountActivityExpCodec.ClampActivity(state.MountActivity[expSlot]);
            var experience = MountActivityExpCodec.ClampExp(expNewValue);
            state.MountActivity = state.MountActivity.SetItem(expSlot, activity);
            state.MountAccumulatedExp = state.MountAccumulatedExp.SetItem(expSlot, experience);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = MountActivityExpStatSort,
                Value = MountActivityExpCodec.Pack(activity, experience),
                Value2 = 0
            });
        }

        if (command.Broadcast is MountBroadcastKind.Mount or MountBroadcastKind.Dismount)
        {
            if (state.AnimalAbsorbState != 0)
            {
                state.AnimalAbsorbState = 0;
                changed = true;
            }

            if (command.Broadcast == MountBroadcastKind.Dismount && state.AnimalNumber != 0)
            {
                state.AnimalNumber = 0;
                changed = true;
            }
        }

        var mountDirtyFlags = (changed ? DirtyFlags.Vitals : DirtyFlags.None) |
                              (garageChanged ? DirtyFlags.Progression : DirtyFlags.None);

        switch (command.Broadcast)
        {
            case MountBroadcastKind.Mount:
                state.MountExpiryCountdownAccrualTicks = 0;
                state.MountActivityDecayAccrualTicks = 0;
                RecomputeAndPublish(state);
                BroadcastAvatarStateFlag(state, 12, state.AnimalNumber, 0, 0);
                PublishMountAbsorptionCleared(state);
                break;
            case MountBroadcastKind.Dismount:
                RecomputeAndPublish(state);
                PublishMountAbsorptionCleared(state);
                BroadcastAvatarStateFlag(state, 13, 0, 0, 0);
                break;
            case MountBroadcastKind.AbsorbToggle:
                RecomputeAndPublish(state);
                BroadcastAvatarStateFlag(state, 26, state.AnimalAbsorbState, 0, 0);
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = 79, Value = state.AnimalAbsorbState, Value2 = 0 });
                BroadcastMountAbsorbAura(state, state.AnimalAbsorbState);
                break;
        }

        if (mountDirtyFlags != DirtyFlags.None)
            state.MarkProgressDirty(dirtyTracker, mountDirtyFlags);
    }

    private void ApplyPetLifecycleTransition(PlayerRuntimeState state, in PetLifecycleTransition transition)
    {
        if (transition.Effects == PetLifecycleEffect.None)
            return;

        state.PetGrowth = transition.NewGrowth;
        state.PetActivity = transition.NewActivity;

        if ((transition.Effects & PetLifecycleEffect.SynchronizeEquippedItem) != 0)
            PetItemState.SynchronizeEquippedState(state.Inventory, state.PetGrowth, state.PetActivity);

        if ((transition.Effects & PetLifecycleEffect.MarkProgressionDirty) != 0)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        if (transition.RequiresStatRecalculation)
            RecomputeAndPublish(state);

        if ((transition.Effects & PetLifecycleEffect.SendActivityResponse) != 0)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = PetActivityStatSort, Value = transition.NewActivity, Value2 = 0 });

        if (transition.RequiresGrowthTierBroadcast)
            BroadcastAvatarStateFlag(state, PetGrowStepAvatarChangeInfoSort, 0, 0, 0);
    }

    private void ApplyMountExperienceCredit(PlayerRuntimeState state, int garageSlot,
        in MountExperienceCreditResult credit)
    {
        if (!credit.IsApplied)
            return;

        state.MountAccumulatedExp = state.MountAccumulatedExp.SetItem(garageSlot, credit.NewExperience);

        if ((credit.Effects & MountLifecycleEffect.MarkProgressionDirty) != 0)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        if ((credit.Effects & MountLifecycleEffect.SendActivityExperienceResponse) != 0)
        {
            var activity = state.MountActivity[garageSlot];
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = MountActivityExpStatSort,
                Value = MountActivityExpCodec.Pack(activity, credit.NewExperience),
                Value2 = 0
            });
        }

        if (credit.RequiresStatRecalculation)
            RecomputeAndPublish(state);
    }

    internal void AdvanceMountExpiry(PlayerRuntimeState state, int minutesElapsed)
    {
        for (var elapsedMinute = 0; elapsedMinute < minutesElapsed; elapsedMinute++)
        {
            if (!MountStateResolver.TryResolveActiveMountedMount(state.AnimalIndex, state.AnimalNumber,
                    state.MountGarage, out var garageSlot))
                return;

            var wasAbsorbing = state.AnimalAbsorbState != 0;
            var transition = MountExpiryPolicy.AdvanceMinute(new MountMinuteState(
                state.AnimalIndex,
                state.AnimalNumber,
                state.AnimalTime,
                state.AnimalAbsorbState,
                state.AnimalAbsorbTime));

            state.AnimalIndex = transition.AnimalIndex;
            state.AnimalNumber = transition.AnimalNumber;
            state.AnimalTime = transition.RideTime;
            state.AnimalAbsorbState = transition.AbsorbState;
            state.AnimalAbsorbTime = transition.AbsorbTime;

            if (wasAbsorbing)
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = MountAbsorbTimeStatSort, Value = state.AnimalAbsorbTime, Value2 = 0 });

            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = MountRideTimeStatSort, Value = state.AnimalTime, Value2 = 0 });

            if (transition.RideExpired)
                state.MountAutoDismountPending = true;

            if (transition.AbsorptionExpired || transition.RideExpired)
                RecomputeAndPublish(state);

            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals | DirtyFlags.Progression);

            if (transition.AbsorptionExpired || transition.RideExpired)
                PublishMountAbsorptionCleared(state);

            if (transition.RideExpired)
                BroadcastAvatarStateFlag(state, 13, 0, 0, 0);

            if (transition.RideExpired)
                return;
        }
    }

    internal void AdvanceMountActivity(PlayerRuntimeState state, int activityPulses)
    {
        for (var pulse = 0; pulse < activityPulses; pulse++)
        {
            if (!MountStateResolver.TryResolveActiveMountedMount(state.AnimalIndex, state.AnimalNumber,
                    state.MountGarage, out var garageSlot))
                return;

            var transition = MountExpiryPolicy.AdvanceThirtySeconds(new MountActivityState(
                state.AnimalIndex,
                state.AnimalNumber,
                state.MountActivity[garageSlot],
                state.AnimalDoubleExp > 0));

            if (!transition.ActivityChanged)
                continue;

            state.MountActivity = state.MountActivity.SetItem(garageSlot, transition.Activity);
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = MountActivityExpStatSort,
                Value = MountActivityExpCodec.Pack(transition.Activity, state.MountAccumulatedExp[garageSlot]),
                Value2 = 0
            });
            RecomputeAndPublish(state);
        }
    }

    private void PublishMountAbsorptionCleared(PlayerRuntimeState state)
    {
        BroadcastAvatarStateFlag(state, 26, 0, 0, 0);
        state.Session.Send(new AvatarStatUpdateResponse { Sort = MountAbsorbStateStatSort, Value = 0, Value2 = 0 });
        BroadcastMountAbsorbAura(state, 0);
    }

    private void BroadcastAvatarStateFlag(PlayerRuntimeState state, int sort, int value01, int value02, int value03)
    {
        var response = new AvatarStateFlagResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Sort = sort,
            Value01 = value01,
            Value02 = value02,
            Value03 = value03
        };

        var total = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendAvatarStateFlagFrame(state.CharacterId, span);
            _avatarStateFlagNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_avatarStateFlagNeighborScratch, state.CurrentCell, state.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            foreach (var neighborId in _avatarStateFlagNeighborScratch)
                SendAvatarStateFlagFrame(neighborId, span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void BroadcastMountAbsorbAura(PlayerRuntimeState state, int absorbState)
    {
        var data = new byte[MountAbsorbAuraDataLength];
        LegacyWireCodec.WriteFixedString(data.AsSpan(0, AvatarNameLength), state.Name);

        var response = new GenericActionResponse
        {
            Result = absorbState,
            Sort = MountAbsorbAuraSort,
            Data = data,
            RuneValue = 0
        };

        var total = FrameWriter.FrameSizeOf<GenericActionResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendMountAbsorbAuraFrame(state.CharacterId, span);
            _avatarStateFlagNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_avatarStateFlagNeighborScratch, state.CurrentCell, state.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            foreach (var neighborId in _avatarStateFlagNeighborScratch)
                SendMountAbsorbAuraFrame(neighborId, span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void SendMountAbsorbAuraFrame(int recipientId, ReadOnlySpan<byte> frame)
    {
        try
        {
            if (TryGetBroadcastRecipient(recipientId, out _, out var clientSession))
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} mount-absorb-aura broadcast to character {RecipientId} failed", MapId,
                recipientId);
        }
    }

    private void SendAvatarStateFlagFrame(int recipientId, ReadOnlySpan<byte> frame)
    {
        try
        {
            if (TryGetBroadcastRecipient(recipientId, out _, out var clientSession))
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} avatar-state-flag broadcast to character {RecipientId} failed", MapId,
                recipientId);
        }
    }

    private void DrainCostumeCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _costumeInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyCostumeCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} costume command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyCostumeCommand(in CostumeZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;
        var wardrobeChanged = false;

        if (command.CostumeIndex is { } costumeIndex)
        {
            state.CostumeIndex = costumeIndex;
            wardrobeChanged = true;
        }

        if (command.CostumeNumber is { } costumeNumber)
        {
            state.CostumeNumber = costumeNumber;
            changed = true;
        }

        if (command.CostumeState is { } costumeState)
            state.CostumeState = costumeState;

        if (command.WardrobeSlotCleared is { } clearedSlot)
        {
            wardrobeChanged = true;
            state.CostumeWardrobe = CompactCosmeticSlots(state.CostumeWardrobe, clearedSlot);
            state.CostumeDate = CompactCosmeticSlots(state.CostumeDate, clearedSlot);
            state.CostumeExpireDate = CompactCosmeticSlots(state.CostumeExpireDate, clearedSlot);
        }

        if (command.WardrobeSlotGranted is { } grantedSlot)
        {
            wardrobeChanged = true;
            state.CostumeWardrobe = state.CostumeWardrobe.SetItem(grantedSlot, command.GrantedItemId ?? 0);
            state.CostumeDate = state.CostumeDate.SetItem(grantedSlot, command.GrantedCostumeDate ?? 0);
            state.CostumeExpireDate = state.CostumeExpireDate.SetItem(grantedSlot, command.GrantedExpireDate ?? 0);
        }

        if (command.NewHeadType is { } newHeadType)
            state.HeadType = newHeadType;

        if (command.NewFaceType is { } newFaceType)
            state.FaceType = newFaceType;

        if (command.NewGender is { } newGender)
            state.Gender = newGender;

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        var costumeDirtyFlags = (changed ? DirtyFlags.Vitals : DirtyFlags.None) |
                                (wardrobeChanged ? DirtyFlags.Progression : DirtyFlags.None);
        if (costumeDirtyFlags != DirtyFlags.None)
            state.MarkProgressDirty(dirtyTracker, costumeDirtyFlags);

        switch (command.Broadcast)
        {
            case CostumeBroadcastKind.Equip:
                RecomputeAndPublish(state);
                BroadcastAvatarStateFlag(state, 16, state.CostumeNumber, 0, 0);
                break;
            case CostumeBroadcastKind.Remove:
                RecomputeAndPublish(state);
                BroadcastAvatarStateFlag(state, 17, 0, 0, 0);
                break;
        }

        if (!command.FullActionRebroadcast) return;
        var characterId = command.CharacterId;
        SendAvatarAction(state.Session, state);
        _costumeFullActionNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_costumeFullActionNeighborScratch, state.CurrentCell, characterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_costumeFullActionNeighborScratch, state);
    }

    private void DrainStellarCoreCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _stellarCoreInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyStellarCoreCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} stellar-core command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyStellarCoreCommand(in StellarCoreZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;
        var wardrobeChanged = false;

        if (command.CoreIndex is { } coreIndex)
        {
            state.StellarCoreIndex = coreIndex;
            wardrobeChanged = true;
        }

        if (command.CoreNumber is { } coreNumber)
        {
            state.StellarCoreNumber = coreNumber;
            changed = true;
        }

        if (command.WardrobeSlotCleared is { } clearedSlot)
        {
            wardrobeChanged = true;
            state.StellarCoreWardrobe = CompactCosmeticSlots(state.StellarCoreWardrobe, clearedSlot);
            state.StellarCoreExpireDate = CompactCosmeticSlots(state.StellarCoreExpireDate, clearedSlot);
        }

        if (command.WardrobeSlotGranted is { } grantedSlot)
        {
            wardrobeChanged = true;
            state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(grantedSlot, command.GrantedItemId ?? 0);
            state.StellarCoreExpireDate =
                state.StellarCoreExpireDate.SetItem(grantedSlot, command.GrantedExpireDate ?? 0);
        }

        if (command.Life is { } life)
        {
            state.Life = life;
            changed = true;
        }

        if (command.Mana is { } mana)
        {
            state.Mana = mana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        var stellarDirtyFlags = (changed ? DirtyFlags.Vitals : DirtyFlags.None) |
                                (wardrobeChanged ? DirtyFlags.Progression : DirtyFlags.None);
        if (stellarDirtyFlags != DirtyFlags.None)
            state.MarkProgressDirty(dirtyTracker, stellarDirtyFlags);

        switch (command.Broadcast)
        {
            case StellarCoreBroadcastKind.Equip:
                RecomputeAndPublish(state);
                BroadcastAvatarStateFlag(state, 37, state.StellarCoreNumber, 0, 0);
                break;
            case StellarCoreBroadcastKind.Remove:
                RecomputeAndPublish(state);
                BroadcastAvatarStateFlag(state, 38, 0, 0, 0);
                break;
        }
    }

    private static ImmutableArray<int> CompactCosmeticSlots(ImmutableArray<int> slots, int clearedSlot)
    {
        if (slots.IsDefaultOrEmpty || clearedSlot < 0 || clearedSlot >= slots.Length)
            return slots;

        var builder = slots.ToBuilder();
        for (var i = clearedSlot; i < builder.Count - 1; i++)
            builder[i] = builder[i + 1];
        builder[^1] = 0;
        return builder.MoveToImmutable();
    }

    private void DrainAvatarBuffCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _avatarBuffInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyAvatarBuffCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} avatar-buff command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyAvatarBuffCommand(in AvatarBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.StateTimeEffect is { } stateTimeEffect)
            state.StateTimeEffect = stateTimeEffect;

        if (command.RankBuffType is { } rankBuffType)
        {
            state.RankBuffType = rankBuffType;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (command.PlayTime2 is { } playTime2)
            state.PlayTime2 = playTime2;

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (command.RecomputeAndClampVitals)
            RecomputeAndPublish(state);
    }

    private void DrainRuneSocketCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _runeInbox.Reader.TryRead(out var command); processed++)
            try
            {
                var result = ApplyRuneSocketCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Rune-socket command could not be applied.");
                command.Applied?.TrySetResult(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} rune-socket command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplyRuneSocketCommand(in RuneSocketZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        state.RuneSystem = state.RuneSystem.SetItem(command.RuneIndex, command.RuneItemId ?? 0);
        state.RuneSystemStat = state.RuneSystemStat.SetItem(command.RuneIndex, command.RuneStat ?? 0);

        RecomputeAndPublish(state);
        return true;
    }

    private void DrainAutoBuffCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _autoBuffInbox.Reader.TryRead(out var command); processed++)
            try
            {
                command.Applied?.TrySetResult(ApplyAutoBuffCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Auto-buff command could not be applied."));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} auto-buff command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplyAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        if ((command.ApplyRegisteredBuffs || command.ActivateAutoBuff) && !state.CanIssueGameplayActions)
            return false;

        if (command.RegisteredSkills is { } registered)
        {
            state.AutoBuffSkill = registered;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (command.NewAutoHuntConfig is { } newAutoHuntConfig)
        {
            state.AutoHuntConfig = newAutoHuntConfig;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (command.DisableAutoHunt)
        {
            state.AutoHuntEnabled = false;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (command.ApplyRegisteredBuffs)
            ApplyRegisteredAutoBuffs(state);

        if (command.ActivateAutoBuff)
        {
            var newMana = AutoBuffActivationResolver.ManaAfterActivation(state.Mana);
            if (newMana != state.Mana)
            {
                state.Mana = newMana;
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = 11, Value = newMana, Value2 = 0 });
            }

            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        if (!command.Broadcast)
            return true;

        if (command.ActionSort is { } sort)
            state.ActionSort = sort;

        state.ActionSkillNumber = 0;
        state.ActionSkillGradeNum1 = 0;
        state.ActionSkillGradeNum2 = 0;

        var autoBuffPet = PetActionFieldsOf(state);
        var action = new ActionInfo
        {
            Type = 0,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = autoBuffPet.PetLocation,
            PetTargetLocation = autoBuffPet.PetTargetLocation,
            PetFront = autoBuffPet.PetFront,
            PetSort = autoBuffPet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var casterId = command.CharacterId;
        _autoBuffNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_autoBuffNeighborScratch, state.CurrentCell, casterId, state.PosX, state.PosY,
            state.PosZ);
        _autoBuffNeighborScratch.Add(casterId);
        BroadcastAvatarAction(_autoBuffNeighborScratch, state, action);
        return true;
    }

    private void DrainPshopCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _pshopInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyPshopCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} pshop command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyPshopCommand(in PshopZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.OpenListing is { } openListing)
        {
            state.PshopOpen = true;
            state.PshopListing = openListing;

            if (command.DisableAutoHunt && state.AutoHuntEnabled)
            {
                state.AutoHuntEnabled = false;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                state.Session.Send(new AutoHuntToggleResponse
                {
                    ServerIndex = state.CharacterId,
                    UniqueNumber = state.UniqueNumber,
                    AutoState = 0
                });
            }
        }

        if (command.Page is { } page && command.Slot is { } slot && state.PshopListing is { } listing)
        {
            var itemInfo = listing.ItemInfo;
            var baseIndex = (page * 5 + slot) * 9;
            for (var k = 0; k < 9; k++)
                itemInfo[baseIndex + k] = 0;

            if (command.SellerSoldNotification is { } soldNotification)
                state.Session.Send(soldNotification);
        }

        if (command.SendSellerListingRefresh && state.PshopListing is { } refreshedListing)
            state.Session.Send(new ViewShopStallResponse { Result = 3, PshopInfo = refreshedListing });

        if (command.BroadcastOpenAction)
        {
            var openingCharacterId = command.CharacterId;
            SendAvatarAction(state.Session, state);
            _pshopCloseFullActionNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_pshopCloseFullActionNeighborScratch, state.CurrentCell, openingCharacterId,
                state.PosX, state.PosY, state.PosZ);
            BroadcastAvatarAction(_pshopCloseFullActionNeighborScratch, state);
        }

        if (!command.CloseShop)
            return;

        state.PshopOpen = false;
        state.Session.Send(new CloseShopStallResponse { Result = 1 });

        var characterId = command.CharacterId;
        SendAvatarAction(state.Session, state);
        _pshopCloseFullActionNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_pshopCloseFullActionNeighborScratch, state.CurrentCell, characterId,
            state.PosX, state.PosY, state.PosZ);
        BroadcastAvatarAction(_pshopCloseFullActionNeighborScratch, state);
    }
}

public readonly record struct DrinkBottleZoneCommand(
    int CharacterId,
    int BottleIndex,
    int RemainingCount,
    int NewLife,
    int? NewItemId = null,
    EffectiveStats? UpdatedStats = null,
    int? DrunkTicksRemaining = null,
    int? DrunkActiveIndex = null,
    TaskCompletionSource? Applied = null);

public readonly record struct HotkeySlotMirrorZoneCommand(
    int CharacterId,
    byte Page,
    byte Index,
    HotkeySlot NewSlot,
    int? LifeGain = null,
    int? ManaGain = null,
    ImmutableArray<SkillCastResolver.BuffWrite> BuffWrites = default,
    HotkeyItemConsumptionResolver.AntiCheatMarkerKind MarkerKind =
        HotkeyItemConsumptionResolver.AntiCheatMarkerKind.None,
    int MarkerValue = 0,
    bool RecomputeStats = true,
    int? PetActivityGain = null,
    int? MountActivityGain = null);

public readonly record struct HeroRankingQueryZoneCommand(int CharacterId, bool Previous, TimeSpan QueriedAtZoneClock);

public readonly record struct HeroRankingRolloverZoneCommand;

public readonly record struct FishingZoneCommand(
    int CharacterId,
    int NewFishingState,
    int NewFishingStep,
    bool CatchingFish,
    bool Broadcast,
    int? ActionSort,
    DateTime? CastAtUtc = null,
    TaskCompletionSource<ZoneCommandResult>? Applied = null,
    bool BiteWasHit = false,
    bool ApplyState = true);

public enum MountBroadcastKind : byte
{
    None,
    Mount,
    Dismount,
    AbsorbToggle
}

public readonly record struct MountZoneCommand(
    int CharacterId,
    int? AnimalIndex = null,
    int? AnimalNumber = null,
    int? AnimalAbsorbState = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    MountBroadcastKind Broadcast = MountBroadcastKind.None,
    int? DeleteGarageSlot = null,
    int? RolledAttributeGarageSlot = null,
    int? RolledAttributeNewPower = null,
    int? ResetAccumulatedExpGarageSlot = null,
    int? MountExpSlot = null,
    int? MountExpNewValue = null,
    TaskCompletionSource? Applied = null);

public enum CostumeBroadcastKind : byte
{
    None,
    Equip,
    Remove
}

public readonly record struct CostumeZoneCommand(
    int CharacterId,
    int? CostumeIndex = null,
    int? CostumeNumber = null,
    int? CostumeState = null,
    int? WardrobeSlotCleared = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    CostumeBroadcastKind Broadcast = CostumeBroadcastKind.None,
    bool FullActionRebroadcast = false,
    int? WardrobeSlotGranted = null,
    int? GrantedItemId = null,
    int? GrantedCostumeDate = null,
    int? GrantedExpireDate = null,
    byte? NewHeadType = null,
    byte? NewFaceType = null,
    byte? NewGender = null,
    TaskCompletionSource? Applied = null);

public enum StellarCoreBroadcastKind : byte
{
    None,
    Equip,
    Remove
}

public readonly record struct StellarCoreZoneCommand(
    int CharacterId,
    int? CoreIndex = null,
    int? CoreNumber = null,
    int? WardrobeSlotCleared = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    StellarCoreBroadcastKind Broadcast = StellarCoreBroadcastKind.None,
    int? WardrobeSlotGranted = null,
    int? GrantedItemId = null,
    int? GrantedExpireDate = null,
    TaskCompletionSource? Applied = null);

public readonly record struct AvatarBuffZoneCommand(
    int CharacterId,
    int? StateTimeEffect = null,
    int? RankBuffType = null,
    bool RecomputeAndClampVitals = false,
    EffectiveStats? UpdatedStats = null,
    int? PlayTime2 = null,
    TaskCompletionSource? Applied = null);

public readonly record struct RuneSocketZoneCommand(
    int CharacterId,
    int RuneIndex,
    int? RuneItemId,
    int? RuneStat,
    TaskCompletionSource<ZoneCommandResult>? Applied = null);

public readonly record struct AutoBuffZoneCommand(
    int CharacterId,
    ImmutableArray<(int SkillId, int Grade)>? RegisteredSkills = null,
    bool ActivateAutoBuff = false,
    bool ApplyRegisteredBuffs = false,
    int? ActionSort = null,
    bool Broadcast = false,
    AutoHunt? NewAutoHuntConfig = null,
    bool DisableAutoHunt = false,
    TaskCompletionSource<ZoneCommandResult>? Applied = null);
