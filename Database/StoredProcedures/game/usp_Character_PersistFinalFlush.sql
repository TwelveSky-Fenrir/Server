CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                        @Position game.tvp_CharacterPosition READONLY,
                                                        @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

    BEGIN TRANSACTION;

    UPDATE c
    SET c.FlushSequence          = p.FlushSequence,
        c.Level                  = p.Level,
        c.Level2                 = p.Level2,
        c.Experience              = p.Experience,
        c.Life                    = p.Life,
        c.MaxLife                 = p.MaxLife,
        c.Mana                    = p.Mana,
        c.MaxMana                 = p.MaxMana,
        c.StatVit                 = p.StatVit,
        c.StatStr                 = p.StatStr,
        c.StatInt                 = p.StatInt,
        c.StatDex                 = p.StatDex,
        c.StatPoints               = p.StatPoints,
        c.SkillPoints              = p.SkillPoints,
        c.ContributionPoints       = p.ContributionPoints,
        c.Exp2                     = p.Exp2,
        c.RebirthCount             = p.RebirthCount,
        c.EatLifePotion            = p.EatLifePotion,
        c.EatManaPotion            = p.EatManaPotion,
        c.EatStrPotion             = p.EatStrPotion,
        c.EatDexPotion             = p.EatDexPotion,
        c.EatElePotion             = p.EatElePotion,
        c.DropItemTime             = p.DropItemTime,
        c.M15PetLuckyBoxPity       = p.M15PetLuckyBoxPity,
        c.MountItemId              = p.MountItemId,
        c.MountExpActivity         = p.MountExpActivity,
        c.MountPower               = p.MountPower,
        c.MountSlotIndex           = p.MountSlotIndex,
        c.MountTime                = p.MountTime,
        c.VisibleState             = p.VisibleState,
        c.SpecialState             = p.SpecialState,
        c.UseOrnament              = p.UseOrnament,
        c.Title                    = p.Title,
        c.Halo                     = p.Halo,
        c.TeacherPoint             = p.TeacherPoint,
        c.WarPoint                 = c.WarPoint + p.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + p.BloodCoinDelta,
        c.PetExpX2Time             = p.PetExpX2Time,
        c.AnimalAbsorbTime         = p.AnimalAbsorbTime,
        c.AnimalAbsorbState        = p.AnimalAbsorbState,
        c.CostumeIndex             = p.CostumeIndex,
        c.ProtectForHalo           = p.ProtectForHalo,
        c.BonusItemLevel           = p.BonusItemLevel,
        c.BonusItemValue           = p.BonusItemValue,
        c.TribeNotifyScrollCount     = p.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance   = p.TribeFourReturnAllowance,
        c.BottleSlots              = p.BottleSlots,
        c.DrunkBottleIndex         = p.DrunkBottleIndex,
        c.AutoBuffTime             = p.AutoBuffTime,
        c.AutoBuffSkill            = p.AutoBuffSkill,
        c.RankPointDate            = p.RankPointDate,
        c.RankBuffType             = p.RankBuffType,
        c.AutoTime                 = p.AutoTime,
        c.AutoTime2                = p.AutoTime2,
        c.BuffX2Time               = p.BuffX2Time,
        c.PremiumExpireUtc         = p.PremiumExpireUtc,
        c.PetGrowth                = p.PetGrowth,
        c.PetActivity              = p.PetActivity,
        c.ImproveItemValue         = p.ImproveItemValue,
        c.AddItemValue             = p.AddItemValue,
        c.HighItemValue            = p.HighItemValue,
        c.TaiyanKeyTimer           = p.TaiyanKeyTimer,
        c.RankPoint                = p.RankPoint,
        c.CloakLuckyBoxPity        = p.CloakLuckyBoxPity,
        c.CloakVariantBoxPity      = p.CloakVariantBoxPity,
        c.MountVariantBoxPity      = p.MountVariantBoxPity,
        c.ProtectForRefine         = p.ProtectForRefine,
        c.ProtectForDestroy        = p.ProtectForDestroy,
        c.ProtectForCostume        = p.ProtectForCostume,
        c.ProtectForDestroy2       = p.ProtectForDestroy2,
        c.LodRounds                = p.LodRounds,
        c.StellarCoreExpireDate    = p.StellarCoreExpireDate,
        c.EliteDungeonTime         = p.EliteDungeonTime,
        c.DungeonKeyTime           = p.DungeonKeyTime,
        c.IvyHallTicketTime        = p.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = p.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = p.FightingGodForDestroy,
        c.PetBagDate               = p.PetBagDate,
        c.PlayTime1                = p.PlayTime1,
        c.PlayTime3                = p.PlayTime3,
        c.HsbStoneRewardClaimed    = p.HsbStoneRewardClaimed,
        c.TowerCpMilestoneCounter  = p.TowerCpMilestoneCounter,
        c.InventoryDate            = p.InventoryDate,
        c.StoreDate                = p.StoreDate,
        c.WarriorPill              = p.WarriorPill,
        c.WarriorScroll            = p.WarriorScroll,
        c.SilverTime               = p.SilverTime,
        c.GoldTime                 = p.GoldTime,
        c.DoubleKillNumTime        = p.DoubleKillNumTime,
        c.DoubleKillExpTime        = p.DoubleKillExpTime,
        c.DoubleKillNumTime2       = p.DoubleKillNumTime2,
        c.ProtectForDeath          = p.ProtectForDeath,
        c.AnimalDoubleExp          = p.AnimalDoubleExp,
        c.DmgBoost                 = p.DmgBoost,
        c.HPBoost                  = p.HPBoost,
        c.CriBoost                 = p.CriBoost,
        c.MapId                    = q.MapId,
        c.PosX                     = q.PosX,
        c.PosY                     = q.PosY,
        c.PosZ                     = q.PosZ,
        c.Heading                  = q.Heading,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied (CharacterId)
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE p.FlushSequence >= c.FlushSequence;

    DELETE ci
    FROM game.CharacterCostumeSlots AS ci
             JOIN @Applied AS a ON a.CharacterId = ci.CharacterId;

    INSERT INTO game.CharacterCostumeSlots (CharacterId, Slot, ItemId, EnchantValue, ExpireDate)
    SELECT cs.CharacterId,
           cs.Slot,
           cs.ItemId,
           cs.EnchantValue,
           cs.ExpireDate
    FROM @Costumes AS cs
             JOIN @Applied AS a ON a.CharacterId = cs.CharacterId;

    COMMIT TRANSACTION;
END;
