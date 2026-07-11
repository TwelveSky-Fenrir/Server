using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.CreateAvatar;

public sealed class CreateAvatarService(
    ICharacterRepository characters,
    IStarterKitRepository starterKits,
    ITribeRepository tribes,
    IOptions<LoginServerOptions> options,
    ILogger<CreateAvatarService> logger)
    : ICreateAvatarService
{
    private const int StartLife = 30;
    private const int StartMana = 21;

    private const int StartMaxLife = 100;
    private const int StartMaxMana = 50;

    private const byte ArmorEquipSlot = 2;
    private const byte WeaponEquipSlot = 7;

    private const byte StarterGearEnchant = 0;
    private const byte StarterGearCombine = 0;

    private const int WelcomeBuffDurationDays = 7;

    private const int StartingStatPoint = 50;
    private const int StartingSkillPoint = 0;

    private const long NoPremiumGrant = 0L;

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
        var existingCharacters = await characters.GetByAccountAsync(accountId, cancellationToken);
        if (existingCharacters.Any(character => character.Slot == avatarPost))
            return new CreateAvatarResult(CreateAvatarOutcome.SlotOccupied, AvatarInfoFactory.Zeroed);

        if (FourthFactionGate.BlocksCreation(tribe, options.Value.EnableFourthFaction))
            return new CreateAvatarResult(CreateAvatarOutcome.FourthFactionDisabled, AvatarInfoFactory.Zeroed);

        if (TribeDominanceGate.BlocksCreation(tribe, await tribes.GetAllAsync(cancellationToken)))
            return new CreateAvatarResult(CreateAvatarOutcome.DominantTribeBlocked, AvatarInfoFactory.Zeroed);

        var mapId = SpawnMapIdByTribe[tribe];

        var kit = await starterKits.GetByPreviousTribeAsync(previousTribe, mapId, cancellationToken);

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

            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            var avatarInfo = AvatarInfoFactory.CreateForCharacter(character!) with
            {
                Vit = 1,
                Str = 1,
                Int = 1,
                Dex = 1,
                Level2 = 0,
                Exp1 = 0,
                Exp2 = 0,
                StatPoint = StartingStatPoint,
                SkillPoint = StartingSkillPoint,
                PreviousTribe = previousTribe,
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
            logger.LogError(ex,
                "Character creation failed for account {AccountId} slot {AvatarPost} name {AvatarName}",
                accountId, avatarPost, avatarName);
            return new CreateAvatarResult(CreateAvatarOutcome.Failure, AvatarInfoFactory.Zeroed);
        }
    }

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

        private static List<CharacterItemSlotTvp> BuildEquipmentRows(IReadOnlyList<StarterKitEquipmentRowDto> catalog,
        int weaponItemId)
    {
        var rows = new List<CharacterItemSlotTvp>(2);

        foreach (var row in catalog)
        {
            if (row.EquipSlot == WeaponEquipSlot && row.ItemId != weaponItemId)
                continue;

            if (row.EquipSlot != WeaponEquipSlot && row.EquipSlot != ArmorEquipSlot)
                continue;

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

        private static int TodayPlusDays(int days)
    {
        var future = DateTime.UtcNow.AddDays(days);
        return future.Year * 10000 + future.Month * 100 + future.Day;
    }
}
