using System.Collections.Immutable;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Already-decided, purely cosmetic/self-mutation mirrors for the smaller per-opcode subsystems (drink
///     bottle, hero-ranking throttle, fishing, mount, costume, stellar core, playtime/rank buff, rune socket,
///     auto-buff, pshop stall) that don't warrant their own dedicated partial. Same posture throughout: the
///     posting handler already validated/persisted the change; this only mirrors it into the tick-owned
///     <see cref="PlayerRuntimeState" /> and optionally rebroadcasts.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     <c>S904UPDATE_HERO_POINT</c> (Server/Header/Protocol/STRUCT.h:1650) -- the <c>AvatarStatUpdateResponse</c>
    ///     Sort code the weekly hero-rank rollover reset (<see cref="ApplyHeroRankingRolloverReset" />) pushes.
    ///     Also used, per the same wire tag, by the separate per-kill grant path
    ///     (<c>MyCenterCom::AddHeroRankPoint</c>, Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:774-820) and the
    ///     on-login initial-state sync (Server/ts25zone/S04_MyWork02.cpp:1113-1116) -- neither of those two is
    ///     wired to push this packet yet; only the rollover reset below is in scope here.
    /// </summary>
    private const int HeroRankPointStatSort = 904;

    /// <summary>
    ///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) auto-buff registration + activation
    ///     mirror. See <see cref="ApplyAutoBuffCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<AutoBuffZoneCommand> _autoBuffInbox =
        Channel.CreateBounded<AutoBuffZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror. See
    ///     <see cref="ApplyAvatarBuffCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<AvatarBuffZoneCommand> _avatarBuffInbox =
        Channel.CreateBounded<AvatarBuffZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Op 129 <c>DrinkBottle</c> self-mutation mirror. See <see cref="ApplyDrinkBottleCommand" />'s remarks.</summary>
    private readonly Channel<DrinkBottleZoneCommand> _bottleInbox =
        Channel.CreateBounded<DrinkBottleZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror. See
    ///     <see cref="ApplyCostumeCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<CostumeZoneCommand> _costumeInbox =
        Channel.CreateBounded<CostumeZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 103/104/105 (<c>FishingLine</c>/<c>FishingProgress</c>/<c>FishingCatch</c>) shared state-machine
    ///     mirror. See <see cref="ApplyFishingCommand" />'s remarks -- awaiting new PlayerRuntimeState fields.
    /// </summary>
    private readonly Channel<FishingZoneCommand> _fishingInbox =
        Channel.CreateBounded<FishingZoneCommand>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 118 <c>HeroRanking</c> throttle-timestamp mirror -- the ranking query itself is read-only and answered
    ///     directly by the handler.
    /// </summary>
    private readonly Channel<HeroRankingQueryZoneCommand> _heroRankingInbox =
        Channel.CreateBounded<HeroRankingQueryZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Weekly hero-ranking Current-&gt;Previous rollover reset trigger -- posted once per successful flip by
    ///     <c>HeroRankingRolloverHost</c> (Hosting), once per hosted zone. Carries no per-instance data: draining
    ///     one just means "sweep every currently connected player once," see
    ///     <see cref="ApplyHeroRankingRolloverReset" />. A 4-slot capacity is generous for an event this rare
    ///     (weekly, at most one post per host check interval).
    /// </summary>
    private readonly Channel<HeroRankingRolloverZoneCommand> _heroRankingRolloverInbox =
        Channel.CreateBounded<HeroRankingRolloverZoneCommand>(
            new BoundedChannelOptions(4) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. See
    ///     <see cref="ApplyMountCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<MountZoneCommand> _mountInbox =
        Channel.CreateBounded<MountZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Fire-and-forget, purely cosmetic PShop-stall mirrors posted by <c>BuyShopItemHandler</c> onto the
    ///     seller's own hosting zone after a purchase already durably committed.
    /// </summary>
    private readonly Channel<PshopZoneCommand> _pshopInbox =
        Channel.CreateBounded<PshopZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 157 <c>RuneSocket</c> self-mutation mirror. See <see cref="ApplyRuneSocketCommand" />'s remarks
    ///     for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<RuneSocketZoneCommand> _runeInbox =
        Channel.CreateBounded<RuneSocketZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 153 <c>StellarCoreState</c> self-mutation mirror, same shape as <see cref="_costumeInbox" />. See
    ///     <see cref="ApplyStellarCoreCommand" />'s remarks.
    /// </summary>
    private readonly Channel<StellarCoreZoneCommand> _stellarCoreInbox =
        Channel.CreateBounded<StellarCoreZoneCommand>(
            new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostPshopCommand(in PshopZoneCommand command)
    {
        return _pshopInbox.Writer.TryWrite(command);
    }

    public bool PostDrinkBottleCommand(in DrinkBottleZoneCommand command)
    {
        return _bottleInbox.Writer.TryWrite(command);
    }

    public bool PostHeroRankingQueryCommand(in HeroRankingQueryZoneCommand command)
    {
        return _heroRankingInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Posted once per successful weekly rollover flip -- see <see cref="ApplyHeroRankingRolloverReset" />.
    /// </summary>
    public bool PostHeroRankingRolloverReset()
    {
        return _heroRankingRolloverInbox.Writer.TryWrite(default);
    }

    public bool PostFishingCommand(in FishingZoneCommand command)
    {
        return _fishingInbox.Writer.TryWrite(command);
    }

    /// <summary>Same contract as <see cref="PostInventoryCommandAndWaitAsync" />.</summary>
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

    /// <summary>
    ///     Same contract as <see cref="PostInventoryCommandAndWaitAsync" />. Callers must already hold
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> and leave <see cref="RuneSocketZoneCommand.Applied" />
    ///     at its default -- overwritten here.
    /// </summary>
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
        while (_bottleInbox.Reader.TryRead(out var command))
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

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    private void DrainHeroRankingQueryCommands()
    {
        while (_heroRankingInbox.Reader.TryRead(out var command))
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

    /// <summary>Same posture as <see cref="ApplyInventoryCommand" />.</summary>
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
        while (_heroRankingRolloverInbox.Reader.TryRead(out _))
            try
            {
                ApplyHeroRankingRolloverReset();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} hero-ranking rollover reset failed", MapId);
            }
    }

    /// <summary>
    ///     Legacy per-tick Monday-00:00 wall-clock check (<c>FIX_HERO_RANK_RESET_PROBLEM</c>, unconditionally
    ///     compiled, Server/ts25zone/S07_MyGame01.cpp:2507-2515): every connected character whose live,
    ///     session-scoped <see cref="PlayerRuntimeState.HeroRankPoints" /> mirror is currently greater than zero
    ///     has it reset to zero and receives a single <see cref="HeroRankPointStatSort" />-coded
    ///     <c>AvatarStatUpdateResponse</c> (value 0) on their own connection only -- never an AOI/zone-wide
    ///     broadcast, matching the legacy <c>B_AVATAR_CHANGE_INFO_2</c> <c>MyUser*</c>-targeted overload
    ///     (Server/ts25zone/S05_MyTransfer.cpp:519-542), not the AOI-broadcast one. A character whose mirror is
    ///     already zero is left untouched and receives no notice, matching legacy's own "counter &gt; 0" gate.
    /// </summary>
    /// <remarks>
    ///     Fires at most once per rollover event per zone (one <see cref="HeroRankingRolloverZoneCommand" />
    ///     posted per flip detected by <c>HeroRankingRolloverHost</c>, drained exactly once here) -- unlike
    ///     legacy's own "re-evaluate every tick while the current second is still 0 or 1" gate, this does not
    ///     rely on the counter happening to already be zero to avoid re-notifying within the same rollover event.
    ///     Only ever changes this zone's own live <see cref="PlayerRuntimeState" /> instances -- never a database
    ///     write; the durable Current-&gt;Previous flip already happened, atomically, inside
    ///     <c>game.usp_HeroRanking_Rollover</c> before this is ever posted.
    /// </remarks>
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
        while (_fishingInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Mirrors the fishing state machine decided by <c>FishingLineHandler</c>/<c>FishingProgressHandler</c>/
    ///     <c>FishingCatchHandler</c> (op 103/104/105) and, when <see cref="FishingZoneCommand.Broadcast" /> is
    ///     set, rebroadcasts the avatar action to self + AOI neighbors -- matching the legacy's own <c>Broadcast11</c>
    ///     (self included) / <c>Broadcast22</c>+<c>USEND</c> (self direct-sent, neighbors excluded) pair, which
    ///     are net-equivalent "everyone in range including self."
    /// </summary>
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

        var action = new ActionInfo
        {
            Type = 0,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var fisherId = command.CharacterId;
        var recipients = _grid.Neighbors(state.CurrentCell).Where(id => id != fisherId).ToList();
        recipients.Add(fisherId);
        BroadcastAvatarAction(recipients, state, action);
    }

    private void DrainMountCommands()
    {
        while (_mountInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. <see cref="MountZoneCommand.Broadcast" />
    ///     decides which AOI/self packets follow, matching the legacy's own per-case B_AVATAR_CHANGE_INFO_1/2 pairing.
    /// </summary>
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

    /// <summary>
    ///     Builds one B_AVATAR_CHANGE_INFO_1-equivalent frame and sends it once to <paramref name="state" />
    ///     itself plus every other AOI neighbor (unlike the legacy's own Broadcast11, which re-sends to the
    ///     source player too -- functionally a no-op duplicate there, so intentionally not reproduced).
    /// </summary>
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

        state.Session.Send(response);
        foreach (var neighborId in _grid.Neighbors(state.CurrentCell))
        {
            if (neighborId == state.CharacterId) continue;
            if (_players.TryGetValue(neighborId, out var neighbor))
                neighbor.Session.Send(response);
        }
    }

    private void DrainCostumeCommands()
    {
        while (_costumeInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror.
    ///     <see cref="CostumeZoneCommand.Broadcast" /> covers op 90's AvatarStateFlag pair;
    ///     <see cref="CostumeZoneCommand.FullActionRebroadcast" /> covers op 139's full avatar-action rebroadcast
    ///     instead (matches the legacy's own B_AVATAR_ACTION_RECV + Broadcast11 for that opcode specifically).
    /// </summary>
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
        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
    }

    private void DrainStellarCoreCommands()
    {
        while (_stellarCoreInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Op 153 <c>StellarCoreState</c> self-mutation mirror. <see cref="StellarCoreZoneCommand.Broadcast" />
    ///     covers the equip/remove AvatarStateFlag pair (sort 37/38), matching the legacy's own
    ///     B_AVATAR_CHANGE_INFO_1 + Broadcast11 pairing for CZ cases 3/4 exactly (same posture as
    ///     <see cref="ApplyCostumeCommand" />'s case 3/4 -- Broadcast11 there re-sends the just-composed
    ///     AVATAR_CHANGE_INFO_1 frame, it does not build a separate full-action packet).
    /// </summary>
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
            state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(clearedSlot, 0);

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

    private void DrainAvatarBuffCommands()
    {
        while (_avatarBuffInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror. Both opcodes' own
    ///     AvatarStatUpdateResponse self-unicast is sent directly by the handler (the value is already known
    ///     before the tick mirrors it, same posture as <c>DrinkBottleHandler</c>) -- this only mirrors the
    ///     already-decided state.
    /// </summary>
    private void ApplyAvatarBuffCommand(in AvatarBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        var changed = false;

        if (command.StateTimeEffect is { } stateTimeEffect)
            state.StateTimeEffect = stateTimeEffect;

        if (command.RankBuffType is { } rankBuffType)
            state.RankBuffType = rankBuffType;

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
    }

    private void DrainRuneSocketCommands()
    {
        while (_runeInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Op 157 <c>RuneSocket</c> self-mutation mirror. <c>RuneItemId</c>/<c>RuneStat</c> null means "clear
    ///     this slot" (both sort 0 insert and sort 1 remove always write the full new value for
    ///     <see cref="RuneSocketZoneCommand.RuneIndex" />, never "leave untouched"). The paired inventory-slot
    ///     mirror rides the existing <see cref="_inventoryInbox" /> separately, same as every other
    ///     economy-adjacent handler. <see cref="RuneSocketZoneCommand.UpdatedStats" /> is always null today --
    ///     see <c>RuneSocketHandler</c>'s remarks for why recomputing would be a pure no-op.
    /// </summary>
    private void ApplyRuneSocketCommand(in RuneSocketZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.RuneSystem = state.RuneSystem.SetItem(command.RuneIndex, command.RuneItemId ?? 0);
        state.RuneSystemStat = state.RuneSystemStat.SetItem(command.RuneIndex, command.RuneStat ?? 0);

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;
    }

    private void DrainAutoBuffCommands()
    {
        while (_autoBuffInbox.Reader.TryRead(out var command))
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

    /// <summary>
    ///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) mirror. <c>RegisteredSkills</c> (op 94)
    ///     lands on <see cref="PlayerRuntimeState.AutoBuffSkill" />; <c>ManaAfterActivation</c>/<c>ActionSort</c>
    ///     (op 95 <c>tSort==1</c>) mirror <see cref="PlayerRuntimeState.Mana" />/<see cref="PlayerRuntimeState.ActionSort" />
    ///     and, when <see cref="AutoBuffZoneCommand.Broadcast" /> is set, rebroadcast the resulting avatar action
    ///     to self + AOI neighbors -- same self-inclusive <c>Broadcast11</c> posture as <see cref="ApplyFishingCommand" />.
    ///     Op 95 <c>tSort==2</c>'s per-tick buff-application logic (<c>ProcessForCreateEffectValue</c>) is out of
    ///     scope -- see <see cref="Skills.AutoBuffActivationResolver" />'s remarks.
    /// </summary>
    private void ApplyAutoBuffCommand(in AutoBuffZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        if (command.RegisteredSkills is { } registered)
            state.AutoBuffSkill = registered;

        if (command.ManaAfterActivation is { } mana)
        {
            state.Mana = mana;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        if (!command.Broadcast)
            return;

        if (command.ActionSort is { } sort)
            state.ActionSort = sort;

        state.ActionSkillNumber = 0;
        state.ActionSkillGradeNum1 = 0;
        state.ActionSkillGradeNum2 = 0;

        var action = new ActionInfo
        {
            Type = 0,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var casterId = command.CharacterId;
        var recipients = _grid.Neighbors(state.CurrentCell).Where(id => id != casterId).ToList();
        recipients.Add(casterId);
        BroadcastAvatarAction(recipients, state, action);
    }

    private void DrainPshopCommands()
    {
        while (_pshopInbox.Reader.TryRead(out var command))
            try
            {
                ApplyPshopCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} pshop command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    /// <summary>
    ///     No-op if the character left, or their stall was already re-opened/re-populated since (a stale mirror is
    ///     harmless).
    /// </summary>
    private void ApplyPshopCommand(in PshopZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state) || state.PshopListing is not { } listing)
            return;

        if (command.CloseShop)
        {
            state.PshopOpen = false;
            return;
        }

        var itemInfo = listing.ItemInfo;
        var baseIndex = (command.Page * 5 + command.Slot) * 9;
        for (var k = 0; k < 9; k++)
            itemInfo[baseIndex + k] = 0;
    }
}

/// <summary>
///     Op 23/129 (<c>UseInventoryItem</c> iSort==26 acquisition / <c>DrinkBottle</c> consumption) shared
///     self-mutation mirror for <see cref="PlayerRuntimeState.BottleSlots" />. <c>NewItemId</c> is null for a
///     consumption (the slot's item id is unchanged, only its count decrements) and non-null for an
///     acquisition (the slot now holds this item id, freshly refilled).
/// </summary>
public readonly record struct DrinkBottleZoneCommand(
    int CharacterId,
    int BottleIndex,
    int RemainingCount,
    int NewLife,
    int? NewItemId = null,
    EffectiveStats? UpdatedStats = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Op 118 <c>HeroRanking</c> throttle-timestamp mirror -- the ranking query itself is read-only and answered
///     directly by the handler.
/// </summary>
public readonly record struct HeroRankingQueryZoneCommand(int CharacterId, bool Previous, TimeSpan QueriedAtZoneClock);

/// <summary>
///     Weekly hero-ranking rollover reset trigger -- see <see cref="Zone.ApplyHeroRankingRolloverReset" />. Carries
///     no per-instance data; posting one just means "sweep every currently connected player once."
/// </summary>
public readonly record struct HeroRankingRolloverZoneCommand;

/// <summary>
///     Op 103/104/105 (<c>FishingLine</c>/<c>FishingProgress</c>/<c>FishingCatch</c>) shared state-machine
///     mirror -- the handler has already decided the outcome (water/geometry check, elapsed-time gate, RNG
///     roll all happen on the request thread reading <see cref="PlayerRuntimeState" /> directly); this command
///     only carries the already-resolved values across to the tick for the single-writer mutation + optional
///     AOI broadcast. <see cref="CastAtUtc" /> is null unless this specific call restamps the cast clock
///     (legacy <c>mFishingTickCount</c>).
/// </summary>
public readonly record struct FishingZoneCommand(
    int CharacterId,
    int NewFishingState,
    int NewFishingStep,
    bool CatchingFish,
    bool Broadcast,
    int? ActionSort,
    DateTime? CastAtUtc = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Which AvatarStateFlag/AvatarStatUpdate pair (if any) <see cref="Zone.ApplyMountCommand" /> sends after
///     mirroring a <see cref="MountZoneCommand" />.
/// </summary>
public enum MountBroadcastKind : byte
{
    None,
    Mount,
    Dismount,
    AbsorbToggle
}

/// <summary>
///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. Nullable/optional-field shape,
///     same convention as <see cref="TribeProgressZoneCommand" />.
/// </summary>
public readonly record struct MountZoneCommand(
    int CharacterId,
    int? AnimalIndex = null,
    int? AnimalNumber = null,
    int? AnimalAbsorbState = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    MountBroadcastKind Broadcast = MountBroadcastKind.None,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Which AvatarStateFlag broadcast (if any) <see cref="Zone.ApplyCostumeCommand" /> sends for a
///     <see cref="CostumeZoneCommand" /> (op 90 only -- op 139 uses
///     <see cref="CostumeZoneCommand.FullActionRebroadcast" /> instead).
/// </summary>
public enum CostumeBroadcastKind : byte
{
    None,
    Equip,
    Remove
}

/// <summary>
///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror, same shape as
///     <see cref="MountZoneCommand" />.
/// </summary>
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
    TaskCompletionSource? Applied = null);

/// <summary>
///     Which AvatarStateFlag broadcast (if any) <see cref="Zone.ApplyStellarCoreCommand" /> sends for a
///     <see cref="StellarCoreZoneCommand" />.
/// </summary>
public enum StellarCoreBroadcastKind : byte
{
    None,
    Equip,
    Remove
}

/// <summary>Op 153 <c>StellarCoreState</c> self-mutation mirror, same shape as <see cref="CostumeZoneCommand" />.</summary>
public readonly record struct StellarCoreZoneCommand(
    int CharacterId,
    int? CoreIndex = null,
    int? CoreNumber = null,
    int? WardrobeSlotCleared = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    StellarCoreBroadcastKind Broadcast = StellarCoreBroadcastKind.None,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror, same shape as
///     <see cref="MountZoneCommand" />.
/// </summary>
public readonly record struct AvatarBuffZoneCommand(
    int CharacterId,
    int? StateTimeEffect = null,
    int? RankBuffType = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     Op 157 <c>RuneSocket</c> self-mutation mirror; economy-adjacent, always posted via
///     <see cref="Zone.PostRuneSocketCommandAndWaitAsync" />.
/// </summary>
public readonly record struct RuneSocketZoneCommand(
    int CharacterId,
    int RuneIndex,
    int? RuneItemId,
    int? RuneStat,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null);

/// <summary>Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) auto-buff registration + activation mirror.</summary>
public readonly record struct AutoBuffZoneCommand(
    int CharacterId,
    ImmutableArray<(int SkillId, int Grade)>? RegisteredSkills = null,
    int? ManaAfterActivation = null,
    int? ActionSort = null,
    bool Broadcast = false,
    TaskCompletionSource? Applied = null);
