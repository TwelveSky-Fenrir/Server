-- database/50_procedures/game/usp_TribeSubMaster_Set.sql
-- Contract: appoint one character to one tribe sub-master slot (DemandInsertTribeSubMaster, doc 10 §2
-- tSort 2 -- S04_MyWork02.cpp:10859). The caller (TribeActionHandler) has already re-derived the legacy's
-- own zone/role/level/CP/free-slot checks against live state; this proc's own guards are the LAST-RESORT
-- backstop against a concurrent racer, same philosophy as usp_GuildMember_Add's member-count gate.
-- Params:
--   @TribeId     TINYINT -- game.Tribes.TribeId (0-3)
--   @SlotIndex   TINYINT -- 0-11 (MAX_TRIBE_SUBMASTER_NUM), the specific free slot the caller already found
--   @CharacterId INT
-- Result set: none.
-- Idempotent: no (a repeat call for an already-occupied slot, or a character already holding a DIFFERENT
-- slot in this tribe, throws rather than silently duplicating -- see game.TribeSubMasters' own
-- UQ_TribeSubMasters_Tribe_Character).
-- Errors:
--   THROW 50310 -- @SlotIndex is already occupied (by anyone) in this tribe.
--   THROW 50311 -- @CharacterId already holds a (different) sub-master slot in this tribe.
CREATE PROCEDURE game.usp_TribeSubMaster_Set @TribeId     TINYINT,
    @SlotIndex   TINYINT,
    @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @TribeId AND SlotIndex = @SlotIndex)
        THROW 50310, N'Tribe sub-master slot is already occupied.', 1;

    IF
EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @TribeId AND CharacterId = @CharacterId)
        THROW 50311, N'Character already holds a sub-master slot in this tribe.', 1;

INSERT INTO game.TribeSubMasters (TribeId, SlotIndex, CharacterId)
VALUES (@TribeId, @SlotIndex, @CharacterId);
END;
