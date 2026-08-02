using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
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

    /// <summary>Sort code for the mount absorb time stat update sent to the client on scroll use.</summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3376
    ///     <c>mTRANSFER.B_AVATAR_CHANGE_INFO_2(tUserInfo, S078MOUNT_ABSORB_TIME, wAvatar.aAnimalAbsorbTime)</c>
    /// </remarks>
    private const int AnimalAbsorbTimeStatSort = 78;

    /// <summary>Sort code for the paid auto-hunt day budget stat update sent to the client on scroll use.</summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795 — <c>B_AVATAR_CHANGE_INFO_2(..., 61, aAutoTime)</c>
    /// </remarks>
    private const int AutoHuntPaidDayBudgetStatSort = 61;

    /// <summary>Sort code for the auto-hunt minute budget stat update.</summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795 — <c>B_AVATAR_CHANGE_INFO_2(..., 62, aAutoTime2)</c>
    /// </remarks>
    private const int AutoHuntPaidMinuteBudgetStatSort = 62;

    /// <summary>Sort code for the Silver Ornament scroll time stat update.</summary>
    /// <remarks>Réf. sort: S090 — <c>B_AVATAR_CHANGE_INFO_2(..., 90, aSilverTime)</c></remarks>
    private const int SilverOrnamentStatSort = 90;

    /// <summary>Sort code for the Gold Ornament scroll time stat update.</summary>
    /// <remarks>Réf. sort: S101 — <c>B_AVATAR_CHANGE_INFO_2(..., 101, aGoldTime)</c></remarks>
    private const int GoldOrnamentStatSort = 101;

    private readonly List<int> _gmTeleportNeighborScratch = [];

    private readonly Channel<GuildMembershipZoneCommand> _guildInbox =
        Channel.CreateBounded<GuildMembershipZoneCommand>(
            new BoundedChannelOptions(GuildInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<InventoryZoneCommand> _inventoryInbox = Channel.CreateBounded<InventoryZoneCommand>(
        new BoundedChannelOptions(InventoryInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<MentorZoneCommand> _mentorInbox =
        Channel.CreateBounded<MentorZoneCommand>(
            new BoundedChannelOptions(MentorInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<MissionZoneCommand> _missionInbox =
        Channel.CreateBounded<MissionZoneCommand>(
            new BoundedChannelOptions(MissionInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<QuestZoneCommand> _questInbox = Channel.CreateBounded<QuestZoneCommand>(
        new BoundedChannelOptions(QuestInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Channel<SkillZoneCommand> _skillInbox = Channel.CreateBounded<SkillZoneCommand>(
        new BoundedChannelOptions(SkillInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _statPotionFullActionNeighborScratch = [];

    private readonly Channel<TribeProgressZoneCommand> _tribeInbox =
        Channel.CreateBounded<TribeProgressZoneCommand>(
            new BoundedChannelOptions(TribeInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostInventoryCommand(in InventoryZoneCommand command)
    {
        return _inventoryInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostInventoryCommandAndWaitAsync(InventoryZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostInventoryCommand(in withSignal))
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

    public bool PostSkillCommand(in SkillZoneCommand command)
    {
        return _skillInbox.Writer.TryWrite(command);
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
        }

        return true;
    }

    public bool PostTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        return _tribeInbox.Writer.TryWrite(command);
    }

    public async Task<bool> PostTribeProgressCommandAndWaitAsync(TribeProgressZoneCommand command,
        CancellationToken ct, TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostTribeProgressCommand(in withSignal))
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
        }

        return true;
    }

    private void DrainInventoryCommands()
    {
        while (_inventoryInbox.Reader.TryRead(out var command))
            try
            {
                ApplyInventoryCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} inventory command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyInventoryCommand(in InventoryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var hadWeaponEquipped =
            state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot) is not null;

        foreach (var snapshot in command.Containers)
        {
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

            if (snapshot.Container == ContainerMatrix.Equipment)
            {
                var newPetItemId = snapshot.Slots.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                    ? petStack.ItemId
                    : 0;
                if (newPetItemId != state.LastSeenPetItemId)
                {
                    state.LastSeenPetItemId = newPetItemId;
                    state.PetGrowth = 0;
                    state.PetActivity = 0;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }
            }
        }

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

        if (hadWeaponEquipped && state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot) is null)
            ClearEffectsOnWeaponUnequip(state);

        if (command.RecomputeCombatPoseAfterEquip)
            BroadcastIdleActionState(state);
    }

    private void ClearEffectsOnWeaponUnequip(PlayerRuntimeState state)
    {
        ClearAllBuffs(state);
        ResetPartyBuffMarker(state);
        state.NoManaCount = 0;

        if (state.AutoHuntConfig is { } autoHuntConfig)
            Array.Clear(autoHuntConfig.BuffStore);
    }

    private void DrainSkillCommands()
    {
        while (_skillInbox.Reader.TryRead(out var command))
            try
            {
                ApplySkillCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} skill command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    private void ApplySkillCommand(in SkillZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.LearnedSkills = command.Skill.SkillId == 0
            ? state.LearnedSkills.Remove(command.Slot)
            : state.LearnedSkills.SetItem(command.Slot, command.Skill);
        state.SkillPoints = command.NewSkillPoints;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    private void DrainMentorCommands()
    {
        while (_mentorInbox.Reader.TryRead(out var command))
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

    private void DrainGuildCommands()
    {
        while (_guildInbox.Reader.TryRead(out var command))
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

    private void DrainTribeProgressCommands()
    {
        while (_tribeInbox.Reader.TryRead(out var command))
            try
            {
                ApplyTribeProgressCommand(in command);
                command.Applied?.TrySetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tribe progress command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                command.Applied?.TrySetException(ex);
            }
    }

    private void ApplyTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

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

        // AutoHuntPaidDayBudget — compact date integer budget for paid auto-hunt day time.
        // Persisted via ProgressWriteBehindHost. Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795
        // Legacy sends B_AVATAR_CHANGE_INFO_2(tUserInfo, 61, wAvatar.aAutoTime) immediately after update.
        if (command.AutoHuntPaidDayBudget is { } newAutoHuntPaidDayBudget)
        {
            state.AutoHuntPaidDayBudget = newAutoHuntPaidDayBudget;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = AutoHuntPaidDayBudgetStatSort, Value = newAutoHuntPaidDayBudget, Value2 = 0 });
            changed = true;
        }

        // AutoHuntPaidMinuteBudget — minute counter for paid auto-hunt minute time.
        // Persisted via ProgressWriteBehindHost. Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795
        // Legacy sends B_AVATAR_CHANGE_INFO_2(tUserInfo, 62, wAvatar.aAutoTime2) immediately after update.
        if (command.AutoHuntPaidMinuteBudget is { } newAutoHuntPaidMinuteBudget)
        {
            state.AutoHuntPaidMinuteBudget = newAutoHuntPaidMinuteBudget;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = AutoHuntPaidMinuteBudgetStatSort, Value = newAutoHuntPaidMinuteBudget, Value2 = 0 });
            changed = true;
        }

        // AutoBuffTime — compact date integer (e.g. 20261231). Persisted via ProgressWriteBehindHost.
        // Client learns the new value from the UseInventoryItemResponse.Value, not a stat-update packet.
        // Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3147-3152
        if (command.AutoBuffTime is { } newAutoBuffTime)
        {
            state.AutoBuffTime = newAutoBuffTime;
            changed = true;
        }

        // AnimalAbsorbTime — minute counter. Persisted via ProgressWriteBehindHost.
        // Legacy broadcasts immediately via B_AVATAR_CHANGE_INFO_2 sort 78 after updating.
        // Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3374-3376
        if (command.AnimalAbsorbTime is { } newAnimalAbsorbTime)
        {
            state.AnimalAbsorbTime = newAnimalAbsorbTime;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = AnimalAbsorbTimeStatSort, Value = newAnimalAbsorbTime, Value2 = 0 });
            changed = true;
        }

        // AnimalDoubleExp — in-memory minute counter only; not in write-behind pipeline and not loaded from DB
        // on zone-enter, so no dirty flag is needed. Client receives the UseInventoryItemResponse success
        // signal; TimedBuffCountdownSystem broadcasts sort 75 decrements as normal.
        // Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3381-3387
        if (command.AnimalDoubleExp is { } newAnimalDoubleExp)
            state.AnimalDoubleExp = newAnimalDoubleExp;

        // BuffX2Time — minute counter that doubles skill-buff durations via SupportSkillTimeUpRatio.
        // Persisted via ProgressWriteBehindHost (DirtyFlags.Progression).
        // The AvatarStatUpdateResponse (sort 42) is sent by the service before posting this command,
        // matching the same split used by SilverTime/GoldTime — no re-broadcast here.
        // RecomputeSupportSkillTimeUpRatio() must run whenever the counter changes, since it controls
        // whether the x2 multiplier is active for any buff the character casts or receives.
        // Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3063-3074 — aBuffX2Time += 60; SetUserBonus2().
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

        if (command.GmSummonMonsterTemplateId is { } gmSummonMonsterTemplateId)
            SpawnGmSummonedMonster(gmSummonMonsterTemplateId, state);

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
    }

    private void DrainQuestCommands()
    {
        while (_questInbox.Reader.TryRead(out var command))
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

        if (command.ExperienceDelta > 0)
            ApplyCharacterExperienceGain(state, command.ExperienceDelta);

        if (command.KillOtherTribeCountDelta != 0)
            state.ContributionPoints += command.KillOtherTribeCountDelta;
        if (command.TeacherPointDelta != 0)
            state.TeacherPoint += command.TeacherPointDelta;
        if (command.KillOtherTribeCountDelta != 0 || command.TeacherPointDelta != 0)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    private void DrainMissionCommands()
    {
        while (_missionInbox.Reader.TryRead(out var command))
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
