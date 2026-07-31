using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Login.Avatars;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.CreateAvatar;

public sealed class CreateAvatarService(
    ICharacterRepository characters,
    IStarterKitRepository starterKits,
    ITribeRepository tribes,
    ILogger<CreateAvatarService> logger)
    : ICreateAvatarService
{
    private const int StartLevel = 1;
    private const int StartLife = 30;
    private const int StartMana = 21;

    private const int StartMaxLife = 100;
    private const int StartMaxMana = 50;

    private const int SqlErrorNameAlreadyTaken = 50202;

    private const byte ArmorEquipSlot = 2;
    private const byte WeaponEquipSlot = 7;

    private const byte StarterGearEnchant = 0;
    private const byte StarterGearCombine = 0;

    private const int WelcomeBuffDurationDays = 7;

    private const int StartingStatPoint = 50;
    private const int StartingSkillPoint = 0;

    private const int WelcomeDoubleExpTimeCounter = 300;
    private const int WelcomeAutoHuntMinutes = 1440;
    private const int WelcomeDeathProtectionStacks = 5;
    private const int WelcomePremiumGrantDays = 1;

    private const byte PetGrantEquipSlot = 8;
    private const int PetGrantItemId = 1016;
    private const int PetGrowthGrant = 640_000_000;
    private const byte PetActivityGrant = 100;

    private const byte CapeGrantEquipSlot = 1;
    private const int CapeGrantItemId = 1403;
    private const byte CapeGrantEnchant = 40;

    private const byte WingGrantEquipSlot = 10;

    private const int MountGrantItemId = 1314;
    private const int MountGrantPower = 10;
    private const int MountGrantTime = 99999999;

    private static int _starterGrantSerialSequence;

    private static readonly short[] SpawnMapIdByTribe = [1, 6, 11, 140];

    private static readonly int[] WingGrantItemIdByPreviousTribe = [216, 217, 218];

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

        if (FourthFactionGate.BlocksCreation(tribe))
            return new CreateAvatarResult(CreateAvatarOutcome.FourthFactionDisabled, AvatarInfoFactory.Zeroed);

        if (!AvatarNameValidator.HasOnlyWhitelistedCharacters(avatarName))
            return new CreateAvatarResult(CreateAvatarOutcome.Failure, AvatarInfoFactory.Zeroed);

        if (TribeDominanceGate.BlocksCreation(tribe, await tribes.GetAllAsync(cancellationToken)))
            return new CreateAvatarResult(CreateAvatarOutcome.DominantTribeBlocked, AvatarInfoFactory.Zeroed);

        var mapId = SpawnMapIdByTribe[tribe];

        var kit = await starterKits.GetByPreviousTribeAsync(previousTribe, mapId, cancellationToken);

        var weaponItemId = 0;
        if (previousTribe is 0 or 1 or 2 && !TryResolveWeaponItemId(kit.Equipment, weapon, out weaponItemId))
            return new CreateAvatarResult(CreateAvatarOutcome.InvalidWeapon, AvatarInfoFactory.Zeroed);

        var equipment = BuildEquipmentRows(kit.Equipment, weaponItemId);
        equipment.AddRange(BuildUnconditionalStarterGrantRows(previousTribe));
        var inventory = BuildInventoryRows(kit.Inventory);
        var skills = BuildSkillRows(kit.Skills);
        var hotkeys = BuildHotkeyRows(kit.Hotkeys);

        var welcomeBuffUntilDate = TodayPlusDays(WelcomeBuffDurationDays);
        var premiumUntilUnixSeconds = DateTimeOffset.UtcNow.AddDays(WelcomePremiumGrantDays).ToUnixTimeSeconds();

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
                Equip = AvatarInfoFactory.BuildEquipArray(equipment, PetGrowthGrant, PetActivityGrant),
                Inventory = AvatarInfoFactory.BuildInventoryArray(inventory),
                Skill = AvatarInfoFactory.BuildSkillArray(skills),
                HotKey = AvatarInfoFactory.BuildHotKeyArray(hotkeys),
                Animal = [MountGrantItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                AnimalIndex = 0,
                AnimalTime = MountGrantTime,
                AnimalPower = [MountGrantPower, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                AutoBuffTime = welcomeBuffUntilDate,
                ProtectForDeath = WelcomeDeathProtectionStacks,
                AutoTime2 = WelcomeAutoHuntMinutes,
                DoubleExpTime1 = WelcomeDoubleExpTimeCounter,
                DoubleExpTime2 = WelcomeDoubleExpTimeCounter,
                Premium = premiumUntilUnixSeconds
            };

            return new CreateAvatarResult(CreateAvatarOutcome.Success, avatarInfo);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorNameAlreadyTaken)
        {
            logger.LogWarning(ex,
                "Character creation rejected for account {AccountId} slot {AvatarPost}: name {AvatarName} already taken",
                accountId, avatarPost, avatarName);
            return new CreateAvatarResult(CreateAvatarOutcome.NameTaken, BuildUnpersistedCandidate());
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Character creation failed for account {AccountId} slot {AvatarPost} name {AvatarName}",
                accountId, avatarPost, avatarName);
            return new CreateAvatarResult(CreateAvatarOutcome.Failure, BuildUnpersistedCandidate());
        }

        AvatarInfo BuildUnpersistedCandidate()
        {
            return AvatarInfoFactory.Zeroed with
            {
                Name = avatarName,
                Tribe = tribe,
                Gender = gender,
                HeadType = head,
                FaceType = face,
                Level1 = StartLevel,
                LogoutInfo =
                [
                    mapId,
                    (int)(kit.Spawn?.PosX ?? 0f),
                    (int)(kit.Spawn?.PosY ?? 0f),
                    (int)(kit.Spawn?.PosZ ?? 0f),
                    StartLife,
                    StartMana
                ],
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
                Equip = AvatarInfoFactory.BuildEquipArray(equipment, PetGrowthGrant, PetActivityGrant),
                Inventory = AvatarInfoFactory.BuildInventoryArray(inventory),
                Skill = AvatarInfoFactory.BuildSkillArray(skills),
                HotKey = AvatarInfoFactory.BuildHotKeyArray(hotkeys),
                AutoBuffTime = welcomeBuffUntilDate
            };
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

    private static List<CharacterItemSlotTvp> BuildUnconditionalStarterGrantRows(byte previousTribe)
    {
        List<CharacterItemSlotTvp> rows =
        [
            new(PetGrantEquipSlot, PetGrantItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, GenerateStarterGrantSerial()),
            new(CapeGrantEquipSlot, CapeGrantItemId, 1, CapeGrantEnchant, 0, 0, 0, 0, 0, 0, 0,
                GenerateStarterGrantSerial())
        ];

        if (previousTribe < WingGrantItemIdByPreviousTribe.Length)
            rows.Add(new CharacterItemSlotTvp(WingGrantEquipSlot, WingGrantItemIdByPreviousTribe[previousTribe], 1, 0,
                0, 0, 0, 0, 0, 0, 0, GenerateStarterGrantSerial()));

        return rows;
    }

    private static int GenerateStarterGrantSerial()
    {
        var sequence = Interlocked.Increment(ref _starterGrantSerialSequence);
        return unchecked((int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + sequence));
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
