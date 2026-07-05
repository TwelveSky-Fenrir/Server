using System.Threading.Channels;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.Tribes;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

/// <summary>
///     Already-validated-and-SQL-durable self-mutation mirrors for the "economy/progression" family of
///     handlers (inventory, skills, mentor, guild, tribe progress, quest, daily mission) -- every command here
///     has already been decided and persisted by its posting handler before reaching the inbox; this partial
///     only mirrors it into the tick-owned <see cref="PlayerRuntimeState" />.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Already-durably-persisted guild-membership mirrors, posted by <c>GuildActionHandler</c> onto this
    ///     character's own hosting zone, whether the target is the actor or a different guild member.
    /// </summary>
    private readonly Channel<GuildMembershipZoneCommand> _guildInbox =
        Channel.CreateBounded<GuildMembershipZoneCommand>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Separate inbox for already-validated-and-SQL-durable inventory results, posted by
    ///     <c>GenericActionHandler</c>. Kept out of <see cref="_inbox" />/<see cref="ZoneCommand" />'s union so
    ///     this concern stays additive-only. Drop-on-full is safe here: the SQL write already committed, so a
    ///     dropped command only leaves the in-memory mirror stale (self-heals on next world entry).
    /// </summary>
    private readonly Channel<InventoryZoneCommand> _inventoryInbox = Channel.CreateBounded<InventoryZoneCommand>(
        new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Mirrors one cross-character field write (mentor bonding, posted by <c>MentorStartHandler</c>) onto
    ///     the target character's own hosting zone rather than mutating it directly from another thread.
    /// </summary>
    private readonly Channel<MentorZoneCommand> _mentorInbox =
        Channel.CreateBounded<MentorZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Already-validated, already-SQL-durable daily-mission claims, posted by <c>DailyMissionHandler</c>.</summary>
    private readonly Channel<MissionZoneCommand> _missionInbox =
        Channel.CreateBounded<MissionZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Already-validated, already-SQL-durable quest-state transitions, posted by <c>QuestProgressHandler</c>.</summary>
    private readonly Channel<QuestZoneCommand> _questInbox = Channel.CreateBounded<QuestZoneCommand>(
        new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Already-validated, already-SQL-durable skill learn/upgrade results, posted by
    ///     <c>GenericActionHandler</c>. Kept as its own channel rather than folded into
    ///     <see cref="_inventoryInbox" />'s union, same additive-only rationale.
    /// </summary>
    private readonly Channel<SkillZoneCommand> _skillInbox = Channel.CreateBounded<SkillZoneCommand>(
        new BoundedChannelOptions(1024) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Already-decided tribe-progress self-mutations posted by <c>TribeActionHandler</c> for the actor's own hosting
    ///     zone.
    /// </summary>
    private readonly Channel<TribeProgressZoneCommand> _tribeInbox =
        Channel.CreateBounded<TribeProgressZoneCommand>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostInventoryCommand(in InventoryZoneCommand command)
    {
        return _inventoryInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Posts <paramref name="command" /> (its <see cref="InventoryZoneCommand.Applied" /> is overwritten --
    ///     leave it default) and waits until this zone's tick has actually mirrored it, not merely accepted it.
    ///     Every economy-affecting handler must call this (never the bare <see cref="PostInventoryCommand" />)
    ///     while holding <see cref="PlayerRuntimeState.EconomyActionLock" /> -- see that property's remarks for
    ///     the duplication race this closes. A timeout still returns true: the SQL write is already durable, a
    ///     timed-out mirror just stays stale until next world entry.
    /// </summary>
    public async Task<bool> PostInventoryCommandAndWaitAsync(InventoryZoneCommand command, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var withSignal = command with { Applied = applied };

        if (!PostInventoryCommand(in withSignal))
            return false; // inbox full -- caller logs this; nothing to wait for.

        try
        {
            await applied.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // SQL is already durable; only the in-memory mirror timing is affected.
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

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Callers acting on the requester's
    ///     own guild membership (anything that also touches money) must call this while holding
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" />; callers mirroring a different, possibly-offline
    ///     member may use the bare <see cref="PostGuildCommand" /> instead.
    /// </summary>
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
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    public bool PostTribeProgressCommand(in TribeProgressZoneCommand command)
    {
        return _tribeInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Must be called while holding
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> for any action that also debits money/CP.
    /// </summary>
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
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    private bool PostQuestCommand(in QuestZoneCommand command)
    {
        return _questInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Callers must already hold
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> and leave <see cref="QuestZoneCommand.Applied" />
    ///     at its default -- overwritten here.
    /// </summary>
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
            // SQL is already durable; only the in-memory mirror timing is affected.
        }

        return true;
    }

    private bool PostMissionCommand(in MissionZoneCommand command)
    {
        return _missionInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Same contract as <see cref="PostQuestCommandAndWaitAsync" />. Callers must already hold
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> and leave
    ///     <see cref="Progression.MissionZoneCommand.Applied" /> at its default -- overwritten here.
    /// </summary>
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
            // SQL is already durable; only the in-memory mirror timing is affected.
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
                // Same containment posture as DrainInbox: one bad inventory command must never take the whole
                // tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} inventory command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                // Still signal as faulted (not merely completed) so a caller awaiting Applied to release its
                // EconomyActionLock never hangs on a command that blew up.
                command.Applied?.TrySetException(ex);
            }
    }

    /// <summary>
    ///     No validation/I/O here on purpose -- already decided and persisted by the posting handler before
    ///     reaching the inbox. A no-op if the character already left this zone by the time the tick drains
    ///     this: their SQL write is already durable, so there is nothing left to mirror.
    /// </summary>
    private void ApplyInventoryCommand(in InventoryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        foreach (var snapshot in command.Containers)
        {
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

            // A pet SWAP (not just any equip touch) resets growth/activity to the newly-equipped pet's fresh state.
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
                    dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);
                }
            }
        }

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
    private void ApplySkillCommand(in SkillZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.LearnedSkills = state.LearnedSkills.SetItem(command.Slot, command.Skill);
        state.SkillPoints = command.NewSkillPoints;
        dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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

    /// <summary>
    ///     Same posture as <see cref="ApplyInventoryCommand" />. Every field is independently optional: null means "not
    ///     touched," never "reset to zero/false."
    /// </summary>
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

        if (changed)
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);

        if (!command.DropItems.IsDefaultOrEmpty)
            foreach (var drop in command.DropItems)
                SpawnGroundItem(drop.ItemId, drop.Quantity, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);

        // tSort 11 Max Rebirth's own B_AVATAR_CHANGE_INFO_1(sort 14)+Broadcast11 pairing (S04_MyWork02.cpp:11367);
        // Value03 (aZone241Time) has no Fenrir-side field yet, same "not modeled" posture as this file's other
        // untracked wire-only counters -- sent as 0.
        if (command.RebirthBroadcast)
            BroadcastAvatarStateFlag(state, 14, state.ContributionPoints, state.RebirthCount, 0);
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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
            dirtyTracker.MarkDirty(command.CharacterId, DirtyFlags.Progression);
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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
