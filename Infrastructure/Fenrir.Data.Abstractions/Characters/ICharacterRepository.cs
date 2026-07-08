using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Data.Abstractions.Characters;

public interface ICharacterRepository
{
    public ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Richer companion to <see cref="GetByAccountAsync" /> for the CL_LOGIN_SEND avatar-roster response
    ///     (LC_USER_AVATAR_RECV2) -- see the "Account-login avatar roster population" legacy-behavior-translator
    ///     contract and usp_Character_GetAccountRoster.sql's own header for the exact field scope, and for what
    ///     is deliberately NOT sourced here (GuildName/Friend/Teacher/Student -- already resolved live by
    ///     IGuildRepository/IFriendRepository/IMentorRepository, the same composition EnterWorldService.
    ///     HandleAsync already uses for the single-character world-entry path; PetBag/Costume/CostumeIndex/
    ///     VisibleState/SpecialState -- no persisted storage exists anywhere in this schema yet). Does not
    ///     collapse an empty roster to null -- a brand-new account with zero characters is a legitimate result,
    ///     not a "not found" error (see <see cref="CharacterAccountRosterBundle" />'s own remarks).
    /// </summary>
    public ValueTask<CharacterAccountRosterBundle> GetAccountRosterAsync(int accountId, CancellationToken ct);

    public ValueTask<int> CreateAsync(
        int accountId,
        byte slot,
        string name,
        byte tribe,
        byte gender,
        byte headType,
        byte faceType,
        short mapId,
        float posX,
        float posY,
        float posZ,
        int life,
        int maxLife,
        int mana,
        int maxMana,
        CancellationToken ct);

    /// <summary>
    ///     Op17's full creation path: same slot/name guards as <see cref="CreateAsync" /> plus the EU33 starter kit
    ///     (stats, pet, welcome buffs, premium day, tribe equipment/inventory/skills/hotkeys) in one transaction.
    /// </summary>
    /// <param name="previousTribe">
    ///     The Noble Dragon/Royal Serpent/Grand Tiger starter-kit template (0-2) already used to select
    ///     <paramref name="equipment" />/<paramref name="skills" />/<paramref name="hotkeys" /> via
    ///     <see cref="IStarterKitRepository.GetByPreviousTribeAsync" /> -- genuinely independent of
    ///     <paramref name="tribe" /> (Server/ts25zone/S04_MyWork02.cpp:880-901's self-consistency check), now
    ///     persisted to <c>game.Characters.PreviousTribe</c> instead of only living in the caller's memory.
    ///     Defaults to 0 (append-only parameter, see Migrations/018_character_previous_tribe_and_mount_readpath.sql)
    ///     so existing callers that don't pass it yet keep compiling; a caller that already knows its real
    ///     previousTribe should pass it explicitly.
    /// </param>
    public ValueTask<int> CreateWithStarterKitAsync(
        int accountId,
        byte slot,
        string name,
        byte tribe,
        byte gender,
        byte headType,
        byte faceType,
        short mapId,
        float posX,
        float posY,
        float posZ,
        int life,
        int maxLife,
        int mana,
        int maxMana,
        int welcomeBuffUntilDate,
        long premiumUntilUnixSeconds,
        IReadOnlyList<CharacterItemSlotTvp> equipment,
        IReadOnlyList<CharacterItemSlotTvp> inventory,
        IReadOnlyList<CharacterSkillSlotTvp> skills,
        IReadOnlyList<CharacterHotkeySlotTvp> hotkeys,
        CancellationToken ct,
        byte previousTribe = 0);

    public ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct);

    public ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct);

    public ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct);

    /// <summary>
    ///     Narrow Life/Mana floor-clamp write (login-tail realignment guard) -- see
    ///     <c>Fenrir.Application.Login.Domain.Avatars.AvatarVitalsFloor</c> for the exact floor values and legacy
    ///     citation. Idempotent on the same per-character FlushSequence guard as
    ///     <see cref="PersistPositionsAsync" />/<see cref="PersistProgressAsync" />.
    /// </summary>
    public ValueTask ClampVitalsFloorAsync(int characterId, long flushSequence, int life, int mana,
        CancellationToken ct);

    public ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct);

    public ValueTask ReplaceContainerAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask ReplaceTwoContainersAsync(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotTvp> itemsB,
        CancellationToken ct);

    public ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows, CancellationToken ct);

    public ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct);

    /// <summary>
    ///     Atomic wallet/Store-money transfer (CZ_PROCESS_DATA_SEND tSort 226 deposit/227 withdraw) -- same-row
    ///     UPDATE, both columns guarded against going negative or past MAX_NUMBER_SIZE (2,000,000,000). Throws
    ///     SQL 50337 on an unknown character or an adjustment either column can't afford.
    /// </summary>
    public ValueTask AdjustStoreMoneyAsync(int characterId, long deltaMoney, long deltaStoreMoney,
        CancellationToken ct);

    public ValueTask AdjustMoneyAndReplaceContainerAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask AdjustMoneyAndReplaceTwoContainersAsync(int characterId, long deltaMoney,
        int deltaBigMoney, byte containerA, IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB, CancellationToken ct);

    public ValueTask UpsertSkillSlotAsync(int characterId, byte slotIndex, int skillId, int grade,
        CancellationToken ct);

    /// <summary>
    ///     Durable single-slot write to game.CharacterHotkeys -- mirrors <see cref="UpsertSkillSlotAsync" />'s
    ///     own shape. <paramref name="sort" />/<paramref name="value1" />/<paramref name="value2" /> are the
    ///     raw legacy triple verbatim, in game.CharacterHotkeys' own column order (see that table's DDL
    ///     comment for why this differs positionally from <c>HotkeySlot</c>'s C# (Kind, Value1, Value2) shape:
    ///     <paramref name="sort" /> is the bound id, <paramref name="value1" /> the secondary value
    ///     (grade/quantity), <paramref name="value2" /> the kind discriminator). A <paramref name="value2" />
    ///     of 0 (<c>HotkeyBindingKind.None</c>) deletes the row outright instead of writing a zeroed one,
    ///     matching "row absence = unassigned key."
    /// </summary>
    public ValueTask UpsertHotkeySlotAsync(int characterId, byte page, byte keyIndex, int sort, int value1,
        int value2, CancellationToken ct);

    /// <summary>
    ///     Atomic two-character trade commit. <paramref name="itemsA0" />/<paramref name="itemsA1" />/
    ///     <paramref name="itemsB0" />/<paramref name="itemsB1" /> are each side's FULL post-trade container
    ///     contents (a whole-container replace, not just the traded slots) and <paramref name="deltaMoneyA" />/
    ///     <paramref name="deltaMoneyB" /> are each side's NET money delta -- neither is enough on its own to
    ///     reconstruct what was actually offered when both sides contribute money in the same trade.
    /// </summary>
    /// <param name="tradedItemsA">
    ///     Character A's finalized trade-window offer only (legacy <c>ST_TRADE_INFO</c>, up to 8 slots,
    ///     <c>MAX_TRADE_SLOT_NUM</c>) -- for audit logging (<c>GL_615_TRADE_ITEM</c>/<c>GL_615_TRADE_ITEM2</c>),
    ///     distinct from <paramref name="itemsA0" />/<paramref name="itemsA1" />'s whole-container contents.
    ///     Defaults to null/omitted (no item audit rows) so existing callers that don't pass it yet keep
    ///     compiling -- see Migrations/037_trade_event_log.sql. Empty/null list = no occupied trade slots to log,
    ///     not an error.
    /// </param>
    /// <param name="tradedItemsB">Character B's finalized trade-window offer -- symmetric to <paramref name="tradedItemsA" />.</param>
    /// <param name="offeredMoneyA">
    ///     Character A's own finalized money offer (regular currency) placed into the trade window -- for audit
    ///     logging (<c>GL_616_TRADE_MONEY</c>) only, gated on being &gt; 0 together with
    ///     <paramref name="offeredBigMoneyA" />; NOT the same value as <paramref name="deltaMoneyA" />. Defaults
    ///     to 0 (no money audit row) so existing callers that don't pass it yet keep compiling.
    /// </param>
    /// <param name="offeredBigMoneyA">
    ///     Character A's own finalized "big"/premium-currency offer -- see
    ///     <paramref name="offeredMoneyA" />.
    /// </param>
    /// <param name="offeredMoneyB">Character B's own finalized money offer -- symmetric to <paramref name="offeredMoneyA" />.</param>
    /// <param name="offeredBigMoneyB">
    ///     Character B's own finalized "big"/premium-currency offer -- symmetric to
    ///     <paramref name="offeredBigMoneyA" />.
    /// </param>
    public ValueTask ExecuteTradeAsync(
        int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0, IReadOnlyList<CharacterItemSlotTvp> itemsA1,
        long deltaMoneyA, int deltaBigMoneyA,
        int characterB, IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1,
        long deltaMoneyB, int deltaBigMoneyB,
        CancellationToken ct,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsA = null,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsB = null,
        long offeredMoneyA = 0, int offeredBigMoneyA = 0,
        long offeredMoneyB = 0, int offeredBigMoneyB = 0);

    public ValueTask ApplyQuestTransitionAsync(int characterId, int stepPermanent, int activeQuestId,
        int qSort, int targetPhase, int killCounter, long deltaMoney,
        byte? container1, IReadOnlyList<CharacterItemSlotTvp> items1,
        byte? container2, IReadOnlyList<CharacterItemSlotTvp> items2,
        CancellationToken ct);

    public ValueTask ApplyDailyMissionClaimAsync(int characterId, int joinWar, int killOtherTribe,
        int killMonster, int playTime, byte? container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct);

    public ValueTask SetAutoPotionThresholdAsync(int characterId, byte autoLifeRatio, byte autoManaRatio,
        CancellationToken ct);

    public ValueTask SetAutoHuntAsync(int characterId, bool enabled, byte[] config, CancellationToken ct);

    public ValueTask SetPetGrowthAsync(int characterId, int petGrowth, byte petActivity, CancellationToken ct);

    public ValueTask<int?> GetIdByNameAsync(string name, CancellationToken ct);

    /// <summary>
    ///     Targeted single-slot read: the ItemId currently at (<paramref name="container" />,
    ///     <paramref name="slot" />) for this character, or null if that slot is empty. Used by op19's rename-
    ///     scroll gate (CL_CHANGE_AVATAR_NAME_SEND) -- a full container/world-entry read would be wasteful for
    ///     checking a single claimed slot.
    /// </summary>
    public ValueTask<int?> GetItemIdAtSlotAsync(int characterId, byte container, byte slot, CancellationToken ct);

    public ValueTask<RewardClaimStateDto?> GetRewardClaimStateAsync(int characterId, int todayDate,
        CancellationToken ct);

    public ValueTask ClaimDailyRewardAsync(int characterId, int todayDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask<int> SpendBloodCoinAndReplaceContainerAsync(int characterId, int deltaBloodCoin,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask ExecutePshopPurchaseAsync(int sellerCharacterId, byte sellerContainer,
        IReadOnlyList<CharacterItemSlotTvp> sellerItems, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, int price, CancellationToken ct);

    /// <summary>
    ///     game.Characters.TribeTransferPermitCount -- banks (or, if <paramref name="delta" /> is negative,
    ///     spends) Faction Transfer Scroll permits. Returns the balance after the adjustment.
    /// </summary>
    public ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct);

    /// <summary>
    ///     game.Characters.ProtectForDeath -- the decrementing "aProtectForDeath" death-protection shield
    ///     counter (Server/ts25zone/S07_MyGame02.cpp:3443-3489). A qualifying monster-kill death consumes
    ///     exactly one charge (<paramref name="delta" /> = -1) in place of that death's CP/XP penalty; the
    ///     shield-vs-penalty decision itself lives in the calling domain code, not here. Same forward-compat
    ///     posture as <see cref="GrantTribeTransferPermitAsync" />'s own <c>delta</c> -- a positive delta is
    ///     equally valid (a future GM grant or protection-charge item), on top of the fixed starting grant of
    ///     5 already applied by <c>usp_Character_CreateWithStarterKit</c>. Returns the balance after the
    ///     adjustment. Throws SQL 50332 on an unknown character or an adjustment that would take
    ///     ProtectForDeath negative.
    /// </summary>
    public ValueTask<int> AdjustDeathProtectionAsync(int characterId, int delta, CancellationToken ct);

    /// <summary>
    ///     game.Characters.Zone241Time -- the "aZone241Time" counter (Database/Tables/game/Characters.sql's own
    ///     column comment for the column's citation). First durable consumer is the legacy-behavior-translator
    ///     Rebirth-advancement contract's Path B ("Max Rebirth", CZ_TRIBE_WORK_SEND tSort 11), whose legacy
    ///     success branch does <c>aZone241Time += 10</c> (Server/ts25zone/S04_MyWork02.cpp:11342-11390). Second
    ///     consumer is the quest-and-daily-systems contract's DailyMission-claim behavior ("Claim", opcode 126)
    ///     side effect 5, whose legacy success branch does <c>aZone241Time += 1</c> when the avatar is exactly
    ///     at the second-tier level cap (Server/ts25zone/S04_MyWork02.cpp:14538-14606,14611-14618) --
    ///     <c>DailyMissionService.ClaimAsync</c>'s own <c>GrantSecondTierZone241TimeBonusAsync</c>. Same
    ///     forward-compat <paramref name="delta" />-sign posture as <see cref="AdjustDeathProtectionAsync" />/
    ///     <see cref="GrantTribeTransferPermitAsync" /> -- this is a dumb delta-adjust primitive; whether an
    ///     increment should be applied unconditionally on passing preconditions (the legacy's own quirk) or
    ///     only on an actual successful rebirth transition (the contract's own hardening recommendation) is
    ///     the calling domain code's decision, not enforced here. Returns the balance after the adjustment.
    ///     Throws SQL 50336 on an unknown character or an adjustment that would take Zone241Time negative.
    /// </summary>
    public ValueTask<int> AdjustZone241TimeAsync(int characterId, int delta, CancellationToken ct);

    /// <summary>
    ///     Book of Noble Dragon/Royal Serpent/Grand Tiger V2 tribe-conversion mechanic (world.Items
    ///     99014/99015/99016) -- see usp_Character_ApplyTribeConversion.sql's own header for the full
    ///     precondition list, THROW codes (50313-50320), and -- importantly -- which 3 preconditions the
    ///     CALLER must still verify itself (zone-37 cluster connectivity, standing in the character's own
    ///     current tribe's capital, no active party) since none of those has any durable representation in
    ///     this database. <paramref name="itemId" /> alone (99014/99015/99016) derives the target tribe and
    ///     re-validates the level gate server-side; the caller never supplies a target tribe directly.
    ///     <paramref name="container" />/<paramref name="items" /> is a whole-container replace of the ONE
    ///     inventory container the book was consumed from (same empty-list-omission rule as
    ///     <see cref="ReplaceContainerAsync" />), with the book's own slot simply absent from
    ///     <paramref name="items" />.
    /// </summary>
    /// <remarks>
    ///     Does NOT persist the worn-costume-wardrobe remap, auto-buff skill slots, or the two auto-hunt
    ///     skill-slot families on success -- none of those three have durable storage in this schema yet
    ///     (see the stored procedure's own header); a caller needing byte-exact parity there must remap them
    ///     itself using <see cref="Fenrir.Data.Abstractions.World.IWorldDataRepository.GetTribeConversionCatalogAsync" />'s
    ///     own equivalence data.
    /// </remarks>
    public ValueTask ApplyTribeConversionAsync(int characterId, int itemId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    /// <summary>
    ///     CZ_CHANGE_TO_TRIBE4_SEND (op37) success -- the fourth-tribe (Fujin) conversion/return behavior.
    ///     Atomically writes game.Characters.Tribe plus the 5-slot inline quest state (mirroring
    ///     <see cref="ApplyQuestTransitionAsync" />'s own column shape, minus that method's money/container
    ///     parameters -- this behavior never touches money, items, or containers). Distinct from, and must
    ///     never be conflated with, <see cref="ApplyTribeConversionAsync" /> -- that is the unrelated
    ///     skill-book-item-driven swap between tribes 0/1/2, which DOES remap equipment/skills; this one is a
    ///     routing move into/out of the neutral tribe-3 pool and touches neither.
    /// </summary>
    public ValueTask ApplyTribeFourConversionAsync(int characterId, byte newTribe, int stepPermanent,
        int activeQuestId, int qSort, int targetPhase, int killCounter, CancellationToken ct);
}
