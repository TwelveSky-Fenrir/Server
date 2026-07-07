-- TVP for usp_Character_PersistProgressBatch: xp/level/vitals/points flush (money excluded -- money
-- only moves via the transactional usp_Character_AdjustMoney). Shares game.Characters.FlushSequence
-- with the position flush; strictly-greater guard makes replays/out-of-order deliveries no-ops.
-- Exp2/RebirthCount appended after StatPoints/SkillPoints/ContributionPoints; the 5 Eat*Potion counters
-- (EatLifePotion/EatManaPotion/EatStrPotion/EatDexPotion/EatElePotion) are appended last -- stat/elixir-potion
-- consumption increments these in PlayerRuntimeState, and without them here the write-behind flush would
-- silently revert a live counter back to its last-persisted value on the next flush cycle.
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
    EatElePotion       INT      NOT NULL
);
