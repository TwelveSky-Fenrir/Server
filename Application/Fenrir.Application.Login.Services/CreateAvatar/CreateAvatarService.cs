using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Services.CreateAvatar;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 business logic -- creates a new character in the requested slot, grants the EU33
///     starter kit (tribe equipment/inventory/skills/hotkeys, stats, pet, welcome buffs, one premium day) and builds
///     the full AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183 (non-USE_CUSTOME_CREATE branch, the one EU33/LNW33
///     builds compile) ; Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation).
/// </remarks>
public sealed class CreateAvatarService(ICharacterRepository characters, IStarterKitRepository starterKits)
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
        var mapId = SpawnMapIdByTribe[tribe];

        var kit = await starterKits.GetByPreviousTribeAsync(previousTribe, mapId, cancellationToken);

        if (!TryResolveWeaponItemId(kit.Equipment, weapon, out var weaponItemId))
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
    ///     Weapon codes double as their own item ids under EU33's non-USE_CUSTOME_CREATE branch, so the client's
    ///     choice must literally match one of the tribe's 3 catalogued weapon rows (EquipSlot 7) -- anything else
    ///     is the same "not one of my 3 offered weapons" case the legacy Quit()s on.
    /// </summary>
    private static bool TryResolveWeaponItemId(IReadOnlyList<StarterKitEquipmentRowDto> equipment,
        int requestedWeapon, out int weaponItemId)
    {
        foreach (var row in equipment)
            if (row.EquipSlot == WeaponEquipSlot && row.ItemId == requestedWeapon)
            {
                weaponItemId = row.ItemId;
                return true;
            }

        weaponItemId = 0;
        return false;
    }

    /// <summary>Armor/Gloves/Boots + the one chosen Weapon from the tribe catalog, plus the universal Cape/Pet.</summary>
    private static List<CharacterItemSlotTvp> BuildEquipmentRows(IReadOnlyList<StarterKitEquipmentRowDto> catalog,
        int weaponItemId)
    {
        var rows = new List<CharacterItemSlotTvp>(catalog.Count + 2);

        foreach (var row in catalog)
        {
            if (row.EquipSlot == WeaponEquipSlot && row.ItemId != weaponItemId)
                continue; // the 2 un-chosen weapon alternatives

            rows.Add(new CharacterItemSlotTvp(row.EquipSlot, row.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
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
