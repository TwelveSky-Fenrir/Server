using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.CreateAvatar;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 business logic -- creates a new character in the requested slot, grants the EU33
///     starter kit (tribe equipment/inventory/skills/hotkeys, stats, pet, welcome buffs, one premium day) and builds
///     the full AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183 -- USE_CUSTOME_CREATE branch (see
///     Migrations/015_starter_kit_elite_grant.sql's header comment for the full citation: this macro is
///     force-defined, unguarded, at S04_MyWork02.cpp:1, before any other header, bypassing the M33/LNW33EU
///     build-variant chain every OTHER file in the codebase is subject to -- so the `#else`/`#ifdef` arms
///     below are what actually ships in every build configuration, not the `#ifndef` arm a prior version of
///     this file assumed) ; Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation) ;
///     Server/ts25login/S04_MyWork02.cpp:625-635 (the combined slot-occupied/name-empty test -- name-empty is
///     handled upstream by CreateAvatarHandler, slot-occupancy is checked here) ;
///     Server/ts25login/S04_MyWork02.cpp:635-646 (the fourth-faction/Tribe-value-3 creation exclusion -- see
///     <see cref="FourthFactionGate" /> for the gate itself and <see cref="LoginServerOptions.EnableFourthFaction" />
///     for the operator toggle) ; Server/ts25login/S04_MyWork02.cpp:739-838 (the PreviousTribe/race switch has no
///     case-3/default branch -- weapon validation only runs for PreviousTribe 0/1/2, see
///     <see cref="TryResolveWeaponItemId" />'s call site) ; Server/ts25login/S04_MyWork02.cpp:1100-1179 (starting
///     level/stats/skill points, the six-category Enchant/Combine grant, and the pet/cape/mount grant -- all
///     literal legacy constants embedded directly in usp_Character_CreateWithStarterKit, not parameters here) ;
///     Server/ts25login/S04_MyWork02.cpp:1094-1097 (the current-life/current-mana literals, 30/21, and the
///     DEFINE.h:751-756 write-macro confirming those are the logout-info slots this session's own current
///     values, not any maximum) ; Server/ts25login/S04_MyWork02.cpp:1174-1179 (the universal starter mount --
///     item 1301/DEFINE.h:157 ANIMAL_NUM_TIGER1, "5%" power, slot 0, permanent expiry) ;
///     Server/ts25login/S04_MyWork02.cpp:885-892 (DoubleExpTime1/DoubleExpTime2 vs AutoBuffTime are two
///     independently-sourced legacy fields, not the same value applied twice -- see the DoubleExpTime1/2
///     assignment below for why this is flagged, not yet fixed) ;
///     Server/ts25zone/S04_MyWork02.cpp:880-901 (the Tribe/PreviousTribe self-consistency check zone entry
///     performs against the persisted PreviousTribe this method now threads through to
///     <see cref="ICharacterRepository.CreateWithStarterKitAsync" /> instead of leaving it un-persisted).
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
    // current life/current mana. Universal across every race/tribe/gender -- these are deliberately low
    // relative to a near-max-level, fully-elite-geared character's real maximum (the character is topped
    // up, or its true maximums recomputed, on first entry into the world), not a mistranscribed number.
    private const int StartLife = 30;
    private const int StartMana = 21;

    // MaxLife/MaxMana have no creation-time legacy value at all -- the legacy engine never persists a
    // maximum, it recomputes both dynamically every time a relevant input changes, from a multi-factor
    // formula (vitality/ki, gear, elixir, level, mount tier, pet, item-set bonuses --
    // Server/Header/Protocol/MyFactor.cpp:1902-2010,2226-2357). These two constants are therefore a known
    // architecture-mismatch placeholder, not a legacy-cited value; treat neither number as authoritative --
    // a dedicated MyFactor-formula contract is needed before any concrete number or persistence strategy is
    // chosen (bridge-stats-equipment contract, gap-table row 11).
    private const int StartMaxLife = 100;
    private const int StartMaxMana = 50;

    // FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h): the slots BuildEquipmentRows writes beyond the tribe catalog.
    private const byte CapeEquipSlot = 1;
    private const byte WeaponEquipSlot = 7;
    private const byte PetEquipSlot = 8;

    private const int StarterCapeItemId = 1407; // Flying Wizard, 120% (legacy: SetISIUIMValue(40,0,0,0))
    private const byte StarterCapeEnchant = 40;
    private const int StarterPetItemId = 2300; // Yin Yang Free; PetGrowth/PetActivity live on game.Characters itself

    // Server/ts25login/S04_MyWork02.cpp:1112-1120: SetISIUIMValue(45, 6, 0, 0) on the six elite-gear
    // categories (Weapon/Armor/Gloves/Ring/Boots/Amulet, i.e. every StarterKitEquipmentRowDto row) -- despite
    // the adjacent "Enchant[20%]" comment at line 1112, the literal first argument is 45, not 20.
    private const byte EliteGearEnchant = 45;
    private const byte EliteGearCombine = 6;

    // Server/ts25login/S04_MyWork02.cpp:1174-1179 (item id: DEFINE.h:157 ANIMAL_NUM_TIGER1): the universal
    // starter mount -- one tiger mount, slot 0, "5%" power tier, already select/equipped, effectively-
    // permanent expiry. Identical to the literals 016_starter_kit_create_atomicity.sql bakes into
    // MountItemId/MountExpActivity/MountPower/MountSlotIndex/MountTime -- game.Characters.Mount* isn't
    // projected onto CharacterWorldEntryDto (see its own doc comment), so this is a second independent
    // literal overlaid onto the immediate response, the same "known at creation time, not read back"
    // pattern as Vit/Str/Int/Dex below.
    private const int StarterMountItemId = 1301;
    private const int StarterMountExpActivity = 0;
    private const int StarterMountPower = 5;
    private const int StarterMountSlotIndex = 0;
    private const int StarterMountTime = 99999999;

    // AutoBuffTime is genuinely this many days out (S04_MyWork02.cpp:892, "Starting Auto Buff Scroll: 7
    // days") -- confirmed legacy-accurate. DoubleExpTime1/DoubleExpTime2 are a SEPARATE, confirmed-wrong
    // concern applied to the same value below -- see that assignment's own comment for why it isn't fixed
    // in this pass.
    private const int WelcomeBuffDurationDays = 7;

    private const int PremiumDurationDays = 1;

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
        // (Noble Dragon/Royal Serpent/Grand Tiger) and no case-3/default branch -- any other PreviousTribe value
        // is a genuine legacy validation gap: no weapon check runs, the request is not rejected on this basis,
        // and the weapon-equip slot is simply left unassigned rather than the client's requested weapon.
        var weaponItemId = 0;
        if (previousTribe is 0 or 1 or 2 && !TryResolveWeaponItemId(kit.Equipment, weapon, out weaponItemId))
            return new CreateAvatarResult(CreateAvatarOutcome.InvalidWeapon, AvatarInfoFactory.Zeroed);

        var equipment = BuildEquipmentRows(kit.Equipment, weaponItemId);
        var inventory = BuildInventoryRows(kit.Inventory);
        var skills = BuildSkillRows(kit.Skills);
        var hotkeys = BuildHotkeyRows(kit.Hotkeys);

        var welcomeBuffUntilDate = TodayPlusDays(WelcomeBuffDurationDays);
        var premiumUntilUnixSeconds = DateTimeOffset.UtcNow.AddDays(PremiumDurationDays).ToUnixTimeSeconds();

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
                premiumUntilUnixSeconds,
                equipment,
                inventory,
                skills,
                hotkeys,
                cancellationToken,
                previousTribe);

            // Guaranteed non-null: we just created this row within this request.
            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            // CharacterWorldEntryDto is a stable narrow prefix (see its own doc comment) that doesn't carry
            // stats/equipment/buffs/premium/PreviousTribe/Mount* -- overlaid here instead of added to that DTO,
            // since every one of these is already known without a second round trip.
            var avatarInfo = AvatarInfoFactory.CreateForCharacter(character!) with
            {
                // Independent fixed literal constants (S04_MyWork02.cpp:748-751), applied unconditionally
                // before the race switch -- NOT read back from the row above, they simply equal the same
                // literals CreateWithStarterKitAsync also persisted.
                Vit = 1,
                Str = 1,
                Int = 1,
                Dex = 1,
                // game.Characters.PreviousTribe (Migrations/018_character_previous_tribe_and_mount_readpath.sql)
                // isn't projected onto CharacterWorldEntryDto either -- already known here as the request's
                // own (unvalidated-by-design, see this method's own remarks) parameter.
                PreviousTribe = previousTribe,
                Equip = AvatarInfoFactory.BuildEquipArray(equipment),
                Animal = SingleMountSlotArray(StarterMountItemId),
                AnimalIndex = StarterMountSlotIndex,
                AnimalTime = StarterMountTime,
                AnimalPower = SingleMountSlotArray(StarterMountPower),
                AnimalExpActivity = SingleMountSlotArray(StarterMountExpActivity),
                // DoubleExpTime1/DoubleExpTime2 vs AutoBuffTime is a CONFIRMED-WRONG collapse, not fixed here:
                // legacy sets DoubleExpTime1/2 to a fixed raw counter (300, S04_MyWork02.cpp:886-887) that is
                // NOT date-derived, while AutoBuffTime genuinely is this 7-day-future date (S04_MyWork02.cpp:
                // 892) -- but usp_Character_CreateWithStarterKit only exposes one @WelcomeBuffUntilDate
                // parameter for all three columns (016_starter_kit_create_atomicity.sql:74,77), and the exact
                // unit/encoding of that 300 literal was not independently confirmed. Both a new stored-
                // procedure parameter and a fresh legacy-research pass on the literal's meaning are needed
                // before this can change (bridge-stats-equipment contract, gap-table row 12) -- deliberately
                // not substituted blind.
                DoubleExpTime1 = welcomeBuffUntilDate,
                DoubleExpTime2 = welcomeBuffUntilDate,
                AutoBuffTime = welcomeBuffUntilDate,
                Premium = premiumUntilUnixSeconds
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
    ///     Quit()s on. The returned id is the row's ItemId, i.e. the elite weapon the raw code remaps to
    ///     (Server/ts25login/S04_MyWork02.cpp:773-778/801-806/829-834's `if (tWeapon == N) tWeapon =
    ///     <elite id>
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
    ///     Amulet/Armor/Gloves/Ring/Boots + the one chosen Weapon from the tribe's elite catalog (each granted
    ///     with the six-category Enchant/Combine encoding, see <see cref="EliteGearEnchant" />), plus the
    ///     universal Cape/Pet.
    /// </summary>
    private static List<CharacterItemSlotTvp> BuildEquipmentRows(IReadOnlyList<StarterKitEquipmentRowDto> catalog,
        int weaponItemId)
    {
        var rows = new List<CharacterItemSlotTvp>(catalog.Count + 2);

        foreach (var row in catalog)
        {
            if (row.EquipSlot == WeaponEquipSlot && row.ItemId != weaponItemId)
                continue; // the 2 un-chosen weapon alternatives

            rows.Add(new CharacterItemSlotTvp(row.EquipSlot, row.ItemId, 1, EliteGearEnchant, EliteGearCombine, 0, 0,
                0, 0, 0, 0, 0));
        }

        rows.Add(new CharacterItemSlotTvp(CapeEquipSlot, StarterCapeItemId, 1, StarterCapeEnchant, 0, 0, 0, 0, 0, 0, 0,
            0));
        rows.Add(new CharacterItemSlotTvp(PetEquipSlot, StarterPetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

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

    /// <summary>
    ///     Places a single value at <see cref="StarterMountSlotIndex" /> of an otherwise-zeroed 10-slot array --
    ///     the shared shape of AVATAR_INFO's Animal/AnimalPower/AnimalExpActivity arrays, all of which the wire
    ///     format sizes at 10 possible owned-mount slots even though creation only ever grants one.
    /// </summary>
    private static int[] SingleMountSlotArray(int value)
    {
        var slots = new int[10];
        slots[StarterMountSlotIndex] = value;
        return slots;
    }
}
