-- Clears only the calling character's own pointer, not the partner's opposite pointer -- a documented
-- legacy asymmetry, not a bug to fix; the partner must separately end their own side.
CREATE PROCEDURE game.usp_CharacterMentor_ClearForCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Characters
SET TeacherCharacterId = NULL,
    StudentCharacterId = NULL
WHERE CharacterId = @CharacterId;
END;
