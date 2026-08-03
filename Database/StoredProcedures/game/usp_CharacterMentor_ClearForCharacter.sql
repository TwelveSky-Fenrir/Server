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
