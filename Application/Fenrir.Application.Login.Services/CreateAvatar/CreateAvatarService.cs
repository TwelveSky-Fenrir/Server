using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
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
///     literal legacy constants embedded directly in usp_Character_CreateWithStarterKit, not parameters here).
/// </remarks>
public sealed class CreateAvatarService(
    ICharacterRepository characters,
    IStarterKitRepository starterKits,
    ITribeRepository tribes,
    IOptions<LoginServerOptions> options)
    : ICreateAvatarService
{
    private const int StartLife = 100;
    private const int StartMaxLife = 100;
    private const int StartMana = 50;
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

    private const int WelcomeBuffDurationDays = 7; // DoubleExpTime1/2 + AutoBuffTime

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
                cancellationToken);

            // Guaranteed non-null: we just created this row within this request.
            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            // CharacterWorldEntryDto is a stable narrow prefix (see its own doc comment) that doesn't carry
            // stats/equipment/buffs/premium -- overlaid here from the exact values just persisted above rather
            // than added to that DTO, since every one of them is already known without a second round trip.
            var avatarInfo = AvatarInfoFactory.CreateForCharacter(character!) with
            {
                Vit = 1,
                Str = 1,
                Int = 1,
                Dex = 1,
                Equip = AvatarInfoFactory.BuildEquipArray(equipment),
                DoubleExpTime1 = welcomeBuffUntilDate,
                DoubleExpTime2 = welcomeBuffUntilDate,
                AutoBuffTime = welcomeBuffUntilDate,
                Premium = premiumUntilUnixSeconds
            };

            return new CreateAvatarResult(CreateAvatarOutcome.Success, avatarInfo);
        }
        catch (Exception)
        {
            // usp_Character_CreateWithStarterKit throws distinct codes (slot occupied/name taken), but the wire
            // contract only documents Result=1 for any failure -- the legacy client has no finer-grained handling.
            return new CreateAvatarResult(CreateAvatarOutcome.Failure, AvatarInfoFactory.Zeroed);
        }
    }

    /// <summary>
    ///     The client's raw weapon code must match one of the tribe's 3 catalogued RawWeaponCode values
    ///     (EquipSlot 7 rows) -- anything else is the same "not one of my 3 offered weapons" case the legacy
    ///     Quit()s on. The returned id is the row's ItemId, i.e. the elite weapon the raw code remaps to
    ///     (Server/ts25login/S04_MyWork02.cpp:773-778/801-806/829-834's `if (tWeapon == N) tWeapon = <elite id>`),
    ///     never the raw code itself.
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
}
