-- Additive progression/economy migration for game.Characters (Server Logic chapter, phase A3).
-- game.Characters shipped with the minimal M1 columns (identity + position + vitals) only; every
-- gameplay vertical (XP, base stats, money, rebirth, elixirs, protection charges, timed boosts) needs
-- persisted progression state. Separate script instead of editing Characters.sql: the migrator journal
-- (admin.SchemaVersions) forbids changing an applied script's content -- additive ALTER is the sanctioned
-- corrective path (_manifest.txt header).
-- Legacy mapping (wAvatar/AVATAR_INFO, STRUCT.h -- mirrors Core/Fenrir.Contracts/Packets/Shared/AvatarInfo.cs):
--   Experience          <- aExp1/aExp2: legacy splits XP across two ints; stored as ONE BIGINT here, the
--                          wire split back into Exp1/Exp2 is app-side presentation, not storage truth.
--   Level2              <- aLevel2 (martial/battle level; aLevel1 is the existing Level column).
--   StatVit/Str/Int/Dex <- aVit/aStr/aInt/aDex (spent base stat points; derived stats are recomputed by
--                          StatCalculator, never persisted -- D8).
--   StatPoints          <- aStatPoint; SkillPoints <- aSkillPoint.
--   Money/BigMoney      <- aMoney/aBigMoney: aMoney is the sub-billion pool, aBigMoney the billions
--                          overflow counter (CheckOverMaximum caps). Money is BIGINT for headroom, but
--                          the legacy per-pool caps stay enforced app-side; the CHECKs here only pin the
--                          non-negative invariant that usp_Character_AdjustMoney's guard relies on.
--   StoreMoney/BigStoreMoney <- aStoreMoney/aBigStoreMoney (personal-warehouse money pools -- the Store
--                          container lives in game.CharacterItems, its money lives here).
--   RebirthCount        <- aRebirthNum (MAX_REBIRTH_LIMIT = 12, enforced app-side).
--   Title/Halo          <- aTitle/aHalo (cosmetic progression unlocks).
--   ContributionPoints  <- aKillOtherTribe (CP -- quest reward type 3 pays into it, report 04 §5).
--   EatLifePotion/EatManaPotion/EatStrPotion/EatDexPotion/EatElePotion <- aEat*Potion permanent-elixir
--                          counters; legacy caps MAX_ELIXIRS_M33 = 200 then MAX_ELIXIRS_G12 = 400
--                          (report 04) are consumption-time app rules, the CHECK here is the 0..400
--                          absolute backstop.
--   ProtectForDeath/ProtectForDestroy <- aProtectForDeath/aProtectForDestroy (consumable protection
--                          charges: XP-loss-on-death / item-destruction-on-improve).
--   DoubleExpTime1/DoubleExpTime2/DropItemTime <- timed-boost expirations, legacy int YYYYMMDD dates
--                          (ReturnNowDate, datetime.h) -- stored verbatim, 0 = no boost (the legacy
--                          0-is-absent idiom, same convention as CharacterItems.ExpireDate).
--   InventoryDate       <- aInventoryDate (bonus inventory page rental, int YYYYMMDD, checked on every
--                          last-page operation -- report 12 §3).
--   StoreDate           <- aStoreDate (warehouse rental date, same format).
-- Deliberately NOT columns here: per-slot state (items/skills/quests/hotkeys/buffs -> their own child
-- tables), transient trade state (never persisted, matching legacy), account-scope state (vault/cash ->
-- game.AccountVault/game.AccountCash), and the long tail of event/zone counters in AVATAR_INFO -- those
-- ride with their gameplay verticals (Phase C) as further additive migrations, not speculatively here.
ALTER TABLE game.Characters
    ADD Experience BIGINT NOT NULL CONSTRAINT DF_Characters_Experience DEFAULT 0,
        Level2             SMALLINT NOT NULL CONSTRAINT DF_Characters_Level2 DEFAULT 1,
        StatVit            INT      NOT NULL CONSTRAINT DF_Characters_StatVit DEFAULT 0,
        StatStr            INT      NOT NULL CONSTRAINT DF_Characters_StatStr DEFAULT 0,
        StatInt            INT      NOT NULL CONSTRAINT DF_Characters_StatInt DEFAULT 0,
        StatDex            INT      NOT NULL CONSTRAINT DF_Characters_StatDex DEFAULT 0,
        StatPoints         INT      NOT NULL CONSTRAINT DF_Characters_StatPoints DEFAULT 0,
        SkillPoints        INT      NOT NULL CONSTRAINT DF_Characters_SkillPoints DEFAULT 0,
        Money              BIGINT   NOT NULL CONSTRAINT DF_Characters_Money DEFAULT 0,
        BigMoney           INT      NOT NULL CONSTRAINT DF_Characters_BigMoney DEFAULT 0,
        StoreMoney         BIGINT   NOT NULL CONSTRAINT DF_Characters_StoreMoney DEFAULT 0,
        BigStoreMoney      INT      NOT NULL CONSTRAINT DF_Characters_BigStoreMoney DEFAULT 0,
        RebirthCount       INT      NOT NULL CONSTRAINT DF_Characters_RebirthCount DEFAULT 0,
        Title              INT      NOT NULL CONSTRAINT DF_Characters_Title DEFAULT 0,
        Halo               INT      NOT NULL CONSTRAINT DF_Characters_Halo DEFAULT 0,
        ContributionPoints INT      NOT NULL CONSTRAINT DF_Characters_ContributionPoints DEFAULT 0,
        EatLifePotion      INT      NOT NULL CONSTRAINT DF_Characters_EatLifePotion DEFAULT 0,
        EatManaPotion      INT      NOT NULL CONSTRAINT DF_Characters_EatManaPotion DEFAULT 0,
        EatStrPotion       INT      NOT NULL CONSTRAINT DF_Characters_EatStrPotion DEFAULT 0,
        EatDexPotion       INT      NOT NULL CONSTRAINT DF_Characters_EatDexPotion DEFAULT 0,
        EatElePotion       INT      NOT NULL CONSTRAINT DF_Characters_EatElePotion DEFAULT 0,
        ProtectForDeath    INT      NOT NULL CONSTRAINT DF_Characters_ProtectForDeath DEFAULT 0,
        ProtectForDestroy  INT      NOT NULL CONSTRAINT DF_Characters_ProtectForDestroy DEFAULT 0,
        DoubleExpTime1     INT      NOT NULL CONSTRAINT DF_Characters_DoubleExpTime1 DEFAULT 0,
        DoubleExpTime2     INT      NOT NULL CONSTRAINT DF_Characters_DoubleExpTime2 DEFAULT 0,
        DropItemTime       INT      NOT NULL CONSTRAINT DF_Characters_DropItemTime DEFAULT 0,
        InventoryDate      INT      NOT NULL CONSTRAINT DF_Characters_InventoryDate DEFAULT 0,
        StoreDate          INT      NOT NULL CONSTRAINT DF_Characters_StoreDate DEFAULT 0;
GO

-- Invariants the transactional money proc (usp_Character_AdjustMoney) and elixir consumption depend on;
-- added after the columns so both batches parse independently (ADD + CHECK on new columns cannot share
-- a batch).
ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_Experience CHECK (Experience >= 0),
        CONSTRAINT CK_Characters_Money CHECK (Money >= 0),
        CONSTRAINT CK_Characters_BigMoney CHECK (BigMoney >= 0),
        CONSTRAINT CK_Characters_StoreMoney CHECK (StoreMoney >= 0),
        CONSTRAINT CK_Characters_BigStoreMoney CHECK (BigStoreMoney >= 0),
        CONSTRAINT CK_Characters_EatLifePotion CHECK (EatLifePotion BETWEEN 0 AND 400),
        CONSTRAINT CK_Characters_EatManaPotion CHECK (EatManaPotion BETWEEN 0 AND 400),
        CONSTRAINT CK_Characters_EatStrPotion CHECK (EatStrPotion BETWEEN 0 AND 400),
        CONSTRAINT CK_Characters_EatDexPotion CHECK (EatDexPotion BETWEEN 0 AND 400),
        CONSTRAINT CK_Characters_EatElePotion CHECK (EatElePotion BETWEEN 0 AND 400),
        CONSTRAINT CK_Characters_ProtectForDeath CHECK (ProtectForDeath >= 0),
        CONSTRAINT CK_Characters_ProtectForDestroy CHECK (ProtectForDestroy >= 0);
