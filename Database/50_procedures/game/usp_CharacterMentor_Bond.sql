-- Both sides update in one transaction: a fault between them must never leave one side bonded, the other not.
-- Preconditions (levels, tribe, not already bonded) are validated by the caller; this proc is the durable write only.
CREATE PROCEDURE game.usp_CharacterMentor_Bond @MasterCharacterId  INT,
    @StudentCharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

UPDATE game.Characters
SET StudentCharacterId = @StudentCharacterId
WHERE CharacterId = @MasterCharacterId;

UPDATE game.Characters
SET TeacherCharacterId = @MasterCharacterId
WHERE CharacterId = @StudentCharacterId;

COMMIT TRANSACTION;
END;
