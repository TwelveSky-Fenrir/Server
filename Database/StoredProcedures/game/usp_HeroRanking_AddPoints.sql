-- Atomic accumulate for a per-kill hero-point gain (PvP kill reward pipeline). usp_HeroRanking_Upsert
-- replaces Points wholesale and is only safe for a claim-time overwrite (see its own header comment);
-- granting points earned during play must never read-then-write from application code, since a concurrent
-- grant to the same character could be silently clobbered by whichever write lands second. This proc adds
-- @Delta to whatever is already stored for (CharacterId, PeriodKind) in one atomic UPDATE, seeding the row
-- via INSERT if the character has no row yet for that period. TribeId/Level are refreshed opportunistically
-- (COALESCE keeps the previously stored value when NULL is passed) since neither is central to the
-- accumulate itself. Returns the new total so the caller's in-memory mirror can stay authoritative without
-- a second round trip.
CREATE PROCEDURE game.usp_HeroRanking_AddPoints @CharacterId INT,
    @PeriodKind TINYINT,
    @Delta      INT,
    @TribeId    TINYINT = NULL,
    @Level      INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.HeroRankings
SET Points        = Points + @Delta,
    TribeId       = COALESCE(@TribeId, TribeId),
    Level         = COALESCE(@Level, Level),
    RecordedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId
  AND PeriodKind = @PeriodKind;

IF
@@ROWCOUNT = 0
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Delta, @TribeId, @Level, SYSUTCDATETIME());

SELECT Points
FROM game.HeroRankings
WHERE CharacterId = @CharacterId
  AND PeriodKind = @PeriodKind;
END;
