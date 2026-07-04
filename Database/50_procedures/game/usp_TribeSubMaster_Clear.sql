-- database/50_procedures/game/usp_TribeSubMaster_Clear.sql
-- Contract: remove one character's tribe sub-master slot (DemandDeleteTribeSubMaster, doc 10 §2 tSort 3
-- -- S04_MyWork02.cpp:10955).
-- Params:
--   @TribeId     TINYINT
--   @CharacterId INT
-- Result set: none.
-- Idempotent: yes -- clearing a character who does not currently hold a slot in this tribe is a silent
-- no-op, same posture as usp_GuildMember_Remove (a concurrent double-clear must not throw).
-- Errors: none.
CREATE PROCEDURE game.usp_TribeSubMaster_Clear @TribeId     TINYINT,
    @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE FROM game.TribeSubMasters
    WHERE TribeId = @TribeId
      AND CharacterId = @CharacterId;
END;
