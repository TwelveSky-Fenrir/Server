-- Merges legacy `herorankcur`/`herorankpre` (nxtserver.sql) into one table with a period marker instead of
-- two near-identical tables: PeriodKind 0 = Current (herorankcur), 1 = Previous (herorankpre). The legacy
-- sentinel row in herorankpre (uCharIdx=0, hDesc='Dont Delete this row', hTribe=-1) was an old data-access-
-- layer hack, not real data, and is not ported -- there is nothing to seed here anyway (starts empty).
-- herorankcur has no hDate/hAccept/hDesc columns of its own (only uCharIdx/hPoint/hTribe/hLevel), so
-- RewardClaimed/Description are meaningful only once a period rolls from Current to Previous; both are
-- NULLable rather than widened with a fake default, matching that real legacy asymmetry.
-- Direct inspection of Protocol/STRUCT.h's RANK_INFO shows the actual runtime ranking is computed PER
-- TRIBE (HERO_RANK.hPoint[4][MAX_HERO_RANK_AVATAR_NUM=10], i.e. a top-10-per-tribe leaderboard slotted by
-- tribe+rank-slot) -- but both MySQL persistence tables are already flattened to one row per qualifying
-- character (PK uCharIdx), which is exactly the shape this table mirrors; the richer slot-indexed in-memory
-- struct is a runtime/ranking-computation concern the application layer owns, not a persistence shape.
-- PLAIN (not memory-optimized) table, deliberately: this table needs enforced FOREIGN KEYs to two regular
-- disk-based tables (game.Characters, game.Tribes), and SQL Server's In-Memory OLTP only allows a FOREIGN
-- KEY on a memory-optimized table when the referenced table is ALSO memory-optimized (same limitation
-- documented on game.TribeBank/game.GuildNotices) -- making this table memory-optimized would have forced
-- dropping both FKs. Point updates are frequent but not a true per-tick hot path (rank recalculation is a
-- periodic/event-driven batch job, not a per-hit write), so the regular-table tradeoff is a good fit.
-- Starts empty (no existing players yet) -- schema only, no seed, per this domain's brief.
CREATE TABLE game.HeroRankings
(
    CharacterId   INT     NOT NULL,
    PeriodKind    TINYINT NOT NULL,
    Points        INT     NOT NULL CONSTRAINT DF_HeroRankings_Points DEFAULT 0,
    TribeId       TINYINT NULL,
    Level         INT NULL,
    RewardClaimed BIT NULL,
    Description   NVARCHAR(255) NULL,
    RecordedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_HeroRankings_RecordedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_HeroRankings PRIMARY KEY CLUSTERED (CharacterId, PeriodKind),
    CONSTRAINT CK_HeroRankings_PeriodKind CHECK (PeriodKind IN (0, 1)),
    CONSTRAINT FK_HeroRankings_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_HeroRankings_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    INDEX         IX_HeroRankings_Period_Points NONCLUSTERED (PeriodKind, Points DESC)
);
