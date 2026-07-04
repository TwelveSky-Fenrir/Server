-- database/50_procedures/game/usp_TribeSubMaster_Clear.sql
-- Idempotent no-op if the character holds no slot in this tribe (DemandDeleteTribeSubMaster,
-- S04_MyWork02.cpp:10955).
CREATE PROCEDURE game.usp_TribeSubMaster_Clear @TribeId     TINYINT,
    @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.TribeSubMasters
WHERE TribeId = @TribeId
  AND CharacterId = @CharacterId;
END;
