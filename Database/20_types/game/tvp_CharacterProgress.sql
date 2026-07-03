-- Read-only shape for usp_Character_PersistProgressBatch's TVP parameter (write-behind regime (a) of
-- D7: xp/level/vitals/points ride the 5 s DirtyTracker flush; money does NOT -- money moves only through
-- the transactional usp_Character_AdjustMoney, regime (b), so a lost flush can never lose value).
-- One row per dirty character. FlushSequence is the SAME per-character monotonic counter the position
-- flush uses (game.Characters.FlushSequence): the write-behind host mints one sequence per character
-- for all its flavors of dirty state, and both PersistBatch procs apply the identical strictly-greater
-- guard, so replays/out-of-order deliveries of either batch are silent no-ops.
-- No PK/index: passed by value per call, never stored.
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
