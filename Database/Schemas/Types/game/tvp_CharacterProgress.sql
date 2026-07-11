-- TVP for usp_Character_PersistProgressBatch: xp/level/vitals/points flush (money excluded -- money
-- only moves via the transactional usp_Character_AdjustMoney). Shares game.Characters.FlushSequence
-- with the position flush; strictly-greater guard makes replays/out-of-order deliveries no-ops.
-- Exp2/RebirthCount appended after StatPoints/SkillPoints/ContributionPoints; the 5 Eat*Potion counters
-- (EatLifePotion/EatManaPotion/EatStrPotion/EatDexPotion/EatElePotion) are appended last -- stat/elixir-potion
-- consumption increments these in PlayerRuntimeState, and without them here the write-behind flush would
-- silently revert a live counter back to its last-persisted value on the next flush cycle.
-- DropItemTime is appended last of all (follow-up to the item-usage-consumables finding): the Lucky Drop/
-- "Acquisition" Scroll minutes counter, already persisted by game.Characters.DropItemTime and already mirrored
-- into PlayerRuntimeState.DropItemTime (Zone.EconomyMirrors' ApplyTribeProgressCommand), but never previously
-- flushed back -- without it here the write-behind flush would silently revert a live counter back to its
-- last-persisted value on the next flush cycle, same gap the Eat*Potion counters closed above.
-- M15PetLuckyBoxPity (Migrations/048, confirmation-pass follow-up): the M15 Pet Lucky Box (world.Items 8111)
-- pity counter -- box-open consumption increments/resets this in PlayerRuntimeState.M15PetLuckyBoxPity, same
-- "without it here the flush would silently revert it" reasoning as DropItemTime/Eat*Potion above.
CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId        INT      NOT NULL,
    FlushSequence      BIGINT   NOT NULL,
    Level              SMALLINT NOT NULL,
    Level2             SMALLINT NOT NULL,
    Experience         BIGINT   NOT NULL,
    Life               INT      NOT NULL,
    MaxLife            INT      NOT NULL,
    Mana               INT      NOT NULL,
    MaxMana            INT      NOT NULL,
    StatVit            INT      NOT NULL,
    StatStr            INT      NOT NULL,
    StatInt            INT      NOT NULL,
    StatDex            INT      NOT NULL,
    StatPoints         INT      NOT NULL,
    SkillPoints        INT      NOT NULL,
    ContributionPoints INT      NOT NULL,
    Exp2               INT      NOT NULL,
    RebirthCount       INT      NOT NULL,
    EatLifePotion      INT      NOT NULL,
    EatManaPotion      INT      NOT NULL,
    EatStrPotion       INT      NOT NULL,
    EatDexPotion       INT      NOT NULL,
    EatElePotion       INT      NOT NULL,
    DropItemTime       INT      NOT NULL,
    M15PetLuckyBoxPity INT      NOT NULL
);
