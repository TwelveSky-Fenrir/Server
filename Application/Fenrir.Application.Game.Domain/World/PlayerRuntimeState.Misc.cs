using System.Collections.Immutable;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    private static readonly ImmutableArray<(int ItemId, int Count)> DefaultBottleSlots =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

    /// <summary>
    ///     aVisibleState -- 1 = normally visible (the value every character is created/enters world with), 0 =
    ///     GM-hidden (legacy <c>IsHiding()</c>, <c>Server/ts25zone/H07_MyGame.h:971</c>). Session-scoped only:
    ///     no <c>game.Characters</c> column exists to load this from at world entry (see
    ///     <c>EnterWorldService</c>'s own self-spawn-packet remarks), so every zone (re)entry starts a
    ///     character visible regardless of whatever a GM last set here in a previous session. Mutated only by
    ///     the GM "Basic"-tier (<see cref="Fenrir.Network.Dispatch.Zone.Sessions.GmCommandTier.Basic" />)
    ///     HIDE/SHOW self-commands (tSort 501/502) -- see
    ///     <see cref="Fenrir.Application.Game.Services.Gm.IGmBasicCommandService" />.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:933-958.</remarks>
    public int VisibleState { get; set; } = 1;

    /// <summary>
    ///     aSpecialState -- grep-verified (this session, mirroring the originating behavior contract's own
    ///     sweep) to have no read/branch consumer anywhere in the zone-server source tree: written at world
    ///     entry (always 0), by the GM "Basic"-tier EQUIP/UNEQUIP self-commands (tSort 511/512, values 1/0) and
    ///     the NCHAT/YCHAT by-name-target commands (tSort 516/517, values 2/0 -- on the TARGET's own record,
    ///     not the invoking GM's), sent outbound in one packing routine, and persisted to the database -- but
    ///     never consulted in any conditional. Functions as a display/persisted value only, not a mechanism;
    ///     session-scoped only here, same "no persisted column yet" posture as <see cref="VisibleState" />. See
    ///     <see cref="Fenrir.Application.Game.Services.Gm.IGmBasicCommandService" />.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:1265-1298 (EQUIP/UNEQUIP), :1411-1468 (NCHAT/YCHAT) ;
    ///     Server/ts25zone/S04_MyWork02.cpp:1000 (world-entry write) ; Server/ts25zone/S05_MyTransfer.cpp:224
    ///     (outbound pack site) ; Server/Header/CSQLAvatar.cpp:559 (DB-persistence site).
    /// </remarks>
    public int SpecialState { get; set; }

    /// <summary>
    ///     aUseOrnament. Session-scoped only -- not yet loaded from/flushed to game.Characters (no persisted column
    ///     exists yet).
    /// </summary>
    public bool UseOrnament { get; set; }

    /// <summary>
    ///     aProtectForHalo -- a consumable charge that absorbs one "halo -1" downgrade. Same open issue as
    ///     <see cref="UseOrnament" />: session-scoped only.
    /// </summary>
    public int ProtectForHalo { get; set; }

    /// <summary>
    ///     <c>mTickCountCPRFC</c> -- same-tick reentry guard on CZ_TRIBE_WORK_SEND tSort 7 (halo enchant, see
    ///     <see cref="Tribes.TribeHaloEnchantResolver" />). Legacy compares this against the server's own
    ///     current logic-tick value with strict equality and disconnects on a match; re-expressed here as a
    ///     sub-<see cref="Simulation.SimulationClock.LegacyTick" /> wall-clock window, matching
    ///     <see cref="LastEnchantAttemptUtc" />'s own translation of the identical
    ///     request-thread/Zone-tick-thread split (<c>Tribes.TribeActionService.HaloEnchantAsync</c> runs on
    ///     the request thread, not the Zone tick thread that owns the only Zone-wide simulated clock in this
    ///     codebase). Defaulted to <see cref="DateTime.UtcNow" /> at construction time, same posture as
    ///     <see cref="LastEnchantAttemptUtc" /> -- NOT a literal port of legacy's "initialized to the current
    ///     tick at avatar registration" (S04_MyWork02.cpp:871), which this default already achieves
    ///     structurally, since a freshly (re)registered avatar constructs a fresh <see cref="PlayerRuntimeState" />
    ///     at that same moment.
    /// </summary>
    public DateTime LastHaloEnchantAttemptUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     aBonusItemLevel -- which level-up milestone's bonus-item claim is pending. Armed by
    ///     <c>Zone.ApplyCharacterExperienceGain</c>'s own level-milestone step
    ///     (<c>Progression.LevelMilestoneBonus.ResolveHighestMilestoneCrossed</c>) the moment a level-up
    ///     crosses one of <c>Progression.LevelMilestoneBonus.ArmableMilestoneLevels</c>, and cleared back
    ///     to 0 by <c>TribeActionService.ClaimLevelBonusAsync</c> (tSort 8) once claimed. Session-scoped only --
    ///     no <c>game.Characters</c> column backs this yet, so an armed-but-unclaimed milestone does not survive
    ///     logout or a zone transfer today; full parity needs a persisted column plus
    ///     <see cref="PlayerEnterData" /> hydration (see that record's own "must travel here or it
    ///     resets" remarks for the pattern to follow).
    /// </summary>
    public int BonusItemLevel { get; set; }

    /// <summary>aBonusItemValue -- companion flag to <see cref="BonusItemLevel" />, same posture.</summary>
    public bool BonusItemValue { get; set; }

    /// <summary>
    ///     aTribeNotifyNum -- remaining CZ_TRIBE_NOTIFY_SEND (opcode 112) "announcement scroll" charges.
    ///     Session-scoped only; always 0 until the UseInventoryItem scroll-item family (op 23) is implemented,
    ///     same open issue as <see cref="BonusItemLevel" />.
    /// </summary>
    public int TribeNotifyScrollCount { get; set; }

    /// <summary>
    ///     aPreviousTribe -- the character's real, persisted <c>game.Characters.PreviousTribe</c> value (0-2, DB
    ///     CHECK-constrained), hydrated at world entry/zone transfer via <see cref="World.PlayerEnterData.PreviousTribe" />.
    ///     Equal to <see cref="Tribe" /> for every main-faction (0-2) character; only genuinely differs for a
    ///     tribe-3 (Fujin) character, who carries their original tribe here -- see
    ///     <c>EnterWorldService.IsTribeAndPreviousTribeConsistent</c> for the world-entry gate that guarantees
    ///     this invariant before a player is ever added to a zone. Never itself mutated by the fourth-tribe
    ///     (Fujin) conversion/return behavior (CZ_CHANGE_TO_TRIBE4_SEND, op37) -- that behavior only reads this
    ///     field to resolve which tribe a returning Fujin member came from; it permanently records "original
    ///     faction" and does not toggle between <see cref="Tribe" />'s two possible values the way <see cref="Tribe" />
    ///     itself does.
    /// </summary>
    public byte PreviousTribe { get; set; }

    /// <summary>
    ///     The per-character fourth-tribe (Fujin) RETURN allowance -- gates
    ///     <c>Tribes.TribeMigrationGate</c>'s return branch (converting FROM tribe 3 back to <see cref="PreviousTribe" />).
    ///     Session-scoped only, same "no producer wired up yet" posture as <see cref="BonusItemLevel" />/
    ///     <see cref="TribeNotifyScrollCount" /> -- the legacy counter this mirrors
    ///     (<c>Server/ts25zone/S04_MyWork03.cpp:5904-5908</c>) never had a working grant path in the shipped
    ///     game either (see the "Fourth-tribe (Fujin) conversion and return" behavior contract's own Edge
    ///     Cases). Always 0 until a separate, explicitly product-reviewed feature grants a charge -- this field
    ///     deliberately has no producer here, only the gate/consumption (decrement-on-success) side.
    /// </summary>
    public int TribeFourReturnAllowance { get; set; }

    /// <summary>
    ///     aBottle/aBottleCount (10 slots, ItemId 0 = empty). Session-scoped only -- same open issue as
    ///     <see cref="UseOrnament" />, no persisted column exists yet. Populated by UseInventoryItemHandler's
    ///     iSort==26 family, consumed/decremented by DrinkBottleHandler.
    /// </summary>
    public ImmutableArray<(int ItemId, int Count)> BottleSlots { get; set; } = DefaultBottleSlots;

    /// <summary>
    ///     True while this character has a live personal-shop stall open. Deliberately not set for a
    ///     proxy/offline shop (that state lives in game.OfflineShops instead, since it must keep working while
    ///     this character is offline). A seller's copy is only ever mutated by a different character's (the
    ///     buyer's) request thread through <see cref="Social.Pshop.PshopZoneCommand" />, routed onto the
    ///     seller's own zone tick.
    /// </summary>
    public bool PshopOpen { get; set; }

    /// <summary>
    ///     The currently-advertised stall listing while <see cref="PshopOpen" /> is true; stale/meaningless otherwise
    ///     (not cleared on close).
    /// </summary>
    public PshopInfo? PshopListing { get; set; }

    /// <summary>
    ///     Legacy <c>mDATA.mFishingState</c> (0/1), zone 52 only. Session-only: the legacy itself keeps this on
    ///     <c>MyUser</c>, not <c>AVATAR_INFO</c>, so no persisted column exists or is needed.
    /// </summary>
    public int FishingState { get; set; }

    /// <summary>Legacy <c>mDATA.mFishingStep</c> (0..5). Same session-only posture as <see cref="FishingState" />.</summary>
    public int FishingStep { get; set; }

    /// <summary>
    ///     Legacy <c>mFishingTickCount</c> -- gates the ~1-minute poll-bite window (CZ_FISHING_RESULT_SEND Sort=1).
    ///     Deliberately wall-clock (unlike most other <c>*AtZoneClock</c> fields): the elapsed check is resolved
    ///     on the request thread reading this field directly, which cannot see Zone's private simulated clock.
    ///     Null = never cast.
    /// </summary>
    public DateTime? FishingCastAtUtc { get; set; }

    /// <summary>
    ///     Legacy <c>mCatchingFish</c> -- true only after a bite (step 4/5), gates CZ_FISHING_REWARD_SEND. Cleared
    ///     unconditionally by every CZ_FISHING_STATE_SEND (cast/reel), matching the legacy exactly.
    /// </summary>
    public bool CatchingFish { get; set; }
}
