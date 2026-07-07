using System.Buffers;
using System.Collections.Immutable;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
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
    ///     Bounded capacity for <see cref="_autoBuffInbox" /> -- also the basis for
    ///     <see cref="AutoBuffInboxDrainCapPerTick" />.
    /// </summary>
    private const int AutoBuffInboxCapacity = 256;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_autoBuffInbox" /> -- same "half of this channel's own bounded
    ///     capacity" convention as <see cref="InboxDrainCapPerTick" /> (see that constant's own remarks for the
    ///     full rationale and the Fenrir-side-safeguard-not-legacy-parity caveat). Every channel declared in
    ///     this file follows the same pairing.
    /// </summary>
    private const int AutoBuffInboxDrainCapPerTick = AutoBuffInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_avatarBuffInbox" /> -- also the basis for
    ///     <see cref="AvatarBuffInboxDrainCapPerTick" />.
    /// </summary>
    private const int AvatarBuffInboxCapacity = 256;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_avatarBuffInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own
    ///     remarks.
    /// </summary>
    private const int AvatarBuffInboxDrainCapPerTick = AvatarBuffInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_bottleInbox" /> -- also the basis for
    ///     <see cref="BottleInboxDrainCapPerTick" />.
    /// </summary>
    private const int BottleInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_bottleInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int BottleInboxDrainCapPerTick = BottleInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_hotkeySlotInbox" /> -- also the basis for
    ///     <see cref="HotkeySlotInboxDrainCapPerTick" />.
    /// </summary>
    private const int HotkeySlotInboxCapacity = 256;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_hotkeySlotInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own
    ///     remarks.
    /// </summary>
    private const int HotkeySlotInboxDrainCapPerTick = HotkeySlotInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_costumeInbox" /> -- also the basis for
    ///     <see cref="CostumeInboxDrainCapPerTick" />.
    /// </summary>
    private const int CostumeInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_costumeInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int CostumeInboxDrainCapPerTick = CostumeInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_fishingInbox" /> -- also the basis for
    ///     <see cref="FishingInboxDrainCapPerTick" />.
    /// </summary>
    private const int FishingInboxCapacity = 512;

    /// <summary>Per-tick drain cap for <see cref="_fishingInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int FishingInboxDrainCapPerTick = FishingInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_heroRankingInbox" /> -- also the basis for
    ///     <see cref="HeroRankingInboxDrainCapPerTick" />.
    /// </summary>
    private const int HeroRankingInboxCapacity = 256;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_heroRankingInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own
    ///     remarks.
    /// </summary>
    private const int HeroRankingInboxDrainCapPerTick = HeroRankingInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_heroRankingRolloverInbox" /> -- also the basis for
    ///     <see cref="HeroRankingRolloverInboxDrainCapPerTick" />. In practice this cap can never bind: this
    ///     channel carries at most one real item per rollover event per zone (see the field's own remarks).
    /// </summary>
    private const int HeroRankingRolloverInboxCapacity = 4;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_heroRankingRolloverInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s
    ///     own remarks.
    /// </summary>
    private const int HeroRankingRolloverInboxDrainCapPerTick = HeroRankingRolloverInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_mountInbox" /> -- also the basis for <see cref="MountInboxDrainCapPerTick" />
    ///     .
    /// </summary>
    private const int MountInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_mountInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int MountInboxDrainCapPerTick = MountInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_pshopInbox" /> -- also the basis for <see cref="PshopInboxDrainCapPerTick" />
    ///     .
    /// </summary>
    private const int PshopInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_pshopInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int PshopInboxDrainCapPerTick = PshopInboxCapacity / 2;

    /// <summary>Bounded capacity for <see cref="_runeInbox" /> -- also the basis for <see cref="RuneInboxDrainCapPerTick" />.</summary>
    private const int RuneInboxCapacity = 256;

    /// <summary>Per-tick drain cap for <see cref="_runeInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own remarks.</summary>
    private const int RuneInboxDrainCapPerTick = RuneInboxCapacity / 2;

    /// <summary>
    ///     Bounded capacity for <see cref="_stellarCoreInbox" /> -- also the basis for
    ///     <see cref="StellarCoreInboxDrainCapPerTick" />.
    /// </summary>
    private const int StellarCoreInboxCapacity = 256;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_stellarCoreInbox" /> -- see <see cref="InboxDrainCapPerTick" />'s own
    ///     remarks.
    /// </summary>
    private const int StellarCoreInboxDrainCapPerTick = StellarCoreInboxCapacity / 2;

    /// <summary>
    ///     S011CHARACTER_MP -- the mana counterpart of <see cref="CharacterHpStatSort" />
    ///     (Server/Header/Protocol/STRUCT.h:1526).
    /// </summary>
    private const int CharacterMpStatSort = 11;

    /// <summary>
    ///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) auto-buff registration + activation
    ///     mirror. See <see cref="ApplyAutoBuffCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<AutoBuffZoneCommand> _autoBuffInbox =
        Channel.CreateBounded<AutoBuffZoneCommand>(
            new BoundedChannelOptions(AutoBuffInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Reusable scratch buffer for <see cref="ApplyAutoBuffCommand" />'s self-plus-neighbors broadcast
    ///     recipient list -- see <see cref="_fishingNeighborScratch" />'s own remarks for the reuse
    ///     justification.
    /// </summary>
    private readonly List<int> _autoBuffNeighborScratch = [];

    /// <summary>
    ///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror. See
    ///     <see cref="ApplyAvatarBuffCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<AvatarBuffZoneCommand> _avatarBuffInbox =
        Channel.CreateBounded<AvatarBuffZoneCommand>(
            new BoundedChannelOptions(AvatarBuffInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Op 129 <c>DrinkBottle</c> self-mutation mirror. See <see cref="ApplyDrinkBottleCommand" />'s remarks.</summary>
    private readonly Channel<DrinkBottleZoneCommand> _bottleInbox =
        Channel.CreateBounded<DrinkBottleZoneCommand>(
            new BoundedChannelOptions(BottleInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 90/139 (<c>CostumeState</c>/<c>CostumeVisibility</c>) self-mutation mirror. See
    ///     <see cref="ApplyCostumeCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<CostumeZoneCommand> _costumeInbox =
        Channel.CreateBounded<CostumeZoneCommand>(
            new BoundedChannelOptions(CostumeInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 103/104/105 (<c>FishingLine</c>/<c>FishingProgress</c>/<c>FishingCatch</c>) shared state-machine
    ///     mirror. See <see cref="ApplyFishingCommand" />'s remarks -- awaiting new PlayerRuntimeState fields.
    /// </summary>
    private readonly Channel<FishingZoneCommand> _fishingInbox =
        Channel.CreateBounded<FishingZoneCommand>(
            new BoundedChannelOptions(FishingInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Reusable scratch buffer for <see cref="ApplyFishingCommand" />'s self-plus-neighbors broadcast
    ///     recipient list -- same non-allocating shape and reuse justification as
    ///     <c>Zone.PlayerLifecycle.cs</c>'s own <c>_moveNeighborScratch</c>: single tick thread, cleared before
    ///     use, consumed entirely by the immediately-following broadcast call before this command's handling
    ///     returns.
    /// </summary>
    private readonly List<int> _fishingNeighborScratch = [];

    /// <summary>
    ///     Op 118 <c>HeroRanking</c> throttle-timestamp mirror -- the ranking query itself is read-only and answered
    ///     directly by the handler.
    /// </summary>
    private readonly Channel<HeroRankingQueryZoneCommand> _heroRankingInbox =
        Channel.CreateBounded<HeroRankingQueryZoneCommand>(
            new BoundedChannelOptions(HeroRankingInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Weekly hero-ranking Current-&gt;Previous rollover reset trigger -- posted once per successful flip by
    ///     <c>HeroRankingRolloverHost</c> (Hosting), once per hosted zone. Carries no per-instance data: draining
    ///     one just means "sweep every currently connected player once," see
    ///     <see cref="ApplyHeroRankingRolloverReset" />. A 4-slot capacity is generous for an event this rare
    ///     (weekly, at most one post per host check interval).
    /// </summary>
    private readonly Channel<HeroRankingRolloverZoneCommand> _heroRankingRolloverInbox =
        Channel.CreateBounded<HeroRankingRolloverZoneCommand>(
            new BoundedChannelOptions(HeroRankingRolloverInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op22 <c>CZ_USE_HOTKEY_ITEM_SEND</c> self-mutation mirror for <see cref="PlayerRuntimeState.Hotkeys" />
    ///     -- the posting handler (<c>UseHotkeyItemService</c>) already validated/decremented and persisted the
    ///     slot to SQL; this only mirrors that decision into the tick-owned <see cref="PlayerRuntimeState" />.
    ///     See <see cref="ApplyHotkeySlotMirrorCommand" />.
    /// </summary>
    private readonly Channel<HotkeySlotMirrorZoneCommand> _hotkeySlotInbox =
        Channel.CreateBounded<HotkeySlotMirrorZoneCommand>(
            new BoundedChannelOptions(HotkeySlotInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 87/113 (<c>MountState</c>/<c>MountAbsorb</c>) self-mutation mirror. See
    ///     <see cref="ApplyMountCommand" />'s remarks for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<MountZoneCommand> _mountInbox =
        Channel.CreateBounded<MountZoneCommand>(
            new BoundedChannelOptions(MountInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Fire-and-forget, purely cosmetic PShop-stall mirrors posted by <c>BuyShopItemHandler</c> onto the
    ///     seller's own hosting zone after a purchase already durably committed.
    /// </summary>
    private readonly Channel<PshopZoneCommand> _pshopInbox =
        Channel.CreateBounded<PshopZoneCommand>(
            new BoundedChannelOptions(PshopInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 157 <c>RuneSocket</c> self-mutation mirror. See <see cref="ApplyRuneSocketCommand" />'s remarks
    ///     for what this stub does/doesn't mirror yet.
    /// </summary>
    private readonly Channel<RuneSocketZoneCommand> _runeInbox =
        Channel.CreateBounded<RuneSocketZoneCommand>(
            new BoundedChannelOptions(RuneInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Op 153 <c>StellarCoreState</c> self-mutation mirror, same shape as <see cref="_costumeInbox" />. See
    ///     <see cref="ApplyStellarCoreCommand" />'s remarks.
    /// </summary>
    private readonly Channel<StellarCoreZoneCommand> _stellarCoreInbox =
        Channel.CreateBounded<StellarCoreZoneCommand>(
            new BoundedChannelOptions(StellarCoreInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostPshopCommand(in PshopZoneCommand command)
    {
        return _pshopInbox.Writer.TryWrite(command);
    }

    /// <summary>Same contract as <see cref="PostInventoryCommandAndWaitAsync" />.</summary>
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

    /// <summary>
    ///     No-op if the character already left -- a stale mirror for a departed character is harmless.
    ///     <see cref="HotkeySlotMirrorZoneCommand.NewLife" />/<see cref="HotkeySlotMirrorZoneCommand.NewMana" />
    ///     (op22 potion-type-1-5 life/mana gain, see <c>HotkeyItemConsumptionResolver</c>) are applied and
    ///     notified AFTER the slot mirror, matching the legacy handler's own "decrement/ack first, stat effect
    ///     second" ordering -- the ack itself is already sent by <c>UseHotkeyItemHandler</c> before this command
    ///     was ever posted.
    /// </summary>
    private void ApplyHotkeySlotMirrorCommand(in HotkeySlotMirrorZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        state.SetHotkeySlot(command.Page, command.Index, command.NewSlot);

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

        // Uses _fishingNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToList() + Add(...) --
        // see that field's own remarks. Self is appended at the tail (never excluded outright), preserving
        // this call site's existing "self included, last" recipient ordering exactly.
        var fisherId = command.CharacterId;
        _fishingNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_fishingNeighborScratch, state.CurrentCell, fisherId);
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

        // Rent-once/write-once/copy-N-times -- same idiom as BroadcastAvatarAction (Zone.PlayerLifecycle.cs):
        // the frame is encoded exactly once and copied into each recipient's own transport, with a
        // per-recipient failure isolated (logged, skipped) rather than aborting the rest of this broadcast.
        var total = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendAvatarStateFlagFrame(state.CharacterId, span);
            foreach (var neighborId in _grid.Neighbors(state.CurrentCell))
            {
                if (neighborId == state.CharacterId) continue;
                SendAvatarStateFlagFrame(neighborId, span);
            }
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

    /// <summary>
    ///     Op 97/111 (<c>PlaytimeBuff</c>/<c>RankBuff</c>) self-mutation mirror. Both opcodes' own
    ///     AvatarStatUpdateResponse self-unicast is sent directly by the handler (the value is already known
    ///     before the tick mirrors it, same posture as <c>DrinkBottleHandler</c>) -- this only mirrors the
    ///     already-decided state. <see cref="AvatarBuffZoneCommand.HealToMax" /> is deliberately a request
    ///     ("heal this character to their current cap"), not a literal life/mana value: the posting handler
    ///     runs on the session thread and cannot know the character's true <see cref="PlayerRuntimeState.MaxLife" />/
    ///     <see cref="PlayerRuntimeState.MaxMana" /> at the moment this command is actually drained here on the
    ///     tick thread (a concurrent buff expiry, gear change, or level-up could have moved the cap in between).
    ///     Deriving Life/Mana from <c>state.Stats</c>/<c>state.MaxLife</c>/<c>state.MaxMana</c> here, at apply
    ///     time, closes that stale-cap race -- see the items-skills-fenrir-self-critique finding this fixes.
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

    /// <summary>
    ///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) mirror. <c>RegisteredSkills</c> (op 94)
    ///     lands on <see cref="PlayerRuntimeState.AutoBuffSkill" />; <c>ActivateAutoBuff</c>/<c>ActionSort</c>
    ///     (op 95 <c>tSort==1</c>) mirror <see cref="PlayerRuntimeState.Mana" />/<see cref="PlayerRuntimeState.ActionSort" />
    ///     and, when <see cref="AutoBuffZoneCommand.Broadcast" /> is set, rebroadcast the resulting avatar action
    ///     to self + AOI neighbors -- same self-inclusive <c>Broadcast11</c> posture as <see cref="ApplyFishingCommand" />.
    ///     Op 95 <c>tSort==2</c>'s per-tick buff-application logic (<c>ProcessForCreateEffectValue</c>) is out of
    ///     scope -- see <see cref="Skills.AutoBuffActivationResolver" />'s remarks.
    /// </summary>
    /// <remarks>
    ///     <c>ActivateAutoBuff</c> is a request flag, not a literal post-activation mana value: the posting
    ///     handler's activate/no-reply/disconnect decision (<see cref="AutoBuffActivationResolver.Resolve" />)
    ///     still runs on the session thread against a snapshot read, but the actual 90%-mana debit is deferred
    ///     to and recomputed here, on the tick thread, against whatever <c>state.Mana</c> is *at apply time* via
    ///     <see cref="AutoBuffActivationResolver.ManaAfterActivation" />. This closes the stale-mana race the
    ///     previous "compute on the session thread, apply verbatim here" shape had -- see the
    ///     items-skills-fenrir-self-critique finding this fixes.
    /// </remarks>
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

        // Uses _autoBuffNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToList() + Add(...) --
        // see _fishingNeighborScratch's own remarks; same self-appended-at-tail ordering preserved.
        var casterId = command.CharacterId;
        _autoBuffNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_autoBuffNeighborScratch, state.CurrentCell, casterId);
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

    /// <summary>
    ///     No-op if the character left, or their stall was already re-opened/re-populated since (a stale mirror is
    ///     harmless). Otherwise clears the sold slot unconditionally, then delivers the seller-only
    ///     notifications the purchase itself never reaches without this hop -- B_BUY_PSHOP_RECV(6) "your item
    ///     sold", B_DEMAND_PSHOP_RECV(3) self-view listing refresh, and -- if that was the stall's last
    ///     occupied slot -- B_END_PSHOP_RECV(1) "your stall closed" plus an avatar-action broadcast to nearby
    ///     players (Server/ts25zone/S04_MyWork02.cpp:7067-7071,7096-7100,7102-7120). The buyer never receives
    ///     any message sent from here -- see <c>BuyShopItemHandler</c>'s own remarks.
    /// </summary>
    private void ApplyPshopCommand(in PshopZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state) || state.PshopListing is not { } listing)
            return;

        var itemInfo = listing.ItemInfo;
        var baseIndex = (command.Page * 5 + command.Slot) * 9;
        for (var k = 0; k < 9; k++)
            itemInfo[baseIndex + k] = 0;

        // B_BUY_PSHOP_RECV(6) "your item just sold" -- seller-only, carries the seller's own source-slot
        // coordinates and the sold item's value/socket details, already built by BuyShopItemService
        // (Server/ts25zone/S04_MyWork02.cpp:7070-7071).
        state.Session.Send(command.SellerSoldNotification);

        // B_DEMAND_PSHOP_RECV(3) self-view listing refresh -- seller-only. listing.ItemInfo is the same
        // backing array just cleared above, so this already reflects the post-clear state
        // (Server/ts25zone/S04_MyWork02.cpp:7099-7100).
        state.Session.Send(new ViewShopStallResponse { Result = 3, PshopInfo = listing });

        if (!command.CloseShop)
            return;

        // Stall fully emptied by this sale: clear the open flag, notify the seller with B_END_PSHOP_RECV(1),
        // and broadcast the shop-closed avatar-action cue to nearby players -- seller-only, never the buyer
        // (Server/ts25zone/S04_MyWork02.cpp:7116-7120).
        state.PshopOpen = false;
        state.Session.Send(new CloseShopStallResponse { Result = 1 });

        var characterId = command.CharacterId;
        SendAvatarAction(state.Session, state);
        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
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
///     Op22 <c>CZ_USE_HOTKEY_ITEM_SEND</c> hotkey-slot mirror -- see
///     <see cref="Zone.ApplyHotkeySlotMirrorCommand" />. <paramref name="NewSlot" /> is the full post-decrement
///     (or post-clear) content, already computed by <c>HotkeyItemConsumptionResolver</c>/<c>UseHotkeyItemService</c>.
///     <paramref name="NewLife" />/<paramref name="NewMana" /> are the absolute post-gain values (already
///     clamped by <c>HotkeyItemConsumptionResolver</c>) for potion types 1-5 -- null means "this pool was not
///     affected by this activation," matching <see cref="DrinkBottleZoneCommand.NewItemId" />'s own
///     null-means-unchanged convention.
/// </summary>
/// <summary>
///     <see cref="LifeGain" />/<see cref="ManaGain" /> are DELTAS applied against the LIVE
///     <see cref="PlayerRuntimeState.Life" />/<see cref="PlayerRuntimeState.Mana" /> at apply time
///     (<see cref="Zone.ApplyHotkeySlotMirrorCommand" />), not precomputed absolute values -- the posting
///     handler runs on the session thread and cannot see concurrent tick-thread mutations (combat damage,
///     HP/MP regen, another potion) between its snapshot read and this command's drain, so an absolute
///     value would silently clobber/revert whatever the tick thread applied in that window. Same
///     recompute-at-apply-time pattern already used by <see cref="AutoBuffZoneCommand" />/
///     <see cref="ApplyAutoBuffCommand" /> and <see cref="AvatarBuffZoneCommand" />/<see cref="ApplyAvatarBuffCommand" />.
/// </summary>
public readonly record struct HotkeySlotMirrorZoneCommand(
    int CharacterId,
    byte Page,
    byte Index,
    HotkeySlot NewSlot,
    int? LifeGain = null,
    int? ManaGain = null);

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
///     <see cref="MountZoneCommand" />. <see cref="HealToMax" /> is a request ("heal to whatever the current
///     cap is"), not a literal Life/Mana value -- see <see cref="Zone.ApplyAvatarBuffCommand" />'s remarks for
///     why the derivation must happen on the tick thread, at apply time, rather than on the posting session
///     thread.
/// </summary>
public readonly record struct AvatarBuffZoneCommand(
    int CharacterId,
    int? StateTimeEffect = null,
    int? RankBuffType = null,
    bool HealToMax = false,
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

/// <summary>
///     Op 94/95 (<c>ContinueSkillStat</c>/<c>ContinueSkillUse</c>) auto-buff registration + activation mirror.
///     <see cref="ActivateAutoBuff" /> is a request flag, not a precomputed post-activation mana value -- see
///     <see cref="Zone.ApplyAutoBuffCommand" />'s remarks for why the mana debit must be recomputed on the
///     tick thread, at apply time, rather than on the posting session thread.
/// </summary>
public readonly record struct AutoBuffZoneCommand(
    int CharacterId,
    ImmutableArray<(int SkillId, int Grade)>? RegisteredSkills = null,
    bool ActivateAutoBuff = false,
    int? ActionSort = null,
    bool Broadcast = false,
    TaskCompletionSource? Applied = null);
