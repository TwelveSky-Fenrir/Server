CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                         @Costumes game.tvp_CharacterCostumeSlot READONLY,
                                                         @Mounts game.tvp_CharacterMountSlot READONLY,
                                                         @StellarCores game.tvp_CharacterStellarCoreSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE
                     (
                         CharacterId INT NOT NULL PRIMARY KEY
                     );

    BEGIN TRANSACTION;

    UPDATE c
    SET c.ProgressFlushSequence    = s.FlushSequence,
        c.Level                    = s.Level,
        c.Level2                   = s.Level2,
        c.Experience               = s.Experience,
        c.Life                     = s.Life,
        c.MaxLife                  = s.MaxLife,
        c.Mana                     = s.Mana,
        c.MaxMana                  = s.MaxMana,
        c.StatVit                  = s.StatVit,
        c.StatStr                  = s.StatStr,
        c.StatInt                  = s.StatInt,
        c.StatDex                  = s.StatDex,
        c.StatPoints               = s.StatPoints,
        c.SkillPoints              = s.SkillPoints,
        c.ContributionPoints       = s.ContributionPoints,
        c.Exp2                     = s.Exp2,
        c.RebirthCount             = s.RebirthCount,
        c.EatLifePotion            = s.EatLifePotion,
        c.EatManaPotion            = s.EatManaPotion,
        c.EatStrPotion             = s.EatStrPotion,
        c.EatDexPotion             = s.EatDexPotion,
        c.EatElePotion             = s.EatElePotion,
        c.DropItemTime             = s.DropItemTime,
        c.M15PetLuckyBoxPity       = s.M15PetLuckyBoxPity,
        c.MountItemId              = s.MountItemId,
        c.MountExpActivity         = s.MountExpActivity,
        c.MountPower               = s.MountPower,
        c.MountSlotIndex           = s.MountSlotIndex,
        c.MountTime                = s.MountTime,
        c.VisibleState             = s.VisibleState,
        c.SpecialState             = s.SpecialState,
        c.UseOrnament              = s.UseOrnament,
        c.Title                    = s.Title,
        c.Halo                     = s.Halo,
        c.TeacherPoint             = s.TeacherPoint,
        c.WarPoint                 = c.WarPoint + s.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + s.BloodCoinDelta,
        c.PetExpX2Time             = s.PetExpX2Time,
        c.AnimalAbsorbTime         = s.AnimalAbsorbTime,
        c.AnimalAbsorbState        = s.AnimalAbsorbState,
        c.CostumeIndex             = s.CostumeIndex,
        c.StellarCoreIndex         = s.StellarCoreIndex,
        c.ProtectForHalo           = s.ProtectForHalo,
        c.BonusItemLevel           = s.BonusItemLevel,
        c.BonusItemValue           = s.BonusItemValue,
        c.TribeNotifyScrollCount   = s.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = s.TribeFourReturnAllowance,
        c.BottleSlots              = s.BottleSlots,
        c.DrunkBottleIndex         = s.DrunkBottleIndex,
        c.AutoBuffTime             = s.AutoBuffTime,
        c.AutoBuffSkill            = s.AutoBuffSkill,
        c.RankPointDate            = s.RankPointDate,
        c.RankBuffType             = s.RankBuffType,
        c.AutoTime                 = s.AutoTime,
        c.AutoTime2                = s.AutoTime2,
        c.BuffX2Time               = s.BuffX2Time,
        c.PremiumExpireUtc         = s.PremiumExpireUtc,
        c.PetGrowth                = s.PetGrowth,
        c.PetActivity              = s.PetActivity,
        c.ImproveItemValue         = s.ImproveItemValue,
        c.AddItemValue             = s.AddItemValue,
        c.HighItemValue            = s.HighItemValue,
        c.TaiyanKeyTimer           = s.TaiyanKeyTimer,
        c.RankPoint                = s.RankPoint,
        c.CloakLuckyBoxPity        = s.CloakLuckyBoxPity,
        c.CloakVariantBoxPity      = s.CloakVariantBoxPity,
        c.MountVariantBoxPity      = s.MountVariantBoxPity,
        c.ProtectForRefine         = s.ProtectForRefine,
        c.ProtectForDestroy        = s.ProtectForDestroy,
        c.ProtectForCostume        = s.ProtectForCostume,
        c.ProtectForDestroy2       = s.ProtectForDestroy2,
        c.LodRounds                = s.LodRounds,
        c.StellarCoreExpireDate    = s.StellarCoreExpireDate,
        c.EliteDungeonTime         = s.EliteDungeonTime,
        c.DungeonKeyTime           = s.DungeonKeyTime,
        c.IvyHallTicketTime        = s.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = s.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = s.FightingGodForDestroy,
        c.PetBagDate               = s.PetBagDate,
        c.PlayTime1                = s.PlayTime1,
        c.PlayTime3                = s.PlayTime3,
        c.HsbStoneRewardClaimed    = s.HsbStoneRewardClaimed,
        c.TowerCpMilestoneCounter  = s.TowerCpMilestoneCounter,
        c.InventoryDate            = s.InventoryDate,
        c.StoreDate                = s.StoreDate,
        c.WarriorPill              = s.WarriorPill,
        c.WarriorScroll            = s.WarriorScroll,
        c.SilverTime               = s.SilverTime,
        c.GoldTime                 = s.GoldTime,
        c.DoubleKillNumTime        = s.DoubleKillNumTime,
        c.DoubleKillExpTime        = s.DoubleKillExpTime,
        c.DoubleKillNumTime2       = s.DoubleKillNumTime2,
        c.ProtectForDeath          = s.ProtectForDeath,
        c.AnimalDoubleExp          = s.AnimalDoubleExp,
        c.DmgBoost                 = s.DmgBoost,
        c.HPBoost                  = s.HPBoost,
        c.CriBoost                 = s.CriBoost,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied (CharacterId)
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.ProgressFlushSequence;

    DELETE ci
    FROM game.CharacterCostumeSlots AS ci
             JOIN @Applied AS a ON a.CharacterId = ci.CharacterId;

    INSERT INTO game.CharacterCostumeSlots (CharacterId, Slot, ItemId, ItemValue, ExpireDate)
    SELECT cs.CharacterId,
           cs.Slot,
           cs.ItemId,
           cs.ItemValue,
           cs.ExpireDate
    FROM @Costumes AS cs
             JOIN @Applied AS a ON a.CharacterId = cs.CharacterId;

    DELETE cm
    FROM game.CharacterMounts AS cm
             JOIN @Applied AS a ON a.CharacterId = cm.CharacterId;

    INSERT INTO game.CharacterMounts (CharacterId, Slot, ItemId, ExpActivity, Power)
    SELECT ms.CharacterId,
           ms.Slot,
           ms.ItemId,
           ms.ExpActivity,
           ms.Power
    FROM @Mounts AS ms
             JOIN @Applied AS a ON a.CharacterId = ms.CharacterId;

    DELETE cc
    FROM game.CharacterStellarCoreSlots AS cc
             JOIN @Applied AS a ON a.CharacterId = cc.CharacterId;

    INSERT INTO game.CharacterStellarCoreSlots (CharacterId, Slot, ItemId)
    SELECT sc.CharacterId,
           sc.Slot,
           sc.ItemId
    FROM @StellarCores AS sc
             JOIN @Applied AS a ON a.CharacterId = sc.CharacterId;

    COMMIT TRANSACTION;
END;
