using System.Buffers;
using System.Collections.Immutable;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int HeroRankPointStatSort = 904;

    private const int AutoBuffInboxCapacity = 256;

    private const int AutoBuffInboxDrainCapPerTick = AutoBuffInboxCapacity / 2;

    private const int AvatarBuffInboxCapacity = 256;

    private const int AvatarBuffInboxDrainCapPerTick = AvatarBuffInboxCapacity / 2;

    private const int BottleInboxCapacity = 256;

    private const int BottleInboxDrainCapPerTick = BottleInboxCapacity / 2;

    private const int HotkeySlotInboxCapacity = 256;

    private const int HotkeySlotInboxDrainCapPerTick = HotkeySlotInboxCapacity / 2;

    private const int CostumeInboxCapacity = 256;

    private const int CostumeInboxDrainCapPerTick = CostumeInboxCapacity / 2;

    private const int FishingInboxCapacity = 512;

    private const int FishingInboxDrainCapPerTick = FishingInboxCapacity / 2;

    private const int HeroRankingInboxCapacity = 256;

    private const int HeroRankingInboxDrainCapPerTick = HeroRankingInboxCapacity / 2;

    private const int HeroRankingRolloverInboxCapacity = 4;

    private const int HeroRankingRolloverInboxDrainCapPerTick = HeroRankingRolloverInboxCapacity / 2;

    private const int MountInboxCapacity = 256;

    private const int MountInboxDrainCapPerTick = MountInboxCapacity / 2;

    private const int PshopInboxCapacity = 256;

    private const int PshopInboxDrainCapPerTick = PshopInboxCapacity / 2;

    private const int RuneInboxCapacity = 256;

    private const int RuneInboxDrainCapPerTick = RuneInboxCapacity / 2;

    private const int StellarCoreInboxCapacity = 256;

    private const int StellarCoreInboxDrainCapPerTick = StellarCoreInboxCapacity / 2;

    private const int CharacterMpStatSort = 11;

    private readonly Channel<AutoBuffZoneCommand> _autoBuffInbox =
        Channel.CreateBounded<AutoBuffZoneCommand>(
            new BoundedChannelOptions(AutoBuffInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

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
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

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
        return _fishingInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostFishingCommandAndWaitAsync(FishingZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostFishingCommand(in withSignal))
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
        return _runeInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostRuneSocketCommandAndWaitAsync(RuneSocketZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostRuneSocketCommand(in withSignal))
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

    public bool PostAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        return _autoBuffInbox.Writer.TryWrite(command);
    }

    private void DrainDrinkBottleCommands()
    {
        var processed = 0;
        while (processed < BottleInboxDrainCapPerTick && _bottleInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= BottleInboxDrainCapPerTick)
            LogDrainCapEngaged(_bottleInbox.Reader, "drink-bottle", BottleInboxDrainCapPerTick);
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

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    private void DrainHotkeySlotMirrorCommands()
    {
        var processed = 0;
        while (processed < HotkeySlotInboxDrainCapPerTick && _hotkeySlotInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= HotkeySlotInboxDrainCapPerTick)
            LogDrainCapEngaged(_hotkeySlotInbox.Reader, "hotkey-slot", HotkeySlotInboxDrainCapPerTick);
    }

    private void ApplyHotkeySlotMirrorCommand(in HotkeySlotMirrorZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.SetHotkeySlot(command.Page, command.Index, command.NewSlot);

        if (!command.BuffWrites.IsDefaultOrEmpty)
            ApplyBuffWrites(state, command.BuffWrites);

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

    private void DrainHeroRankingQueryCommands()
    {
        var processed = 0;
        while (processed < HeroRankingInboxDrainCapPerTick && _heroRankingInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= HeroRankingInboxDrainCapPerTick)
            LogDrainCapEngaged(_heroRankingInbox.Reader, "hero-ranking-query", HeroRankingInboxDrainCapPerTick);
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

    private void DrainHeroRankingRolloverCommands()
    {
        var processed = 0;
        while (processed < HeroRankingRolloverInboxDrainCapPerTick &&
               _heroRankingRolloverInbox.Reader.TryRead(out _))
        {
            processed++;
            try
            {
                ApplyHeroRankingRolloverReset();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hero-ranking rollover reset failed", MapId);
            }
        }

        if (processed >= HeroRankingRolloverInboxDrainCapPerTick)
            LogDrainCapEngaged(_heroRankingRolloverInbox.Reader, "hero-ranking-rollover",
                HeroRankingRolloverInboxDrainCapPerTick);
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

    private void DrainFishingCommands()
    {
        var processed = 0;
        while (processed < FishingInboxDrainCapPerTick && _fishingInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyFishingCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} fishing command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= FishingInboxDrainCapPerTick)
            LogDrainCapEngaged(_fishingInbox.Reader, "fishing", FishingInboxDrainCapPerTick);
    }

    private void ApplyFishingCommand(in FishingZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.FishingState = command.NewFishingState;
        state.FishingStep = command.NewFishingStep;
        state.CatchingFish = command.CatchingFish;

        if (command.CastAtUtc is { } castAt)
            state.FishingCastAtUtc = castAt;

        if (!command.Broadcast)
            return;

        if (command.ActionSort is { } sort)
            state.ActionSort = sort;

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
    }

    private void DrainMountCommands()
    {
        var processed = 0;
        while (processed < MountInboxDrainCapPerTick && _mountInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= MountInboxDrainCapPerTick)
            LogDrainCapEngaged(_mountInbox.Reader, "mount", MountInboxDrainCapPerTick);
    }

    private void ApplyMountCommand(in MountZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var wasAbsorbed = state.AnimalAbsorbState != 0;
        var changed = false;

        if (command.AnimalIndex is { } animalIndex)
            state.AnimalIndex = animalIndex;

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
            state.MountGarage = state.MountGarage.SetItem(deleteGarageSlot, 0);
            state.MountAccumulatedExp = state.MountAccumulatedExp.SetItem(deleteGarageSlot, 0);
            state.MountRolledAttributeTotal = state.MountRolledAttributeTotal.SetItem(deleteGarageSlot, 0);
            for (var statSlotIndex = 0; statSlotIndex < MountStateResolver.StatSlotCount; statSlotIndex++)
                state.MountRolledAttributes = state.MountRolledAttributes.SetItem(
                    MountStateResolver.AttributeIndex(deleteGarageSlot, statSlotIndex), 0);
        }

        if (command.AttributeDeleteGarageSlot is { } attributeGarageSlot &&
            command.AttributeDeleteStatSlotIndex is { } attributeStatSlotIndex)
            state.MountRolledAttributes = state.MountRolledAttributes.SetItem(
                MountStateResolver.AttributeIndex(attributeGarageSlot, attributeStatSlotIndex), 0);

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        switch (command.Broadcast)
        {
            case MountBroadcastKind.Mount:
                BroadcastAvatarStateFlag(state, 12, state.AnimalNumber, 0, 0);
                BroadcastAvatarStateFlag(state, 26, 0, 0, 0);
                break;
            case MountBroadcastKind.Dismount:
                if (wasAbsorbed)
                    state.Session.Send(new AvatarStatUpdateResponse { Sort = 79, Value = 0, Value2 = 0 });
                BroadcastAvatarStateFlag(state, 13, 0, 0, 0);
                break;
            case MountBroadcastKind.AbsorbToggle:
                BroadcastAvatarStateFlag(state, 26, state.AnimalAbsorbState, 0, 0);
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = 79, Value = state.AnimalAbsorbState, Value2 = 0 });
                break;
        }
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

    private void SendAvatarStateFlagFrame(int recipientId, ReadOnlySpan<byte> frame)
    {
        try
        {
            if (_players.TryGetValue(recipientId, out var recipient) &&
                recipient.Session is ClientSession clientSession)
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} avatar-state-flag broadcast to character {RecipientId} failed", MapId,
                recipientId);
        }
    }

    private void DrainCostumeCommands()
    {
        var processed = 0;
        while (processed < CostumeInboxDrainCapPerTick && _costumeInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= CostumeInboxDrainCapPerTick)
            LogDrainCapEngaged(_costumeInbox.Reader, "costume", CostumeInboxDrainCapPerTick);
    }

    private void ApplyCostumeCommand(in CostumeZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.CostumeIndex is { } costumeIndex)
            state.CostumeIndex = costumeIndex;

        if (command.CostumeNumber is { } costumeNumber)
        {
            state.CostumeNumber = costumeNumber;
            changed = true;
        }

        if (command.CostumeState is { } costumeState)
            state.CostumeState = costumeState;

        if (command.WardrobeSlotCleared is { } clearedSlot)
            state.CostumeWardrobe = state.CostumeWardrobe.SetItem(clearedSlot, 0);

        if (command.WardrobeSlotGranted is { } grantedSlot)
        {
            state.CostumeWardrobe = state.CostumeWardrobe.SetItem(grantedSlot, command.GrantedItemId ?? 0);
            state.CostumeDate = state.CostumeDate.SetItem(grantedSlot, command.GrantedCostumeDate ?? 0);
            state.CostumeExpireDate = state.CostumeExpireDate.SetItem(grantedSlot, command.GrantedExpireDate ?? 0);
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

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        switch (command.Broadcast)
        {
            case CostumeBroadcastKind.Equip:
                BroadcastAvatarStateFlag(state, 16, state.CostumeNumber, 0, 0);
                break;
            case CostumeBroadcastKind.Remove:
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

    private void DrainStellarCoreCommands()
    {
        var processed = 0;
        while (processed < StellarCoreInboxDrainCapPerTick && _stellarCoreInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= StellarCoreInboxDrainCapPerTick)
            LogDrainCapEngaged(_stellarCoreInbox.Reader, "stellar-core", StellarCoreInboxDrainCapPerTick);
    }

    private void ApplyStellarCoreCommand(in StellarCoreZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.CoreIndex is { } coreIndex)
            state.StellarCoreIndex = coreIndex;

        if (command.CoreNumber is { } coreNumber)
        {
            state.StellarCoreNumber = coreNumber;
            changed = true;
        }

        if (command.WardrobeSlotCleared is { } clearedSlot)
            state.StellarCoreWardrobe = CompactStellarCoreWardrobe(state.StellarCoreWardrobe, clearedSlot);

        if (command.WardrobeSlotGranted is { } grantedSlot)
        {
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

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        switch (command.Broadcast)
        {
            case StellarCoreBroadcastKind.Equip:
                BroadcastAvatarStateFlag(state, 37, state.StellarCoreNumber, 0, 0);
                break;
            case StellarCoreBroadcastKind.Remove:
                BroadcastAvatarStateFlag(state, 38, 0, 0, 0);
                break;
        }
    }

    private static ImmutableArray<int> CompactStellarCoreWardrobe(ImmutableArray<int> wardrobe, int clearedSlot)
    {
        var builder = wardrobe.ToBuilder();
        for (var i = clearedSlot; i < builder.Count - 1; i++)
            builder[i] = builder[i + 1];
        builder[^1] = 0;
        return builder.MoveToImmutable();
    }

    private void DrainAvatarBuffCommands()
    {
        var processed = 0;
        while (processed < AvatarBuffInboxDrainCapPerTick && _avatarBuffInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= AvatarBuffInboxDrainCapPerTick)
            LogDrainCapEngaged(_avatarBuffInbox.Reader, "avatar-buff", AvatarBuffInboxDrainCapPerTick);
    }

    private void ApplyAvatarBuffCommand(in AvatarBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.StateTimeEffect is { } stateTimeEffect)
            state.StateTimeEffect = stateTimeEffect;

        if (command.RankBuffType is { } rankBuffType)
            state.RankBuffType = rankBuffType;

        if (command.PlayTime2 is { } playTime2)
            state.PlayTime2 = playTime2;

        if (command.HealToMax)
        {
            state.Life = state.Stats?.MaxLife ?? state.MaxLife;
            state.Mana = state.Stats?.MaxMana ?? state.MaxMana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    private void DrainRuneSocketCommands()
    {
        var processed = 0;
        while (processed < RuneInboxDrainCapPerTick && _runeInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyRuneSocketCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} rune-socket command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= RuneInboxDrainCapPerTick)
            LogDrainCapEngaged(_runeInbox.Reader, "rune-socket", RuneInboxDrainCapPerTick);
    }

    private void ApplyRuneSocketCommand(in RuneSocketZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.RuneSystem = state.RuneSystem.SetItem(command.RuneIndex, command.RuneItemId ?? 0);
        state.RuneSystemStat = state.RuneSystemStat.SetItem(command.RuneIndex, command.RuneStat ?? 0);

        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        state.Stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;
    }

    private void DrainAutoBuffCommands()
    {
        var processed = 0;
        while (processed < AutoBuffInboxDrainCapPerTick && _autoBuffInbox.Reader.TryRead(out var command))
        {
            processed++;
            try
            {
                ApplyAutoBuffCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} auto-buff command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= AutoBuffInboxDrainCapPerTick)
            LogDrainCapEngaged(_autoBuffInbox.Reader, "auto-buff", AutoBuffInboxDrainCapPerTick);
    }

    private void ApplyAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.RegisteredSkills is { } registered)
            state.AutoBuffSkill = registered;

        if (command.ActivateAutoBuff)
        {
            state.Mana = AutoBuffActivationResolver.ManaAfterActivation(state.Mana);
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        if (!command.Broadcast)
            return;

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
    }

    private void DrainPshopCommands()
    {
        var processed = 0;
        while (processed < PshopInboxDrainCapPerTick && _pshopInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= PshopInboxDrainCapPerTick)
            LogDrainCapEngaged(_pshopInbox.Reader, "pshop", PshopInboxDrainCapPerTick);
    }

    private void ApplyPshopCommand(in PshopZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state) || state.PshopListing is not { } listing)
            return;

        var itemInfo = listing.ItemInfo;
        var baseIndex = (command.Page * 5 + command.Slot) * 9;
        for (var k = 0; k < 9; k++)
            itemInfo[baseIndex + k] = 0;

        state.Session.Send(command.SellerSoldNotification);

        state.Session.Send(new ViewShopStallResponse { Result = 3, PshopInfo = listing });

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
    TaskCompletionSource? Applied = null);

public readonly record struct HotkeySlotMirrorZoneCommand(
    int CharacterId,
    byte Page,
    byte Index,
    HotkeySlot NewSlot,
    int? LifeGain = null,
    int? ManaGain = null,
    ImmutableArray<SkillCastResolver.BuffWrite> BuffWrites = default);

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
    TaskCompletionSource? Applied = null);

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
    int? AttributeDeleteGarageSlot = null,
    int? AttributeDeleteStatSlotIndex = null,
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
    bool HealToMax = false,
    EffectiveStats? UpdatedStats = null,
    int? PlayTime2 = null,
    TaskCompletionSource? Applied = null);

public readonly record struct RuneSocketZoneCommand(
    int CharacterId,
    int RuneIndex,
    int? RuneItemId,
    int? RuneStat,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null);

public readonly record struct AutoBuffZoneCommand(
    int CharacterId,
    ImmutableArray<(int SkillId, int Grade)>? RegisteredSkills = null,
    bool ActivateAutoBuff = false,
    int? ActionSort = null,
    bool Broadcast = false,
    TaskCompletionSource? Applied = null);
