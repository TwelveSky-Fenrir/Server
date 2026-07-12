-- Atomic accumulate for a per-kill hero-point gain (PvP kill reward pipeline). usp_HeroRanking_Upsert
-- replaces Points wholesale and is only safe for a claim-time overwrite (see its own header comment);
-- granting points earned during play must never read-then-write from application code, since a concurrent
-- grant to the same character could be silently clobbered by whichever write lands second. This proc adds
-- @Delta to whatever is already stored for (CharacterId, PeriodKind), seeding the row via INSERT if the
-- character has no row yet for that period. TribeId is refreshed opportunistically (COALESCE keeps the
-- previously stored value when NULL is passed).
--
-- Concurrency fix (2026-07-12 hardening pass, same race class the sql-server-2025-stored-procedure-conventions
-- skill asks to check every "AddPoints"-style accumulator against, using game.usp_WorldStateTribe_AddPoints as
-- the reference-good pattern): the original shape here was a bare "UPDATE; IF @@ROWCOUNT = 0 INSERT" with NO
-- explicit transaction. game.WorldStateTribes' AddPoints is safe because every row is pre-seeded at boot
-- (usp_WorldState_EnsureInitialized) so its UPDATE never needs an INSERT fallback at all -- but
-- game.HeroRankings legitimately has no row until a character's FIRST-ever hero point in a period, and two
-- concurrent first-time grants to the SAME (CharacterId, PeriodKind) (e.g. two kill-reward writes racing on
-- the same shard tick, or reward pipeline retries) could both see "no row" from their own UPDATE, both fall
-- into the INSERT branch, and the second INSERT would fail on PK_HeroRankings -- silently dropping that kill's
-- points rather than accumulating them (exactly the corrupted/lost-value failure mode this hardening pass was
-- asked to rule out). Fixed the same way this schema already guards an analogous "insert vs update" race
-- elsewhere (usp_GuildMember_Add's member-count check, usp_HeroRanking_Rollover's singleton-row check): an
-- explicit BEGIN TRANSACTION wrapping a WITH (UPDLOCK, HOLDLOCK) existence check on the exact
-- PK_HeroRankings (CharacterId, PeriodKind) key. HOLDLOCK (=SERIALIZABLE, verified via Microsoft Learn's
-- "Table hints (Transact-SQL)"/"Transaction locking and row versioning guide" pages) takes a key-range lock on
-- that specific composite-key value, so a second concurrent caller for the SAME key blocks until the first
-- commits instead of racing into a duplicate INSERT; callers for a DIFFERENT (CharacterId, PeriodKind) are
-- untouched and never block each other -- this is a per-key lock, not a table-wide one, so normal per-kill
-- throughput across many different characters is unaffected.
--
-- Level is deliberately NOT refreshed on the UPDATE branch: legacy's AddOrUpdateHeroRank
-- (Server/ts25center/S08_MyDB.cpp:213-216, `... ON DUPLICATE KEY UPDATE hTribe=%d,hPoint=hPoint+%d;`) omits
-- hLevel from its ON DUPLICATE KEY UPDATE clause entirely, so once a hero-rank row exists for a character,
-- its Level column is frozen forever at whatever level the character was when they first ever earned a hero
-- point for that ranking period. @Level is still accepted as a parameter for the INSERT branch (first-ever
-- grant, or first grant after a period rollover/reset re-creates the row).
CREATE PROCEDURE game.usp_HeroRanking_AddPoints @CharacterId INT,
                                                @PeriodKind TINYINT,
                                                @Delta INT,
                                                @TribeId TINYINT = NULL,
                                                @Level SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1
                   FROM game.HeroRankings WITH (UPDLOCK, HOLDLOCK)
                   WHERE CharacterId = @CharacterId
                     AND PeriodKind = @PeriodKind)
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Delta, @TribeId, @Level, SYSUTCDATETIME());
    ELSE
        UPDATE game.HeroRankings
        SET Points        = Points + @Delta,
            TribeId       = COALESCE(@TribeId, TribeId),
            RecordedAtUtc = SYSUTCDATETIME()
        WHERE CharacterId = @CharacterId
          AND PeriodKind = @PeriodKind;

    COMMIT TRANSACTION;

    SELECT Points
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind;
END;
