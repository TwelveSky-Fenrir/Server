using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int GuildInboxCapacity = 512;

    private const int InventoryInboxCapacity = 2048;

    private const int MentorInboxCapacity = 256;

    private const int MissionInboxCapacity = 256;

    private const int PetGrowStepAvatarChangeInfoSort = 10;

    private const int QuestInboxCapacity = 512;

    private const int SkillInboxCapacity = 1024;

    private const int TribeInboxCapacity = 512;

    private const int AnimalAbsorbTimeStatSort = 78;

    private const int AutoHuntPaidDayBudgetStatSort = 61;

    private const int AutoHuntPaidMinuteBudgetStatSort = 62;

    private const int SilverOrnamentStatSort = 90;

    private const int GoldOrnamentStatSort = 101;

    private readonly List<int> _gmTeleportNeighborScratch = [];

    private readonly Channel<GuildMembershipZoneCommand> _guildInbox =
        Channel.CreateBounded<GuildMembershipZoneCommand>(
            new BoundedChannelOptions(GuildInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly Channel<InventoryZoneCommand> _inventoryInbox = Channel.CreateBounded<InventoryZoneCommand>(
        new BoundedChannelOptions(InventoryInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly Channel<MentorZoneCommand> _mentorInbox =
        Channel.CreateBounded<MentorZoneCommand>(
            new BoundedChannelOptions(MentorInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly Channel<MissionZoneCommand> _missionInbox =
        Channel.CreateBounded<MissionZoneCommand>(
            new BoundedChannelOptions(MissionInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly Channel<QuestZoneCommand> _questInbox = Channel.CreateBounded<QuestZoneCommand>(
        new BoundedChannelOptions(QuestInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly Channel<SkillZoneCommand> _skillInbox = Channel.CreateBounded<SkillZoneCommand>(
        new BoundedChannelOptions(SkillInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly List<int> _statPotionFullActionNeighborScratch = [];

    private readonly Channel<TribeProgressZoneCommand> _tribeInbox =
        Channel.CreateBounded<TribeProgressZoneCommand>(
            new BoundedChannelOptions(TribeInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public bool PostInventoryCommand(in InventoryZoneCommand command)
    {
        if (_inventoryInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Inventory inbox is full."));
        return false;
    }

    public async Task<ZoneCommandResult> PostInventoryCommandAndWaitForResultAsync(InventoryZoneCommand command,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostInventoryCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Inventory inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Inventory command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Inventory command wait was cancelled.");
        }
    }

    public async Task<bool> PostInventoryCommandAndWaitAsync(InventoryZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        return (await PostInventoryCommandAndWaitForResultAsync(command, ct, timeout).ConfigureAwait(false)).Kind ==
               ZoneCommandResultKind.Applied;
    }

    public async Task<ZoneCommandResult> PostInventoryAndGroundItemCommandAndWaitForResultAsync(
        InventoryZoneCommand command, CancellationToken ct, TimeSpan? timeout = null)
    {
        if (command.GroundItemSpawn is null)
            throw new ArgumentException("A ground-item spawn plan is required.", nameof(command));

        return await PostInventoryCommandAndWaitForResultAsync(command, ct, timeout).ConfigureAwait(false);
    }

    public async Task<bool> PostInventoryAndGroundItemCommandAndWaitAsync(InventoryZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        return (await PostInventoryAndGroundItemCommandAndWaitForResultAsync(command, ct, timeout)
                .ConfigureAwait(false)).Kind == ZoneCommandResultKind.Applied;
    }

    public bool PostSkillCommand(in SkillZoneCommand command)
    {
        if (_skillInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Skill inbox is full."));
        return false;
    }

    public async Task<ZoneCommandResult> PostSkillCommandAndWaitForResultAsync(SkillZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostSkillCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Skill inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Skill command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Skill command wait was cancelled.");
        }
    }

    public bool PostMentorCommand(in MentorZoneCommand command)
    {
        return _mentorInbox.Writer.TryWrite(command);
    }

    public bool PostGuildCommand(in GuildMembershipZoneCommand command)
    {
        return _guildInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostGuildCommandAndWaitAsync(GuildMembershipZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostGuildCommand(in withSignal))
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

    public bool PostTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        if (_tribeInbox.Writer.TryWrite(command))
            return true;

        command.Applied?.TrySetResult(ZoneCommandResult.Backpressured("Tribe-progress inbox is full."));
        return false;
    }

    public async Task<bool> PostTribeProgressCommandAndWaitAsync(TribeProgressZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        return (await PostTribeProgressCommandAndWaitForResultAsync(command, ct, timeout).ConfigureAwait(false)).Kind
               == ZoneCommandResultKind.Applied;
    }

    public async Task<ZoneCommandResult> PostTribeProgressCommandAndWaitForResultAsync(
        TribeProgressZoneCommand command, CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostTribeProgressCommand(in withSignal))
            return ZoneCommandResult.Backpressured("Tribe-progress inbox is full.");

        try
        {
            return await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ZoneCommandResult.Cancelled("Tribe-progress command timed out.");
        }
        catch (OperationCanceledException)
        {
            return ZoneCommandResult.Cancelled("Tribe-progress command wait was cancelled.");
        }
    }

    private bool PostQuestCommand(in QuestZoneCommand command)
    {
        return _questInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostQuestCommandAndWaitAsync(QuestZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostQuestCommand(in withSignal))
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

    private bool PostMissionCommand(in MissionZoneCommand command)
    {
        return _missionInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostMissionCommandAndWaitAsync(MissionZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostMissionCommand(in withSignal))
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

    private void DrainInventoryCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _inventoryInbox.Reader.TryRead(out var command); processed++)
            try
            {
                var result = ApplyInventoryCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Inventory command could not be applied.");
                command.Applied?.TrySetResult(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} inventory command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplyInventoryCommand(in InventoryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        var equippedPetBefore = state.Inventory.GetSlot(ContainerMatrix.Equipment, PetSlots.EquipmentSlot);
        var equipmentUpdated = false;

        if (command.GroundItemSpawn is { } groundItemSpawn &&
            !SpawnGroundItem(in groundItemSpawn, state.DungeonInstanceId))
            return false;

        foreach (var snapshot in command.Containers)
        {
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

            if (snapshot.Container == ContainerMatrix.Equipment)
            {
                equipmentUpdated = true;
                state.LastSeenPetItemId = snapshot.Slots.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                    ? petStack.ItemId
                    : 0;
            }
        }

        if (!command.SkillChanges.IsDefaultOrEmpty)
        {
            foreach (var change in command.SkillChanges)
                state.LearnedSkills = change.Skill.SkillId == 0
                    ? state.LearnedSkills.Remove(change.Slot)
                    : state.LearnedSkills.SetItem(change.Slot, change.Skill);
        }

        if (command.SkillPoints is { } skillPoints)
            state.SkillPoints = skillPoints;

        if (!command.SkillChanges.IsDefaultOrEmpty || command.SkillPoints is not null)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        var petStateChanged = false;
        if (command.PetGrowth is { } petGrowth)
        {
            state.PetGrowth = petGrowth;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            petStateChanged = true;
        }

        if (command.PetActivity is { } petActivity)
        {
            state.PetActivity = petActivity;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            petStateChanged = true;
        }

        if (!petStateChanged && equipmentUpdated)
        {
            var equippedPetAfter = state.Inventory.GetSlot(ContainerMatrix.Equipment, PetSlots.EquipmentSlot);
            if (equippedPetAfter != equippedPetBefore)
            {
                state.PetGrowth = equippedPetAfter is { } nextPet ? PetItemState.Growth(nextPet) : 0;
                state.PetActivity = equippedPetAfter is { } nextPetActivity
                    ? PetItemState.Activity(nextPetActivity)
                    : (byte)0;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                petStateChanged = true;
            }
        }

        if (petStateChanged)
            PetItemState.SynchronizeEquippedState(state.Inventory, state.PetGrowth, state.PetActivity);

        if (command.UpdatedStats is { } stats)
        {
            state.Stats = stats;

            state.MaxLife = stats.MaxLife;
            state.MaxMana = stats.MaxMana;

            if (state.Life > state.MaxLife)
                state.Life = state.MaxLife;
            if (state.Mana > state.MaxMana)
                state.Mana = state.MaxMana;

            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        var vaultDateChanged = false;

        if (command.InventoryDate is { } inventoryDate)
        {
            state.InventoryDate = inventoryDate;
            vaultDateChanged = true;
        }

        if (command.StoreDate is { } storeDate)
        {
            state.StoreDate = storeDate;
            vaultDateChanged = true;
        }

        if (command.PetBagDate is { } petBagDate)
        {
            state.PetBagDate = petBagDate;
            vaultDateChanged = true;
        }

        if (vaultDateChanged)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        if (command.ClearEffectsAfterWeaponUnequip)
            ClearEffectsOnWeaponSlotWrite(state);

        if (command.RecomputeCombatPoseAfterEquip)
            BroadcastIdleActionState(state);

        return true;
    }

    private void ClearEffectsOnWeaponSlotWrite(PlayerRuntimeState state)
    {
        ClearAllBuffs(state);
        ResetPartyBuffMarker(state);
        state.NoManaCount = 0;

        if (state.AutoHuntConfig is not { } autoHuntConfig)
            return;

        Array.Clear(autoHuntConfig.BuffStore);
        Array.Clear(autoHuntConfig.AttackType);
    }

    private void DrainSkillCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _skillInbox.Reader.TryRead(out var command); processed++)
            try
            {
                command.Applied?.TrySetResult(ApplySkillCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Skill command could not be applied."));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} skill command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplySkillCommand(in SkillZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        state.LearnedSkills = command.Skill.SkillId == 0
            ? state.LearnedSkills.Remove(command.Slot)
            : state.LearnedSkills.SetItem(command.Slot, command.Skill);
        state.SkillPoints = command.NewSkillPoints;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        return true;
    }

    private void DrainMentorCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _mentorInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyMentorCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} mentor command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    private void ApplyMentorCommand(in MentorZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.TeacherCharacterId = command.TeacherCharacterId;
    }

    private void DrainGuildCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _guildInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyGuildMembershipCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} guild command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyGuildMembershipCommand(in GuildMembershipZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.GuildId = command.GuildId;
        state.GuildName = command.GuildName;
        state.GuildRoleDb = command.GuildRoleDb;
        state.GuildCallName = command.GuildCallName;
    }

    private void DrainTribeProgressCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _tribeInbox.Reader.TryRead(out var command); processed++)
            try
            {
                var result = ApplyTribeProgressCommand(in command)
                    ? ZoneCommandResult.Applied()
                    : ZoneCommandResult.Rejected("Tribe-progress command could not be applied.");
                command.Applied?.TrySetResult(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tribe progress command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
            }
    }

    private bool ApplyTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return false;

        var changed = false;

        if (command.ContributionPoints is { } contributionPoints)
        {
            state.ContributionPoints = contributionPoints;
            changed = true;
        }

        if (command.TribeRole is { } tribeRole)
        {
            state.TribeRole = tribeRole;
            changed = true;
        }

        if (command.Title is { } title)
        {
            state.Title = title;
            changed = true;
        }

        if (command.Halo is { } halo)
        {
            state.Halo = halo;
            changed = true;
        }

        if (command.ProtectForHalo is { } protectForHalo)
        {
            state.ProtectForHalo = protectForHalo;
            changed = true;
        }

        if (command.UseOrnament is { } useOrnament)
        {
            state.UseOrnament = useOrnament;
            changed = true;
        }

        if (command.SilverTime is { } silverTime)
        {
            state.SilverTime = silverTime;
            changed = true;
        }

        if (command.GoldTime is { } goldTime)
        {
            state.GoldTime = goldTime;
            changed = true;
        }

        if (command.DoubleKillNumTime is { } dkNumTime)
        {
            state.DoubleKillNumTime = dkNumTime;
            changed = true;
        }

        if (command.DoubleKillExpTime is { } dkExpTime)
        {
            state.DoubleKillExpTime = dkExpTime;
            changed = true;
        }

        if (command.DoubleKillNumTime2 is { } dkNumTime2)
        {
            state.DoubleKillNumTime2 = dkNumTime2;
            changed = true;
        }

        if (command.DmgBoost is { } dmgBoost)
        {
            state.DmgBoost = dmgBoost;
            changed = true;
        }

        if (command.HPBoost is { } hpBoost)
        {
            state.HPBoost = hpBoost;
            changed = true;
        }

        if (command.CriBoost is { } criBoost)
        {
            state.CriBoost = criBoost;
            changed = true;
        }

        if (command.WarriorPill is { } warriorPill)
        {
            state.WarriorPill = warriorPill;
            changed = true;
        }

        if (command.WarriorScroll is { } warriorScroll)
        {
            state.WarriorScroll = warriorScroll;
            changed = true;
        }

        if (command.BonusItemLevel is { } bonusItemLevel)
        {
            state.BonusItemLevel = bonusItemLevel;
            changed = true;
        }

        if (command.BonusItemValue is { } bonusItemValue)
        {
            state.BonusItemValue = bonusItemValue;
            changed = true;
        }

        if (command.StatVit is { } statVit)
        {
            state.StatVit = statVit;
            changed = true;
        }

        if (command.StatStr is { } statStr)
        {
            state.StatStr = statStr;
            changed = true;
        }

        if (command.StatInt is { } statInt)
        {
            state.StatInt = statInt;
            changed = true;
        }

        if (command.StatDex is { } statDex)
        {
            state.StatDex = statDex;
            changed = true;
        }

        if (command.StatPoints is { } statPoints)
        {
            state.StatPoints = statPoints;
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

        if (command.MaxLife is { } maxLife)
        {
            state.MaxLife = maxLife;
            changed = true;
        }

        if (command.MaxMana is { } maxMana)
        {
            state.MaxMana = maxMana;
            changed = true;
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;

        if (state.ClampVitalsToMax())
            changed = true;

        if (command.TribeNotifyScrollCount is { } tribeNotifyScrollCount)
        {
            state.TribeNotifyScrollCount = tribeNotifyScrollCount;
            changed = true;
        }

        if (command.Exp2 is { } exp2)
        {
            state.Exp2 = exp2;
            changed = true;
        }

        if (command.RebirthCount is { } rebirthCount)
        {
            state.RebirthCount = rebirthCount;
            changed = true;
        }

        if (command.Zone241Time is { } zone241Time)
            state.Zone241Time = zone241Time;

        if (command.LodRounds is { } lodRounds)
        {
            state.LodRounds = lodRounds;
            changed = true;
        }

        if (command.ProtectForRefine is { } protectForRefine)
        {
            state.ProtectForRefine = protectForRefine;
            changed = true;
        }

        if (command.ProtectForDestroy is { } protectForDestroy)
        {
            state.ProtectForDestroy = protectForDestroy;
            changed = true;
        }

        if (command.ProtectForCostume is { } protectForCostume)
        {
            state.ProtectForCostume = protectForCostume;
            changed = true;
        }

        if (command.ProtectForDestroy2 is { } protectForDestroy2)
        {
            state.ProtectForDestroy2 = protectForDestroy2;
            changed = true;
        }

        if (command.ProtectForWing is { } protectForWing)
        {
            state.ProtectForWing = protectForWing;
            changed = true;
        }

        if (command.ImproveItemValue is { } improveItemValue)
        {
            state.ImproveItemValue = improveItemValue;
            changed = true;
        }

        if (command.AddItemValue is { } addItemValue)
        {
            state.AddItemValue = addItemValue;
            changed = true;
        }

        if (command.HighItemValue is { } highItemValue)
        {
            state.HighItemValue = highItemValue;
            changed = true;
        }

        if (command.DoubleExpTime1 is { } doubleExpTime1)
        {
            state.DoubleExpTime1 = doubleExpTime1;
            changed = true;
        }

        if (command.DoubleExpTime2 is { } doubleExpTime2)
        {
            state.DoubleExpTime2 = doubleExpTime2;
            changed = true;
        }

        if (command.FightingGodForDestroy is { } fightingGodForDestroy)
        {
            state.FightingGodForDestroy = fightingGodForDestroy;
            changed = true;
        }

        if (command.DropItemTime is { } dropItemTime)
        {
            state.DropItemTime = dropItemTime;
            changed = true;
        }

        if (command.TaiyanKeyTimer is { } taiyanKeyTimer)
        {
            state.TaiyanKeyTimer = taiyanKeyTimer;
            changed = true;
        }

        if (command.EliteDungeonTime is { } eliteDungeonTime)
        {
            state.EliteDungeonTime = eliteDungeonTime;
            changed = true;
        }

        if (command.DungeonKeyTime is { } dungeonKeyTime)
        {
            state.DungeonKeyTime = dungeonKeyTime;
            changed = true;
        }

        if (command.IvyHallTicketTime is { } ivyHallTicketTime)
        {
            state.IvyHallTicketTime = ivyHallTicketTime;
            changed = true;
        }

        if (command.ScrollOfSeekersTime is { } scrollOfSeekersTime)
        {
            state.ScrollOfSeekersTime = scrollOfSeekersTime;
            changed = true;
        }

        if (command.TeacherPoint is { } teacherPoint)
        {
            state.TeacherPoint = teacherPoint;
            changed = true;
        }

        if (command.PetGrowth is { } petGrowth)
        {
            state.PetGrowth = petGrowth;
            changed = true;
        }

        if (command.PetActivity is { } petActivity)
        {
            state.PetActivity = petActivity;
            changed = true;
        }

        if (command.PlayTimeEvent is { } playTimeEvent)
        {
            state.PlayTimeEvent = playTimeEvent;
            changed = true;
        }

        if (command.EatLifePotion is { } eatLifePotion)
        {
            state.EatLifePotion = eatLifePotion;
            changed = true;
        }

        if (command.EatManaPotion is { } eatManaPotion)
        {
            state.EatManaPotion = eatManaPotion;
            changed = true;
        }

        if (command.EatStrPotion is { } eatStrPotion)
        {
            state.EatStrPotion = eatStrPotion;
            changed = true;
        }

        if (command.EatDexPotion is { } eatDexPotion)
        {
            state.EatDexPotion = eatDexPotion;
            changed = true;
        }

        if (command.EatElePotion is { } eatElePotion)
        {
            state.EatElePotion = eatElePotion;
            changed = true;
        }

        if (command.PetExpX2Time is { } petExpX2Time)
        {
            state.PetExpX2Time = petExpX2Time;
            changed = true;
        }

        if (command.Tribe is { } newTribe)
            state.Tribe = newTribe;

        if (command.PreviousTribe is { } newPreviousTribe)
            state.PreviousTribe = newPreviousTribe;

        if (command.QuestProgress is { } questProgress)
        {
            state.QuestStepPermanent = questProgress.StepPermanent;
            state.QuestActiveFlag = questProgress.ActiveFlag;
            state.QuestSort = questProgress.QSort;
            state.QuestTargetPhase = questProgress.TargetPhase;
            state.QuestKillCounter = questProgress.KillCounter;
        }

        if (command.TribeFourReturnAllowance is { } tribeFourReturnAllowance)
            state.TribeFourReturnAllowance = tribeFourReturnAllowance;

        if (command.StoreMoney is { } storeMoney)
            state.StoreMoney = storeMoney;

        if (command.BigMoneyDelta is { } bigMoneyDelta)
            state.BigMoney += bigMoneyDelta;

        if (command.Money is { } money)
            state.Money = money;

        if (command.SkillPoints is { } skillPoints)
        {
            state.SkillPoints = skillPoints;
            changed = true;
        }

        if (command.VisibleState is { } visibleState)
        {
            state.VisibleState = visibleState;
            changed = true;
        }

        if (command.SpecialState is { } specialState)
        {
            state.SpecialState = specialState;
            changed = true;
        }

        if (command.Level is { } newLevel)
        {
            state.Level = newLevel;
            changed = true;
        }

        if (command.Level2 is { } newLevel2)
        {
            state.Level2 = newLevel2;
            changed = true;
        }

        if (command.Experience is { } newExperience)
        {
            state.Experience = newExperience;
            changed = true;
        }

        if (command.M15PetLuckyBoxPity is { } m15PetLuckyBoxPity)
        {
            state.M15PetLuckyBoxPity = m15PetLuckyBoxPity;
            changed = true;
        }

        if (command.PremiumExpireUtc is { } premiumExpireUtc)
        {
            state.PremiumExpireUtc = premiumExpireUtc;
            changed = true;
        }

        if (command.AutoHuntPaidDayBudget is { } newAutoHuntPaidDayBudget)
        {
            state.AutoHuntPaidDayBudget = newAutoHuntPaidDayBudget;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = AutoHuntPaidDayBudgetStatSort, Value = newAutoHuntPaidDayBudget, Value2 = 0 });
            changed = true;
        }

        if (command.AutoHuntPaidMinuteBudget is { } newAutoHuntPaidMinuteBudget)
        {
            state.AutoHuntPaidMinuteBudget = newAutoHuntPaidMinuteBudget;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = AutoHuntPaidMinuteBudgetStatSort, Value = newAutoHuntPaidMinuteBudget, Value2 = 0 });
            changed = true;
        }

        if (command.AutoBuffTime is { } newAutoBuffTime)
        {
            state.AutoBuffTime = newAutoBuffTime;
            changed = true;
        }

        if (command.AnimalAbsorbTime is { } newAnimalAbsorbTime)
        {
            state.AnimalAbsorbTime = newAnimalAbsorbTime;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = AnimalAbsorbTimeStatSort, Value = newAnimalAbsorbTime, Value2 = 0 });
            changed = true;
        }

        if (command.AnimalDoubleExp is { } newAnimalDoubleExp)
            state.AnimalDoubleExp = newAnimalDoubleExp;

        if (command.BuffX2Time is { } newBuffX2Time)
        {
            state.BuffX2Time = newBuffX2Time;
            state.RecomputeSupportSkillTimeUpRatio();
            changed = true;
        }

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        if (command.LifeGain is { } inventoryLifeGain)
        {
            var gainedMaxLife = state.Stats?.MaxLife ?? state.MaxLife;
            state.Life = Math.Clamp(state.Life + inventoryLifeGain, 0, gainedMaxLife);
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = CharacterHpStatSort, Value = state.Life, Value2 = 0 });
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        if (command.ManaGain is { } inventoryManaGain)
        {
            var gainedMaxMana = state.Stats?.MaxMana ?? state.MaxMana;
            state.Mana = Math.Clamp(state.Mana + inventoryManaGain, 0, gainedMaxMana);
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = CharacterMpStatSort, Value = state.Mana, Value2 = 0 });
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        if (command.ResetAfkTick)
            state.AfkTick = 0;

        if (command.TeleportTo is { } teleportTo)
        {
            state.PosX = teleportTo.X;
            state.PosY = teleportTo.Y;
            state.PosZ = teleportTo.Z;

            var newCell = _grid.CellOf(state.PosX, state.PosZ);
            _grid.Move(command.CharacterId, state.CurrentCell, newCell, state.PosX, state.PosY, state.PosZ);
            state.CurrentCell = newCell;

            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Position);
        }

        if (command.NeighborActionBroadcast)
        {
            _gmTeleportNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_gmTeleportNeighborScratch, state.CurrentCell, command.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            BroadcastAvatarAction(_gmTeleportNeighborScratch, state);
        }

        if (!command.DropItems.IsDefaultOrEmpty)
            foreach (var drop in command.DropItems)
                SpawnGroundItem(drop.ItemId, drop.Quantity, state.PosX, state.PosY, state.PosZ, state.Name, "",
                    drop.DropSort);

        if (command.GmSummonMonsterTemplateId is { } gmSummonMonsterTemplateId &&
            !SpawnGmSummonedMonster(gmSummonMonsterTemplateId, state))
            return false;

        if (command.GmForceKillMonsterServerIndex is { } gmForceKillMonsterServerIndex &&
            TryGetMonster(gmForceKillMonsterServerIndex, out var gmForceKillMonster) &&
            gmForceKillMonster is not null)
            TryDamageMonster(gmForceKillMonsterServerIndex, gmForceKillMonster.Life, null, out _, out _);

        if (command.RebirthBroadcast)
            BroadcastAvatarStateFlag(state, 14, state.ContributionPoints, state.RebirthCount, state.Zone241Time);

        if (command.PetGrowStepBroadcast)
            BroadcastAvatarStateFlag(state, PetGrowStepAvatarChangeInfoSort, 0, 0, 0);

        if (command.FullActionRebroadcast)
        {
            var characterId = command.CharacterId;
            SendAvatarAction(state.Session, state);
            _statPotionFullActionNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_statPotionFullActionNeighborScratch, state.CurrentCell, characterId,
                state.PosX, state.PosY, state.PosZ);
            BroadcastAvatarAction(_statPotionFullActionNeighborScratch, state);
        }
        return true;
    }

    private void DrainQuestCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _questInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyQuestCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} quest command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyQuestCommand(in QuestZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.QuestStepPermanent = command.Progress.StepPermanent;
        state.QuestActiveFlag = command.Progress.ActiveFlag;
        state.QuestSort = command.Progress.QSort;
        state.QuestTargetPhase = command.Progress.TargetPhase;
        state.QuestKillCounter = command.Progress.KillCounter;

        foreach (var snapshot in command.Containers)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

        if (command.MoneyDelta != 0)
            state.Money += command.MoneyDelta;

        if (command.ExperienceDelta > 0)
            ApplyCharacterExperienceGain(state, command.ExperienceDelta);

        if (command.KillOtherTribeCountDelta != 0)
            state.ContributionPoints += command.KillOtherTribeCountDelta;
        if (command.TeacherPointDelta != 0)
            state.TeacherPoint += command.TeacherPointDelta;
        if (command.KillOtherTribeCountDelta != 0 || command.TeacherPointDelta != 0)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    private void DrainMissionCommands(int maximum)
    {
        for (var processed = 0; processed < maximum && _missionInbox.Reader.TryRead(out var command); processed++)
            try
            {
                ApplyMissionCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} mission command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyMissionCommand(in MissionZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.MissionJoinWar = command.JoinWar;
        state.MissionKillOtherTribe = command.KillOtherTribe;
        state.MissionKillMonster = command.KillMonster;
        state.MissionPlayTime = command.PlayTime;

        foreach (var snapshot in command.Containers)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);
    }
}
