using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.CreateAvatar;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 business logic -- creates a new character in the requested slot at Level 1,
///     grants a basic weapon + torso-armor starter kit (plus the tribe's base inventory/skills/hotkeys) and
///     builds the full AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     CONFIRMED PRODUCT DECISION (character-creation-level1-redesign workflow, explicit user instruction --
///     NOT a legacy-parity fix): new characters start at Level 1 with only one weapon and one torso/chest
///     armor piece equipped. This deliberately replaces the EU33/USE_CUSTOME_CREATE instant-elite creation
///     grant Server/ts25login/S04_MyWork02.cpp:740-1179 actually compiles (force-defined unconditionally, no
///     matching #undef anywhere under Server/ -- see Database/Migrations/027_character_create_level1_basic_kit.sql's
///     own header for the full literal-by-literal rationale on the SQL side). That EU33 grant is real,
///     heavily cited, and was a deliberate, correctly-implemented byte-exact wire-parity choice; this class no
///     longer replicates it, so none of its constants below should be read as legacy citations for a starting
///     value -- each one says explicitly whether it is legacy-sourced or a Fenrir product default.
///     <para>
///         What legacy behavior IS still honored (unaffected by the redesign, kept exactly as before):
///         Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation, spawn map per Tribe) ;
///         Server/ts25login/S04_MyWork02.cpp:625-635 (the combined slot-occupied/name-empty test -- name-empty
///         is handled upstream by CreateAvatarHandler, slot-occupancy is checked here) ;
///         Server/ts25login/S04_MyWork02.cpp:635-646 (the fourth-faction/Tribe-value-3 creation exclusion --
///         see <see cref="FourthFactionGate" /> for the gate itself and
///         <see cref="LoginServerOptions.EnableFourthFaction" /> for the operator toggle) ;
///         Server/ts25login/S04_MyWork02.cpp:739-838 (the PreviousTribe/race switch has no case-3/default
///         branch -- weapon validation only runs for PreviousTribe 0/1/2, see
///         <see cref="TryResolveWeaponItemId" />'s call site) ;
///         Server/ts25login/S04_MyWork02.cpp:1094-1097 (the current-life/current-mana literals, 30/21, and the
///         DEFINE.h:751-756 write-macro confirming those are the logout-info slots' own current values, not
///         any maximum -- see <see cref="StartLife" />/<see cref="StartMana" />'s own remarks for why these
///         remain legacy-accurate even under a genuine Level 1 character) ;
///         Server/ts25login/S04_MyWork02.cpp:892 ("Starting Auto Buff Scroll: 7 days" -- the welcome-buff/
///         second-inventory-page/second-store-page rental grant, independent of the EU33 instant-elite block
///         and kept as-is by this redesign).
///     </para>
///     <para>
///         What this redesign REMOVES entirely (no longer granted, no successor value): the six-category
///         elite-gear Enchant/Combine stamp (Gloves/Ring/Boots/Amulet dropped outright; Weapon/Armor now
///         Enchant 0/Combine 0), the universal Cape and Pet grants, the universal starter mount, the
///         ProtectForDeath/AutoTime2/DoubleExpTime1/DoubleExpTime2 "instant boost" counters, and the one-day
///         premium grant. See each removed constant's git history / this class's own prior revision for the
///         legacy citations that justified them under the EU33 design this class no longer implements.
///     </para>
///     <para>
///         Known deferred gap (Game-side, explicitly out of this LoginServer-scoped workflow's remit):
///         <c>Fenrir.Application.Game.Domain.Avatars.AvatarInfoFactory.MaxGeneralExperience</c> still
///         unconditionally reports <c>Exp1 = 2,000,000,000</c> on every world entry and zone transfer, on the
///         explicit (now-stale) assumption that every character is created at the general-level cap. A
///         freshly created Level 1 character's <c>Exp1</c> will misreport under that Game-side factory until
///         a follow-up change (outside this workflow) teaches it to reflect the character's own persisted
///         Experience instead of a fixed constant.
///     </para>
/// </remarks>
public sealed class CreateAvatarService(
    ICharacterRepository characters,
    IStarterKitRepository starterKits,
    ITribeRepository tribes,
    IOptions<LoginServerOptions> options,
    ILogger<CreateAvatarService> logger)
    : ICreateAvatarService
{
    // S04_MyWork02.cpp:1096-1097; DEFINE.h:751-756's write-macro confirms these logout-info slots are
    // current life/current mana. Universal across every race/tribe/gender. Under the old EU33 design these
    // were documented as "deliberately low relative to a near-max-level character's real maximum"; under
    // this redesign's genuine Level 1 character they are simply the correct current life/mana outright, so
    // no reinterpretation was needed when the rest of this class moved off EU33.
    private const int StartLife = 30;
    private const int StartMana = 21;

    // MaxLife/MaxMana have no creation-time legacy value at all -- the legacy engine never persists a
    // maximum, it recomputes both dynamically every time a relevant input changes, from a multi-factor
    // formula (vitality/ki, gear, elixir, level, mount tier, pet, item-set bonuses --
    // Server/Header/Protocol/MyFactor.cpp:1902-2010,2226-2357). These two constants are therefore a known
    // architecture-mismatch placeholder, not a legacy-cited value; treat neither number as authoritative --
    // a dedicated MyFactor-formula contract is needed before any concrete number or persistence strategy is
    // chosen (bridge-stats-equipment contract, gap-table row 11). Unaffected by the level-1 redesign.
    private const int StartMaxLife = 100;
    private const int StartMaxMana = 50;

    // FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h): the two slots the redesigned BuildEquipmentRows grants.
    // Weapon (client-selected, validated via TryResolveWeaponItemId) and Armor/torso (the tribe's single
    // catalogued EquipSlot=2 row) -- Amulet(0)/Gloves(3)/Ring(4)/Boots(5), the Cape, and the Pet slots are no
    // longer granted at all under this redesign.
    private const byte ArmorEquipSlot = 2;
    private const byte WeaponEquipSlot = 7;

    // Fenrir product default (NOT legacy-cited): a freshly created Level 1 character's weapon and torso
    // armor are granted completely unenchanted/uncombined -- a genuine fresh start, not the EU33
    // USE_CUSTOME_CREATE block's SetISIUIMValue(45, 6, 0, 0) elite-gear stamp this redesign replaces.
    private const byte StarterGearEnchant = 0;
    private const byte StarterGearCombine = 0;

    // AutoBuffTime is genuinely this many days out (S04_MyWork02.cpp:892, "Starting Auto Buff Scroll: 7
    // days") -- confirmed legacy-accurate, independent of the EU33 instant-elite block and kept as-is by
    // this redesign (also drives InventoryDate/StoreDate, the second-inventory-page/second-store-page rental
    // grant -- see usp_Character_CreateWithStarterKit's own header).
    private const int WelcomeBuffDurationDays = 7;

    // Fenrir product defaults (NOT legacy-cited): no compiled non-USE_CUSTOME_CREATE branch exists anywhere
    // in the reviewed Server/ts25login source to draw a level-1 starting stat/skill-point pool from -- the
    // USE_CUSTOME_CREATE macro is force-defined unconditionally in every build (see this class's own
    // <remarks>), so there is no "vanilla creation" literal to fall back to. Chosen instead:
    //   * StartingStatPoint = 50 gives a new character enough to make one real early build choice (e.g. a
    //     meaningful Strength investment) without reproducing anything close to the old 3175-point EU33
    //     pool. Application/Fenrir.Application.Game.Stats/LevelProgressionCalculator.cs:59-63's own level
    //     1-to-2 transition grants 5 stat points under the general (non-rebirth) ladder -- offered only as a
    //     loose anchor for "small, early-game-appropriate," not a mandate that this exact number is somehow
    //     legacy-sourced.
    //   * StartingSkillPoint = 0: the starter kit already grants every starting skill directly via
    //     game.CharacterSkills (BuildSkillRows/world.StarterKitSkills), so no separate spendable pool is
    //     needed at Level 1.
    private const int StartingStatPoint = 50;
    private const int StartingSkillPoint = 0;

    // Fenrir product default (NOT legacy-cited): usp_Character_CreateWithStarterKit still declares
    // @PremiumUntilUnixSeconds (signature parity with the unchanged ICharacterRepository.
    // CreateWithStarterKitAsync call shape -- see Database/Migrations/027_character_create_level1_basic_kit.
    // sql's own header) but no longer writes it into PremiumExpireUtc; this fixed 0 is what gets passed for
    // it now that no premium-day grant happens at creation.
    private const long NoPremiumGrant = 0L;

    // GetReturnBornInTownLocation: Tribe (the playable faction, 0-3) decides the spawn map. PreviousTribe (the
    // Noble Dragon/Royal Serpent/Grand Tiger starting-kit template, 0-2) separately decides equipment/inventory/
    // skills/hotkeys below -- the two fields are independent on the wire and both are honored independently here.
    private static readonly short[] SpawnMapIdByTribe = [1, 6, 11, 140];

    public async ValueTask<CreateAvatarResult> CreateAvatarAsync(
        int accountId,
        byte avatarPost,
        string avatarName,
        byte tribe,
        byte previousTribe,
        byte gender,
        byte head,
        byte face,
        int weapon,
        CancellationToken cancellationToken)
    {
        // Legacy order (S04_MyWork02.cpp): the combined slot-occupied/name-empty test (625-635) runs before the
        // fourth-faction exclusion (640-646), which in turn runs before the dominant-tribe gate (724-730) --
        // checked in that same order here. Only slot-occupancy is checked below; the request's name is already
        // known non-empty by this point (CreateAvatarHandler rejects an empty name before this method runs).
        var existingCharacters = await characters.GetByAccountAsync(accountId, cancellationToken);
        if (existingCharacters.Any(character => character.Slot == avatarPost))
            return new CreateAvatarResult(CreateAvatarOutcome.SlotOccupied, AvatarInfoFactory.Zeroed);

        if (FourthFactionGate.BlocksCreation(tribe, options.Value.EnableFourthFaction))
            return new CreateAvatarResult(CreateAvatarOutcome.FourthFactionDisabled, AvatarInfoFactory.Zeroed);

        // Cross-executable read path: Login has no shared-memory segment with Game (unlike the legacy
        // single-process design -- see TribeDominanceGate's <remarks>), so this reads game.WorldStateTribes.Points
        // through the same ITribeRepository/usp_Tribe_GetAll surface Game's own tribe features already use --
        // no new table, no new schema. Game's WorldStateService caches tribe points in memory and only persists
        // them via a write-behind flush every 5s (Fenrir.Application.Game.Hosting.World.WorldState.
        // WorldStateWriteBehindHost.Interval), so this read can lag the true in-memory standings by up to that
        // interval; the legacy shared-memory read had no such delay. Accepted here as a bounded, documented
        // deviation rather than adding a synchronous cross-process call on the avatar-creation path.
        if (TribeDominanceGate.BlocksCreation(tribe, await tribes.GetAllAsync(cancellationToken)))
            return new CreateAvatarResult(CreateAvatarOutcome.DominantTribeBlocked, AvatarInfoFactory.Zeroed);

        var mapId = SpawnMapIdByTribe[tribe];

        var kit = await starterKits.GetByPreviousTribeAsync(previousTribe, mapId, cancellationToken);

        // The weapon-vs-race matching switch (S04_MyWork02.cpp:739-838) only has cases for PreviousTribe 0/1/2
        // (Noble Dragon/Royal Serpent/Grand Tiger) and no case-3/default branch -- legacy itself grants nothing
        // for any other value rather than rejecting the request on this basis. CreateAvatarHandler now
        // range-checks PreviousTribe to 0-2 before ever calling this method (see its own remarks for the full
        // citation and the CK_Characters_PreviousTribe DB constraint that motivated it), so in practice
        // previousTribe is always 0/1/2 by the time this line runs; the `is 0 or 1 or 2` guard below is kept as
        // defense-in-depth for any other caller of this public service method, not because an out-of-range
        // value is still expected to reach here from the handler.
        var weaponItemId = 0;
        if (previousTribe is 0 or 1 or 2 && !TryResolveWeaponItemId(kit.Equipment, weapon, out weaponItemId))
            return new CreateAvatarResult(CreateAvatarOutcome.InvalidWeapon, AvatarInfoFactory.Zeroed);

        var equipment = BuildEquipmentRows(kit.Equipment, weaponItemId);
        var inventory = BuildInventoryRows(kit.Inventory);
        var skills = BuildSkillRows(kit.Skills);
        var hotkeys = BuildHotkeyRows(kit.Hotkeys);

        var welcomeBuffUntilDate = TodayPlusDays(WelcomeBuffDurationDays);

        try
        {
            var characterId = await characters.CreateWithStarterKitAsync(
                accountId,
                avatarPost,
                avatarName,
                tribe,
                gender,
                head,
                face,
                mapId,
                kit.Spawn?.PosX ?? 0f,
                kit.Spawn?.PosY ?? 0f,
                kit.Spawn?.PosZ ?? 0f,
                StartLife,
                StartMaxLife,
                StartMana,
                StartMaxMana,
                welcomeBuffUntilDate,
                NoPremiumGrant,
                equipment,
                inventory,
                skills,
                hotkeys,
                cancellationToken,
                previousTribe);

            // Guaranteed non-null: we just created this row within this request.
            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            // CharacterWorldEntryDto is a stable narrow prefix (see its own doc comment) that doesn't carry
            // stats/equipment/buffs/PreviousTribe -- overlaid here instead of added to that DTO, since every
            // one of these is already known without a second round trip. Mount/premium/ProtectForDeath/
            // AutoTime2/DoubleExpTime1/DoubleExpTime2 are deliberately NOT overlaid here anymore: this
            // redesign grants none of them, and AvatarInfoFactory.Zeroed's own 0/empty-array defaults already
            // match that "nothing granted" reality, so there is nothing left to mirror onto the response the
            // way the old EU33 design needed (its own literals were never read back from the row above
            // either -- see this class's git history for that prior overlay).
            var avatarInfo = AvatarInfoFactory.CreateForCharacter(character!) with
            {
                // Independent fixed literal constants (S04_MyWork02.cpp:748-751), applied unconditionally
                // before the race switch -- NOT read back from the row above, they simply equal the same
                // literals CreateWithStarterKitAsync also persisted. Genuinely legacy-accurate even for a
                // Level 1 character (every tribe/gender/previous-tribe starts every base stat at exactly 1).
                Vit = 1,
                Str = 1,
                Int = 1,
                Dex = 1,
                // A genuine Level 1 character: no post-cap "high level" ladder progress (Level2), no
                // experience of either kind (Exp1/Exp2). NOT read back from the row above, for the same
                // reason Vit/Str/Int/Dex above aren't -- these simply equal the literals
                // usp_Character_CreateWithStarterKit also persists.
                Level2 = 0,
                Exp1 = 0,
                Exp2 = 0,
                // See StartingStatPoint/StartingSkillPoint's own remarks for the full "Fenrir product
                // default, not legacy-cited" reasoning.
                StatPoint = StartingStatPoint,
                SkillPoint = StartingSkillPoint,
                // game.Characters.PreviousTribe (Migrations/018_character_previous_tribe_and_mount_readpath.sql)
                // isn't projected onto CharacterWorldEntryDto either -- already known here as the request's
                // own (unvalidated-by-design, see this method's own remarks) parameter.
                PreviousTribe = previousTribe,
                // No petGrowth/petActivity arguments: BuildEquipArray defaults both to 0, matching this
                // redesign's "no pet is ever granted" reality (see BuildEquipmentRows' own remarks).
                Equip = AvatarInfoFactory.BuildEquipArray(equipment),
                Inventory = AvatarInfoFactory.BuildInventoryArray(inventory),
                Skill = AvatarInfoFactory.BuildSkillArray(skills),
                HotKey = AvatarInfoFactory.BuildHotKeyArray(hotkeys),
                AutoBuffTime = welcomeBuffUntilDate
            };

            return new CreateAvatarResult(CreateAvatarOutcome.Success, avatarInfo);
        }
        catch (Exception ex)
        {
            // usp_Character_CreateWithStarterKit throws distinct codes (slot occupied/name taken), but the wire
            // contract only documents Result=1 for any failure -- the legacy client has no finer-grained handling.
            // Previously swallowed with no trace at all; logged here (the only place the exception itself is
            // still in scope) so a real starter-kit failure is diagnosable instead of vanishing silently.
            logger.LogError(ex,
                "Character creation failed for account {AccountId} slot {AvatarPost} name {AvatarName}",
                accountId, avatarPost, avatarName);
            return new CreateAvatarResult(CreateAvatarOutcome.Failure, AvatarInfoFactory.Zeroed);
        }
    }

    /// <summary>
    ///     The client's raw weapon code must match one of the tribe's 3 catalogued RawWeaponCode values
    ///     (EquipSlot 7 rows) -- anything else is the same "not one of my 3 offered weapons" case the legacy
    ///     Quit()s on. The returned id is the row's ItemId, i.e. the weapon the raw code remaps to
    ///     (Server/ts25login/S04_MyWork02.cpp:773-778/801-806/829-834's `if (tWeapon == N) tWeapon =
    ///     <id>
    ///         `),
    ///         never the raw code itself.
    /// </summary>
    private static bool TryResolveWeaponItemId(IReadOnlyList<StarterKitEquipmentRowDto> equipment,
        int requestedWeapon, out int weaponItemId)
    {
        foreach (var row in equipment)
            if (row.EquipSlot == WeaponEquipSlot && row.RawWeaponCode == requestedWeapon)
            {
                weaponItemId = row.ItemId;
                return true;
            }

        weaponItemId = 0;
        return false;
    }

    /// <summary>
    ///     Exactly two rows: the one chosen Weapon (EquipSlot 7, matched via <paramref name="weaponItemId" />)
    ///     and the tribe's single Armor/torso row (EquipSlot 2, world.StarterKitEquipment's FEQUIP_TYPE::EARMOR
    ///     row) -- both stamped with <see cref="StarterGearEnchant" />/<see cref="StarterGearCombine" />
    ///     (0/0, unenchanted). Amulet(0)/Gloves(3)/Ring(4)/Boots(5) rows in <paramref name="catalog" />, plus
    ///     the universal Cape/Pet this method used to append, are no longer granted at all under this
    ///     redesign -- see this class's own &lt;remarks&gt; for the full "what this redesign removes" list.
    /// </summary>
    private static List<CharacterItemSlotTvp> BuildEquipmentRows(IReadOnlyList<StarterKitEquipmentRowDto> catalog,
        int weaponItemId)
    {
        var rows = new List<CharacterItemSlotTvp>(2);

        foreach (var row in catalog)
        {
            if (row.EquipSlot == WeaponEquipSlot && row.ItemId != weaponItemId)
                continue; // the 2 un-chosen weapon alternatives

            if (row.EquipSlot != WeaponEquipSlot && row.EquipSlot != ArmorEquipSlot)
                continue; // Amulet/Gloves/Ring/Boots -- no longer granted

            rows.Add(new CharacterItemSlotTvp(row.EquipSlot, row.ItemId, 1, StarterGearEnchant, StarterGearCombine, 0,
                0, 0, 0, 0, 0, 0));
        }

        return rows;
    }

    private static List<CharacterItemSlotTvp> BuildInventoryRows(IReadOnlyList<StarterKitInventoryRowDto> catalog)
    {
        var rows = new List<CharacterItemSlotTvp>(catalog.Count);

        foreach (var row in catalog)
            rows.Add(new CharacterItemSlotTvp(row.SlotIndex, row.ItemId, row.Quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        return rows;
    }

    private static List<CharacterSkillSlotTvp> BuildSkillRows(IReadOnlyList<StarterKitSkillRowDto> catalog)
    {
        var rows = new List<CharacterSkillSlotTvp>(catalog.Count);

        foreach (var row in catalog)
            rows.Add(new CharacterSkillSlotTvp(row.SlotIndex, row.SkillId, row.Grade));

        return rows;
    }

    private static List<CharacterHotkeySlotTvp> BuildHotkeyRows(IReadOnlyList<StarterKitHotkeyRowDto> catalog)
    {
        var rows = new List<CharacterHotkeySlotTvp>(catalog.Count);

        foreach (var row in catalog)
            rows.Add(new CharacterHotkeySlotTvp(row.Page, row.KeyIndex, row.Sort, row.Value1, row.Value2));

        return rows;
    }

    /// <summary>
    ///     Legacy datetime.h::ReturnAddDate(0, days) -- YYYYMMDD, the same encoding Game's own GameDate.Today() uses
    ///     (Login has no reference to the Game project, so this is a deliberately independent one-liner, not a
    ///     copy-paste).
    /// </summary>
    private static int TodayPlusDays(int days)
    {
        var future = DateTime.UtcNow.AddDays(days);
        return future.Year * 10000 + future.Month * 100 + future.Day;
    }
}
