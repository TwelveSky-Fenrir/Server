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
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int GuildInboxCapacity = 512;

    private const int GuildInboxDrainCapPerTick = GuildInboxCapacity / 2;

    private const int InventoryInboxCapacity = 2048;

    private const int InventoryInboxDrainCapPerTick = InventoryInboxCapacity / 2;

    private const int MentorInboxCapacity = 256;

    private const int MentorInboxDrainCapPerTick = MentorInboxCapacity / 2;

    private const int MissionInboxCapacity = 256;

    private const int MissionInboxDrainCapPerTick = MissionInboxCapacity / 2;

    private const int QuestInboxCapacity = 512;

    private const int QuestInboxDrainCapPerTick = QuestInboxCapacity / 2;

    private const int SkillInboxCapacity = 1024;

    private const int SkillInboxDrainCapPerTick = SkillInboxCapacity / 2;

    private const int TribeInboxCapacity = 512;

    private const int TribeInboxDrainCapPerTick = TribeInboxCapacity / 2;

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
        var processed = 0;
        while (processed < InventoryInboxDrainCapPerTick && _inventoryInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= InventoryInboxDrainCapPerTick)
            LogDrainCapEngaged(_inventoryInbox.Reader, "inventory", InventoryInboxDrainCapPerTick);
    }

    private void ApplyInventoryCommand(in InventoryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

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
    }

    private void DrainSkillCommands()
    {
        var processed = 0;
        while (processed < SkillInboxDrainCapPerTick && _skillInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= SkillInboxDrainCapPerTick)
            LogDrainCapEngaged(_skillInbox.Reader, "skill", SkillInboxDrainCapPerTick);
    }

    private void ApplySkillCommand(in SkillZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.LearnedSkills = state.LearnedSkills.SetItem(command.Slot, command.Skill);
        state.SkillPoints = command.NewSkillPoints;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    private void DrainMentorCommands()
    {
        var processed = 0;
        while (processed < MentorInboxDrainCapPerTick && _mentorInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= MentorInboxDrainCapPerTick)
            LogDrainCapEngaged(_mentorInbox.Reader, "mentor", MentorInboxDrainCapPerTick);
    }

    private void ApplyMentorCommand(in MentorZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.TeacherCharacterId = command.TeacherCharacterId;
    }

    private void DrainGuildCommands()
    {
        var processed = 0;
        while (processed < GuildInboxDrainCapPerTick && _guildInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= GuildInboxDrainCapPerTick)
            LogDrainCapEngaged(_guildInbox.Reader, "guild", GuildInboxDrainCapPerTick);
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
        var processed = 0;
        while (processed < TribeInboxDrainCapPerTick && _tribeInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= TribeInboxDrainCapPerTick)
            LogDrainCapEngaged(_tribeInbox.Reader, "tribe-progress", TribeInboxDrainCapPerTick);
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

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

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
        var processed = 0;
        while (processed < QuestInboxDrainCapPerTick && _questInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= QuestInboxDrainCapPerTick)
            LogDrainCapEngaged(_questInbox.Reader, "quest", QuestInboxDrainCapPerTick);
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

        if (command.ExperienceDelta != 0)
            state.Experience += command.ExperienceDelta;
        if (command.ContributionPointsDelta != 0)
            state.ContributionPoints += command.ContributionPointsDelta;
        if (command.TeacherPointDelta != 0)
            state.TeacherPoint += command.TeacherPointDelta;
        if (command.ExperienceDelta != 0 || command.ContributionPointsDelta != 0 || command.TeacherPointDelta != 0)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    private void DrainMissionCommands()
    {
        var processed = 0;
        while (processed < MissionInboxDrainCapPerTick && _missionInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= MissionInboxDrainCapPerTick)
            LogDrainCapEngaged(_missionInbox.Reader, "mission", MissionInboxDrainCapPerTick);
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
