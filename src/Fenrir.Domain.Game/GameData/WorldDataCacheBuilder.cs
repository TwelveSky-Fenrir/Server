using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Domain.Game.GameData;

public static class WorldDataCacheBuilder
{
    private const int MaxLevelIndex = 145;

    private const int MaxLevelCombatStat = 10000;

    private const int MaxExpRangeBound = 2_000_000_000;

    private const int MaxRangeInfo3 = 100;

    private const int MinQuestStep = 1;

    private const int MaxQuestStep = 1000;

    private const int MinQuestRewardAmount = 0;

    private const int MaxQuestRewardAmount = 100_000_000;

    private const int MaxQuestSpeechLength = 50;

    private const int MaxItemCheckDateItem = 365;

    private const int MaxItemDataNumber3D = 10000;

    private const int PotionType1RestrictedValue = 9;

    private const int MinPotionType2Restricted = 1;
    private const int MaxPotionType2Restricted = 3;
    private const int MaxPotionType2Default = MaxItemDataNumber3D;

    private const int MinGemSocketType = 1;

    private const int MaxGemSocketType = 46;

    private const int MaxMonsterNameLength = 24;
    private const int MaxMonsterChatLineLength = 100;
    private const int MinMonsterType = 1;
    private const int MaxMonsterType = 15;
    private const int MinMonsterSpecialType = 1;
    private const int MaxMonsterSpecialType = 53;
    private const int MinMonsterDamageType = 1;
    private const int MaxMonsterDamageType = 2;
    private const int MinMonsterDataSortNumber = 1;
    private const int MaxMonsterDataSortNumber = 10000;
    private const int MinMonsterSize = 1;
    private const int MaxMonsterSize = 1000;
    private const int MaxMonsterSize4 = 1000;
    private const int MinMonsterSizeCategory = 1;
    private const int MaxMonsterSizeCategory = 4;
    private const int MinMonsterCheckCollision = 1;
    private const int MaxMonsterCheckCollision = 2;
    private const int MaxMonsterHitCount = 3;
    private const int MinMonsterItemLevel = 1;
    private const int MaxMonsterItemLevel = 145;
    private const int MaxMonsterMartialItemLevel = 25;
    private const int MinMonsterRealLevel = 1;
    private const int MaxMonsterRealLevel = 1000;
    private const int MaxMonsterMartialRealLevel = 1000;
    private const int MaxMonsterExperienceReward = 100_000_000;
    private const int MinMonsterLife = 1;
    private const int MaxMonsterLife = 2_000_000_000;
    private const int MinMonsterAttackType = 1;
    private const int MaxMonsterAttackType = 6;
    private const int MaxMonsterRadiusInfo = 10000;
    private const int MaxMonsterMovementSpeed = 1000;
    private const int MaxMonsterAttackPower = 1_000_000;
    private const int MaxMonsterCombatStat = 100_000;
    private const int MaxMonsterCritical = 100;
    private const int MaxMonsterFollowInfo = 100;
    private const int MinMonsterSummonTime = 1;
    private const int MaxMonsterSummonTime = 1_000_000;
    private const int MinMonsterFrameInfo = 1;
    private const int MaxMonsterFrameInfo = 10000;
    private const int MaxMonsterHitFrame = 10000;

    private const int MaxMonsterDropRate = 1_000_000;

    private const int MaxMonsterDropItemId = 99_999;

    private const int MaxMonsterDropMoneyAmount = 100_000_000;

    private const int MinReferenceIndex = 1;

    private const int MaxSkillIndex = 300;
    private const int MaxSkillNameLength = 24;
    private const int MaxSkillDescriptionLength = 50;
    private const int MinSkillType = 1;
    private const int MaxSkillType = 4;
    private const int MinSkillAttackType = 1;
    private const int MaxSkillAttackType = 5;
    private const int MinSkillDataNumber2D = 1;
    private const int MaxSkillDataNumber2D = 10000;
    private const int MinSkillTribeInfo1 = 1;
    private const int MaxSkillTribeInfo1 = 4;
    private const int MinSkillTribeInfo2 = 1;
    private const int MaxSkillTribeInfo2 = 10;

    private const int MinSkillLearnOrUpgradePoint = 1;

    private const int MaxSkillTotalHitNumber = 10;
    private const int MaxSkillValidRadius = 1000;
    private const int MaxSkillGradeManaUse = 10000;
    private const int MaxSkillGradeStun = 100;
    private const int MaxSkillGradeFastRunSpeed = 1000;
    private const int MaxSkillGradeAttackInfo1 = 1000;
    private const int MaxSkillGradeRunTime = 10000;

    private const int MaxNpcIndex = 500;
    private const int MaxNpcNameLength = 27;
    private const int MaxNpcSpeechLength = 50;
    private const int MinNpcTribe = 1;
    private const int MaxNpcTribe = 5;
    private const int MinNpcType = 1;
    private const int MaxNpcType = 17;
    private const int MinNpcDataSortNumber = 1;
    private const int MaxNpcDataSortNumber = 10000;
    private const int MinNpcSize = 1;
    private const int MaxNpcSize = 1000;
    private const int MinNpcMenuOption = 1;
    private const int MaxNpcMenuOption = 2;
    private const int MaxNpcShopItemId = 99999;
    private const int MaxNpcSkillOfferId = 300;
    private const int MaxNpcGambleCost = 100_000_000;

    public static (WorldDataCache Cache, WorldDataFilterStats Stats) Build(WorldDataRows rows)
    {
        EnsureCriticalDatasetNotEmpty(rows.Items.Count, "world.Items");
        EnsureCriticalDatasetNotEmpty(rows.Monsters.Count, "world.Monsters");
        EnsureCriticalDatasetNotEmpty(rows.Zones.Count, "world.Zones");
        EnsureCriticalDatasetNotEmpty(rows.ZonePortals.Count, "world.ZonePortals");
        EnsureCriticalDatasetNotEmpty(rows.Levels.Count, "world.Levels");
        EnsureCriticalDatasetNotEmpty(rows.Skills.Count, "world.Skills");

        ValidateLevels(rows.Levels);
        ValidateQuests(rows.Quests, rows.QuestRewards, rows.QuestSpeeches);
        ValidateItems(rows.Items);
        ValidateMonsters(rows.Monsters, rows.MonsterDropMoney, rows.MonsterDropPotions, rows.MonsterDropCategoryRates,
            rows.MonsterDropExtraItems, rows.MonsterDropQuestItems);
        ValidateGemSockets(rows.GemSockets);
        ValidateSkills(rows.Skills, rows.SkillDescriptions, rows.SkillGrades);
        ValidateNpcs(rows.Npcs, rows.NpcMenuOptions, rows.NpcShopItems, rows.NpcSkillOffers, rows.NpcSpeeches,
            rows.NpcGambleCosts);
        RunLowestIndexSelfTest(rows);

        var (zonesByNumber, stats) = BuildZones(
            rows.Zones, rows.ZonePortals, rows.ZoneSpawnPoints, rows.ZoneNpcSpawns, rows.MonsterSpawnRegions);

        var cache = new WorldDataCache
        {
            ItemsById = BuildItems(rows.Items, rows.ItemBonusSkills),
            SkillsById = BuildSkills(rows.Skills, rows.SkillDescriptions, rows.SkillGrades),
            MonstersById = BuildMonsters(rows.Monsters, rows.MonsterDropMoney, rows.MonsterDropPotions,
                rows.MonsterDropExtraItems, rows.MonsterDropCategoryRates, rows.MonsterDropQuestItems),
            NpcsById = BuildNpcs(rows.Npcs, rows.NpcMenuOptions, rows.NpcShopItems, rows.NpcSkillOffers,
                rows.NpcSpeeches, rows.NpcGambleCosts),
            QuestsById = BuildQuests(rows.Quests, rows.QuestRewards, rows.QuestSpeeches),
            LevelsByLevel = rows.Levels.ToFrozenDictionary(static level => level.Level),
            ZonesByNumber = zonesByNumber,
            GemSocketsById = rows.GemSockets.ToFrozenDictionary(static gem => gem.GemSocketId),
            GemSocketsByTypeAndValue = BuildGemSocketTypeValueIndex(rows.GemSockets),
            BloodExchangeCatalog = [.. rows.BloodExchangeCatalog],
            EventDefinitions = [.. rows.EventDefinitions],
            ItemMallProductsById =
                rows.ItemMallProducts.ToFrozenDictionary(static product => product.ItemMallProductId),
            RewardBundleItemsByBundleId = BuildRewardBundles(rows.RewardBundles, rows.RewardBundleItems),
            CashCatalog = CashCatalogBuilder.Build(rows.ItemMallProducts),
            CashCatalogVersion = CashCatalogBuilder.ResolveVersion(rows.ItemMallProducts)
        };

        return (cache, stats);
    }

    private static void ValidateLevels(IReadOnlyList<LevelRowDto> levels)
    {
        var ordered = levels.OrderBy(static level => level.Level).ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            var row = ordered[index];
            var expectedLevel = index + 1;

            if (row.Level != expectedLevel || row.Level > MaxLevelIndex)
                throw new InvalidOperationException(
                    $"world.Levels row at position {index} has Level={row.Level}, expected {expectedLevel} " +
                    $"-- rows must be contiguous starting at 1, and Level must never exceed {MaxLevelIndex}.");

            if (row.ExpRangeMin < 0 || row.ExpRangeMin > MaxExpRangeBound)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has ExpRangeMin={row.ExpRangeMin} outside the " +
                    $"legacy 0-{MaxExpRangeBound} bound.");

            if (row.ExpRangeMax < 1 || row.ExpRangeMax > MaxExpRangeBound)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has ExpRangeMax={row.ExpRangeMax} outside the " +
                    $"legacy 1-{MaxExpRangeBound} bound.");

            if (row.ExpRangeMax <= row.ExpRangeMin)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has an invalid experience range " +
                    $"[{row.ExpRangeMin}, {row.ExpRangeMax}] -- ExpRangeMax must be strictly greater than " +
                    "ExpRangeMin.");

            if (index < ordered.Length - 1 && ordered[index + 1].ExpRangeMin != row.ExpRangeMax + 1)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} (ExpRangeMax={row.ExpRangeMax}) must tile exactly " +
                    $"into the next level's ExpRangeMin -- expected {row.ExpRangeMax + 1}, found " +
                    $"{ordered[index + 1].ExpRangeMin}.");

            if (row.RangeInfo3 > MaxRangeInfo3)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has RangeInfo3={row.RangeInfo3} outside the legacy " +
                    $"0-{MaxRangeInfo3} bound.");

            if (row.AttackPower is < 0 or > MaxLevelCombatStat
                || row.DefensePower is < 0 or > MaxLevelCombatStat
                || row.AttackSuccess is < 0 or > MaxLevelCombatStat
                || row.AttackBlock is < 0 or > MaxLevelCombatStat
                || row.ElementAttack is < 0 or > MaxLevelCombatStat
                || row.Life is < 0 or > MaxLevelCombatStat
                || row.Mana is < 0 or > MaxLevelCombatStat)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has a combat stat, Life, or Mana outside the " +
                    $"legacy 0-{MaxLevelCombatStat} bound.");
        }
    }

    private static void ValidateQuests(
        IReadOnlyList<QuestRowDto> quests,
        IReadOnlyList<QuestRewardRowDto> rewards,
        IReadOnlyList<QuestSpeechRowDto> speeches)
    {
        foreach (var quest in quests.OrderBy(static quest => quest.QuestId))
            if (quest.Step is < MinQuestStep or > MaxQuestStep)
                throw new InvalidOperationException(
                    $"world.Quests row QuestId={quest.QuestId} has Step={quest.Step} outside the legacy " +
                    $"{MinQuestStep}-{MaxQuestStep} bound.");

        foreach (var reward in rewards.OrderBy(static reward => reward.QuestId)
                     .ThenBy(static reward => reward.SlotIndex))
            if (reward.Amount is { } amount && amount is < MinQuestRewardAmount or > MaxQuestRewardAmount)
                throw new InvalidOperationException(
                    $"world.QuestRewards row QuestId={reward.QuestId} SlotIndex={reward.SlotIndex} has " +
                    $"Amount={amount} outside the legacy {MinQuestRewardAmount}-{MaxQuestRewardAmount} bound.");

        foreach (var speech in speeches.OrderBy(static speech => speech.QuestId)
                     .ThenBy(static speech => speech.SpeechKind)
                     .ThenBy(static speech => speech.LineIndex))
            if (speech.Text.Length > MaxQuestSpeechLength)
                throw new InvalidOperationException(
                    $"world.QuestSpeeches row QuestId={speech.QuestId} SpeechKind={speech.SpeechKind} " +
                    $"LineIndex={speech.LineIndex} has Text longer than {MaxQuestSpeechLength} characters.");
    }

    private static void ValidateItems(IReadOnlyList<ItemRowDto> items)
    {
        foreach (var item in items.OrderBy(static item => item.ItemId))
        {
            if (item.ItemId == 0)
                continue;

            if (item.CheckDateItem is < 0 or > MaxItemCheckDateItem)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has CheckDateItem={item.CheckDateItem} outside the " +
                    $"legacy 0-{MaxItemCheckDateItem} bound.");

            if (item.DataNumber3D is < 0 or > MaxItemDataNumber3D)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has DataNumber3D={item.DataNumber3D} outside the " +
                    $"legacy 0-{MaxItemDataNumber3D} bound.");

            if (item.AddDataNumber3D is < 0 or > MaxItemDataNumber3D)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has AddDataNumber3D={item.AddDataNumber3D} outside " +
                    $"the legacy 0-{MaxItemDataNumber3D} bound.");

            var potionType2InRange = item.PotionType1 == PotionType1RestrictedValue
                ? item.PotionType2 is >= MinPotionType2Restricted and <= MaxPotionType2Restricted
                : item.PotionType2 is >= 0 and <= MaxPotionType2Default;

            if (!potionType2InRange)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has PotionType2={item.PotionType2} outside the " +
                    $"legacy bound for PotionType1={item.PotionType1} (" +
                    $"{MinPotionType2Restricted}-{MaxPotionType2Restricted} when PotionType1=" +
                    $"{PotionType1RestrictedValue}, else 0-{MaxPotionType2Default}).");
        }
    }

    private static void ValidateGemSockets(IReadOnlyList<GemSocketRowDto> gemSockets)
    {
        foreach (var gemSocket in gemSockets.OrderBy(static gemSocket => gemSocket.GemSocketId))
        {
            if (gemSocket.Value02 == 0 || gemSocket.Type == 0)
                continue;

            var satisfiesBand = gemSocket.Type switch
            {
                1 => gemSocket.Value02 is >= 1 and <= 33
                     && gemSocket.Value03 is >= 0 and <= 400
                     && gemSocket.Value04 is >= 0 and <= 400,
                >= 2 and <= 29 => gemSocket.Value02 is >= 1 and <= 100
                                  && gemSocket.Value03 is >= 0 and <= 1000
                                  && gemSocket.Value04 is >= 0 and <= 1000,
                >= 30 and <= 38 => true,
                >= 39 and <= 42 => gemSocket.Value02 is >= 1 and <= 10
                                   && gemSocket.Value03 >= 1
                                   && gemSocket.Value04 == 0,
                >= 43 and <= 46 => gemSocket.Value02 is >= 1 and <= 10
                                   && gemSocket.Value03 >= 6
                                   && gemSocket.Value04 == 0,
                _ => false
            };

            if (!satisfiesBand)
                throw new InvalidOperationException(
                    $"world.GemSockets row GemSocketId={gemSocket.GemSocketId} has Type={gemSocket.Type}, " +
                    $"Value02={gemSocket.Value02}, Value03={gemSocket.Value03}, Value04={gemSocket.Value04} " +
                    $"violating the legacy Type-band rules (Type must be {MinGemSocketType}-{MaxGemSocketType} " +
                    "with band-specific Value02/Value03/Value04 bounds).");
        }
    }

    private static FrozenDictionary<int, GemSocketRowDto> BuildGemSocketTypeValueIndex(
        IReadOnlyList<GemSocketRowDto> gemSockets)
    {
        var byTypeAndValue = new Dictionary<int, GemSocketRowDto>(gemSockets.Count);
        foreach (var gemSocket in gemSockets.OrderBy(static gemSocket => gemSocket.GemSocketId))
            byTypeAndValue.TryAdd(GemSocketTypeValueKey(gemSocket.Type, gemSocket.Value02), gemSocket);

        return byTypeAndValue.ToFrozenDictionary();
    }

    private static int GemSocketTypeValueKey(int type, int value02)
    {
        if ((uint)type > byte.MaxValue || (uint)value02 > byte.MaxValue)
            throw new InvalidOperationException(
                $"world.GemSockets row has Type={type}, Value02={value02}, which does not fit the byte-packed " +
                "lookup key (0-255 each). Type band 30-38 is validated as value-unconstrained, so a row landing " +
                "here would silently collide with another entry instead of failing -- widen the key encoding if " +
                "this band legitimately needs values outside 0-255.");

        return (type << 8) | value02;
    }

    private static void ValidateMonsters(
        IReadOnlyList<MonsterRowDto> monsters,
        IReadOnlyList<MonsterDropMoneyRowDto> dropMoney,
        IReadOnlyList<MonsterDropPotionRowDto> dropPotions,
        IReadOnlyList<MonsterDropCategoryRateRowDto> dropCategoryRates,
        IReadOnlyList<MonsterDropExtraItemRowDto> dropExtraItems,
        IReadOnlyList<MonsterDropQuestItemRowDto> dropQuestItems)
    {
        foreach (var monster in monsters.OrderBy(static monster => monster.MonsterId))
        {
            if (monster.Name.Length > MaxMonsterNameLength)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a Name longer than " +
                    $"{MaxMonsterNameLength} characters.");

            if (monster.ChatLine1 is { Length: > MaxMonsterChatLineLength }
                || monster.ChatLine2 is { Length: > MaxMonsterChatLineLength })
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a ChatLine longer than " +
                    $"{MaxMonsterChatLineLength} characters.");

            if (monster.Type is < MinMonsterType or > MaxMonsterType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Type={monster.Type} outside the " +
                    $"legacy {MinMonsterType}-{MaxMonsterType} bound.");

            if (monster.SpecialType is < MinMonsterSpecialType or > MaxMonsterSpecialType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has SpecialType={monster.SpecialType} " +
                    $"outside the legacy {MinMonsterSpecialType}-{MaxMonsterSpecialType} bound.");

            if (monster.DamageType is < MinMonsterDamageType or > MaxMonsterDamageType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has DamageType={monster.DamageType} " +
                    $"outside the legacy {MinMonsterDamageType}-{MaxMonsterDamageType} bound.");

            if (monster.DataSortNumber is < MinMonsterDataSortNumber or > MaxMonsterDataSortNumber)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has DataSortNumber=" +
                    $"{monster.DataSortNumber} outside the legacy {MinMonsterDataSortNumber}-" +
                    $"{MaxMonsterDataSortNumber} bound.");

            if (monster.Size1 is < MinMonsterSize or > MaxMonsterSize
                || monster.Size2 is < MinMonsterSize or > MaxMonsterSize
                || monster.Size3 is < MinMonsterSize or > MaxMonsterSize)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a Size1/Size2/Size3 outside the " +
                    $"legacy {MinMonsterSize}-{MaxMonsterSize} bound.");

            if (monster.Size4 is < 0 or > MaxMonsterSize4)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Size4={monster.Size4} outside the " +
                    $"legacy 0-{MaxMonsterSize4} bound.");

            if (monster.SizeCategory is < MinMonsterSizeCategory or > MaxMonsterSizeCategory)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has SizeCategory={monster.SizeCategory} " +
                    $"outside the legacy {MinMonsterSizeCategory}-{MaxMonsterSizeCategory} bound.");

            if (monster.CheckCollision is < MinMonsterCheckCollision or > MaxMonsterCheckCollision)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has CheckCollision=" +
                    $"{monster.CheckCollision} outside the legacy {MinMonsterCheckCollision}-" +
                    $"{MaxMonsterCheckCollision} bound.");

            if (monster.TotalHitNum > MaxMonsterHitCount || monster.TotalSkillHitNum > MaxMonsterHitCount)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a TotalHitNum/TotalSkillHitNum " +
                    $"outside the legacy 0-{MaxMonsterHitCount} bound.");

            if (monster.ItemLevel is < MinMonsterItemLevel or > MaxMonsterItemLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has ItemLevel={monster.ItemLevel} " +
                    $"outside the legacy {MinMonsterItemLevel}-{MaxMonsterItemLevel} bound.");

            if (monster.MartialItemLevel is < 0 or > MaxMonsterMartialItemLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has MartialItemLevel=" +
                    $"{monster.MartialItemLevel} outside the legacy 0-{MaxMonsterMartialItemLevel} bound.");

            if (monster.RealLevel is < MinMonsterRealLevel or > MaxMonsterRealLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has RealLevel={monster.RealLevel} " +
                    $"outside the legacy {MinMonsterRealLevel}-{MaxMonsterRealLevel} bound.");

            if (monster.MartialRealLevel is < 0 or > MaxMonsterMartialRealLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has MartialRealLevel=" +
                    $"{monster.MartialRealLevel} outside the legacy 0-{MaxMonsterMartialRealLevel} bound.");

            if (monster.GeneralExperience is < 0 or > MaxMonsterExperienceReward
                || monster.PatExperience is < 0 or > MaxMonsterExperienceReward)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a GeneralExperience/PatExperience " +
                    $"outside the legacy 0-{MaxMonsterExperienceReward} bound.");

            if (monster.Life is < MinMonsterLife or > MaxMonsterLife)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Life={monster.Life} outside the " +
                    $"legacy {MinMonsterLife}-{MaxMonsterLife} bound.");

            if (monster.AttackType is < MinMonsterAttackType or > MaxMonsterAttackType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has AttackType={monster.AttackType} " +
                    $"outside the legacy {MinMonsterAttackType}-{MaxMonsterAttackType} bound.");

            if (monster.RadiusInfo1 is < 0 or > MaxMonsterRadiusInfo
                || monster.RadiusInfo2 is < 0 or > MaxMonsterRadiusInfo
                || monster.RadiusInfo2 < monster.RadiusInfo1)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has an invalid RadiusInfo1/RadiusInfo2 " +
                    $"pair [{monster.RadiusInfo1}, {monster.RadiusInfo2}] -- both must be 0-" +
                    $"{MaxMonsterRadiusInfo} and RadiusInfo2 must be greater than or equal to RadiusInfo1.");

            if (monster.WalkSpeed is < 0 or > MaxMonsterMovementSpeed
                || monster.RunSpeed is < 0 or > MaxMonsterMovementSpeed
                || monster.DeathSpeed is < 0 or > MaxMonsterMovementSpeed)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a WalkSpeed/RunSpeed/DeathSpeed " +
                    $"outside the legacy 0-{MaxMonsterMovementSpeed} bound.");

            if (monster.AttackPower is < 0 or > MaxMonsterAttackPower)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has AttackPower={monster.AttackPower} " +
                    $"outside the legacy 0-{MaxMonsterAttackPower} bound.");

            if (monster.DefensePower is < 0 or > MaxMonsterCombatStat
                || monster.AttackSuccess is < 0 or > MaxMonsterCombatStat
                || monster.AttackBlock is < 0 or > MaxMonsterCombatStat
                || monster.ElementAttackPower is < 0 or > MaxMonsterCombatStat
                || monster.ElementDefensePower is < 0 or > MaxMonsterCombatStat)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a DefensePower/AttackSuccess/" +
                    $"AttackBlock/ElementAttackPower/ElementDefensePower outside the legacy 0-" +
                    $"{MaxMonsterCombatStat} bound.");

            if (monster.Critical is < 0 or > MaxMonsterCritical)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Critical={monster.Critical} " +
                    $"outside the legacy 0-{MaxMonsterCritical} bound.");

            if (monster.FollowInfo1 is < 0 or > MaxMonsterFollowInfo
                || monster.FollowInfo2 is < 0 or > MaxMonsterFollowInfo
                || monster.FollowInfo2 < monster.FollowInfo1)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has an invalid FollowInfo1/FollowInfo2 " +
                    $"pair [{monster.FollowInfo1}, {monster.FollowInfo2}] -- both must be 0-" +
                    $"{MaxMonsterFollowInfo} and FollowInfo2 must be greater than or equal to FollowInfo1.");

            if (monster.SummonTime1 is < MinMonsterSummonTime or > MaxMonsterSummonTime
                || monster.SummonTime2 is < MinMonsterSummonTime or > MaxMonsterSummonTime
                || monster.SummonTime2 < monster.SummonTime1)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has an invalid SummonTime1/SummonTime2 " +
                    $"pair [{monster.SummonTime1}, {monster.SummonTime2}] -- both must be " +
                    $"{MinMonsterSummonTime}-{MaxMonsterSummonTime} and SummonTime2 must be greater than or " +
                    "equal to SummonTime1.");

            if (monster.FrameInfo1 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo2 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo3 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo4 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo5 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo6 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a FrameInfo1-6 outside the legacy " +
                    $"{MinMonsterFrameInfo}-{MaxMonsterFrameInfo} bound.");

            if (monster.HitFrame1 is < 0 or > MaxMonsterHitFrame
                || monster.HitFrame2 is < 0 or > MaxMonsterHitFrame
                || monster.HitFrame3 is < 0 or > MaxMonsterHitFrame)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a HitFrame1-3 outside the legacy " +
                    $"0-{MaxMonsterHitFrame} bound.");

            if (monster.SkillHitFrame1 is < 0 or > MaxMonsterHitFrame
                || monster.SkillHitFrame2 is < 0 or > MaxMonsterHitFrame
                || monster.SkillHitFrame3 is < 0 or > MaxMonsterHitFrame)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a SkillHitFrame1-3 outside the " +
                    $"legacy 0-{MaxMonsterHitFrame} bound.");

            if (monster.BulletInfo1 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.BulletInfo2 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a BulletInfo1/BulletInfo2 outside " +
                    $"the legacy {MinMonsterFrameInfo}-{MaxMonsterFrameInfo} bound.");
        }

        foreach (var row in dropMoney.OrderBy(static row => row.MonsterId))
            if (row.DropRate is < 0 or > MaxMonsterDropRate
                || row.MinAmount is < 0 or > MaxMonsterDropMoneyAmount
                || row.MaxAmount is < 0 or > MaxMonsterDropMoneyAmount)
                throw new InvalidOperationException(
                    $"world.MonsterDropMoney row MonsterId={row.MonsterId} has a DropRate/MinAmount/MaxAmount " +
                    "outside its legacy bound (DropRate 0-" +
                    $"{MaxMonsterDropRate}, MinAmount/MaxAmount 0-{MaxMonsterDropMoneyAmount}).");

        foreach (var row in dropPotions.OrderBy(static row => row.MonsterId).ThenBy(static row => row.SlotIndex))
            if (row.DropRate is < 0 or > MaxMonsterDropRate || row.PotionItemId is < 0 or > MaxMonsterDropItemId)
                throw new InvalidOperationException(
                    $"world.MonsterDropPotions row MonsterId={row.MonsterId} SlotIndex={row.SlotIndex} has a " +
                    $"DropRate/PotionItemId outside its legacy bound (DropRate 0-{MaxMonsterDropRate}, " +
                    $"PotionItemId 0-{MaxMonsterDropItemId}).");

        foreach (var row in dropCategoryRates.OrderBy(static row => row.MonsterId)
                     .ThenBy(static row => row.CategoryIndex))
            if (row.Value is < 0 or > MaxMonsterDropRate)
                throw new InvalidOperationException(
                    $"world.MonsterDropCategoryRates row MonsterId={row.MonsterId} " +
                    $"CategoryIndex={row.CategoryIndex} has Value={row.Value} outside the legacy 0-" +
                    $"{MaxMonsterDropRate} bound.");

        foreach (var row in dropExtraItems.OrderBy(static row => row.MonsterId).ThenBy(static row => row.SlotIndex))
            if (row.DropRate is < 0 or > MaxMonsterDropRate
                || (row.ItemId is { } itemId && itemId is < 0 or > MaxMonsterDropItemId))
                throw new InvalidOperationException(
                    $"world.MonsterDropExtraItems row MonsterId={row.MonsterId} SlotIndex={row.SlotIndex} has " +
                    $"a DropRate/ItemId outside its legacy bound (DropRate 0-{MaxMonsterDropRate}, ItemId 0-" +
                    $"{MaxMonsterDropItemId}).");

        foreach (var row in dropQuestItems.OrderBy(static row => row.MonsterId))
            if (row.DropRate is < 0 or > MaxMonsterDropRate || row.QuestItemId is < 0 or > MaxMonsterDropItemId)
                throw new InvalidOperationException(
                    $"world.MonsterDropQuestItems row MonsterId={row.MonsterId} has a DropRate/QuestItemId " +
                    $"outside its legacy bound (DropRate 0-{MaxMonsterDropRate}, QuestItemId 0-" +
                    $"{MaxMonsterDropItemId}).");
    }

    private static void ValidateSkills(
        IReadOnlyList<SkillRowDto> skills,
        IReadOnlyList<SkillDescriptionRowDto> descriptions,
        IReadOnlyList<SkillGradeRowDto> grades)
    {
        foreach (var skill in skills.OrderBy(static skill => skill.SkillId))
        {
            if (skill.SkillId == 0)
                continue;

            if (skill.SkillId is < MinReferenceIndex or > MaxSkillIndex)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} is outside the legacy {MinReferenceIndex}-" +
                    $"{MaxSkillIndex} index cap (the fixed skill-table capacity).");

            if (skill.Name.Length > MaxSkillNameLength)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has a Name longer than {MaxSkillNameLength} " +
                    "characters.");

            if (skill.Type is < MinSkillType or > MaxSkillType)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has Type={skill.Type} outside the legacy " +
                    $"{MinSkillType}-{MaxSkillType} bound.");

            if (skill.AttackType is < MinSkillAttackType or > MaxSkillAttackType)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has AttackType={skill.AttackType} outside the " +
                    $"legacy {MinSkillAttackType}-{MaxSkillAttackType} bound.");

            if (skill.DataNumber2D is < MinSkillDataNumber2D or > MaxSkillDataNumber2D)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has DataNumber2D={skill.DataNumber2D} outside the " +
                    $"legacy {MinSkillDataNumber2D}-{MaxSkillDataNumber2D} bound.");

            if (skill.TribeInfo1 is < MinSkillTribeInfo1 or > MaxSkillTribeInfo1)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has TribeInfo1={skill.TribeInfo1} outside the " +
                    $"legacy {MinSkillTribeInfo1}-{MaxSkillTribeInfo1} bound.");

            if (skill.TribeInfo2 is < MinSkillTribeInfo2 or > MaxSkillTribeInfo2)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has TribeInfo2={skill.TribeInfo2} outside the " +
                    $"legacy {MinSkillTribeInfo2}-{MaxSkillTribeInfo2} bound.");

            if (skill.LearnSkillPoint < MinSkillLearnOrUpgradePoint)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has LearnSkillPoint={skill.LearnSkillPoint} below " +
                    $"the legacy floor of {MinSkillLearnOrUpgradePoint}.");

            if (skill.MaxUpgradePoint < MinSkillLearnOrUpgradePoint)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has MaxUpgradePoint={skill.MaxUpgradePoint} below " +
                    $"the legacy floor of {MinSkillLearnOrUpgradePoint} (also the divide-by-zero guard in " +
                    "SKILLSYSTEM::ReturnSkillValue).");

            if (skill.TotalHitNumber > MaxSkillTotalHitNumber)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has TotalHitNumber={skill.TotalHitNumber} outside " +
                    $"the legacy 0-{MaxSkillTotalHitNumber} bound.");

            if (skill.ValidRadius is < 0 or > MaxSkillValidRadius)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has ValidRadius={skill.ValidRadius} outside the " +
                    $"legacy 0-{MaxSkillValidRadius} bound.");
        }

        foreach (var description in descriptions.OrderBy(static row => row.SkillId)
                     .ThenBy(static row => row.LineIndex))
            if (description.Text.Length > MaxSkillDescriptionLength)
                throw new InvalidOperationException(
                    $"world.SkillDescriptions row SkillId={description.SkillId} LineIndex={description.LineIndex} " +
                    $"has Text longer than {MaxSkillDescriptionLength} characters.");

        foreach (var grade in grades.OrderBy(static row => row.SkillId).ThenBy(static row => row.GradeIndex))
        {
            if (grade.ManaUse is < 0 or > MaxSkillGradeManaUse)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"ManaUse={grade.ManaUse} outside the legacy 0-{MaxSkillGradeManaUse} bound.");

            if (grade.StunAttack > MaxSkillGradeStun || grade.StunDefense > MaxSkillGradeStun)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has a StunAttack/" +
                    $"StunDefense outside the legacy 0-{MaxSkillGradeStun} bound.");

            if (grade.FastRunSpeed is < 0 or > MaxSkillGradeFastRunSpeed)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"FastRunSpeed={grade.FastRunSpeed} outside the legacy 0-{MaxSkillGradeFastRunSpeed} bound.");

            if (grade.AttackInfo1 is < 0 or > MaxSkillGradeAttackInfo1)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"AttackInfo1={grade.AttackInfo1} outside the legacy 0-{MaxSkillGradeAttackInfo1} bound.");

            if (grade.RunTime is < 0 or > MaxSkillGradeRunTime)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"RunTime={grade.RunTime} outside the legacy 0-{MaxSkillGradeRunTime} bound.");
        }
    }

    private static void ValidateNpcs(
        IReadOnlyList<NpcRowDto> npcs,
        IReadOnlyList<NpcMenuOptionRowDto> menuOptions,
        IReadOnlyList<NpcShopItemRowDto> shopItems,
        IReadOnlyList<NpcSkillOfferRowDto> skillOffers,
        IReadOnlyList<NpcSpeechRowDto> speeches,
        IReadOnlyList<NpcGambleCostRowDto> gambleCosts)
    {
        foreach (var npc in npcs.OrderBy(static npc => npc.NpcId))
        {
            if (npc.NpcId == 0)
                continue;

            if (npc.NpcId is < MinReferenceIndex or > MaxNpcIndex)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} is outside the legacy {MinReferenceIndex}-{MaxNpcIndex} " +
                    "index cap (the fixed NPC-table capacity).");

            if (npc.Name.Length > MaxNpcNameLength)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has a Name longer than {MaxNpcNameLength} characters.");

            if (npc.Tribe is < MinNpcTribe or > MaxNpcTribe)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has Tribe={npc.Tribe} outside the legacy {MinNpcTribe}-" +
                    $"{MaxNpcTribe} bound.");

            if (npc.Type is < MinNpcType or > MaxNpcType)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has Type={npc.Type} outside the legacy {MinNpcType}-" +
                    $"{MaxNpcType} bound.");

            if (npc.DataSortNumber2D is < MinNpcDataSortNumber or > MaxNpcDataSortNumber
                || npc.DataSortNumber3D is < MinNpcDataSortNumber or > MaxNpcDataSortNumber)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has a DataSortNumber2D/DataSortNumber3D outside the legacy " +
                    $"{MinNpcDataSortNumber}-{MaxNpcDataSortNumber} bound.");

            if (npc.Size1 is < MinNpcSize or > MaxNpcSize
                || npc.Size2 is < MinNpcSize or > MaxNpcSize
                || npc.Size3 is < MinNpcSize or > MaxNpcSize)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has a Size1/Size2/Size3 outside the legacy {MinNpcSize}-" +
                    $"{MaxNpcSize} bound.");
        }

        foreach (var speech in speeches.OrderBy(static row => row.NpcId).ThenBy(static row => row.SpeechGroup)
                     .ThenBy(static row => row.SpeechIndex))
            if (speech.Text.Length > MaxNpcSpeechLength)
                throw new InvalidOperationException(
                    $"world.NpcSpeeches row NpcId={speech.NpcId} SpeechGroup={speech.SpeechGroup} " +
                    $"SpeechIndex={speech.SpeechIndex} has Text longer than {MaxNpcSpeechLength} characters.");

        foreach (var option in menuOptions.OrderBy(static row => row.NpcId).ThenBy(static row => row.SlotIndex))
            if (option.OptionId is < MinNpcMenuOption or > MaxNpcMenuOption)
                throw new InvalidOperationException(
                    $"world.NpcMenuOptions row NpcId={option.NpcId} SlotIndex={option.SlotIndex} has " +
                    $"OptionId={option.OptionId} outside the legacy {MinNpcMenuOption}-{MaxNpcMenuOption} bound.");

        foreach (var shopItem in shopItems.OrderBy(static row => row.NpcId).ThenBy(static row => row.ShopPage)
                     .ThenBy(static row => row.SlotIndex))
            if (shopItem.ItemId is { } itemId && itemId is < 0 or > MaxNpcShopItemId)
                throw new InvalidOperationException(
                    $"world.NpcShopItems row NpcId={shopItem.NpcId} ShopPage={shopItem.ShopPage} " +
                    $"SlotIndex={shopItem.SlotIndex} has ItemId={itemId} outside the legacy 0-{MaxNpcShopItemId} " +
                    "bound.");

        foreach (var offer in skillOffers.OrderBy(static row => row.NpcId).ThenBy(static row => row.NpcSkillOfferId))
            if (offer.SkillId is { } skillId && skillId is < 0 or > MaxNpcSkillOfferId)
                throw new InvalidOperationException(
                    $"world.NpcSkillOffers row NpcId={offer.NpcId} NpcSkillOfferId={offer.NpcSkillOfferId} has " +
                    $"SkillId={skillId} outside the legacy 0-{MaxNpcSkillOfferId} bound.");

        foreach (var gamble in gambleCosts.OrderBy(static row => row.NpcId).ThenBy(static row => row.GambleTier)
                     .ThenBy(static row => row.CostIndex))
            if (gamble.Value is < 0 or > MaxNpcGambleCost)
                throw new InvalidOperationException(
                    $"world.NpcGambleCosts row NpcId={gamble.NpcId} GambleTier={gamble.GambleTier} " +
                    $"CostIndex={gamble.CostIndex} has Value={gamble.Value} outside the legacy 0-{MaxNpcGambleCost} " +
                    "bound.");
    }

    private static void RunLowestIndexSelfTest(WorldDataRows rows)
    {
        if (!rows.Skills.Any(static skill => skill.SkillId == MinReferenceIndex))
            throw new InvalidOperationException(
                $"world.Skills lowest-index self-test failed: the canonical skill (SkillId {MinReferenceIndex}) " +
                "did not resolve -- the skill reference table loaded but is missing its first element, so the " +
                "GameServer must not begin serving (legacy MyGame::Init lowest-index probe).");

        if (rows.Npcs.Count > 0 && !rows.Npcs.Any(static npc => npc.NpcId == MinReferenceIndex))
            throw new InvalidOperationException(
                $"world.Npcs lowest-index self-test failed: the table is non-empty but the canonical NPC " +
                $"(NpcId {MinReferenceIndex}) did not resolve -- an NPC table that loaded rows must contain its " +
                "first element, so the GameServer must not begin serving (legacy MyGame::Init lowest-index probe).");
    }

    public static FrozenDictionary<int, ItemDefinition> BuildItems(
        IReadOnlyList<ItemRowDto> items,
        IReadOnlyList<ItemBonusSkillRowDto> bonusSkills)
    {
        var bonusSkillsByItem = GroupToLists(bonusSkills, static row => row.ItemId);
        var result = new Dictionary<int, ItemDefinition>(items.Count);

        foreach (var item in items)
            result.Add(item.ItemId, new ItemDefinition(item, TakeGroup(bonusSkillsByItem, item.ItemId)));

        return result.ToFrozenDictionary();
    }

    public static FrozenDictionary<int, SkillDefinition> BuildSkills(
        IReadOnlyList<SkillRowDto> skills,
        IReadOnlyList<SkillDescriptionRowDto> descriptions,
        IReadOnlyList<SkillGradeRowDto> grades)
    {
        var descriptionsBySkill = GroupToLists(descriptions, static row => row.SkillId);
        var gradesBySkill = GroupToLists(grades, static row => row.SkillId);
        var result = new Dictionary<int, SkillDefinition>(skills.Count);

        foreach (var skill in skills)
            result.Add(skill.SkillId, new SkillDefinition(
                skill,
                TakeGroup(descriptionsBySkill, skill.SkillId),
                TakeGroup(gradesBySkill, skill.SkillId)));

        return result.ToFrozenDictionary();
    }

    public static FrozenDictionary<int, MonsterDefinition> BuildMonsters(
        IReadOnlyList<MonsterRowDto> monsters,
        IReadOnlyList<MonsterDropMoneyRowDto> dropMoney,
        IReadOnlyList<MonsterDropPotionRowDto> dropPotions,
        IReadOnlyList<MonsterDropExtraItemRowDto> dropExtraItems,
        IReadOnlyList<MonsterDropCategoryRateRowDto> dropCategoryRates,
        IReadOnlyList<MonsterDropQuestItemRowDto> dropQuestItems)
    {
        var moneyByMonster = new Dictionary<int, MonsterDropMoneyRowDto>(dropMoney.Count);
        foreach (var row in dropMoney)
            moneyByMonster.Add(row.MonsterId, row);

        var questItemByMonster = new Dictionary<int, MonsterDropQuestItemRowDto>(dropQuestItems.Count);
        foreach (var row in dropQuestItems)
            questItemByMonster.Add(row.MonsterId, row);

        var potionsByMonster = GroupToLists(dropPotions, static row => row.MonsterId);
        var extraItemsByMonster = GroupToLists(dropExtraItems, static row => row.MonsterId);
        var categoryRatesByMonster = GroupToLists(dropCategoryRates, static row => row.MonsterId);
        var result = new Dictionary<int, MonsterDefinition>(monsters.Count);

        foreach (var monster in monsters)
            result.Add(monster.MonsterId, new MonsterDefinition(
                monster,
                moneyByMonster.GetValueOrDefault(monster.MonsterId),
                TakeGroup(potionsByMonster, monster.MonsterId),
                TakeGroup(extraItemsByMonster, monster.MonsterId),
                TakeGroup(categoryRatesByMonster, monster.MonsterId),
                questItemByMonster.GetValueOrDefault(monster.MonsterId)));

        return result.ToFrozenDictionary();
    }

    public static FrozenDictionary<int, NpcDefinition> BuildNpcs(
        IReadOnlyList<NpcRowDto> npcs,
        IReadOnlyList<NpcMenuOptionRowDto> menuOptions,
        IReadOnlyList<NpcShopItemRowDto> shopItems,
        IReadOnlyList<NpcSkillOfferRowDto> skillOffers,
        IReadOnlyList<NpcSpeechRowDto> speeches,
        IReadOnlyList<NpcGambleCostRowDto> gambleCosts)
    {
        var menuOptionsByNpc = GroupToLists(menuOptions, static row => row.NpcId);
        var shopItemsByNpc = GroupToLists(shopItems, static row => row.NpcId);
        var skillOffersByNpc = GroupToLists(skillOffers, static row => row.NpcId);
        var speechesByNpc = GroupToLists(speeches, static row => row.NpcId);
        var gambleCostsByNpc = GroupToLists(gambleCosts, static row => row.NpcId);
        var result = new Dictionary<int, NpcDefinition>(npcs.Count);

        foreach (var npc in npcs)
            result.Add(npc.NpcId, new NpcDefinition(
                npc,
                TakeGroup(menuOptionsByNpc, npc.NpcId),
                TakeGroup(shopItemsByNpc, npc.NpcId),
                TakeGroup(skillOffersByNpc, npc.NpcId),
                TakeGroup(speechesByNpc, npc.NpcId),
                TakeGroup(gambleCostsByNpc, npc.NpcId)));

        return result.ToFrozenDictionary();
    }

    public static FrozenDictionary<int, QuestDefinition> BuildQuests(
        IReadOnlyList<QuestRowDto> quests,
        IReadOnlyList<QuestRewardRowDto> rewards,
        IReadOnlyList<QuestSpeechRowDto> speeches)
    {
        var rewardsByQuest = GroupToLists(rewards, static row => row.QuestId);
        var speechesByQuest = GroupToLists(speeches, static row => row.QuestId);
        var result = new Dictionary<int, QuestDefinition>(quests.Count);

        foreach (var quest in quests)
            result.Add(quest.QuestId, new QuestDefinition(
                quest,
                TakeGroup(rewardsByQuest, quest.QuestId),
                TakeGroup(speechesByQuest, quest.QuestId)));

        return result.ToFrozenDictionary();
    }

    public static (FrozenDictionary<short, ZoneDefinition> ZonesByNumber, WorldDataFilterStats Stats) BuildZones(
        IReadOnlyList<ZoneRowDto> zones,
        IReadOnlyList<ZonePortalRowDto> portals,
        IReadOnlyList<ZoneSpawnPointRowDto> spawnPoints,
        IReadOnlyList<ZoneNpcSpawnRowDto> npcSpawns,
        IReadOnlyList<MonsterSpawnRegionRowDto> spawnRegions)
    {
        var portalsWithoutDestination = 0;
        var portalsByZone = new Dictionary<short, List<ZonePortalRowDto>>();
        foreach (var portal in portals)
        {
            if (portal.TargetZoneNumber is null)
            {
                portalsWithoutDestination++;
                continue;
            }

            AddToGroup(portalsByZone, portal.ZoneNumber, portal);
        }

        var spawnPointsByZone = GroupToLists(spawnPoints, static row => row.ZoneNumber);

        var npcPlacementsWithoutNpc = 0;
        var npcSpawnsByZone = new Dictionary<short, List<ZoneNpcSpawnRowDto>>();
        foreach (var npcSpawn in npcSpawns)
        {
            if (npcSpawn.NpcId is null)
            {
                npcPlacementsWithoutNpc++;
                continue;
            }

            AddToGroup(npcSpawnsByZone, npcSpawn.ZoneNumber, npcSpawn);
        }

        var spawnRegionsWithoutZone = 0;
        var spawnRegionsWithoutMonster = 0;
        var fieldSpawnRegionsByZone = new Dictionary<short, List<MonsterSpawnRegionRowDto>>();
        var bossSpawnRegionsByZone = new Dictionary<short, List<MonsterSpawnRegionRowDto>>();
        foreach (var region in spawnRegions)
        {
            if (region.ZoneNumber is not { } zoneNumber)
            {
                spawnRegionsWithoutZone++;
                continue;
            }

            if (region.MonsterId is null)
            {
                spawnRegionsWithoutMonster++;
                continue;
            }

            var targetGroup = IsBossMonsterSpawnRegionFile(region.SourceFileName)
                ? bossSpawnRegionsByZone
                : fieldSpawnRegionsByZone;
            AddToGroup(targetGroup, zoneNumber, region);
        }

        var result = new Dictionary<short, ZoneDefinition>(zones.Count);
        foreach (var zone in zones)
        {
            var canonicalSpawnZoneId = ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(zone.ZoneNumber);
            result.Add(zone.ZoneNumber, new ZoneDefinition(
                zone,
                TakeGroup(portalsByZone, zone.ZoneNumber),
                TakeGroup(spawnPointsByZone, zone.ZoneNumber),
                TakeGroup(npcSpawnsByZone, zone.ZoneNumber),
                TakeGroup(fieldSpawnRegionsByZone, canonicalSpawnZoneId),
                TakeGroup(bossSpawnRegionsByZone, canonicalSpawnZoneId)));
        }

        var stats = new WorldDataFilterStats(
            portalsWithoutDestination,
            npcPlacementsWithoutNpc,
            spawnRegionsWithoutZone,
            spawnRegionsWithoutMonster);

        return (result.ToFrozenDictionary(), stats);
    }

    public static FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> BuildRewardBundles(
        IReadOnlyList<RewardBundleRowDto> bundles,
        IReadOnlyList<RewardBundleItemRowDto> bundleItems)
    {
        var itemsByBundle = GroupToLists(bundleItems, static row => row.RewardBundleId);
        var result = new Dictionary<int, ImmutableArray<RewardBundleItemRowDto>>(bundles.Count);

        foreach (var bundle in bundles)
            result.Add(bundle.RewardBundleId, TakeGroup(itemsByBundle, bundle.RewardBundleId));

        return result.ToFrozenDictionary();
    }

    private static bool IsBossMonsterSpawnRegionFile(string sourceFileName)
    {
        return sourceFileName.Contains("SUMMONBOSSMONSTER", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureCriticalDatasetNotEmpty(int rowCount, string datasetName)
    {
        if (rowCount == 0)
            throw new InvalidOperationException(
                $"Critical world dataset '{datasetName}' is empty -- the database is not seeded, and the " +
                "GameServer must not accept a single connection without its reference data (ADR-0011).");
    }

    private static Dictionary<TKey, List<TRow>> GroupToLists<TKey, TRow>(
        IReadOnlyList<TRow> rows,
        Func<TRow, TKey> keySelector)
        where TKey : notnull
    {
        var groups = new Dictionary<TKey, List<TRow>>();
        foreach (var row in rows)
            AddToGroup(groups, keySelector(row), row);

        return groups;
    }

    private static void AddToGroup<TKey, TRow>(Dictionary<TKey, List<TRow>> groups, TKey key, TRow row)
        where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = [];
            groups.Add(key, list);
        }

        list.Add(row);
    }

    private static ImmutableArray<TRow> TakeGroup<TKey, TRow>(Dictionary<TKey, List<TRow>> groups, TKey key)
        where TKey : notnull
    {
        return groups.TryGetValue(key, out var list)
            ? ImmutableArray.CreateRange(list)
            : ImmutableArray<TRow>.Empty;
    }
}
