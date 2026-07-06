-- Login-side counterpart of legacy SetIntegerLow(aLogoutInfo[4], 1) / SetIntegerLow(aLogoutInfo[5], 0)
-- (Server/Header/function.h:242-245, applied at Server/ts25login/S04_MyWork02.cpp:357-358): floors a
-- character's stored Life to at least 1 and Mana to at least 0. Idempotent on the same per-character
-- FlushSequence guard as usp_Character_PersistBatch/usp_Character_PersistProgressBatch (one shared
-- monotonic counter), so a retried/duplicated call can never regress a more recent write. Deliberately a
-- narrow two-column update (not routed through usp_Character_PersistProgressBatch) -- ZoneTransferService
-- only ever holds CharacterWorldEntryDto's narrow prefix, not the full stat/progression row, and this floor
-- clamp must never overwrite Stat*/SkillPoints/Experience with stale zeros.
CREATE PROCEDURE game.usp_Character_ClampVitalsFloor @CharacterId INT,
    @FlushSequence BIGINT,
    @Life          INT,
    @Mana          INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET Life          = @Life,
        Mana          = @Mana,
        FlushSequence = @FlushSequence,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND @FlushSequence > FlushSequence;
END;
