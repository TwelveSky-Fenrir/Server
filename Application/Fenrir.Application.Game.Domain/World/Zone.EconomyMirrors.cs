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

/// <summary>
///     Already-validated-and-SQL-durable self-mutation mirrors for the "economy/progression" family of
///     handlers (inventory, skills, mentor, guild, tribe progress, quest, daily mission) -- every command here
///     has already been decided and persisted by its posting handler before reaching the inbox; this partial
///     only mirrors it into the tick-owned <see cref="PlayerRuntimeState" />.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Bounded capacity for <see cref="_guildInbox" /> -- also the basis for <see cref="GuildInboxDrainCapPerTick" />
    ///     .
    /// </summary>
    private const int GuildInboxCapacity = 512;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_guildInbox" /> -- same "half of this channel's own bounded
    ///     capacity" convention as <see cref="InboxDrainCapPerTick" /> (see that constant's own remarks for the
    ///     full rationale and the Fenrir-side-safeguard-not-legacy-parity caveat). Every channel declared in
    ///     this file follows the same pairing.
    /// </summary>
    private const int GuildInboxDrainCapPerTick = GuildInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_inventoryInbox" /> -- also the basis for
    ///     <see cref="InventoryInboxDrainCapPerTick" />.
    /// </summary>
    private const int InventoryInboxCapacity = 2048;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_inventoryInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own
    ///     remarks.
    /// </summary>
    private const int InventoryInboxDrainCapPerTick = InventoryInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_mentorInbox" /> -- also the basis for
    ///     <see cref="MentorInboxDrainCapPerTick" />.
    /// </summary>
    private const int MentorInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_mentorInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int MentorInboxDrainCapPerTick = MentorInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_missionInbox" /> -- also the basis for
    ///     <see cref="MissionInboxDrainCapPerTick" />.
    /// </summary>
    private const int MissionInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_missionInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int MissionInboxDrainCapPerTick = MissionInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_questInbox" /> -- also the basis for <see cref="QuestInboxDrainCapPerTick" />
    ///     .
    /// </summary>
    private const int QuestInboxCapacity = 512;

    /// <summary>Per-tick drain cap for <see cref="_questInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int QuestInboxDrainCapPerTick = QuestInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_skillInbox" /> -- also the basis for <see cref="SkillInboxDrainCapPerTick" />
    ///     .
    /// </summary>
    private const int SkillInboxCapacity = 1024;

    /// <summary>Per-tick drain cap for <see cref="_skillInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int SkillInboxDrainCapPerTick = SkillInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_tribeInbox" /> -- also the basis for <see cref="TribeInboxDrainCapPerTick" />
    ///     .
    /// </summary>
    private const int TribeInboxCapacity = 512;

    /// <summary>Per-tick drain cap for <see cref="_tribeInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int TribeInboxDrainCapPerTick = TribeInboxCapacity / 2;

    /// <summary>
    ///     Already-durably-persisted guild-membership mirrors, posted by <c>GuildActionHandler</c> onto this
    ///     character's own hosting zone, whether the target is the actor or a different guild member.
    /// </summary>
    private readonly Channel<GuildMembershipZoneCommand> _guildInbox =
        Channel.CreateBounded<GuildMembershipZoneCommand>(
            new BoundedChannelOptions(GuildInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Separate inbox for already-validated-and-SQL-durable inventory results, posted by
    ///     <c>GenericActionHandler</c>. Kept out of <see cref="_inbox" />/<see cref="ZoneCommand" />'s union so
    ///     this concern stays additive-only. Drop-on-full is safe here: the SQL write already committed, so a
    ///     dropped command only leaves the in-memory mirror stale (self-heals on next world entry).
    /// </summary>
    private readonly Channel<InventoryZoneCommand> _inventoryInbox = Channel.CreateBounded<InventoryZoneCommand>(
        new BoundedChannelOptions(InventoryInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Mirrors one cross-character field write (mentor bonding, posted by <c>MentorStartHandler</c>) onto
    ///     the target character's own hosting zone rather than mutating it directly from another thread.
    /// </summary>
    private readonly Channel<MentorZoneCommand> _mentorInbox =
        Channel.CreateBounded<MentorZoneCommand>(
            new BoundedChannelOptions(MentorInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Already-validated, already-SQL-durable daily-mission claims, posted by <c>DailyMissionHandler</c>.</summary>
    private readonly Channel<MissionZoneCommand> _missionInbox =
        Channel.CreateBounded<MissionZoneCommand>(
            new BoundedChannelOptions(MissionInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Already-validated, already-SQL-durable quest-state transitions, posted by <c>QuestProgressHandler</c>.</summary>
    private readonly Channel<QuestZoneCommand> _questInbox = Channel.CreateBounded<QuestZoneCommand>(
        new BoundedChannelOptions(QuestInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Already-validated, already-SQL-durable skill learn/upgrade results, posted by
    ///     <c>GenericActionHandler</c>. Kept as its own channel rather than folded into
    ///     <see cref="_inventoryInbox" />'s union, same additive-only rationale.
    /// </summary>
    private readonly Channel<SkillZoneCommand> _skillInbox = Channel.CreateBounded<SkillZoneCommand>(
        new BoundedChannelOptions(SkillInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Already-decided tribe-progress self-mutations posted by <c>TribeActionHandler</c> for the actor's own hosting
    ///     zone.
    /// </summary>
    private readonly Channel<TribeProgressZoneCommand> _tribeInbox =
        Channel.CreateBounded<TribeProgressZoneCommand>(
            new BoundedChannelOptions(TribeInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

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
                // Same containment posture as DrainInbox: one bad inventory command must never take the whole
                // tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} inventory command for character {CharacterId} failed", MapId,
                    command.CharacterId);
                // Still signal as faulted (not merely completed) so a caller awaiting Applied to release its
                // EconomyActionLock never hangs on a command that blew up.
                command.Applied?.TrySetException(ex);
            }
        }

        if (processed >= InventoryInboxDrainCapPerTick)
            LogDrainCapEngaged(_inventoryInbox.Reader, "inventory", InventoryInboxDrainCapPerTick);
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
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }
            }
        }

        if (command.UpdatedStats is { } stats)
        {
            state.Stats = stats;

            // SetBasicAbilityFromEquip (S07_MyGame04.cpp:158-183, called from both equip's and unequip's call
            // sites at S04_MyWork05.cpp:1303/1615) recomputes MaxLife/MaxMana unconditionally as part of the
            // same recompute that produced `stats` above -- there is exactly one stored max-life/max-mana
            // value in the legacy model, not a separate "internal" copy vs. "reported" copy. Mirror it into
            // the flat fields here too, since those (not Stats.MaxLife/Stats.MaxMana) are what the outbound
            // broadcast (BuildAvatarActionRecv) and write-behind persistence (ProgressWriteBehindHost/
            // PositionWriteBehindHost) actually read.
            state.MaxLife = stats.MaxLife;
            state.MaxMana = stats.MaxMana;

            // Unequip's own inline clamp (S04_MyWork05.cpp:1616-1617: SetIntegerUp(aLifeValue, GetMaxLife(),
            // GetMaxLife()) and the symmetric mana call; function.h:237-240 confirms SetIntegerUp is
            // downward-only -- it overwrites only when the current value exceeds the check value, never
            // raises it) guards against a lower max after removing a VIT/INT-boosting item. Equip's call site
            // performs no equivalent clamp, but equip is never expected to lower Max (no cited scenario where
            // it does), so applying this same downward-only guard regardless of direction is behaviorally
            // identical to the legacy equip/unequip split while avoiding an equip/unequip flag threaded
            // through InventoryZoneCommand just to gate a clamp that can only ever fire on the unequip side in
            // practice.
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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

        // aZone241Time -- already synchronously persisted by ICharacterRepository.AdjustZone241TimeAsync
        // before this command is posted (same posture as Tribe/QuestProgress/TribeFourReturnAllowance
        // further below), so this does not set `changed`/mark progress-dirty; there is nothing left to flush.
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

        // Op37 (CZ_CHANGE_TO_TRIBE4_SEND) success -- already synchronously persisted by
        // game.usp_Character_ApplyTribeFourConversion before this command is posted, so -- same posture as
        // ApplyQuestCommand's own StepPermanent/ActiveFlag/QSort/TargetPhase/KillCounter mirror -- none of
        // these three set `changed`/mark progress-dirty; there is nothing left to flush.
        if (command.Tribe is { } newTribe)
            state.Tribe = newTribe;

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

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        if (!command.DropItems.IsDefaultOrEmpty)
            foreach (var drop in command.DropItems)
                SpawnGroundItem(drop.ItemId, drop.Quantity, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);

        // tSort 11 Max Rebirth's own B_AVATAR_CHANGE_INFO_1(sort 14)+Broadcast11 pairing (S04_MyWork02.cpp:11367),
        // also posted by the Rebirth-Pill item-consumption path (Path A). Value03 (aZone241Time) is read from
        // the just-mirrored state.Zone241Time above (0 for Path A, which never touches this counter).
        if (command.RebirthBroadcast)
            BroadcastAvatarStateFlag(state, 14, state.ContributionPoints, state.RebirthCount, state.Zone241Time);

        // Op 23 (CZ_USE_INVENTORY_ITEM_SEND) stat-potion family's own post-consumption avatar-action refresh
        // -- see TribeProgressZoneCommand.FullActionRebroadcast's own remarks for why this sends the
        // self-refresh once (not legacy's literal twice) plus the AOI-neighbor broadcast.
        if (command.FullActionRebroadcast)
        {
            var characterId = command.CharacterId;
            SendAvatarAction(state.Session, state);
            var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
            BroadcastAvatarAction(neighbors, state);
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
