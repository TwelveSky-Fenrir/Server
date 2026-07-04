-- TVP for usp_Character_PersistProgressBatch: xp/level/vitals/points flush (money excluded -- money
-- only moves via the transactional usp_Character_AdjustMoney). Shares game.Characters.FlushSequence
-- with the position flush; strictly-greater guard makes replays/out-of-order deliveries no-ops.
CREATE TYPE game.tvp_CharacterProgress AS TABLE
    (
    CharacterId INT NOT NULL,
    FlushSequence BIGINT NOT NULL,
    Level SMALLINT NOT NULL,
    Level2 SMALLINT NOT NULL,
    Experience BIGINT NOT NULL,
    Life INT NOT NULL,
    MaxLife INT NOT NULL,
    Mana INT NOT NULL,
    MaxMana INT NOT NULL,
    StatVit INT NOT NULL,
    StatStr INT NOT NULL,
    StatInt INT NOT NULL,
    StatDex INT NOT NULL,
    StatPoints INT NOT NULL,
    SkillPoints INT NOT NULL,
    ContributionPoints INT NOT NULL
    );
